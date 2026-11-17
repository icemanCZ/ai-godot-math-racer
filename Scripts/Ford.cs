using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer;

/// <summary>Material carried in the trailer. Values are persisted.</summary>
public enum HaulLoad
{
    Stones = 0,
    Soil = 1,
    Sand = 2,
}

/// <summary>
/// Seeded plan for the beaver's bridge (still called the ford in code and saves): a straight crossing
/// along X at <see cref="Z"/>, like the main bridge.
/// <see cref="Side"/> is +1 when the far bank lies at larger X. The dumps are dry unloading spots on
/// each bank and the road points start their cleared off-road approaches.
/// </summary>
internal sealed record FordSite(
    float Z,
    float CenterX,
    float Side,
    float HalfLength,
    float DeckHeight,
    Vector3 NearDump,
    Vector3 FarDump,
    Vector3 NearRoad,
    Vector3 FarRoad,
    Vector3 BeaverSpot
)
{
    public Vector3 NearEnd => new(CenterX - Side * HalfLength, DeckHeight, Z);
    public Vector3 FarEnd => new(CenterX + Side * HalfLength, DeckHeight, Z);

    public bool OnFarBank(Vector3 point) => (point.X - River.CenterX(point.Z)) * Side > 0;
}

/// <summary>Where one load is picked up: a quarry, a clay pit or a sand pit beside a road.</summary>
internal readonly record struct HaulSource(HaulLoad Load, Vector3 Position, Vector3 Road);

/// <summary>
/// The beaver's bridge. Stones raise the piers and abutments, soil the ramps and sand finishes them,
/// and the beaver adds the wooden beams, planks and railings. The last load opens the crossing.
/// </summary>
public partial class Ford : Node3D
{
    public const int Trips = 6;

    /// <summary>Half of the drivable width across the river.</summary>
    internal const float HalfWidth = 3.5f;

    /// <summary>Bank decorations and trees keep this far from the bridge's centre line.</summary>
    internal const float ClearHalfWidth = 12;

    /// <summary>Distance of the railings from the centre line, just outside the drivable width.</summary>
    private const float RailOffset = 3.8f;

    internal static readonly HaulLoad[] TripLoads =
    {
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Soil,
        HaulLoad.Soil,
        HaulLoad.Sand,
    };

    /// <summary>The second load of soil goes to the far bank, reached over the main bridge.</summary>
    internal static bool DumpsOnFarBank(int trip) => trip == 4;

    internal FordSite Site { get; }
    public int Stage { get; private set; }
    public bool Passable => Stage >= Trips;
    private readonly Terrain ground;
    private readonly Node3D[] stages = new Node3D[Trips];
    private StaticBody3D deck = null!;
    private CollisionShape3D deckShape = null!;
    private readonly List<CollisionShape3D> railings = new();

    internal Ford(FordSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "BeaverFord";
        Position = new Vector3(site.CenterX, 0, site.Z);
    }

    internal Vector3 DumpFor(int trip) => DumpsOnFarBank(trip) ? Site.FarDump : Site.NearDump;

    public bool Contains(float x, float z) =>
        Mathf.Abs(z - Site.Z) < HalfWidth && Mathf.Abs(x - Site.CenterX) < Site.HalfLength;

    /// <summary>The whole footprint with railings and shoulders, where no grass grows through.</summary>
    internal bool Covers(Vector3 p) =>
        Mathf.Abs(p.Z - Site.Z) < RailOffset + .8f && Mathf.Abs(p.X - Site.CenterX) < Site.HalfLength + .5f;

    /// <summary>Raises <paramref name="below"/> to the finished deck where the car can drive on it.</summary>
    public float Surface(float x, float z, float below) =>
        Passable && Contains(x, z) ? Mathf.Max(below, Site.DeckHeight) : below;

    public override void _Ready()
    {
        var random = new Random(Settings.Seed + 2312);
        float level = River.Level(Site.Z);
        float top = Site.DeckHeight;
        float half = Site.HalfLength;
        float side = Site.Side;
        for (int i = 0; i < Trips; i++)
        {
            stages[i] = new Node3D { Name = "BridgeStage" + i, Visible = false };
            AddChild(stages[i]);
        }
        float Bed(float x) => ground.Height(Site.CenterX + x, Site.Z);
        float shore = Mathf.Min(River.HalfWidth(Site.Z) + 1, half - 2);
        float[] piers = { -shore * .5f, 0, shore * .5f };
        string[] greys = { "8f8f8a", "a3a39d", "b4b3ab" };
        // First stones settle on the riverbed around each pier and break the surface.
        foreach (float x in piers)
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.Tau / 7 + (float)random.NextDouble() * .4f;
                var spot = new Vector3(x + Mathf.Cos(angle) * .9f, 0, Mathf.Sin(angle) * 2.9f);
                float size = .55f + (float)random.NextDouble() * .3f;
                spot.Y = Mathf.Max(Bed(spot.X) + size * .2f, level - .05f);
                Art.Ball(stages[0], spot, new(size, size * .6f, size), greys[i % 3]);
            }
        // Stone pillars rise from the rubble and carry capstones just under the beams.
        foreach (float x in piers)
        foreach (float z in new[] { -2.4f, 0, 2.4f })
        {
            float bottom = Bed(x) - .2f;
            float height = top - .42f - bottom;
            Art.Block(stages[1], new(x, bottom + height / 2, z), new(.95f, height, 1.1f), "9d9c95");
            Art.Block(stages[1], new(x, top - .47f, z), new(1.15f, .14f, 1.3f), "b4b3ab");
        }
        // Abutment walls hold both banks, with loose stones along the water.
        foreach (float direction in new[] { -1f, 1f })
        {
            float x = direction * shore;
            float bottom = Bed(x) - .4f;
            float height = Mathf.Max(top - .4f - bottom, .5f);
            Art.Block(stages[2], new(x, bottom + height / 2, 0), new(1.4f, height, 7.6f), "8f8f8a");
            for (int i = 0; i < 6; i++)
            {
                var spot = new Vector3(x - direction * .9f, 0, -4.3f + i * 1.7f);
                spot.Y = Mathf.Max(Bed(spot.X), level - .1f);
                Art.Ball(stages[2], spot, new(.6f, .35f, .55f), greys[(i + 1) % 3]);
            }
        }
        // Soil builds the near ramp, then the far one; the beaver lays the beams on each finished side.
        foreach (int stage in new[] { 3, 4 })
        {
            float direction = stage == 3 ? -side : side;
            float from = shore - .7f;
            float length = half - from + 1;
            Art.Block(
                stages[stage],
                new(direction * (from + length / 2), top - .55f, 0),
                new(length, .8f, 7.4f),
                "8a6142"
            );
            for (int i = 0; i < 4; i++)
                Art.Ball(
                    stages[stage],
                    new(direction * (from + .5f + i * length / 4), top - .2f, (i % 2 == 0 ? 1 : -1) * 3.3f),
                    new(.6f, .25f, .5f),
                    "7b5539"
                );
            foreach (float z in new[] { -2.4f, 0, 2.4f })
                Art.Beam(
                    stages[stage],
                    new(0, top - .3f, z),
                    new(direction * (shore + .4f), top - .3f, z),
                    .26f,
                    "8a6a4a"
                );
        }
        // Sand smooths the ramp shoulders; the beaver lays planks from bank to bank, raises the railings
        // and puts up a sign. The planks reach into both banks, where the approach roads begin.
        foreach (float direction in new[] { -1f, 1f })
        foreach (float z in new[] { -4.4f, 4.4f })
            for (int i = 0; i < 3; i++)
            {
                float x = direction * (shore + .8f + i * (half - shore) / 3);
                var spot = new Vector3(x, Mathf.Max(Bed(x), level) + .05f, z);
                Art.Ball(stages[5], spot, new(1.5f, .35f, 1.1f), i % 2 == 0 ? "e0c88e" : "d6b873");
            }
        int planks = Mathf.CeilToInt(half * 2);
        for (int i = 0; i < planks; i++)
        {
            float x = -planks / 2f + .5f + i;
            Art.Block(stages[5], new(x, top - .1f, 0), new(.95f, .2f, 7.4f), i % 2 == 0 ? "c39770" : "b8895f");
        }
        float rail = Mathf.Max(shore + .5f, half - 2.5f);
        foreach (float z in new[] { -RailOffset, RailOffset })
        {
            int posts = Mathf.Max(3, Mathf.RoundToInt(rail * 2 / 2.5f) + 1);
            for (int i = 0; i < posts; i++)
                Art.Cylinder(
                    stages[5],
                    new(-rail + i * rail * 2 / (posts - 1), top + .6f, z),
                    .13f,
                    .11f,
                    1.4f,
                    "a77a59"
                );
            Art.Beam(stages[5], new(-rail, top + 1.15f, z), new(rail, top + 1.15f, z), .16f, "e2bb8b");
            Art.Beam(stages[5], new(-rail, top + .55f, z), new(rail, top + .55f, z), .12f, "e2bb8b");
        }
        var post = new Vector3(-side * (half + 1.5f), 0, HalfWidth + 1.6f);
        post.Y = ground.Height(Site.CenterX + post.X, Site.Z + post.Z);
        Art.Cylinder(stages[5], post + Vector3.Up * 1.3f, .12f, .12f, 2.6f, "a77a59");
        Art.Sign(stages[5], post + Vector3.Up * 2.9f, "BOBRŮV MOST", 30);
        deck = new StaticBody3D { Name = "BridgeDeck", Position = new Vector3(0, top - .8f, 0) };
        deckShape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(half * 2, 1.6f, HalfWidth * 2) },
            Disabled = true,
        };
        deck.AddChild(deckShape);
        foreach (float z in new[] { -RailOffset, RailOffset })
        {
            var railing = new CollisionShape3D
            {
                Position = new Vector3(0, .8f + .65f, z),
                Shape = new BoxShape3D { Size = new Vector3(rail * 2, 1.3f, .25f) },
                Disabled = true,
            };
            deck.AddChild(railing);
            railings.Add(railing);
        }
        AddChild(deck);
        SetStage(Stage);
    }

    /// <summary>Shows the work for <paramref name="stage"/> delivered loads; all six open the crossing.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Trips);
        if (!IsInsideTree() || deckShape == null)
            return;
        for (int i = 0; i < Trips; i++)
            stages[i].Visible = i < Stage;
        deckShape.Disabled = !Passable;
        foreach (var railing in railings)
            railing.Disabled = !Passable;
    }
}
