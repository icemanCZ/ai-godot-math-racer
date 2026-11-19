using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Settlement;

/// <summary>The places of the village where a decoration can stand; each has a frame of its own (z towards the road).</summary>
internal enum TownPlace
{
    Square,
    House0,
    House1,
    House2,
    House3,
    Garage,
    Shop0,
    Shop1,
    Shop2,
}

/// <summary>
/// One copy of a decoration: where it stands (x, z in the frame of <paramref name="Place"/>) and how far it is turned
/// round from that frame. Every slot lies inside the ground that its place already reserves.
/// </summary>
internal readonly record struct TownSlot(TownPlace Place, float X, float Z, float Yaw = 0);

/// <summary>
/// One decoration of the town that the mayor sells. <paramref name="Id"/> is saved in a game and so is never renamed;
/// <paramref name="Slots"/> are the places in the village where the decoration stands once it is bought, one copy in
/// each; <paramref name="Build"/> draws one copy around the origin of the node it is given (front towards +Z) and, when
/// asked, with solid parts that block the car.
/// </summary>
internal sealed record TownDecoration(string Id, string Name, int Price, TownSlot[] Slots, Action<Node3D, bool> Build);

/// <summary>
/// The eight decorations of the mayor, in the order the shop lists them. Their copies are spread over the village: the
/// square, the front yards of the houses, the garage and the shops. Each is placed in the frame of one of those places
/// and inside the ground it already reserves, so buying one never moves a tree or a character, and the same drawing
/// serves the picture in the shop and the decoration in the village. A decoration only adds boxes and cylinders to the
/// colliders, never the capsules of trunks, which the collision checks pick their test shapes from.
/// </summary>
internal static class TownDecorations
{
    private const float Quarter = Mathf.Pi / 2;

    public static IReadOnlyList<TownDecoration> All { get; } =
        new TownDecoration[]
        {
            new(
                "flowerbeds",
                "Květinové záhony",
                10,
                new TownSlot[]
                {
                    new(TownPlace.Square, -9.7f, 2.2f),
                    new(TownPlace.Square, 9.7f, 2.2f),
                    new(TownPlace.House0, 5, -1),
                    new(TownPlace.House3, 5, -1),
                },
                Flowerbed
            ),
            new(
                "flags",
                "Vlajky u garáže",
                8,
                new TownSlot[] { new(TownPlace.Garage, -5.2f, 5), new(TownPlace.Garage, 5.2f, 5) },
                Flagpole
            ),
            new(
                "lanterns",
                "Papírové lampiony",
                8,
                new TownSlot[] { new(TownPlace.Square, -10, -5.4f), new(TownPlace.House2, -5.4f, 7.2f, Quarter) },
                Lanterns
            ),
            new(
                "statue",
                "Zlatý králíček",
                25,
                new TownSlot[] { new(TownPlace.Square, -4.3f, 1.9f), new(TownPlace.Shop1, 4.8f, 5.6f) },
                Statue
            ),
            new(
                "well",
                "Studna přání",
                15,
                new TownSlot[] { new(TownPlace.Square, 4.3f, 1.9f), new(TownPlace.Shop0, 4.8f, 5.6f) },
                Well
            ),
            new(
                "clock",
                "Hodinový sloup",
                20,
                new TownSlot[] { new(TownPlace.Square, -6.3f, .2f), new(TownPlace.House1, 5.2f, 5.6f) },
                Clock
            ),
            new(
                "tree",
                "Slavnostní stromek",
                12,
                new TownSlot[] { new(TownPlace.Square, 6.3f, .2f), new(TownPlace.Shop2, 4.8f, 5.6f) },
                Tree
            ),
            new("arch", "Uvítací oblouk", 30, new TownSlot[] { new(TownPlace.Square, 0, 7.1f) }, Arch),
        };

    public static TownDecoration? Find(string? id) => All.FirstOrDefault(decoration => decoration.Id == id);

    public static int TotalPrice => All.Sum(decoration => decoration.Price);

    private static readonly string[] Petals = { "f4b1c6", "ffd966", "fff0ce", "ef8f7d", "b9a2e0" };

    private static void Flowerbed(Node3D root, bool solid)
    {
        Art.Cylinder(root, new(0, .12f, 0), 1.25f, 1.25f, .24f, "6d4c3a");
        for (int i = 0; i < 14; i++)
        {
            float angle = i * Mathf.Tau / 14;
            var stone = Art.Block(
                root,
                new(Mathf.Sin(angle) * 1.28f, .17f, Mathf.Cos(angle) * 1.28f),
                new(.62f, .34f, .34f),
                i % 2 == 0 ? "d9cdb4" : "c9bda4"
            );
            stone.Rotation = new(0, angle, 0);
        }
        var random = new Random(31);
        for (int i = 0; i < 26; i++)
        {
            float radius = 1.05f * MathF.Sqrt(random.NextSingle()),
                angle = random.NextSingle() * Mathf.Tau,
                height = .35f + random.NextSingle() * .3f;
            var foot = new Vector3(Mathf.Sin(angle) * radius, .24f, Mathf.Cos(angle) * radius);
            Art.Cylinder(root, foot + Vector3.Up * height / 2, .02f, .02f, height, "4f9a58");
            Art.Ball(root, foot + Vector3.Up * height, new(.15f, .1f, .15f), Petals[i % Petals.Length]);
            Art.Ball(root, foot + Vector3.Up * (height + .06f), Vector3.One * .05f, "efbf59");
        }
        Art.Ball(root, new(0, .5f, 0), new(.5f, .4f, .5f), "5d9f63");
        if (solid)
            Obstacles.Add(root, new(0, .2f, 0), new CylinderShape3D { Radius = 1.35f, Height = .4f });
    }

    private static void Flagpole(Node3D root, bool solid)
    {
        Art.Cylinder(root, new(0, .1f, 0), .34f, .3f, .2f, "b8ac98");
        Art.Cylinder(root, new(0, 3.1f, 0), .05f, .07f, 6.2f, "d8d4cc");
        Art.Ball(root, new(0, 6.3f, 0), Vector3.One * .13f, "f3cf70");
        string[] stripes = { "ef9980", "fff4df", "83c4b3" };
        for (int i = 0; i < 3; i++)
        {
            var stripe = Art.Block(root, new(.95f, 5.85f - i * .36f, 0), new(1.8f, .36f, .04f), stripes[i]);
            stripe.Rotation = new(0, .08f, 0);
        }
        Art.Ball(root, new(.34f, 5.49f, .03f), new(.16f, .16f, .05f), "f0cc76");
        if (solid)
            Obstacles.Box(root, new(0, 3, 0), new(.4f, 6, .4f));
    }

    /// <summary>A string of paper lanterns that runs 10.8 m along +Z, from one lamp post of the square to the next.</summary>
    private static void Lanterns(Node3D root, bool solid)
    {
        string[] colours = { "ef8f7d", "ffd966", "f4b1c6", "8fd1c3", "b9a2e0" };
        const int segments = 12;
        Vector3 Point(int i) => new(0, 3.5f - Mathf.Sin(i / (float)segments * Mathf.Pi) * .6f, i * 10.8f / segments);
        // A post at each end, so that a string that hangs between no lamp posts of the square is held up too.
        foreach (float end in new[] { 0f, 10.8f })
            Art.Cylinder(root, new(0, 1.8f, end), .07f, .06f, 3.6f, "526f71");
        for (int i = 0; i < segments; i++)
            Art.Beam(root, Point(i), Point(i + 1), .03f, "6a5a48");
        for (int i = 1; i < segments; i++)
        {
            var top = Point(i);
            Art.Cylinder(root, top + Vector3.Down * .1f, .012f, .012f, .2f, "6a5a48");
            var colour = colours[i % colours.Length];
            Art.Ball(root, top + Vector3.Down * .38f, new(.2f, .27f, .2f), colour);
            Art.Cylinder(root, top + Vector3.Down * .16f, .08f, .12f, .05f, "5b4a39");
            Art.Cylinder(root, top + Vector3.Down * .6f, .07f, .03f, .06f, "5b4a39");
        }
    }

    private static void Statue(Node3D root, bool solid)
    {
        Art.Block(root, new(0, .17f, 0), new(1.3f, .35f, 1.3f), "c9bda4");
        Art.Block(root, new(0, .8f, 0), new(1, .9f, 1), "e3d8c0");
        Art.Block(root, new(0, 1.3f, 0), new(1.15f, .18f, 1.15f), "c9bda4");
        Art.Block(root, new(0, .75f, .51f), new(.5f, .22f, .03f), "8a6b3b");
        const string gold = "e8b93a";
        Art.Ball(root, new(0, 1.85f, 0), new(.36f, .46f, .32f), gold);
        Art.Ball(root, new(0, 1.78f, .22f), new(.22f, .28f, .12f), "f3cf70");
        Art.Ball(root, new(0, 2.38f, .03f), new(.27f, .26f, .25f), gold);
        foreach (float x in new[] { -.1f, .1f })
        {
            var ear = Art.Ball(root, new(x, 2.85f, 0), new(.08f, .33f, .07f), gold);
            ear.RotationDegrees = new(0, 0, -x * 80);
            Art.Ball(root, new(x * .8f, 2.4f, .24f), Vector3.One * .035f, "384d51");
        }
        Art.Ball(root, new(0, 2.33f, .27f), Vector3.One * .05f, "7a5a2c");
        Art.Ball(root, new(0, 1.55f, -.3f), Vector3.One * .13f, "f3cf70");
        if (solid)
            Obstacles.Box(root, new(0, .75f, 0), new(1.3f, 1.5f, 1.3f));
    }

    private static void Well(Node3D root, bool solid)
    {
        Art.Cylinder(root, new(0, .48f, 0), .98f, .98f, .96f, "b5aa96");
        Art.Cylinder(root, new(0, .79f, 0), .84f, .84f, .05f, "79d0db");
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12;
            var stone = Art.Block(
                root,
                new(Mathf.Sin(angle) * .96f, .6f, Mathf.Cos(angle) * .96f),
                new(.5f, .84f, .24f),
                i % 2 == 0 ? "cfc4ad" : "bdb29b"
            );
            stone.Rotation = new(0, angle, 0);
        }
        Art.Cylinder(root, new(0, 1.03f, 0), 1.08f, 1.08f, .1f, "d6ccb6");
        foreach (float x in new[] { -.88f, .88f })
            Art.Block(root, new(x, 1.7f, 0), new(.14f, 1.35f, .14f), "8a5f43");
        var left = Art.Block(root, new(-.52f, 2.55f, 0), new(1.55f, .1f, 1.5f), "d0674c");
        left.Rotation = new(0, 0, .5f);
        var right = Art.Block(root, new(.52f, 2.55f, 0), new(1.55f, .1f, 1.5f), "c05a40");
        right.Rotation = new(0, 0, -.5f);
        Art.Block(root, new(0, 2.92f, 0), new(.2f, .1f, 1.55f), "a8473a");
        Art.Beam(root, new(-.88f, 1.95f, 0), new(.88f, 1.95f, 0), .09f, "7a5233");
        Art.Beam(root, new(0, 1.95f, 0), new(0, 1.2f, 0), .02f, "6a5a48");
        Art.Cylinder(root, new(0, 1.05f, 0), .17f, .14f, .26f, "a8794e");
        Art.Ball(root, new(.3f, 1.55f, .5f), Vector3.One * .09f, "ffd966");
        if (solid)
        {
            Obstacles.Add(root, new(0, .52f, 0), new CylinderShape3D { Radius = 1.05f, Height = 1.05f });
            foreach (float x in new[] { -.88f, .88f })
                Obstacles.Box(root, new(x, 1.7f, 0), new(.3f, 3.4f, .3f));
        }
    }

    private static void Clock(Node3D root, bool solid)
    {
        Art.Block(root, new(0, .17f, 0), new(.95f, .34f, .95f), "c9bda4");
        Art.Cylinder(root, new(0, 1.55f, 0), .22f, .18f, 2.4f, "3f5f66");
        Art.Cylinder(root, new(0, 2.8f, 0), .3f, .27f, .2f, "526f71");
        var rim = Art.Cylinder(root, new(0, 3.35f, 0), .7f, .7f, .34f, "526f71");
        rim.RotationDegrees = new(90, 0, 0);
        var face = Art.Cylinder(root, new(0, 3.35f, 0), .6f, .6f, .4f, "fff4df");
        face.RotationDegrees = new(90, 0, 0);
        foreach (float side in new[] { 1f, -1f })
        {
            float z = side * .21f;
            for (int hour = 0; hour < 12; hour++)
            {
                float angle = hour * Mathf.Tau / 12;
                Art.Block(
                    root,
                    new(Mathf.Sin(angle) * .5f, 3.35f + Mathf.Cos(angle) * .5f, z),
                    new(hour % 3 == 0 ? .07f : .04f, hour % 3 == 0 ? .1f : .06f, .02f),
                    "3c5960"
                ).Rotation = new(0, 0, -angle);
            }
            // The hands stand at ten past ten and are drawn from the centre of the face.
            var hourHand = Art.Block(root, new(-.12f, 3.5f, z + side * .015f), new(.06f, .36f, .02f), "3c5960");
            hourHand.Rotation = new(0, 0, .55f);
            var minute = Art.Block(root, new(.17f, 3.52f, z + side * .03f), new(.045f, .52f, .02f), "3c5960");
            minute.Rotation = new(0, 0, -.9f);
            Art.Ball(root, new(0, 3.35f, z + side * .04f), Vector3.One * .05f, "c9594d");
        }
        Art.Cylinder(root, new(0, 4.25f, 0), .62f, .02f, .65f, "c9594d");
        Art.Ball(root, new(0, 4.66f, 0), Vector3.One * .08f, "f3cf70");
        if (solid)
            Obstacles.Box(root, new(0, 2, 0), new(.9f, 4, .9f));
    }

    private static void Tree(Node3D root, bool solid)
    {
        string[] baubles = { "ef5b4b", "ffd24a", "7fc9ff", "f4b1c6" };
        Art.Cylinder(root, new(0, .18f, 0), .5f, .44f, .36f, "a8794e");
        Art.Cylinder(root, new(0, .8f, 0), .2f, .15f, 1.2f, "7a5a40");
        (float Radius, float Height, float Middle, string Colour)[] tiers =
        {
            (1.35f, 1.5f, 1.9f, "3f8f55"),
            (1.02f, 1.3f, 2.85f, "4aa062"),
            (.68f, 1.1f, 3.7f, "58b36f"),
        };
        for (int tier = 0; tier < tiers.Length; tier++)
        {
            var (radius, height, middle, colour) = tiers[tier];
            Art.Cylinder(root, new(0, middle, 0), radius, .03f, height, colour);
            for (int k = 0; k < 8; k++)
            {
                float angle = k * Mathf.Tau / 8 + tier * .4f,
                    fraction = .3f + (k % 2) * .18f,
                    reach = radius * (1 - fraction) + .05f;
                var spot = new Vector3(
                    Mathf.Sin(angle) * reach,
                    middle - height / 2 + fraction * height,
                    Mathf.Cos(angle) * reach
                );
                if (k % 4 == 3)
                    Art.Lamp(root, spot, Vector3.One * .09f, "fff3a0");
                else
                    Art.Ball(root, spot, Vector3.One * .1f, baubles[(k + tier) % baubles.Length]);
            }
        }
        Art.Lamp(root, new(0, 4.5f, 0), new(.34f, .34f, .08f), "ffd24a");
        Art.Lamp(root, new(0, 4.5f, 0), new(.34f, .34f, .08f), "ffd24a").Rotation = new(0, 0, Mathf.Pi / 4);
        string[] gifts = { "ef9980", "83c4b3", "b9a2e0" };
        for (int i = 0; i < gifts.Length; i++)
        {
            float angle = 2.3f + i * 1.15f;
            var spot = new Vector3(Mathf.Sin(angle) * 1.05f, .16f, Mathf.Cos(angle) * 1.05f);
            Art.Block(root, spot, new(.42f - i * .05f, .32f, .42f - i * .05f), gifts[i]);
            Art.Block(root, spot + Vector3.Up * .01f, new(.07f, .34f, .44f - i * .05f), "fff0ce");
        }
        if (solid)
            Obstacles.Box(root, new(0, 1.5f, 0), new(1.1f, 3, 1.1f));
    }

    private static void Arch(Node3D root, bool solid)
    {
        foreach (float x in new[] { -3.25f, 3.25f })
        {
            Art.Block(root, new(x, .17f, 0), new(1, .34f, 1), "c9bda4");
            Art.Block(root, new(x, 2.55f, 0), new(.7f, 4.4f, .7f), "e3d8c0");
            Art.Block(root, new(x, 4.85f, 0), new(.95f, .25f, .95f), "c9bda4");
            Art.Ball(root, new(x, 5.15f, 0), Vector3.One * .22f, "f3cf70");
        }
        Art.Block(root, new(0, 5.2f, 0), new(7.5f, .75f, .85f), "e3d8c0");
        Art.Block(root, new(0, 5.7f, 0), new(7.9f, .25f, 1.05f), "c9bda4");
        Art.Block(root, new(0, 6.2f, 0), new(2.8f, .75f, .85f), "e3d8c0");
        Art.Block(root, new(0, 6.65f, 0), new(3.1f, .2f, 1.05f), "c9bda4");
        Art.Ball(root, new(0, 6.95f, 0), Vector3.One * .26f, "f3cf70");
        string[] garland = { "f4b1c6", "ffd966", "8fd1c3", "ef8f7d" };
        for (int i = 0; i < 15; i++)
            Art.Ball(
                root,
                new(-3.15f + i * .45f, 4.68f - Mathf.Sin(i / 14f * Mathf.Pi) * .2f, .5f),
                new(.17f, .17f, .12f),
                garland[i % garland.Length]
            );
        Art.Sign(root, new(0, 5.2f, .6f), "VÍTEJTE", 40);
        if (solid)
            foreach (float x in new[] { -3.25f, 3.25f })
                Obstacles.Box(root, new(x, 2.4f, 0), new(1, 4.8f, 1));
    }
}
