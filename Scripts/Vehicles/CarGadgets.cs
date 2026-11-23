using Godot;
using MathRacer.Graphics;

namespace MathRacer.Vehicles;

/// <summary>
/// What makes each of the four cars special, a part that moves as it drives (<see cref="Gadget"/>): the retro car is a
/// tin toy with a wind-up key in its side, the buggy's engine pumps on its open back, a dog rides in the pickup's bed
/// and a child holds a pinwheel out of the wagon's back window. They sit where none of the garage's accessories does.
/// </summary>
internal static class CarGadgets
{
    private const string Dark = "344555",
        Skin = "ffd7b0";

    private static MeshInstance3D Round(Node3D parent, Vector3 position, Vector3 scale, string color) =>
        Art.Mesh(parent, VehicleModel.SmoothSphere, position, scale, color);

    private static Node3D Pivot(Node3D parent, string name, Vector3 position)
    {
        var node = new Node3D { Name = name, Position = position };
        parent.AddChild(node);
        return node;
    }

    private static void Moves(Node3D root, System.Action<Drive> move) =>
        root.AddChild(new Gadget { Name = "Gadget", Move = move });

    internal static void Add(Node3D root, string id)
    {
        switch (id)
        {
            case "classic":
                Classic(root);
                break;
            case "buggy":
                Buggy(root);
                break;
            case "pickup":
                Pickup(root);
                break;
            case "wagon":
                Wagon(root);
                break;
        }
    }

    private static void Classic(Node3D root)
    {
        // A brass key in the right door, like the key of a tin toy car; it ticks round while the car stands and
        // unwinds as the car drives, backwards when it reverses.
        const string Brass = "e8b54a";
        var shaft = Art.Cylinder(root, new(1.02f, .93f, .2f), .035f, .035f, .16f, Brass);
        shaft.Rotation = new(0, 0, Mathf.Pi / 2);
        var key = Pivot(root, "WindUpKey", new(1.11f, .93f, .2f));
        Round(key, Vector3.Zero, new(.04f, .06f, .06f), Brass);
        foreach (float end in new[] { -1f, 1f })
        {
            Round(key, new(0, end * .15f, 0), new(.035f, .12f, .1f), Brass);
            Round(key, new(.025f, end * .17f, 0), new(.02f, .05f, .04f), "c48a2e");
        }
        float angle = 0;
        Moves(root, drive =>
        {
            // Standing, the key turns in quarter-turn ticks; driving, it runs round with the wheels.
            float tick = Mathf.Floor(drive.Time * 1.5f) + Mathf.SmoothStep(.75f, 1, Mathf.PosMod(drive.Time * 1.5f, 1));
            angle += drive.Speed * .9f * drive.Dt;
            key.Rotation = new(angle + tick * Mathf.Pi / 2, 0, 0);
        });
    }

    private static void Buggy(Node3D root)
    {
        // The engine lies open behind the seats: a block, two chrome trumpets that pump up and down and an air filter
        // on top. It shivers while it idles, and the faster the engine runs the quicker the trumpets pump.
        var engine = Pivot(root, "Engine", new(0, 1.24f, 1.06f));
        Art.Block(engine, new(0, .12f, 0), new(.72f, .24f, .38f), "5b6873");
        Art.Block(engine, new(0, .25f, 0), new(.6f, .04f, .3f), Dark);
        foreach (float side in new[] { -1f, 1f })
            Art.Block(engine, new(side * .38f, .1f, 0), new(.05f, .16f, .3f), "6a7a88");
        var trumpets = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            var trumpet = Pivot(engine, "Trumpet", new((i * 2 - 1) * .17f, .27f, 0));
            Art.Cylinder(trumpet, new(0, .1f, 0), .07f, .07f, .2f, "c6dce3");
            Art.Cylinder(trumpet, new(0, .22f, 0), .07f, .11f, .06f, "c6dce3");
            Art.Cylinder(trumpet, new(0, .25f, 0), .085f, .085f, .015f, Dark);
            trumpets[i] = trumpet;
        }
        Round(engine, new(0, .36f, -.02f), new(.1f, .05f, .1f), "e2563b");
        float phase = 0;
        Moves(root, drive =>
        {
            phase += (12 + drive.Effort * 26 + Mathf.Abs(drive.Speed) * 1.2f) * drive.Dt;
            for (int i = 0; i < 2; i++)
                trumpets[i].Position = trumpets[i].Position with { Y = .27f + .05f * Mathf.Sin(phase + i * Mathf.Pi) };
            float shiver = .006f + drive.Effort * .006f;
            engine.Position = new(Mathf.Sin(phase * 2.3f) * shiver, 1.24f + Mathf.Sin(phase * 2) * shiver, 1.06f);
        });
    }

    private static void Pickup(Node3D root)
    {
        // A happy dog sits in the bed and looks out to the left: it wags its tail, faster when the pickup drives,
        // the wind blows its ears back and makes them flap, and standing it looks round.
        const string Fur = "c8895a",
            Light = "f3e3c8",
            Ears = "8a5a3c";
        var dog = Pivot(root, "Dog", new(-.28f, 1.235f, 1.0f));
        dog.Rotation = new(0, .5f, 0);
        dog.Scale = Vector3.One * 1.2f;
        Round(dog, new(0, .22f, .08f), new(.17f, .22f, .21f), Fur);
        Round(dog, new(0, .3f, -.07f), new(.13f, .16f, .12f), Light);
        foreach (float side in new[] { -1f, 1f })
        {
            Round(dog, new(side * .11f, .1f, .14f), new(.09f, .1f, .14f), Fur);
            Round(dog, new(side * .07f, .12f, -.1f), new(.045f, .12f, .045f), Fur);
            Round(dog, new(side * .07f, .02f, -.14f), new(.05f, .025f, .06f), Light);
        }
        Round(dog, new(0, .42f, -.07f), new(.12f, .03f, .11f), "e2563b");
        var head = Pivot(dog, "Head", new(0, .54f, -.1f));
        Round(head, Vector3.Zero, new(.14f, .13f, .14f), Fur);
        Round(head, new(0, -.03f, -.13f), new(.075f, .065f, .085f), Light);
        Round(head, new(0, .0f, -.215f), new(.035f, .028f, .022f), "1d2a22");
        Round(head, new(0, -.085f, -.17f), new(.032f, .014f, .045f), "e98a7d");
        foreach (float side in new[] { -1f, 1f })
            Round(head, new(side * .06f, .045f, -.115f), new(.022f, .026f, .015f), "1d2a22");
        var ears = new Node3D[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i * 2 - 1;
            ears[i] = Pivot(head, "Ear", new(side * .1f, .08f, 0));
            Round(ears[i], new(side * .03f, -.08f, 0), new(.035f, .1f, .065f), Ears);
        }
        var tail = Pivot(dog, "Tail", new(0, .1f, .27f));
        var tip = Round(tail, new(0, .1f, .05f), new(.03f, .12f, .03f), Fur);
        tip.Rotation = new(.45f, 0, 0);
        float wind = 0;
        Moves(root, drive =>
        {
            float t = drive.Time;
            wind = Mathf.Lerp(wind, Mathf.Clamp(Mathf.Abs(drive.Speed) / 12, 0, 1), Gadget.Ease(4, drive.Dt));
            tail.Rotation = new(0, 0, Mathf.Sin(t * (10 + wind * 8)) * (.5f + wind * .2f));
            for (int i = 0; i < 2; i++)
            {
                float flap = Mathf.Sin(t * 19 + i * 1.7f) * .25f * wind;
                ears[i].Rotation = new(-wind * 1.1f + flap, 0, (i * 2 - 1) * (.15f + wind * .35f));
            }
            float look = (1 - wind) * .45f * Mathf.Sin(t * .6f);
            head.Rotation = new(Mathf.Sin(t * 2.2f) * .04f, look, 0);
            head.Position = new(0, .54f + Mathf.Abs(Mathf.Sin(t * 4)) * .012f * (.3f + wind), -.1f);
        });
    }

    private static void Wagon(Node3D root)
    {
        // A child's arm out of the back window on the left holds a pinwheel up in the wind: it waves while the wagon
        // stands and the pinwheel spins with the speed.
        var arm = Pivot(root, "Arm", new(-.74f, 1.5f, .84f));
        Art.Beam(arm, Vector3.Zero, new(-.28f, .12f, 0), .07f, Skin);
        Round(arm, new(-.06f, .03f, 0), new(.075f, .065f, .075f), "4f8fb8");
        Round(arm, new(-.29f, .13f, 0), new(.055f, .06f, .055f), Skin);
        Art.Beam(arm, new(-.29f, .1f, 0), new(-.31f, .5f, 0), .025f, "fff5df");
        var pinwheel = Pivot(arm, "Pinwheel", new(-.31f, .5f, -.03f));
        string[] colors = { "e2563b", "f2c14e", "4f8fb8", "5aa86f" };
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.Tau / 4;
            var blade = Round(
                pinwheel,
                new(Mathf.Cos(angle) * .1f, Mathf.Sin(angle) * .1f, 0),
                new(.11f, .05f, .012f),
                colors[i]
            );
            blade.Rotation = new(0, 0, angle + .5f);
        }
        Round(pinwheel, new(0, 0, -.015f), new(.03f, .03f, .02f), "fff5df");
        float spin = 0,
            turned = 0;
        Moves(root, drive =>
        {
            float wind = Mathf.Clamp(Mathf.Abs(drive.Speed) / 10, 0, 1);
            spin = Mathf.Lerp(spin, 1.5f + Mathf.Abs(drive.Speed) * 2.4f, Gadget.Ease(2, drive.Dt));
            turned += Mathf.Min(spin, 40) * drive.Dt;
            pinwheel.Rotation = new(0, 0, turned);
            arm.Rotation = new(0, wind * .25f, (1 - wind) * .15f * Mathf.Sin(drive.Time * 5));
        });
    }
}
