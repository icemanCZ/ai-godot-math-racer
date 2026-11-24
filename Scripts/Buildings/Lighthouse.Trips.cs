using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// What the lighthouse on the shore needs (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 3): five loads of stone from the
/// coast quarry beside the road to the sea, then planks for the stairs and the gallery, bricks for the keeper's lodge and
/// glasses for the lantern from the works of the old valley. A valley without those works has the lighthouse standing.
/// </summary>
internal static class LighthousePlans
{
    /// <summary>Deliveries that finish the lighthouse, one for each of its stages.</summary>
    public const int Trips = Lighthouse.Stages;

    /// <summary>The loads of stone, which come first, from the coast quarry.</summary>
    public const int StoneTrips = 5;

    /// <summary>Coins paid when the lighthouse is finished.</summary>
    public const int Coins = 30;

    private static readonly HaulLoad[] cargo =
    {
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Stones,
        HaulLoad.Planks,
        HaulLoad.Bricks,
        HaulLoad.Glass,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>The part of the lighthouse that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "základy na skále",
        "kamennou podezdívku",
        "první patro",
        "druhé patro",
        "třetí patro",
        "schody a ochoz",
        "strážní domek",
        "lucernu",
    };
}

/// <summary>The lighthouse, raised with eight deliveries; its keeper sends the trailer and thanks the driver.</summary>
public partial class Lighthouse : ITripTarget
{
    /// <summary>The keeper of the sea border, who sends the trailer.</summary>
    internal Character Keeper { get; set; } = null!;

    /// <summary>Where the stone is loaded, beside the road to the sea; null in a valley without it.</summary>
    internal CoastQuarrySite? Quarry { get; set; }

    string ITripTarget.Kind => TripKinds.Lighthouse;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Keeper;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => LighthousePlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => Site.Drop;

    Vector3 ITripTarget.DropRoadFor(int step) => Site.Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {LighthousePlans.Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    /// <summary>The stone is broken at the coast quarry, not at the quarry of the old valley.</summary>
    HaulSource? ITripTarget.SourceFor(int step) =>
        step < LighthousePlans.StoneTrips && Quarry is { } quarry
            ? new HaulSource(HaulLoad.Stones, quarry.Position, quarry.Road)
            : null;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishLighthouseDump(trip);
}
