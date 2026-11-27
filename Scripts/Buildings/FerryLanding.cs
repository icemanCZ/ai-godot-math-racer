using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The landing of the ferry to the bird island (<c>docs/FERRY_PLAN.md</c>), raised by <see cref="FerryPlans.Trips"/>
/// deliveries: the piles of the landing beside the homestead of the river border, the piles of the pier that runs from
/// the edge of the valley down the bank to the border river, the boards of the pier, the bottom of the punt and its sides
/// with the pole. Before the first the old punt of the ferrywoman lies broken on the bank. All the parts are made at once
/// and <see cref="SetStage"/> only shows those built. The punt is a node of its own (<see cref="Punt"/>), which the
/// crossing moves down the river with the car and the heron aboard. The landing and its pier take no collision: the car
/// drives over the landing's deck as over the ground and never onto the pier, which lies beyond the edge of the valley.
/// </summary>
public partial class FerryLanding : Node3D
{
    private readonly Node3D[] parts = new Node3D[FerryPlans.Trips + 1];
    private readonly Terrain ground;
    private readonly FerryRoute route;
    private readonly Node3D wreck;

    internal FerrySite Site { get; }

    /// <summary>The deliveries the landing has had, 0 to <see cref="FerryPlans.Trips"/>.</summary>
    internal int Stage { get; private set; }

    internal bool Complete => Stage >= FerryPlans.Trips;

    /// <summary>The punt, which waits at the end of the pier and carries the car to the island and back.</summary>
    internal Node3D Punt { get; }

    /// <summary>The heron at the landing, who sends the trailer and poles the punt.</summary>
    internal Character Heron { get; set; } = null!;

    /// <summary>The middle of the landing, for the camera of a celebration.</summary>
    internal Vector3 Focus => Site.Landing + Vector3.Up * 1.5f;

    /// <summary>Where the car stands on the punt, in the punt's frame (+Z ahead).</summary>
    private static readonly Vector3 DeckSeat = new(0, .42f, .7f);

    /// <summary>Where the heron stands on the punt with her pole, at its stern.</summary>
    private static readonly Vector3 SternSpot = new(0, .42f, -3.1f);

    internal FerryLanding(FerrySite site, FerryRoute route, Terrain ground)
    {
        Site = site;
        this.route = route;
        this.ground = ground;
        for (int i = 0; i <= FerryPlans.Trips; i++)
        {
            parts[i] = new Node3D { Name = "FerryStage" + i };
            AddChild(parts[i]);
        }
        Punt = new Node3D { Name = "Punt" };
        AddChild(Punt);
        wreck = new Node3D { Name = "OldPunt" };
        parts[0].AddChild(wreck);
        BuildSign();
        BuildWreck();
        BuildLandingPiles();
        BuildPierPiles();
        BuildPierDeck();
        BuildPunt();
        ParkAtHome();
        SetStage(0);
    }

    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, FerryPlans.Trips);
        for (int i = 1; i <= FerryPlans.Trips; i++)
            parts[i].Visible = i <= Stage;
        // The punt's bottom and its sides are its own stages, carried on the punt's node.
        puntBottom.Visible = Stage >= 4;
        puntSides.Visible = Stage >= 5;
        wreck.Visible = Stage < 4;
    }

    private Node3D puntBottom = null!;
    private Node3D puntSides = null!;

    /// <summary>Puts the punt at the end of the pier, headed down the river.</summary>
    internal void ParkAtHome()
    {
        var (position, yaw) = route.At(0, ValleyBorders.SeaLevel, false);
        PlacePunt(position, yaw);
    }

    /// <summary>Puts the punt at <paramref name="distance"/> along the way, on the water of the sea at its surface now.</summary>
    internal void Sail(float distance, float seaSurface, bool home)
    {
        var (position, yaw) = route.At(distance, seaSurface, home);
        // It rocks a little on the water.
        float time = Time.GetTicksMsec() / 1000f;
        PlacePunt(position + Vector3.Up * Mathf.Sin(time * 1.7f) * .05f, yaw, Mathf.Sin(time * 1.3f) * .02f);
    }

    /// <summary>Puts the punt at the island's pier, headed back up to the river, on the sea at its surface now.</summary>
    internal void ParkAtIsland(float seaSurface)
    {
        var (position, yaw) = route.At(0, seaSurface, true);
        PlacePunt(position, yaw);
    }

    private void PlacePunt(Vector3 position, float yaw, float roll = 0)
    {
        Punt.Position = position;
        Punt.Rotation = new Vector3(0, yaw, roll);
    }

    /// <summary>Where the car stands on the punt now, and the way the punt heads.</summary>
    internal (Vector3 Position, float Heading) CarSeat => (Punt.Transform * DeckSeat, Punt.Rotation.Y + Mathf.Pi);

    /// <summary>Where the heron stands on the punt now.</summary>
    internal Vector3 HeronSeat => Punt.Transform * SternSpot;

    /// <summary>A post with the sign of the ferry, at the landing's side towards the road.</summary>
    private void BuildSign()
    {
        var foot = Site.Local(-3.6f, -2.2f);
        var post = new Node3D { Name = "FerrySign", Position = ground.At(foot.X, foot.Z) };
        post.Rotation = new Vector3(0, Site.Yaw + Mathf.Pi, 0);
        parts[0].AddChild(post);
        Art.Block(post, new Vector3(0, 1.1f, 0), new Vector3(.16f, 2.2f, .16f), "7a5536");
        Art.Block(post, new Vector3(0, 2.1f, 0), new Vector3(1.9f, .7f, .1f), "d7b47c");
        Art.Sign(post, new Vector3(0, 2.1f, .1f), Texts.SignBirds, 30).Billboard = BaseMaterial3D
            .BillboardModeEnum
            .Disabled;
        // A little punt with a bird above it, for those who cannot read the sign.
        Art.Block(post, new Vector3(0, 2.85f, 0), new Vector3(.9f, .14f, .1f), "8a5a35");
        Art.Ball(post, new Vector3(.12f, 3.05f, 0), new Vector3(.22f, .16f, .1f), "c9d3dc");
        Art.Ball(post, new Vector3(-.12f, 3.2f, 0), new Vector3(.08f, .2f, .08f), "c9d3dc");
    }

    /// <summary>The ferrywoman's old punt, broken, on the ground beside the landing until the new one is built.</summary>
    private void BuildWreck()
    {
        var at = Site.Local(4.5f, 1.5f);
        wreck.Position = ground.At(at.X, at.Z);
        wreck.Rotation = new Vector3(0, Site.Yaw + .5f, .2f);
        Art.Block(wreck, new Vector3(0, .15f, 0), new Vector3(2.2f, .14f, 4.6f), "6d5139");
        Art.Block(wreck, new Vector3(-1, .4f, -.4f), new Vector3(.12f, .5f, 3.4f), "5e4530").RotationDegrees = new(
            0,
            0,
            -18
        );
        Art.Block(wreck, new Vector3(.9f, .25f, 1.2f), new Vector3(.12f, .3f, 1.6f), "5e4530").RotationDegrees = new(
            0,
            8,
            25
        );
        Art.Block(wreck, new Vector3(.3f, .25f, -2.6f), new Vector3(1.6f, .1f, .5f), "6d5139").RotationDegrees = new(
            0,
            30,
            0
        );
    }

    /// <summary>Stage 1: the landing's deck on short piles, with a rail on the side of the border and a mooring post.</summary>
    private void BuildLandingPiles()
    {
        var node = parts[1];
        var center = Site.Landing;
        float floor = float.MinValue;
        for (float a = -3; a <= 3; a += 3)
        for (float b = -3; b <= 3; b += 3)
        {
            var p = Site.Local(a, b);
            floor = Mathf.Max(floor, ground.Height(p.X, p.Z));
        }
        floor += .25f;
        var deck = new Node3D
        {
            Name = "LandingDeck",
            Position = new Vector3(center.X, floor, center.Z),
            Rotation = new Vector3(0, Site.Yaw, 0),
        };
        node.AddChild(deck);
        for (float a = -3; a <= 3; a += 2)
        for (float b = -3; b <= 3; b += 3)
        {
            var p = Site.Local(a, b);
            float h = ground.Height(p.X, p.Z);
            float tall = floor - h + .3f;
            Art.Cylinder(deck, new Vector3(a, -tall / 2 + .05f, b), .14f, .14f, tall, "6b4a2e");
        }
        for (int board = 0; board <= 10; board++)
            Art.Block(
                deck,
                new Vector3(0, .02f, -3 + board * .6f),
                new Vector3(6.6f, .1f, .52f),
                board % 2 == 0 ? "c49a66" : "b88c5a"
            );
        // A rail on the side of the border, open in the middle where the pier begins.
        foreach (float a in new[] { -2.4f, 2.4f })
        {
            Art.Block(deck, new Vector3(a, .6f, 3.2f), new Vector3(1.8f, .1f, .1f), "8a5a35");
            Art.Block(deck, new Vector3(a + Mathf.Sign(a) * .9f, .35f, 3.2f), new Vector3(.12f, .7f, .12f), "6b4a2e");
        }
        Art.Cylinder(deck, new Vector3(2.8f, .45f, -2.6f), .16f, .2f, .9f, "6b4a2e");
        Art.Cylinder(deck, new Vector3(2.8f, .6f, -2.6f), .22f, .22f, .1f, "c9b28a");
    }

    /// <summary>The points of the pier from the landing to the end of it in the river, every three metres, on the flat.</summary>
    private List<Vector3> PierPoints()
    {
        var from = Site.Local(0, 3);
        var to = Site.Berth with { Y = 0 };
        var points = new List<Vector3>();
        float length = new Vector2(to.X - from.X, to.Z - from.Z).Length();
        int count = Mathf.Max(2, Mathf.CeilToInt(length / 3));
        for (int i = 0; i <= count; i++)
            points.Add(from.Lerp(to, i / (float)count) with { Y = 0 });
        return points;
    }

    /// <summary>How high the pier's deck lies at a point: a little over the ground, and over the water at its end.</summary>
    private float DeckHeight(Vector3 p) =>
        Mathf.Max(ground.Height(p.X, p.Z) + .7f, ValleyBorders.BorderRiverLevel(p.Z) + .9f);

    /// <summary>Stage 2: the piles of the pier, two by two all the way down to the river.</summary>
    private void BuildPierPiles()
    {
        var node = parts[2];
        var right = Site.Right;
        foreach (var p in PierPoints())
        {
            float top = DeckHeight(p);
            float foot = Mathf.Min(ground.Height(p.X, p.Z), top - .3f) - .4f;
            foreach (int side in new[] { -1, 1 })
            {
                var q = p + right * side * 1.3f;
                Art.Cylinder(node, new Vector3(q.X, (top + foot) / 2, q.Z), .13f, .13f, top - foot, "6b4a2e");
            }
        }
    }

    /// <summary>Stage 3: the boards of the pier, with a hand rail, stepping down the bank to the water.</summary>
    private void BuildPierDeck()
    {
        var node = parts[3];
        var points = PierPoints();
        for (int i = 0; i + 1 < points.Count; i++)
        {
            var a = points[i] with { Y = DeckHeight(points[i]) };
            var b = points[i + 1] with { Y = DeckHeight(points[i + 1]) };
            var middle = (a + b) / 2;
            var board = Art.Block(node, middle, new Vector3(2.8f, .12f, a.DistanceTo(b) + .05f), "c49a66");
            // Turned along the pier here, as the landing is not yet in the scene for LookAt.
            var along = Basis.LookingAt(b - a, Vector3.Up);
            board.Basis = along * Basis.FromScale(board.Scale);
            foreach (int side in new[] { -1, 1 })
            {
                var offset = Site.Right * side * 1.35f + Vector3.Up * .85f;
                var rail = Art.Block(node, middle + offset, new Vector3(.08f, .08f, a.DistanceTo(b) + .05f), "8a5a35");
                rail.Basis = along * Basis.FromScale(rail.Scale);
                Art.Block(node, a + Site.Right * side * 1.35f + Vector3.Up * .42f, new(.1f, .85f, .1f), "6b4a2e");
            }
        }
    }

    /// <summary>Stages 4 and 5: the punt's flat bottom, then its sides, its seat and the heron's long pole.</summary>
    private void BuildPunt()
    {
        puntBottom = new Node3D { Name = "PuntBottom" };
        Punt.AddChild(puntBottom);
        Art.Block(puntBottom, new Vector3(0, .12f, 0), new Vector3(3.6f, .24f, 8), "a87b4c");
        for (float z = -3.6f; z <= 3.61f; z += .8f)
            Art.Block(puntBottom, new Vector3(0, .25f, z), new Vector3(3.5f, .04f, .06f), "8a6038");
        // The bow and the stern rise a little out of the water.
        Art.Block(puntBottom, new Vector3(0, .3f, 4.2f), new Vector3(3.4f, .2f, .6f), "a87b4c").RotationDegrees = new(
            -18,
            0,
            0
        );
        Art.Block(puntBottom, new Vector3(0, .3f, -4.2f), new Vector3(3.4f, .2f, .6f), "a87b4c").RotationDegrees = new(
            18,
            0,
            0
        );
        puntSides = new Node3D { Name = "PuntSides" };
        Punt.AddChild(puntSides);
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(puntSides, new Vector3(side * 1.85f, .5f, 0), new Vector3(.14f, .6f, 8.2f), "8a5a35");
            Art.Block(puntSides, new Vector3(side * 1.85f, .82f, 0), new Vector3(.22f, .06f, 8.2f), "c9a06a");
        }
        foreach (int end in new[] { -1, 1 })
            Art.Block(puntSides, new Vector3(0, .55f, end * 4.45f), new Vector3(3.6f, .5f, .14f), "8a5a35");
        // A red stripe round the top, and the pole leaning at the stern.
        foreach (int side in new[] { -1, 1 })
            Art.Block(puntSides, new Vector3(side * 1.93f, .62f, 0), new Vector3(.02f, .12f, 8), "c0453b");
        var pole = Art.Cylinder(puntSides, new Vector3(1.2f, 2.2f, -3.4f), .05f, .05f, 5.6f, "d9b886");
        pole.RotationDegrees = new Vector3(-14, 0, -12);
    }
}
