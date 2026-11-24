using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The homestead of a border's keeper at the end of the road to the border (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>,
/// phase 1), one look for each border: the lighthouse keeper's whitewashed cottage by the sea, the shepherd's log hut
/// with a sheep fold in the mountains, the ferry-woman's reed-roofed cottage with a punt by the river and the forester's
/// lodge with a deer rack in the forest. The node stands at <see cref="BorderHomeSite.Yard"/>, turned so that +Z points
/// on away from the road and +X to <see cref="BorderHomeSite.Right"/>; everything stays inside the yard.
/// </summary>
/// <remarks>
/// The ground of the yard is not levelled (the world fingerprints hold the terrain), so each building stands on a stone
/// plinth from the lowest ground under it to above the highest, and every prop stands on the ground at its own spot.
/// The parts are plain meshes of <see cref="Art"/>: <see cref="DistantDetails"/> gives the small ones their ranges and
/// <see cref="StaticMeshes"/> may merge them, as nothing here moves.
/// </remarks>
public partial class BorderHome : Node3D
{
    private static readonly PrismMesh Prism = new();

    /// <summary>The footprint of the house: its middle (across, along) and its size.</summary>
    private static readonly Vector2 HouseCenter = new(-2.5f, 11.6f);

    private readonly Terrain ground;
    private readonly Basis basis;
    private float floor;

    internal Border Border { get; }

    /// <summary>The middle of the house a little above its floor, for the camera.</summary>
    internal Vector3 Focus => Position + basis * new Vector3(HouseCenter.X, floor + 2, HouseCenter.Y);

    internal BorderHome(BorderHomeSite site, Terrain ground)
    {
        this.ground = ground;
        Border = site.Border;
        Name = "BorderHome" + site.Border;
        Position = ground.At(site.Yard.X, site.Yard.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        switch (site.Border)
        {
            case Border.Sea:
                BuildSea();
                break;
            case Border.Mountains:
                BuildMountains();
                break;
            case Border.River:
                BuildRiver();
                break;
            default:
                BuildForest();
                break;
        }
    }

    /// <summary>Height above the node of the ground under a point of the yard's frame.</summary>
    private float Floor(float x, float z)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    /// <summary>A point of the yard on the ground, lifted by <paramref name="lift"/>.</summary>
    private Vector3 At(float x, float z, float lift = 0) => new(x, Floor(x, z) + lift, z);

    /// <summary>
    /// A stone plinth under a footprint that reaches from below the lowest ground under it (corners, edges and middle) to
    /// a little above the highest. Returns the height of the lowest ground and of the plinth's top.
    /// </summary>
    private (float Low, float Top) Plinth(Vector2 center, Vector2 size, string color = "9d968c")
    {
        float low = float.MaxValue,
            high = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
        {
            float h = Floor(center.X + dx * size.X, center.Y + dz * size.Y);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        float top = high + .3f,
            bottom = low - .4f;
        Art.Block(this, new(center.X, (top + bottom) / 2, center.Y), new(size.X, top - bottom, size.Y), color);
        Art.Block(this, new(center.X, top - .05f, center.Y), new(size.X + .14f, .1f, size.Y + .14f), "b9b2a6");
        return (low, top);
    }

    /// <summary>
    /// The house on its plinth: walls (a facade, or bare logs when <paramref name="style"/> is null), a door and two
    /// windows towards the road, a window in each side (unless <paramref name="sideWindows"/> is false) and a solid box
    /// from the plinth's foot to the eaves. Returns the floor height.
    /// </summary>
    private float House(
        Vector2 size,
        float height,
        string wall,
        FacadeStyle? style,
        string door,
        string frame,
        bool sideWindows = true
    )
    {
        var c = HouseCenter;
        var (low, y) = Plinth(c, size + new Vector2(.6f, .6f));
        floor = y;
        var body = Art.Block(this, new(c.X, y + height / 2, c.Y), new(size.X, height, size.Y), wall);
        if (style is { } look)
            body.MaterialOverride = Art.Facade(wall, look);
        float front = c.Y - size.Y / 2;
        float doorX = c.X + size.X * .22f;
        Art.Block(this, new(doorX, y + 1.08f, front - .03f), new(1.22f, 2.2f, .06f), frame);
        Art.Block(this, new(doorX, y + 1.0f, front - .07f), new(.98f, 2.0f, .08f), door);
        Art.Ball(this, new(doorX + .33f, y + 1.0f, front - .13f), Vector3.One * .06f, "f2c14e");
        foreach (float wx in new[] { c.X - size.X * .1f, c.X - size.X * .34f })
            Window(new(wx, y + 1.55f, front), false, frame);
        if (sideWindows)
            foreach (int side in new[] { -1, 1 })
                Window(new(c.X + side * size.X / 2, y + 1.55f, c.Y), true, frame);
        // Two flat stepping stones from the front of the yard to the door.
        foreach (float z in new[] { front - 1.2f, front - 2.6f })
            Art.Ball(this, At(doorX, z, .02f), new(.42f, .06f, .36f), "b3aca4");
        float bottom = low - .4f;
        Obstacles.Box(
            this,
            new(c.X, (bottom + y + height) / 2, c.Y),
            new(size.X + .6f, y + height - bottom, size.Y + .6f)
        );
        return y;
    }

    /// <summary>A window with a frame and a cross bar on a wall at <paramref name="p"/>, facing -Z or (side) ±X.</summary>
    private void Window(Vector3 p, bool side, string frame)
    {
        float sx = p.X > HouseCenter.X ? 1 : -1;
        var outward = side ? new Vector3(sx, 0, 0) : Vector3.Forward;
        Vector3 Flat(float w, float h, float t) => side ? new(t, h, w) : new(w, h, t);
        Art.Block(this, p + outward * .03f, Flat(1.0f, .95f, .06f), frame);
        Art.Window(this, p + outward * .07f, Flat(.78f, .74f, .06f), "a9d6ef");
        Art.Block(this, p + outward * .11f, Flat(.07f, .74f, .03f), frame);
        Art.Block(this, p + outward * .11f, Flat(.78f, .07f, .03f), frame);
    }

    /// <summary>
    /// A gabled roof over the house with its ridge along X: a gable in <paramref name="gable"/> under two panels of
    /// <paramref name="roof"/> (a facade when <paramref name="style"/> is given) of <paramref name="thickness"/>.
    /// </summary>
    private void Roof(
        Vector2 size,
        float eaves,
        float rise,
        string gable,
        string roof,
        FacadeStyle? style,
        float thickness = .16f,
        float overhang = .5f
    )
    {
        var c = HouseCenter;
        var prism = Art.Mesh(this, Prism, new(c.X, eaves + rise / 2, c.Y), new(size.Y, rise, size.X), gable);
        prism.RotationDegrees = new(0, 90, 0);
        float half = size.Y / 2;
        float angle = Mathf.Atan2(rise, half);
        float slope = Mathf.Sqrt(half * half + rise * rise) + overhang;
        foreach (int s in new[] { -1, 1 })
        {
            var normal = new Vector3(0, Mathf.Cos(angle), s * Mathf.Sin(angle));
            var down = new Vector3(0, -Mathf.Sin(angle), s * Mathf.Cos(angle));
            var middle = new Vector3(c.X, eaves + rise / 2, c.Y + s * half / 2);
            var center = middle + normal * (thickness / 2) + down * (overhang / 2);
            var scale = new Vector3(size.X + .7f, thickness, slope);
            var panel = style is { } look
                ? Art.Wall(this, center, scale, roof, look)
                : Art.Block(this, center, scale, roof);
            panel.Rotation = new(s * angle, 0, 0);
        }
        Art.Block(
            this,
            new(c.X, eaves + rise + thickness * .7f, c.Y),
            new(size.X + .8f, thickness + .08f, thickness + .2f),
            roof
        );
    }

    /// <summary>A chimney through the roof, smoking when <paramref name="smoke"/>.</summary>
    private void Chimney(float x, float z, float top, string color, bool smoke)
    {
        Art.Block(this, new(x, top - 1.1f, z), new(.55f, 2.2f, .55f), color);
        Art.Block(this, new(x, top + .05f, z), new(.68f, .12f, .68f), "6f6a64");
        if (smoke)
            Art.ChimneySmoke(this, new(x, top + .15f, z));
    }

    /// <summary>A round post from the ground with a solid box for the car.</summary>
    private Vector3 Post(float x, float z, float height, float radius, string color, bool solid = true)
    {
        var foot = At(x, z);
        Art.Cylinder(this, foot + Vector3.Up * (height / 2 - .2f), radius, radius * .9f, height + .4f, color);
        if (solid)
            Obstacles.Box(this, foot + Vector3.Up * (height / 2), new(radius * 2 + .2f, height, radius * 2 + .2f));
        return foot;
    }

    /// <summary>A straight bar from <paramref name="a"/> to <paramref name="b"/>, without the scene tree.</summary>
    private MeshInstance3D Rail(Vector3 a, Vector3 b, float thickness, string color)
    {
        var rail = Art.Block(this, (a + b) / 2, Vector3.One, color);
        var along = b - a;
        var up = Mathf.Abs(along.Normalized().Y) > .99f ? Vector3.Right : Vector3.Up;
        rail.Basis = Basis.LookingAt(along, up) * Basis.FromScale(new(thickness, thickness, along.Length()));
        return rail;
    }

    /// <summary>A painted board with a word on it on two posts, facing the road.</summary>
    private void Signboard(float x, float z, string text)
    {
        foreach (float dx in new[] { -.7f, .7f })
            Post(x + dx, z, 1.7f, .07f, "7a5536", false);
        var board = At(x, z, 1.45f);
        Art.Block(this, board, new(1.9f, .6f, .1f), "e9d3a2");
        Art.Block(this, board + new Vector3(0, 0, .02f), new(2.0f, .7f, .06f), "7a5536");
        Art.Sign(this, board + new Vector3(0, .02f, -.1f), text, 34);
        Obstacles.Box(this, board + Vector3.Down * .6f, new(1.8f, 1.8f, .3f));
    }

    /// <summary>
    /// The lighthouse keeper's whitewashed cottage with a blue door and a slate roof, a fishing net hung to dry on poles,
    /// a red and white life ring by the door, a stack of crab pots, a coil of rope and an old anchor at the gate.
    /// </summary>
    private void BuildSea()
    {
        var size = new Vector2(7, 5.2f);
        float y = House(size, 2.7f, "f3f0e8", FacadeStyle.Plaster, "2f6db3", "e9f1f7");
        Roof(size, y + 2.7f, 2.0f, "f3f0e8", "52657a", FacadeStyle.Shingles);
        Chimney(HouseCenter.X - 2.2f, HouseCenter.Y + .9f, y + 5.1f, "ece6da", true);
        // The life ring hangs on the wall beside the door.
        float front = HouseCenter.Y - size.Y / 2;
        var ring = Art.Mesh(
            this,
            new TorusMesh
            {
                InnerRadius = .24f,
                OuterRadius = .4f,
                Rings = 16,
                RingSegments = 8,
            },
            new(HouseCenter.X + 3.0f, y + 1.6f, front - .12f),
            Vector3.One,
            "e2563b"
        );
        ring.RotationDegrees = new(90, 0, 0);
        foreach (float angle in new[] { 45f, 135f, 225f, 315f })
        {
            float a = Mathf.DegToRad(angle);
            var band = Art.Block(
                ring,
                new(Mathf.Sin(a) * .32f, 0, Mathf.Cos(a) * .32f),
                new(.15f, .18f, .2f),
                "fbf8f0"
            );
            band.RotationDegrees = new(0, angle, 0);
        }
        // The net: three poles, a rope along their tops and the net hung over it with red floats.
        var poles = new[]
        {
            Post(4.4f, 5.4f, 2.3f, .08f, "8a6440", false),
            Post(8.2f, 5.4f, 2.3f, .08f, "8a6440", false),
        };
        Post(6.3f, 5.4f, 2.3f, .08f, "8a6440", false);
        var top0 = poles[0] + Vector3.Up * 2.1f;
        var top1 = poles[1] + Vector3.Up * 2.1f;
        Rail(top0, top1, .04f, "d8c49a");
        var net = Art.Block(this, (top0 + top1) / 2 + Vector3.Down * .7f, new(3.7f, 1.35f, .04f), "c9b98f");
        net.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color("c9b98f").SrgbToLinear() with { A = .6f },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        for (int i = 0; i < 5; i++)
            Art.Ball(this, top0.Lerp(top1, (i + .5f) / 5) + Vector3.Down * .12f, Vector3.One * .09f, "e2563b");
        Obstacles.Box(this, (poles[0] + poles[1]) / 2 + Vector3.Up * 1.1f, new(4.2f, 2.2f, .4f));
        // Crab pots: round wooden traps with a rope net, two on the ground and one on top.
        foreach (var (x, z, lift) in new[] { (6.6f, 11.4f, 0f), (7.5f, 12.1f, 0f), (7.0f, 11.8f, .5f) })
        {
            var foot = At(x, z, .25f + lift);
            Art.Cylinder(this, foot, .36f, .36f, .5f, "a5753f");
            Art.Cylinder(this, foot + Vector3.Up * .02f, .33f, .3f, .52f, "d8c49a");
            Art.Ball(this, foot + Vector3.Up * .26f, new(.3f, .08f, .3f), "a5753f");
        }
        Obstacles.Box(this, At(7.05f, 11.75f, .5f), new(1.8f, 1.0f, 1.6f));
        var coil = Art.Mesh(
            this,
            new TorusMesh
            {
                InnerRadius = .2f,
                OuterRadius = .42f,
                Rings = 14,
                RingSegments = 6,
            },
            At(5.3f, 13.4f, .08f),
            new(1, 1.6f, 1),
            "d8c49a"
        );
        coil.RotationDegrees = new(0, 30, 0);
        // The old anchor stands upright at the gate.
        var anchor = At(-7.2f, 2.6f);
        Art.Cylinder(this, anchor + Vector3.Up * .9f, .09f, .09f, 1.9f, "3b3f45");
        Art.Block(this, anchor + Vector3.Up * 1.6f, new(.9f, .1f, .1f), "3b3f45");
        var eye = Art.Mesh(
            this,
            new TorusMesh
            {
                InnerRadius = .1f,
                OuterRadius = .17f,
                Rings = 12,
                RingSegments = 6,
            },
            anchor + Vector3.Up * 1.95f,
            Vector3.One,
            "3b3f45"
        );
        eye.RotationDegrees = new(90, 0, 0);
        foreach (int side in new[] { -1, 1 })
        {
            var arm = Art.Cylinder(this, anchor + new Vector3(side * .32f, .32f, 0), .08f, .08f, .8f, "3b3f45");
            arm.RotationDegrees = new(0, 0, side * 58);
            var fluke = Art.Block(this, anchor + new Vector3(side * .62f, .58f, 0), new(.26f, .32f, .1f), "3b3f45");
            fluke.RotationDegrees = new(0, 0, side * -20);
        }
        Obstacles.Box(this, anchor + Vector3.Up * 1.0f, new(1.4f, 2.0f, .4f));
    }

    /// <summary>
    /// The shepherd's log hut (salaš) with a steep shingle roof, a fold of rails with three sheep in it and a bell on a
    /// post at the gate.
    /// </summary>
    private void BuildMountains()
    {
        var size = new Vector2(6.4f, 4.8f);
        float y = House(size, 2.4f, "6e4529", FacadeStyle.Boards, "5a3a22", "8b5a35", false);
        // Logs along both sides with their pale ends sticking out at the corners; the front keeps its boards.
        var c = HouseCenter;
        for (int row = 0; row < 5; row++)
        {
            float ly = y + .22f + row * .47f;
            foreach (int s in new[] { -1, 1 })
                Art.Log(
                    this,
                    new(c.X + s * (size.X / 2 + .05f), ly, c.Y),
                    Vector3.Back,
                    .2f,
                    size.Y + .6f,
                    row % 2 == 0 ? "8b5a35" : "7d4f2e"
                );
        }
        Roof(size, y + 2.4f, 2.4f, "7d4f2e", "7b5a3e", FacadeStyle.Shingles, .18f, .6f);
        Chimney(c.X + 1.8f, c.Y + .6f, y + 5.4f, "9d968c", true);
        // The fold: rails on posts round a little paddock, its gate closed.
        float x0 = 2.4f,
            x1 = 8.6f,
            z0 = 2.4f,
            z1 = 9.6f;
        Fence(new(x0, z0), new(x0, z1));
        Fence(new(x0, z1), new(x1, z1));
        Fence(new(x1, z1), new(x1, z0));
        Fence(new(x1, z0), new(x0, z0));
        foreach (var (x, z, turn) in new[] { (4.2f, 5.0f, .6f), (6.6f, 7.4f, -2.2f), (7.0f, 4.0f, 2.6f) })
            Sheep(At(x, z), turn);
        // The bell on a post with a little roof, which the shepherd rings for the flock.
        var foot = Post(-7.4f, 2.4f, 2.6f, .1f, "7a5536");
        Art.Block(this, foot + new Vector3(.35f, 2.45f, 0), new(.9f, .1f, .12f), "6b4a2e");
        Art.Block(this, foot + new Vector3(.4f, 2.75f, 0), new(1.1f, .08f, .6f), "7b5a3e");
        Art.Cylinder(this, foot + new Vector3(.65f, 2.17f, 0), .1f, .22f, .36f, "d9a93a");
        Art.Ball(this, foot + new Vector3(.65f, 1.96f, 0), Vector3.One * .06f, "6b4a2e");
    }

    /// <summary>A side of the sheep fold: posts every two metres or so, two rails between them and a solid board.</summary>
    private void Fence(Vector2 from, Vector2 to)
    {
        int spans = Mathf.Max(1, Mathf.CeilToInt(from.DistanceTo(to) / 2.1f));
        Vector3? last = null;
        for (int i = 0; i <= spans; i++)
        {
            var flat = from.Lerp(to, i / (float)spans);
            var foot = At(flat.X, flat.Y);
            Art.Cylinder(this, foot + Vector3.Up * .5f, .08f, .07f, 1.3f, "8a6440");
            if (last is { } previous)
                foreach (float h in new[] { .45f, .95f })
                    Rail(previous + Vector3.Up * h, foot + Vector3.Up * h, .08f, "b08355");
            last = foot;
        }
        var a = At(from.X, from.Y);
        var b = At(to.X, to.Y);
        var along = b - a;
        Obstacles.Box(
            this,
            (a + b) / 2 + Vector3.Up * .65f,
            new(.3f, 1.3f, new Vector2(along.X, along.Z).Length() + .2f),
            new Basis(Vector3.Up, Mathf.Atan2(along.X, along.Z))
        );
    }

    /// <summary>A woolly sheep with a dark face and legs, turned by <paramref name="yaw"/>.</summary>
    private void Sheep(Vector3 foot, float yaw)
    {
        var sheep = new Node3D { Position = foot, Rotation = new(0, yaw, 0) };
        AddChild(sheep);
        Art.Ball(sheep, new(0, .62f, 0), new(.4f, .34f, .55f), "f4efe4");
        Art.Ball(sheep, new(0, .82f, -.1f), new(.3f, .2f, .36f), "fbf8f0");
        Art.Ball(sheep, new(0, .74f, .58f), new(.16f, .19f, .22f), "3b3433");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(sheep, new(side * .17f, .82f, .55f), new(.1f, .04f, .06f), "3b3433");
            foreach (float z in new[] { -.3f, .3f })
                Art.Cylinder(sheep, new(side * .18f, .2f, z), .05f, .045f, .42f, "3b3433");
        }
    }

    /// <summary>
    /// The ferry-woman's cottage with a thick reed roof, an oar leaning by the door, her punt (pramice) lying on two
    /// trestles and a signpost to the ferry at the front of the yard.
    /// </summary>
    private void BuildRiver()
    {
        var size = new Vector2(6.8f, 5.0f);
        float y = House(size, 2.5f, "efe4cc", FacadeStyle.Plaster, "7a5536", "6b4a2e");
        Roof(size, y + 2.5f, 2.5f, "efe4cc", "c2a35c", null, .45f, .75f);
        var c = HouseCenter;
        float front = c.Y - size.Y / 2;
        // The oar leans against the wall, its blade up.
        var oar = Art.Cylinder(this, new(c.X + 3.0f, y + 1.3f, front - .2f), .04f, .04f, 2.8f, "c39770");
        oar.RotationDegrees = new(-6, 0, 8);
        var blade = Art.Block(this, new(c.X + 2.83f, y + 2.5f, front - .26f), new(.24f, .8f, .04f), "c39770");
        blade.RotationDegrees = new(-6, 0, 8);
        // The punt on its trestles: a flat bottom, low sides, square ends and a seat.
        float bx = 5.8f,
            bz = 8.6f;
        float top = float.MinValue;
        foreach (float dz in new[] { -1.3f, 1.3f })
        {
            var foot = At(bx, bz + dz);
            top = Mathf.Max(top, foot.Y + .8f);
            foreach (int side in new[] { -1, 1 })
            {
                var leg = Art.Block(this, foot + new Vector3(side * .4f, .4f, 0), new(.08f, .95f, .08f), "8a6440");
                leg.RotationDegrees = new(0, 0, side * 18);
            }
        }
        foreach (float dz in new[] { -1.3f, 1.3f })
            Art.Block(this, new(bx, top - .05f, bz + dz), new(1.3f, .1f, .12f), "8a6440");
        var boat = new Node3D { Position = new(bx, top, bz), RotationDegrees = new(0, 0, 4) };
        AddChild(boat);
        Art.Block(boat, new(0, .08f, 0), new(1.2f, .12f, 3.8f), "3e7a5a");
        Art.Block(boat, new(0, .15f, 0), new(1.05f, .04f, 3.6f), "c39770");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(boat, new(side * .58f, .34f, 0), new(.07f, .46f, 4.0f), "3e7a5a");
            var end = Art.Block(boat, new(0, .38f, side * 2.05f), new(1.2f, .5f, .07f), "3e7a5a");
            end.RotationDegrees = new(side * 30, 0, 0);
            Art.Block(boat, new(side * .58f, .6f, 0), new(.1f, .06f, 4.0f), "f2e6c8");
        }
        Art.Block(boat, new(0, .42f, .4f), new(1.1f, .06f, .36f), "a77a52");
        Obstacles.Box(this, new(bx, (top + Floor(bx, bz)) / 2 + .2f, bz), new(1.6f, top - Floor(bx, bz) + .9f, 4.4f));
        Signboard(-6.4f, 1.6f, Texts.SignFerry);
        // A mooring post by the signpost.
        Post(-4.4f, 1.2f, .9f, .12f, "6b4a2e");
    }

    /// <summary>
    /// The forester's lodge (hájovna) with a pair of antlers above the door, a wooden feeding rack for the deer
    /// (krmelec) with hay in it, a woodpile beside the house and a chopping block with an axe.
    /// </summary>
    private void BuildForest()
    {
        var size = new Vector2(7.2f, 5.2f);
        float y = House(size, 2.7f, "ede3cc", FacadeStyle.Plaster, "6b4a2e", "3f6338");
        Roof(size, y + 2.7f, 2.2f, "8b5a35", "5a4636", FacadeStyle.Shingles);
        var c = HouseCenter;
        Chimney(c.X - 2.0f, c.Y + .8f, y + 5.3f, "b5543c", true);
        // The antlers on a little board above the door.
        float front = c.Y - size.Y / 2;
        var mount = new Vector3(c.X + size.X * .22f, y + 2.45f, front - .1f);
        Art.Ball(this, mount, new(.22f, .16f, .06f), "6b4a2e");
        foreach (int side in new[] { -1, 1 })
        {
            var beam = Art.Cylinder(this, mount + new Vector3(side * .28f, .22f, -.06f), .035f, .025f, .65f, "efe3c8");
            beam.RotationDegrees = new(0, 0, side * -48);
            foreach (var (dx, dy, tilt) in new[] { (.3f, .36f, -10f), (.48f, .2f, -70f) })
            {
                var tine = Art.Cylinder(this, mount + new Vector3(side * dx, dy, -.08f), .025f, .015f, .3f, "efe3c8");
                tine.RotationDegrees = new(0, 0, side * tilt);
            }
        }
        // The feeding rack: four posts, a little gabled roof and slatted sides over a trough of hay.
        var rack = At(5.4f, 7.0f);
        var rackNode = new Node3D { Position = rack };
        AddChild(rackNode);
        const float w = 3.0f,
            d = 2.0f;
        foreach (int sx in new[] { -1, 1 })
        foreach (int sz in new[] { -1, 1 })
            Art.Cylinder(rackNode, new(sx * w / 2, 1.0f, sz * d / 2), .09f, .09f, 2.4f, "7a5536");
        foreach (int side in new[] { -1, 1 })
        {
            var roof = Art.Block(rackNode, new(0, 2.42f, side * d * .27f), new(w + .7f, .12f, d * .66f), "5a4636");
            roof.RotationDegrees = new(side * 30, 0, 0);
            for (int i = 0; i < 6; i++)
            {
                var slat = Art.Block(
                    rackNode,
                    new(-w / 2 + .3f + i * (w - .6f) / 5, 1.25f, side * d * .25f),
                    new(.05f, .9f, .05f),
                    "a77a52"
                );
                slat.RotationDegrees = new(side * 16, 0, 0);
            }
        }
        Art.Block(rackNode, new(0, 1.15f, 0), new(w - .3f, .8f, d * .45f), "e2c36a");
        Art.Block(rackNode, new(0, .62f, 0), new(w, .14f, d * .8f), "8a6440");
        Art.Block(rackNode, new(0, .4f, 0), new(w, .4f, d * .8f), "a77a52");
        Obstacles.Box(rackNode, new(0, 1.1f, 0), new(w + .3f, 2.2f, d + .3f));
        // The woodpile against the side of the house, the cut ends towards the road.
        float px = c.X - size.X / 2 - 1.35f;
        var pile = At(px, c.Y - .4f);
        int[] rows = { 3, 2, 1 };
        for (int row = 0; row < rows.Length; row++)
        for (int i = 0; i < rows[row]; i++)
            Art.Log(
                this,
                pile + new Vector3((i - (rows[row] - 1) / 2f) * .5f, .25f + row * .44f, 0),
                Vector3.Back,
                .24f,
                2.4f,
                (row + i) % 2 == 0 ? "8b5a35" : "a06a40"
            );
        Obstacles.Box(this, pile + Vector3.Up * .7f, new(1.7f, 1.4f, 2.5f));
        // The chopping block with an axe stuck in it.
        var stump = At(-5.6f, 5.4f);
        Art.Cylinder(this, stump + Vector3.Up * .3f, .36f, .4f, .7f, "8b5a35");
        Art.Cylinder(this, stump + Vector3.Up * .66f, .34f, .34f, .03f, "dcb887");
        var handle = Art.Cylinder(this, stump + new Vector3(.12f, 1.0f, 0), .035f, .03f, .75f, "c39770");
        handle.RotationDegrees = new(0, 0, -30);
        Art.Block(this, stump + new Vector3(0, .72f, 0), new(.24f, .16f, .05f), "6b6f73");
        Obstacles.Box(this, stump + Vector3.Up * .35f, new(.8f, .7f, .8f));
    }
}
