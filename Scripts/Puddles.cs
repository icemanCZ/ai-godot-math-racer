using Godot;
using System.Collections.Generic;

namespace MathRacer;

public partial class Puddles : Node3D
{
    private readonly record struct Patch(Vector3 Center, Vector3 Forward, float Width, float Length);
    private readonly List<Patch> patches = new();
    private readonly ShaderMaterial water = new() { Shader = GD.Load<Shader>("res://Shaders/Puddle.gdshader") };
    private readonly CpuParticles3D[] spray = new CpuParticles3D[12];
    private readonly float[] wheelTimers = new float[4];
    private int nextSpray;
    private World world = null!;

    internal void Generate(World valley)
    {
        world = valley;
        Visible = false;
        var random = new RandomNumberGenerator { Seed = (ulong)Settings.Seed + 1389 };
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var anchors = world.Routes.NavigationAnchors;
        for (int i = 1; i < anchors.Count; i += random.RandiRange(2, 4))
        {
            var (center, forward) = anchors[i];
            if (forward.LengthSquared() < .5f || River.Distance(center.X, center.Z) < River.HalfWidth(center.Z) + 10 ||
                patches.Exists(p => p.Center.DistanceSquaredTo(center) < 16 * 16)) continue;
            var patch = new Patch(center + forward.Cross(Vector3.Up) * random.RandfRange(-.65f, .65f),
                forward, random.RandfRange(1.65f, 2.3f), random.RandfRange(2.1f, 3.8f));
            patches.Add(patch);
            AddSurface(surface, patch);
        }
        surface.Index();
        surface.GenerateNormals();
        AddChild(new MeshInstance3D
        {
            Mesh = surface.Commit(), MaterialOverride = water,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });

        var droplets = new SphereMesh
        {
            Radius = .075f, Height = .15f, RadialSegments = 5, Rings = 3,
            Material = Art.Mat("b8d5e6")
        };
        for (int i = 0; i < spray.Length; i++)
        {
            spray[i] = new CpuParticles3D
            {
                Emitting = false, OneShot = true, LocalCoords = false, Amount = 14,
                Lifetime = .5f, Explosiveness = 1, LifetimeRandomness = .35f,
                Mesh = droplets, Spread = 38, Gravity = new Vector3(0, -14, 0),
                InitialVelocityMin = 1.8f, InitialVelocityMax = 4.3f,
                ScaleAmountMin = .45f, ScaleAmountMax = 1.2f
            };
            AddChild(spray[i]);
        }
    }

    private float SurfaceHeight(Vector3 point) =>
        world.Routes.SurfaceHeight(point, world.Ground.Height(point.X, point.Z));

    private void AddSurface(SurfaceTool mesh, Patch patch)
    {
        const int steps = 14;
        var side = patch.Forward.Cross(Vector3.Up);
        var points = new Vector3[steps + 1, steps + 1];
        for (int x = 0; x <= steps; x++)
        for (int z = 0; z <= steps; z++)
        {
            var point = patch.Center + side * ((2f * x / steps - 1) * patch.Width) +
                patch.Forward * ((2f * z / steps - 1) * patch.Length);
            point.Y = SurfaceHeight(point) + .035f;
            points[x, z] = point;
        }
        for (int x = 0; x < steps; x++)
        for (int z = 0; z < steps; z++)
        {
            Vertex(x, z); Vertex(x + 1, z); Vertex(x, z + 1);
            Vertex(x + 1, z); Vertex(x + 1, z + 1); Vertex(x, z + 1);
        }
        void Vertex(int x, int z)
        {
            mesh.SetUV(new Vector2(x / (float)steps, z / (float)steps));
            mesh.AddVertex(points[x, z]);
        }
    }

    internal void Tick(float dt, Vehicle car, float wetness, float rainfall)
    {
        Visible = wetness > .01f;
        if (!Visible) return;
        water.SetShaderParameter("wetness", wetness);
        water.SetShaderParameter("rain_amount", rainfall);
        var motion = new Vector3(car.Velocity.X, 0, car.Velocity.Z);
        float speed = motion.Length();
        for (int i = 0; i < car.Wheels.Count; i++)
        {
            wheelTimers[i] -= dt;
            if (wheelTimers[i] > 0 || speed < 1.6f || !car.IsOnFloor() || wetness < .16f) continue;
            var wheel = car.Wheels[i];
            var point = wheel.GlobalPosition;
            if (!patches.Exists(p => Contains(p, point))) continue;
            float surface = SurfaceHeight(point);
            if (Mathf.Abs(point.Y - surface - .48f) > .6f) continue;
            point.Y = surface + .12f;
            var emitter = spray[nextSpray];
            nextSpray = (nextSpray + 1) % spray.Length;
            var outward = car.GlobalBasis.X * Mathf.Sign(wheel.Position.X);
            emitter.Direction = (Vector3.Up * 1.2f + outward - motion * .06f).Normalized();
            emitter.GlobalPosition = point;
            emitter.InitialVelocityMax = 3f + speed * .15f;
            emitter.Restart();
            emitter.Emitting = true;
            wheelTimers[i] = Mathf.Lerp(.24f, .14f, speed / Settings.MaxSpeed);
        }
    }

    private static bool Contains(Patch patch, Vector3 point)
    {
        var offset = point - patch.Center;
        var p = new Vector2(offset.Dot(patch.Forward.Cross(Vector3.Up)) / patch.Width,
            offset.Dot(patch.Forward) / patch.Length);
        float angle = p.Angle();
        return p.Length() + Mathf.Sin(angle * 5) * .055f + Mathf.Cos(angle * 3) * .075f < .94f;
    }
}
