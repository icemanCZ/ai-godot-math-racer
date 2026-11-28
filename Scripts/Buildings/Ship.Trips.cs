using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The ship, raised with fourteen deliveries on the slipway (<c>docs/SHIP_PLAN.md</c>): the captain sends the trailer
/// and thanks the driver, each load is tipped out on the edge of the shipyard, and the anchor is loaded at the forge of
/// the great line, whose place has no worker, so the captain asks the loading question from afar.
/// </summary>
public sealed partial class Ship : ITripTarget
{
    /// <summary>Where the shipyard is; set when the valley raises it.</summary>
    internal ShipSite Site { get; set; } = null!;

    internal Terrain Ground { get; set; } = null!;

    /// <summary>The forge of the great line, which forges the anchor, or null in a valley without it.</summary>
    internal LineForge? Forge { get; set; }

    string ITripTarget.Kind => TripKinds.Ship;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Captain;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => ShipPlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => Ground.At(Site.Drop.X, Site.Drop.Z);

    Vector3 ITripTarget.DropRoadFor(int step) => Site.Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {ShipPlans.Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    /// <summary>The anchor is forged at the forge and loaded there; everything else comes from the sources of its material.</summary>
    HaulSource? ITripTarget.SourceFor(int step) =>
        ShipPlans.CargoOf(step) == HaulLoad.Anchor && Forge != null
            ? new HaulSource(HaulLoad.Anchor, Forge.Drop, Forge.Site.Road)
            : null;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishShipDump(trip);
}

/// <summary>
/// A load of stone for the forge of the great line: it is loaded at a quarry like any stone, tipped out before the forge,
/// and the forge makes the ship's anchor of it.
/// </summary>
internal sealed class ShipForgeTrip : ITripTarget
{
    private readonly Ship ship;

    internal ShipForgeTrip(Ship ship) => this.ship = ship;

    public string Kind => TripKinds.ShipForge;
    public int Index => 0;
    public Character Contractor => ship.Captain;
    public int NextStep => 0;

    public HaulLoad LoadFor(int step) => HaulLoad.Stones;

    public Vector3 DropFor(int step) => ship.Forge!.Drop;

    public Vector3 DropRoadFor(int step) => ship.Forge!.Site.Road;

    public string CounterFor(int step) => "";

    public bool DropsAtContractor(int step) => false;

    public void FinishDump(Game game, HaulTrip trip) => game.FinishShipForgeDump(trip);
}
