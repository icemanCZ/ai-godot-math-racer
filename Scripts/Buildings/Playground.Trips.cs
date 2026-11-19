using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Buildings;

/// <summary>The playground, which is raised with six deliveries of goods.</summary>
public sealed partial class Playground : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Playground;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Helper;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => PlaygroundPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => Drop;

    Vector3 ITripTarget.DropRoadFor(int step) => Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishPlaygroundDump(trip);
}
