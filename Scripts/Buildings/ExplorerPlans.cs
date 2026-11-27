using Godot;

namespace MathRacer.Buildings;

/// <summary>
/// The owl's map of the valley (<c>docs/EXPLORER_PLAN.md</c>): the eight wonders of the land beyond the old valley, one of
/// each kind, what the map pays for each wonder written into it and for the whole map, and the little owl Houkalka once
/// all eight are on it. A save holds the numbers of the wonders, which are their kinds, so the order never changes.
/// </summary>
internal static class ExplorerPlans
{
    /// <summary>How many wonders the land beyond the old valley has, one in every eighth of it.</summary>
    public const int Landmarks = 8;

    /// <summary>The coins for every wonder written into the map.</summary>
    public const int CoinsPerLandmark = 6;

    /// <summary>The bonus once every wonder is on the map.</summary>
    public const int MapCoins = 30;

    /// <summary>The car spots a wonder once it comes this much nearer than the wonder is wide.</summary>
    public const float SpotReach = 15;

    public const int Oak = 0,
        Arch = 1,
        Ruin = 2,
        Mushroom = 3,
        Stones = 4,
        Dinosaur = 5,
        Well = 6,
        Scarecrow = 7;

    /// <summary>The names of the wonders, in their order; a save holds their numbers.</summary>
    public static readonly string[] Names =
    {
        "Starý dub",
        "Skalní brána",
        "Zřícenina hradu",
        "Obří muchomůrka",
        "Kamenný kruh",
        "Kostra dinosaura",
        "Studánka",
        "Strašák v poli",
    };

    public static string Name(int landmark) => Names[Mathf.Clamp(landmark, 0, Names.Length - 1)];

    /// <summary>
    /// How far round its middle a wonder takes ground, which no plant grows on and which keeps clear of everything else
    /// in the valley.
    /// </summary>
    public static float Radius(int landmark) =>
        landmark switch
        {
            Oak => 8,
            Arch => 8,
            Ruin => 9,
            Mushroom => 6,
            Stones => 8,
            Dinosaur => 9,
            Well => 5,
            _ => 6,
        };
}
