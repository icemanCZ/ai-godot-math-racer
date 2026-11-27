using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Buildings;

/// <summary>The landing of the ferry, built with five deliveries; the heron sends the trailer and thanks the driver.</summary>
public partial class FerryLanding : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Ferry;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Heron;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => FerryPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => ground.At(Site.Drop.X, Site.Drop.Z);

    Vector3 ITripTarget.DropRoadFor(int step) => Site.Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {FerryPlans.Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishFerryDump(trip);
}
