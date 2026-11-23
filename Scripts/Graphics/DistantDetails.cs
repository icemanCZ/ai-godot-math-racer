using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace MathRacer.Graphics;

/// <summary>
/// Stops drawing the small single meshes of the valley (a door knob, a fence post, a toy in a yard) once they are so far
/// that they would cover only three or four pixels. The valley has some ten thousand of them, each its own draw call
/// wherever it is on screen, which made the village cost the processor more than anything else. Every mesh under the
/// watched node gets a visibility range from its size when it appears, the ones added during play too.
/// </summary>
/// <remarks>
/// Left alone: meshes that already have a range, glowing, unshaded or see-through ones and those with a shader of their
/// own (a lit window or a lamp is seen from afar at night), and whatever <c>excluded</c> names (the sky, the weather).
/// The size counts a parent's scale only where it enlarges: much of the valley grows from nothing or is kept tiny until it
/// is needed (goods at a works), and such a part must keep the range of its full size.
/// </remarks>
internal sealed class DistantDetails
{
    /// <summary>The range for each metre of size: a part is about 3.6 pixels across when it goes (1080p, 62° view).</summary>
    private const float RangePerMetre = 250;

    /// <summary>No part goes nearer than this, however small.</summary>
    private const float MinimumRange = 60;

    /// <summary>A part that would stay longer than this crosses the valley anyway and keeps no range.</summary>
    private const float LongestRange = 450;

    /// <summary>A part smaller than this (metres) casts no shadow.</summary>
    private const float SmallestShadow = .5f;

    /// <summary>How long a frame may spend giving ranges; a new valley has thousands of parts to go through.</summary>
    private const double MillisecondsPerFrame = 1.5;

    private readonly Queue<MeshInstance3D> waiting = new();
    private Node root = null!;
    private Func<Node, bool> excluded = null!;

    internal int Ranged { get; private set; }
    internal int Waiting => waiting.Count;

    internal void Watch(Node root, Func<Node, bool> excluded)
    {
        this.root = root;
        this.excluded = excluded;
        foreach (var node in root.FindChildren("*", nameof(MeshInstance3D), true, false))
            waiting.Enqueue((MeshInstance3D)node);
        var tree = root.GetTree();
        tree.NodeAdded += Added;
        // A new game frees the valley and keeps the tree, which must stop calling this one.
        root.TreeExiting += () =>
        {
            tree.NodeAdded -= Added;
            waiting.Clear();
        };
    }

    /// <summary>Looked at in a later tick, once the code that added the part has placed and scaled it.</summary>
    private void Added(Node node)
    {
        if (node is MeshInstance3D mesh)
            waiting.Enqueue(mesh);
    }

    internal void Tick()
    {
        long started = Stopwatch.GetTimestamp();
        int count = waiting.Count;
        for (int i = 0; i < count; i++)
        {
            Range(waiting.Dequeue());
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds > MillisecondsPerFrame)
                break;
        }
    }

    /// <summary>Gives every waiting part its range now. Checks use it so results do not depend on the frame rate.</summary>
    internal void Complete()
    {
        while (waiting.TryDequeue(out var mesh))
            Range(mesh);
    }

    /// <summary>Gives one part its range and shadow at once, before <see cref="StaticMeshes"/> merges it.</summary>
    internal void RangeNow(MeshInstance3D mesh) => Range(mesh);

    private void Range(MeshInstance3D mesh)
    {
        if (!GodotObject.IsInstanceValid(mesh) || !mesh.IsInsideTree() || mesh.VisibilityRangeEnd > 0 || !Plain(mesh))
            return;
        float scale = Largest(mesh.Basis.Scale);
        for (var node = mesh.GetParent(); node != root; node = node.GetParent())
        {
            if (node == null || excluded(node))
                return;
            if (node is Node3D parent)
                scale *= Mathf.Max(1, Largest(parent.Basis.Scale));
        }
        float size = Largest(mesh.GetAabb().Size) * scale;
        // A part this small casts a shadow of a few pixels, yet each one was another draw call in every shadow split.
        if (size < SmallestShadow)
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        float range = Mathf.Max(MinimumRange, size * RangePerMetre);
        if (range > LongestRange)
            return;
        mesh.VisibilityRangeEnd = range;
        mesh.VisibilityRangeEndMargin = 5;
        Ranged++;
    }

    private static float Largest(Vector3 v) => Mathf.Max(Mathf.Abs(v.X), Mathf.Max(Mathf.Abs(v.Y), Mathf.Abs(v.Z)));

    /// <summary>Whether every surface has a plain lit, opaque material that does not glow.</summary>
    private static bool Plain(MeshInstance3D mesh)
    {
        if (mesh.Mesh == null)
            return false;
        for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
        {
            var material = mesh.GetActiveMaterial(surface);
            if (material == null)
                continue;
            if (
                material is not StandardMaterial3D standard
                || standard.EmissionEnabled
                || standard.ShadingMode != BaseMaterial3D.ShadingModeEnum.PerPixel
                || standard.Transparency != BaseMaterial3D.TransparencyEnum.Disabled
            )
                return false;
        }
        return true;
    }
}
