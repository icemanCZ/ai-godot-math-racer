using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Buildings;

/// <summary>The zoo, which is raised with six deliveries of goods.</summary>
public sealed partial class Zoo : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Zoo;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Helper;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => ZooPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => Drop;

    Vector3 ITripTarget.DropRoadFor(int step) => Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishZooDump(trip);
}
