using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Buildings;

/// <summary>The sawmill, the brickworks or the gravel works, which take in raw material.</summary>
public partial class Works : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Supply;
    int ITripTarget.Index => Site.Index;
    Animal ITripTarget.Contractor => Worker;
    int ITripTarget.NextStep => 0;

    HaulLoad ITripTarget.LoadFor(int step) => Spec.Input;

    Vector3 ITripTarget.DropFor(int step) => Site.Position;

    Vector3 ITripTarget.DropRoadFor(int step) => Site.Road;

    string ITripTarget.CounterFor(int step) => "";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishSupplyDump(trip);
}
