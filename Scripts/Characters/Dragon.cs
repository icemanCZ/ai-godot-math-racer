using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

/// <summary>
/// The dragon Kvído (<c>docs/DRAGON_PLAN.md</c>): a big, friendly green dragon with an orange belly, horns, a row of
/// spikes down his back and a curled tail, about fourteen metres long, made of balls and cylinders like the other
/// characters. He lies with +Z towards the valley. His chest breathes, his tail waves, his eyes blink and his head turns
/// to a car that comes near. While he has a cold he wears a scarf and sneezes rings of smoke; when he is well he puffs a
/// little flame now and then. <see cref="Sneeze"/> is the big sneeze that lights his fire, and while he flies
/// (<see cref="Flying"/>) his wings spread and beat. He is scenery: the character the player talks to stands under his
/// chin, and the ledge he lies on holds his solid shape.
/// </summary>
internal sealed partial class Dragon : Node3D
{
    private const string Green = "5fae5a",
        ScaleDark = "3f8a4a",
        ScaleLight = "77c46c",
        Belly = "f4cf7e",
        BellyStripe = "e0ad5c",
        Spike = "f08a3c",
        Horn = "fff2d0",
        Membrane = "f5a04a";

    /// <summary>Farther from the camera than this the dragon stays still: his motion is too small to see.</summary>
    private const float StillDistance = 260;

    /// <summary>Within this distance of the car he turns his head to it.</summary>
    private const float LookDistance = 60;

    private readonly Node3D body = new() { Name = "Body", Position = new(0, .3f, 0) };
    private readonly Node3D chest = new() { Name = "Chest", Position = new(0, 2.1f, 0) };
    private readonly Node3D neck = new() { Name = "Neck", Position = new(0, 2.9f, 2.5f) };
    private readonly Node3D head = new() { Name = "Head" };
    private readonly Node3D jaw = new() { Name = "Jaw", Position = new(0, -.45f, .25f) };
    private readonly Node3D scarf = new() { Name = "Scarf" };
    private readonly List<Node3D> lids = new();
    private readonly List<Node3D> tail = new();
    private readonly Node3D[] wings = new Node3D[2];
    private readonly Node3D flame = new() { Name = "Flame", Visible = false };
    private readonly List<(MeshInstance3D Ring, StandardMaterial3D Material)> rings = new();
    private readonly float[] ringAge = new float[RingCount];
    private readonly Vector3[] ringDrift = new Vector3[RingCount];

    /// <summary>How much bigger than the parts are laid out the dragon is drawn: a big dragon, taller than a cabin.</summary>
    internal const float Size = 1.3f;

    private const int RingCount = 6;
    private const int TailSegments = 10;
    private static readonly Vector3 HeadOnNeck = new(0, 2.35f, 2.7f);

    /// <summary>The head's tilt at rest: looking down at the nest in front of him.</summary>
    private const float RestPitch = .42f;

    private float time;
    private float blink = 2.5f;
    private float puff = 3;
    private float lookYaw,
        lookPitch = RestPitch;
    private float swell,
        swellTarget;
    private float sneeze;
    private float smallFlame;
    private Action? onFlame;
    private float celebration;
    private float spread;
    private bool still;
    private float stillCheck;

    /// <summary>Where the camera sits while the player rides: behind his head, above the root of his neck.</summary>
    internal Node3D Seat { get; } = new() { Name = "Seat", Position = new(0, 4.4f, 1.6f) };

    /// <summary>The tip of his snout, where smoke and fire come out.</summary>
    internal Node3D Mouth { get; } = new() { Name = "Mouth", Position = new(0, -.3f, 2.25f) };

    /// <summary>Whether his cold is over: no scarf, and a little flame instead of the rings of smoke.</summary>
    internal bool Healthy { get; set; }

    /// <summary>Whether he is in the air: his wings spread and beat, his tail and neck stretch out.</summary>
    internal bool Flying { get; set; }

    /// <summary>Whether the big sneeze is going on.</summary>
    internal bool Sneezing => sneeze > 0;

    /// <summary>Whether the flame is out at the moment, for the checks.</summary>
    internal bool Flaming => flame.Visible;

    /// <summary>How deep he has breathed in for the sneeze: one step for every answer of the riddle, up to three.</summary>
    internal int Breath { get; private set; }

    internal Dragon()
    {
        body.Scale = Vector3.One * Size;
        AddChild(body);
        body.AddChild(chest);
        BuildTorso();
        BuildLegs();
        body.AddChild(neck);
        BuildNeck();
        neck.AddChild(head);
        head.Position = HeadOnNeck;
        BuildHead();
        BuildTail();
        for (int i = 0; i < 2; i++)
            wings[i] = BuildWing(i == 0 ? 1 : -1);
        neck.AddChild(Seat);
        Pose(0);
    }

    /// <summary>Lays him down on his ledge, his wings folded.</summary>
    internal void Rest(Transform3D home)
    {
        Transform = home;
        Flying = false;
        spread = 0;
        Pose(0);
    }

    /// <summary>Takes a deeper breath: <paramref name="breath"/> answers of the riddle have been given.</summary>
    internal void Inhale(int breath)
    {
        Breath = Math.Clamp(breath, 0, 3);
        swellTarget = Breath * .06f;
    }

    /// <summary>
    /// The big sneeze: he breathes in with his head thrown back, then sneezes it forward with a great flame, when
    /// <paramref name="flaming"/> is called (the fire catches), and settles again.
    /// </summary>
    internal void Sneeze(Action? flaming)
    {
        sneeze = SneezeLength;
        onFlame = flaming;
    }

    private const float SneezeLength = 3.2f,
        SneezeFlame = 2.0f;

    /// <summary>A happy wiggle: he lifts his head, wags his tail and half opens his wings.</summary>
    internal void Celebrate() => celebration = 2.5f;

    /// <summary>One frame of his life, with <paramref name="car"/> where the car is.</summary>
    internal void Tick(float delta, Vector3 car)
    {
        if (rings.Count == 0)
            BuildPuffs();
        time += delta;
        if ((stillCheck -= delta) <= 0)
        {
            stillCheck = .5f;
            still =
                !Flying
                && sneeze <= 0
                && GetViewport()?.GetCamera3D() is { } camera
                && camera.GlobalPosition.DistanceSquaredTo(GlobalPosition) > StillDistance * StillDistance;
        }
        TickRings(delta);
        if (still)
            return;
        celebration = Mathf.Max(0, celebration - delta);
        swell = Mathf.Lerp(swell, swellTarget, 1 - Mathf.Exp(-delta * 3));
        spread = Mathf.MoveToward(
            spread,
            Flying ? 1
                : celebration > 0 ? .45f
                : 0,
            delta * 1.6f
        );
        LookAround(delta, car);
        TickSneeze(delta);
        if (!Flying && sneeze <= 0)
            TickPuffs(delta, car);
        Pose(delta);
    }

    /// <summary>Turns his head to the car within reach, else down to his nest; in the air he looks ahead.</summary>
    private void LookAround(float delta, Vector3 car)
    {
        float yaw = 0,
            pitch = RestPitch;
        if (Flying)
            pitch = .05f;
        else if (IsInsideTree() && GlobalPosition.DistanceSquaredTo(car) < LookDistance * LookDistance)
        {
            var local = ToLocal(car) - (neck.Position + HeadOnNeck + body.Position);
            yaw = Mathf.Clamp(Mathf.Atan2(local.X, Mathf.Max(local.Z, .5f)), -.75f, .75f);
            pitch = Mathf.Clamp(Mathf.Atan2(-local.Y, new Vector2(local.X, local.Z).Length()), -.1f, .7f);
        }
        if (celebration > 0)
            pitch -= .35f;
        float blend = 1 - Mathf.Exp(-delta * 2.5f);
        lookYaw = Mathf.LerpAngle(lookYaw, yaw, blend);
        lookPitch = Mathf.Lerp(lookPitch, pitch, blend);
    }

    private void TickSneeze(float delta)
    {
        if (sneeze <= 0)
            return;
        float before = sneeze;
        sneeze = Mathf.Max(0, sneeze - delta);
        // The flame comes out SneezeFlame seconds before the end.
        if (before > SneezeFlame && sneeze <= SneezeFlame)
        {
            onFlame?.Invoke();
            onFlame = null;
            for (int i = 0; i < 3; i++)
                Puff(i * .25f);
        }
        flame.Visible = sneeze is > .6f and <= SneezeFlame;
        if (flame.Visible)
        {
            float t = (SneezeFlame - sneeze) / (SneezeFlame - .6f);
            float grow = Mathf.Sin(Mathf.Min(1, t * 1.3f) * Mathf.Pi * .5f) * (1 - Mathf.Pow(t, 6));
            float flicker = 1 + .12f * Mathf.Sin(time * 41) + .08f * Mathf.Sin(time * 67);
            flame.Scale = new Vector3(flicker, flicker, Mathf.Max(.05f, grow * flicker));
        }
        if (sneeze <= 0)
        {
            Breath = 0;
            swellTarget = 0;
        }
    }

    /// <summary>With a cold, a ring of smoke now and then, more often when the car is near; when well, a little flame.</summary>
    private void TickPuffs(float delta, Vector3 car)
    {
        if ((puff -= delta) > 0)
            return;
        bool near = IsInsideTree() && GlobalPosition.DistanceSquaredTo(car) < 45 * 45;
        if (Healthy)
        {
            puff = 5 + 2 * Mathf.Sin(time * 1.3f);
            flame.Visible = true;
            flame.Scale = new Vector3(.45f, .45f, .25f);
            smallFlame = .4f;
            Puff(0);
        }
        else
        {
            puff = (near ? 2.3f : 3.8f) + Mathf.Sin(time * .7f);
            Puff(0);
            // Now and then a little sneeze that only blows two rings.
            if (near && Mathf.Sin(time * 2.1f) > .55f)
            {
                celebration = Mathf.Max(celebration, .4f);
                Puff(.3f);
            }
        }
    }

    /// <summary>Sets every moving part from the breath, the look, the sneeze, the tail's wave and the wings.</summary>
    private void Pose(float delta)
    {
        float breath = Mathf.Sin(time * 1.4f);
        float deep = 1 + swell + breath * .035f;
        chest.Scale = new Vector3(deep, 1 + swell * 1.2f + breath * .045f, 1 + swell * .4f);
        float sneezePitch = 0;
        float jawOpen = .05f;
        if (sneeze > 0)
        {
            float t = SneezeLength - sneeze;
            // Head back while breathing in, then forward and down with the sneeze, then back to rest.
            sneezePitch =
                t < 1.2f ? -.55f * Mathf.SmoothStep(0, 1.2f, t)
                : t < 1.45f ? Mathf.Lerp(-.55f, .45f, (t - 1.2f) / .25f)
                : .45f * (1 - Mathf.SmoothStep(1.45f, SneezeLength, t));
            jawOpen = t is > 1.1f and < 2.8f ? .45f : .05f;
            if (t < 1.2f)
                chest.Scale *= 1 + .08f * Mathf.SmoothStep(0, 1.2f, t);
        }
        else if (flame.Visible)
            jawOpen = .3f;
        float bob = celebration > 0 ? Mathf.Abs(Mathf.Sin(celebration * 6)) * .35f : 0;
        body.Position = new Vector3(0, .3f + bob + (Flying ? .4f : 0), 0);
        neck.Rotation = new Vector3((Flying ? .5f : 0) + lookPitch * .35f, lookYaw * .6f, 0);
        head.Rotation = new Vector3(
            lookPitch * .65f + sneezePitch - (Flying ? .5f : 0),
            lookYaw * .4f,
            Mathf.Sin(time * .9f) * .03f
        );
        jaw.Rotation = new Vector3(jawOpen, 0, 0);
        scarf.Visible = !Healthy;
        // Blink every few seconds.
        if ((blink -= delta) <= 0)
            blink = 2.2f + 2.5f * (.5f + .5f * Mathf.Sin(time * 3.7f));
        bool shut = blink < .14f;
        foreach (var lid in lids)
            lid.Visible = shut;
        // The tail curls round beside him and waves; in the air it streams out behind.
        float curl = Flying ? 0 : 1;
        float speed = celebration > 0 ? 6 : 1.6f;
        for (int i = 0; i < tail.Count; i++)
        {
            float wave = Mathf.Sin(time * speed - i * .55f) * (celebration > 0 ? .16f : .07f);
            tail[i].Rotation = new Vector3(
                Flying ? Mathf.Sin(time * 2 - i * .6f) * .05f
                    : i == 0 ? -.08f
                    : .03f,
                curl * (i == 0 ? .1f : .2f) + wave,
                0
            );
        }
        // The wings: folded on his flanks, or spread and beating.
        float beat =
            Flying ? .3f + .55f * Mathf.Sin(time * 3.4f)
            : celebration > 0 ? .3f * Mathf.Sin(time * 7)
            : 0;
        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? 1 : -1;
            var folded =
                new Quaternion(Vector3.Up, side * 1.62f)
                * new Quaternion(Vector3.Forward, side * .32f)
                * new Quaternion(Vector3.Right, -1.9f);
            var open = new Quaternion(Vector3.Back, side * beat) * new Quaternion(Vector3.Up, side * -.12f);
            wings[i].Quaternion = folded.Slerp(open, spread);
        }
    }

    private static StandardMaterial3D Glow(string color, float alpha = 1) =>
        new()
        {
            AlbedoColor = new Color(color).SrgbToLinear() with { A = alpha },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = alpha < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        };

    /// <summary>The flame and the pool of smoke rings; made on the first tick, after the outline, which they do without.</summary>
    private void BuildPuffs()
    {
        Mouth.AddChild(flame);
        foreach (
            var (radius, length, color) in new[]
            {
                (.95f, 6.5f, "ff6a1a"),
                (.6f, 5.2f, "ffb02e"),
                (.3f, 3.6f, "fff2a0"),
            }
        )
        {
            var cone = Art.Cylinder(flame, new Vector3(0, 0, length / 2), radius, .02f, length, color);
            cone.Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
            cone.MaterialOverride = Glow(color, .85f);
            cone.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        for (int i = 0; i < RingCount; i++)
        {
            var material = Glow("d9dcdf", 0);
            var ring = new MeshInstance3D
            {
                Name = "SmokeRing",
                Mesh = new TorusMesh
                {
                    InnerRadius = .16f,
                    OuterRadius = .34f,
                    Rings = 12,
                    RingSegments = 6,
                },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                TopLevel = true,
                Visible = false,
            };
            AddChild(ring);
            rings.Add((ring, material));
            ringAge[i] = -1;
        }
    }

    /// <summary>Blows a ring of smoke out of the nose, <paramref name="late"/> seconds later than now.</summary>
    private void Puff(float late)
    {
        if (!IsInsideTree() || rings.Count == 0)
            return;
        // A free ring has the age -1, one that waits to be blown -10 less the seconds it still waits.
        int free = Array.IndexOf(ringAge, -1f);
        if (free < 0)
            free = 0;
        if (late > 0)
        {
            ringAge[free] = -10 - late;
            rings[free].Ring.Visible = false;
            return;
        }
        Launch(free);
    }

    /// <summary>Puts a ring at the tip of the snout, facing the way it flies: forward out of the nose and up.</summary>
    private void Launch(int i)
    {
        var (ring, material) = rings[i];
        var mouth = Mouth.GlobalTransform;
        var forward = mouth.Basis.Z.Normalized();
        ringDrift[i] = forward * 2.2f + Vector3.Up * 1.4f;
        ring.GlobalTransform = new Transform3D(
            new Basis(new Quaternion(Vector3.Up, (forward + Vector3.Up * .6f).Normalized())),
            mouth.Origin
        );
        material.AlbedoColor = material.AlbedoColor with { A = .75f };
        ringAge[i] = 0;
        ring.Visible = true;
    }

    private void TickRings(float delta)
    {
        if (smallFlame > 0 && (smallFlame -= delta) <= 0 && sneeze <= 0)
            flame.Visible = false;
        for (int i = 0; i < rings.Count; i++)
        {
            var (ring, material) = rings[i];
            float age = ringAge[i];
            if (age <= -10)
            {
                age += delta;
                if (age > -10)
                    Launch(i);
                else
                    ringAge[i] = age;
                continue;
            }
            if (age < 0)
                continue;
            age += delta;
            const float life = 3.2f;
            if (age >= life)
            {
                ringAge[i] = -1;
                ring.Visible = false;
                continue;
            }
            ringAge[i] = age;
            ring.GlobalPosition += ringDrift[i] * delta * (1 - age / life * .6f);
            float grow = 1 + age * 1.1f;
            ring.Scale = Vector3.One * grow;
            material.AlbedoColor = material.AlbedoColor with { A = .75f * (1 - age / life) };
        }
    }

    // ---- The model ----

    private static void Claws(Node3D parent, Vector3 toe, float spacing)
    {
        foreach (int i in new[] { -1, 0, 1 })
        {
            var claw = Art.Cylinder(parent, toe + new Vector3(i * spacing, 0, 0), .11f, .01f, .42f, Horn);
            claw.Rotation = new Vector3(Mathf.Pi / 2, 0, 0);
        }
    }

    /// <summary>A cylinder from <paramref name="from"/> to <paramref name="to"/> in the parent's space.</summary>
    private static MeshInstance3D Bone(Node3D parent, Vector3 from, Vector3 to, float radius, string color)
    {
        var bone = Art.Cylinder(parent, (from + to) / 2, radius, radius * .7f, from.DistanceTo(to), color);
        bone.Quaternion = new Quaternion(Vector3.Up, (to - from).Normalized());
        return bone;
    }

    private void BuildTorso()
    {
        Art.Ball(chest, Vector3.Zero, new(2.0f, 1.75f, 3.3f), Green);
        Art.Ball(chest, new(0, -.5f, .95f), new(1.62f, 1.3f, 2.55f), Belly);
        // Pale bands across the belly, as a crocodile's or a caterpillar's.
        for (int i = 0; i < 4; i++)
        {
            float z = 3.15f - i * .5f;
            var band = Art.Ball(chest, new(0, -.15f - i * .32f, z - .1f), new(1.0f - i * .1f, .09f, .3f), BellyStripe);
            band.Rotation = new Vector3(-.5f - i * .18f, 0, 0);
        }
        // Spikes from the shoulders down to the root of the tail.
        for (int i = 0; i < 7; i++)
        {
            float z = 2.2f - i * .8f;
            float y = 1.75f * Mathf.Sqrt(Mathf.Max(0, 1 - z * z / (3.3f * 3.3f)));
            float h = .8f - Mathf.Abs(i - 2) * .08f;
            var spike = Art.Cylinder(chest, new(0, y + h * .35f, z), .32f, .02f, h, Spike);
            spike.Rotation = new Vector3(-.35f, 0, 0);
        }
        // Darker scales scattered on the back.
        foreach (
            var (x, z) in new[] { (.9f, .8f), (-1.0f, -.4f), (.7f, -1.6f), (-.6f, 1.4f), (1.2f, -.9f), (-1.2f, .5f) }
        )
        {
            float y = 1.7f * Mathf.Sqrt(Mathf.Max(0, 1 - x * x / 4 - z * z / (3.3f * 3.3f)));
            Art.Ball(chest, new(x, y - .05f, z), new(.32f, .1f, .32f), ScaleDark);
        }
    }

    private void BuildLegs()
    {
        foreach (int side in new[] { -1, 1 })
        {
            // The hind legs folded under him, their big feet forward.
            Art.Ball(body, new(side * 1.75f, 1.2f, -1.55f), new(1.05f, 1.1f, 1.45f), Green);
            Art.Ball(body, new(side * 2.0f, .3f, -.35f), new(.68f, .33f, 1.0f), ScaleDark);
            Claws(body, new(side * 2.0f, .25f, .62f), .26f);
            // The front legs and paws, side by side in front of his chest.
            Art.Ball(body, new(side * 1.45f, 1.0f, 2.35f), new(.56f, .95f, .62f), Green);
            Art.Ball(body, new(side * 1.5f, .3f, 3.15f), new(.66f, .32f, .82f), ScaleDark);
            Claws(body, new(side * 1.5f, .24f, 3.95f), .22f);
        }
    }

    private void BuildNeck()
    {
        for (int i = 0; i < 6; i++)
        {
            float t = i / 5f;
            var at = new Vector3(0, 2.2f * Mathf.Pow(t, .85f), 2.45f * t);
            float r = 1.15f - .33f * t;
            Art.Ball(neck, at, new(r, r, r * 1.05f), Green);
            Art.Ball(neck, at + new Vector3(0, -r * .45f, r * .55f), new(r * .75f, r * .55f, r * .5f), Belly);
            if (i is > 0 and < 5)
            {
                var spike = Art.Cylinder(neck, at + new Vector3(0, r * .95f, -.1f), .24f, .02f, .55f, Spike);
                spike.Rotation = new Vector3(-.6f, 0, 0);
            }
        }
        // The red and white scarf of a dragon with a cold, wound round his neck with its ends hanging down.
        neck.AddChild(scarf);
        scarf.Position = new Vector3(0, .95f, 1.05f);
        scarf.Rotation = new Vector3(.75f, 0, 0);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            Art.Ball(
                scarf,
                new(Mathf.Sin(a) * 1.05f, 0, Mathf.Cos(a) * 1.05f),
                new(.42f, .3f, .42f),
                i % 2 == 0 ? "d9432f" : "fff5df"
            );
        }
        foreach (var (x, drop, color) in new[] { (.75f, .6f, "d9432f"), (1.05f, .45f, "fff5df") })
        {
            var end = Art.Block(scarf, new(x, -drop, .9f), new(.36f, 1.1f, .14f), color);
            end.Rotation = new Vector3(-.75f, 0, .25f);
        }
    }

    private void BuildHead()
    {
        Art.Ball(head, Vector3.Zero, new(1.2f, 1.0f, 1.25f), Green);
        Art.Ball(head, new(0, -.3f, 1.15f), new(.95f, .68f, 1.05f), ScaleLight);
        head.AddChild(jaw);
        Art.Ball(jaw, new(0, -.2f, .75f), new(.82f, .32f, .95f), Belly);
        foreach (int side in new[] { -1, 1 })
        {
            var tooth = Art.Cylinder(jaw, new(side * .33f, .06f, 1.45f), .07f, .01f, .24f, "ffffff");
            tooth.Rotation = new Vector3(0, 0, 0);
        }
        // A wide smile on the snout and two big nostrils at its tip.
        Art.Ball(head, new(0, -.58f, 1.6f), new(.62f, .05f, .5f), "3b2a2a");
        head.AddChild(Mouth);
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(head, new(side * .33f, -.02f, 2.08f), new(.14f, .11f, .08f), "2f5a33");
            // Big friendly eyes with a glint, and the lids that blink over them.
            Art.Ball(head, new(side * .6f, .38f, .78f), new(.38f, .42f, .3f), "fffaf0");
            Art.Ball(head, new(side * .64f, .36f, 1.02f), new(.2f, .26f, .1f), "26302f");
            Art.Ball(head, new(side * .58f, .5f, 1.1f), new(.07f, .07f, .04f), "ffffff");
            var lid = new Node3D { Name = "Lid", Visible = false };
            head.AddChild(lid);
            Art.Ball(lid, new(side * .6f, .4f, .8f), new(.42f, .46f, .34f), ScaleDark);
            lids.Add(lid);
            var brow = Art.Ball(head, new(side * .62f, .88f, .76f), new(.34f, .08f, .12f), ScaleDark);
            brow.Rotation = new Vector3(0, 0, side * -.25f);
            Art.Ball(head, new(side * .86f, -.32f, .92f), new(.22f, .15f, .08f), "f4a29b");
            // Horns that sweep back and the orange frills behind the cheeks.
            var horn = Art.Cylinder(head, new(side * .55f, 1.0f, -.45f), .2f, .03f, 1.15f, Horn);
            horn.Rotation = new Vector3(-.75f, 0, side * -.28f);
            var frill = Art.Ball(head, new(side * 1.12f, .2f, -.3f), new(.12f, .42f, .52f), Spike);
            frill.Rotation = new Vector3(.3f, side * .3f, side * -.3f);
        }
        for (int i = 0; i < 3; i++)
        {
            var spike = Art.Cylinder(head, new(0, .95f - i * .1f, -.15f - i * .45f), .2f, .02f, .5f, Spike);
            spike.Rotation = new Vector3(-.6f, 0, 0);
        }
    }

    private void BuildTail()
    {
        Node3D parent = body;
        var at = new Vector3(0, 1.45f, -3.0f);
        for (int i = 0; i < TailSegments; i++)
        {
            float t = i / (float)(TailSegments - 1);
            var segment = new Node3D { Name = $"Tail{i}", Position = at };
            parent.AddChild(segment);
            float r = Mathf.Lerp(1.05f, .3f, t);
            Art.Ball(segment, new(0, 0, -.5f), new(r, r * .9f, .75f), Green);
            if (i % 2 == 1)
            {
                var spike = Art.Cylinder(
                    segment,
                    new(0, r * .85f, -.5f),
                    .2f * (1.2f - t),
                    .02f,
                    .5f * (1.2f - t),
                    Spike
                );
                spike.Rotation = new Vector3(-.6f, 0, 0);
            }
            tail.Add(segment);
            parent = segment;
            // Each segment hangs on to the one before, a little lower until the tail lies on the ledge.
            at = new Vector3(0, i < 4 ? -.24f : 0, -1.0f);
        }
        // The spade at the tip.
        var tip = Art.Ball(parent, new(0, 0, -1.15f), new(.55f, .12f, .62f), Spike);
        tip.Rotation = new Vector3(0, Mathf.Pi / 4, 0);
    }

    /// <summary>
    /// A wing on side <paramref name="side"/>: an arm with three long fingers and an orange skin between them. Built
    /// spread out sideways with the skin trailing behind; <see cref="Pose"/> folds it on the flank.
    /// </summary>
    private Node3D BuildWing(int side)
    {
        var wing = new Node3D { Name = side > 0 ? "WingRight" : "WingLeft", Position = new(side * 1.25f, 3.45f, 1.1f) };
        body.AddChild(wing);
        var elbow = new Vector3(side * 2.6f, .55f, .3f);
        var wrist = new Vector3(side * 5.0f, .85f, -.1f);
        var tips = new[]
        {
            new Vector3(side * 6.4f, .3f, -1.7f),
            new Vector3(side * 5.0f, .05f, -3.6f),
            new Vector3(side * 3.0f, 0, -4.0f),
        };
        Bone(wing, Vector3.Zero, elbow, .28f, Green);
        Bone(wing, elbow, wrist, .22f, Green);
        foreach (var tip in tips)
            Bone(wing, wrist, tip, .1f, ScaleDark);
        Art.Ball(wing, wrist, Vector3.One * .26f, Green);
        var claw = Art.Cylinder(wing, wrist + new Vector3(side * .2f, .3f, .1f), .1f, .01f, .45f, Horn);
        claw.Rotation = new Vector3(0, 0, side * -.6f);
        // The skin between the fingers, with a scalloped trailing edge.
        var outline = new List<Vector3> { Vector3.Zero, elbow, wrist, tips[0] };
        outline.Add((tips[0] + tips[1]) / 2 + new Vector3(-side * .5f, 0, .6f));
        outline.Add(tips[1]);
        outline.Add((tips[1] + tips[2]) / 2 + new Vector3(0, 0, .7f));
        outline.Add(tips[2]);
        outline.Add(new Vector3(side * 1.4f, 0, -2.4f));
        outline.Add(new Vector3(side * .3f, 0, -1.2f));
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        var center = new Vector3(side * 3.0f, .2f, -1.6f);
        for (int i = 0; i < outline.Count; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Count];
            tool.SetNormal(Vector3.Up);
            tool.AddVertex(center);
            tool.AddVertex(a);
            tool.AddVertex(b);
        }
        var skin = new MeshInstance3D
        {
            Name = "WingSkin",
            Mesh = tool.Commit(),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(Membrane).SrgbToLinear(),
                Roughness = .9f,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
        };
        wing.AddChild(skin);
        return wing;
    }
}
