using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Settlement;

/// <summary>
/// One building plot of the village. It starts as a staked-out lot with a newcomer waiting beside it; every
/// delivery of material shows the next stage of a little house, and the last one lets the newcomer move in.
/// The plot's frame has +Z pointing at the road, so the door faces the street.
/// </summary>
public sealed partial class HousePlot : Node3D
{
    public const int Trips = PlotPlans.Trips;

    // The house stands slightly behind the middle of the lot so that the yard lies between it and the street.
    private const float Width = 6,
        Depth = 5,
        CenterZ = -1.2f,
        Height = 2.6f,
        Thickness = .3f;

    private static readonly (string Roof, string Accent)[] Looks =
    {
        ("eb644b", "4f8fb8"),
        ("6c8fd4", "d9604f"),
        ("5aa86f", "e8a23c"),
        ("d99a45", "6a5aa8"),
        ("a86fb8", "4fa89b"),
        ("d9718f", "5d8a4f"),
    };

    private readonly Terrain ground;
    private readonly Node3D[] stages = new Node3D[Trips];
    private readonly List<CollisionShape3D>[] solids = new List<CollisionShape3D>[Trips];
    private Node3D marks = null!;
    private Label3D progress = null!;
    private float foundation;

    internal int Index { get; }

    /// <summary>Deliveries made so far.</summary>
    public int Stage { get; private set; }

    public bool Complete => Stage >= Trips;

    /// <summary>The newcomer who waits on the lot and moves into the finished house.</summary>
    internal Character Resident { get; set; } = null!;

    /// <summary>The road point from which the way to the lot starts.</summary>
    internal Vector3 Road { get; }

    /// <summary>The ring in front of the lot where the trailer tips out its load.</summary>
    internal Vector3 Drop { get; }

    /// <summary>Where the newcomer waits beside the empty lot, and where they stand once they live here.</summary>
    internal Vector3 WaitSpot { get; set; }

    internal Vector3 HomeSpot { get; set; }

    /// <summary>The middle of the house, for celebrations and the camera.</summary>
    internal Vector3 Focus => ToGlobal(new Vector3(0, 2.5f, CenterZ));

    /// <summary>The newcomer's place for the stage that has been reached.</summary>
    internal Vector3 ResidentSpot => Complete ? HomeSpot : WaitSpot;

    internal HousePlot(int index, Transform3D plot, Vector3 road, Terrain ground)
    {
        Index = index;
        Road = road;
        this.ground = ground;
        Name = "BuildPlot" + index;
        float highest = float.NegativeInfinity,
            lowest = float.PositiveInfinity;
        for (float x = -Width / 2 - .4f; x <= Width / 2 + .4f; x += .4f)
        for (float z = CenterZ - Depth / 2 - .4f; z <= CenterZ + Depth / 2 + .4f; z += .4f)
        {
            var p = plot * new Vector3(x, 0, z);
            float y = ground.Height(p.X, p.Z);
            highest = Mathf.Max(highest, y);
            lowest = Mathf.Min(lowest, y);
        }
        foundation = highest - lowest + .4f;
        Transform = new Transform3D(plot.Basis, new Vector3(plot.Origin.X, highest + .15f, plot.Origin.Z));
        Drop = Ground(plot, 0, 5);
        WaitSpot = Ground(plot, -3.9f, 3.3f);
        HomeSpot = Ground(plot, 1.9f, 3.3f);
        for (int i = 0; i < Trips; i++)
            solids[i] = new List<CollisionShape3D>();
    }

    private Vector3 Ground(Transform3D plot, float x, float z)
    {
        var p = plot * new Vector3(x, 0, z);
        return ground.At(p.X, p.Z);
    }

    /// <summary>
    /// Shows the work for <paramref name="stage"/> delivered loads; all six finish the house. The newcomer stands
    /// on the lot until the house is finished. With <paramref name="walkIn"/> they walk to the doorstep, otherwise
    /// (a restored game) they are simply there.
    /// </summary>
    internal void SetStage(int stage, bool walkIn = false)
    {
        Stage = Math.Clamp(stage, 0, Trips);
        if (IsInsideTree())
            Refresh();
        walking = false;
        if (Resident == null)
            return;
        Resident.Running = false;
        if (walkIn && Complete)
            walking = true;
        else
            Resident.Position = ResidentSpot;
    }

    private bool walking;

    /// <summary>The newcomer walks from the lot to the doorstep of the new house.</summary>
    public override void _Process(double delta)
    {
        if (!walking || Resident.AnimationPaused)
            return;
        var toHome = HomeSpot - Resident.Position;
        toHome.Y = 0;
        float step = 2.4f * (float)delta;
        if (toHome.Length() <= step)
        {
            walking = false;
            Resident.Running = false;
            Resident.Position = HomeSpot;
            Resident.Rotation = new Vector3(0, Rotation.Y, 0);
            return;
        }
        Resident.Running = true;
        Resident.Position += toHome.Normalized() * step;
        Resident.Rotation = new Vector3(0, Mathf.Atan2(toHome.X, toHome.Z), 0);
    }

    public override void _Ready()
    {
        marks = new Node3D { Name = "LotMarks" };
        AddChild(marks);
        BuildMarks();
        for (int i = 0; i < Trips; i++)
        {
            stages[i] = new Node3D { Name = "PlotStage" + i, Visible = false };
            AddChild(stages[i]);
        }
        BuildSlab(stages[0]);
        BuildLowerWalls(stages[1]);
        BuildUpperWalls(stages[2]);
        BuildRoofFrame(stages[3]);
        BuildRoof(stages[4]);
        BuildFinish(stages[5]);
        progress = Art.Sign(this, new(3.9f, 2.6f, 4.2f), "", 27);
        Refresh();
    }

    private Node3D? windowLights;

    /// <summary>
    /// The windows of the finished house shine in the evening. The lights are built the first time they are needed
    /// and hang from the lot itself, not from a stage, so they never show on a house that is not finished.
    /// </summary>
    internal void ShowNightLight(bool on)
    {
        if (windowLights == null)
        {
            if (!on || !IsInsideTree())
                return;
            windowLights = new Node3D { Name = "WindowLights" };
            AddChild(windowLights);
            float frontZ = CenterZ + Depth / 2;
            foreach (float x in new[] { -1.825f, 1.825f })
                Art.Lamp(windowLights, new(x, 1.7f, frontZ + .112f), new(.94f, .74f, .02f), "ffd77a");
            foreach (int side in new[] { -1, 1 })
                Art.Lamp(windowLights, new(side * (Width / 2 + .122f), 1.7f, CenterZ), new(.02f, .74f, .94f), "ffd77a");
        }
        if (windowLights.Visible != on)
            windowLights.Visible = on;
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
        progress.Text = Complete ? "✓  Domeček" : $"Parcela  {Stage} / {Trips}";
    }

    /// <summary>A solid box that blocks the car once <paramref name="stage"/> (0-5) has been built.</summary>
    private void Solid(int stage, Vector3 center, Vector3 size)
    {
        var body = Obstacles.Add(stages[stage], center, new BoxShape3D { Size = size });
        var shape = (CollisionShape3D)body.GetChild(0);
        shape.Disabled = stage >= Stage;
        solids[stage].Add(shape);
    }

    /// <summary>Pegs and a rope on three sides; the side to the street stays open for the trailer.</summary>
    private void BuildMarks()
    {
        var corners = new Vector3[]
        {
            new(-4.3f, 0, -4.4f),
            new(4.3f, 0, -4.4f),
            new(4.3f, 0, 1.9f),
            new(-4.3f, 0, 1.9f),
        };
        var pegs = new Vector3[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            var global = ToGlobal(corners[i]);
            pegs[i] = new Vector3(corners[i].X, ground.Height(global.X, global.Z) - Position.Y, corners[i].Z);
            Art.Cylinder(marks, pegs[i] + Vector3.Up * .45f, .07f, .06f, .9f, "a77a59");
            Art.Ball(marks, pegs[i] + Vector3.Up * .92f, new(.1f, .06f, .1f), Looks[Index].Accent);
        }
        for (int i = 0; i < 3; i++)
            Art.Beam(marks, pegs[i] + Vector3.Up * .75f, pegs[i + 1] + Vector3.Up * .75f, .035f, "f2ddb0");
        // A pair of boards left beside the lot, and the post that carries the plot's sign.
        Art.Block(marks, new(5.0f, .12f, -1.2f), new(.7f, .16f, 1.8f), "c39771");
        Art.Block(marks, new(5.0f, .28f, -1.2f), new(.6f, .14f, 1.7f), "b0855c");
        var post = new Vector3(3.9f, 0, 4.2f);
        Art.Cylinder(this, post + Vector3.Up * 1.1f, .09f, .09f, 2.2f, "a77a59");
    }

    /// <summary>Stage 1: gravel and a slab that levels the ground for the walls.</summary>
    private void BuildSlab(Node3D stage)
    {
        float slabDepth = foundation;
        Art.Block(stage, new(0, -slabDepth / 2, CenterZ), new(Width + .6f, slabDepth, Depth + .6f), "b7ab96");
        Art.Block(stage, new(0, .02f, CenterZ), new(Width + .3f, .05f, Depth + .3f), "cfc4ad");
        foreach (var heap in new[] { new Vector3(-3.9f, 0, -3.6f), new Vector3(-3.4f, 0, -2.7f) })
            Art.Ball(stage, heap + Vector3.Up * .1f, new(.75f, .4f, .7f), "9c968c");
        Solid(0, new(0, -slabDepth / 2 + .02f, CenterZ), new(Width + .6f, slabDepth, Depth + .6f));
    }

    /// <summary>The four walls as boxes; the front is split in two, leaving a doorway 1.3 m wide.</summary>
    private void Walls(Node3D stage, float bottom, float height, string color)
    {
        float y = bottom + height / 2;
        float back = CenterZ - Depth / 2 + Thickness / 2,
            frontZ = CenterZ + Depth / 2 - Thickness / 2;
        Art.Block(stage, new(0, y, back), new(Width, height, Thickness), color);
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(stage, new(side * (Width / 2 - Thickness / 2), y, CenterZ), new(Thickness, height, Depth), color);
            Art.Block(stage, new(side * 1.825f, y, frontZ), new(2.35f, height, Thickness), color);
        }
    }

    /// <summary>Stage 2: the lower half of the brick walls, with the doorway open.</summary>
    private void BuildLowerWalls(Node3D stage)
    {
        Walls(stage, 0, Height / 2, "c9795c");
        Solid(1, new(0, Height / 2, CenterZ), new(Width, Height, Depth));
    }

    /// <summary>Stage 3: the upper half, a lintel over the doorway and dark openings where the windows will be.</summary>
    private void BuildUpperWalls(Node3D stage)
    {
        Walls(stage, Height / 2, Height / 2, "d4856a");
        float frontZ = CenterZ + Depth / 2;
        Art.Block(stage, new(0, 2.3f, frontZ - Thickness / 2), new(1.3f, .6f, Thickness), "d4856a");
        foreach (float x in new[] { -1.825f, 1.825f })
            Art.Block(stage, new(x, 1.7f, frontZ + .01f), new(1.2f, 1, .06f), "35444c");
        foreach (int side in new[] { -1, 1 })
            Art.Block(stage, new(side * (Width / 2 + .01f), 1.7f, CenterZ), new(.06f, 1, 1.2f), "35444c");
    }

    /// <summary>Stage 4: rafters and the ridge beam of the roof.</summary>
    private void BuildRoofFrame(Node3D stage)
    {
        const float ridge = Height + 1.9f;
        float back = CenterZ - Depth / 2,
            front = CenterZ + Depth / 2;
        for (int i = 0; i < 5; i++)
        {
            float z = Mathf.Lerp(back + .3f, front - .3f, i / 4f);
            foreach (int side in new[] { -1, 1 })
                Art.Beam(stage, new(side * (Width / 2 + .35f), Height, z), new(0, ridge, z), .16f, "c39771");
        }
        Art.Beam(stage, new(0, ridge, back), new(0, ridge, front), .2f, "b0855c");
        foreach (int side in new[] { -1, 1 })
            Art.Beam(
                stage,
                new(side * (Width / 2 - Thickness / 2), Height + .08f, back),
                new(side * (Width / 2 - Thickness / 2), Height + .08f, front),
                .16f,
                "b0855c"
            );
    }

    /// <summary>Stage 5: the roof, in a colour of its own for every plot, and a chimney.</summary>
    private void BuildRoof(Node3D stage)
    {
        const float ridge = Height + 1.9f;
        float back = CenterZ - Depth / 2 - .35f,
            front = CenterZ + Depth / 2 + .35f,
            eave = Width / 2 + .45f;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] v =
        {
            new(-eave, Height - .05f, back),
            new(eave, Height - .05f, back),
            new(0, ridge + .1f, back),
            new(-eave, Height - .05f, front),
            new(eave, Height - .05f, front),
            new(0, ridge + .1f, front),
        };
        int[] indices = { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 2, 5, 4, 2, 4, 1 };
        foreach (int i in indices)
            st.AddVertex(v[i]);
        st.GenerateNormals();
        var roof = Art.Mesh(stage, st.Commit(), Vector3.Zero, Vector3.One, Looks[Index].Roof);
        var material = (StandardMaterial3D)Art.Mat(Looks[Index].Roof).Duplicate();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        roof.MaterialOverride = material;
        Art.Block(stage, new(1.7f, Height + 1.5f, CenterZ - 1.3f), new(.7f, 1.7f, .7f), "b35d4b");
        Art.Block(stage, new(1.7f, Height + 2.4f, CenterZ - 1.3f), new(.9f, .18f, .9f), "8c4a3d");
    }

    /// <summary>Stage 6: door, windows with shutters, flower boxes, a doorstep and a mailbox: the house is ready to live in.</summary>
    private void BuildFinish(Node3D stage)
    {
        var look = Looks[Index];
        float frontZ = CenterZ + Depth / 2;
        Art.Block(stage, new(0, 1, frontZ - .12f), new(1.1f, 2, .12f), look.Accent);
        Art.Ball(stage, new(.35f, 1, frontZ - .02f), Vector3.One * .07f, "f5d36b");
        Art.Block(stage, new(0, .06f, frontZ + .55f), new(1.8f, .12f, 1.1f), "c5b598");
        foreach (float x in new[] { -1.825f, 1.825f })
        {
            Art.Block(stage, new(x, 1.7f, frontZ + .03f), new(1.3f, 1.1f, .08f), "fff1d2");
            Art.Block(stage, new(x, 1.7f, frontZ + .08f), new(1.0f, .8f, .06f), "88c7d8");
            Art.Block(stage, new(x, 1.7f, frontZ + .12f), new(.08f, .85f, .06f), "fff1d2");
            foreach (int side in new[] { -1, 1 })
                Art.Block(stage, new(x + side * .85f, 1.7f, frontZ + .06f), new(.4f, 1.1f, .08f), look.Accent);
            Art.Block(stage, new(x, 1.05f, frontZ + .25f), new(1.2f, .22f, .3f), "a87b61");
            foreach (float flower in new[] { -.4f, 0, .4f })
                Art.Ball(stage, new(x + flower, 1.24f, frontZ + .25f), new(.13f, .13f, .13f), "f56f8f");
        }
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * (Width / 2 + .03f);
            Art.Block(stage, new(x, 1.7f, CenterZ), new(.08f, 1.1f, 1.3f), "fff1d2");
            Art.Block(stage, new(x + side * .05f, 1.7f, CenterZ), new(.06f, .8f, 1.0f), "88c7d8");
            foreach (float z in new[] { CenterZ - Depth / 2 + .1f, CenterZ + Depth / 2 - .1f })
                Art.Block(stage, new(side * (Width / 2 + .06f), Height / 2, z), new(.2f, Height, .2f), "fff1d2");
        }
        // The mailbox stands on the verge, painted in the plot's colour.
        Art.Cylinder(stage, new(-2.4f, .5f, 3.8f), .05f, .05f, 1, "a77a59");
        Art.Block(stage, new(-2.4f, 1.1f, 3.8f), new(.5f, .35f, .35f), look.Accent);
    }
}
