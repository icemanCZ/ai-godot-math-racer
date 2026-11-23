using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Valley;

/// <summary>
/// What the hunter's hut needs before he sends anyone hunting: four deliveries of goods from the works, the gravel of the
/// foundation, the planks of the walls and of the roof and the bricks of the chimney. A valley without the works has the
/// hut standing from the start.
/// </summary>
internal static class HutPlans
{
    /// <summary>Deliveries that finish the hut.</summary>
    public const int Trips = 4;

    /// <summary>Coins paid when the hut is finished.</summary>
    public const int Coins = 10;

    private static readonly HaulLoad[] cargo = { HaulLoad.Gravel, HaulLoad.Planks, HaulLoad.Planks, HaulLoad.Bricks };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>The part of the hut that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts = { "základy", "stěny", "střechu", "komín" };
}

/// <summary>The hunter's hut beside his pen, which is raised with four deliveries of goods.</summary>
internal sealed partial class HunterPen : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Hunter;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Hunter;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => HutPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => site.Drop;

    Vector3 ITripTarget.DropRoadFor(int step) => site.Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {HutPlans.Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishHutDump(trip);
}
