namespace MathRacer.Buildings;

/// <summary>
/// The ferry to the bird island (<c>docs/FERRY_PLAN.md</c>): the five loads that build the landing and the punt, the eight
/// kinds of bird of the atlas, how often each comes, how many come on every crossing, how the telescope sees them and
/// what they pay.
/// </summary>
internal static class FerryPlans
{
    /// <summary>Deliveries that build the landing, its pier and the punt.</summary>
    public const int Trips = 5;

    /// <summary>Coins paid when the punt is finished.</summary>
    public const int Coins = 25;

    private static readonly HaulLoad[] cargo =
    {
        HaulLoad.Logs,
        HaulLoad.Logs,
        HaulLoad.Planks,
        HaulLoad.Planks,
        HaulLoad.Planks,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>The part that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "kůly přístaviště",
        "kůly mola",
        "lávku mola",
        "dno pramice",
        "boky a bidlo",
    };

    public const int Kinds = 8;

    /// <summary>How many birds hide in the reeds round the island on every crossing, each of a kind of its own.</summary>
    public const int PerVisit = 4;

    /// <summary>The clumps of reeds round the island that the birds of a crossing hide in.</summary>
    public const int Spots = 12;

    /// <summary>Coins for every bird seen, more for the first of its kind and a bonus for the full atlas.</summary>
    public const int CoinsPerBird = 2;
    public const int NewKindCoins = 5;
    public const int AtlasCoins = 25;

    /// <summary>More birds of a kind than anyone sees: a save with more is not one the game wrote.</summary>
    public const int MostOfAKind = 1_000_000;

    /// <summary>More crossings than anyone makes.</summary>
    public const int MostVisits = 1_000_000;

    /// <summary>A bird is seen once it stays this near the middle of the telescope, in degrees, for this long.</summary>
    public const float SpotAngle = 4.5f;
    public const float SpotSeconds = .6f;

    /// <summary>How wide the telescope sees, in degrees from top to bottom.</summary>
    public const float TelescopeFov = 24;

    public const int Swan = 0,
        Stork = 1,
        Cormorant = 2,
        Tern = 3,
        Plover = 4,
        Grebe = 5,
        Pelican = 6,
        Flamingo = 7;

    private static readonly string[] Names =
    {
        "Labuť",
        "Čáp",
        "Kormorán",
        "Rybák",
        "Kulík",
        "Potápka",
        "Pelikán",
        "Plameňák",
    };

    /// <summary>How often each kind comes, out of the sum of them: the pelican and the flamingo are rare guests.</summary>
    internal static readonly int[] Weights = { 22, 18, 16, 14, 12, 9, 6, 3 };

    public static string Name(int kind) => Names[kind];

    /// <summary>The swan and the grebe swim on the water; the others stand in the reeds.</summary>
    public static bool Swims(int kind) => kind is Swan or Grebe;
}
