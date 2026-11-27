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
    public const string Zoo = "zoo";

    /// <summary>Wheat from the farmer's field to the finished mill, which grinds it into flour for the bakery.</summary>
    public const string Mill = "mill";

    /// <summary>Glasses from the glassworks to the lamp maker on the village square, who blows lamps from them.</summary>
    public const string Glassmaker = "glassmaker";

    /// <summary>Goods from the works for the hunter's hut beside his pen.</summary>
    public const string Hunter = "hunter";

    /// <summary>Stone from the coast quarry and goods from the works for the lighthouse on the shore.</summary>
    public const string Lighthouse = "lighthouse";

    /// <summary>Sand, stone and logs from the mountain slope for the nest of the dragon under the mountains.</summary>
    public const string Dragon = "dragon";

    /// <summary>Logs from the log yard and planks from the sawmill for the heron's landing and punt.</summary>
    public const string Ferry = "ferry";

    public static bool IsKnown(string? kind) =>
        kind
            is Build
                or Supply
                or House
                or Plot
                or Playground
                or Railway
                or Zoo
                or Mill
                or Glassmaker
                or Hunter
                or Lighthouse
                or Dragon
                or Ferry;
}

/// <summary>
/// What a trailer trip is for: a building, a works, a house, a building plot or the lamp maker. The trip takes everything it needs
/// from the target (the load, where it is tipped out, who sends it), and the target decides what the finished
/// delivery does, so a new kind of trip is a new target and nothing else.
/// </summary>
internal interface ITripTarget
{
    /// <summary>One of <see cref="TripKinds"/>.</summary>
    string Kind { get; }

    /// <summary>The index of the target among those of its kind: project, works, house or plot; 0 where there is one.</summary>
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

    /// <summary>
    /// The one place where the load of a step is picked up, for a target that has a source of its own (the lighthouse's
    /// coast quarry); null where the load comes from the sources of its material, as for every other target.
    /// </summary>
    HaulSource? SourceFor(int step) => null;
}

/// <summary>
/// Planks or bricks that a village resident wants for the next improvement of their house, or the glasses and then the
/// bread that a finished house asks for.
/// </summary>
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

/// <summary>
/// Glasses for the lamp maker on the village square: he blows candles and lamps from them, so he takes them again and
/// again, and the glassworks always has someone to make glasses for.
/// </summary>
internal sealed class GlassmakerTripTarget : ITripTarget
{
    private readonly World world;

    public GlassmakerTripTarget(World world) => this.world = world;

    public string Kind => TripKinds.Glassmaker;
    public int Index => 0;
    public Character Contractor => world.Village.Glassmaker;
    public int NextStep => 0;

    public HaulLoad LoadFor(int step) => HaulLoad.Glass;

    public Vector3 DropFor(int step) => world.Village.GlassmakerDrop;

    public Vector3 DropRoadFor(int step) => world.Village.GlassmakerDrop;

    public string CounterFor(int step) => "";

    public bool DropsAtContractor(int step) => true;

    public void FinishDump(Game game, HaulTrip trip) => game.FinishGlassmakerDump(trip);
}
