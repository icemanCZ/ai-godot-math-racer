namespace MathRacer.Buildings;

/// <summary>
/// The shells of the beach (<c>docs/SHELLS_PLAN.md</c>): their eight kinds, how often each is washed up, how many lie on
/// the beach after a high tide and what they pay.
/// </summary>
internal static class ShellPlans
{
    public const int Kinds = 8;

    /// <summary>How many shells every high tide leaves on the wet sand of the cove.</summary>
    public const int PerTide = 10;

    /// <summary>A coin for every shell, more for the first of its kind and a bonus once every kind is found.</summary>
    public const int CoinsPerShell = 1;
    public const int NewKindCoins = 4;
    public const int AllKindsCoins = 25;

    /// <summary>More shells of a kind than anyone picks up: a save with more is not one the game wrote.</summary>
    public const int MostOfAKind = 1_000_000;

    /// <summary>How near the car comes to a shell to pick it up.</summary>
    public const float PickReach = 3.6f;

    public const int Scallop = 0,
        Cockle = 1,
        Mussel = 2,
        Whelk = 3,
        Cowrie = 4,
        Starfish = 5,
        Urchin = 6,
        Pearl = 7;

    private static readonly string[] Names =
    {
        "Hřebenatka",
        "Srdcovka",
        "Slávka",
        "Ulita",
        "Zavinutec",
        "Hvězdice",
        "Ježovka",
        "Perlorodka",
    };

    /// <summary>How often each kind is washed up, out of the sum of them: the pearl oyster is the rarest.</summary>
    internal static readonly int[] Weights = { 22, 20, 20, 14, 10, 7, 5, 2 };

    public static string Name(int kind) => Names[kind];
}
