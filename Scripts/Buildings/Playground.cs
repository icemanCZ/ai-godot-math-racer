using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The village playground: a staked-out yard behind a ring where the trailer tips out its load. Every one of the six
/// deliveries brings a toy and a small animal who plays on it: a sandbox, swings, a seesaw, a brick playhouse, a slide
/// and a roundabout under bunting. The yard's frame has +Z pointing away from the road, +X to the right.
/// </summary>
public sealed partial class Playground : Node3D
{
    public const int Trips = PlaygroundPlans.Trips;

    // Where the toys stand in the frame of the yard: across (x) and along (z).
    private const float SandboxX = -6.8f,
        SandboxZ = 7f,
        SwingX = 6.8f,
        SwingZ = 7f,
        RoundaboutZ = 9.2f,
        SeesawX = -6.8f,
        SeesawZ = 14.2f,
        HouseX = 6.8f,
        HouseZ = 14.8f,
        SlideZ = 16.2f,
        SlideHeight = 1.9f;

    /// <summary>How far from the yard a car makes the children cheer.</summary>
    private const float CheerDistance = 30;

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D[] stages = new Node3D[Trips];
    private readonly List<CollisionShape3D>[] solids = new List<CollisionShape3D>[Trips];
    private readonly List<(int Stage, Character Who)> children = new();

    private Node3D marks = null!;
    private Label3D sign = null!;
    private Node3D? swingA,
        swingB,
        seesaw,
        roundabout,
        balloons;
    private Character? slideChild,
        sandChild,
        houseChild;
    private float slideBase;
    private float time;
    private float cheerTimer = 2;
    private bool wasNear;

    internal PlaygroundSite Site { get; }

    /// <summary>Deliveries made so far.</summary>
    public int Stage { get; private set; }

    public bool Complete => Stage >= Trips;

    /// <summary>The keeper who sends the trailer and thanks the driver.</summary>
    internal Character Helper { get; set; } = null!;

    internal Vector3 Drop => Site.Position;

    internal Vector3 Road => Site.Road;

    /// <summary>The middle of the yard, for celebrations and the camera.</summary>
    internal Vector3 Focus => ToGlobal(new Vector3(0, 2.5f, 10));

    /// <summary>How far the first swing is swung and how far the roundabout has turned, for the checks.</summary>
    internal float SwingAngle => swingA?.Rotation.X ?? 0;

    internal float RoundaboutAngle => roundabout?.Rotation.Y ?? 0;

    /// <summary>The children who play on the toys built so far.</summary>
    internal int ChildrenPlaying
    {
        get
        {
            int count = 0;
            foreach (var (stage, _) in children)
                if (stage < Stage)
                    count++;
            return count;
        }
    }

    internal Playground(PlaygroundSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "Playground";
        Position = site.Position;
        float yaw = Mathf.Atan2(site.Inward.X, site.Inward.Z);
        Rotation = new Vector3(0, yaw, 0);
        basis = new Basis(Vector3.Up, yaw);
        for (int i = 0; i < Trips; i++)
            solids[i] = new List<CollisionShape3D>();
    }

    /// <summary>Shows the toys of <paramref name="stage"/> delivered loads; a restored game simply has them.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Trips);
        if (IsInsideTree())
            Refresh();
    }

    // ----- the ground under the toys -------------------------------------------------------------------------------

    /// <summary>Height above the ring of the ground below a point given in the yard's own frame.</summary>
    private float Floor(float x, float z)
    {
        var global = Position + basis * new Vector3(x, 0, z);
        return ground.Height(global.X, global.Z) - Position.Y;
    }

    private float Highest(float x, float z, float width, float depth)
    {
        float top = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
            top = Mathf.Max(top, Floor(x + dx * width, z + dz * depth));
        return top + .05f;
    }

    /// <summary>A thick flat slab under a toy, so that the toy stands level; returns the height of its top.</summary>
    private float Pad(Node3D parent, float x, float z, float width, float depth, string color)
    {
        float top = Highest(x, z, width, depth);
        Art.Block(parent, new(x, top - 2.5f, z), new(width, 5, depth), color);
        return top;
    }

    private float PadDisc(Node3D parent, float x, float z, float radius, string color)
    {
        float top = Highest(x, z, radius * 1.6f, radius * 1.6f);
        Art.Cylinder(parent, new(x, top - 2.5f, z), radius, radius, 5, color);
        return top;
    }

    /// <summary>A solid box that blocks the car once <paramref name="stage"/> (0-5) has been built.</summary>
    private void Solid(int stage, Vector3 center, Vector3 size, Basis? rotation = null)
    {
        var body = Obstacles.Add(stages[stage], center, new BoxShape3D { Size = size }, rotation);
        var shape = (CollisionShape3D)body.GetChild(0);
        shape.Disabled = stage >= Stage;
        solids[stage].Add(shape);
    }

    /// <summary>A small animal of the village who plays on a toy (never one who can be talked to).</summary>
    private Character Child(
        int stage,
        Node3D parent,
        Vector3 position,
        float yaw,
        int kind,
        string fur,
        float size = .38f
    )
    {
        var child = Character.Create("", "", kind, fur);
        child.Position = position;
        child.Rotation = new Vector3(0, yaw, 0);
        child.Scale = Vector3.One * size;
        child.Guide(false);
        parent.AddChild(child);
        children.Add((stage, child));
        return child;
    }

    // ----- building ------------------------------------------------------------------------------------------------

    public override void _Ready()
    {
        marks = new Node3D { Name = "YardMarks" };
        AddChild(marks);
        BuildMarks();
        for (int i = 0; i < Trips; i++)
        {
            stages[i] = new Node3D { Name = "PlaygroundStage" + i, Visible = false };
            AddChild(stages[i]);
        }
        BuildPathAndSandbox(stages[0]);
        BuildSwings(stages[1]);
        BuildSeesaw(stages[2]);
        BuildPlayhouse(stages[3]);
        BuildSlide(stages[4]);
        BuildRoundabout(stages[5]);
        BuildBunting(stages[5]);
        BuildBalloons(stages[5]);
        BuildArch();
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < Trips; i++)
        {
            stages[i].Visible = i < Stage;
            foreach (var shape in solids[i])
                shape.Disabled = i >= Stage;
        }
        marks.Visible = !Complete;
        sign.Text = Complete ? "✓  Hřiště" : $"Hřiště  {Stage} / {Trips}";
    }

    private static readonly (float X, float Z)[] Corners =
    {
        (-10.5f, 3.5f),
        (-10.5f, 17.5f),
        (10.5f, 17.5f),
        (10.5f, 3.5f),
    };

    /// <summary>Pegs and a rope on three sides of the yard; the side to the road stays open for the trailer.</summary>
    private void BuildMarks()
    {
        var pegs = new Vector3[Corners.Length];
        for (int i = 0; i < Corners.Length; i++)
        {
            pegs[i] = new Vector3(Corners[i].X, Floor(Corners[i].X, Corners[i].Z), Corners[i].Z);
            Art.Cylinder(marks, pegs[i] + Vector3.Up * .5f, .07f, .06f, 1, "a77a59");
            Art.Ball(marks, pegs[i] + Vector3.Up * 1.02f, new(.11f, .07f, .11f), "f2c14e");
        }
        for (int i = 0; i < 3; i++)
            Art.Beam(marks, pegs[i] + Vector3.Up * .8f, pegs[i + 1] + Vector3.Up * .8f, .035f, "f2ddb0");
    }

    /// <summary>The arch over the path with the sign of the playground; it stands from the first day.</summary>
    private void BuildArch()
    {
        float y = Floor(0, 4.2f);
        foreach (float x in new[] { -1.8f, 1.8f })
        {
            float foot = Floor(x, 4.2f);
            Art.Cylinder(this, new(x, foot + 1.5f, 4.2f), .11f, .13f, 3, "b0855c");
            Art.Ball(this, new(x, foot + 3.05f, 4.2f), new(.2f, .2f, .2f), x < 0 ? "e2563b" : "4f8fb8");
        }
        Art.Block(this, new(0, y + 2.85f, 4.2f), new(3.9f, .22f, .22f), "c39771");
        sign = Art.Sign(this, new(0, y + 3.6f, 4.2f), "", 30);
    }

    /// <summary>Stage 1: a path of gravel from the ring and a sandbox with a bucket, a spade, a castle and a child.</summary>
    private void BuildPathAndSandbox(Node3D stage)
    {
        float path = Pad(stage, 0, 4.3f, 2.2f, 3.8f, "b9b4a5");
        for (int i = 0; i < 4; i++)
            Art.Ball(stage, new(i % 2 == 0 ? -.55f : .6f, path + .05f, 2.9f + i * .9f), new(.5f, .12f, .4f), "8f8a7d");
        foreach (float side in new[] { -1f, 1f })
            Art.Ball(stage, new(side * 1.6f, path + .1f, 3.3f), new(.7f, .35f, .6f), "9c968c");

        float top = Pad(stage, SandboxX, SandboxZ, 5.4f, 5.4f, "bfd98c");
        Art.Block(stage, new(SandboxX, top + .16f, SandboxZ), new(4.4f, .32f, 4.4f), "efd9a0");
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Block(stage, new(SandboxX + side * 2.3f, top + .24f, SandboxZ), new(.24f, .48f, 4.8f), "b0855c");
            Art.Block(stage, new(SandboxX, top + .24f, SandboxZ + side * 2.3f), new(4.8f, .48f, .24f), "c39771");
        }
        // The bucket, the spade stuck in the sand and a castle with a flag.
        Art.Cylinder(stage, new(SandboxX + 1.2f, top + .5f, SandboxZ + 1.1f), .26f, .34f, .42f, "e9566b");
        Art.Beam(
            stage,
            new(SandboxX - .9f, top + .35f, SandboxZ - 1.5f),
            new(SandboxX - .5f, top + 1.0f, SandboxZ - 1.2f),
            .08f,
            "a77a59"
        );
        Art.Ball(stage, new(SandboxX - .45f, top + 1.05f, SandboxZ - 1.15f), new(.22f, .26f, .08f), "5aa0d6");
        var castle = new Vector3(SandboxX - 1.0f, top + .32f, SandboxZ + 1.0f);
        Art.Cylinder(stage, castle + Vector3.Up * .22f, .5f, .62f, .5f, "e0c47c");
        Art.Cylinder(stage, castle + Vector3.Up * .62f, .28f, .4f, .4f, "e8d08a");
        Art.Cylinder(stage, castle + Vector3.Up * 1.05f, .02f, .02f, .5f, "a77a59");
        Art.Block(stage, castle + new Vector3(.12f, 1.2f, 0), new(.26f, .16f, .02f), "e2563b");
        sandChild = Child(
            0,
            stage,
            new(SandboxX + .3f, top + .32f, SandboxZ + 1.2f),
            Mathf.Pi * .75f,
            0,
            "f3d9e0",
            .36f
        );
        Solid(0, new(SandboxX, top + .25f, SandboxZ), new(5, .6f, 5));
    }

    /// <summary>Stage 2: an A-frame with two swings; the child on the first one swings high.</summary>
    private void BuildSwings(Node3D stage)
    {
        float top = Pad(stage, SwingX, SwingZ, 6.4f, 5.2f, "e3d3ab");
        float bar = top + 3f;
        foreach (float x in new[] { SwingX - 2.1f, SwingX + 2.1f })
        foreach (float side in new[] { -1f, 1f })
            Art.Beam(stage, new(x, top, SwingZ + side * 1.1f), new(x, bar, SwingZ), .17f, "c39771");
        Art.Beam(stage, new(SwingX - 2.4f, bar, SwingZ), new(SwingX + 2.4f, bar, SwingZ), .22f, "b0855c");
        swingA = SwingSeat(stage, SwingX - .95f, bar, "e2563b");
        swingB = SwingSeat(stage, SwingX + .95f, bar, "4f8fb8");
        Child(1, swingA, new(0, -2.26f, 0), Mathf.Pi, 1, "f0b58a");
        Solid(1, new(SwingX, top + 1.5f, SwingZ), new(5, 3, 2.6f));
    }

    private Node3D SwingSeat(Node3D stage, float x, float bar, string color)
    {
        var pivot = new Node3D { Position = new(x, bar, SwingZ) };
        stage.AddChild(pivot);
        foreach (float side in new[] { -.3f, .3f })
            Art.Beam(pivot, new(side, 0, 0), new(side, -2.3f, 0), .04f, "dfe6e8");
        Art.Block(pivot, new(0, -2.34f, 0), new(.85f, .08f, .36f), color);
        return pivot;
    }

    /// <summary>Stage 3: a seesaw with a child at each end.</summary>
    private void BuildSeesaw(Node3D stage)
    {
        float top = Pad(stage, SeesawX, SeesawZ, 7.4f, 3.4f, "e3d3ab");
        Art.Block(stage, new(SeesawX, top + .4f, SeesawZ), new(.55f, .8f, 1.5f), "b0855c");
        seesaw = new Node3D { Position = new(SeesawX, top + .9f, SeesawZ) };
        stage.AddChild(seesaw);
        Art.Block(seesaw, new(0, 0, 0), new(6.2f, .16f, .72f), "e2563b");
        Art.Block(seesaw, new(0, .02f, 0), new(6.0f, .18f, .2f), "fff1d2");
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Cylinder(seesaw, new(side * 2.0f, .32f, .1f), .04f, .04f, .55f, "a77a59");
            Art.Block(seesaw, new(side * 2.0f, .6f, .1f), new(.5f, .05f, .05f), "a77a59");
        }
        Child(2, seesaw, new(-2.45f, .08f, 0), Mathf.Pi, 2, "c49a74", .34f);
        Child(2, seesaw, new(2.45f, .08f, 0), Mathf.Pi, 0, "d9c7f0", .34f);
        Solid(2, new(SeesawX, top + .8f, SeesawZ), new(6.6f, 1.6f, 1.5f));
    }

    /// <summary>Stage 4: a little brick house with a door, a window with a flower box, a roof and a child who waves.</summary>
    private void BuildPlayhouse(Node3D stage)
    {
        const float width = 3.8f,
            depth = 3.2f,
            height = 2.1f,
            thick = .25f;
        float top = Pad(stage, HouseX, HouseZ, 5f, 4.4f, "c5b598");
        float frontZ = HouseZ - depth / 2;
        void Wall(float x0, float x1, float y0, float y1, float z, float thin = thick) =>
            Art.Block(
                stage,
                new(HouseX + (x0 + x1) / 2, top + (y0 + y1) / 2, z),
                new(x1 - x0, y1 - y0, thin),
                (y0 > 1) ? "d4856a" : "c9795c"
            );
        // The raised floor, where the child stands.
        Art.Block(stage, new(HouseX, top + .2f, HouseZ), new(width - .3f, .4f, depth - .3f), "d9c29a");
        Wall(-width / 2, width / 2, 0, height, HouseZ + depth / 2 - thick / 2);
        foreach (float side in new[] { -1f, 1f })
            Art.Block(
                stage,
                new(HouseX + side * (width / 2 - thick / 2), top + height / 2, HouseZ),
                new(thick, height, depth),
                "c9795c"
            );
        // The front: a door on the left, a window on the right.
        float z = frontZ + thick / 2;
        Wall(-width / 2 + thick, -1.5f, 0, height, z);
        Wall(-1.5f, -.5f, 1.7f, height, z);
        Wall(-.5f, .5f, 0, height, z);
        Wall(.5f, 1.5f, 0, .9f, z);
        Wall(.5f, 1.5f, 1.7f, height, z);
        Wall(1.5f, width / 2 - thick, 0, height, z);
        Art.Window(stage, new(HouseX + 1f, top + 1.3f, z), new(1f, .8f, .06f), "88c7d8");
        Art.Block(stage, new(HouseX - 1f, top + .75f, frontZ + .02f), new(.92f, 1.5f, .08f), "4f8fb8");
        Art.Ball(stage, new(HouseX - .65f, top + .78f, frontZ - .04f), Vector3.One * .06f, "f5d36b");
        Art.Block(stage, new(HouseX + 1f, top + .98f, frontZ - .2f), new(1.3f, .18f, .32f), "a87b61");
        foreach (float flower in new[] { -.4f, 0, .4f })
            Art.Ball(stage, new(HouseX + 1f + flower, top + 1.12f, frontZ - .2f), Vector3.One * .13f, "f56f8f");

        // The roof: a prism along the depth, in the blue of the house's door.
        float ridge = top + height + 1.1f,
            eave = width / 2 + .3f,
            back = HouseZ + depth / 2 + .3f,
            front = frontZ - .3f,
            low = top + height - .05f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] v =
        {
            new(HouseX - eave, low, front),
            new(HouseX + eave, low, front),
            new(HouseX, ridge, front),
            new(HouseX - eave, low, back),
            new(HouseX + eave, low, back),
            new(HouseX, ridge, back),
        };
        int[] indices = { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 2, 5, 4, 2, 4, 1 };
        foreach (int i in indices)
            st.AddVertex(v[i]);
        st.GenerateNormals();
        var roof = Art.Mesh(stage, st.Commit(), Vector3.Zero, Vector3.One, "6c8fd4");
        var material = (StandardMaterial3D)Art.Mat("6c8fd4").Duplicate();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        roof.MaterialOverride = material;

        houseChild = Child(3, stage, new(HouseX + 1f, top + .4f, frontZ + .75f), Mathf.Pi, 1, "e8bf73", .36f);
        Solid(3, new(HouseX, top + height / 2, HouseZ), new(width, height, depth));
    }

    /// <summary>Stage 5: a tower with a ladder behind it and a slide in front; a child climbs up, slides down and runs back.</summary>
    private void BuildSlide(Node3D stage)
    {
        float top = Pad(stage, 0, 15f, 6.2f, 6.2f, "e3d3ab");
        slideBase = top;
        foreach (float x in new[] { -.8f, .8f })
        foreach (float z in new[] { SlideZ - .8f, SlideZ + .8f })
            Art.Cylinder(stage, new(x, top + SlideHeight / 2, z), .07f, .08f, SlideHeight, "b0855c");
        Art.Block(stage, new(0, top + SlideHeight, SlideZ), new(1.9f, .14f, 1.9f), "c39771");
        foreach (float side in new[] { -.88f, .88f })
            Art.Block(stage, new(side, top + SlideHeight + .3f, SlideZ), new(.07f, .5f, 1.9f), "e2bb8b");
        Art.Block(stage, new(0, top + SlideHeight + .3f, SlideZ + .88f), new(1.9f, .5f, .07f), "e2bb8b");
        // The ladder leans on the back of the tower.
        foreach (float side in new[] { -.4f, .4f })
            Art.Beam(stage, new(side, top, SlideZ + 1.7f), new(side, top + SlideHeight, SlideZ + .9f), .07f, "a77a59");
        for (int i = 1; i <= 5; i++)
        {
            float u = i / 6f;
            Art.Block(
                stage,
                new(0, top + SlideHeight * u, SlideZ + Mathf.Lerp(1.7f, .9f, u)),
                new(.8f, .06f, .08f),
                "e2bb8b"
            );
        }
        // The slide itself runs from the front of the tower down to the ground, 31 degrees steep.
        float runStart = SlideZ - .9f,
            runEnd = SlideZ - 3.8f;
        var runMiddle = new Vector3(0, top + (SlideHeight + .15f) / 2 + .02f, (runStart + runEnd) / 2);
        float angle = -Mathf.Atan2(SlideHeight - .15f, runStart - runEnd);
        float length = new Vector2(runStart - runEnd, SlideHeight - .15f).Length();
        var tilt = new Basis(Vector3.Right, angle);
        var ramp = Art.Block(stage, runMiddle, new(1.1f, .1f, length), "f2c14e");
        ramp.Basis = tilt.Scaled(new Vector3(1.1f, .1f, length));
        foreach (float side in new[] { -.58f, .58f })
        {
            var rail = Art.Block(stage, runMiddle + new Vector3(side, .1f, 0), new(.08f, .22f, length), "e2563b");
            rail.Basis = tilt.Scaled(new Vector3(.08f, .22f, length));
        }
        Art.Block(stage, new(0, top + .06f, runEnd - .15f), new(1.1f, .1f, .5f), "f2c14e");
        slideChild = Child(4, stage, new(0, top, SlideZ + 1.7f), 0, 2, "a8c9a0", .36f);
        Solid(4, new(0, top + SlideHeight / 2, SlideZ), new(1.9f, SlideHeight + .4f, 1.9f));
        Solid(4, runMiddle, new(1.2f, .4f, length), tilt);
    }

    /// <summary>Stage 6: a roundabout on a ring of bricks, under a red and white roof, with two children aboard.</summary>
    private void BuildRoundabout(Node3D stage)
    {
        float top = PadDisc(stage, 0, RoundaboutZ, 2.9f, "b7ab96");
        Art.Cylinder(stage, new(0, top + .08f, RoundaboutZ), 2.7f, 2.7f, .16f, "c9795c");
        roundabout = new Node3D { Position = new(0, top + .22f, RoundaboutZ) };
        stage.AddChild(roundabout);
        Art.Cylinder(roundabout, new(0, 0, 0), 2.2f, 2.2f, .14f, "f2c14e");
        Art.Cylinder(roundabout, new(0, 1.2f, 0), .12f, .12f, 2.4f, "e2563b");
        Art.Cylinder(roundabout, new(0, 2.7f, 0), 2.35f, .14f, .85f, "e2563b");
        Art.Cylinder(roundabout, new(0, 2.28f, 0), 2.4f, 2.36f, .14f, "fff1d2");
        Art.Ball(roundabout, new(0, 3.15f, 0), Vector3.One * .16f, "f2c14e");
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.Pi / 4 + i * Mathf.Pi / 2;
            Art.Cylinder(
                roundabout,
                new(Mathf.Sin(a) * 2.05f, 1.15f, Mathf.Cos(a) * 2.05f),
                .05f,
                .05f,
                2.3f,
                "fff1d2"
            );
            var seat = Art.Block(
                roundabout,
                new(Mathf.Sin(a) * 1.3f, .32f, Mathf.Cos(a) * 1.3f),
                new(.9f, .12f, .42f),
                i % 2 == 0 ? "4f8fb8" : "5aa86f"
            );
            seat.Rotation = new Vector3(0, a, 0);
        }
        float first = Mathf.Pi / 4,
            second = Mathf.Pi / 4 + Mathf.Pi;
        Child(5, roundabout, new(Mathf.Sin(first) * 1.3f, .38f, Mathf.Cos(first) * 1.3f), first, 0, "f3e8d5", .36f);
        Child(5, roundabout, new(Mathf.Sin(second) * 1.3f, .38f, Mathf.Cos(second) * 1.3f), second, 1, "f0b58a", .36f);
        Solid(5, new(0, top + .6f, RoundaboutZ), new(5.2f, 1.2f, 5.2f));
    }

    private static readonly string[] FlagColors = { "e2563b", "f2c14e", "4f8fb8", "5aa86f", "d9718f", "ffffff" };

    /// <summary>Stage 6: poles at the corners of the yard and bunting between them on three sides.</summary>
    private void BuildBunting(Node3D stage)
    {
        var tops = new Vector3[Corners.Length];
        for (int i = 0; i < Corners.Length; i++)
        {
            float foot = Floor(Corners[i].X, Corners[i].Z);
            tops[i] = new Vector3(Corners[i].X, foot + 3.4f, Corners[i].Z);
            Art.Cylinder(stage, new(Corners[i].X, foot + 1.7f, Corners[i].Z), .07f, .09f, 3.4f, "a77a59");
            Art.Ball(stage, tops[i] + Vector3.Up * .1f, Vector3.One * .13f, "f2c14e");
        }
        int flag = 0;
        for (int side = 0; side < 3; side++)
        {
            var from = tops[side];
            var to = tops[side + 1];
            Art.Beam(stage, from, to, .03f, "f2ddb0");
            const int count = 13;
            for (int i = 1; i < count; i++)
            {
                float t = i / (float)count;
                var p = from.Lerp(to, t) + Vector3.Down * (.28f + Mathf.Sin(t * Mathf.Pi) * .1f);
                var pennant = Art.Block(stage, p, new(.3f, .36f, .03f), FlagColors[flag++ % FlagColors.Length]);
                // Flags hang as diamonds, turned across the string.
                pennant.Rotation = new Vector3(0, side % 2 == 0 ? Mathf.Pi / 2 : 0, Mathf.Pi / 4);
            }
        }
    }

    private static readonly (string Color, float X, float Y, float Z)[] BalloonSpots =
    {
        ("e2563b", -.3f, 1.9f, .0f),
        ("f2c14e", .35f, 2.3f, .25f),
        ("4f8fb8", .05f, 2.7f, -.2f),
        ("5aa86f", -.55f, 2.45f, -.3f),
        ("d9718f", .6f, 1.75f, -.25f),
        ("a86fb8", .0f, 3.2f, .15f),
        ("f08a3c", -.1f, 2.1f, .45f),
    };

    /// <summary>Stage 6: a bunch of balloons tied to the arch, which bobs in the wind.</summary>
    private void BuildBalloons(Node3D stage)
    {
        float foot = Floor(1.8f, 4.2f);
        balloons = new Node3D { Position = new(1.8f, foot + 3.05f, 4.2f) };
        stage.AddChild(balloons);
        foreach (var (color, x, y, z) in BalloonSpots)
        {
            Art.Beam(balloons, Vector3.Zero, new(x, y - .45f, z), .015f, "f5f0e6");
            Art.Ball(balloons, new(x, y, z), new(.42f, .52f, .42f), color);
            Art.Ball(balloons, new(x, y - .55f, z), new(.07f, .07f, .07f), color);
        }
    }

    // ----- life ----------------------------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (Helper != null && Helper.AnimationPaused)
            return;
        float dt = (float)delta;
        time += dt;
        if (swingA != null)
            swingA.Rotation = new Vector3(Mathf.Sin(time * 1.7f) * .52f, 0, 0);
        if (swingB != null)
            swingB.Rotation = new Vector3(Mathf.Sin(time * 1.7f + 2.3f) * .4f, 0, 0);
        if (seesaw != null)
            seesaw.Rotation = new Vector3(0, 0, Mathf.Sin(time * 1.25f) * .22f);
        if (roundabout != null)
            roundabout.Rotation = new Vector3(0, time * .9f, 0);
        if (balloons != null)
        {
            balloons.Rotation = new Vector3(Mathf.Sin(time * 1.1f) * .08f, 0, Mathf.Sin(time * .8f) * .1f);
        }
        if (Stage > 4)
            TickSlide();
        cheerTimer -= dt;
        if (cheerTimer <= 0)
        {
            cheerTimer = 4.5f;
            if (Stage > 0)
                sandChild?.Celebrate();
            if (Stage > 3)
                houseChild?.Celebrate();
        }
    }

    private const float SlideCycle = 9.5f;

    private static readonly Vector3[] WayBack =
    {
        new(0, 0, SlideZ - 3.9f),
        new(2.6f, 0, SlideZ - 2.9f),
        new(2.6f, 0, SlideZ + 1.7f),
        new(0, 0, SlideZ + 1.7f),
    };

    private int slidePhase = -1;

    /// <summary>The slide's child: up the ladder, a hop on the platform, down the slide, a cheer, and back round.</summary>
    private void TickSlide()
    {
        if (slideChild == null)
            return;
        float t = time % SlideCycle;
        float b = slideBase;
        var ladderBase = new Vector3(0, b, SlideZ + 1.7f);
        var ladderTop = new Vector3(0, b + SlideHeight, SlideZ + .95f);
        var platform = new Vector3(0, b + SlideHeight + .07f, SlideZ);
        var runTop = new Vector3(0, b + SlideHeight + .1f, SlideZ - .8f);
        var runBottom = new Vector3(0, b + .2f, SlideZ - 3.7f);
        Vector3 position;
        float yaw = Mathf.Pi;
        bool running = false;
        int phase;
        if (t < 2.4f)
        {
            phase = 0;
            position = ladderBase.Lerp(ladderTop, t / 2.4f);
            yaw = 0;
            running = true;
        }
        else if (t < 3.0f)
        {
            phase = 1;
            position = ladderTop.Lerp(platform, (t - 2.4f) / .6f);
            running = true;
        }
        else if (t < 3.8f)
        {
            // A hop on the platform before the ride.
            phase = 2;
            position = platform.Lerp(runTop, Mathf.Clamp((t - 3.4f) / .4f, 0, 1));
        }
        else if (t < 4.9f)
        {
            phase = 3;
            float u = (t - 3.8f) / 1.1f;
            position = runTop.Lerp(runBottom, u * u * .6f + u * .4f);
        }
        else if (t < 5.9f)
        {
            // A cheer at the bottom.
            phase = 4;
            position = runBottom with { Y = b };
        }
        else
        {
            phase = 5;
            // Back round to the foot of the ladder along the way back.
            float total = 0;
            for (int i = 0; i < WayBack.Length - 1; i++)
                total += WayBack[i].DistanceTo(WayBack[i + 1]);
            float walk = (t - 5.9f) / (SlideCycle - 5.9f) * total;
            position = WayBack[^1];
            yaw = 0;
            for (int i = 0; i < WayBack.Length - 1; i++)
            {
                float leg = WayBack[i].DistanceTo(WayBack[i + 1]);
                if (walk <= leg)
                {
                    var direction = WayBack[i + 1] - WayBack[i];
                    position = WayBack[i].Lerp(WayBack[i + 1], walk / leg);
                    yaw = Mathf.Atan2(direction.X, direction.Z);
                    break;
                }
                walk -= leg;
            }
            position.Y = b;
            running = true;
        }
        if (phase != slidePhase && phase is 2 or 4)
            slideChild.Celebrate();
        slidePhase = phase;
        slideChild.Running = running;
        slideChild.Position = position;
        slideChild.Rotation = new Vector3(0, yaw, 0);
    }

    /// <summary>
    /// The children cheer when the car comes near, each time it arrives. <paramref name="car"/> is the car's position.
    /// </summary>
    internal void Tick(Vector3 car)
    {
        bool near = new Vector2(car.X - Position.X, car.Z - Position.Z).Length() < CheerDistance;
        if (near && !wasNear)
            foreach (var (stage, who) in children)
                if (stage < Stage)
                    who.Celebrate();
        wasNear = near;
    }

    /// <summary>All the children hop at once, as when the last toy has been built.</summary>
    internal void CheerAll()
    {
        foreach (var (_, who) in children)
            who.Celebrate();
    }
}
