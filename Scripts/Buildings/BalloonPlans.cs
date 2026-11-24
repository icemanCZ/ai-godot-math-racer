namespace MathRacer.Buildings;

/// <summary>
/// What the balloon flights pay (<c>docs/BALLOON_PLAN.md</c>): a coin for every star caught, a bonus for a flight that
/// catches all of them, a few coins for the very first flight, and the rooster Kokrháček once enough stars are caught.
/// </summary>
internal static class BalloonPlans
{
    /// <summary>The coins for every star caught.</summary>
    public const int CoinsPerStar = 1;

    /// <summary>The bonus for a flight that catches every star.</summary>
    public const int AllStarsCoins = 5;

    /// <summary>The coins of the very first flight.</summary>
    public const int FirstFlightCoins = 10;

    /// <summary>The stars caught in all that bring the rooster, the balloonist's pet.</summary>
    public const int RoosterStars = 20;

    /// <summary>The most flights a save may count.</summary>
    public const int FlightLimit = 100000;
}
