using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Godot;
using MathRacer.Garage;
using MathRacer.Valley;

namespace MathRacer.Vehicles;

/// <summary>Bounded, world-space tyre ribbons, sampled by distance rather than speed.</summary>
public partial class WheelTracks : MeshInstance3D
{
    private const float Lifetime = 25;

    // At most two segments per physics tick: retain the whole fade interval
    // even at full speed instead of abruptly deleting still-visible tracks.
    private const int Capacity = 4096;

    private const int VerticesPerSegment = 6;

    /// <summary>The birth time of a slot that holds no segment yet: the shader has long faded it out.</summary>
    private const float NeverBorn = -1000;

    private static readonly StringName TrackTime = "track_time";

    private readonly record struct Segment(
        Vector3 A,
        Vector3 B,
        Vector3 C,
        Vector3 D,
        float Along,
        float Length,
        float Born
    );

    private readonly Queue<Segment> segments = new();

    // A ring of Capacity segments made once: a new segment overwrites the oldest slot and only its six vertices go to
    // the engine. Rebuilding the whole ribbon for every new segment made most of the game's garbage while driving. An
    // old segment needs no removing, the shader fades it out by its birth time (UV2) when its lifetime is over.
    private readonly ArrayMesh ribbon = new();
    private byte[] vertexBytes = [];
    private byte[] attributeBytes = [];
    private int vertexStride;
    private int attributeStride;
    private int positionOffset;
    private int uvOffset;
    private int birthOffset;
    private int nextSlot;
    private readonly ShaderMaterial material = new() { Shader = GD.Load<Shader>("res://Shaders/WheelTracks.gdshader") };
    private readonly Vector3[] previous = new Vector3[2];
    private readonly Vector3[] previousSide = new Vector3[2];
    private readonly float[] along = new float[2];
    private bool connected;
    private TrailStamps? stamps;
    private float time;
    internal int SegmentCount => segments.Count;
    internal float DistanceRecorded => along[0] + along[1];
    internal float OldestAge => segments.Count == 0 ? 0 : time - segments.Peek().Born;

    internal int HiddenSamples(Terrain terrain, Roads roads)
    {
        int hidden = 0;
        foreach (var s in segments)
            hidden += Hidden(s.A, s.B, s.C) + Hidden(s.B, s.D, s.C);
        return hidden;

        int Hidden(Vector3 a, Vector3 b, Vector3 c)
        {
            int count = 0;
            for (int u = 0; u <= 8; u++)
            for (int v = 0; v <= 8 - u; v++)
            {
                var p = a + (b - a) * (u / 8f) + (c - a) * (v / 8f);
                if (p.Y < roads.SurfaceHeight(p, terrain.Height(p.X, p.Z)))
                    count++;
            }
            return count;
        }
    }

    public override void _Ready()
    {
        TopLevel = true;
        GlobalTransform = Transform3D.Identity;
        Mesh = ribbon;
        MaterialOverride = material;
        CastShadow = ShadowCastingSetting.Off;
        // The ring's vertices are written later, so the mesh's own box says nothing about where the tracks are.
        CustomAabb = new Aabb(new Vector3(-1e5f, -1e4f, -1e5f), new Vector3(2e5f, 2e4f, 2e5f));
        CreateRibbon();
    }

    internal void Break() => connected = false;

    internal TrailStamps? Stamps => stamps;

    private TrailStamps AddStamps()
    {
        var node = new TrailStamps { Name = "TrailStamps" };
        AddChild(node);
        return node;
    }

    internal void Update(float dt, Vehicle car, Terrain terrain, Roads roads)
    {
        time += dt;
        material.SetShaderParameter(TrackTime, time);
        // A trail of flowers, hearts or stars from the garage takes the place of the tyre marks.
        stamps ??= AddStamps();
        string trail = car.Appearance.CurrentTrailId;
        stamps.Update(dt, car, terrain, roads, trail);
        if (trail != CarTrail.Plain.Id)
        {
            connected = false;
            if (segments.Count > 0)
            {
                segments.Clear();
                CreateRibbon();
            }
            return;
        }
        while (segments.Count > 0 && time - segments.Peek().Born > Lifetime)
            segments.Dequeue();

        // Actual displacement prevents marks when the accelerator is held against a wall.
        if (!car.IsOnFloor())
            Break();
        else
        {
            var side = car.GlobalBasis.X * (car.Appearance.BigWheels ? .15f : .13f);
            var (rearX, rearZ) = car.Appearance.RearWheel;
            var left = car.ToGlobal(new Vector3(-rearX, 0, rearZ));
            var right = car.ToGlobal(new Vector3(rearX, 0, rearZ));
            bool wet = Submerged(left) || Submerged(right);
            float distance = connected
                ? Mathf.Max(FlatDistance(left, previous[0]), FlatDistance(right, previous[1]))
                : 0;
            if (wet || distance > 3)
                Break();
            else if (!connected || distance >= .25f)
            {
                for (int i = 0; i < 2; i++)
                {
                    var point = i == 0 ? left : right;
                    float length = FlatDistance(point, previous[i]);
                    if (connected)
                    {
                        var a = Project(previous[i] - previousSide[i]);
                        var b = Project(previous[i] + previousSide[i]);
                        var c = Project(point - side);
                        var d = Project(point + side);
                        // A ribbon can cross a raised road edge or terrain crease between
                        // its corners. Keep both triangle interiors above that surface too.
                        float lift = Mathf.Max(Clearance(a, b, c), Clearance(b, d, c));
                        var offset = Vector3.Up * lift;
                        var segment = new Segment(
                            a + offset,
                            b + offset,
                            c + offset,
                            d + offset,
                            along[i],
                            length,
                            time
                        );
                        segments.Enqueue(segment);
                        Write(segment);
                        along[i] += length;
                    }
                    previous[i] = point;
                    previousSide[i] = side;
                }
                connected = true;
            }
        }
        // The slot of the oldest segment has just been written over.
        while (segments.Count > Capacity)
            segments.Dequeue();

        // The river's height profile is not a global sea level: low grassland
        // remains dry even when it is lower than water elsewhere on the map.
        bool Submerged(Vector3 p) =>
            !River.OnBridge(p.X, p.Z)
            && River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 2
            && terrain.Height(p.X, p.Z) < River.Level(p.Z);
        Vector3 Project(Vector3 p)
        {
            return new Vector3(p.X, Surface(p) + .035f, p.Z);
        }
        float Surface(Vector3 p)
        {
            float height = roads.SurfaceHeight(p, terrain.Height(p.X, p.Z));
            return River.OnBridge(p.X, p.Z) ? Mathf.Max(height, River.BridgeSurfaceHeight(p.X)) : height;
        }
        float Clearance(Vector3 a, Vector3 b, Vector3 c)
        {
            float lift = 0;
            for (int u = 0; u <= 4; u++)
            for (int v = 0; v <= 4 - u; v++)
            {
                var p = a + (b - a) * (u / 4f) + (c - a) * (v / 4f);
                lift = Mathf.Max(lift, Surface(p) + .035f - p.Y);
            }
            return lift;
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>Makes the ring with every slot empty; the strides say where each vertex's values lie in the buffers.</summary>
    private void CreateRibbon()
    {
        ribbon.ClearSurfaces();
        int count = Capacity * VerticesPerSegment;
        var births = new Vector2[count];
        Array.Fill(births, new Vector2(NeverBorn, 0));
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[count];
        arrays[(int)Mesh.ArrayType.TexUV] = new Vector2[count];
        arrays[(int)Mesh.ArrayType.TexUV2] = births;
        ribbon.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var format = (RenderingServer.ArrayFormat)ribbon.SurfaceGetFormat(0);
        vertexStride = (int)RenderingServer.MeshSurfaceGetFormatVertexStride(format, count);
        attributeStride = (int)RenderingServer.MeshSurfaceGetFormatAttributeStride(format, count);
        positionOffset = (int)RenderingServer.MeshSurfaceGetFormatOffset(format, count, (int)Mesh.ArrayType.Vertex);
        uvOffset = (int)RenderingServer.MeshSurfaceGetFormatOffset(format, count, (int)Mesh.ArrayType.TexUV);
        birthOffset = (int)RenderingServer.MeshSurfaceGetFormatOffset(format, count, (int)Mesh.ArrayType.TexUV2);
        vertexBytes = new byte[vertexStride * VerticesPerSegment];
        attributeBytes = new byte[attributeStride * VerticesPerSegment];
        nextSlot = 0;
    }

    /// <summary>Writes a new segment over the oldest slot of the ring.</summary>
    private void Write(in Segment s)
    {
        Put(0, s.A, 0, s.Along, s.Born);
        Put(1, s.B, 1, s.Along, s.Born);
        Put(2, s.C, 0, s.Along + s.Length, s.Born);
        Put(3, s.B, 1, s.Along, s.Born);
        Put(4, s.D, 1, s.Along + s.Length, s.Born);
        Put(5, s.C, 0, s.Along + s.Length, s.Born);
        var rid = ribbon.GetRid();
        RenderingServer.MeshSurfaceUpdateVertexRegion(rid, 0, nextSlot * vertexBytes.Length, vertexBytes);
        RenderingServer.MeshSurfaceUpdateAttributeRegion(rid, 0, nextSlot * attributeBytes.Length, attributeBytes);
        nextSlot = (nextSlot + 1) % Capacity;
    }

    private void Put(int vertex, Vector3 p, float across, float distance, float born)
    {
        var position = vertexBytes.AsSpan(vertex * vertexStride + positionOffset);
        BinaryPrimitives.WriteSingleLittleEndian(position, p.X);
        BinaryPrimitives.WriteSingleLittleEndian(position[4..], p.Y);
        BinaryPrimitives.WriteSingleLittleEndian(position[8..], p.Z);
        var attributes = attributeBytes.AsSpan(vertex * attributeStride);
        BinaryPrimitives.WriteSingleLittleEndian(attributes[uvOffset..], across);
        BinaryPrimitives.WriteSingleLittleEndian(attributes[(uvOffset + 4)..], distance);
        BinaryPrimitives.WriteSingleLittleEndian(attributes[birthOffset..], born);
        BinaryPrimitives.WriteSingleLittleEndian(attributes[(birthOffset + 4)..], 0);
    }
}
