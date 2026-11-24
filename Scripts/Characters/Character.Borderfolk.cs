using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Dresses the keeper of a border (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 1): the lighthouse keeper (a bear)
    /// by the sea, the shepherd (a rabbit) in the mountains, the ferry-woman (a fox) by the river and the forester (a
    /// bear) in the forest.
    /// </summary>
    internal void DressAsKeeper(Border border)
    {
        switch (border)
        {
            case Border.Sea:
                DressAsLighthouseKeeper();
                break;
            case Border.Mountains:
                DressAsShepherd();
                break;
            case Border.River:
                DressAsFerrywoman();
                break;
            default:
                DressAsForester();
                break;
        }
    }

    /// <summary>A layer of cloth over the body, a little larger than it, for a sweater or a jacket.</summary>
    private void Coat(string color) => Art.Ball(model, new(0, .8f, 0), new(.68f, .87f, .58f), color);

    /// <summary>A collar between the coat and the head, seen at the front and the back.</summary>
    private void Collar(string color) =>
        Art.Cylinder(model, new(0, 1.36f, 0), .6f, .56f, .14f, color).Scale = new(1, 1, .92f);

    /// <summary>
    /// A navy captain's cap with a black peak and a golden anchor badge, a striped blue and white sweater and a pipe in
    /// the corner of the mouth.
    /// </summary>
    private void DressAsLighthouseKeeper()
    {
        // The sweater: white with navy rings, which follow the round body.
        Coat("f4f4f0");
        foreach (float y in new[] { .45f, .72f, .99f, 1.26f })
        {
            float t = (y - .8f) / .87f;
            float r = .69f * Mathf.Sqrt(Mathf.Max(0, 1 - t * t));
            var stripe = Art.Cylinder(model, new(0, y, 0), r, r, .11f, "2a4a8a");
            stripe.Scale = new(1, 1, .86f);
        }
        // The cap.
        Art.Cylinder(model, new(0, 2.42f, 0), .55f, .55f, .12f, "1f2f4f");
        Art.Cylinder(model, new(0, 2.6f, 0), .56f, .66f, .26f, "1f2f4f");
        Art.Cylinder(model, new(0, 2.74f, 0), .66f, .66f, .04f, "f4f4f0");
        Art.Block(model, new(0, 2.38f, .58f), new(.74f, .06f, .36f), "15171c").RotationDegrees = new(-8, 0, 0);
        var badge = new Node3D { Name = "AnchorBadge", Position = new(0, 2.56f, .62f) };
        model.AddChild(badge);
        Art.Block(badge, Vector3.Zero, new(.04f, .18f, .03f), "f2c14e");
        Art.Block(badge, new(0, .06f, 0), new(.13f, .03f, .03f), "f2c14e");
        Art.Ball(badge, new(0, .11f, 0), Vector3.One * .03f, "f2c14e");
        Art.Ball(badge, new(0, -.08f, 0), new(.09f, .035f, .03f), "f2c14e");
        // The pipe: a stem from the corner of the mouth and a bowl.
        var stem = Art.Cylinder(model, new(.24f, 1.47f, .78f), .025f, .025f, .34f, "5a3a22");
        stem.RotationDegrees = new(80, 0, -40);
        Art.Cylinder(model, new(.38f, 1.5f, .86f), .06f, .05f, .16f, "6b4a2e");
        Art.Ball(model, new(.38f, 1.58f, .86f), new(.05f, .02f, .05f), "2b211c");
    }

    /// <summary>
    /// A wide brown felt hat with a red band and an edelweiss, worn low between the long ears, a woolly sheepskin
    /// waistcoat and a shepherd's crook in the paw.
    /// </summary>
    private void DressAsShepherd()
    {
        // The hat: a wide brim at the brow, which the ears go through, and a low crown between them.
        Art.Cylinder(model, new(0, 2.36f, .04f), .92f, .92f, .06f, "6b5a3a");
        Art.Cylinder(model, new(0, 2.5f, .04f), .32f, .27f, .26f, "6b5a3a");
        Art.Cylinder(model, new(0, 2.44f, .04f), .33f, .33f, .07f, "c0504d");
        var flower = new Node3D { Name = "Edelweiss", Position = new(0, 2.47f, .39f) };
        model.AddChild(flower);
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5;
            Art.Ball(flower, new(Mathf.Sin(a) * .06f, Mathf.Cos(a) * .06f, 0), new(.045f, .045f, .02f), "fbf8f0");
        }
        Art.Ball(flower, new(0, 0, .02f), Vector3.One * .03f, "f2c14e");
        // The sheepskin waistcoat: two woolly fronts open over the chest, with a fluffy edge.
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(model, new(side * .27f, .98f, .52f), new(.3f, .95f, .12f), "d9c9a8");
            for (int i = 0; i < 4; i++)
                Art.Ball(model, new(side * .13f, .62f + i * .25f, .54f), Vector3.One * .07f, "f7f0e0");
        }
        Art.Ball(model, new(0, 1.32f, .3f), new(.5f, .12f, .32f), "f7f0e0");
        // The crook: a staff in the left paw, its top bent over into a hook.
        Art.Cylinder(model, new(-.82f, 1.3f, .2f), .04f, .04f, 2.6f, "8a6440");
        var hook = new Node3D { Name = "CrookHook", Position = new(-.82f, 2.6f, .2f) };
        model.AddChild(hook);
        for (int i = 0; i < 4; i++)
        {
            float a = (i + .5f) * Mathf.Pi / 4;
            var piece = Art.Cylinder(
                hook,
                new(-.16f + Mathf.Cos(a) * .16f, Mathf.Sin(a) * .16f, 0),
                .04f,
                .04f,
                .14f,
                "8a6440"
            );
            piece.Rotation = new(0, 0, a);
        }
    }

    /// <summary>
    /// A red headscarf with white dots tied under the chin, the fox's pointed ears standing up out of it, a yellow rain
    /// jacket with toggles and an oar held up in the paw.
    /// </summary>
    private void DressAsFerrywoman()
    {
        Art.Ball(model, new(0, 2.2f, -.06f), new(.86f, .5f, .64f), "d9432f");
        foreach (
            var (x, y, z) in new[] { (-.3f, 2.46f, .3f), (.25f, 2.55f, .22f), (0f, 2.66f, .05f), (.5f, 2.3f, .2f) }
        )
            Art.Ball(model, new(x, y, z), new(.06f, .06f, .03f), "fbf8f0");
        Art.Ball(model, new(0, 1.24f, .5f), new(.13f, .1f, .1f), "d9432f");
        foreach (int side in new[] { -1, 1 })
        {
            var tail = Art.Ball(model, new(side * .1f, 1.1f, .56f), new(.06f, .14f, .04f), "d9432f");
            tail.RotationDegrees = new(0, 0, side * 25);
        }
        // The rain jacket with a collar and dark toggles.
        Coat("f2c230");
        Collar("e0ac1c");
        Art.Block(model, new(0, .9f, .58f), new(.05f, .95f, .03f), "c99a1a");
        foreach (float y in new[] { .65f, .9f, 1.15f })
            Art.Block(model, new(.09f, y, .6f), new(.12f, .04f, .04f), "3b3f45");
        // The oar, held up beside her with the blade at the top.
        var oar = new Node3D
        {
            Name = "Oar",
            Position = new(-.84f, 1.4f, .3f),
            RotationDegrees = new(0, 0, -6),
        };
        model.AddChild(oar);
        Art.Cylinder(oar, Vector3.Zero, .04f, .04f, 2.8f, "c39770");
        Art.Block(oar, new(0, 1.55f, 0), new(.26f, .8f, .05f), "c39770");
        Art.Block(oar, new(0, 1.92f, 0), new(.26f, .06f, .055f), "3e7a5a");
    }

    /// <summary>
    /// A green forester's hat with a band and a pheasant feather, a green jacket with lapels and buttons and a pair of
    /// binoculars hung on a strap round the neck.
    /// </summary>
    private void DressAsForester()
    {
        Art.Cylinder(model, new(0, 2.44f, 0), .72f, .72f, .07f, "3f5a30");
        Art.Cylinder(model, new(0, 2.68f, 0), .48f, .36f, .44f, "4f6b3a");
        Art.Cylinder(model, new(0, 2.52f, 0), .495f, .48f, .09f, "2f4024");
        Art.Ball(model, new(-.42f, 2.9f, -.12f), new(.04f, .42f, .08f), "8a5a35").RotationDegrees = new(0, 0, 32);
        Art.Ball(model, new(-.48f, 2.86f, -.15f), new(.03f, .3f, .06f), "d9b27a").RotationDegrees = new(0, 0, 40);
        // The jacket.
        Coat("4f6b3a");
        foreach (int side in new[] { -1, 1 })
        {
            var lapel = Art.Block(model, new(side * .15f, 1.18f, .57f), new(.16f, .4f, .05f), "6b8a4a");
            lapel.RotationDegrees = new(0, 0, side * 22);
        }
        foreach (float y in new[] { .55f, .8f })
            Art.Ball(model, new(0, y, .63f), Vector3.One * .045f, "8a5a35");
        // The binoculars on a strap.
        Art.Block(model, new(0, 1.28f, .5f), new(.05f, .5f, .03f), "6b4a2e").RotationDegrees = new(-30, 0, 0);
        foreach (float x in new[] { -.11f, .11f })
        {
            var tube = Art.Cylinder(model, new(x, 1.06f, .72f), .085f, .075f, .26f, "2b2b33");
            tube.RotationDegrees = new(90, 0, 0);
            var lens = Art.Cylinder(model, new(x, 1.06f, .86f), .06f, .06f, .02f, "9ed5df");
            lens.RotationDegrees = new(90, 0, 0);
        }
        Art.Block(model, new(0, 1.06f, .72f), new(.12f, .07f, .12f), "2b2b33");
    }

    /// <summary>
    /// The postman: a blue postal cap with a black peak and the yellow post horn on it, a blue jacket with yellow trim and
    /// a brown leather satchel on a strap, with a letter sticking out of it.
    /// </summary>
    internal void DressAsPostman()
    {
        // The cap sits between the long ears.
        Art.Cylinder(model, new(0, 2.44f, .04f), .44f, .48f, .3f, "2f5da8");
        Art.Cylinder(model, new(0, 2.6f, .04f), .49f, .49f, .04f, "24498a");
        Art.Block(model, new(0, 2.32f, .48f), new(.6f, .05f, .3f), "15171c").RotationDegrees = new(-10, 0, 0);
        var horn = new Node3D { Name = "PostHorn", Position = new(0, 2.45f, .5f) };
        model.AddChild(horn);
        var loop = Art.Mesh(
            horn,
            new TorusMesh
            {
                InnerRadius = .045f,
                OuterRadius = .07f,
                Rings = 12,
                RingSegments = 4,
            },
            Vector3.Zero,
            Vector3.One,
            "f2c14e"
        );
        loop.RotationDegrees = new(90, 0, 0);
        var bell = Art.Cylinder(horn, new(.1f, -.03f, 0), .02f, .055f, .1f, "f2c14e");
        bell.RotationDegrees = new(0, 0, -70);
        // The jacket.
        Coat("2f5da8");
        Collar("f2c14e");
        Art.Block(model, new(0, .9f, .58f), new(.04f, .95f, .03f), "f2c14e");
        foreach (float y in new[] { .65f, .9f, 1.15f })
            Art.Ball(model, new(.09f, y, .6f), Vector3.One * .045f, "f2c14e");
        // The satchel on a strap across the chest, at the left hip.
        Art.Block(model, new(.05f, 1.0f, .6f), new(.08f, 1.15f, .03f), "6b4a2e").RotationDegrees = new(0, 0, -35);
        var satchel = new Node3D { Name = "Satchel", Position = new(-.62f, .62f, .2f) };
        model.AddChild(satchel);
        Art.Block(satchel, Vector3.Zero, new(.2f, .48f, .6f), "8a5a35");
        Art.Block(satchel, new(-.02f, .12f, 0), new(.22f, .24f, .62f), "6b4a2e");
        Art.Ball(satchel, new(-.12f, .02f, 0), Vector3.One * .04f, "f2c14e");
        var letter = Art.Block(satchel, new(.02f, .3f, .1f), new(.04f, .3f, .4f), "fbf6ea");
        letter.RotationDegrees = new(12, 0, 0);
        Art.Block(satchel, new(-.005f, .38f, .22f), new(.03f, .08f, .08f), "d9432f");
    }
}
