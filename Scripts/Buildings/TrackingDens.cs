using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The dens at the ends of the forest keeper's trails (<c>docs/TRACKING_PLAN.md</c>): a thick bush for the hare, a burrow
/// in a bank for the fox, ferns round a young birch for the deer, a burrow with a heap of sand for the badger, a muddy
/// wallow for the boar's piglet and a pile of rocks by a fallen trunk for the lynx. Each den stands from the start; its
/// animal shows only while its trail is followed, peeping out, and once found it sits in front of the den for good. In
/// each den's frame +Z points from the den towards where the trail arrives.
/// </summary>
public partial class TrackingDens : Node3D
{
    private enum Look
    {
        Hidden,
        Peeping,
        Out,
    }

    private sealed class Den
    {
        public required Node3D Root;
        public required Node3D Animal;
        public required List<Node3D> Ears;
        public Look Look = Look.Hidden;
        public float Phase;
    }

    private readonly List<Den> dens = new();
    private float time;

    /// <summary>How far in front of the den's middle a found animal sits.</summary>
    private const float OutAhead = 1.6f;

    /// <summary>How many animals are found; with <see cref="SetState"/>.</summary>
    internal int Found { get; private set; }

    internal TrackingDens(IReadOnlyList<TrackingTrail> trails)
    {
        foreach (var trail in trails)
        {
            var root = new Node3D
            {
                Name = $"Den{trail.Animal}",
                Position = trail.Den,
                Rotation = new Vector3(0, trail.Yaw, 0),
            };
            AddChild(root);
            BuildDen(root, trail.Animal);
            var animal = new Node3D { Name = "Animal" };
            root.AddChild(animal);
            var ears = BuildAnimal(animal, trail.Animal);
            animal.Visible = false;
            // A trunk in the middle of the den, so that the car stops at it rather than drives through.
            Obstacles.Trunk(root, Vector3.Zero, trail.Animal == TrackingAnimal.Boar ? .3f : 1.1f, 1.2f);
            dens.Add(
                new Den
                {
                    Root = root,
                    Animal = animal,
                    Ears = ears,
                    Phase = (int)trail.Animal * 1.7f,
                }
            );
        }
    }

    /// <summary>
    /// The animals of the trails before <paramref name="found"/> sit in front of their dens; the animal of the trail
    /// followed now peeps out of its den; the others hide.
    /// </summary>
    internal void SetState(bool started, int found)
    {
        Found = found;
        for (int i = 0; i < dens.Count; i++)
        {
            var den = dens[i];
            den.Look =
                i < found ? Look.Out
                : started && i == found ? Look.Peeping
                : Look.Hidden;
            den.Animal.Visible = den.Look != Look.Hidden;
            Pose(den);
        }
    }

    /// <summary>Whether the animal of a den shows, for the checks.</summary>
    internal bool Shows(int den) => dens[den].Animal.Visible;

    /// <summary>Whether the animal of a den sits out in front of it, for the checks.</summary>
    internal bool SitsOut(int den) => dens[den].Look == Look.Out;

    /// <summary>A point above the animal of a den, for a celebration.</summary>
    internal Vector3 Focus(int den) => dens[den].Root.Transform * new Vector3(0, 1.4f, OutAhead);

    /// <summary>A peeping animal bobs up and looks round; a found one breathes and twitches its ears.</summary>
    internal void Tick(float delta)
    {
        time += delta;
        foreach (var den in dens)
            if (den.Look != Look.Hidden)
                Pose(den);
    }

    private void Pose(Den den)
    {
        float t = time + den.Phase;
        if (den.Look == Look.Peeping)
        {
            // Out of the mouth of the den and back, now and then.
            float peep = Mathf.SmoothStep(.2f, .8f, .5f + .5f * Mathf.Sin(t * .9f));
            den.Animal.Position = new Vector3(0, Mathf.Lerp(-.45f, 0, peep), Mathf.Lerp(.2f, .75f, peep));
            den.Animal.Rotation = new Vector3(0, Mathf.Sin(t * .6f) * .5f, 0);
        }
        else if (den.Look == Look.Out)
        {
            den.Animal.Position = new Vector3(.9f, Mathf.Max(0, Mathf.Sin(t * 1.3f)) * .02f, OutAhead);
            den.Animal.Rotation = new Vector3(0, .4f + Mathf.Sin(t * .35f) * .3f, 0);
        }
        float twitch = Mathf.Max(0, Mathf.Sin(t * 2.3f) - .85f) * 3;
        foreach (var ear in den.Ears)
            ear.Rotation = new Vector3(-twitch * .5f, 0, 0);
    }

    private static void BuildDen(Node3D root, TrackingAnimal animal)
    {
        switch (animal)
        {
            case TrackingAnimal.Hare:
                // A thick round bush, a few flowers and a hollow in front, where the hare sits.
                foreach (
                    var (x, y, z, r) in new[]
                    {
                        (0f, .7f, -.4f, 1.1f),
                        (-.9f, .5f, -.1f, .75f),
                        (.95f, .55f, -.2f, .8f),
                        (.2f, 1.25f, -.6f, .7f),
                    }
                )
                    Art.Ball(root, new(x, y, z), new(r, r * .82f, r), "4f8a3e");
                foreach (var (x, z) in new[] { (-1.2f, .6f), (1.1f, .7f), (.4f, .9f) })
                    Art.Ball(root, new(x, .12f, z), new(.09f, .09f, .09f), "f4f0e2");
                Art.Ball(root, new(0, .02f, .7f), new(.6f, .06f, .5f), "7a6a3c");
                break;
            case TrackingAnimal.Fox:
                // A grassy bank with the round dark mouth of the burrow and the earth dug out in front of it.
                Art.Ball(root, new(0, .1f, -.6f), new(2.1f, 1.2f, 1.6f), "6f8f43");
                Art.Ball(root, new(0, .45f, .62f), new(.5f, .42f, .3f), "2a1d14");
                Art.Ball(root, new(0, .03f, 1.2f), new(.9f, .14f, .6f), "8a6440");
                break;
            case TrackingAnimal.Deer:
                // A young birch with its white trunk and black marks, and ferns all round it.
                Art.Cylinder(root, new(-.9f, 1.6f, -.8f), .14f, .08f, 3.2f, "efece4");
                foreach (float y in new[] { .7f, 1.4f, 2.1f })
                    Art.Block(root, new(-.9f, y, -.68f), new(.16f, .05f, .05f), "2b2b2b");
                Art.Ball(root, new(-.9f, 3.4f, -.8f), new(.9f, 1f, .9f), "8cbf4a");
                for (int fern = 0; fern < 9; fern++)
                {
                    float a = fern * Mathf.Tau / 9;
                    var frond = Art.Ball(
                        root,
                        new(Mathf.Cos(a) * 1.1f, .35f, Mathf.Sin(a) * 1.1f - .2f),
                        new(.16f, .5f, .55f),
                        fern % 2 == 0 ? "3f7f3a" : "4f9444"
                    );
                    frond.Rotation = new Vector3(.7f, -a + Mathf.Pi / 2, 0);
                }
                break;
            case TrackingAnimal.Badger:
                // A burrow under the roots of an old stump, with a big heap of dug-out sand beside it.
                Art.Cylinder(root, new(0, .5f, -.7f), .75f, .6f, 1f, "7a5a3a");
                Art.Cylinder(root, new(0, 1.02f, -.7f), .6f, .6f, .05f, "c9a87a");
                Art.Ball(root, new(0, .3f, .1f), new(1.1f, .55f, .7f), "6a5a3c");
                Art.Ball(root, new(0, .32f, .62f), new(.42f, .34f, .24f), "241a12");
                Art.Ball(root, new(1.2f, .05f, .7f), new(.8f, .35f, .7f), "e0c58c");
                break;
            case TrackingAnimal.Boar:
                // A muddy wallow with reeds at its edge and a log to scratch against.
                Art.Ball(root, new(0, -.02f, 0), new(1.9f, .08f, 1.5f), "6b4a2c");
                Art.Ball(root, new(.3f, .01f, -.2f), new(1.2f, .05f, .9f), "523720");
                foreach (var (x, z) in new[] { (-1.8f, -.6f), (-1.6f, .2f), (1.8f, -.4f), (1.5f, .8f), (-.2f, -1.6f) })
                    for (int reed = 0; reed < 3; reed++)
                        Art.Cylinder(root, new(x + reed * .12f, .5f, z), .025f, .015f, 1f + reed * .2f, "7d9a3e");
                var log = Art.Cylinder(root, new(.2f, .25f, -1.4f), .25f, .22f, 2.4f, "7a5536");
                log.Rotation = new Vector3(0, 0, Mathf.Pi / 2);
                break;
            default:
                // A pile of grey rocks and a fallen trunk with moss on it.
                foreach (
                    var (x, y, z, r) in new[]
                    {
                        (0f, .5f, -.6f, 1f),
                        (-1f, .35f, -.2f, .7f),
                        (.9f, .3f, -.1f, .6f),
                        (.3f, 1.2f, -.7f, .55f),
                    }
                )
                    Art.Ball(root, new(x, y, z), new(r, r * .75f, r), "8e9296");
                var trunk = Art.Cylinder(root, new(-1.6f, .3f, .4f), .32f, .28f, 3f, "6a4a30");
                trunk.Rotation = new Vector3(0, .5f, Mathf.Pi / 2);
                Art.Ball(root, new(-1.6f, .55f, .4f), new(.6f, .12f, .3f), "5f8a3a").Rotation = new Vector3(0, .5f, 0);
                break;
        }
    }

    /// <summary>The animal, facing +Z, standing on y = 0; returns its ears, which twitch.</summary>
    private static List<Node3D> BuildAnimal(Node3D a, TrackingAnimal animal)
    {
        var ears = new List<Node3D>();
        void Eyes(float x, float y, float z)
        {
            foreach (int side in new[] { -1, 1 })
            {
                Art.Ball(a, new(side * x, y, z), Vector3.One * .035f, "1d1d1d");
                Art.Ball(a, new(side * x - .008f, y + .014f, z + .02f), Vector3.One * .011f, "ffffff");
            }
        }
        Node3D Ear(Vector3 at, Vector3 size, string color, float tilt, string? inner = null)
        {
            var root = new Node3D { Position = at, Rotation = new Vector3(0, 0, tilt) };
            a.AddChild(root);
            // The ear itself, which twitches inside its tilted root.
            var ear = new Node3D();
            root.AddChild(ear);
            Art.Ball(ear, new(0, size.Y, 0), size, color);
            if (inner != null)
                Art.Ball(ear, new(0, size.Y, .02f), size * new Vector3(.55f, .8f, .5f), inner);
            ears.Add(ear);
            return ear;
        }
        void Legs(float x, float zFront, float zBack, float height, float radius, string color)
        {
            foreach (float z in new[] { zFront, zBack })
            foreach (int side in new[] { -1, 1 })
                Art.Cylinder(a, new(side * x, height / 2, z), radius, radius * 1.1f, height, color);
        }
        switch (animal)
        {
            case TrackingAnimal.Hare:
                Art.Ball(a, new(0, .3f, 0), new(.24f, .26f, .32f), "a5815a");
                Art.Ball(a, new(0, .5f, .26f), new(.17f, .16f, .18f), "a5815a");
                Art.Ball(a, new(0, .46f, .41f), new(.06f, .05f, .05f), "f1e2cf");
                Art.Ball(a, new(0, .49f, .45f), new(.025f, .02f, .02f), "e08a8a");
                Art.Ball(a, new(0, .32f, -.3f), new(.09f, .09f, .09f), "f6f1e8");
                Ear(new(-.06f, .62f, .22f), new(.045f, .2f, .035f), "a5815a", .12f, "e4b2a4");
                Ear(new(.06f, .62f, .22f), new(.045f, .2f, .035f), "a5815a", -.12f, "e4b2a4");
                Eyes(.1f, .55f, .36f);
                break;
            case TrackingAnimal.Fox:
                Legs(.09f, .2f, -.2f, .28f, .035f, "3a2a20");
                Art.Ball(a, new(0, .38f, 0), new(.18f, .17f, .34f), "d9733f");
                Art.Ball(a, new(0, .36f, .12f), new(.13f, .13f, .2f), "f4e7d4");
                Art.Ball(a, new(0, .5f, .34f), new(.15f, .14f, .15f), "d9733f");
                Art.Ball(a, new(0, .46f, .48f), new(.07f, .06f, .11f), "f4e7d4");
                Art.Ball(a, new(0, .47f, .59f), new(.025f, .022f, .022f), "1d1d1d");
                var tail = Art.Ball(a, new(0, .38f, -.48f), new(.11f, .11f, .3f), "d9733f");
                tail.Rotation = new Vector3(-.5f, 0, 0);
                Art.Ball(a, new(0, .26f, -.72f), new(.07f, .07f, .09f), "f6f1e8");
                Ear(new(-.08f, .6f, .32f), new(.05f, .09f, .03f), "c45f2f", .25f, "2a1d14");
                Ear(new(.08f, .6f, .32f), new(.05f, .09f, .03f), "c45f2f", -.25f, "2a1d14");
                Eyes(.07f, .54f, .46f);
                break;
            case TrackingAnimal.Deer:
                Legs(.1f, .3f, -.3f, .55f, .04f, "8a5a34");
                Art.Ball(a, new(0, .7f, 0), new(.2f, .22f, .42f), "b07a4a");
                Art.Ball(a, new(0, .66f, -.42f), new(.08f, .08f, .06f), "f6f1e8");
                foreach (var (x, z) in new[] { (-.12f, .1f), (.12f, -.05f), (-.1f, -.2f), (.11f, .22f) })
                    Art.Ball(a, new(x, .82f, z), Vector3.One * .03f, "f2e6d0");
                var neck = Art.Cylinder(a, new(0, .92f, .32f), .07f, .06f, .4f, "b07a4a");
                neck.Rotation = new Vector3(.5f, 0, 0);
                Art.Ball(a, new(0, 1.12f, .44f), new(.11f, .11f, .14f), "b07a4a");
                Art.Ball(a, new(0, 1.08f, .58f), new(.05f, .05f, .05f), "2a1d14");
                Ear(new(-.09f, 1.2f, .4f), new(.05f, .1f, .03f), "a06c40", .6f, "e8c9a8");
                Ear(new(.09f, 1.2f, .4f), new(.05f, .1f, .03f), "a06c40", -.6f, "e8c9a8");
                Eyes(.07f, 1.15f, .54f);
                break;
            case TrackingAnimal.Badger:
                Legs(.13f, .18f, -.18f, .14f, .045f, "2a2a2a");
                Art.Ball(a, new(0, .24f, 0), new(.24f, .17f, .34f), "8c9194");
                Art.Ball(a, new(0, .26f, .32f), new(.13f, .12f, .17f), "f2f2ef");
                foreach (int side in new[] { -1, 1 })
                    Art.Ball(a, new(side * .065f, .28f, .34f), new(.035f, .07f, .15f), "1d1d1d");
                Art.Ball(a, new(0, .23f, .48f), new(.03f, .03f, .03f), "1d1d1d");
                Ear(new(-.09f, .34f, .26f), new(.035f, .03f, .025f), "f2f2ef", .3f);
                Ear(new(.09f, .34f, .26f), new(.035f, .03f, .025f), "f2f2ef", -.3f);
                Eyes(.06f, .3f, .43f);
                break;
            case TrackingAnimal.Boar:
                Legs(.09f, .15f, -.15f, .16f, .035f, "4a3020");
                Art.Ball(a, new(0, .26f, 0), new(.17f, .15f, .27f), "8a5b3a");
                foreach (float x in new[] { -.1f, 0, .1f })
                    Art.Ball(a, new(x, .33f, 0), new(.025f, .1f, .25f), "c9a16a");
                Art.Ball(a, new(0, .29f, .26f), new(.11f, .1f, .12f), "8a5b3a");
                Art.Cylinder(a, new(0, .26f, .38f), .045f, .05f, .08f, "c98a7a").Rotation = new Vector3(
                    Mathf.Pi / 2,
                    0,
                    0
                );
                Art.Ball(a, new(0, .29f, -.27f), new(.02f, .05f, .02f), "4a3020");
                Ear(new(-.07f, .36f, .24f), new(.035f, .05f, .02f), "6a4028", .5f);
                Ear(new(.07f, .36f, .24f), new(.035f, .05f, .02f), "6a4028", -.5f);
                Eyes(.055f, .33f, .34f);
                break;
            default:
                Legs(.12f, .26f, -.26f, .4f, .05f, "c09660");
                Art.Ball(a, new(0, .52f, 0), new(.2f, .19f, .38f), "c8a06a");
                foreach (
                    var (x, y, z) in new[]
                    {
                        (-.14f, .58f, .1f),
                        (.15f, .6f, -.08f),
                        (-.1f, .66f, -.22f),
                        (.12f, .55f, .24f),
                        (0f, .7f, .02f),
                    }
                )
                    Art.Ball(a, new(x, y, z), Vector3.One * .035f, "6a4a30");
                Art.Ball(a, new(0, .58f, -.4f), new(.05f, .05f, .08f), "c8a06a");
                Art.Ball(a, new(0, .58f, -.47f), new(.04f, .04f, .04f), "1d1d1d");
                Art.Ball(a, new(0, .72f, .38f), new(.17f, .15f, .14f), "c8a06a");
                foreach (int side in new[] { -1, 1 })
                    Art.Ball(a, new(side * .14f, .66f, .36f), new(.06f, .08f, .05f), "f2e6d0");
                Art.Ball(a, new(0, .68f, .5f), new(.06f, .05f, .04f), "f2e6d0");
                Art.Ball(a, new(0, .7f, .53f), new(.022f, .018f, .015f), "8a4a4a");
                var left = Ear(new(-.1f, .84f, .34f), new(.04f, .07f, .025f), "b08850", .2f);
                var right = Ear(new(.1f, .84f, .34f), new(.04f, .07f, .025f), "b08850", -.2f);
                foreach (var ear in new[] { left, right })
                    Art.Cylinder(ear, new(0, .19f, 0), .012f, .004f, .1f, "1d1d1d");
                Eyes(.075f, .76f, .48f);
                break;
        }
        return ears;
    }
}
