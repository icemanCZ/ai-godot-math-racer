using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Buildings;

/// <summary>A building the player raises with ten deliveries: the bridge, the mill, the tower or the jetty.</summary>
public abstract partial class BuildProject : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Build;
    int ITripTarget.Index => Index;
    Character ITripTarget.Contractor => Builder;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => Steps[step].Cargo;

    Vector3 ITripTarget.DropFor(int step) => DropFor(step);

    Vector3 ITripTarget.DropRoadFor(int step) => DropRoadFor(step);

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {Trips}";

    bool ITripTarget.DropsAtContractor(int step) => !Steps[step].FarBank;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishBuildDump(trip);
}
