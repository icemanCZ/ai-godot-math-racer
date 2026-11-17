using System;
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
/// Seeded plan for the beaver's ford: a straight crossing along X at <see cref="Z"/>, like the bridge.
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

/// <summary>The stone ford. Each delivered load adds visible work; the last one opens the crossing.</summary>
public partial class Ford : Node3D
{
    public const int Trips = 6;

    /// <summary>Half of the drivable width across the river.</summary>
    internal const float HalfWidth = 3.5f;

    /// <summary>Bank decorations and trees keep this far from the ford's centre line.</summary>
    internal const float ClearHalfWidth = 12;

    internal static readonly HaulLoad[] TripLoads =
    {
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Soil,
        HaulLoad.Soil,
        HaulLoad.Sand,
    };

    /// <summary>The second load of soil goes to the far bank, reached over the bridge.</summary>
    internal static bool DumpsOnFarBank(int trip) => trip == 4;

    internal FordSite Site { get; }
    public int Stage { get; private set; }
    public bool Passable => Stage >= Trips;
    private readonly Terrain ground;
    private readonly Node3D[] stages = new Node3D[Trips];
    private StaticBody3D deck = null!;
    private CollisionShape3D deckShape = null!;

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

    /// <summary>Raises <paramref name="below"/> to the finished deck where the car can drive on it.</summary>
    public float Surface(float x, float z, float below) =>
        Passable && Contains(x, z) ? Mathf.Max(below, Site.DeckHeight) : below;

    public override void _Ready()
    {
        var random = new Random(Settings.Seed + 2312);
        float level = River.Level(Site.Z);
        float deckTop = Site.DeckHeight;
        float half = Site.HalfLength;
        float side = Site.Side;
        for (int i = 0; i < Trips; i++)
        {
            stages[i] = new Node3D { Name = "FordStage" + i, Visible = false };
            AddChild(stages[i]);
        }
        float Wet(float x) => ground.Height(Site.CenterX + x, Site.Z) - .05f;
        // Stones first settle on the riverbed, then reach the surface and form stepping stones.
        for (int i = 0; i < 9; i++)
        {
            float x = (float)(random.NextDouble() * 2 - 1) * (half - 2);
            float z = (float)(random.NextDouble() * 2 - 1) * (HalfWidth - .6f);
            float size = .45f + (float)random.NextDouble() * .3f;
            Art.Ball(stages[0], new(x, Mathf.Max(Wet(x), level - .75f), z), new(size, size * .6f, size), "8f8f8a");
        }
        for (int i = 0; i < 12; i++)
        {
            float x = (float)(random.NextDouble() * 2 - 1) * (half - 1.5f);
            float z = (float)(random.NextDouble() * 2 - 1) * (HalfWidth - .5f);
            float size = .55f + (float)random.NextDouble() * .3f;
            Art.Ball(stages[1], new(x, Mathf.Max(Wet(x), level - .4f), z), new(size, size * .55f, size), "a3a39d");
        }
        int stepping = Mathf.Max(4, Mathf.RoundToInt(half * 2 / 1.6f));
        for (int i = 0; i < stepping; i++)
        {
            float x = -half + .8f + i * (half * 2 - 1.6f) / (stepping - 1);
            foreach (float z in new[] { -1.6f, 1.6f })
            {
                float jitter = (float)(random.NextDouble() - .5) * .5f;
                Art.Ball(stages[2], new(x + jitter, level + .02f, z + jitter), new(.75f, .22f, .7f), "b4b3ab");
            }
        }
        // Soil fills the near half, then the far half, between the stones.
        foreach (int stage in new[] { 3, 4 })
        {
            float direction = stage == 3 ? -side : side;
            Art.Block(stages[stage], new(direction * half / 2, deckTop - .17f, 0), new(half, .3f, 6.6f), "8a6142");
            for (int i = 0; i < 5; i++)
            {
                float x = direction * (.6f + i * (half - 1.2f) / 4);
                Art.Ball(
                    stages[stage],
                    new(x, deckTop - .02f, (i % 2 == 0 ? 1 : -1) * 2.2f),
                    new(.5f, .18f, .45f),
                    "7b5539"
                );
            }
        }
        // Sand finishes the surface. Kerb stones mark both edges and a sign greets the driver.
        Art.Block(stages[5], new(0, deckTop - .05f, 0), new(half * 2, .1f, 6.8f), "e0c88e");
        int kerbs = Mathf.RoundToInt(half * 2 / 1.4f);
        for (int i = 0; i <= kerbs; i++)
        {
            float x = -half + i * half * 2 / kerbs;
            foreach (float z in new[] { -HalfWidth, HalfWidth })
                Art.Ball(stages[5], new(x, deckTop + .05f, z), new(.42f, .22f, .38f), "9d9c95");
        }
        var post = new Vector3(-side * (half + 1.5f), 0, HalfWidth + 1.2f);
        post.Y = ground.Height(Site.CenterX + post.X, Site.Z + post.Z);
        Art.Cylinder(stages[5], post + Vector3.Up * 1.3f, .12f, .12f, 2.6f, "a77a59");
        Art.Sign(stages[5], post + Vector3.Up * 2.9f, "BOBRŮV BROD", 30);
        deck = new StaticBody3D { Name = "FordDeck", Position = new Vector3(0, deckTop - .8f, 0) };
        deckShape = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(half * 2, 1.6f, HalfWidth * 2) },
            Disabled = true,
        };
        deck.AddChild(deckShape);
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
    }
}
