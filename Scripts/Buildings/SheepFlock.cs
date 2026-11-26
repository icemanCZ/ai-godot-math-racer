using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The shepherd's eight lost sheep in the scene (<c>docs/SHEEP_PLAN.md</c>): woolly sheep with a bell at the neck that
/// graze on their meadows while they are lost, ringing now and then; trot after the car along its way once found, legs
/// swinging; stop, bleat and show a pale beacon when left behind; and run to the fold of the shepherd's homestead, jump
/// over its front rail one after another with the count of the sheep home over each, and graze in the fold from then on.
/// What each sheep does comes from the <see cref="SheepHerd"/>; this node only moves them there smoothly.
/// </summary>
public partial class SheepFlock : Node3D
{
    /// <summary>How fast a sheep catches up with the place the herd gives it, in metres a second.</summary>
    private const float CatchUp = 16;

    /// <summary>How fast the sheep run to the fold, and how long the jump over its rail takes.</summary>
    private const float FoldRun = 7,
        JumpTime = .85f,
        JumpHeight = 1.5f;

    /// <summary>The sheep go into the fold this long after each other.</summary>
    private const float FoldGap = .7f;

    private enum Mode
    {
        Hidden,
        Grazing,
        Trotting,
        Waiting,
        ToFold,
        Jumping,
        InFold,
    }

    private sealed class Sheep
    {
        public Node3D Root = null!;
        public Node3D Body = null!;
        public Node3D Head = null!;
        public readonly List<Node3D> Legs = new();
        public Node3D Beacon = null!;
        public AudioStreamPlayer3D Bell = null!;
        public AudioStreamPlayer3D Voice = null!;
        public Mode Mode = Mode.Hidden;
        public float Phase;
        public float Speed;
        public float BellTimer;
        public float BleatTimer;

        // The way into the fold: wait, run to the rail, jump over it, walk to the place in the fold.
        public float Delay;
        public float Clock;
        public Vector3 From;
        public Vector3 Approach;
        public Vector3 Landing;
        public Vector3 Home;
        public int Count;
    }

    private readonly World world;
    private readonly Vector3[] spots;
    private readonly float yaw;
    private readonly Sheep[] sheep;
    private readonly List<Label3D> counts = new();
    private readonly Random random = new(7);

    private AudioStreamWav? bellSound;
    private AudioStreamWav? bleatSound;

    /// <summary>How many sheep are home, for the sheepdog puppy, who comes for all eight.</summary>
    internal int HomeCount { get; private set; }

    internal SheepFlock(World world, IReadOnlyList<Vector3> spots, float yaw)
    {
        this.world = world;
        this.spots = new Vector3[spots.Count];
        for (int i = 0; i < spots.Count; i++)
            this.spots[i] = spots[i];
        this.yaw = yaw;
        sheep = new Sheep[spots.Count];
        for (int i = 0; i < sheep.Length; i++)
            sheep[i] = Build(i);
    }

    private Sheep Build(int number)
    {
        var s = new Sheep
        {
            Root = new Node3D
            {
                Name = $"Sheep{number}",
                Position = spots[number],
                Visible = false,
            },
            Phase = number * 1.7f,
            BellTimer = 1 + number * .4f,
            BleatTimer = 3 + number,
        };
        AddChild(s.Root);
        s.Body = new Node3D { Name = "Body" };
        s.Root.AddChild(s.Body);
        // The fleece: a big ball and puffs on top and at the sides, a little greyer for every second sheep.
        string wool = number % 3 == 1 ? "efe8da" : "f6f1e6";
        Art.Ball(s.Body, new(0, .64f, 0), new(.42f, .36f, .58f), wool);
        Art.Ball(s.Body, new(0, .86f, -.08f), new(.32f, .2f, .38f), "fbf8f0");
        foreach (int side in new[] { -1, 1 })
            Art.Ball(s.Body, new(side * .2f, .74f, .2f), new(.2f, .22f, .22f), "fbf8f0");
        Art.Ball(s.Body, new(0, .7f, -.56f), new(.12f, .12f, .1f), wool);
        // The head with ears, eyes and the bell on a red collar under it.
        s.Head = new Node3D { Name = "Head", Position = new(0, .74f, .5f) };
        s.Body.AddChild(s.Head);
        Art.Ball(s.Head, new(0, .02f, .12f), new(.16f, .18f, .22f), "3b3433");
        Art.Ball(s.Head, new(0, .16f, .02f), new(.17f, .1f, .14f), "fbf8f0");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(s.Head, new(side * .18f, .07f, .04f), new(.11f, .04f, .06f), "3b3433").RotationDegrees = new(
                0,
                0,
                side * -20
            );
            Art.Ball(s.Head, new(side * .08f, .07f, .28f), Vector3.One * .035f, "fff5e1");
            Art.Ball(s.Head, new(side * .08f, .07f, .305f), Vector3.One * .018f, "1d1a1a");
        }
        Art.Cylinder(s.Body, new(0, .5f, .46f), .13f, .13f, .06f, "c0392b");
        Art.Cylinder(s.Body, new(0, .38f, .52f), .05f, .1f, .14f, "e8b938");
        Art.Ball(s.Body, new(0, .3f, .53f), Vector3.One * .03f, "6b4a2e");
        // Four dark legs that swing from the hip.
        foreach (int side in new[] { -1, 1 })
        foreach (float z in new[] { -.3f, .3f })
        {
            var leg = new Node3D { Position = new(side * .18f, .42f, z) };
            s.Body.AddChild(leg);
            Art.Cylinder(leg, new(0, -.21f, 0), .05f, .045f, .42f, "3b3433");
            s.Legs.Add(leg);
        }
        s.Beacon = Art.Beacon(s.Root, "fff3c4");
        s.Bell = new AudioStreamPlayer3D
        {
            Name = "Bell",
            Stream = bellSound ??= MakeBell(),
            MaxDistance = 140,
            UnitSize = 14,
            VolumeDb = -4,
            Position = new(0, .5f, .5f),
        };
        s.Root.AddChild(s.Bell);
        s.Voice = new AudioStreamPlayer3D
        {
            Name = "Voice",
            Stream = bleatSound ??= MakeBleat(),
            MaxDistance = 90,
            UnitSize = 10,
            VolumeDb = -2,
            Position = new(0, .8f, .6f),
        };
        s.Root.AddChild(s.Voice);
        return s;
    }

    /// <summary>Whether a sheep is shown, for the checks.</summary>
    internal bool Shows(int number) => sheep[number].Root.Visible;

    /// <summary>Where a sheep stands now.</summary>
    internal Vector3 PositionOf(int number) => sheep[number].Root.Position;

    /// <summary>Whether a sheep stands in the fold, its jump over, for the checks.</summary>
    internal bool InFold(int number) => sheep[number].Mode == Mode.InFold;

    /// <summary>Whether a sheep shows its beacon, which it does while it waits for the car.</summary>
    internal bool Beckons(int number) => sheep[number].Beacon.Visible;

    /// <summary>Whether any sheep is still on its way into the fold.</summary>
    internal bool Penning
    {
        get
        {
            foreach (var s in sheep)
                if (s.Mode is Mode.ToFold or Mode.Jumping)
                    return true;
            return false;
        }
    }

    /// <summary>
    /// Puts every sheep where the herd says, at once: lost ones on their meadows (hidden until the shepherd has sent the
    /// player out), found ones behind the car and those at home in the fold.
    /// </summary>
    internal void Restore(SheepHerd herd, Vector3 car, Vector3 back)
    {
        HomeCount = herd.HomeCount;
        foreach (var label in counts)
            label.Visible = false;
        for (int i = 0; i < sheep.Length; i++)
        {
            var s = sheep[i];
            var status = herd.StatusOf(i);
            s.Beacon.Visible = false;
            if (status == SheepStatus.Home)
            {
                s.Mode = Mode.InFold;
                Place(s, FoldSpot(i), yaw + Mathf.Pi + i);
                continue;
            }
            if (!herd.Started)
            {
                s.Mode = Mode.Hidden;
                s.Root.Visible = false;
                continue;
            }
            var target = herd.Target(i, car, back, out var forward);
            target.Y = world.SheepSurface(target.X, target.Z);
            s.Mode = status == SheepStatus.Lost ? Mode.Grazing : Mode.Trotting;
            Place(s, target, Mathf.Atan2(forward.X, forward.Z) + (status == SheepStatus.Lost ? i * 2.1f : 0));
        }
    }

    private static void Place(Sheep s, Vector3 at, float turn)
    {
        s.Root.Visible = true;
        s.Root.Position = at;
        s.Root.Rotation = new(0, turn, 0);
        s.Speed = 0;
    }

    /// <summary>Where a sheep stands in the fold, on the ground.</summary>
    private Vector3 FoldSpot(int number)
    {
        var spot = SheepPlans.FoldSpots[number % SheepPlans.FoldSpots.Length];
        return world.FoldPoint(spot.X, spot.Y);
    }

    /// <summary>
    /// The sheep the herd has just put in the fold run there from where they are, one after another, jump over the front
    /// rail and count themselves; <paramref name="before"/> sheep were home already.
    /// </summary>
    internal void Pen(IReadOnlyList<int> penned, int before)
    {
        for (int k = 0; k < penned.Count; k++)
        {
            var s = sheep[penned[k]];
            var spot = SheepPlans.FoldSpots[penned[k] % SheepPlans.FoldSpots.Length];
            s.Mode = Mode.ToFold;
            s.Delay = k * FoldGap;
            s.Clock = 0;
            s.From = s.Root.Position;
            s.Approach = world.FoldPoint(spot.X, SheepPlans.FoldFront - 1.4f);
            s.Landing = world.FoldPoint(spot.X, SheepPlans.FoldFront + 1.1f);
            s.Home = FoldSpot(penned[k]);
            s.Count = before + k + 1;
            s.Beacon.Visible = false;
            s.Root.Visible = true;
        }
        HomeCount = before + penned.Count;
    }

    /// <summary>A sheep has just joined the car: it bleats.</summary>
    internal void Joined(int number)
    {
        var s = sheep[number];
        s.Beacon.Visible = false;
        if (s.Root.IsInsideTree())
            s.Voice.Play();
        s.BleatTimer = 4;
    }

    /// <summary>
    /// Moves every sheep one frame: lost ones graze round their meadow and ring, found ones trot to their places behind
    /// the car, waiting ones stand, face the car, bleat and beckon, and those on their way home go into the fold.
    /// </summary>
    internal void Tick(float delta, SheepHerd herd, Vector3 car, Vector3 back)
    {
        if (delta <= 0)
            return;
        for (int i = 0; i < sheep.Length; i++)
        {
            var s = sheep[i];
            s.Phase += delta;
            switch (s.Mode)
            {
                case Mode.ToFold:
                case Mode.Jumping:
                    StepIntoFold(s, delta);
                    continue;
                case Mode.InFold:
                    Graze(s, delta, false);
                    continue;
            }
            var status = herd.StatusOf(i);
            if (!herd.Started || status == SheepStatus.Home)
            {
                s.Root.Visible = status == SheepStatus.Home;
                continue;
            }
            s.Root.Visible = true;
            if (status == SheepStatus.Lost)
            {
                s.Mode = Mode.Grazing;
                s.Beacon.Visible = false;
                // Round and round the meadow, slowly, a couple of metres from its middle.
                float a = s.Phase * .12f + i * 2.1f;
                var at = spots[i] + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.6f;
                at.Y = world.SheepSurface(at.X, at.Z);
                s.Root.Position = at;
                s.Root.Rotation = new(0, -a, 0);
                Graze(s, delta, true);
                continue;
            }
            var target = herd.Target(i, car, back, out var forward);
            target.Y = world.SheepSurface(target.X, target.Z);
            var offset = (target - s.Root.Position) with { Y = 0 };
            float distance = offset.Length();
            if (distance > 45)
            {
                // Far off its place (after a load or a jump of the car): it is put there.
                s.Root.Position = target;
                distance = 0;
            }
            float step = Mathf.Min(distance, CatchUp * delta);
            var move = distance > .001f ? offset / distance * step : Vector3.Zero;
            var next = s.Root.Position + move;
            next.Y = world.SheepSurface(next.X, next.Z);
            s.Speed = Mathf.Lerp(s.Speed, step / delta, 1 - Mathf.Exp(-6 * delta));
            s.Root.Position = next;
            var facing = s.Speed > .6f && move.LengthSquared() > 1e-6f ? move : forward;
            float wanted = Mathf.Atan2(facing.X, facing.Z);
            s.Root.Rotation = new(0, Mathf.LerpAngle(s.Root.Rotation.Y, wanted, 1 - Mathf.Exp(-8 * delta)), 0);
            if (status == SheepStatus.Waiting)
            {
                s.Mode = Mode.Waiting;
                s.Beacon.Visible = true;
                s.BleatTimer -= delta;
                if (s.BleatTimer <= 0)
                {
                    s.BleatTimer = 2.6f + (float)random.NextDouble();
                    if (s.Root.IsInsideTree())
                        s.Voice.Play();
                }
                Walk(s, 0, true);
                continue;
            }
            s.Mode = Mode.Trotting;
            s.Beacon.Visible = false;
            Walk(s, s.Speed, false);
            if (s.Speed > 2)
                Ring(s, delta, 1.1f);
        }
        foreach (var label in counts)
        {
            if (!label.Visible)
                continue;
            label.Position += Vector3.Up * delta * .8f;
            label.Modulate = label.Modulate with { A = Mathf.Max(0, label.Modulate.A - delta * .45f) };
            if (label.Modulate.A <= 0)
                label.Visible = false;
        }
    }

    /// <summary>One frame of a sheep's way into the fold.</summary>
    private void StepIntoFold(Sheep s, float delta)
    {
        if (s.Delay > 0)
        {
            s.Delay -= delta;
            Walk(s, 0, false);
            return;
        }
        if (s.Mode == Mode.ToFold)
        {
            var offset = (s.Approach - s.Root.Position) with { Y = 0 };
            float distance = offset.Length();
            float step = FoldRun * delta;
            if (distance <= step)
            {
                s.Root.Position = s.Approach;
                s.Mode = Mode.Jumping;
                s.Clock = 0;
                s.Root.Rotation = new(0, yaw, 0);
                if (s.Root.IsInsideTree())
                    s.Voice.Play();
                return;
            }
            var next = s.Root.Position + offset / distance * step;
            next.Y = world.SheepSurface(next.X, next.Z);
            s.Root.Position = next;
            s.Root.Rotation = new(0, Mathf.Atan2(offset.X, offset.Z), 0);
            Walk(s, FoldRun, false);
            Ring(s, delta, 1);
            return;
        }
        // Jumping over the rail on an arc, then trotting to its place.
        s.Clock += delta;
        if (s.Clock <= JumpTime)
        {
            float t = s.Clock / JumpTime;
            var at = s.Approach.Lerp(s.Landing, t);
            at.Y += JumpHeight * 4 * t * (1 - t);
            s.Root.Position = at;
            s.Body.Rotation = new(Mathf.Lerp(-.35f, .35f, t), 0, 0);
            foreach (var leg in s.Legs)
                leg.Rotation = new(.6f * Mathf.Sin(t * Mathf.Pi), 0, 0);
            return;
        }
        if (s.Count > 0)
        {
            ShowCount(s.Landing + Vector3.Up * 2.2f, s.Count);
            s.Count = 0;
        }
        s.Body.Rotation = Vector3.Zero;
        var rest = (s.Home - s.Root.Position) with { Y = 0 };
        float left = rest.Length();
        float walk = 2.4f * delta;
        if (left <= walk)
        {
            s.Root.Position = s.Home;
            s.Mode = Mode.InFold;
            return;
        }
        var go = s.Root.Position + rest / left * walk;
        go.Y = world.Ground.Height(go.X, go.Z);
        s.Root.Position = go;
        s.Root.Rotation = new(0, Mathf.Atan2(rest.X, rest.Z), 0);
        Walk(s, 2.4f, false);
    }

    /// <summary>The number of the sheep home, floating up over the rail and fading, the way sheep are counted at bedtime.</summary>
    private void ShowCount(Vector3 at, int count)
    {
        Label3D? label = null;
        foreach (var free in counts)
            if (!free.Visible)
                label = free;
        if (label == null)
        {
            label = Art.Sign(this, at, "", 96);
            label.Modulate = new Color("fff3c4");
            label.OutlineModulate = new Color("5a3a22");
            label.OutlineSize = 18;
            counts.Add(label);
        }
        label.Text = count.ToString();
        label.Position = at;
        label.Modulate = label.Modulate with { A = 1 };
        label.Visible = true;
    }

    /// <summary>A grazing sheep: its head goes down to the grass and up again, and now and then the bell rings.</summary>
    private void Graze(Sheep s, float delta, bool ring)
    {
        float nod = .5f + .5f * Mathf.Sin(s.Phase * .7f);
        s.Head.Rotation = new(Mathf.Lerp(-.1f, .75f, nod * nod), 0, 0);
        s.Head.Position = new(0, .74f - .18f * nod, .5f + .05f * nod);
        s.Body.Position = Vector3.Up * .01f * Mathf.Sin(s.Phase * 1.3f);
        foreach (var leg in s.Legs)
            leg.Rotation = Vector3.Zero;
        if (ring)
            Ring(s, delta, 1);
    }

    /// <summary>The bell rings every few seconds, faster when the sheep trots.</summary>
    private void Ring(Sheep s, float delta, float rate)
    {
        s.BellTimer -= delta * rate;
        if (s.BellTimer > 0)
            return;
        s.BellTimer = 2.2f + (float)random.NextDouble() * 1.8f;
        if (s.Root.IsInsideTree())
            s.Bell.Play();
    }

    /// <summary>The legs swing and the body bobs with <paramref name="speed"/>; a waiting sheep lifts its head to bleat.</summary>
    private static void Walk(Sheep s, float speed, bool calling)
    {
        float stride = Mathf.Clamp(speed / 6, 0, 1);
        float swing = Mathf.Sin(s.Phase * (6 + speed * .8f));
        for (int i = 0; i < s.Legs.Count; i++)
        {
            int sign = (i % 2 == 0) == (i < 2) ? 1 : -1;
            s.Legs[i].Rotation = new(sign * swing * .7f * stride, 0, 0);
        }
        s.Body.Position = Vector3.Up * .1f * stride * Mathf.Abs(swing);
        s.Head.Position = new(0, .74f, .5f);
        s.Head.Rotation = new(calling ? -.35f + .1f * Mathf.Sin(s.Phase * 9) : 0, 0, 0);
    }

    /// <summary>A small sheep's bell, made once: two bright partials that ring out.</summary>
    private static AudioStreamWav MakeBell() => Wave(.7f, (t, i) =>
        {
            float decay = MathF.Exp(-t * 6);
            return (MathF.Sin(MathF.Tau * 1320 * t) * .6f + MathF.Sin(MathF.Tau * 2110 * t) * .3f)
                * decay
                * MathF.Min(1, i / 60f);
        });

    /// <summary>A sheep's "béé", made once: a reedy tone that wobbles quickly, rises a little and fades.</summary>
    private static AudioStreamWav MakeBleat() => Wave(.75f, (t, i) =>
        {
            float wobble = 1 + .06f * MathF.Sin(MathF.Tau * 23 * t);
            float pitch = (360 + 50 * MathF.Sin(MathF.PI * t / .75f)) * wobble;
            float phase = MathF.Tau * pitch * t;
            float tone = MathF.Sin(phase) * .55f + MathF.Sin(2 * phase) * .25f + MathF.Sin(3 * phase) * .12f;
            float envelope = MathF.Min(1, t / .06f) * MathF.Min(1, (.75f - t) / .25f);
            return tone * envelope * (.75f + .25f * MathF.Sin(MathF.Tau * 23 * t));
        });

    private static AudioStreamWav Wave(float seconds, Func<float, int, float> sample)
    {
        const int rate = 22050;
        int samples = (int)(rate * seconds);
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float value = Math.Clamp(sample(i / (float)rate, i), -1, 1);
            short s = (short)(value * .5f * short.MaxValue);
            data[i * 2] = (byte)(s & 0xFF);
            data[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Stereo = false,
            Data = data,
        };
    }
}
