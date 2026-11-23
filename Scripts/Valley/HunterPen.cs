using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The hunter's big pen at the edge of the village: a wooden fence with a gate towards the road, a log hut that the player
/// raises with four deliveries (<see cref="HutPlans"/>), a hay rack, a
/// trough and bales inside, and the rack of nets outside beside the hunter. The animals the player brought live in it and
/// stroll about; a new one runs in through the gate. Scene only: the animals follow <see cref="AnimalHunt.Penned"/>. The
/// node stands at the gate, turned so that +Z points into the pen.
/// </summary>
internal sealed partial class HunterPen : Node3D
{
    /// <summary>At most this many animals are drawn in the pen; the count goes on beyond it.</summary>
    public const int Shown = 24;

    private const float PostSpacing = 2.75f;
    private const float GateHalf = 2.6f;

    private static readonly Rect2 Hut = new(-10.6f, 14.6f, 5.6f, 5.0f);
    private static readonly Rect2 Rack = new(3.8f, 15.4f, 5.4f, 3.2f);
    private static readonly Rect2 Trough = new(-1.4f, 8.6f, 3.2f, 1.2f);

    private sealed class Resident
    {
        public WildAnimal Animal = null!;
        public Vector2 Spot;
        public float Heading;
        public float Timer;
        public float Entering;
    }

    private readonly HunterSite site;
    private readonly Terrain ground;
    private readonly Node3D residents = new() { Name = "Residents" };
    private readonly List<Resident> animals = new();
    private readonly Random random;
    private float cheer;
    private int stage;
    private Node3D? hut;

    /// <summary>The hunter, who sends the trailer for the hut and thanks the driver.</summary>
    internal Character Hunter { get; set; } = null!;

    internal HunterSite Site => site;

    /// <summary>How many animals are drawn in the pen now.</summary>
    internal int AnimalCount => animals.Count;

    internal IEnumerable<WildKind> AnimalKinds
    {
        get
        {
            foreach (var resident in animals)
                yield return resident.Animal.Kind;
        }
    }

    /// <summary>The middle of the pen, for the camera of a celebration.</summary>
    internal Vector3 Focus => site.Local(0, HunterSite.Depth / 2) + Vector3.Up * 2;

    public HunterPen(HunterSite site, Terrain ground)
    {
        this.site = site;
        this.ground = ground;
        random = new Random(Settings.Seed + 6161);
        Name = "HunterPen";
        Position = site.Gate;
        Rotation = new Vector3(0, site.Yaw, 0);
    }

    // The beams of the fence are turned to their posts in the valley's frame, so everything is drawn once the pen is in it.
    public override void _Ready()
    {
        AddChild(residents);
        BuildFence();
        BuildHut();
        BuildRack();
        BuildInside();
        BuildNetRack();
    }

    /// <summary>A point of the pen's frame on the ground, lifted by <paramref name="lift"/>.</summary>
    private Vector3 At(float across, float along, float lift = 0)
    {
        var p = site.Local(across, along);
        return new Vector3(across, ground.Height(p.X, p.Z) - site.Gate.Y + lift, along);
    }

    private void BuildFence()
    {
        float w = HunterSite.HalfWidth,
            d = HunterSite.Depth;
        // The four sides, each walked from corner to corner; the front leaves the gate open.
        Side(new(-w, d), new(w, d));
        Side(new(-w, 0), new(-w, d));
        Side(new(w, 0), new(w, d));
        Side(new(-w, 0), new(-GateHalf, 0));
        Side(new(GateHalf, 0), new(w, 0));
        // The gate: two tall posts, a beam with a sign, and one leaf swung open into the pen.
        foreach (int side in new[] { -1, 1 })
        {
            var foot = At(side * GateHalf, 0);
            Art.Cylinder(this, foot + Vector3.Up * 1.35f, .17f, .17f, 2.7f, "7a5536");
            Obstacles.Box(this, foot + Vector3.Up * 1.35f, new(.36f, 2.7f, .36f));
        }
        var top = At(0, 0, 2.6f);
        Art.Block(this, top, new(GateHalf * 2 + .6f, .22f, .26f), "6b4a2e");
        var board = Art.Block(this, top + new Vector3(0, .52f, 0), new(3.8f, .8f, .12f), "e9d3a2");
        var sign = Art.Sign(this, top + new Vector3(0, .54f, -.12f), "OHRADA", 40);
        sign.RotationDegrees = new(0, 180, 0);
        var leaf = new Node3D { Position = At(GateHalf, 0), RotationDegrees = new(0, 70, 0) };
        AddChild(leaf);
        for (int i = 0; i < 2; i++)
            Art.Block(leaf, new(-GateHalf * .95f, .45f + i * .55f, 0), new(GateHalf * 1.9f, .12f, .08f), "a77a52");
        Art.Block(leaf, new(-GateHalf * .95f, .72f, 0), new(GateHalf * 2.1f, .1f, .07f), "a77a52").RotationDegrees =
            new(0, 0, 24);
    }

    /// <summary>One side of the fence: posts every few metres, two rails between them and a solid board for the car.</summary>
    private void Side(Vector2 from, Vector2 to)
    {
        float length = from.DistanceTo(to);
        int spans = Math.Max(1, Mathf.CeilToInt(length / PostSpacing));
        Vector3? previous = null;
        for (int i = 0; i <= spans; i++)
        {
            var flat = from.Lerp(to, i / (float)spans);
            var foot = At(flat.X, flat.Y);
            Art.Cylinder(this, foot + Vector3.Up * .7f, .11f, .1f, 1.4f, "8a6440");
            Art.Ball(this, foot + Vector3.Up * 1.42f, new(.12f, .07f, .12f), "7a5536");
            if (previous is { } last)
            {
                foreach (float height in new[] { .55f, 1.08f })
                    Art.Beam(this, last + Vector3.Up * height, foot + Vector3.Up * height, .1f, "b08355");
                var middle = (last + foot) / 2;
                var along = foot - last;
                float yaw = Mathf.Atan2(along.X, along.Z);
                Obstacles.Box(
                    this,
                    middle + Vector3.Up * .8f,
                    new(.3f, 1.6f, new Vector2(along.X, along.Z).Length() + .2f),
                    new Basis(Vector3.Up, yaw)
                );
            }
            previous = foot;
        }
    }

    /// <summary>
    /// The hunter's little log hut in the back corner, as far as the deliveries have raised it: the stakes of the plan, the
    /// gravel foundation, the log walls with the door and the window, the green roof and the brick chimney that smokes.
    /// </summary>
    private void BuildHut()
    {
        if (hut != null)
        {
            RemoveChild(hut);
            hut.QueueFree();
        }
        var center = Hut.GetCenter();
        hut = new Node3D { Name = "Hut", Position = At(center.X, center.Y) };
        AddChild(hut);
        float w = Hut.Size.X - .8f,
            d = Hut.Size.Y - .8f;
        if (stage == 0)
        {
            // Four stakes and a string mark where the hut will stand.
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                Art.Cylinder(hut, new(sx * w / 2, .4f, sz * d / 2), .06f, .05f, .8f, "a77a52");
                Art.Ball(hut, new(sx * w / 2, .82f, sz * d / 2), new(.1f, .06f, .1f), "e25b45");
            }
            foreach (int side in new[] { -1, 1 })
            {
                Art.Block(hut, new(0, .62f, side * d / 2), new(w, .02f, .02f), "f3ead2");
                Art.Block(hut, new(side * w / 2, .62f, 0), new(.02f, .02f, d), "f3ead2");
            }
            return;
        }
        // The foundation: a bed of gravel with a ring of stones.
        Art.Block(hut, new(0, .08f, 0), new(w + .9f, .16f, d + .9f), "a79f94");
        for (int i = 0; i < 12; i++)
        {
            float t = i / 12f * Mathf.Tau;
            var stone = new Vector3(Mathf.Cos(t) * (w / 2 + .45f), .14f, Mathf.Sin(t) * (d / 2 + .45f));
            Art.Ball(hut, stone, new(.22f, .12f, .18f), i % 2 == 0 ? "8f8a84" : "b3aca4");
        }
        if (stage < 2)
            return;
        for (int row = 0; row < 6; row++)
        {
            float y = .25f + row * .42f;
            Art.Log(hut, new(0, y, d / 2), Vector3.Right, .22f, w + .5f, row % 2 == 0 ? "8b5a35" : "7d4f2e");
            Art.Log(hut, new(0, y, -d / 2), Vector3.Right, .22f, w + .5f, row % 2 == 0 ? "8b5a35" : "7d4f2e");
            Art.Log(hut, new(w / 2, y + .21f, 0), Vector3.Forward, .22f, d + .5f, "7d4f2e");
            Art.Log(hut, new(-w / 2, y + .21f, 0), Vector3.Forward, .22f, d + .5f, "7d4f2e");
        }
        Art.Block(hut, new(0, 1.3f, 0), new(w - .2f, 2.6f, d - .2f), "6e4529");
        // The door and the window look towards the gate.
        Art.Block(hut, new(1.1f, .95f, -d / 2 - .24f), new(.9f, 1.7f, .08f), "5a3a22");
        Art.Ball(hut, new(1.42f, 1.0f, -d / 2 - .3f), Vector3.One * .07f, "f2c14e");
        Art.Window(hut, new(-1.0f, 1.45f, -d / 2 - .24f), new(.8f, .7f, .06f), "a9d6ef");
        Art.Block(hut, new(-1.0f, 1.45f, -d / 2 - .27f), new(.9f, .08f, .04f), "f3ead2");
        Art.Block(hut, new(-1.0f, 1.45f, -d / 2 - .27f), new(.08f, .8f, .04f), "f3ead2");
        Obstacles.Box(hut, new(0, 1.4f, 0), new(w + .4f, 2.8f, d + .4f));
        if (stage < 3)
            return;
        foreach (int side in new[] { -1, 1 })
        {
            var roof = Art.Block(hut, new(0, 3.25f, side * d * .29f), new(w + 1.2f, .2f, d * .66f), "4f7a45");
            roof.RotationDegrees = new(side * 32, 0, 0);
        }
        Art.Block(hut, new(0, 3.62f, 0), new(w + 1.3f, .2f, .3f), "3f6338");
        if (stage < 4)
            return;
        Art.Block(hut, new(w * .3f, 3.9f, d * .2f), new(.5f, 1.2f, .5f), "b5543c");
        Art.Block(hut, new(w * .3f, 4.52f, d * .2f), new(.62f, .12f, .62f), "8f3f2e");
        Art.ChimneySmoke(hut, new(w * .3f, 4.6f, d * .2f));
    }

    /// <summary>The deliveries the hut has had, 0 to <see cref="HutPlans.Trips"/>.</summary>
    internal int Stage => stage;

    /// <summary>The hut stands: the hunter sends the player hunting.</summary>
    internal bool Complete => stage >= HutPlans.Trips;

    /// <summary>The middle of the hut, for the camera when it is finished.</summary>
    internal Vector3 HutFocus => site.Local(Hut.GetCenter().X, Hut.GetCenter().Y) + Vector3.Up * 2;

    /// <summary>Raises the hut to a number of deliveries: at once from a save, or after a load is tipped out.</summary>
    internal void SetStage(int value)
    {
        stage = Math.Clamp(value, 0, HutPlans.Trips);
        if (IsInsideTree())
            BuildHut();
    }

    /// <summary>A hay rack under a little roof, with hay in it, where the animals of the pen come to eat.</summary>
    private void BuildRack()
    {
        var center = Rack.GetCenter();
        var rack = new Node3D { Name = "HayRack", Position = At(center.X, center.Y) };
        AddChild(rack);
        float w = Rack.Size.X - .6f,
            d = Rack.Size.Y - .6f;
        foreach (int sx in new[] { -1, 1 })
        foreach (int sz in new[] { -1, 1 })
            Art.Cylinder(rack, new(sx * w / 2, 1.2f, sz * d / 2), .1f, .1f, 2.4f, "7a5536");
        foreach (int side in new[] { -1, 1 })
        {
            var roof = Art.Block(rack, new(0, 2.55f, side * d * .27f), new(w + .8f, .14f, d * .62f), "8b5a35");
            roof.RotationDegrees = new(side * 28, 0, 0);
        }
        Art.Block(rack, new(0, .75f, 0), new(w - .2f, .9f, d * .55f), "e2c36a");
        for (int i = 0; i < 9; i++)
        {
            float x = -w / 2 + .3f + i * (w - .6f) / 8;
            foreach (int side in new[] { -1, 1 })
                Art.Block(rack, new(x, .95f, side * d * .3f), new(.06f, 1.0f, .06f), "a77a52").RotationDegrees = new(
                    side * 14,
                    0,
                    0
                );
        }
        Art.Block(rack, new(0, .32f, 0), new(w, .12f, d * .75f), "8a6440");
        Obstacles.Box(rack, new(0, 1.1f, 0), new(w + .3f, 2.2f, d + .3f));
    }

    /// <summary>A water trough, two hay bales and a few tufts of clover.</summary>
    private void BuildInside()
    {
        var trough = Trough.GetCenter();
        var foot = At(trough.X, trough.Y);
        Art.Block(this, foot + Vector3.Up * .3f, new(Trough.Size.X, .6f, Trough.Size.Y), "8b5a35");
        Art.Block(this, foot + Vector3.Up * .55f, new(Trough.Size.X - .3f, .1f, Trough.Size.Y - .3f), "6fb4d6");
        foreach (var (x, z, turn) in new[] { (9.2f, 4.6f, .3f), (8.4f, 7.2f, 1.4f) })
        {
            var bale = Art.Cylinder(this, At(x, z, .55f), .55f, .55f, 1.3f, "e2c36a");
            bale.Rotation = new(Mathf.Pi / 2, turn, 0);
            Art.Cylinder(bale, Vector3.Zero, .56f, .56f, .1f, "c9a64a");
        }
        foreach (var (x, z) in new[] { (-6f, 6f), (-3f, 12f), (5f, 11f), (0f, 18f), (-8f, 3f) })
        {
            Art.Ball(this, At(x, z, .05f), new(.5f, .12f, .5f), "6fae55");
            Art.Ball(this, At(x + .2f, z + .1f, .18f), new(.08f, .08f, .08f), "f4f0f6");
        }
    }

    /// <summary>Outside, beside the hunter: a rack with two nets hung up to dry.</summary>
    private void BuildNetRack()
    {
        var rack = new Node3D { Name = "NetRack", Position = At(7.6f, -1.6f) };
        AddChild(rack);
        foreach (float x in new[] { -1.2f, 1.2f })
            Art.Cylinder(rack, new(x, .95f, 0), .07f, .07f, 1.9f, "7a5536");
        Art.Block(rack, new(0, 1.85f, 0), new(2.8f, .1f, .1f), "6b4a2e");
        foreach (float x in new[] { -.6f, .6f })
        {
            var net = HuntField.BuildNet(rack, .5f, "d8c49a", coarse: true);
            net.Position = new(x, 1.3f, 0);
            net.Scale = new(1, 1.2f, .2f);
        }
    }

    /// <summary>Shows the animals of a save at once, strolling about the pen.</summary>
    internal void SetAnimals(IReadOnlyList<WildKind> penned)
    {
        foreach (var resident in animals)
            resident.Animal.QueueFree();
        animals.Clear();
        for (int i = 0; i < penned.Count && i < Shown; i++)
            Add(penned[i], false);
    }

    /// <summary>A new animal runs in through the gate; every one in the pen hops for joy.</summary>
    internal void Admit(WildKind kind)
    {
        cheer = 2.5f;
        if (animals.Count < Shown)
            Add(kind, true);
    }

    private void Add(WildKind kind, bool entering)
    {
        var animal = new WildAnimal(kind, random.NextSingle() * Mathf.Tau) { Name = $"Penned{kind}{animals.Count}" };
        residents.AddChild(animal);
        Art.Outline(animal, .02f);
        var resident = new Resident
        {
            Animal = animal,
            Spot = entering ? new Vector2(0, -2.5f) : FreeSpot(),
            Heading = random.NextSingle() * Mathf.Tau,
            Timer = random.NextSingle() * 3,
            Entering = entering ? 2.2f : 0,
        };
        animals.Add(resident);
        Place(resident, 0, false);
    }

    private Vector2 FreeSpot()
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var p = new Vector2(
                (random.NextSingle() * 2 - 1) * (HunterSite.HalfWidth - 1.5f),
                1.5f + random.NextSingle() * (HunterSite.Depth - 3)
            );
            if (Inside(p))
                return p;
        }
        return new Vector2(0, HunterSite.Depth / 2);
    }

    /// <summary>Whether an animal may stand there: inside the fence and off the hut, the rack and the trough.</summary>
    private static bool Inside(Vector2 p) =>
        Mathf.Abs(p.X) < HunterSite.HalfWidth - 1
        && p.Y > 1.2f
        && p.Y < HunterSite.Depth - 1
        && !Hut.Grow(.8f).HasPoint(p)
        && !Rack.Grow(.6f).HasPoint(p)
        && !Trough.Grow(.5f).HasPoint(p);

    /// <summary>The animals stroll, stop to graze and turn at the fence; only while the car is near enough to see them.</summary>
    internal void Tick(float delta, Vector3 car, bool running)
    {
        if (!running || animals.Count == 0 || new Vector2(car.X - site.Gate.X, car.Z - site.Gate.Z).Length() > 150)
            return;
        cheer = Mathf.Max(0, cheer - delta);
        foreach (var resident in animals)
        {
            resident.Animal.Phase += delta;
            bool moving;
            if (resident.Entering > 0)
            {
                resident.Entering -= delta;
                resident.Heading = 0;
                resident.Spot += new Vector2(0, 4.2f * delta);
                moving = true;
            }
            else
            {
                resident.Timer -= delta;
                if (resident.Timer <= 0)
                {
                    resident.Timer = 1.5f + random.NextSingle() * 4;
                    resident.Heading += (random.NextSingle() - .5f) * 2.4f;
                }
                bool grazing = Mathf.Sin(resident.Animal.Phase * .6f + resident.Heading) < -.2f;
                moving = !grazing || cheer > 0;
                if (moving)
                {
                    float speed = cheer > 0 ? 2.2f : .7f;
                    var step = new Vector2(Mathf.Sin(resident.Heading), Mathf.Cos(resident.Heading)) * speed * delta;
                    if (!Inside(resident.Spot + step * 6))
                    {
                        resident.Heading += Mathf.Pi * (.6f + random.NextSingle() * .4f);
                        moving = false;
                    }
                    else
                        resident.Spot += step;
                }
            }
            Place(resident, delta, moving);
        }
    }

    private void Place(Resident resident, float delta, bool moving)
    {
        var p = At(resident.Spot.X, resident.Spot.Y);
        if (cheer > 0)
            p.Y += Mathf.Abs(Mathf.Sin(resident.Animal.Phase * 9)) * .35f;
        resident.Animal.Position = p;
        resident.Animal.Rotation = new Vector3(0, resident.Heading, 0);
        resident.Animal.Fear = cheer;
        resident.Animal.Animate(delta, moving);
    }
}
