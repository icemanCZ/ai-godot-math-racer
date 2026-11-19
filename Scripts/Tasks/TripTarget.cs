using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Tasks;

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
    public const string Playground = "playground";
    public const string Railway = "railway";

    public static bool IsKnown(string? kind) => kind is Build or Supply or House or Plot or Playground or Railway;
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
    Character Contractor { get; }

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
    public Character Contractor => world.Village.Residents[House];
    public int NextStep => 0;

    public HaulLoad LoadFor(int step) => load;

    public Vector3 DropFor(int step) => world.Village.HouseDrop(House);

    public Vector3 DropRoadFor(int step) => world.Village.HouseDrop(House);

    public string CounterFor(int step) => "";

    public bool DropsAtContractor(int step) => true;

    public void FinishDump(Game game, HaulTrip trip) => game.FinishHouseDump(trip);
}
