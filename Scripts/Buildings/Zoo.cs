using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The village zoo, which starts as a construction site: a gravel path from the ring between six enclosures, three on each side. Every enclosure
/// needs two deliveries of different goods, the first for its fence and ground and the second for the animal and its fittings: penguins on their ice, monkeys on a climbing
/// frame, an elephant who sprays water, a giraffe who nods at her feeding tower, flamingos in a pond with a brick bridge
/// and pandas in a bamboo grove. Visitors stroll along the path once it is built. The frame has +Z pointing away from
/// the road and +X to the right, like the playground's.
/// </summary>
public sealed partial class Zoo : Node3D
{
    public const int Trips = ZooPlans.Trips;

    /// <summary>Centres of the six enclosures in the zoo's frame (x across, z along): penguins, monkeys, elephant, giraffe, flamingos, pandas.</summary>
    internal static readonly (float X, float Z)[] Pens =
    {
        (-6.9f, 8.9f),
        (6.9f, 8.9f),
        (-6.9f, 17.1f),
        (6.9f, 17.1f),
        (-6.9f, 25.3f),
        (6.9f, 25.3f),
    };

    internal const float PenWidth = 9.2f,
        PenDepth = 7.6f;

    /// <summary>How far from the zoo a car makes the animals cheer.</summary>
    private const float CheerDistance = 34;

    internal const float PathStart = 3.4f,
        PathEnd = 28.5f;

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D[] stages = new Node3D[Trips];
    private readonly List<CollisionShape3D>[] solids = new List<CollisionShape3D>[Trips];
    private readonly List<Beast> beasts = new();
    private readonly ZooPens pens;
    private readonly List<(int Stage, Character Who, float Offset)> visitors = new();

    private Node3D marks = null!;
    private Label3D sign = null!;
    private Node3D gate = null!;
    private Node3D? digArm;
    private float time;
    private float cheer;
    private bool wasNear;

    /// <summary>One animal of the zoo: where it stands, how it paces and what moves on it.</summary>
    internal sealed class Beast
    {
        public int Stage;
        public Node3D Root = null!;
        public Vector3 Home;
        public float Yaw;
        public Vector3 Dir = Vector3.Right;
        public float Amp,
            Speed = 1,
            Phase;

        // What the animation of the animal sets each frame.
        public float Pitch,
            Roll,
            Lift;
        public Action<float>? Animate;
    }

    internal ZooSite Site { get; }

    /// <summary>Deliveries made so far.</summary>
    public int Stage { get; private set; }

    public bool Complete => Stage >= Trips;

    /// <summary>The keeper who sends the trailer and thanks the driver.</summary>
    internal Character Helper { get; set; } = null!;

    internal Vector3 Drop => Site.Position;

    internal Vector3 Road => Site.Road;

    /// <summary>The middle of the zoo, for celebrations and the camera.</summary>
    internal Vector3 Focus => ToGlobal(new Vector3(0, 3.2f, 12));

    /// <summary>Whether the construction site stands (until the zoo is finished) and whether the gate does (only then).</summary>
    internal bool SiteStands => marks.Visible;

    internal bool GateStands => gate.Visible;

    /// <summary>True while the animals are hopping because a car has come or a new animal has moved in, for the checks.</summary>
    internal bool Cheering => cheer > 0;

    /// <summary>True while the elephant's trunk is up and the water sprays, for the checks.</summary>
    internal bool Spraying { get; set; }

    /// <summary>How far the monkey's swing is swung, for the checks.</summary>
    internal float SwingAngle => pens.Swing?.Rotation.X ?? 0;

    /// <summary>The animals (of every kind) who have moved in so far.</summary>
    internal int AnimalsHome
    {
        get
        {
            int count = 0;
            foreach (var beast in beasts)
                if (beast.Stage < Stage)
                    count++;
            return count;
        }
    }

    /// <summary>How many animals live in the enclosure of <paramref name="stage"/>.</summary>
    internal int AnimalsIn(int stage)
    {
        int count = 0;
        foreach (var beast in beasts)
            if (beast.Stage == stage)
                count++;
        return count;
    }

    /// <summary>Where the first animal of an enclosure stands now (zoo frame), for the checks.</summary>
    internal Vector3 FirstAnimalPosition(int stage)
    {
        foreach (var beast in beasts)
            if (beast.Stage == stage)
                return beast.Root.Position;
        return Vector3.Zero;
    }

    /// <summary>The visitors who stroll along the path.</summary>
    internal int VisitorsWalking
    {
        get
        {
            int count = 0;
            foreach (var (stage, _, _) in visitors)
                if (stage < Stage)
                    count++;
            return count;
        }
    }

    internal Vector3 FirstVisitorPosition => visitors.Count > 0 ? visitors[0].Who.Position : Vector3.Zero;

    internal Zoo(ZooSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "Zoo";
        Position = site.Position;
        float yaw = Mathf.Atan2(site.Inward.X, site.Inward.Z);
        Rotation = new Vector3(0, yaw, 0);
        basis = new Basis(Vector3.Up, yaw);
        for (int i = 0; i < Trips; i++)
            solids[i] = new List<CollisionShape3D>();
        pens = new ZooPens(this, stages);
    }

    /// <summary>Shows the enclosures of <paramref name="stage"/> delivered loads; a restored game simply has them.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Trips);
        if (IsInsideTree())
            Refresh();
    }

    // ----- the ground under the enclosures -------------------------------------------------------------------------

    /// <summary>Height above the ring of the ground below a point given in the zoo's own frame.</summary>
    private float Floor(float x, float z)
    {
        var global = Position + basis * new Vector3(x, 0, z);
        return ground.Height(global.X, global.Z) - Position.Y;
    }

    internal float Highest(float x, float z, float width, float depth)
    {
        float top = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
            top = Mathf.Max(top, Floor(x + dx * width, z + dz * depth));
        return top + .05f;
    }

    /// <summary>A thick flat slab under an enclosure, so that it stands level; returns the height of its top.</summary>
    internal float Pad(Node3D parent, float x, float z, float width, float depth, string color)
    {
        float top = Highest(x, z, width, depth);
        Art.Block(parent, new(x, top - 2.5f, z), new(width, 5, depth), color);
        return top;
    }

    /// <summary>A solid box that blocks the car once <paramref name="stage"/> (0-5) has been built.</summary>
    internal void Solid(int stage, Vector3 center, Vector3 size)
    {
        var body = Obstacles.Add(stages[stage], center, new BoxShape3D { Size = size });
        var shape = (CollisionShape3D)body.GetChild(0);
        shape.Disabled = stage >= Stage;
        solids[stage].Add(shape);
    }

    // ----- building ------------------------------------------------------------------------------------------------

    public override void _Ready()
    {
        marks = new Node3D { Name = "ZooMarks" };
        AddChild(marks);
        BuildMarks();
        for (int i = 0; i < Trips; i++)
        {
            stages[i] = new Node3D { Name = "ZooStage" + i, Visible = false };
            AddChild(stages[i]);
        }
        pens.Build();
        BuildVisitors();
        BuildGate();
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
        gate.Visible = Complete;
        sign.Text = $"Staveniště zoo  {Stage} / {Trips}";
    }

    private static readonly (float X, float Z)[] Corners =
    {
        (-12.8f, 4.5f),
        (-12.8f, 29.6f),
        (12.8f, 29.6f),
        (12.8f, 4.5f),
    };

    /// <summary>
    /// The construction site, which is all there is of the zoo on the first day: pegs with red and white tape on three
    /// sides, a barrier across the path, a sign that counts the deliveries, heaps of gravel, planks and bricks, a
    /// wheelbarrow, a cement mixer, a hut for the builders and a little excavator whose arm digs. It lies in the strip
    /// in front of the enclosures and is cleared away when the zoo is finished.
    /// </summary>
    private void BuildMarks()
    {
        var pegs = new Vector3[Corners.Length];
        for (int i = 0; i < Corners.Length; i++)
        {
            pegs[i] = new Vector3(Corners[i].X, Floor(Corners[i].X, Corners[i].Z), Corners[i].Z);
            Art.Cylinder(marks, pegs[i] + Vector3.Up * .6f, .07f, .06f, 1.2f, "a77a59");
            Art.Ball(marks, pegs[i] + Vector3.Up * 1.25f, new(.11f, .07f, .11f), "e2563b");
        }
        for (int side = 0; side < 3; side++)
        {
            var from = pegs[side] + Vector3.Up * .95f;
            var to = pegs[side + 1] + Vector3.Up * .95f;
            int count = Mathf.CeilToInt(from.DistanceTo(to) / 1.1f);
            for (int i = 0; i < count; i++)
                Art.Beam(
                    marks,
                    from.Lerp(to, i / (float)count),
                    from.Lerp(to, (i + 1) / (float)count),
                    .06f,
                    i % 2 == 0 ? "e2563b" : "fff5df"
                );
        }

        // The barrier across the path, with cones, and the sign that counts the deliveries.
        const float bz = 3.3f;
        float by = Floor(0, bz);
        foreach (float x in new[] { -2.0f, 2.0f })
        {
            Art.Cylinder(marks, new(x, by + .55f, bz), .07f, .07f, 1.1f, "6b6f78");
            Art.Block(marks, new(x, by + .06f, bz), new(.6f, .12f, .6f), "6b6f78");
        }
        for (int i = 0; i < 6; i++)
            Art.Block(
                marks,
                new(-1.67f + i * .67f, by + .95f, bz),
                new(.66f, .14f, .06f),
                i % 2 == 0 ? "e2563b" : "fff5df"
            );
        foreach (float x in new[] { -3.0f, 3.0f })
        {
            float cy = Floor(x, bz - .6f);
            Art.Cylinder(marks, new(x, cy + .3f, bz - .6f), .04f, .17f, .6f, "f08a3c");
            Art.Cylinder(marks, new(x, cy + .38f, bz - .6f), .12f, .13f, .1f, "fff5df");
            Art.Block(marks, new(x, cy + .02f, bz - .6f), new(.42f, .04f, .42f), "3a3d45");
        }
        float sy = Floor(-3.4f, 0.4f);
        Art.Cylinder(marks, new(-3.4f, sy + 1.0f, .4f), .06f, .07f, 2.0f, "8a5f3f");
        Art.Block(marks, new(-3.4f, sy + 2.4f, .4f), new(.1f, 1.0f, 3.0f), "f2c14e");
        Art.Block(marks, new(-3.4f, sy + 2.4f, .4f), new(.14f, .86f, 2.84f), "fff5df");
        sign = Art.Sign(marks, new(-3.4f, sy + 2.4f, .4f), "", 24);

        // Heaps of gravel, planks and bricks on the left, a wheelbarrow and the cement mixer.
        float gy = Floor(-8.4f, .6f);
        Art.Ball(marks, new(-8.4f, gy + .15f, .6f), new(1.5f, .75f, 1.2f), "9c968c");
        Art.Ball(marks, new(-7.5f, gy + .1f, 1.5f), new(.8f, .45f, .7f), "b3ad9c");
        for (int i = 0; i < 4; i++)
            Art.Block(
                marks,
                new(-10.2f, Floor(-10.2f, .6f) + .1f + i * .14f, .6f),
                new(2.2f, .12f, .5f + (i % 2) * .1f),
                i % 2 == 0 ? "c39771" : "b0855c"
            );
        for (int i = 0; i < 3; i++)
        for (int j = 0; j < 3 - i; j++)
            Art.Block(
                marks,
                new(-7.9f + j * .5f + i * .25f, Floor(-7.9f, 3.1f) + .12f + i * .24f, 3.1f),
                new(.46f, .22f, .24f),
                "c9795c"
            );
        var barrow = new Vector3(-5.9f, Floor(-5.9f, 3.6f), 3.6f);
        Art.Block(marks, barrow + new Vector3(0, .5f, 0), new(.7f, .3f, .9f), "4f8fb8");
        Art.Beam(marks, barrow + new Vector3(-.3f, .5f, -.3f), barrow + new Vector3(-.3f, .55f, -1.3f), .05f, "a77a59");
        Art.Beam(marks, barrow + new Vector3(.3f, .5f, -.3f), barrow + new Vector3(.3f, .55f, -1.3f), .05f, "a77a59");
        Art.Cylinder(marks, barrow + new Vector3(0, .22f, .5f), .22f, .22f, .1f, "3a3d45").RotationDegrees = new(
            0,
            0,
            90
        );
        var mixer = new Vector3(-11.0f, Floor(-11.0f, 3.2f), 3.2f);
        Art.Block(marks, mixer + new Vector3(0, .5f, 0), new(1.1f, .1f, 1.5f), "6b6f78");
        foreach (float x in new[] { -.45f, .45f })
            Art.Cylinder(marks, mixer + new Vector3(x, .2f, .5f), .22f, .22f, .1f, "3a3d45").RotationDegrees = new(
                0,
                0,
                90
            );
        Art.Cylinder(marks, mixer + new Vector3(0, 1.1f, 0), .55f, .3f, 1.0f, "e2563b").RotationDegrees = new(35, 0, 0);
        Art.Cylinder(marks, mixer + new Vector3(0, 1.28f, -.2f), .35f, .35f, .06f, "3a3d45").RotationDegrees = new(
            35,
            0,
            0
        );

        // The hut for the builders on the right and the excavator, whose arm digs up and down.
        var hut = new Vector3(10.6f, Floor(10.6f, 1.0f), 1.0f);
        Art.Block(marks, hut + new Vector3(0, .9f, 0), new(2.6f, 1.8f, 2.0f), "f2c14e");
        Art.Block(marks, hut + new Vector3(0, 1.88f, 0), new(2.8f, .16f, 2.2f), "6b6f78");
        Art.Block(marks, hut + new Vector3(-.5f, .8f, -1.02f), new(.7f, 1.4f, .06f), "8a5f3f");
        Art.Block(marks, hut + new Vector3(.6f, 1.1f, -1.02f), new(.8f, .6f, .06f), "4f8fb8");
        Art.Cylinder(marks, hut + new Vector3(.9f, 2.05f, .3f), .32f, .32f, .05f, "f2c14e");
        Art.Ball(marks, hut + new Vector3(.9f, 2.1f, .3f), new(.3f, .12f, .3f), "f2c14e");
        var digger = new Vector3(8.2f, Floor(8.2f, 2.6f), 2.6f);
        Art.Block(marks, digger + new Vector3(0, .25f, 0), new(1.0f, .5f, 2.2f), "3a3d45");
        Art.Block(marks, digger + new Vector3(0, .8f, 0), new(1.5f, .6f, 1.5f), "f2c14e");
        Art.Block(marks, digger + new Vector3(.2f, 1.5f, .3f), new(.95f, .85f, .95f), "f2c14e");
        Art.Block(marks, digger + new Vector3(.2f, 1.55f, -.17f), new(.8f, .6f, .06f), "9fd3e8");
        digArm = new Node3D { Position = digger + new Vector3(-.35f, 1.1f, -.6f), Rotation = new(-.3f, 0, 0) };
        marks.AddChild(digArm);
        Art.Beam(digArm, Vector3.Zero, new(0, .9f, -1.4f), .26f, "f2c14e");
        Art.Beam(digArm, new(0, .9f, -1.4f), new(0, -.4f, -2.3f), .2f, "f2c14e");
        Art.Block(digArm, new(0, -.6f, -2.4f), new(.7f, .4f, .5f), "6b6f78");
    }

    /// <summary>
    /// The gate over the path with the sign of the zoo and a panda and a giraffe on the pillars. It is the grand
    /// opening: it appears only when the last delivery has been made and the construction site is cleared away.
    /// </summary>
    private void BuildGate()
    {
        gate = new Node3D { Name = "ZooGate" };
        AddChild(gate);
        const float z = 3.0f;
        float y = Floor(0, z);
        foreach (float x in new[] { -2.3f, 2.3f })
        {
            float foot = Floor(x, z);
            Art.Cylinder(gate, new(x, foot + 1.7f, z), .22f, .26f, 3.4f, "b0855c");
            Art.Block(gate, new(x, foot + .25f, z), new(.8f, .5f, .8f), "9c7450");
        }
        Art.Block(gate, new(0, y + 3.3f, z), new(5.2f, .5f, .35f), "6dbb5f");
        Art.Block(gate, new(0, y + 3.05f, z), new(5.0f, .1f, .3f), "fff1d2");
        Art.Sign(gate, new(0, y + 4.5f, z), "Zoo", 34);
        // The panda on the left pillar.
        float left = Floor(-2.3f, z) + 3.75f;
        Art.Ball(gate, new(-2.3f, left, z), new(.48f, .42f, .4f), "f8f8f3");
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Ball(gate, new(-2.3f + side * .33f, left + .36f, z), Vector3.One * .15f, "23252b");
            Art.Ball(gate, new(-2.3f + side * .17f, left + .05f, z - .36f), new(.1f, .075f, .04f), "23252b");
            Art.Ball(gate, new(-2.3f + side * .17f, left + .06f, z - .39f), Vector3.One * .024f, "ffffff");
        }
        Art.Ball(gate, new(-2.3f, left - .06f, z - .4f), new(.07f, .05f, .04f), "23252b");
        // The giraffe's head on the right pillar.
        float right = Floor(2.3f, z) + 3.85f;
        Art.Ball(gate, new(2.3f, right, z), new(.34f, .4f, .34f), "f2c14e");
        Art.Ball(gate, new(2.3f, right - .12f, z - .3f), new(.2f, .17f, .2f), "f6d98f");
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Cylinder(gate, new(2.3f + side * .12f, right + .5f, z), .03f, .03f, .3f, "f2c14e");
            Art.Ball(gate, new(2.3f + side * .12f, right + .68f, z), Vector3.One * .06f, "8a5a3b");
            Art.Ball(gate, new(2.3f + side * .3f, right + .15f, z), new(.12f, .05f, .06f), "f2c14e");
            Art.Ball(gate, new(2.3f + side * .2f, right + .08f, z - .27f), Vector3.One * .04f, "23252b");
        }
        foreach (var (x, c) in new[] { (-2.3f, "d9432f"), (2.3f, "4f8fb8") })
        {
            float foot = Floor(x, z);
            Art.Ball(gate, new(x, foot + 1.0f, z - .27f), new(.1f, .1f, .04f), c);
        }
    }

    // ----- small helpers for the animals ---------------------------------------------------------------------------

    internal Beast NewBeast(
        int stage,
        Node3D parent,
        Vector3 home,
        float yaw,
        float scale,
        float phase = 0,
        Vector3? dir = null,
        float amp = 0,
        float speed = 1
    )
    {
        var root = new Node3D
        {
            Position = home,
            Rotation = new Vector3(0, yaw, 0),
            Scale = Vector3.One * scale,
        };
        parent.AddChild(root);
        var beast = new Beast
        {
            Stage = stage,
            Root = root,
            Home = home,
            Yaw = yaw,
            Dir = dir ?? Vector3.Right,
            Amp = amp,
            Speed = speed,
            Phase = phase,
        };
        beasts.Add(beast);
        return beast;
    }

    // ----- the visitors on the path -------------------------------------------------------------------------------

    private static readonly (int Kind, string Fur, int Stage, float Offset)[] VisitorSpecs =
    {
        (0, "f3e8d5", 1, 0f),
        (1, "e8a070", 5, 17f),
        (2, "c49a74", 9, 31f),
    };

    private void BuildVisitors()
    {
        foreach (var (kind, fur, stage, offset) in VisitorSpecs)
        {
            var visitor = Character.Create("", "", kind, fur);
            visitor.Scale = Vector3.One * .38f;
            visitor.Guide(false);
            visitor.Visible = false;
            stages[stage].AddChild(visitor);
            visitors.Add((stage, visitor, offset));
            PlaceVisitor(visitor, offset);
        }
    }

    private const float PathLength = PathEnd - PathStart;

    private void PlaceVisitor(Character visitor, float travelled)
    {
        float u = travelled % (PathLength * 2);
        bool forward = u < PathLength;
        float z = PathStart + (forward ? u : PathLength * 2 - u);
        float lane = forward ? .55f : -.55f;
        visitor.Position = new Vector3(lane, Floor(lane, z) + .04f, z);
        visitor.Rotation = new Vector3(0, forward ? 0 : Mathf.Pi, 0);
        visitor.Running = true;
    }

    // ----- life ----------------------------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (Helper != null && Helper.AnimationPaused)
            return;
        float dt = (float)delta;
        time += dt;
        cheer = Mathf.Max(0, cheer - dt);
        if (digArm != null && !Complete)
            digArm.Rotation = new Vector3(-.3f + Mathf.Sin(time * .9f) * .28f, 0, 0);
        if (pens.Swing != null && Stage > 3)
            pens.Swing.Rotation = new Vector3(Mathf.Sin(time * 1.5f) * .55f, 0, 0);
        foreach (var beast in beasts)
        {
            if (beast.Stage >= Stage)
                continue;
            float angle = time * beast.Speed + beast.Phase;
            float yaw = beast.Yaw;
            var offset = Vector3.Zero;
            if (beast.Amp > 0)
            {
                offset = beast.Dir * (Mathf.Sin(angle) * beast.Amp);
                var heading = beast.Dir * (Mathf.Cos(angle) >= 0 ? 1 : -1);
                yaw = Mathf.Atan2(heading.X, heading.Z);
            }
            beast.Pitch = beast.Roll = beast.Lift = 0;
            beast.Animate?.Invoke(time);
            float hop = cheer > 0 ? Mathf.Abs(Mathf.Sin(cheer * 8 + beast.Phase)) * .4f * Mathf.Min(1, cheer) : 0;
            beast.Root.Position = beast.Home + offset + Vector3.Up * (beast.Lift + hop);
            beast.Root.Rotation = new Vector3(beast.Pitch, yaw, beast.Roll);
        }
        foreach (var (stage, who, offset) in visitors)
        {
            who.Visible = stage < Stage;
            if (stage < Stage)
                PlaceVisitor(who, time * .85f + offset);
        }
    }

    /// <summary>The animals cheer when the car comes near, each time it arrives. <paramref name="car"/> is the car's position.</summary>
    internal void Tick(Vector3 car)
    {
        bool near = new Vector2(car.X - Position.X, car.Z - Position.Z).Length() < CheerDistance;
        if (near && !wasNear && Stage > 0)
            CheerAll();
        wasNear = near;
    }

    /// <summary>All the animals hop at once, as when a new animal has moved in.</summary>
    internal void CheerAll()
    {
        cheer = 2.2f;
        foreach (var (stage, who, _) in visitors)
            if (stage < Stage)
                who.Celebrate();
    }
}
