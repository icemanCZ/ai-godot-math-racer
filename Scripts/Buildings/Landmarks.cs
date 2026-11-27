using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The eight wonders of the land beyond the old valley (<c>docs/EXPLORER_PLAN.md</c>), each a node at the middle of its
/// ground turned so that its local +Z faces the road it stands beside: an old oak with a little door in its trunk, a rock
/// arch the car drives through, the ruin of a castle tower, a giant toadstool, a ring of standing stones, the skeleton of
/// a dinosaur, a well under a little roof and a scarecrow in a pumpkin patch. Beside each, towards the road, the owl's
/// blue flag goes up once the wonder is written into her map. Every part stands on the ground at its own spot.
/// </summary>
public partial class LandmarkModels : Node3D
{
    private readonly Terrain ground;
    private readonly Node3D[] flags;

    /// <summary>The wonders in the map, which the owl's little owl counts.</summary>
    internal int RecordedCount { get; private set; }

    internal LandmarkModels(IReadOnlyList<Landmark> landmarks, Terrain ground)
    {
        this.ground = ground;
        flags = new Node3D[landmarks.Count];
        foreach (var landmark in landmarks)
        {
            var node = new Node3D
            {
                Name = $"Landmark{landmark.Kind}",
                Position = ground.At(landmark.Center.X, landmark.Center.Z),
                Rotation = new Vector3(0, landmark.Yaw, 0),
            };
            AddChild(node);
            var at = new Placer(node, ground);
            switch (landmark.Kind)
            {
                case ExplorerPlans.Oak:
                    Oak(at);
                    break;
                case ExplorerPlans.Arch:
                    Arch(at);
                    break;
                case ExplorerPlans.Ruin:
                    Ruin(at);
                    break;
                case ExplorerPlans.Mushroom:
                    Mushroom(at);
                    break;
                case ExplorerPlans.Stones:
                    Stones(at);
                    break;
                case ExplorerPlans.Dinosaur:
                    Dinosaur(at);
                    break;
                case ExplorerPlans.Well:
                    Well(at);
                    break;
                default:
                    Scarecrow(at);
                    break;
            }
            flags[landmark.Kind] = Flag(at, ExplorerPlans.Radius(landmark.Kind));
        }
    }

    /// <summary>Puts up the owl's flag beside every wonder written into her map, and takes it down from the others.</summary>
    internal void SetRecorded(IReadOnlyCollection<int> recorded)
    {
        RecordedCount = 0;
        for (int i = 0; i < flags.Length; i++)
        {
            bool shown = false;
            foreach (int kind in recorded)
                shown |= kind == i;
            flags[i].Visible = shown;
            if (shown)
                RecordedCount++;
        }
    }

    /// <summary>Whether the owl's flag stands beside a wonder, for the checks.</summary>
    internal bool FlagShown(int kind) => flags[kind].Visible;

    /// <summary>Places the parts of one wonder in its frame, each on the ground at its own spot.</summary>
    private readonly struct Placer
    {
        internal readonly Node3D Node;
        private readonly Terrain ground;
        private readonly Basis basis;

        internal Placer(Node3D node, Terrain ground)
        {
            Node = node;
            this.ground = ground;
            basis = new Basis(Vector3.Up, node.Rotation.Y);
        }

        /// <summary>The ground under a point of the wonder's frame, as a height in that frame.</summary>
        internal float Floor(float x, float z)
        {
            var p = Node.Position + basis * new Vector3(x, 0, z);
            return ground.Height(p.X, p.Z) - Node.Position.Y;
        }

        /// <summary>A point of the wonder's frame, <paramref name="lift"/> above the ground there.</summary>
        internal Vector3 At(float x, float z, float lift = 0) => new(x, Floor(x, z) + lift, z);

        /// <summary>A point of the wonder's frame, <paramref name="lift"/> above the ground under its middle.</summary>
        internal Vector3 Up(float x, float z, float lift) => new(x, lift, z);
    }

    /// <summary>A small blue flag with a white star on a pole, beside the wonder towards the road: the owl was told.</summary>
    private static Node3D Flag(Placer at, float radius)
    {
        var flag = new Node3D { Name = "OwlFlag", Visible = false };
        at.Node.AddChild(flag);
        var foot = at.At(radius * .55f, radius - .5f);
        Art.Cylinder(flag, foot + Vector3.Up * 1.4f, .06f, .05f, 2.8f, "e8e1d0");
        Art.Ball(flag, foot + Vector3.Up * 2.86f, Vector3.One * .1f, "f3c23c");
        Art.Block(flag, foot + new Vector3(.45f, 2.45f, 0), new(.9f, .6f, .04f), "3b78c9");
        Art.Ball(flag, foot + new Vector3(.45f, 2.45f, .03f), new(.15f, .15f, .02f), "fff5df");
        return flag;
    }

    private static void Oak(Placer at)
    {
        var n = at.Node;
        float floor = at.Floor(0, 0);
        // A thick old trunk with roots spreading over the ground, a red door with a round window and a hollow above it.
        Art.Cylinder(n, at.Up(0, 0, floor + 3.4f), 1.55f, 1.05f, 7, "6b4a2e");
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5 + .3f;
            var root = Art.Ball(
                n,
                at.At(Mathf.Sin(angle) * 1.6f, Mathf.Cos(angle) * 1.6f, .15f),
                new(.55f, .45f, 1.1f),
                "5d3f27"
            );
            root.Rotation = new Vector3(0, angle, 0);
        }
        Art.Block(n, at.Up(0, 1.46f, floor + .75f), new(.8f, 1.4f, .1f), "c0504d");
        Art.Ball(n, at.Up(0, 1.5f, floor + 1.15f), new(.18f, .18f, .04f), "ffe9a6");
        Art.Ball(n, at.Up(.26f, 1.52f, floor + .65f), Vector3.One * .06f, "f3c23c");
        Art.Ball(n, at.Up(0, 1.2f, floor + 3.3f), new(.42f, .62f, .2f), "2e2118");
        // Three great boughs and a round crown of four clumps of leaves.
        foreach (var (x, z, tilt) in new[] { (-1.6f, .4f, 38f), (1.7f, -.3f, -40f), (.2f, -1.6f, 30f) })
        {
            var bough = Art.Cylinder(n, at.Up(x, z, floor + 7.4f), .45f, .3f, 4, "6b4a2e");
            bough.RotationDegrees = z < -1 ? new Vector3(-tilt, 0, 0) : new Vector3(0, 0, tilt);
        }
        Art.Ball(n, at.Up(0, 0, floor + 10.2f), new(6.2f, 4.2f, 6.2f), "4f8a3a");
        Art.Ball(n, at.Up(-3.6f, 1.3f, floor + 9.2f), new(4.2f, 3.4f, 4.2f), "5a9a42");
        Art.Ball(n, at.Up(3.4f, -1.2f, floor + 9.6f), new(4.4f, 3.5f, 4.4f), "467f33");
        Art.Ball(n, at.Up(.4f, .2f, floor + 12.8f), new(4.2f, 2.8f, 4.2f), "5fa548");
        // A swing on two ropes under the bough towards the road.
        var seat = at.At(-2.6f, 1.6f, .7f);
        foreach (float side in new[] { -.45f, .45f })
            Art.Beam(n, seat + new Vector3(side, 0, 0), seat + new Vector3(side, 6.6f, 0), .05f, "d9c79f");
        Art.Block(n, seat, new(1.2f, .1f, .45f), "a77a59");
        Obstacles.Trunk(n, at.Up(0, 0, floor), 1.4f, 7);
    }

    private static void Arch(Placer at)
    {
        var n = at.Node;
        float floor = Mathf.Min(at.Floor(-4.2f, 0), at.Floor(4.2f, 0));
        // Two piles of rounded rock with a great boulder across them, wide enough for the car to drive through, moss on
        // top and a few loose stones round the feet.
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * 4.3f;
            Art.Ball(n, at.Up(x, 0, floor + 1.2f), new(1.9f, 1.8f, 2.1f), "8d8a83");
            Art.Ball(n, at.Up(x - side * .2f, .2f, floor + 3.4f), new(1.6f, 1.6f, 1.8f), "9b978f");
            Art.Ball(n, at.Up(x - side * .5f, -.2f, floor + 5.3f), new(1.5f, 1.3f, 1.7f), "a5a198");
            Obstacles.Box(n, at.Up(x, 0, floor + 3), new(3, 6, 3.2f));
            Art.Ball(n, at.At(x + side * 2.1f, 1.4f, .3f), new(.7f, .5f, .6f), "8d8a83");
        }
        var lintel = Art.Ball(n, at.Up(0, 0, floor + 7.2f), new(5.8f, 1.5f, 1.9f), "a29e95");
        lintel.RotationDegrees = new Vector3(0, 0, 3);
        Art.Ball(n, at.Up(-.6f, 0, floor + 8.4f), new(3.6f, .6f, 1.5f), "6f9a4a");
        Art.Ball(n, at.Up(2.4f, .3f, floor + 8.2f), new(1.4f, .5f, 1.1f), "7aa653");
        Obstacles.Box(n, at.Up(0, 0, floor + 7.2f), new(11, 2.6f, 3.4f));
    }

    private static void Ruin(Placer at)
    {
        var n = at.Node;
        float floor = at.Floor(0, -1);
        // A round tower with a broken top, battlements on the part that still stands, an arched window and a door, and the
        // stumps of the walls that ran out from it, with fallen stones in the grass.
        Art.Cylinder(n, at.Up(0, -1, floor + 4.5f), 3, 2.8f, 9, "a7a196");
        for (int i = 0; i < 7; i++)
        {
            float angle = Mathf.Pi * .1f + i * Mathf.Pi * .17f;
            Art.Block(
                n,
                at.Up(Mathf.Sin(angle) * 2.55f, -1 + Mathf.Cos(angle) * 2.55f, floor + 9.5f),
                new(1, 1.1f, .8f),
                "9c968b"
            ).Rotation = new Vector3(0, angle, 0);
        }
        Art.Cylinder(n, at.Up(0, -1, floor + 9.1f), 2.4f, 2.4f, .3f, "6c675f");
        Art.Block(n, at.Up(0, 1.82f, floor + 6), new(.9f, 1.6f, .2f), "2d2a26");
        Art.Ball(n, at.Up(0, 1.82f, floor + 6.8f), new(.45f, .45f, .1f), "2d2a26");
        Art.Block(n, at.Up(0, 1.86f, floor + 1.1f), new(1.3f, 2.2f, .2f), "4a3424");
        Art.Ball(n, at.Up(0, 1.86f, floor + 2.2f), new(.65f, .5f, .1f), "4a3424");
        foreach (var (x, z, w, h, yaw) in new[] { (-5.2f, -.6f, 4.6f, 3.4f, 0f), (4.6f, -2.6f, 3.4f, 2.2f, 30f) })
        {
            var wall = Art.Block(n, at.Up(x, z, at.Floor(x, z) + h / 2 - .1f), new(w, h, 1.2f), "a39d92");
            wall.RotationDegrees = new Vector3(0, yaw, 0);
            Obstacles.Box(
                n,
                at.Up(x, z, at.Floor(x, z) + h / 2),
                new(w, h, 1.2f),
                new Basis(Vector3.Up, Mathf.DegToRad(yaw))
            );
        }
        foreach (var (x, z) in new[] { (-3.4f, 3.4f), (3f, 2.6f), (5.8f, .8f), (-6.2f, 2f) })
            Art.Block(n, at.At(x, z, .25f), new(.9f, .5f, .7f), "9c968b").RotationDegrees = new(0, x * 20, 8);
        // A little red pennant on a pole on the battlements.
        Art.Cylinder(n, at.Up(1.4f, -1.6f, floor + 11), .05f, .05f, 3, "5b4636");
        Art.Block(n, at.Up(1.9f, -1.6f, floor + 12.1f), new(1, .6f, .04f), "c0392b");
        Obstacles.Trunk(n, at.Up(0, -1, floor), 2.9f, 9);
    }

    private static void Mushroom(Placer at)
    {
        var n = at.Node;
        float floor = at.Floor(0, 0);
        // A toadstool as tall as a house: a white stem with a ring, a red cap with white spots, and three small ones.
        Art.Cylinder(n, at.Up(0, 0, floor + 2.6f), 1.25f, .95f, 5.2f, "f4eee0");
        Art.Cylinder(n, at.Up(0, 0, floor + 3.9f), 1.35f, 1.6f, .35f, "ebe3d1");
        Art.Ball(n, at.Up(0, 0, floor + 5.6f), new(4.6f, 2, 4.6f), "d23b2f");
        Art.Ball(n, at.Up(0, 0, floor + 5.1f), new(4.3f, 1.1f, 4.3f), "f1e3c7");
        for (int i = 0; i < 11; i++)
        {
            float angle = i * 2.4f;
            float r = i == 0 ? 0 : 1.6f + (i % 3) * .8f;
            float y = 7.4f - r * r * .07f;
            Art.Ball(n, at.Up(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, floor + y), new(.5f, .22f, .5f), "fff8ec");
        }
        foreach (var (x, z, s) in new[] { (3.2f, 2.2f, .6f), (-3.4f, 1.6f, .45f), (2.2f, -3.3f, .5f) })
        {
            Art.Cylinder(n, at.At(x, z, s), s * .35f, s * .3f, s * 2, "f4eee0");
            Art.Ball(n, at.At(x, z, s * 2.1f), new(s * 1.1f, s * .55f, s * 1.1f), "d23b2f");
            Art.Ball(n, at.At(x, z, s * 2.55f), new(s * .25f, s * .1f, s * .25f), "fff8ec");
        }
        Obstacles.Trunk(n, at.Up(0, 0, floor), 1.2f, 5);
    }

    private static void Stones(Placer at)
    {
        var n = at.Node;
        // Seven tall grey stones in a ring, two of them with a third across their tops, and a flat stone in the middle.
        for (int i = 0; i < 7; i++)
        {
            float angle = i * Mathf.Tau / 7 + .45f;
            float x = Mathf.Sin(angle) * 5.6f,
                z = Mathf.Cos(angle) * 5.6f;
            float height = 3.2f + (i % 3) * .5f;
            var stone = Art.Block(n, at.At(x, z, height / 2 - .2f), new(1.3f, height, .8f), "8f8c86");
            stone.Rotation = new Vector3(0, angle, i % 2 == 0 ? .04f : -.05f);
            Art.Block(n, at.At(x, z, height - .1f), new(1.1f, .25f, .7f), "9e9b94").Rotation = new(0, angle, 0);
            Obstacles.Box(n, at.At(x, z, height / 2), new(1.3f, height, .8f), new Basis(Vector3.Up, angle));
        }
        float a0 = 4 * Mathf.Tau / 7 + .45f,
            a1 = 5 * Mathf.Tau / 7 + .45f;
        var middle = new Vector3((Mathf.Sin(a0) + Mathf.Sin(a1)) * 2.8f, 0, (Mathf.Cos(a0) + Mathf.Cos(a1)) * 2.8f);
        var cap = Art.Block(n, at.At(middle.X, middle.Z, 4.15f), new(5.4f, .7f, .9f), "99968f");
        cap.Rotation = new Vector3(0, (a0 + a1) / 2 + Mathf.Pi / 2, 0);
        Art.Cylinder(n, at.At(0, 0, .2f), 1.5f, 1.4f, .4f, "a6a39b");
    }

    private static void Dinosaur(Placer at)
    {
        var n = at.Node;
        float floor = at.Floor(0, 0);
        const string bone = "f2ead6",
            shade = "ddd2b8";
        // The skeleton of a long-necked dinosaur, standing across the view from the road: a curved backbone of round
        // bones from the tip of the tail up the long neck to the skull, ribs hanging from the back, and four legs.
        Vector3 Spine(float t)
        {
            // t from -1 (tip of the tail) to 1 (the skull).
            float x = t * 8.5f;
            float y = t < 0 ? 4.2f - t * t * 3.6f : 4.2f + Mathf.Pow(t, 1.6f) * 5.6f;
            return at.Up(x, 0, floor + y);
        }
        for (int i = 0; i <= 26; i++)
        {
            float t = -1 + i / 13f;
            float size = t < 0 ? .5f + (1 + t) * .3f : .8f - t * .45f;
            Art.Ball(n, Spine(t), Vector3.One * size, i % 2 == 0 ? bone : shade);
        }
        for (int i = 0; i < 7; i++)
        {
            float x = -2.6f + i * .85f;
            float top = 4.2f + (x > 0 ? Mathf.Pow(x / 8.5f, 1.6f) * 5.6f : 0) - .4f;
            float drop = 2 - Mathf.Abs(i - 3) * .3f;
            foreach (int side in new[] { -1, 1 })
            {
                var from = at.Up(x, side * .2f, floor + top);
                var to = at.Up(x + .2f, side * 1.3f, floor + top - drop);
                Art.Beam(n, from, to, .16f, bone);
            }
        }
        var skull = Spine(1);
        Art.Ball(n, skull + new Vector3(.7f, -.1f, 0), new(1.1f, .6f, .6f), bone);
        Art.Ball(n, skull + new Vector3(.4f, .15f, .38f), Vector3.One * .17f, "3b3433");
        Art.Ball(n, skull + new Vector3(.4f, .15f, -.38f), Vector3.One * .17f, "3b3433");
        for (int i = 0; i < 4; i++)
            Art.Block(n, skull + new Vector3(1 + i * .2f, -.55f, 0), new(.07f, .2f, .5f), "fffdf5");
        foreach (var (x, z) in new[] { (-2.4f, 1.1f), (-2.4f, -1.1f), (2.2f, 1.1f), (2.2f, -1.1f) })
        {
            var hip = at.Up(x, z * .5f, floor + 3.8f);
            var knee = at.Up(x + .3f, z, floor + 1.9f);
            var foot = at.At(x, z, .2f);
            Art.Beam(n, hip, knee, .38f, bone);
            Art.Beam(n, knee, foot, .32f, shade);
            Art.Ball(n, knee, Vector3.One * .32f, bone);
            Art.Ball(n, foot, new(.5f, .2f, .6f), bone);
            Obstacles.Box(n, at.At(x, z, 1.9f), new(.8f, 3.8f, .8f));
        }
        // A little sign on two posts in front: the bones of the valley's oldest dweller.
        var board = at.At(3.6f, 3.4f, .9f);
        Art.Block(n, board, new(1.6f, .8f, .1f), "c39770");
        foreach (float side in new[] { -.6f, .6f })
            Art.Block(n, board + new Vector3(side, -.5f, -.06f), new(.1f, 1, .1f), "8a5a35");
        Art.Ball(n, board + new Vector3(0, 0, .06f), new(.45f, .18f, .02f), bone);
    }

    private static void Well(Placer at)
    {
        var n = at.Node;
        float floor = at.Floor(0, 0);
        // A round stone well with water inside, two posts and a little shingled roof, a winch with its rope and bucket,
        // and a birch beside it so that it is seen over the grass.
        Art.Cylinder(n, at.Up(0, 0, floor + .55f), 1.5f, 1.5f, 1.1f, "9a958c");
        Art.Cylinder(n, at.Up(0, 0, floor + 1.12f), 1.58f, 1.58f, .14f, "b2ada3");
        Art.Cylinder(n, at.Up(0, 0, floor + 1), 1.15f, 1.15f, .1f, "4f8fc0");
        foreach (int side in new[] { -1, 1 })
            Art.Block(n, at.Up(side * 1.35f, 0, floor + 2.1f), new(.2f, 2.4f, .2f), "7a5236");
        foreach (int side in new[] { -1, 1 })
        {
            var roof = Art.Block(n, at.Up(0, side * .55f, floor + 3.45f), new(3.4f, .12f, 1.4f), "b5543c");
            roof.RotationDegrees = new Vector3(side * 35, 0, 0);
        }
        Art.Block(n, at.Up(0, 0, floor + 3.86f), new(3.5f, .12f, .14f), "8e3f2d");
        Art.Log(n, at.Up(0, 0, floor + 2.6f), Vector3.Right, .14f, 2.5f, "a77a59");
        Art.Beam(n, at.Up(0, 0, floor + 2.6f), at.Up(0, 0, floor + 1.7f), .03f, "d9c79f");
        Art.Cylinder(n, at.Up(0, 0, floor + 1.5f), .25f, .3f, .4f, "8a6a45");
        Art.Ball(n, at.At(1.9f, .9f, .25f), new(.35f, .25f, .3f), "f2cf4a");
        Art.Ball(n, at.At(-2, .7f, .2f), new(.3f, .2f, .3f), "e98ab0");
        var birch = at.At(-3.2f, -1.8f);
        Art.Cylinder(n, birch + Vector3.Up * 3.5f, .26f, .18f, 7, "f1ede4");
        for (int i = 0; i < 5; i++)
            Art.Block(n, birch + Vector3.Up * (1.2f + i * 1.2f), new(.3f, .08f, .3f), "3b3433").RotationDegrees = new(
                0,
                i * 40,
                0
            );
        Art.Ball(n, birch + Vector3.Up * 7.4f, new(2.2f, 2.8f, 2.2f), "8fc45a");
        Art.Ball(n, birch + new Vector3(.8f, 6.2f, .4f), new(1.5f, 1.8f, 1.5f), "7cb84d");
        Obstacles.Trunk(n, at.Up(0, 0, floor), 1.5f, 2);
        Obstacles.Trunk(n, birch, .25f, 7);
    }

    private static void Scarecrow(Placer at)
    {
        var n = at.Node;
        float floor = at.Floor(0, 0);
        // A scarecrow on its post with arms out, a straw hat, a patched shirt and a turnip head, two crows on his arms,
        // and big orange pumpkins round him.
        Art.Cylinder(n, at.Up(0, 0, floor + 2.2f), .12f, .12f, 4.4f, "7a5236");
        Art.Block(n, at.Up(0, 0, floor + 3.2f), new(3.6f, .16f, .16f), "7a5236");
        Art.Block(n, at.Up(0, 0, floor + 2.8f), new(1.3f, 1.5f, .6f), "4f7fb8");
        Art.Block(n, at.Up(-.3f, .31f, floor + 2.6f), new(.4f, .4f, .02f), "e8b938");
        Art.Block(n, at.Up(.35f, .31f, floor + 3.1f), new(.3f, .3f, .02f), "c0504d");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(n, at.Up(side * 1.2f, 0, floor + 3.2f), new(1.1f, .5f, .5f), "4f7fb8");
            Art.Ball(n, at.Up(side * 1.85f, 0, floor + 3.15f), new(.25f, .35f, .25f), "e8c46a");
        }
        Art.Ball(n, at.Up(0, 0, floor + 4.1f), new(.55f, .55f, .55f), "f0dfb0");
        Art.Ball(n, at.Up(-.2f, .47f, floor + 4.2f), Vector3.One * .08f, "2d2a26");
        Art.Ball(n, at.Up(.2f, .47f, floor + 4.2f), Vector3.One * .08f, "2d2a26");
        Art.Block(n, at.Up(0, .5f, floor + 3.95f), new(.35f, .05f, .03f), "c0504d");
        Art.Cylinder(n, at.Up(0, 0, floor + 4.55f), 1.1f, 1.1f, .08f, "e0b85a");
        Art.Cylinder(n, at.Up(0, 0, floor + 4.8f), .5f, .45f, .5f, "e0b85a");
        Art.Cylinder(n, at.Up(0, 0, floor + 4.62f), .52f, .52f, .12f, "c0504d");
        foreach (float x in new[] { -1.4f, 1.1f })
        {
            Art.Ball(n, at.Up(x, 0, floor + 3.55f), new(.22f, .2f, .32f), "2b2b30");
            Art.Ball(n, at.Up(x, .24f, floor + 3.68f), Vector3.One * .13f, "2b2b30");
            Art.Block(n, at.Up(x, .38f, floor + 3.66f), new(.05f, .05f, .14f), "e8b938");
        }
        for (int i = 0; i < 9; i++)
        {
            float angle = i * 2.1f + .5f;
            float r = 2.2f + (i % 3) * 1.1f;
            float s = .45f + (i % 4) * .12f;
            var p = at.At(Mathf.Sin(angle) * r, Mathf.Cos(angle) * r, s * .75f);
            Art.Ball(n, p, new(s * 1.2f, s * .85f, s * 1.2f), i % 3 == 0 ? "e8862a" : "f39a33");
            Art.Cylinder(n, p + Vector3.Up * s * .85f, .05f, .04f, .25f, "5a7a2e");
        }
        Obstacles.Trunk(n, at.Up(0, 0, floor), .3f, 4.4f);
    }
}
