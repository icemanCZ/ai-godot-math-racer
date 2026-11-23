using System.Collections.Generic;
using Godot;

namespace MathRacer.Graphics;

/// <summary>
/// Draws the plain parts of a building that never change as a few meshes instead of hundreds. Every part is a mesh of its
/// own (<see cref="Art.Block"/>, <see cref="Art.Ball"/>, <see cref="Art.Cylinder"/>), and the renderer of the
/// compatibility driver draws each mesh separately, in every pass: the village was thousands of draw calls.
/// </summary>
/// <remarks>
/// The parts that are merged are those of one parent node with the same material, outline, shadow and visibility range:
/// the merged mesh takes their place under that parent, so whatever shows, hides, moves or scales the parent still does
/// so to them. A part is left alone when it has a name (code finds it by it), children, a script, more than one surface,
/// a see-through material or a surface material of its own. Only call this for parts that no code keeps to change
/// later on its own: a merged part is freed.
/// </remarks>
internal static class StaticMeshes
{
    private readonly record struct Look(
        Node3D Parent,
        Material Material,
        Material? Overlay,
        GeometryInstance3D.ShadowCastingSetting Shadow,
        float RangeEnd,
        float RangeEndMargin
    );

    /// <summary>
    /// Merges the plain parts under <paramref name="root"/>, leaving out whatever is under a node <paramref name="skip"/>
    /// names (characters and animals move their parts themselves); <paramref name="prepare"/> gives each part its range
    /// and shadow first, so that the parts merged together have the same. Returns how many parts it freed.
    /// </summary>
    internal static int Merge(Node3D root, System.Func<Node, bool> skip, System.Action<MeshInstance3D> prepare)
    {
        var groups = new Dictionary<Look, List<MeshInstance3D>>();
        Collect(root, groups, skip, prepare);
        int freed = 0;
        foreach (var (look, parts) in groups)
        {
            if (parts.Count < 2)
                continue;
            var surface = new SurfaceTool();
            foreach (var part in parts)
                surface.AppendFrom(part.Mesh, 0, part.Transform);
            var mesh = surface.Commit();
            surface.Dispose();
            var merged = new MeshInstance3D
            {
                Name = "Merged",
                Mesh = mesh,
                MaterialOverride = look.Material,
                MaterialOverlay = look.Overlay,
                CastShadow = look.Shadow,
                VisibilityRangeEnd = look.RangeEnd,
                VisibilityRangeEndMargin = look.RangeEndMargin,
            };
            // Before the first part, so that the parent's children keep their order among themselves.
            look.Parent.AddChild(merged);
            look.Parent.MoveChild(merged, parts[0].GetIndex());
            foreach (var part in parts)
            {
                look.Parent.RemoveChild(part);
                part.QueueFree();
                freed++;
            }
        }
        return freed;
    }

    private static void Collect(
        Node node,
        Dictionary<Look, List<MeshInstance3D>> groups,
        System.Func<Node, bool> skip,
        System.Action<MeshInstance3D> prepare
    )
    {
        if (skip(node))
            return;
        if (node is MeshInstance3D mesh && Plain(mesh) && mesh.GetParent() is Node3D parent)
        {
            prepare(mesh);
            var look = new Look(
                parent,
                mesh.MaterialOverride!,
                mesh.MaterialOverlay,
                mesh.CastShadow,
                mesh.VisibilityRangeEnd,
                mesh.VisibilityRangeEndMargin
            );
            if (!groups.TryGetValue(look, out var parts))
                groups[look] = parts = new List<MeshInstance3D>();
            parts.Add(mesh);
            return;
        }
        foreach (var child in node.GetChildren())
            Collect(child, groups, skip, prepare);
    }

    private static bool Plain(MeshInstance3D mesh) =>
        mesh.GetType() == typeof(MeshInstance3D)
        && mesh.Name.ToString().StartsWith('@')
        && mesh.GetChildCount() == 0
        && mesh.Visible
        && mesh.Mesh is { } shape
        && shape.GetSurfaceCount() == 1
        && mesh.GetSurfaceOverrideMaterial(0) == null
        && mesh.MaterialOverride is { } material
        && material is not BaseMaterial3D { Transparency: not BaseMaterial3D.TransparencyEnum.Disabled }
        && mesh.VisibilityRangeBegin == 0
        && mesh.GetScript().VariantType == Variant.Type.Nil;
}
