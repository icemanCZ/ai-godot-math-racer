namespace MathRacer.Buildings;

/// <summary>
/// What the little railway round the playground needs: six deliveries of goods from the works, each of which adds
/// something to the track or the train. Like the playground it only ever wants goods, never a raw load.
/// </summary>
internal static class RailwayPlans
{
    /// <summary>Deliveries that finish the railway.</summary>
    public const int Trips = 6;

    /// <summary>Coins paid when the railway is finished.</summary>
    public const int Coins = 25;

    /// <summary>The delivery after which the engine first sets off (the third: the station is built).</summary>
    public const int EngineAfter = 3;

    /// <summary>The delivery that brings the first and the second wagon.</summary>
    public const int FirstWagonAfter = 4,
        SecondWagonAfter = 6;

    private static readonly HaulLoad[] cargo =
    {
        // The bed of gravel, the sleepers and rails, the station with its platform, the bench and the first wagon,
        // the tunnel and the second wagon with the flags.
        HaulLoad.Gravel,
        HaulLoad.Planks,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Bricks,
        HaulLoad.Planks,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>What each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "štěrkový násyp",
        "koleje",
        "nádražíčko a lokomotivu",
        "první vagónek",
        "tunel",
        "druhý vagónek",
    };
}
