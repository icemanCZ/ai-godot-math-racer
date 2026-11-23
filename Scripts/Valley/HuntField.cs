using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Vehicles;

namespace MathRacer.Valley;

/// <summary>Where the hunter's net is.</summary>
internal enum NetState
{
    /// <summary>No hunt is on.</summary>
    None,

    /// <summary>Rolled up on the car, ready to be thrown.</summary>
    InHand,
    Flying,

    /// <summary>It missed and lies in the grass until the player picks it up.</summary>
    Lying,

    /// <summary>It is wrapped round a caught animal, on the ground or on the car.</summary>
    Wrapped,

    /// <summary>It fell into the water; the hunter gives a new one.</summary>
    Lost,
}

/// <summary>What happened to a thrown net when it came down.</summary>
internal enum NetLanding
{
    None,
    Caught,
    Missed,
    InWater,
}

/// <summary>
/// The scene of a hunt: three animals that run free in a meadow far from the pen, the net that the car throws at them and
/// the caught animal on the car's roof. An animal strolls about its meadow and runs away when the car comes close, slower than
/// the car, and it is pulled back when it strays far; a net thrown from a few metres away while it does not notice the car
/// catches it. A missed net lies in the grass, beside a tall glowing column, until the car comes back for it; a net that falls
/// into the river is lost. Nothing here is saved: after a load the animals run in a new meadow and the net is on the car.
/// </summary>
internal sealed partial class HuntField : Node3D
{
    /// <summary>How many animals run free during a hunt.</summary>
    public const int HerdSize = 3;

    /// <summary>The farthest the net is thrown, and the widest an animal may stand off the car's nose to be aimed at.</summary>
    public const float ThrowRange = 20;

    public const float AimAngle = .62f;

    /// <summary>The net is thrown this far ahead when no animal is in front of the car.</summary>
    public const float BlindThrow = 11;

    /// <summary>How close to an animal the net must come down to catch it.</summary>
    public const float HitRadius = 2.6f;

    public const float FlightTime = .75f;

    /// <summary>How close the car must come to a net in the grass, or to a caught animal, to take it.</summary>
    public const float Reach = 5.5f;

    /// <summary>An animal nearer the car than this notices it and runs away.</summary>
    public const float Alert = 12;

    /// <summary>An animal farther than this from its meadow is led back to it.</summary>
    public const float Roam = 45;

    private sealed class Prey
    {
        public WildAnimal Animal = null!;
        public Vector3 Velocity;
        public bool Wrapped;
    }

    private readonly World world;
    private readonly List<Prey> herd = new();
    private readonly Random random = new(Settings.Seed + 8181);
    private Node3D net = null!;
    private Node3D beacon = null!;
    private Node3D bundle = null!;
    private Node3D carried = null!;
    private Vehicle? car;
    private Vector3 flightFrom,
        flightTo;
    private float flight;
    private float spin;
    private Prey? target;
    private WildKind? carriedKind;

    internal NetState Net { get; private set; }

    /// <summary>The meadow where the animals of this hunt run free.</summary>
    internal Vector3 Meadow { get; private set; }

    public HuntField(World world) => this.world = world;

    public override void _Ready()
    {
        net = BuildNet(this, .55f, "e4cf9e");
        net.Name = "HuntNet";
        net.Visible = false;
        beacon = Art.Beacon(this, "f2c14e");
    }

    /// <summary>Puts the rolled net and the place for a caught animal on the car's roof (called once the car exists).</summary>
    internal void Setup(Vehicle vehicle)
    {
        car = vehicle;
        bundle = new Node3D
        {
            Name = "NetBundle",
            Position = new(0, 2.25f, .75f),
            Visible = false,
        };
        vehicle.AddChild(bundle);
        var roll = Art.Cylinder(bundle, Vector3.Zero, .2f, .2f, 1.3f, "d8c49a");
        roll.RotationDegrees = new(0, 0, 90);
        foreach (float x in new[] { -.4f, 0, .4f })
            Art.Cylinder(bundle, new(x, 0, 0), .215f, .215f, .07f, "8a6440").RotationDegrees = new(0, 0, 90);
        carried = new Node3D
        {
            Name = "CarriedAnimal",
            Position = new(0, 2.4f, .3f),
            Visible = false,
        };
        vehicle.AddChild(carried);
    }

    /// <summary>
    /// A net: a ball of rope, a grid of meridians and rings with a knot where they cross, which flies flat, lies flat in the
    /// grass or wraps an animal, only by being scaled.
    /// </summary>
    internal static Node3D BuildNet(Node3D parent, float radius, string rope, bool coarse = false)
    {
        var root = new Node3D();
        parent.AddChild(root);
        var cloth = Art.Ball(root, Vector3.Zero, Vector3.One * radius * .97f, rope);
        cloth.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(rope).SrgbToLinear() with { A = .28f },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        cloth.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        int meridians = coarse ? 3 : 5,
            rings = coarse ? 2 : 3,
            steps = coarse ? 6 : 8;
        Vector3 Point(float latitude, float longitude) =>
            new Vector3(
                Mathf.Cos(latitude) * Mathf.Sin(longitude),
                Mathf.Sin(latitude),
                Mathf.Cos(latitude) * Mathf.Cos(longitude)
            ) * radius;
        for (int m = 0; m < meridians; m++)
        {
            float longitude = m * Mathf.Pi / meridians;
            for (int s = 0; s < steps; s++)
            {
                float a = s * Mathf.Tau / steps,
                    b = (s + 1) * Mathf.Tau / steps;
                var from = new Vector3(
                    Mathf.Sin(a) * Mathf.Sin(longitude),
                    Mathf.Cos(a),
                    Mathf.Sin(a) * Mathf.Cos(longitude)
                );
                var to = new Vector3(
                    Mathf.Sin(b) * Mathf.Sin(longitude),
                    Mathf.Cos(b),
                    Mathf.Sin(b) * Mathf.Cos(longitude)
                );
                Art.Beam(root, from * radius, to * radius, radius * .06f, rope);
            }
        }
        for (int r = 1; r <= rings; r++)
        {
            float latitude = -Mathf.Pi / 2 + r * Mathf.Pi / (rings + 1);
            for (int s = 0; s < steps + 2; s++)
            {
                float a = s * Mathf.Tau / (steps + 2),
                    b = (s + 1) * Mathf.Tau / (steps + 2);
                Art.Beam(root, Point(latitude, a), Point(latitude, b), radius * .06f, rope);
                Art.Ball(root, Point(latitude, a), Vector3.One * radius * .07f, "8a6440");
            }
        }
        return root;
    }

    /// <summary>The animals of a new hunt run in a meadow far from <paramref name="from"/>; the net is on the car.</summary>
    internal void Begin(Vector3 from, int caught)
    {
        Clear();
        Meadow = world.HerdSpot(from, 170, 320, random)
            ?? world.HerdSpot(from, 90, 420, random)
            ?? world.HerdSpot(from, 40, 520, random)
            ?? from;
        for (int i = 0; i < HerdSize; i++)
        {
            var kind = AnimalHunt.Prey[(caught + i) % AnimalHunt.Prey.Length];
            var animal = new WildAnimal(kind, random.NextSingle() * Mathf.Tau) { Name = $"Prey{kind}" };
            AddChild(animal);
            Art.Outline(animal, .02f);
            var spot = Meadow;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float turn = random.NextSingle() * Mathf.Tau;
                float distance = 3 + random.NextSingle() * 9;
                var p = world.Ground.At(Meadow.X + Mathf.Sin(turn) * distance, Meadow.Z + Mathf.Cos(turn) * distance);
                if (world.IsMeadow(p))
                {
                    spot = p;
                    break;
                }
            }
            animal.Position = spot;
            animal.Home = Meadow;
            animal.Direction = new Vector3(Mathf.Sin(animal.Phase), 0, Mathf.Cos(animal.Phase));
            animal.Animate(0, false);
            herd.Add(new Prey { Animal = animal });
        }
        SetNet(NetState.InHand);
    }

    /// <summary>The hunt is over: the animals and the net go, and nothing is on the car.</summary>
    internal void Clear()
    {
        foreach (var prey in herd)
            prey.Animal.QueueFree();
        herd.Clear();
        target = null;
        Carry(null, false);
        SetNet(NetState.None);
    }

    /// <summary>The hunter hands over a new net: the old one, wherever it lies, is gone.</summary>
    internal void GiveNewNet()
    {
        if (Net is NetState.Lying or NetState.Lost or NetState.None)
            SetNet(NetState.InHand);
    }

    private void SetNet(NetState state)
    {
        Net = state;
        if (!IsInsideTree())
            return;
        net.Visible = state is NetState.Flying or NetState.Lying || (state == NetState.Wrapped && WrappedPrey != null);
        beacon.Visible = state == NetState.Lying || (state == NetState.Wrapped && WrappedPrey != null);
        if (bundle != null)
            bundle.Visible = state == NetState.InHand;
        if (state == NetState.Lying)
        {
            net.Scale = new(1.5f, .14f, 1.5f);
            net.Rotation = Vector3.Zero;
        }
    }

    /// <summary>Animals that still run free.</summary>
    internal int FreeCount
    {
        get
        {
            int count = 0;
            foreach (var prey in herd)
                if (!prey.Wrapped)
                    count++;
            return count;
        }
    }

    internal int HerdCount => herd.Count;

    /// <summary>Where animal <paramref name="index"/> of the herd is, for the checks.</summary>
    internal Vector3 PositionOf(int index) => herd[index].Animal.GlobalPosition;

    internal WildKind KindOf(int index) => herd[index].Animal.Kind;

    internal bool IsWrapped(int index) => herd[index].Wrapped;

    /// <summary>Puts animal <paramref name="index"/> at a place of the checks, where it stands still.</summary>
    internal void Place(int index, Vector3 ground)
    {
        var prey = herd[index];
        prey.Animal.Position = ground;
        prey.Animal.Home = ground;
        prey.Animal.Fear = 0;
        prey.Velocity = Vector3.Zero;
    }

    private Prey? WrappedPrey
    {
        get
        {
            foreach (var prey in herd)
                if (prey.Wrapped)
                    return prey;
            return null;
        }
    }

    /// <summary>Where the net lies in the grass or the caught animal waits, or null.</summary>
    internal Vector3? NetSpot =>
        Net == NetState.Lying ? net.GlobalPosition
        : Net == NetState.Wrapped && WrappedPrey is { } prey ? prey.Animal.GlobalPosition
        : null;

    /// <summary>The kind of the caught animal that waits in the net on the ground, or null.</summary>
    internal WildKind? WrappedKind => Net == NetState.Wrapped ? WrappedPrey?.Animal.Kind : null;

    /// <summary>The free animal nearest to <paramref name="from"/>, or null.</summary>
    internal (Vector3 Position, WildKind Kind)? NearestFree(Vector3 from)
    {
        Prey? best = null;
        float bestDistance = float.MaxValue;
        foreach (var prey in herd)
        {
            if (prey.Wrapped)
                continue;
            float distance = Flat(prey.Animal.GlobalPosition - from).Length();
            if (distance < bestDistance)
            {
                best = prey;
                bestDistance = distance;
            }
        }
        return best == null ? null : (best.Animal.GlobalPosition, best.Animal.Kind);
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    private bool Near(Vector3 spot, float reach) => car != null && Flat(spot - car.Position).Length() <= reach;

    /// <summary>The net lies near enough to the car to be picked up.</summary>
    internal bool NetWithinReach => Net == NetState.Lying && Near(net.GlobalPosition, Reach);

    /// <summary>The caught animal waits near enough to the car to be loaded.</summary>
    internal bool CatchWithinReach =>
        Net == NetState.Wrapped && WrappedPrey is { } prey && Near(prey.Animal.GlobalPosition, Reach);

    /// <summary>Picks the net up from the grass; false when it is not within reach.</summary>
    internal bool PickUpNet()
    {
        if (!NetWithinReach)
            return false;
        SetNet(NetState.InHand);
        return true;
    }

    /// <summary>Takes the caught animal, still in its net, up onto the car; returns its kind, or null when none is in reach.</summary>
    internal WildKind? LoadCatch()
    {
        if (!CatchWithinReach || WrappedPrey is not { } prey)
            return null;
        herd.Remove(prey);
        prey.Animal.QueueFree();
        Carry(prey.Animal.Kind, false);
        SetNet(NetState.Wrapped);
        return prey.Animal.Kind;
    }

    /// <summary>
    /// Shows the caught animal wrapped in its net on the car's roof (null takes it down). It sits higher when the roof
    /// carries the cargo of a delivery.
    /// </summary>
    internal void Carry(WildKind? kind, bool raised)
    {
        carriedKind = kind;
        if (carried == null)
            return;
        foreach (var child in carried.GetChildren())
        {
            carried.RemoveChild(child);
            child.QueueFree();
        }
        carried.Visible = kind != null;
        if (kind is not { } shown)
            return;
        carried.Position = new(0, raised ? 3.3f : 2.4f, .3f);
        if (Net != NetState.Wrapped)
            SetNet(NetState.Wrapped);
        float scale = Size(shown);
        Art.Block(carried, new(0, -.1f, 0), new(1.5f, .18f, 1.2f), "bc8b61");
        var animal = new WildAnimal(shown, 0) { Name = "CarriedPrey", Scale = Vector3.One * scale };
        carried.AddChild(animal);
        animal.Position = Vector3.Zero;
        animal.Rotation = new(0, Mathf.Pi / 2, 0);
        animal.Animate(0, false);
        var wrap = BuildNet(carried, .55f, "e4cf9e");
        wrap.Position = new(0, NetHeight(shown) * scale, 0);
        wrap.Scale = WrapScale(shown) * scale;
        wrap.Rotation = new(0, Mathf.Pi / 2, 0);
    }

    /// <summary>The caught animal shown on the car, or null.</summary>
    internal WildKind? CarriedKind => carriedKind;

    private static float Size(WildKind kind) =>
        kind switch
        {
            WildKind.Deer => .62f,
            WildKind.Fox => .9f,
            _ => 1.1f,
        };

    private static float NetHeight(WildKind kind) =>
        kind switch
        {
            WildKind.Deer => 1.15f,
            WildKind.Fox => .6f,
            _ => .45f,
        };

    private static Vector3 WrapScale(WildKind kind) =>
        kind switch
        {
            WildKind.Deer => new(1.0f, 2.0f, 2.0f),
            WildKind.Fox => new(.85f, 1.1f, 2.3f),
            _ => new(.8f, 1.0f, 1.0f),
        };

    /// <summary>
    /// Throws the net from the car: at the free animal most nearly in front of the car within reach, a little ahead of where
    /// it runs, or straight ahead when none is there. False when the net is not on the car.
    /// </summary>
    internal bool Throw()
    {
        if (Net != NetState.InHand || car == null)
            return false;
        var forward = Flat(-car.GlobalBasis.Z).Normalized();
        Prey? aim = null;
        float best = AimAngle;
        foreach (var prey in herd)
        {
            if (prey.Wrapped)
                continue;
            var offset = Flat(prey.Animal.GlobalPosition - car.Position);
            float distance = offset.Length();
            if (distance > ThrowRange || distance < .5f)
                continue;
            float angle = forward.AngleTo(offset / distance);
            if (angle < best)
            {
                best = angle;
                aim = prey;
            }
        }
        target = aim;
        var to =
            aim != null
                ? aim.Animal.GlobalPosition + aim.Velocity * FlightTime * .5f
                : car.Position + forward * BlindThrow + Flat(car.Velocity) * FlightTime * .5f;
        flightTo = new Vector3(to.X, world.DriveSurface(to.X, to.Z), to.Z);
        flightFrom = car.Position + Vector3.Up * 2.3f;
        flight = 0;
        spin = 0;
        net.GlobalPosition = flightFrom;
        net.Scale = new(1.6f, .32f, 1.6f);
        SetNet(NetState.Flying);
        return true;
    }

    /// <summary>
    /// Moves the animals and the net. Returns what happened to a net that came down in this frame; the animals run only
    /// while the game runs and the car drives.
    /// </summary>
    internal NetLanding Tick(float delta, bool running)
    {
        if (!running || car == null)
            return NetLanding.None;
        foreach (var prey in herd)
        {
            if (prey.Wrapped)
            {
                // A caught animal wriggles in its net.
                prey.Animal.Phase += delta;
                prey.Animal.Animate(delta, true);
                prey.Animal.Rotation = new(0, prey.Animal.Rotation.Y, Mathf.Sin(prey.Animal.Phase * 14) * .12f);
                continue;
            }
            Step(prey, delta);
        }
        if (Net == NetState.Wrapped && WrappedPrey is { } wrapped)
        {
            var kind = wrapped.Animal.Kind;
            net.GlobalPosition = wrapped.Animal.GlobalPosition + Vector3.Up * NetHeight(kind) * 1.05f;
            net.Rotation = new(0, wrapped.Animal.Rotation.Y, 0);
            net.Scale = new Vector3(WrapScale(kind).X, WrapScale(kind).Y, WrapScale(kind).Z) * 1.05f;
        }
        if (NetSpot is { } spot)
            beacon.GlobalPosition = spot;
        if (Net != NetState.Flying)
            return NetLanding.None;
        flight += delta / FlightTime;
        spin += delta * 9;
        float t = Mathf.Min(1, flight);
        float arc = 2.2f + flightFrom.DistanceTo(flightTo) * .12f;
        net.GlobalPosition = flightFrom.Lerp(flightTo, t) + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * arc + .1f);
        net.Rotation = new(0, spin, 0);
        if (t < 1)
            return NetLanding.None;
        return Land();
    }

    private NetLanding Land()
    {
        Prey? caught = null;
        float nearest = HitRadius;
        foreach (var prey in herd)
        {
            if (prey.Wrapped)
                continue;
            float distance = Flat(prey.Animal.GlobalPosition - flightTo).Length();
            // The animal the net was thrown at is a little easier to catch.
            float reach = prey == target ? nearest * 1.15f : nearest;
            if (distance < reach)
            {
                caught = prey;
                nearest = distance;
            }
        }
        target = null;
        if (caught != null)
        {
            caught.Wrapped = true;
            caught.Velocity = Vector3.Zero;
            caught.Animal.Fear = 0;
            SetNet(NetState.Wrapped);
            return NetLanding.Caught;
        }
        bool water =
            !world.OnCrossing(flightTo.X, flightTo.Z)
            && River.Distance(flightTo.X, flightTo.Z) < River.HalfWidth(flightTo.Z) + .6f;
        if (water || !ValleyBounds.Contains(flightTo.X, flightTo.Z))
        {
            SetNet(NetState.Lost);
            net.Visible = false;
            return NetLanding.InWater;
        }
        net.GlobalPosition = flightTo + Vector3.Up * .08f;
        SetNet(NetState.Lying);
        return NetLanding.Missed;
    }

    /// <summary>One step of a free animal: it grazes and strolls about its meadow and runs from a car that comes close.</summary>
    private void Step(Prey prey, float dt)
    {
        var animal = prey.Animal;
        var threat = car!.Position;
        var away = Flat(animal.Position - threat);
        var predicted = Flat(animal.Position - threat - Flat(car.Velocity) * .5f);
        bool danger = away.Length() < Alert || predicted.Length() < Alert;
        animal.Fear = danger ? 2.2f : Mathf.Max(0, animal.Fear - dt);
        bool fleeing = animal.Fear > 0;
        animal.Phase += dt;
        var home = Flat(animal.Home - animal.Position);
        Vector3 direction;
        if (fleeing)
        {
            direction = (away.Normalized() + predicted.Normalized() * .6f).Normalized();
            if (direction.IsZeroApprox())
                direction = animal.Direction;
            // Far from its meadow an animal turns back towards it, so it never runs off across the whole valley.
            if (home.Length() > Roam)
                direction = (direction + home.Normalized() * ((home.Length() - Roam) / 12)).Normalized();
        }
        else
            direction =
                home.Length() > 14
                    ? home.Normalized()
                    : new Vector3(Mathf.Sin(animal.Phase * .37f), 0, Mathf.Cos(animal.Phase * .29f));
        float speed = fleeing ? animal.EscapeSpeed * .72f : .55f;
        if (!fleeing && Mathf.Sin(animal.Phase * .7f) < .2f)
            speed = 0;
        bool moved = false;
        prey.Velocity = Vector3.Zero;
        foreach (float angle in new[] { 0f, .55f, -.55f, 1.1f, -1.1f, 1.65f, -1.65f, Mathf.Pi })
        {
            var heading = direction.Rotated(Vector3.Up, angle);
            var next = animal.Position + heading * speed * dt;
            var probe = next + heading * (animal.Kind == WildKind.Deer ? 1.6f : .8f);
            if (speed > 0 && (!world.IsMeadow(next, 2) || !world.IsMeadow(probe, 2)))
                continue;
            float height = world.Ground.Height(next.X, next.Z);
            if (speed > 0 && Mathf.Abs(world.Ground.Height(probe.X, probe.Z) - height) > 1.3f)
                continue;
            animal.Position = new Vector3(next.X, height, next.Z);
            if (speed > 0)
            {
                animal.Direction = heading;
                prey.Velocity = heading * speed;
            }
            animal.Rotation = new Vector3(
                0,
                Mathf.LerpAngle(animal.Rotation.Y, Mathf.Atan2(heading.X, heading.Z), Mathf.Min(1, dt * 12)),
                0
            );
            moved = speed > 0;
            break;
        }
        animal.Animate(dt, moved);
    }
}
