using System.Collections.Generic;
using Godot;
using MathRacer.Garage;
using MathRacer.Graphics;

namespace MathRacer.Vehicles;

/// <summary>
/// The coachwork that is not a car: a plane with a pilot, a peaceful tank, a steam engine with a face, a rocket and a
/// flying saucer with a little green passenger. Like the cars they are drawn in the vehicle's local space (the front is
/// −Z, the ground is Y = 0, about 2 m wide and 3.3 m long) and take the garage paints through the body, roof and trim
/// parts. A toy brings the wheels it needs; <see cref="CarBody.OwnWheels"/> hides the four tyres of the car. Each toy
/// but the steam engine (which smokes) has a part that moves as it drives, a <see cref="Gadget"/>.
/// </summary>
internal static class VehicleToys
{
    internal static bool Handles(string id) => id is "plane" or "tank" or "train" or "rocket" or "ufo";

    internal static void Build(Node3D root, VehicleAppearance appearance, string id, List<Node3D> wheels)
    {
        var kit = new Kit(root, appearance, id, wheels);
        switch (id)
        {
            case "plane":
                Plane(kit);
                break;
            case "tank":
                Tank(kit);
                break;
            case "train":
                Train(kit);
                break;
            case "rocket":
                Rocket(kit);
                break;
            case "ufo":
                Saucer(kit);
                break;
        }
    }

    private static void Plane(Kit k)
    {
        const string Skin = "ffd7b0",
            Dark = "344555";
        // A chubby fuselage of two eggs, a ring of engine cowl, a round spinner and a propeller with twisted blades.
        k.Body(k.Ball(new(0, 1.05f, -.15f), new(.64f, .62f, 1.2f), "f54257"));
        k.Body(k.Ball(new(0, 1.12f, .95f), new(.3f, .3f, .72f), "f54257"));
        k.Trim(k.Tube(new(0, 1.05f, -1.3f), .5f, .5f, .36f, "fff0ce"));
        k.Tube(new(0, 1.05f, -1.49f), .37f, .37f, .04f, Dark);
        k.Body(k.Ball(new(0, 1.05f, -1.53f), new(.24f, .24f, .3f), "f54257"));
        var propeller = k.Pivot("Propeller", new(0, 1.05f, -1.72f));
        foreach (float end in new[] { -1f, 1f })
        {
            var blade = k.Trim(k.Ball(new(0, end * .42f, 0), new(.11f, .42f, .035f), "fff0ce", propeller));
            blade.Rotation = new(0, end * .35f, 0);
        }
        k.Ball(Vector3.Zero, new(.07f, .07f, .07f), Dark, propeller);
        // Fast, the blades melt into a pale disc, as a real propeller does to the eye.
        var blur = k.Disc(new(0, 1.05f, -1.73f), .47f, .01f, "fff5df");
        blur.Rotation = new(Mathf.Pi / 2, 0, 0);
        var haze = k.Clear(blur, "fff5df", 0);
        blur.Visible = false;

        // The pilot in a leather cap and yellow goggles, under a see-through canopy.
        k.Ball(new(0, 1.62f, -.05f), new(.19f, .19f, .19f), Skin);
        k.Ball(new(0, 1.71f, -.03f), new(.2f, .13f, .2f), "8a5a3c");
        foreach (float side in new[] { -1f, 1f })
        {
            k.Ball(new(side * .07f, 1.65f, -.21f), new(.065f, .065f, .03f), "ffd04b");
            k.Ball(new(side * .07f, 1.65f, -.225f), new(.035f, .04f, .015f), Dark);
        }
        k.Ball(new(0, 1.55f, -.2f), new(.04f, .03f, .02f), "e98a7d");
        k.Clear(k.Ball(new(0, 1.52f, -.1f), new(.4f, .34f, .62f), "bfeaf5"), "bfeaf5", .4f);

        // A parasol wing on struts, with round tips, and a tail with a fin and a stabiliser.
        k.Roof(k.Block(new(0, 1.98f, .05f), new(2f, .09f, 1f), "ff8992"));
        foreach (float side in new[] { -1f, 1f })
        {
            k.Trim(k.Ball(new(side * 1f, 1.98f, .05f), new(.3f, .045f, .5f), "fff0ce"));
            foreach (float z in new[] { -.3f, .35f })
                k.Beam(new(side * .26f, 1.58f, z), new(side * .7f, 1.95f, z), .06f, Dark);
            k.Stripe(new(side * .35f, 2.027f, .05f), new(.12f, .012f, .9f));
            k.Stripe(new(side * .7f, 2.027f, .05f), new(.12f, .012f, .9f));
        }
        k.Roof(k.Ball(new(0, 1.7f, 1.2f), new(.045f, .48f, .3f), "ff8992"));
        var rudder = k.Pivot("Rudder", new(0, 1.68f, 1.42f));
        k.Trim(k.Ball(new(0, 0, .12f), new(.04f, .36f, .17f), "fff0ce", rudder));
        k.Roof(k.Ball(new(0, 1.28f, 1.42f), new(.78f, .045f, .3f), "ff8992"));

        // Landing gear: two main wheels under the wings and a tail wheel.
        foreach (float side in new[] { -1f, 1f })
        {
            k.Beam(new(side * .3f, .7f, -.35f), new(side * .6f, .32f, -.35f), .07f, Dark);
            k.Wheel(new(side * .62f, .3f, -.35f), .3f, .16f, "263440");
        }
        k.Beam(new(0, .9f, 1.35f), new(0, .17f, 1.38f), .06f, Dark);
        k.Wheel(new(.05f, .15f, 1.38f), .15f, .1f, "263440");
        k.Wheel(new(-.05f, .15f, 1.38f), .15f, .1f, "263440");

        // The engine idles while the plane stands and roars with the pedal; the rudder leans into a turn.
        float spin = 0,
            turn = 0;
        k.Moves(drive =>
        {
            float rate = 9 + drive.Effort * 30 + Mathf.Abs(drive.Speed) * 1.6f;
            spin = Mathf.Lerp(spin, rate, Gadget.Ease(3, drive.Dt));
            propeller.RotateZ(Mathf.Min(spin, 44) * drive.Dt);
            float melt = Mathf.Clamp((spin - 18) / 30, 0, 1);
            blur.Visible = melt > 0;
            haze.AlbedoColor = haze.AlbedoColor with { A = melt * .42f };
            turn = Mathf.Lerp(turn, Mathf.Clamp(-drive.Turn * .6f, -.55f, .55f), Gadget.Ease(8, drive.Dt));
            rudder.Rotation = new(0, turn, 0);
        });
    }

    private static void Tank(Kit k)
    {
        const string Rubber = "3a4650",
            Dark = "344555",
            Gun = "6a7a88";
        // Two tracks with rounded ends, a belly between them and a row of road wheels on each side.
        k.Block(new(0, .3f, 0), new(1.1f, .5f, 2.6f), Dark);
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * .76f;
            k.Block(new(x, .38f, 0), new(.42f, .76f, 2.3f), Rubber);
            foreach (float z in new[] { -1.15f, 1.15f })
                k.Disc(new(x, .38f, z), .38f, .42f, Rubber);
            for (int i = 0; i < 9; i++)
                k.Block(new(x, .775f, -1.2f + i * .3f), new(.46f, .04f, .13f), "2b353d");
            foreach (var (z, radius) in new[] { (-1.15f, .3f), (-.4f, .27f), (.4f, .27f), (1.15f, .3f) })
                k.Wheel(new(side * .97f, .38f, z), radius, .1f, "5b6873");
        }

        // The hull, a round turret with a hatch, and a long gun with a flower in its muzzle.
        k.Body(
            VehicleModel.Loft(
                k.Root,
                new[]
                {
                    new VehicleModel.Ring(.5f, .8f, 1.42f, 0, .3f),
                    new VehicleModel.Ring(.76f, .9f, 1.47f, 0, .32f),
                    new VehicleModel.Ring(1.1f, .84f, 1.38f, 0, .32f),
                    new VehicleModel.Ring(1.24f, .74f, 1.2f, 0, .32f),
                },
                "f54257"
            )
        );
        // The turret, its driver and the gun turn together on a ring, drawn round the turret's axis.
        var turret = k.Pivot("Turret", new(0, 0, .1f));
        k.Trim(k.Post(new(0, 1.28f, 0), .7f, .7f, .08f, "fff0ce", parent: turret));
        k.Roof(k.Post(new(0, 1.45f, 0), .66f, .6f, .34f, "ff8992", parent: turret));
        k.Roof(k.Ball(new(0, 1.6f, 0), new(.58f, .2f, .58f), "ff8992", turret));
        k.Trim(k.Post(new(0, 1.76f, .18f), .2f, .2f, .06f, "fff0ce", parent: turret));
        k.Ball(new(0, 1.83f, .18f), new(.17f, .17f, .17f), "ffd7b0", turret);
        k.Ball(new(0, 1.9f, .18f), new(.19f, .11f, .19f), "5aa86f", turret);
        foreach (float side in new[] { -1f, 1f })
            k.Ball(new(side * .06f, 1.85f, .02f), new(.025f, .03f, .02f), Dark, turret);
        k.Tube(new(0, 1.52f, -1.2f), .1f, .1f, 1.3f, Gun, turret);
        k.Trim(k.Tube(new(0, 1.52f, -1.82f), .15f, .15f, .16f, "fff0ce", turret));
        k.Ball(new(0, 1.52f, -1.93f), new(.06f, .06f, .05f), "ffca38", turret);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6;
            k.Ball(
                new(Mathf.Cos(angle) * .1f, 1.52f + Mathf.Sin(angle) * .1f, -1.93f),
                new(.055f, .055f, .04f),
                "fff5df",
                turret
            );
        }

        // Lamps in front, a grille and two pipes behind.
        foreach (float side in new[] { -1f, 1f })
        {
            k.Trim(k.Ball(new(side * .55f, .98f, -1.4f), new(.17f, .17f, .09f), "fff0ce"));
            k.Glow(k.Ball(new(side * .55f, .98f, -1.46f), new(.12f, .12f, .05f), "fff3c4"), "ffe6a1");
            k.Tube(new(side * .45f, .85f, 1.5f), .08f, .08f, .3f, Gun);
        }
        k.Block(new(0, .95f, 1.47f), new(.9f, .3f, .06f), Dark);
        for (int i = -2; i <= 2; i++)
            k.Trim(k.Block(new(i * .16f, .95f, 1.51f), new(.04f, .26f, .03f), "fff0ce"));
        foreach (float x in new[] { -.25f, .25f })
        {
            k.Stripe(new(x, 1.265f, -.82f), new(.13f, .012f, .4f));
            k.Stripe(new(x, 1.265f, 1.0f), new(.13f, .012f, .3f));
        }

        // In a turn the turret runs ahead of the hull, so it turns faster than the tank, and swings back on a spring
        // when the tank drives straight on; standing, the driver slowly looks round the valley.
        float yaw = 0,
            swing = 0;
        k.Moves(drive =>
        {
            float standing = 1 - Mathf.Clamp(Mathf.Abs(drive.Speed) / 2, 0, 1);
            float target =
                Mathf.Clamp(drive.Turn * TurretLead, -.7f, .7f) + standing * .35f * Mathf.Sin(drive.Time * .45f);
            swing += ((target - yaw) * 30 - swing * 6) * drive.Dt;
            yaw += swing * drive.Dt;
            turret.Rotation = new(0, yaw, 0);
        });
    }

    /// <summary>How many seconds of a turn the tank's turret runs ahead of the hull.</summary>
    internal const float TurretLead = .6f;

    private static void Train(Kit k)
    {
        const string Iron = "3b3b44",
            Dark = "293e50";
        // The frame, a boiler with brass bands, a smoke box with a smiling face and a chimney that smokes.
        k.Block(new(0, .62f, .05f), new(1.55f, .22f, 3.05f), Iron);
        foreach (float side in new[] { -1f, 1f })
            k.Trim(k.Block(new(side * .8f, .62f, 0), new(.06f, .2f, 3f), "fff0ce"));
        k.Body(k.Tube(new(0, 1.28f, -.62f), .58f, .58f, 1.75f, "f54257"));
        foreach (float z in new[] { -.25f, -.8f })
            k.Trim(k.Tube(new(0, 1.28f, z), .6f, .6f, .08f, "fff0ce"));
        k.Tube(new(0, 1.28f, -1.4f), .6f, .6f, .3f, Iron);
        foreach (float side in new[] { -1f, 1f })
        {
            k.Ball(new(side * .22f, 1.38f, -1.55f), new(.17f, .19f, .05f), "ffffff");
            k.Ball(new(side * .22f, 1.36f, -1.59f), new(.08f, .1f, .03f), "1d2a22");
        }
        foreach (var (x, y) in new[] { (-.22f, 1.12f), (-.12f, 1.06f), (0f, 1.03f), (.12f, 1.06f), (.22f, 1.12f) })
            k.Ball(new(x, y, -1.56f), new(.035f, .035f, .03f), "1d2a22");
        k.Post(new(0, 2.05f, -1.15f), .16f, .2f, .45f, Iron);
        k.Trim(k.Post(new(0, 2.3f, -1.15f), .2f, .3f, .14f, "fff0ce"));
        k.Trim(k.Ball(new(0, 1.9f, -.55f), new(.27f, .22f, .27f), "fff0ce"));
        k.Trim(k.Post(new(0, 1.92f, -1.42f), .1f, .1f, .08f, "fff0ce"));
        k.Glow(k.Ball(new(0, 2.0f, -1.42f), new(.14f, .14f, .14f), "fff3c4"), "ffe6a1");
        k.Root.AddChild(new TrainSmoke { Name = "ChimneySmoke", Position = new(0, 2.38f, -1.15f) });

        // The cab: walls, windows on each side and behind, and a round roof that carries the stripes.
        k.Body(k.Block(new(0, 1.35f, .8f), new(1.62f, 1.3f, 1.1f), "f54257"));
        foreach (float side in new[] { -1f, 1f })
        {
            k.Block(new(side * .82f, 1.55f, .8f), new(.04f, .5f, .62f), Dark);
            k.Block(new(side * .835f, 1.55f, .8f), new(.03f, .42f, .54f), "82c8d7");
        }
        k.Block(new(0, 1.6f, 1.36f), new(.6f, .45f, .03f), Dark);
        k.Block(new(0, 1.6f, 1.375f), new(.52f, .37f, .03f), "72becf");
        k.Roof(
            VehicleModel.Loft(
                k.Root,
                new[]
                {
                    new VehicleModel.Ring(2.02f, .98f, .72f, .8f, .22f),
                    new VehicleModel.Ring(2.12f, .92f, .66f, .8f, .22f),
                    new VehicleModel.Ring(2.2f, .8f, .52f, .8f, .2f),
                },
                "ff8992"
            )
        );
        foreach (float x in new[] { -.55f, -.2f, .2f, .55f })
            k.Stripe(new(x, 2.206f, .8f), new(.12f, .012f, 1f));

        // Two big drivers and a small leading wheel on each side, joined by a rod; buffers and a cowcatcher in front.
        foreach (float side in new[] { -1f, 1f })
        {
            k.Wheel(new(side * .88f, .42f, .75f), .42f, .1f, "263440", 5);
            k.Wheel(new(side * .88f, .42f, -.2f), .42f, .1f, "263440", 5);
            k.Wheel(new(side * .88f, .26f, -1.05f), .26f, .1f, "263440");
            k.Trim(k.Block(new(side * .985f, .42f, .275f), new(.04f, .07f, .95f), "fff0ce"));
        }
        k.Trim(k.Block(new(0, .72f, -1.52f), new(1.5f, .2f, .14f), "fff0ce"));
        foreach (float x in new[] { -.5f, .5f })
            k.Tube(new(x, .72f, -1.62f), .08f, .08f, .12f, Iron);
        var catcher = k.Trim(k.Block(new(0, .42f, -1.58f), new(1.1f, .05f, .6f), "fff0ce"));
        catcher.Rotation = new(-.55f, 0, 0);
        k.Tube(new(0, .65f, 1.56f), .06f, .06f, .2f, Iron);
    }

    private static void Rocket(Kit k)
    {
        const string Dark = "344555";
        // A fat rocket between four car wheels: the nose is a cone with a round tip, the tail tapers to a nozzle with a
        // flame, and two swept fins and a small top fin make it a rocket.
        k.Body(k.Tube(new(0, 1.22f, -.05f), .72f, .72f, 1.7f, "f54257"));
        k.Roof(k.Tube(new(0, 1.22f, -1.3f), .1f, .72f, .8f, "ff8992"));
        k.Trim(k.Ball(new(0, 1.22f, -1.72f), new(.14f, .14f, .14f), "fff0ce"));
        k.Trim(k.Tube(new(0, 1.22f, -.9f), .74f, .74f, .1f, "fff0ce"));
        k.Body(k.Tube(new(0, 1.22f, .975f), .72f, .5f, .35f, "f54257"));
        k.Tube(new(0, 1.22f, 1.275f), .26f, .38f, .25f, Dark);
        // The flame grows from the nozzle, so it is drawn behind a node at the nozzle's mouth.
        var exhaust = k.Pivot("Flame", new(0, 1.22f, 1.4f));
        var flame = k.Glow(k.Tube(new(0, 0, .14f), .24f, .02f, .28f, "ff9d3b", exhaust), "ff9d3b", 1.4f);
        var core = k.Glow(k.Tube(new(0, 0, .1f), .13f, .01f, .2f, "ffe27a", exhaust), "ffe27a", 1.6f);
        foreach (float side in new[] { -1f, 1f })
        {
            var fin = k.Roof(k.Ball(new(side * 1f, 1.3f, .72f), new(.34f, .06f, .5f), "ff8992"));
            fin.Rotation = new(0, 0, side * .35f);
            foreach (float z in new[] { -.45f, .3f })
            {
                k.Trim(k.Disc(new(side * .715f, 1.32f, z), .25f, .06f, "fff0ce"));
                k.Glow(k.Disc(new(side * .73f, 1.32f, z), .19f, .08f, "9ed5df"), "bfeaf5");
            }
        }
        k.Roof(k.Ball(new(0, 1.95f, .85f), new(.05f, .35f, .4f), "ff8992"));
        // The axles reach out to the wheels of the car.
        foreach (float z in new[] { -.93f, .96f })
            k.Disc(new(0, .51f, z), .07f, 1.9f, Dark);
        foreach (float x in new[] { -.14f, .14f })
        {
            k.Stripe(new(x, 1.915f, -.4f), new(.13f, .03f, .6f));
            k.Stripe(new(x, 1.915f, .4f), new(.13f, .03f, .5f));
        }

        // The flame flickers while the rocket stands and roars out long and bright with the pedal down.
        float roar = 0;
        k.Moves(drive =>
        {
            roar = Mathf.Lerp(roar, drive.Effort, Gadget.Ease(5, drive.Dt));
            float t = drive.Time;
            float flicker = .1f * Mathf.Sin(t * 31) + .07f * Mathf.Sin(t * 47 + 1) + .05f * Mathf.Sin(t * 73 + 2);
            float width = 1 + roar * .2f + flicker * .4f;
            exhaust.Scale = new(width, width, 1 + roar * .9f + flicker * (1 + roar));
            flame.EmissionEnergyMultiplier = 1.4f + roar * 1.6f + flicker * 2;
            core.EmissionEnergyMultiplier = 1.6f + roar * 2.4f + flicker * 2;
        });
    }

    private const int LightCount = 12;

    private static void Saucer(Kit k)
    {
        const string Dark = "344555";
        // A flat saucer with a cap and a glass dome, a green passenger inside, a ring of lights and four small wheels.
        k.Body(k.Ball(new(0, 1.1f, 0), new(1.3f, .3f, 1.3f), "f54257"));
        k.Roof(k.Post(new(0, 1.3f, 0), .95f, .5f, .3f, "ff8992", 40));
        k.Glow(k.Ball(new(0, .84f, 0), new(.55f, .1f, .55f), "9bf0ff"), "9bf0ff", .8f);
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.Tau / 10;
            k.Trim(
                k.Ball(new(Mathf.Cos(angle) * 1.22f, 1.12f, Mathf.Sin(angle) * 1.22f), new(.1f, .08f, .1f), "fff0ce")
            );
        }
        // A ring of coloured lights round the belly runs round the saucer, faster as it drives, and a bright wave
        // chases through it while every bulb slowly changes its colour.
        var ring = k.Pivot("Lights", Vector3.Zero);
        var bulbs = new StandardMaterial3D[LightCount];
        for (int i = 0; i < LightCount; i++)
        {
            float angle = i * Mathf.Tau / LightCount;
            var bulb = k.Ball(
                new(Mathf.Cos(angle) * 1.23f, 1.0f, Mathf.Sin(angle) * 1.23f),
                new(.1f, .085f, .1f),
                "ffffff",
                ring
            );
            bulbs[i] = k.Glow(bulb, "ffffff", 1);
            bulb.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        float turn = 0;
        k.Moves(drive =>
        {
            turn += (.7f + Mathf.Abs(drive.Speed) * .18f) * drive.Dt;
            ring.Rotation = new(0, turn, 0);
            for (int i = 0; i < LightCount; i++)
            {
                var color = Color.FromHsv(Mathf.PosMod(i / (float)LightCount + drive.Time * .12f, 1), .7f, 1);
                float wave = Mathf.Pow(.5f + .5f * Mathf.Sin(drive.Time * 7 - i * Mathf.Tau * 3 / LightCount), 3);
                bulbs[i].AlbedoColor = color.SrgbToLinear();
                bulbs[i].Emission = color.SrgbToLinear();
                bulbs[i].EmissionEnergyMultiplier = .5f + wave * 2.5f;
            }
        });
        k.Ball(new(0, 1.58f, 0), new(.19f, .22f, .19f), "7fd35a");
        foreach (float side in new[] { -1f, 1f })
        {
            k.Ball(new(side * .08f, 1.62f, -.16f), new(.06f, .085f, .035f), "1d2a22");
            k.Beam(new(side * .06f, 1.75f, 0), new(side * .14f, 1.9f, 0), .02f, "7fd35a");
        }
        k.Ball(new(0, 1.52f, -.18f), new(.04f, .015f, .02f), "1d2a22");
        k.Clear(k.Ball(new(0, 1.45f, 0), new(.5f, .5f, .5f), "c6f5ff"), "c6f5ff", .35f);
        foreach (float side in new[] { -1f, 1f })
        foreach (float z in new[] { -.75f, .75f })
        {
            k.Post(new(side * .55f, .6f, z), .05f, .05f, .7f, Dark);
            k.Wheel(new(side * .62f, .26f, z), .26f, .12f, "263440");
        }
        foreach (float x in new[] { -.16f, .16f })
        {
            k.Stripe(new(x, 1.31f, -.72f), new(.1f, .012f, .42f), -.59f);
            k.Stripe(new(x, 1.31f, .72f), new(.1f, .012f, .42f), .59f);
        }
    }

    /// <summary>The parts and helpers that the toys share, bound to the coachwork they are drawn into.</summary>
    private sealed class Kit
    {
        private readonly VehicleAppearance appearance;
        private readonly string model;
        private readonly List<Node3D> wheels;

        public Kit(Node3D root, VehicleAppearance appearance, string model, List<Node3D> wheels)
        {
            Root = root;
            this.appearance = appearance;
            this.model = model;
            this.wheels = wheels;
        }

        public Node3D Root { get; }

        public MeshInstance3D Body(MeshInstance3D mesh)
        {
            appearance.RegisterBody(mesh);
            return mesh;
        }

        public MeshInstance3D Roof(MeshInstance3D mesh)
        {
            appearance.RegisterRoof(mesh);
            return mesh;
        }

        public MeshInstance3D Trim(MeshInstance3D mesh)
        {
            appearance.RegisterTrim(mesh);
            return mesh;
        }

        public MeshInstance3D Ball(Vector3 position, Vector3 scale, string color, Node3D? parent = null) =>
            Art.Mesh(parent ?? Root, VehicleModel.SmoothSphere, position, scale, color);

        public MeshInstance3D Block(Vector3 position, Vector3 size, string color, Node3D? parent = null) =>
            Art.Block(parent ?? Root, position, size, color);

        /// <summary>A round cylinder standing upright; <paramref name="bottom"/> and <paramref name="top"/> are its radii.</summary>
        public MeshInstance3D Post(
            Vector3 position,
            float bottom,
            float top,
            float height,
            string color,
            int segments = 32,
            Node3D? parent = null
        )
        {
            var mesh = Art.Cylinder(parent ?? Root, position, bottom, top, height, color);
            ((CylinderMesh)mesh.Mesh).RadialSegments = segments;
            return mesh;
        }

        /// <summary>A cylinder lying along the car, <paramref name="front"/> being the radius at the nose end.</summary>
        public MeshInstance3D Tube(
            Vector3 position,
            float front,
            float rear,
            float length,
            string color,
            Node3D? parent = null
        )
        {
            var mesh = Post(position, front, rear, length, color, parent: parent);
            mesh.Rotation = new(Mathf.Pi / 2, 0, 0);
            return mesh;
        }

        /// <summary>A cylinder lying across the car: an axle or a disc seen from the side.</summary>
        public MeshInstance3D Disc(Vector3 position, float radius, float width, string color, Node3D? parent = null)
        {
            var mesh = Art.Cylinder(parent ?? Root, position, radius, radius, width, color);
            ((CylinderMesh)mesh.Mesh).RadialSegments = 32;
            mesh.Rotation = new(0, 0, Mathf.Pi / 2);
            return mesh;
        }

        /// <summary>A thin bar between two points, set by its basis so it needs no scene tree.</summary>
        public void Beam(Vector3 from, Vector3 to, float width, string color)
        {
            var direction = to - from;
            var up = Mathf.Abs(direction.Normalized().Y) > .99f ? Vector3.Right : Vector3.Up;
            var bar = Art.Block(Root, (from + to) / 2, Vector3.One, color);
            bar.Basis = Basis.LookingAt(direction, up) * Basis.FromScale(new(width, width, direction.Length()));
        }

        /// <summary>See-through glass over something that must stay visible, like the pilot under the canopy.</summary>
        public StandardMaterial3D Clear(MeshInstance3D mesh, string color, float alpha)
        {
            var material = new StandardMaterial3D
            {
                AlbedoColor = new Color(color).SrgbToLinear() with { A = alpha },
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = .1f,
                Metallic = .2f,
            };
            mesh.MaterialOverride = material;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            return material;
        }

        /// <summary>A part that shines a little by itself: a lamp, a window, a flame.</summary>
        public StandardMaterial3D Glow(MeshInstance3D mesh, string color, float energy = .35f)
        {
            var material = (StandardMaterial3D)Art.Mat(color).Duplicate();
            material.EmissionEnabled = true;
            material.Emission = new Color(color).SrgbToLinear();
            material.EmissionEnergyMultiplier = energy;
            mesh.MaterialOverride = material;
            return material;
        }

        /// <summary>A node that a part of the model turns or moves on, like the tank's turret or the plane's rudder.</summary>
        public Node3D Pivot(string name, Vector3 position)
        {
            var node = new Node3D { Name = name, Position = position };
            Root.AddChild(node);
            return node;
        }

        /// <summary>Lets the model's moving parts follow the driving (<see cref="Gadget"/>).</summary>
        public void Moves(System.Action<Drive> move) => Root.AddChild(new Gadget { Name = "Gadget", Move = move });

        /// <summary>A wheel that turns with the car and grows with the garage's bigger wheels.</summary>
        public Node3D Wheel(Vector3 position, float radius, float width, string tyre, int spokes = 0)
        {
            float side = position.X < 0 ? -1 : 1;
            var wheel = new Node3D { Position = position };
            Root.AddChild(wheel);
            wheels.Add(wheel);
            appearance.RegisterWheel(wheel, model);
            Disc(Vector3.Zero, radius, width, tyre, wheel);
            Trim(Disc(new(side * (width / 2 + .004f), 0, 0), radius * .62f, .03f, "fff0ce", wheel));
            Ball(new(side * (width / 2 + .03f), radius * .34f, 0), new(.035f, .035f, .035f), "344555", wheel);
            for (int i = 0; i < spokes; i++)
            {
                float angle = i * Mathf.Tau / spokes;
                var spoke = Block(
                    new(side * (width / 2 + .02f), Mathf.Cos(angle) * radius * .5f, Mathf.Sin(angle) * radius * .5f),
                    new(.02f, radius * .9f, .05f),
                    "fff0ce",
                    wheel
                );
                spoke.Rotation = new(angle, 0, 0);
                Trim(spoke);
            }
            return wheel;
        }

        /// <summary>A racing stripe on a flat part, turned about X by <paramref name="slope"/> on a slanted one.</summary>
        public void Stripe(Vector3 position, Vector3 size, float slope = 0)
        {
            var stripe = Art.Block(Root, position, size, "ffffff");
            stripe.Rotation = new(slope, 0, 0);
            appearance.RegisterStripe(stripe, model);
        }
    }
}
