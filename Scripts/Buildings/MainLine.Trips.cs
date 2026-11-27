using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Buildings;

/// <summary>
/// The great line, raised with seventeen deliveries: the engine driver sends the trailer and thanks the driver, each
/// load is tipped out where the track laid so far ends, and the rails are loaded at the forge, whose place has no worker,
/// so the engine driver asks the loading question from afar.
/// </summary>
public sealed partial class MainLine : ITripTarget
{
    string ITripTarget.Kind => TripKinds.MainLine;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Engineer;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => MainLinePlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step)
    {
        var front = Front(step);
        return ground.At(front.X, front.Z);
    }

    Vector3 ITripTarget.DropRoadFor(int step) => Front(step);

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {MainLinePlans.Trips}";

    bool ITripTarget.DropsAtContractor(int step) => false;

    /// <summary>The rails are made at the forge and loaded there; everything else comes from the sources of its material.</summary>
    HaulSource? ITripTarget.SourceFor(int step) =>
        MainLinePlans.CargoOf(step) == HaulLoad.Rails
            ? new HaulSource(HaulLoad.Rails, Forge.Drop, Forge.Site.Road)
            : null;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishMainLineDump(trip);
}

/// <summary>
/// A load of stone for the forge: it is loaded at a quarry like any stone, tipped out before the forge, and the forge makes
/// rails of it.
/// </summary>
internal sealed class LineForgeTrip : ITripTarget
{
    private readonly MainLine line;

    internal LineForgeTrip(MainLine line) => this.line = line;

    public string Kind => TripKinds.MainLineForge;
    public int Index => 0;
    public Character Contractor => line.Engineer;
    public int NextStep => 0;

    public HaulLoad LoadFor(int step) => HaulLoad.Stones;

    public Vector3 DropFor(int step) => line.Forge.Drop;

    public Vector3 DropRoadFor(int step) => line.Forge.Site.Road;

    public string CounterFor(int step) => "";

    public bool DropsAtContractor(int step) => false;

    public void FinishDump(Game game, HaulTrip trip) => game.FinishForgeDump(trip);
}
