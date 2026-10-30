using Godot;

namespace MathRacer;

public partial class Village
{
    public Node3D Square { get; private set; } = null!;

    private void GenerateSquare()
    {
        // Place after the existing residents so saved destinations keep their positions.
        var (plot, road) = FindCollectionPlot(105, -11, 11, 1200);
        Square = new Node3D { Name = "VillageSquare", Transform = plot };
        AddChild(Square);
        plots.Add(plot);

        // Small alternating pavers follow the terrain; no tall slab at the entrance.
        var surfaces = new[] { new SurfaceTool(), new SurfaceTool(), new SurfaceTool() };
        foreach (var surface in surfaces) surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Ground(float x, float z)
        {
            var p = plot * new Vector3(x, 0, z);
            return new Vector3(p.X, ground.Height(p.X, p.Z) + .065f, p.Z);
        }
        for (int x = -22; x < 22; x++)
            for (int z = -13; z < 13; z++)
            {
                bool border = x is -22 or 21 || z is -13 or 12;
                var surface = surfaces[border ? 2 : (x + z + 35) % 2];
                var a = Ground(x * .5f, z * .5f);
                var b = Ground((x + 1) * .5f, z * .5f);
                var c = Ground(x * .5f, (z + 1) * .5f);
                var d = Ground((x + 1) * .5f, (z + 1) * .5f);
                // Plot yaw preserves the winding of the surface in world space.
                foreach (var p in new[] { a, b, c, b, d, c }) surface.AddVertex(p);
                walkwayTriangles.Add((a, c, b)); walkwayTriangles.Add((b, c, d));
            }
        string[] paving = { "e7d5b7", "dfcba9", "b99878" };
        for (int i = 0; i < surfaces.Length; i++)
        {
            surfaces[i].GenerateNormals();
            var mesh = new MeshInstance3D { Name = "SquarePaving", Mesh = surfaces[i].Commit(), MaterialOverride = Art.Mat(paving[i]) };
            AddChild(mesh); mesh.CreateTrimeshCollision();
        }
        var entrance = Ground(0, 6.3f);
        Path(entrance, road, 5);

        var fountain = SquareProp("Fountain", 0, -.7f);
        float high = float.NegativeInfinity, low = float.PositiveInfinity;
        for (float x = -2.2f; x <= 2.2f; x += .2f)
            for (float z = -2.2f; z <= 2.2f; z += .2f)
            {
                if (x * x + z * z > 2.2f * 2.2f) continue;
                var p = fountain.GlobalTransform * new Vector3(x, 0, z);
                float height = ground.Height(p.X, p.Z);
                high = Mathf.Max(high, height); low = Mathf.Min(low, height);
            }
        fountain.GlobalPosition = new(fountain.GlobalPosition.X, high + .1f, fountain.GlobalPosition.Z);
        float foundationHeight = high - low + .2f;
        Art.Cylinder(fountain, Vector3.Down * foundationHeight / 2, 2.2f, 2.2f, foundationHeight, "c5b598");
        Obstacles.Add(fountain, Vector3.Down * foundationHeight / 2,
            new CylinderShape3D { Radius = 2.2f, Height = foundationHeight });
        Art.Cylinder(fountain, new(0, .15f, 0), 2.2f, 2.2f, .3f, "c5b598");
        Art.Cylinder(fountain, new(0, .31f, 0), 1.85f, 1.85f, .06f, "79d0db");
        // Individual rim stones leave the turquoise pool visible from the road.
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.Tau / 16;
            var stone = Art.Block(fountain, new(Mathf.Sin(angle) * 2, .48f, Mathf.Cos(angle) * 2), new(.8f, .5f, .38f), "eee0c3");
            stone.Rotation = new(0, angle, 0);
        }
        Art.Cylinder(fountain, new(0, .9f, 0), .4f, .25f, 1.4f, "dbceb4");
        Art.Cylinder(fountain, new(0, 1.6f, 0), 1, 1.1f, .2f, "eee0c3");
        Art.Cylinder(fountain, new(0, 1.72f, 0), .96f, .96f, .06f, "79d0db");
        Art.Cylinder(fountain, new(0, 2.05f, 0), .12f, .06f, .65f, "b3e8e5");
        Art.Ball(fountain, new(0, 2.4f, 0), Vector3.One * .18f, "c6f1e9");
        Obstacles.Add(fountain, new(0, .5f, 0), new CylinderShape3D { Radius = 2.2f, Height = 1 });

        SquareStall(-7, -3.2f, "ef9980", false);
        SquareStall(7, -3.2f, "83c4b3", true);
        foreach (float x in new[] { -7f, 7f })
        {
            var bench = SquareProp("SquareBench", x, 3.4f);
            for (int i = 0; i < 3; i++)
                Art.Block(bench, new(0, .65f, -.3f + i * .3f), new(2.8f, .12f, .25f), "bc8b60");
            foreach (float y in new[] { 1.05f, 1.35f })
                Art.Block(bench, new(0, y, -.5f), new(2.8f, .22f, .12f), "cd9e70");
            foreach (float leg in new[] { -1f, 1f })
                Art.Block(bench, new(leg, .3f, 0), new(.15f, .6f, .9f), "526f71");
            Obstacles.Box(bench, new(0, .7f, -.1f), new(2.8f, 1.4f, 1));
        }
        foreach (float x in new[] { -10f, 10f })
            foreach (float z in new[] { -5.4f, 5.4f })
            {
                var lamp = SquareProp("SquareLantern", x, z);
                Art.Cylinder(lamp, new(0, .15f, 0), .35f, .26f, .3f, "526f71");
                Art.Cylinder(lamp, new(0, 1.8f, 0), .095f, .07f, 3.6f, "526f71");
                Art.Block(lamp, new(0, 3.65f, 0), new(.55f, .7f, .55f), "ffe3a0");
                foreach (float corner in new[] { -.28f, .28f })
                    foreach (float depth in new[] { -.28f, .28f })
                        Art.Block(lamp, new(corner, 3.65f, depth), new(.06f, .8f, .06f), "526f71");
                Art.Cylinder(lamp, new(0, 4.1f, 0), .48f, .06f, .3f, "526f71");
                Obstacles.Trunk(lamp, Vector3.Zero, .2f, 4.2f);
            }
        foreach (float x in new[] { -3.5f, 3.5f })
        {
            var pot = SquareProp("FlowerPlanter", x, 5.3f);
            Art.Cylinder(pot, new(0, .3f, 0), .4f, .58f, .6f, "c98e71");
            Art.Cylinder(pot, new(0, .62f, 0), .61f, .61f, .12f, "e5ab87");
            Art.Ball(pot, new(0, .8f, 0), new(.55f, .3f, .55f), "6eaa75");
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.Tau / 7;
                var flower = new Vector3(Mathf.Sin(angle) * .35f, 1, Mathf.Cos(angle) * .35f);
                Art.Ball(pot, flower, new(.17f, .12f, .17f), i % 2 == 0 ? "f4b1c6" : "fff0ce");
                Art.Ball(pot, flower + Vector3.Up * .09f, Vector3.One * .065f, "efbf59");
            }
            Obstacles.Trunk(pot, Vector3.Zero, .58f, .8f);
        }
        // A high string of pennants frames the stalls without crossing the car entrance.
        var left = SquareProp("BuntingPost", -4.3f, -5.6f);
        var right = SquareProp("BuntingPost", 4.3f, -5.6f);
        foreach (var post in new[] { left, right })
        {
            Art.Cylinder(post, new(0, 2.4f, 0), .09f, .07f, 4.8f, "ac805f");
            Obstacles.Trunk(post, Vector3.Zero, .1f, 4.8f);
        }
        var previous = left.Position + Vector3.Up * 4.65f;
        for (int i = 1; i <= 12; i++)
        {
            float t = i / 12f;
            var next = left.Position.Lerp(right.Position, t) + Vector3.Up * (4.65f - Mathf.Sin(t * Mathf.Pi) * .65f);
            Art.Beam(Square, previous, next, .025f, "ac805f");
            var flag = new SurfaceTool(); flag.Begin(Mesh.PrimitiveType.Triangles);
            var middle = (previous + next) / 2 + Vector3.Down * .55f;
            foreach (var p in new[] { previous, next, middle, next, previous, middle }) flag.AddVertex(p);
            flag.GenerateNormals();
            Art.Mesh(Square, flag.Commit(), Vector3.Zero, Vector3.One, new[] { "ef9980", "83c4b3", "f0cc76", "b5a2d6" }[i % 4]);
            previous = next;
        }
        Art.Sign(Square, (left.Position + right.Position) / 2 + Vector3.Up * 3, "NÁVES U KAŠNY", 30);
    }

    private Node3D SquareProp(string name, float x, float z)
    {
        var p = Square.Transform * new Vector3(x, 0, z);
        var local = Square.Transform.AffineInverse() * ground.At(p.X, p.Z, .08f);
        var prop = new Node3D { Name = name, Position = local };
        Square.AddChild(prop);
        return prop;
    }

    private void SquareStall(float x, float z, string color, bool fruit)
    {
        var stall = SquareProp(fruit ? "FruitStall" : "BakeryStall", x, z);
        Art.Block(stall, new(0, .65f, 0), new(3.4f, 1.3f, 1.5f), "bb8e65");
        Art.Block(stall, new(0, 1.35f, 0), new(3.7f, .16f, 1.8f), "efd6a6");
        foreach (float side in new[] { -1.6f, 1.6f })
            Art.Cylinder(stall, new(side, 1.65f, -.5f), .07f, .07f, 3.3f, "926c51");
        for (int i = 0; i < 8; i++)
        {
            var awning = Art.Block(stall, new(-1.75f + i * .5f, 3.1f, 0), new(.5f, .12f, 2.3f), i % 2 == 0 ? color : "fff0d3");
            awning.Rotation = new(.12f, 0, 0);
            Art.Block(stall, new(-1.75f + i * .5f, 2.82f, 1.1f), new(.5f, .4f, .1f), i % 2 == 0 ? color : "fff0d3");
        }
        for (int i = 0; i < 3; i++)
        {
            float position = -1.1f + i * 1.1f;
            Art.Block(stall, new(position, 1.48f, 0), new(.95f, .16f, 1.1f), "916c4d");
            for (int j = 0; j < 4; j++)
                Art.Ball(stall, new(position - .2f + j % 2 * .4f, 1.7f, -.25f + j / 2 * .45f),
                    fruit ? Vector3.One * .21f : new(.2f, .16f, .32f),
                    fruit ? new[] { "df7864", "edc45e", "91b96a" }[i] : "dca15f");
        }
        Obstacles.Box(stall, new(0, .7f, 0), new(3.6f, 1.4f, 1.8f));
    }

    private void HouseMailbox(Transform3D plot, string color)
    {
        var p = plot * new Vector3(-4.3f, 0, 6.3f);
        var mailbox = new Node3D { Name = "HouseMailbox", Position = ground.At(p.X, p.Z), Basis = plot.Basis };
        AddChild(mailbox);
        Art.Cylinder(mailbox, new(0, .65f, 0), .09f, .07f, 1.3f, "a77a59");
        Art.Block(mailbox, new(0, 1.45f, 0), new(.8f, .65f, .55f), color);
        Art.Block(mailbox, new(0, 1.82f, 0), new(.95f, .12f, .7f), "fff0d3");
        Art.Block(mailbox, new(0, 1.55f, .285f), new(.45f, .065f, .025f), "526f71");
        Art.Block(mailbox, new(0, 1.3f, .29f), new(.28f, .16f, .025f), "fff0d3");
        Art.Block(mailbox, new(.45f, 1.65f, 0), new(.055f, .65f, .055f), "526f71");
        Art.Block(mailbox, new(.59f, 1.94f, 0), new(.3f, .18f, .065f), "eb644b");
        Art.Ball(mailbox, new(0, 1.38f, .32f), Vector3.One * .045f, "c39871");
        Obstacles.Box(mailbox, new(0, .95f, 0), new(.8f, 1.9f, .6f));
    }
}
