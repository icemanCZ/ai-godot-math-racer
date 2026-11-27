using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The castle on the hill (<c>docs/CASTLE_PLAN.md</c>), raised by <see cref="CastlePlans.Trips"/> deliveries: the
/// foundations, a stone plinth, the walls in two courses and their battlements, four round corner towers, the gatehouse
/// and its drawbridge, the two storeys of the palace with its great square keep, the windows and roofs, the flags with
/// the feast table and the well, and the lanterns. Until it is finished a building site stands round it: a treadwheel
/// crane with a squirrel running in its wheel, heaps of stone, scaffolding and the sign of the site. Once finished, the
/// drawbridge comes down when the car comes near, the lanterns and windows shine at night, the great fire on the keep
/// burns once every beacon of the borders does, the guests of the feast sit at the table and fireworks burst over the
/// towers at night after the full feast.
/// </summary>
/// <remarks>
/// The node stands at <see cref="CastleSite.Center"/>, turned by <see cref="CastleSite.Yaw"/>: local -Z points to the
/// gate and the road, +X to the right. All the parts are made at once and <see cref="SetStage"/> only shows and hides
/// them, so it may be called before the node is in the tree and as often as needed. The solid parts block the car only
/// once their stage is built. The walls stand on a plinth that reaches down to the ground, so no ground is levelled.
/// </remarks>
public partial class Castle : Node3D
{
    internal const int Stages = CastlePlans.Trips;

    private const float Half = CastleSite.HalfWidth;
    private const float WallThickness = 2;
    private const float WallHeight = 6;
    private const float TowerRadius = 3.4f;
    private const float TowerHeight = 12.5f;
    private const float GateHalf = 3.2f;
    private const float BridgeLength = 6.5f;

    /// <summary>The drawbridge comes down when the car is this near the gate, and inside the courtyard.</summary>
    private const float BridgeReach = 46;

    private const string Stone = "a8a29a",
        DarkStone = "8a857e",
        Wood = "8a6040",
        Roof = "b2473a",
        Plaster = "efe3c8";

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D[] parts = new Node3D[Stages + 1];
    private readonly List<CollisionShape3D>[] solids = new List<CollisionShape3D>[Stages + 1];
    private readonly Node3D marks = new() { Name = "CastleSite" };
    private readonly List<CollisionShape3D> markSolids = new();
    private readonly Node3D scaffold = new() { Name = "Scaffold" };
    private readonly Node3D wheel = new() { Name = "TreadWheel" };
    private readonly Node3D squirrel = new() { Name = "Squirrel" };
    private readonly Node3D hinge = new() { Name = "DrawbridgeHinge" };
    private readonly Node3D torchFlames = new() { Name = "TorchFlames", Visible = false };
    private readonly Node3D keepFire = new() { Name = "KeepFire", Visible = false };
    private readonly List<MeshInstance3D> tongues = new();
    private readonly List<Node3D> flags = new();
    private readonly Node3D?[] seated = new Node3D?[CastlePlans.Guests];
    private readonly List<(Vector3 Position, float Yaw)> chairs = new();
    private readonly Burst[] bursts = new Burst[3];
    private readonly OmniLight3D gateLight = new()
    {
        Name = "GateLight",
        LightColor = new Color("ffc06a"),
        OmniRange = 18,
        LightEnergy = 0,
        ShadowEnabled = false,
        Visible = false,
    };
    private readonly OmniLight3D fireLight = new()
    {
        Name = "KeepFireLight",
        LightColor = new Color("ff9a3c"),
        OmniRange = 45,
        LightEnergy = 0,
        ShadowEnabled = false,
        Visible = false,
    };
    private Label3D sign = null!;
    private float gateY;
    private float bridgeDown;
    private float bridgeAngle;
    private float keepTop;
    private float time;
    private float nextBurst = 1;
    private readonly RandomNumberGenerator sparkle = new() { Seed = 4711 };

    internal CastleSite Site { get; }

    /// <summary>The deliveries the castle has had, 0 to <see cref="Stages"/>.</summary>
    internal int Stage { get; private set; }

    internal bool Complete => Stage >= Stages;

    /// <summary>Whether the great fire on the keep burns: every beacon of the borders does.</summary>
    internal bool KeepFire { get; set; }

    /// <summary>Whether fireworks burst over the towers at night: the feast is full.</summary>
    internal bool Fireworks { get; set; }

    /// <summary>How far the drawbridge is down, 0 standing up to 1 lying on the ground (after the last <see cref="Tick"/>).</summary>
    internal float BridgeDown => bridgeDown;

    /// <summary>Whether the lanterns burn now (after the last <see cref="Tick"/>).</summary>
    internal bool Lit => torchFlames.Visible;

    /// <summary>Whether the great fire on the keep burns now (after the last <see cref="Tick"/>).</summary>
    internal bool FireBurning => keepFire.Visible;

    /// <summary>The middle of the castle above the courtyard, for the camera of a celebration.</summary>
    internal Vector3 Focus => Position + Vector3.Up * 8;

    /// <summary>The top of the keep, where the great fire burns.</summary>
    internal Vector3 TopFocus => Position + basis * new Vector3(0, keepTop + 2, 11.8f);

    /// <summary>The guest seated at each chair of the feast, or null.</summary>
    internal bool Seated(int chair) => seated[chair] != null;

    internal Castle(CastleSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "Castle";
        Position = ground.At(site.Center.X, site.Center.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        for (int i = 0; i <= Stages; i++)
        {
            parts[i] = new Node3D { Name = "CastleStage" + i };
            AddChild(parts[i]);
            solids[i] = new List<CollisionShape3D>();
        }
        AddChild(marks);
        marks.AddChild(scaffold);
        gateY = Floor(0, -Half);
        BuildPlan();
        BuildWalls();
        for (int tower = 0; tower < 4; tower++)
            BuildTower(tower);
        BuildGatehouse();
        BuildDrawbridge();
        BuildPalace();
        BuildCourtyard();
        BuildLanterns();
        BuildSiteMarks();
        for (int i = 0; i < bursts.Length; i++)
            bursts[i] = new Burst(this);
        SetStage(0);
    }

    /// <summary>Height above the node of the ground under a point of the castle's frame.</summary>
    private float Floor(float x, float z)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    /// <summary>The lowest and the highest ground over a box of the castle's frame.</summary>
    private (float Low, float High) Span(float x0, float z0, float x1, float z1)
    {
        float low = float.MaxValue,
            high = float.MinValue;
        for (int i = 0; i <= 4; i++)
        for (int j = 0; j <= 4; j++)
        {
            float h = Floor(Mathf.Lerp(x0, x1, i / 4f), Mathf.Lerp(z0, z1, j / 4f));
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        return (low, high);
    }

    /// <summary>A small part that is not drawn from afar, where the castle shows its plain shape.</summary>
    private static MeshInstance3D Small(MeshInstance3D mesh)
    {
        mesh.VisibilityRangeEnd = 280;
        return mesh;
    }

    /// <summary>A solid shape that blocks the car once <paramref name="stage"/> is built.</summary>
    private void Solid(int stage, Vector3 center, Shape3D shape)
    {
        var body = Obstacles.Add(parts[stage], center, shape);
        solids[stage].Add((CollisionShape3D)body.GetChild(0));
    }

    private void Solid(int stage, Vector3 center, Vector3 size) => Solid(stage, center, new BoxShape3D { Size = size });

    /// <summary>Raises the castle to a number of deliveries: at once from a save, or after a load is tipped out.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Stages);
        parts[0].Visible = Stage == 0;
        for (int i = 1; i <= Stages; i++)
        {
            parts[i].Visible = i <= Stage;
            foreach (var shape in solids[i])
                shape.Disabled = i > Stage;
        }
        marks.Visible = !Complete;
        foreach (var shape in markSolids)
            shape.Disabled = Complete;
        scaffold.Visible = Stage is >= 3 and < Stages;
        sign.Text = Texts.SignCastle + "\n" + $"{Stage} / {Stages}";
        // The drawbridge lies on the ground while the castle is built; once finished it stands up until the car comes.
        bridgeDown = 1;
        PlaceBridge();
    }

    // ---- Stage 0: the plan of the castle ----

    /// <summary>Stakes with red tips along the lines of the walls and a string between them.</summary>
    private void BuildPlan()
    {
        var plan = parts[0];
        for (int side = 0; side < 4; side++)
        for (int i = 0; i <= 8; i++)
        {
            var (x, z) = Around(side, i / 8f);
            float y = Floor(x, z);
            Art.Cylinder(plan, new(x, y + .35f, z), .06f, .05f, .9f, "a77a59");
            Art.Ball(plan, new(x, y + .82f, z), new(.1f, .06f, .1f), "e25b45");
            if (i == 8)
                continue;
            var (nx, nz) = Around(side, (i + 1) / 8f);
            var from = new Vector3(x, y + .6f, z);
            var to = new Vector3(nx, Floor(nx, nz) + .6f, nz);
            Art.Beam(plan, from, to, .025f, "f3ead2");
        }
    }

    /// <summary>A point of the wall line: side 0 is the front, 1 the right, 2 the back and 3 the left, run clockwise.</summary>
    private static (float X, float Z) Around(int side, float t) =>
        side switch
        {
            0 => (Mathf.Lerp(-Half, Half, t), -Half),
            1 => (Half, Mathf.Lerp(-Half, Half, t)),
            2 => (Mathf.Lerp(Half, -Half, t), Half),
            _ => (-Half, Mathf.Lerp(Half, -Half, t)),
        };

    // ---- Stages 1 to 5: the foundations, the plinth and the walls ----

    /// <summary>
    /// The walls in pieces of about four metres between the towers, each on its own ground: a bed of gravel (stage 1), a
    /// stone plinth from below the ground (stage 2), two courses of stone (stages 3 and 4) and the battlements on top
    /// with the walk behind them (stage 5). The front wall leaves the gate open.
    /// </summary>
    private void BuildWalls()
    {
        for (int side = 0; side < 4; side++)
        {
            float start = TowerRadius - .6f,
                end = 2 * Half - TowerRadius + .6f;
            var pieces = new List<(float From, float To)>();
            if (side == 0)
            {
                pieces.AddRange(Split(start, Half - GateHalf - 2.2f));
                pieces.AddRange(Split(Half + GateHalf + 2.2f, end));
            }
            else
                pieces.AddRange(Split(start, end));
            foreach (var (from, to) in pieces)
                BuildWallPiece(side, from, to);
        }
    }

    private static IEnumerable<(float, float)> Split(float from, float to)
    {
        int count = Mathf.Max(1, Mathf.RoundToInt((to - from) / 4));
        float step = (to - from) / count;
        for (int i = 0; i < count; i++)
            yield return (from + i * step, from + (i + 1) * step);
    }

    private void BuildWallPiece(int side, float from, float to)
    {
        var (ax, az) = Around(side, from / (2 * Half));
        var (bx, bz) = Around(side, to / (2 * Half));
        var a = new Vector3(ax, 0, az);
        var b = new Vector3(bx, 0, bz);
        var middle = (a + b) / 2;
        float length = a.DistanceTo(b) + .3f;
        bool alongX = side is 0 or 2;
        var (low, high) = alongX
            ? Span(Mathf.Min(ax, bx), az - 1, Mathf.Max(ax, bx), az + 1)
            : Span(ax - 1, Mathf.Min(az, bz), ax + 1, Mathf.Max(az, bz));
        float bottom = low - .8f,
            plinthTop = high + .7f,
            top = plinthTop + WallHeight;
        Vector3 Size(float thick, float height) => alongX ? new(length, height, thick) : new(thick, height, length);
        Vector3 At(float y, float outward = 0)
        {
            var normal = side switch
            {
                0 => Vector3.Forward,
                1 => Vector3.Right,
                2 => Vector3.Back,
                _ => Vector3.Left,
            };
            return middle + Vector3.Up * y + normal * outward;
        }
        // Stage 1: the bed of gravel along the line of the wall.
        var bed = Art.Block(parts[1], At(high + .05f), Size(WallThickness + .8f, .3f), "b9b4a5");
        bed.Scale = new Vector3(bed.Scale.X, high - low + .35f, bed.Scale.Z);
        bed.Position = At((high + low) / 2 - .05f);
        // Stage 2: the plinth of rough stone.
        Art.Wall(
            parts[2],
            At((bottom + plinthTop) / 2, .15f),
            Size(WallThickness + .3f, plinthTop - bottom),
            DarkStone,
            FacadeStyle.Bricks
        );
        // Stages 3 and 4: the two courses of the wall.
        float course = WallHeight / 2;
        for (int c = 0; c < 2; c++)
        {
            float y0 = plinthTop + c * course;
            Art.Wall(parts[3 + c], At(y0 + course / 2), Size(WallThickness, course), Stone, FacadeStyle.Bricks);
        }
        Solid(3, At((bottom + top) / 2), Size(WallThickness + .3f, top - bottom));
        // Stage 5: the walk on top and the battlements on its outer edge.
        Art.Block(parts[5], At(top + .06f), Size(WallThickness - .1f, .12f), "8f7f6a");
        int merlons = Mathf.Max(1, Mathf.RoundToInt(length / 2));
        for (int m = 0; m < merlons; m++)
        {
            float t = (m + .5f) / merlons - .5f;
            var along = alongX ? new Vector3(t * length, 0, 0) : new Vector3(0, 0, t * length);
            var merlon = Art.Block(parts[5], At(top + .5f, WallThickness / 2 - .3f) + along, Vector3.One, Stone);
            merlon.Scale = alongX ? new(1.0f, 1.0f, .6f) : new(.6f, 1.0f, 1.0f);
            Small(merlon);
        }
        // The scaffolding stands outside the front and the sides while the castle is being built.
        if (side != 2)
            for (int pole = 0; pole < 2; pole++)
            {
                var foot = At(0, WallThickness / 2 + 1.1f) + (pole == 0 ? a - middle : b - middle) * .9f;
                float y = Floor(foot.X, foot.Z);
                Art.Cylinder(scaffold, new(foot.X, y + 3.6f, foot.Z), .08f, .08f, 7.2f, "b48a5a");
                foreach (float level in new[] { 2.6f, 5.2f })
                    Art.Block(scaffold, At(y + level, WallThickness / 2 + 1.1f), Size(.5f, .08f), "c49a64");
            }
    }

    // ---- Stages 6 to 9: the corner towers ----

    private static readonly Vector2[] Corners =
    {
        new(-Half, -Half),
        new(Half, -Half),
        new(Half, Half),
        new(-Half, Half),
    };

    /// <summary>A round tower at a corner: its plinth, the body with slit windows, the battlements and the cone roof.</summary>
    private void BuildTower(int tower)
    {
        var part = parts[6 + tower];
        var corner = Corners[tower];
        var (low, high) = Span(
            corner.X - TowerRadius,
            corner.Y - TowerRadius,
            corner.X + TowerRadius,
            corner.Y + TowerRadius
        );
        float bottom = low - .8f,
            body = high + .7f,
            top = body + TowerHeight;
        var at = new Vector3(corner.X, 0, corner.Y);
        Art.Cylinder(
            part,
            at + Vector3.Up * ((bottom + body) / 2),
            TowerRadius + .35f,
            TowerRadius + .3f,
            body - bottom,
            DarkStone
        );
        var shaft = Art.Cylinder(
            part,
            at + Vector3.Up * ((body + top) / 2),
            TowerRadius,
            TowerRadius - .15f,
            TowerHeight,
            Stone
        );
        shaft.MaterialOverride = Art.Facade(Stone, FacadeStyle.Bricks);
        // A ring of battlements round the top, and a red cone roof inside it.
        Art.Cylinder(part, at + Vector3.Up * (top + .1f), TowerRadius + .25f, TowerRadius + .25f, .2f, "8f7f6a");
        for (int m = 0; m < 10; m++)
        {
            float angle = m * Mathf.Tau / 10;
            var merlon = Art.Block(
                part,
                at + new Vector3(Mathf.Sin(angle) * TowerRadius, top + .65f, Mathf.Cos(angle) * TowerRadius),
                new(1.0f, 1.0f, .55f),
                Stone
            );
            merlon.Rotation = new Vector3(0, angle, 0);
            Small(merlon);
        }
        Art.Cylinder(part, at + Vector3.Up * (top + 2.6f), TowerRadius - .3f, .05f, 4.6f, Roof);
        // Slit windows, dark, on the side away from the middle of the castle.
        var outward = new Vector3(corner.X, 0, corner.Y).Normalized();
        foreach (float y in new[] { body + 3.5f, body + 7.5f, body + 10.5f })
        {
            var slit = Art.Block(
                part,
                at + outward * (TowerRadius - .05f) + Vector3.Up * y,
                new(.35f, 1.3f, .3f),
                "3a332c"
            );
            slit.Rotation = new Vector3(0, Mathf.Atan2(outward.X, outward.Z), 0);
            Small(slit);
        }
        Solid(
            6 + tower,
            at + Vector3.Up * ((bottom + top) / 2),
            new CylinderShape3D { Radius = TowerRadius + .2f, Height = top - bottom }
        );
        // The flag on the tip of the roof, put up with the table (stage 15).
        Flag(parts[15], at + Vector3.Up * (top + 4.8f), 2.4f, tower % 2 == 0 ? "e2563b" : "f2c14e");
    }

    /// <summary>A pole with a pennant that waves in the wind.</summary>
    private void Flag(Node3D part, Vector3 foot, float pole, string color)
    {
        Small(Art.Cylinder(part, foot + Vector3.Up * (pole / 2), .05f, .04f, pole, "5a4636"));
        Small(Art.Ball(part, foot + Vector3.Up * pole, Vector3.One * .13f, "f2c14e"));
        var cloth = new Node3D { Position = foot + Vector3.Up * (pole - .15f) };
        part.AddChild(cloth);
        var mesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { new(0, 0, 0), new(0, -.4f, 1.5f), new(0, -.8f, 0) };
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var material = (StandardMaterial3D)Art.Mat(color).Duplicate();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var pennant = Art.Mesh(cloth, mesh, Vector3.Zero, Vector3.One, color);
        pennant.MaterialOverride = material;
        pennant.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        Small(pennant);
        flags.Add(cloth);
    }

    // ---- Stages 10 and 11: the gatehouse and the drawbridge ----

    /// <summary>Two square pillars either side of the gate, the arch over it with battlements, and the raised grille.</summary>
    private void BuildGatehouse()
    {
        var part = parts[10];
        var (low, high) = Span(-GateHalf - 2.4f, -Half - 1.8f, GateHalf + 2.4f, -Half + 1.8f);
        float bottom = low - .8f,
            top = high + .7f + WallHeight + 2.5f;
        foreach (int side in new[] { -1, 1 })
        {
            var at = new Vector3(side * (GateHalf + 1.1f), (bottom + top) / 2, -Half);
            Art.Wall(part, at, new(2.2f, top - bottom, 3.4f), Stone, FacadeStyle.Bricks);
            Solid(10, at, new Vector3(2.3f, top - bottom, 3.5f));
            for (int m = 0; m < 2; m++)
                Small(
                    Art.Block(
                        part,
                        new(side * (GateHalf + .6f + m * 1.1f), top + .45f, -Half - 1.35f),
                        new(.8f, .9f, .6f),
                        Stone
                    )
                );
        }
        float openTop = gateY + 5.2f;
        Art.Wall(
            part,
            new(0, (openTop + top) / 2, -Half),
            new(2 * GateHalf + .2f, top - openTop, 3.4f),
            Stone,
            FacadeStyle.Bricks
        );
        // The grille, raised up under the arch: dark bars with spikes showing below.
        for (int bar = -3; bar <= 3; bar++)
            Small(Art.Block(part, new(bar * .85f, openTop - .25f, -Half - .4f), new(.12f, .9f, .12f), "3b3530"));
        Small(Art.Block(part, new(0, openTop + .1f, -Half - .4f), new(2 * GateHalf, .14f, .14f), "3b3530"));
        // The coat of arms over the gate: a red shield with a golden crown.
        Art.Block(part, new(0, openTop + 1.4f, -Half - 1.75f), new(1.4f, 1.6f, .12f), "c23b30");
        Small(Art.Block(part, new(0, openTop + 1.65f, -Half - 1.83f), new(.8f, .3f, .06f), "f2c14e"));
        Art.Ball(part, new(0, openTop + .75f, -Half - 1.75f), new(.7f, .5f, .12f), "c23b30");
    }

    /// <summary>The drawbridge of planks on two chains, hinged at the foot of the gate and turned by <see cref="Tick"/>.</summary>
    private void BuildDrawbridge()
    {
        var part = parts[11];
        hinge.Position = new Vector3(0, gateY + .12f, -Half - 1.75f);
        part.AddChild(hinge);
        Art.Block(hinge, new(0, .1f, -BridgeLength / 2), new(2 * GateHalf - .4f, .2f, BridgeLength), "9a6c45");
        for (int plank = 0; plank < 7; plank++)
            Small(Art.Block(hinge, new(0, .21f, -.5f - plank * .9f), new(2 * GateHalf - .5f, .03f, .08f), "6a4a30"));
        foreach (int side in new[] { -1, 1 })
            Small(
                Art.Block(
                    hinge,
                    new(side * (GateHalf - .3f), .25f, -BridgeLength / 2),
                    new(.14f, .14f, BridgeLength),
                    "5a3d26"
                )
            );
        float end = Floor(0, hinge.Position.Z - BridgeLength) - hinge.Position.Y;
        bridgeAngle = Mathf.Atan2(end, BridgeLength);
        PlaceBridge();
    }

    private void PlaceBridge() => hinge.Rotation = new Vector3(Mathf.Lerp(Mathf.Pi / 2, bridgeAngle, bridgeDown), 0, 0);

    // ---- Stages 12 to 14: the palace and its keep ----

    private const float PalaceFront = 8.6f,
        PalaceBack = Half - WallThickness / 2,
        PalaceHalf = 11,
        StoreyHeight = 4.5f;

    /// <summary>
    /// The palace along the back wall: the ground floor with its door (stage 12), the upper floor and the great square
    /// keep that rises from its middle (stage 13), and the windows, the roof and the keep's basket of fire (stage 14).
    /// </summary>
    private void BuildPalace()
    {
        float depth = PalaceBack - PalaceFront,
            middleZ = (PalaceFront + PalaceBack) / 2;
        var (low, high) = Span(-PalaceHalf, PalaceFront, PalaceHalf, PalaceBack);
        float bottom = low - .6f,
            floor = high + .5f;
        Art.Wall(
            parts[12],
            new(0, (bottom + floor + StoreyHeight) / 2, middleZ),
            new(2 * PalaceHalf, floor + StoreyHeight - bottom, depth),
            Plaster,
            FacadeStyle.Plaster
        );
        Art.Block(parts[12], new(0, floor + 1.25f, PalaceFront - .06f), new(1.8f, 2.5f, .12f), "6a4a30");
        Small(Art.Ball(parts[12], new(.5f, floor + 1.3f, PalaceFront - .16f), Vector3.One * .12f, "f2c14e"));
        Art.Block(parts[12], new(0, floor + .15f, PalaceFront - .9f), new(3.2f, .3f, 1.6f), DarkStone);
        Solid(
            12,
            new(0, (bottom + floor + 2 * StoreyHeight) / 2, middleZ),
            new Vector3(2 * PalaceHalf + .2f, floor + 2 * StoreyHeight - bottom, depth + .2f)
        );
        float upper = floor + StoreyHeight;
        Art.Wall(
            parts[13],
            new(0, upper + StoreyHeight / 2, middleZ),
            new(2 * PalaceHalf, StoreyHeight, depth),
            Plaster,
            FacadeStyle.Plaster
        );
        Art.Block(parts[13], new(0, upper + .05f, PalaceFront - .2f), new(2 * PalaceHalf + .2f, .25f, .5f), "c7b48f");
        // The keep, square and tall, from the ground through the palace.
        const float keep = 5;
        keepTop = floor + 20;
        Art.Wall(
            parts[13],
            new(0, (floor + keepTop) / 2, middleZ),
            new(keep, keepTop - floor, keep),
            Stone,
            FacadeStyle.Bricks
        );
        Art.Block(parts[13], new(0, keepTop + .1f, middleZ), new(keep + .5f, .2f, keep + .5f), "8f7f6a");
        for (int side = 0; side < 4; side++)
        for (int m = -1; m <= 1; m++)
        {
            var offset = side switch
            {
                0 => new Vector3(m * 1.7f, 0, -keep / 2),
                1 => new Vector3(keep / 2, 0, m * 1.7f),
                2 => new Vector3(m * 1.7f, 0, keep / 2),
                _ => new Vector3(-keep / 2, 0, m * 1.7f),
            };
            Small(Art.Block(parts[13], new Vector3(0, keepTop + .65f, middleZ) + offset, new(.9f, .9f, .9f), Stone));
        }
        // Stage 14: windows on both floors and on the keep, the roof and the basket of fire on the keep.
        foreach (float x in new[] { -8.5f, -5f, 5f, 8.5f })
        {
            Art.Window(parts[14], new(x, floor + 2.2f, PalaceFront - .07f), new(1.3f, 1.7f, .1f), "9fc8d8");
            Art.Window(parts[14], new(x, upper + 2.3f, PalaceFront - .07f), new(1.3f, 1.7f, .1f), "9fc8d8");
            Small(Art.Block(parts[14], new(x, floor + 1.25f, PalaceFront - .15f), new(1.5f, .12f, .2f), "c7b48f"));
        }
        foreach (float y in new[] { upper + 6, upper + 9.5f, upper + 13 })
            if (y < keepTop - 1)
                Art.Window(
                    parts[14],
                    new(0, y, PalaceFront + (depth - keep) / 2 - .07f),
                    new(1.1f, 1.5f, .1f),
                    "9fc8d8"
                );
        float roofTop = upper + StoreyHeight;
        foreach (int side in new[] { -1, 1 })
        {
            float width = PalaceHalf - keep / 2 + .3f;
            var roof = Art.Mesh(
                parts[14],
                Prism,
                new(side * (keep / 2 + width / 2 - .15f), roofTop + 1.6f, middleZ),
                new(depth + .8f, 3.2f, width),
                Roof
            );
            roof.RotationDegrees = new(0, 90, 0);
            roof.MaterialOverride = Art.RoofTiles(Roof, Plaster);
        }
        var basket = new Vector3(0, keepTop + .2f, middleZ);
        Art.Cylinder(parts[14], basket + Vector3.Up * .6f, .7f, 1.2f, 1.0f, "3b3530");
        Art.Cylinder(parts[14], basket + Vector3.Up * .1f, .25f, .25f, .4f, "3b3530");
        keepFire.Position = basket + Vector3.Up * 1.0f;
        parts[14].AddChild(keepFire);
        Flames(keepFire, 1.8f);
        keepFire.AddChild(fireLight);
        fireLight.Position = Vector3.Up * 2;
        // The keep's great flag.
        Flag(parts[15], new Vector3(keep / 2 - .3f, keepTop + .2f, middleZ + keep / 2 - .3f), 4.2f, "4f8fb8");
    }

    private static readonly PrismMesh Prism = new();

    /// <summary>Cones of flame one inside the other, for a lantern or the keep's basket; they flicker in <see cref="Tick"/>.</summary>
    private void Flames(Node3D parent, float size)
    {
        foreach (
            var (radius, height, color) in new[]
            {
                (.42f, 1.3f, "ff6a1a"),
                (.28f, 1.0f, "ffb02e"),
                (.15f, .7f, "fff2a0"),
            }
        )
        {
            var cone = Art.Cylinder(
                parent,
                Vector3.Up * (height * size / 2),
                radius * size,
                .02f,
                height * size,
                color
            );
            cone.MaterialOverride = Glow(color, .9f);
            cone.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            tongues.Add(cone);
        }
    }

    private static StandardMaterial3D Glow(string color, float alpha = 1) =>
        new()
        {
            AlbedoColor = new Color(color).SrgbToLinear() with { A = alpha },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = alpha < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        };

    // ---- Stage 15: the courtyard ----

    /// <summary>The well, the long feast table with seven chairs, and the banner over the gate.</summary>
    private void BuildCourtyard()
    {
        var part = parts[15];
        // The well on the left of the courtyard: a ring of stone, two posts and a little roof.
        var well = new Vector3(-9, Floor(-9, -5), -5);
        Art.Cylinder(part, well + Vector3.Up * .45f, 1.15f, 1.15f, .9f, DarkStone);
        Art.Cylinder(part, well + Vector3.Up * .92f, .9f, .9f, .05f, "3f6f8f");
        foreach (int side in new[] { -1, 1 })
            Art.Cylinder(part, well + new Vector3(side * 1.0f, 1.5f, 0), .08f, .08f, 2.2f, Wood);
        var wellRoof = Art.Mesh(part, Prism, well + Vector3.Up * 2.85f, new(2.6f, .9f, 1.6f), Roof);
        wellRoof.MaterialOverride = Art.RoofTiles(Roof, Wood);
        Solid(15, well + Vector3.Up * .9f, new CylinderShape3D { Radius = 1.3f, Height = 1.8f });
        // The table on the right, along the courtyard, with three chairs on each side and one at its head.
        const float tableX = 8.5f,
            tableZ = -1,
            tableLength = 9;
        float y = Floor(tableX, tableZ);
        Art.Block(part, new(tableX, y + .95f, tableZ), new(2.2f, .14f, tableLength), "a8754a");
        Art.Block(part, new(tableX, y + 1.03f, tableZ), new(1.5f, .02f, tableLength - .6f), "f3ead2");
        foreach (int sx in new[] { -1, 1 })
        foreach (int sz in new[] { -1, 1 })
            Art.Block(
                part,
                new(tableX + sx * .9f, y + .45f, tableZ + sz * (tableLength / 2 - .4f)),
                new(.16f, .9f, .16f),
                "7a5536"
            );
        Solid(15, new(tableX, y + .6f, tableZ), new Vector3(2.4f, 1.2f, tableLength + .2f));
        for (int i = 0; i < 3; i++)
        {
            float z = tableZ - 3 + i * 3;
            chairs.Add((new Vector3(tableX - 1.9f, 0, z), Mathf.Pi / 2));
            chairs.Add((new Vector3(tableX + 1.9f, 0, z), -Mathf.Pi / 2));
        }
        chairs.Add((new Vector3(tableX, 0, tableZ + tableLength / 2 + 1.4f), Mathf.Pi));
        foreach (var (at, yaw) in chairs)
        {
            float floor = Floor(at.X, at.Z);
            var chair = new Node3D { Position = at with { Y = floor }, Rotation = new Vector3(0, yaw, 0) };
            part.AddChild(chair);
            Art.Block(chair, new(0, .5f, 0), new(1.0f, .12f, 1.0f), "8a5a3a");
            Art.Block(chair, new(0, 1.15f, -.45f), new(1.0f, 1.3f, .12f), "8a5a3a");
            Small(Art.Ball(chair, new(0, 1.85f, -.45f), new(.2f, .2f, .1f), "f2c14e"));
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Small(Art.Block(chair, new(sx * .4f, .25f, sz * .4f), new(.1f, .5f, .1f), "6a4a30"));
        }
        // Plates and goblets on the table, one for every chair.
        foreach (var (at, _) in chairs)
        {
            var toward = new Vector3(tableX, 0, tableZ) - at;
            var plate = at + toward.Normalized() * (Mathf.Abs(toward.Z) > Mathf.Abs(toward.X) ? 1.7f : 1.25f);
            Small(Art.Cylinder(part, new(plate.X, y + 1.06f, plate.Z), .3f, .3f, .03f, "fff5df"));
            Small(Art.Cylinder(part, new(plate.X + .35f, y + 1.2f, plate.Z + .2f), .09f, .06f, .3f, "f2c14e"));
        }
        // The banner over the gate, inside the courtyard.
        Art.Block(part, new(0, gateY + 7.2f, -Half + 1.15f), new(2.2f, 2.6f, .08f), "4f8fb8");
        Small(Art.Block(part, new(0, gateY + 7.5f, -Half + 1.1f), new(.9f, .45f, .04f), "f2c14e"));
    }

    // ---- Stage 16: the lanterns ----

    /// <summary>Glass lanterns on brackets beside the gate, on the front towers and in the courtyard, with their flames.</summary>
    private void BuildLanterns()
    {
        var part = parts[16];
        var spots = new List<Vector3>
        {
            new(-(GateHalf + 1.1f), gateY + 3.6f, -Half - 1.85f),
            new(GateHalf + 1.1f, gateY + 3.6f, -Half - 1.85f),
            new(-Half + TowerRadius * .7f, Floor(-Half, -Half) + 4.5f, -Half - TowerRadius * .7f),
            new(Half - TowerRadius * .7f, Floor(Half, -Half) + 4.5f, -Half - TowerRadius * .7f),
        };
        foreach (var (x, z) in new[] { (-4f, 2f), (4f, 2f) })
        {
            // A lamp post in the courtyard.
            float floor = Floor(x, z);
            Art.Cylinder(part, new(x, floor + 1.4f, z), .08f, .06f, 2.8f, "3b3530");
            spots.Add(new(x, floor + 2.95f, z));
        }
        foreach (var spot in spots)
        {
            Small(Art.Block(part, spot + new Vector3(0, .5f, 0), new(.5f, .08f, .5f), "3b3530"));
            Art.Window(part, spot, new(.4f, .8f, .4f), "efe6c4");
            Small(Art.Block(part, spot - new Vector3(0, .46f, 0), new(.46f, .1f, .46f), "3b3530"));
            var flame = new Node3D { Position = spot + Vector3.Down * .3f };
            torchFlames.AddChild(flame);
            Flames(flame, .4f);
        }
        part.AddChild(torchFlames);
        gateLight.Position = new Vector3(0, gateY + 4, -Half - 3.5f);
        part.AddChild(gateLight);
    }

    // ---- The building site ----

    /// <summary>
    /// What stands round the castle until it is finished: the treadwheel crane outside the front right tower with a
    /// squirrel in its wheel and a stone hanging from its jib, heaps of stone and the sign with the count of the loads.
    /// </summary>
    private void BuildSiteMarks()
    {
        var crane = new Vector3(10.5f, Floor(10.5f, -24), -24);
        var frame = new Node3D { Position = crane };
        marks.AddChild(frame);
        // Two A-frames hold the axle of the wheel.
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * 1.1f;
            Art.Beam(frame, new(x, 0, -1.4f), new(x, 3, 0), .16f, "8a6040");
            Art.Beam(frame, new(x, 0, 1.4f), new(x, 3, 0), .16f, "8a6040");
        }
        wheel.Position = new Vector3(0, 3, 0);
        frame.AddChild(wheel);
        var rim = Art.Mesh(
            wheel,
            new TorusMesh
            {
                InnerRadius = 2.45f,
                OuterRadius = 2.7f,
                Rings = 24,
                RingSegments = 6,
            },
            Vector3.Zero,
            new(1, 1, 1),
            "9a6c45"
        );
        rim.RotationDegrees = new(0, 0, 90);
        for (int spoke = 0; spoke < 6; spoke++)
        {
            var bar = Art.Block(wheel, Vector3.Zero, new(.12f, 5.1f, .12f), "8a6040");
            bar.Rotation = new Vector3(spoke * Mathf.Pi / 6, 0, 0);
        }
        for (int rung = 0; rung < 16; rung++)
        {
            float angle = rung * Mathf.Tau / 16;
            Small(
                Art.Block(
                    wheel,
                    new(0, Mathf.Cos(angle) * 2.55f, Mathf.Sin(angle) * 2.55f),
                    new(1.5f, .1f, .14f),
                    "c49a64"
                )
            );
        }
        // The squirrel runs at the bottom of the wheel, inside it, so the wheel turns under her feet.
        squirrel.Position = new Vector3(0, .55f, 0);
        frame.AddChild(squirrel);
        Art.Ball(squirrel, new(0, .5f, 0), new(.4f, .5f, .55f), "c46a2e");
        Art.Ball(squirrel, new(0, .95f, .35f), new(.32f, .3f, .3f), "c46a2e");
        Art.Ball(squirrel, new(0, .88f, .58f), new(.14f, .1f, .1f), "f3e2c8");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(squirrel, new(side * .14f, 1.2f, .3f), new(.08f, .18f, .06f), "c46a2e");
            Art.Ball(squirrel, new(side * .12f, 1.0f, .6f), Vector3.One * .05f, "2a2522");
        }
        var tail = Art.Ball(squirrel, new(0, .95f, -.5f), new(.3f, .65f, .3f), "d9813f");
        tail.Rotation = new Vector3(-.35f, 0, 0);
        // The jib leans over the front wall with a rope and a block of stone.
        Art.Beam(frame, new(0, 3, 0), new(-2.5f, 9.5f, 6.5f), .2f, "8a6040");
        Art.Beam(frame, new(-2.5f, 9.5f, 6.5f), new(-2.5f, 6.4f, 6.5f), .04f, "e8dcc0");
        Art.Block(frame, new(-2.5f, 5.9f, 6.5f), new(.9f, .8f, .9f), Stone);
        var footing = Obstacles.Add(frame, new Vector3(0, 1.5f, 0), new BoxShape3D { Size = new(2.6f, 3, 3.2f) });
        markSolids.Add((CollisionShape3D)footing.GetChild(0));
        // Heaps of stone and the sign of the site, on the other side of the way.
        foreach (var (x, z, size) in new[] { (-11f, -22f, 1.4f), (-13.5f, -24.5f, 1.1f), (-9.5f, -25f, .9f) })
            Art.Ball(marks, new(x, Floor(x, z) + size * .35f, z), new(size * 1.6f, size, size * 1.4f), DarkStone);
        var post = new Vector3(-6.5f, Floor(-6.5f, -27), -27);
        foreach (float x in new[] { -.75f, .75f })
            Art.Cylinder(marks, post + new Vector3(x, .95f, 0), .07f, .07f, 1.9f, "7a5536");
        Art.Block(marks, post + new Vector3(0, 1.7f, 0), new(2.0f, .9f, .1f), "e9d3a2");
        sign = Art.Sign(marks, post + new Vector3(0, 1.75f, -.12f), Texts.SignCastle, 34);
    }

    // ---- The guests of the feast ----

    /// <summary>The place of a chair of the feast on the ground, in the world, and the way its guest faces.</summary>
    internal (Vector3 Position, float Yaw) Chair(int chair)
    {
        var (at, yaw) = chairs[chair];
        var local = at with { Y = Floor(at.X, at.Z) };
        return (Position + basis * local, Rotation.Y + yaw);
    }

    /// <summary>Seats a guest's figure at a chair of the feast, or empties it with null.</summary>
    internal void Seat(int chair, Node3D? figure)
    {
        if (seated[chair] is { } old)
        {
            old.GetParent()?.RemoveChild(old);
            old.QueueFree();
        }
        seated[chair] = figure;
        if (figure == null)
            return;
        var (at, yaw) = chairs[chair];
        figure.Position = at with { Y = Floor(at.X, at.Z) + .45f };
        figure.Rotation = new Vector3(0, yaw, 0);
        figure.Scale = Vector3.One * .8f;
        parts[15].AddChild(figure);
    }

    // ---- Every frame ----

    /// <summary>
    /// Turns the crane's wheel while the castle is built, waves the flags, lets the drawbridge down for a car near the gate
    /// and up again behind it, lights the lanterns and the great fire for the darkness of the sky (<paramref name="night"/>
    /// from 0 by day to 1 at full night) and sends up the fireworks.
    /// </summary>
    internal void Tick(float delta, float night, Vector3 car)
    {
        time += delta;
        if (!Complete)
        {
            wheel.Rotation = new Vector3(time * .9f, 0, 0);
            squirrel.Position = new Vector3(0, .55f + .06f * Mathf.Abs(Mathf.Sin(time * 9)), 0);
        }
        for (int i = 0; i < flags.Count; i++)
            flags[i].Rotation = new Vector3(
                0,
                .35f * Mathf.Sin(time * 2.1f + i * 1.3f),
                .05f * Mathf.Sin(time * 3.7f + i)
            );
        if (Stage >= 11)
        {
            var local = basis.Inverse() * (car - Position);
            bool near =
                Complete
                && (
                    new Vector2(local.X, local.Z + Half).Length() < BridgeReach
                    || Mathf.Abs(local.X) < Half && Mathf.Abs(local.Z) < Half
                );
            float target = Complete ? (near ? 1 : 0) : 1;
            if (!Mathf.IsEqualApprox(bridgeDown, target))
            {
                bridgeDown = Mathf.MoveToward(bridgeDown, target, delta * .45f);
                PlaceBridge();
            }
        }
        bool lit = Complete && night > .05f;
        if (torchFlames.Visible != lit)
        {
            torchFlames.Visible = lit;
            gateLight.Visible = lit;
        }
        bool fire = Stage >= 14 && KeepFire;
        if (keepFire.Visible != fire)
            keepFire.Visible = fire;
        fireLight.Visible = fire && night > .05f;
        if (lit || fire)
        {
            for (int i = 0; i < tongues.Count; i++)
            {
                float flicker =
                    1 + .15f * Mathf.Sin(time * (9 + i * 1.9f) + i) + .07f * Mathf.Sin(time * 23 + i * 1.3f);
                tongues[i].Scale = new Vector3(1, flicker, 1);
            }
            gateLight.LightEnergy = 1.4f * night + .15f * Mathf.Sin(time * 11);
            fireLight.LightEnergy = .5f + 3 * night + .3f * Mathf.Sin(time * 13);
        }
        TickFireworks(delta, Fireworks && Complete && night > .3f);
    }

    private void TickFireworks(float delta, bool on)
    {
        foreach (var burst in bursts)
            burst.Tick(delta);
        if (!on)
            return;
        nextBurst -= delta;
        if (nextBurst > 0)
            return;
        nextBurst = 1.1f + sparkle.Randf() * 1.4f;
        foreach (var burst in bursts)
            if (!burst.Active)
            {
                var at = new Vector3(
                    sparkle.RandfRange(-14, 14),
                    keepTop + 10 + sparkle.RandfRange(0, 12),
                    sparkle.RandfRange(-12, 14)
                );
                burst.Start(at, FireworkColors[sparkle.RandiRange(0, FireworkColors.Length - 1)]);
                break;
            }
    }

    /// <summary>Whether fireworks are bursting over the castle now, for the checks.</summary>
    internal bool Bursting => Array.Exists(bursts, burst => burst.Active);

    private static readonly string[] FireworkColors = { "ff5a4a", "ffd23f", "5ad1ff", "9cf06a", "ff7ad8" };

    /// <summary>One burst of the fireworks: fourteen sparks that fly out from a point, sink a little and fade.</summary>
    private sealed class Burst
    {
        private const int Sparks = 14;
        private const float Life = 1.7f;
        private readonly Node3D root = new() { Name = "Burst", Visible = false };
        private readonly MeshInstance3D[] sparks = new MeshInstance3D[Sparks];
        private readonly Vector3[] directions = new Vector3[Sparks];
        private readonly StandardMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        private float age = -1;

        internal bool Active => age >= 0;

        internal Burst(Node3D parent)
        {
            parent.AddChild(root);
            for (int i = 0; i < Sparks; i++)
            {
                // Spread evenly over a sphere, by the golden angle.
                float y = 1 - 2 * (i + .5f) / Sparks;
                float r = Mathf.Sqrt(1 - y * y);
                float a = i * 2.39996f;
                directions[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                sparks[i] = Art.Ball(root, Vector3.Zero, Vector3.One * .45f, "ffffff");
                sparks[i].MaterialOverride = material;
                sparks[i].CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }

        internal void Start(Vector3 at, string color)
        {
            root.Position = at;
            root.Visible = true;
            material.AlbedoColor = new Color(color);
            age = 0;
        }

        internal void Tick(float delta)
        {
            if (age < 0)
                return;
            age += delta;
            if (age >= Life)
            {
                age = -1;
                root.Visible = false;
                return;
            }
            float t = age / Life;
            float reach = 7 * (1 - (1 - t) * (1 - t));
            for (int i = 0; i < Sparks; i++)
                sparks[i].Position = directions[i] * reach + Vector3.Down * (2.5f * t * t);
            material.AlbedoColor = material.AlbedoColor with { A = 1 - t * t };
        }
    }
}
