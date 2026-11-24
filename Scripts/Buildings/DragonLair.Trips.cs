using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// What the dragon's nest needs (<c>docs/DRAGON_PLAN.md</c>, phase 2): two loads of sand for the bed, two of stones for
/// the ring that keeps the warmth in and two of logs for the fire, all from the mountain slope beside the road to the
/// mountains. Then comes the riddle (phase 3) and the flight (phase 4).
/// </summary>
internal static class DragonPlans
{
    /// <summary>Deliveries that finish the nest.</summary>
    public const int Trips = 6;

    /// <summary>The questions of the dragon's riddle, after the nest is finished.</summary>
    public const int Riddles = 3;

    /// <summary>Coins paid when the egg hatches.</summary>
    public const int Coins = 40;

    /// <summary>Coins for the first flight on the dragon's back.</summary>
    public const int FlightCoins = 10;

    private static readonly HaulLoad[] cargo =
    {
        HaulLoad.Sand,
        HaulLoad.Sand,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Logs,
        HaulLoad.Logs,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>The part of the nest that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "hromadu písku",
        "měkké hnízdo",
        "první kameny",
        "kamenný kruh",
        "polena na ohniště",
        "hranici dřeva",
    };
}

/// <summary>The dragon's nest, raised with six deliveries from the mountain slope; the dragon sends the trailer.</summary>
public partial class DragonLair : ITripTarget
{
    /// <summary>The dragon to talk to, who sends the trailer and asks the loading question from afar.</summary>
    internal Character Dragon { get; set; } = null!;

    /// <summary>Where everything is loaded, beside the road to the mountains; null in a valley without it.</summary>
    internal MountainSlopeSite? Slope { get; set; }

    string ITripTarget.Kind => TripKinds.Dragon;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Dragon;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => DragonPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => Site.Drop;

    Vector3 ITripTarget.DropRoadFor(int step) => Site.Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {DragonPlans.Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    /// <summary>Everything is loaded at the mountain slope, not at the sources of the old valley.</summary>
    HaulSource? ITripTarget.SourceFor(int step) =>
        Slope is { } slope ? new HaulSource(DragonPlans.CargoOf(step), slope.Position, slope.Road) : null;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishDragonDump(trip);
}
