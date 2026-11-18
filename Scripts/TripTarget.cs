using Godot;

namespace MathRacer;

/// <summary>
/// The names under which a trailer trip is saved. A saved game stores the name and the index of the target, so a
/// name is never renamed or reused for another kind of target.
/// </summary>
internal static class TripKinds
{
    public const string Build = "build";
    public const string Supply = "supply";
    public const string House = "house";
    public const string Plot = "plot";

    public static bool IsKnown(string? kind) => kind is Build or Supply or House or Plot;
}

/// <summary>
/// What a trailer trip is for: a building, a works, a house or a building plot. The trip takes everything it needs
/// from the target (the load, where it is tipped out, who sends it), and the target decides what the finished
/// delivery does, so a new kind of trip is a new target and nothing else.
/// </summary>
internal interface ITripTarget
{
    /// <summary>One of <see cref="TripKinds"/>.</summary>
    string Kind { get; }

    /// <summary>The index of the target among those of its kind: project, works, house or plot.</summary>
    int Index { get; }

    /// <summary>The character who sends the trailer and thanks the driver.</summary>
    Animal Contractor { get; }

    /// <summary>The delivery a trip started now brings: the stage of a building or plot, 0 for the others.</summary>
    int NextStep { get; }

    HaulLoad LoadFor(int step);

    /// <summary>Where the load is tipped out.</summary>
    Vector3 DropFor(int step);

    /// <summary>The road point from which the way to <see cref="DropFor"/> starts.</summary>
    Vector3 DropRoadFor(int step);

    /// <summary>Short text for the HUD, such as the number of the delivery; empty when there is none.</summary>
    string CounterFor(int step);

    /// <summary>Whether the loaded trailer is taken to the contractor (shown in the HUD) rather than elsewhere.</summary>
    bool DropsAtContractor(int step);

    /// <summary>The load has been tipped out: the target is raised, paid or improved.</summary>
    void FinishDump(Game game, HaulTrip trip);
}

/// <summary>Planks or bricks that a village resident wants for the next improvement of their house.</summary>
internal sealed class HouseTripTarget : ITripTarget
{
    private readonly World world;
    private readonly HaulLoad load;

    public int House { get; }

    public HouseTripTarget(World world, int house, HaulLoad load)
    {
        this.world = world;
        this.load = load;
        House = house;
    }

    public string Kind => TripKinds.House;
    public int Index => House;
    public Animal Contractor => world.Village.Residents[House];
    public int NextStep => 0;

    public HaulLoad LoadFor(int step) => load;

    public Vector3 DropFor(int step) => world.Village.HouseDrop(House);

    public Vector3 DropRoadFor(int step) => world.Village.HouseDrop(House);

    public string CounterFor(int step) => "";

    public bool DropsAtContractor(int step) => true;

    public void FinishDump(Game game, HaulTrip trip) => game.FinishHouseDump(trip);
}

// The three kinds of target that are nodes of the world implement ITripTarget explicitly, so that their own members
// keep their names and accessibility.

/// <summary>A building the player raises with ten deliveries: the bridge, the mill, the tower or the jetty.</summary>
public abstract partial class BuildProject : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Build;
    int ITripTarget.Index => Index;
    Animal ITripTarget.Contractor => Builder;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => Steps[step].Cargo;

    Vector3 ITripTarget.DropFor(int step) => DropFor(step);

    Vector3 ITripTarget.DropRoadFor(int step) => DropRoadFor(step);

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {Trips}";

    bool ITripTarget.DropsAtContractor(int step) => !Steps[step].FarBank;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishBuildDump(trip);
}

/// <summary>One of the six building plots, which are raised with six deliveries of goods.</summary>
public sealed partial class HousePlot : ITripTarget
{
    string ITripTarget.Kind => TripKinds.Plot;
    int ITripTarget.Index => Index;
    Animal ITripTarget.Contractor => Resident;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => PlotPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => Drop;

    Vector3 ITripTarget.DropRoadFor(int step) => Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishPlotDump(trip);
}

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
