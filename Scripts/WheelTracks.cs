using Godot;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Bounded, world-space tyre ribbons, sampled by distance rather than speed.</summary>
public partial class WheelTracks : MeshInstance3D
{
    private const float Lifetime = 25;
    // At most two segments per physics tick: retain the whole fade interval
    // even at full speed instead of abruptly deleting still-visible tracks.
    private const int Capacity = 4096;
    private readonly record struct Segment(Vector3 A, Vector3 B, Vector3 C, Vector3 D, float Along, float Length, float Born);
    private readonly Queue<Segment> segments = new();
    private readonly ImmediateMesh ribbon = new();
    private readonly ShaderMaterial material = new() { Shader = GD.Load<Shader>("res://Shaders/WheelTracks.gdshader") };
    private readonly Vector3[] previous = new Vector3[2];
    private readonly Vector3[] previousSide = new Vector3[2];
    private readonly float[] along = new float[2];
    private bool connected;
    private float time;
    internal int SegmentCount => segments.Count;
    internal float DistanceRecorded => along[0] + along[1];
    internal float OldestAge => segments.Count == 0 ? 0 : time - segments.Peek().Born;
    internal int HiddenSamples(Terrain terrain, Roads roads)
    {
        int hidden = 0;
        var faces = ribbon.GetFaces();
        for (int i = 0; i + 2 < faces.Length; i += 3)
            for (int u = 0; u <= 8; u++)
                for (int v = 0; v <= 8 - u; v++)
                {
                    var p = faces[i] + (faces[i + 1] - faces[i]) * (u / 8f) + (faces[i + 2] - faces[i]) * (v / 8f);
                    if (p.Y < roads.SurfaceHeight(p, terrain.Height(p.X, p.Z))) hidden++;
                }
        return hidden;
    }

    public override void _Ready()
    {
        TopLevel = true;
        GlobalTransform = Transform3D.Identity;
        Mesh = ribbon;
        MaterialOverride = material;
        CastShadow = ShadowCastingSetting.Off;
    }

    internal void Break() => connected = false;

    internal void Update(float dt, Vehicle car, Terrain terrain, Roads roads)
    {
        time += dt;
        material.SetShaderParameter("track_time", time);
        bool dirty = false;
        while (segments.Count > 0 && time - segments.Peek().Born > Lifetime)
        {
            segments.Dequeue(); dirty = true;
        }

        // Actual displacement prevents marks when the accelerator is held against a wall.
        if (!car.IsOnFloor()) Break();
        else
        {
            var side = car.GlobalBasis.X * (car.Appearance.BigWheels ? .15f : .13f);
            var left = car.ToGlobal(new Vector3(-.9875f, 0, .96f));
            var right = car.ToGlobal(new Vector3(.9875f, 0, .96f));
            bool wet = Submerged(left) || Submerged(right);
            float distance = connected ? Mathf.Max(FlatDistance(left, previous[0]), FlatDistance(right, previous[1])) : 0;
            if (wet || distance > 3) Break();
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
                        segments.Enqueue(new Segment(a + offset, b + offset, c + offset, d + offset, along[i], length, time));
                        along[i] += length;
                        dirty = true;
                    }
                    previous[i] = point;
                    previousSide[i] = side;
                }
                connected = true;
            }
        }
        while (segments.Count > Capacity) { segments.Dequeue(); dirty = true; }
        if (dirty) Rebuild();

        // The river's height profile is not a global sea level: low grassland
        // remains dry even when it is lower than water elsewhere on the map.
        bool Submerged(Vector3 p) => !River.OnBridge(p.X, p.Z)
            && River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 2
            && terrain.Height(p.X, p.Z) < River.Level(p.Z);
        Vector3 Project(Vector3 p)
        {
            return new Vector3(p.X, Surface(p) + .035f, p.Z);
        }
        float Surface(Vector3 p)
        {
            float height = roads.SurfaceHeight(p, terrain.Height(p.X, p.Z));
            return River.OnBridge(p.X, p.Z) ? Mathf.Max(height, River.BridgeHeight + .01f) : height;
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

    private void Rebuild()
    {
        ribbon.ClearSurfaces();
        if (segments.Count == 0) return;
        ribbon.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var s in segments)
        {
            Vertex(s.A, 0, s.Along); Vertex(s.B, 1, s.Along); Vertex(s.C, 0, s.Along + s.Length);
            Vertex(s.B, 1, s.Along); Vertex(s.D, 1, s.Along + s.Length); Vertex(s.C, 0, s.Along + s.Length);
            void Vertex(Vector3 p, float across, float distance)
            {
                ribbon.SurfaceSetUV2(new Vector2(s.Born, 0));
                ribbon.SurfaceSetUV(new Vector2(across, distance));
                ribbon.SurfaceSetNormal(Vector3.Up);
                ribbon.SurfaceAddVertex(p);
            }
        }
        ribbon.SurfaceEnd();
    }
}
