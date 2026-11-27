namespace MathRacer.Buildings;

/// <summary>
/// The castle on the hill (<c>docs/CASTLE_PLAN.md</c>): the sixteen loads that raise it, which of them are stone from the
/// coast quarry or the mountain slope, the parts each brings, the four beacons on the borders, the seven guests of the
/// feast and what everything pays.
/// </summary>
internal static class CastlePlans
{
    /// <summary>Deliveries that finish the castle.</summary>
    public const int Trips = 16;

    /// <summary>Coins paid when the castle is finished.</summary>
    public const int Coins = 60;

    /// <summary>Coins paid when every beacon of the borders burns.</summary>
    public const int BeaconCoins = 30;

    /// <summary>Coins for every guest brought to the feast, and for the full table.</summary>
    public const int GuestCoins = 8;
    public const int FeastCoins = 40;

    /// <summary>The beacons, one beside the homestead of each border (in the order of <c>Border</c>).</summary>
    public const int Beacons = 4;

    /// <summary>
    /// The guests of the feast, in the order of their chairs: the four keepers of the borders (in the order of
    /// <c>Border</c>: sea, mountains, river, forest), the balloonist, the owl and the heron.
    /// </summary>
    public const int Guests = 7;

    public const int BalloonistGuest = 4,
        CartographerGuest = 5,
        BirdwatcherGuest = 6;

    private static readonly HaulLoad[] cargo =
    {
        HaulLoad.Gravel,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Logs,
        HaulLoad.Planks,
        HaulLoad.Bricks,
        HaulLoad.Bricks,
        HaulLoad.Glass,
        HaulLoad.Planks,
        HaulLoad.Glass,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>
    /// Whether a load of stone is broken at the coast quarry (the first and the third) rather than on the mountain
    /// slope (the second and the fourth), so that the trailer goes once to the sea and once to the mountains.
    /// </summary>
    public static bool StoneFromSea(int step) => step is 1 or 3;

    /// <summary>The part of the castle that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "základy",
        "kamenný sokl",
        "první kus hradeb",
        "druhý kus hradeb",
        "cimbuří na hradbách",
        "první věž",
        "druhou věž",
        "třetí věž",
        "čtvrtou věž",
        "bránu",
        "padací most",
        "přízemí paláce",
        "patro paláce a velkou věž",
        "okna a střechy",
        "praporky a stůl na nádvoří",
        "pochodně",
    };
}
