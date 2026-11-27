using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The castle, raised with sixteen deliveries; the princess sends the trailer, thanks the driver and asks the loading
/// question of the stone from afar, since the coast quarry and the mountain slope have no worker of their own.
/// </summary>
public partial class Castle : ITripTarget
{
    /// <summary>The princess at the gate, who sends the trailer.</summary>
    internal Character Princess { get; set; } = null!;

    /// <summary>Where the first and third loads of stone are broken, beside the road to the sea; null without it.</summary>
    internal CoastQuarrySite? Quarry { get; set; }

    /// <summary>Where the second and fourth loads of stone are broken, beside the road to the mountains; null without it.</summary>
    internal MountainSlopeSite? Slope { get; set; }

    string ITripTarget.Kind => TripKinds.Castle;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Princess;
    int ITripTarget.NextStep => Stage;

    HaulLoad ITripTarget.LoadFor(int step) => CastlePlans.CargoOf(step);

    Vector3 ITripTarget.DropFor(int step) => ground.At(Site.Drop.X, Site.Drop.Z);

    Vector3 ITripTarget.DropRoadFor(int step) => Site.Road;

    string ITripTarget.CounterFor(int step) => $"{step + 1} / {CastlePlans.Trips}";

    bool ITripTarget.DropsAtContractor(int step) => true;

    /// <summary>The stone is broken once at the sea and once in the mountains, or wherever of the two there is.</summary>
    HaulSource? ITripTarget.SourceFor(int step) => StoneSource(step);

    internal HaulSource? StoneSource(int step)
    {
        if (CastlePlans.CargoOf(step) != HaulLoad.Stones)
            return null;
        HaulSource? sea = Quarry is { } quarry ? new HaulSource(HaulLoad.Stones, quarry.Position, quarry.Road) : null;
        HaulSource? mountains = Slope is { } slope ? new HaulSource(HaulLoad.Stones, slope.Position, slope.Road) : null;
        return CastlePlans.StoneFromSea(step) ? sea ?? mountains : mountains ?? sea;
    }

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishCastleDump(trip);
}

/// <summary>
/// A guest of the castle's feast: the trailer goes empty to the guest's home, where the guest asks a question before
/// getting on its bench, and takes the guest to the ring before the gate, where the guest goes to a chair at the table.
/// </summary>
internal sealed class CastleGuestTrip : ITripTarget
{
    private readonly Castle castle;
    private readonly Vector3 road;

    /// <summary>The number of the guest and of the chair, 0 to <see cref="CastlePlans.Guests"/> - 1.</summary>
    internal int Guest { get; }

    /// <summary>The character invited, who stays at home; a figure of them rides and sits at the table.</summary>
    internal Character Character { get; }

    internal CastleGuestTrip(Castle castle, int guest, Character character, Vector3 road)
    {
        this.castle = castle;
        this.road = road;
        Guest = guest;
        Character = character;
    }

    public string Kind => TripKinds.CastleGuest;
    public int Index => Guest;
    public Character Contractor => castle.Princess;
    public int NextStep => 0;

    public HaulLoad LoadFor(int step) => HaulLoad.Guest;

    public Vector3 DropFor(int step) => castle.Site.Drop with { Y = castle.Position.Y };

    public Vector3 DropRoadFor(int step) => castle.Site.Road;

    public string CounterFor(int step) => "";

    public bool DropsAtContractor(int step) => true;

    public HaulSource? SourceFor(int step) => new HaulSource(HaulLoad.Guest, Character.Position, road);

    /// <summary>The guest asks the question at home before getting on, not the princess.</summary>
    public Character? AskerFor(int step) => Character;

    public void FinishDump(Game game, HaulTrip trip) => game.FinishGuestDump(trip);
}
