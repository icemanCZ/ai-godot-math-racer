using Godot;
using System.Collections.Generic;

namespace MathRacer;

public partial class Village : Node3D
{
    private Terrain ground = null!;
    private readonly List<Transform3D> plots = new();
    public Vector3 Center { get; private set; }
    public Vector3 GreetingSpot { get; private set; }

    public void Generate(Terrain terrain, Roads roads)
    {
        ground = terrain;
        // Staggered pairs on opposite verges follow the road's actual curve.
        (float Distance, float Side, string Color)[] sites =
        {
            (62, -1, "f7ddac"), (68, 1, "68cfee"),
            (90, -1, "ffbb7d"), (98, 1, "ffe58a")
        };
        foreach (var site in sites)
        {
            var frame = roads.SampleMain(site.Distance);
            var outward = frame.Forward.Cross(Vector3.Up) * site.Side;
            var position = ground.At(frame.Center.X + outward.X * 11, frame.Center.Z + outward.Z * 11);
            var inward = -outward;
            float yaw = Mathf.Atan2(inward.X, inward.Z);
            var plot = new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)), position);
            plots.Add(plot);
            House(position.X, position.Z, site.Color, yaw);
            var doorway = plot * new Vector3(0, 0, 3.15f);
            Path(doorway, frame.Center + outward * (Roads.MainWidth / 2 - .25f));
            Fence(plot, -5, -1.4f);
            Fence(plot, 1.4f, 5);
            var shrub = plot * new Vector3(-4.4f, 0, 1.5f);
            Art.Ball(this, ground.At(shrub.X, shrub.Z, .4f), new(.7f, .6f, .7f), "56c980");
        }
        var center = roads.SampleMain(80).Center;
        Center = ground.At(center.X, center.Z);
        var entrance = roads.SampleMain(49);
        var greeting = entrance.Center - entrance.Forward.Cross(Vector3.Up) * 6.8f;
        GreetingSpot = ground.At(greeting.X, greeting.Z);
    }

    public bool IsReserved(Vector3 point)
    {
        foreach (var plot in plots)
        {
            var local = plot.AffineInverse() * point;
            if (Mathf.Abs(local.X) < 6.5f && Mathf.Abs(local.Z) < 7.7f) return true;
        }
        return false;
    }

    private void Path(Vector3 from, Vector3 to)
    {
        var direction = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        var side = direction.Normalized().Cross(Vector3.Up) * .9f;
        int segments = Mathf.CeilToInt(direction.Length() / .6f);
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < segments; i++)
        {
            var p = from.Lerp(to, (float)i / segments);
            var q = from.Lerp(to, (float)(i + 1) / segments);
            var a = ground.At(p.X - side.X, p.Z - side.Z, .16f);
            var b = ground.At(p.X + side.X, p.Z + side.Z, .16f);
            var c = ground.At(q.X - side.X, q.Z - side.Z, .16f);
            var d = ground.At(q.X + side.X, q.Z + side.Z, .16f);
            surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(b);
            surface.AddVertex(b); surface.AddVertex(c); surface.AddVertex(d);
        }
        surface.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = Art.Mat("e9cf9e"), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private void Fence(Transform3D plot, float left, float right)
    {
        Vector3 Position(float x, float lift)
        {
            var p = plot * new Vector3(x, 0, 4.8f);
            return ground.At(p.X, p.Z, lift);
        }
        for (int i = 0; i <= 2; i++)
        {
            var center = Position(Mathf.Lerp(left, right, i / 2f), .65f);
            Art.Cylinder(this, center, .1f, .085f, 1.3f, "f2ddb0");
            Obstacles.Trunk(this, center - Vector3.Up * .65f, .1f, 1.3f);
        }
        foreach (float lift in new[] { .45f, .95f })
        {
            var from = Position(left, lift);
            var to = Position(right, lift);
            Art.Beam(this, from, to, .13f, "f2ddb0");
            Obstacles.Box(this, (from + to) / 2, new Vector3(.13f, .13f, from.DistanceTo(to)),
                Basis.LookingAt(to - from, Vector3.Up));
        }
    }
    private void House(float x, float z, string color, float yaw)
    {
        var h = new Node3D { Position = ground.At(x, z), Rotation = new Vector3(0, yaw, 0) }; AddChild(h);
        Art.Block(h, new(0, 2.1f, 0), new(7, 4.2f, 6), color);
        // A gabled roof, with a deliberately generous overhang.
        var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] v = { new(-4, 4, -3.6f), new(4, 4, -3.6f), new(0, 6.7f, -3.6f), new(-4, 4, 3.6f), new(4, 4, 3.6f), new(0, 6.7f, 3.6f) };
        int[] indices = { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 2, 5, 4, 2, 4, 1 };
        foreach (int i in indices) st.AddVertex(v[i]); st.GenerateNormals();
        var roof = Art.Mesh(h, st.Commit(), Vector3.Zero, Vector3.One, "eb644b");
        var roofMaterial = (StandardMaterial3D)Art.Mat("eb644b").Duplicate();
        roofMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled; roof.MaterialOverride = roofMaterial;
        Art.Block(h, new(0, 1.25f, 3.04f), new(1.35f, 2.5f, .15f), "a87b61");
        foreach (float wx in new[] { -2.15f, 2.15f })
        {
            Art.Block(h, new(wx, 2.5f, 3.08f), new(1.5f, 1.5f, .18f), "fff1d2");
            Art.Block(h, new(wx, 2.5f, 3.2f), new(1.12f, 1.12f, .1f), "88c7d8");
            Art.Block(h, new(wx, 2.5f, 3.28f), new(.12f, 1.2f, .1f), "fff1d2");
        }
        Art.Block(h, new(2, 5.3f, 0), new(.85f, 3, 1), "f7ddac");
        var body = new StaticBody3D(); h.AddChild(body); body.AddChild(new CollisionShape3D { Position = new(0, 2, 0), Shape = new BoxShape3D { Size = new(7, 4, 6) } });
    }
}

