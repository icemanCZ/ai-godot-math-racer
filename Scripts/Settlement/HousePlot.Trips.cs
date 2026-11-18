using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Settlement;

/// <summary>One of the six building plots, which are raised with six deliveries of goods.</summary>
public sealed partial class HousePlot : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Plot;
    int ITripTarget.Index => Index;
    Character ITripTarget.Contractor => Resident;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => PlotPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => Drop;

    Vector3 ITripTarget.DropRoadFor(int step) => Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishPlotDump(trip);
}
