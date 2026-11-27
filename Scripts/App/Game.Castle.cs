using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Ui;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The castle on the hill (<c>docs/CASTLE_PLAN.md</c>). Lvice kněžna Libuše waits at the gate of a castle still to be
/// built on the highest ground beside a road beyond the old valley. Her panel (state <see cref="GameState.CastleStatus"/>)
/// first raises the castle with sixteen trailer trips: stone from the sea and from the mountains, whose loading question
/// she asks from afar, gravel, bricks, planks, logs and glass from the works. Then a load of logs to each homestead of
/// the borders puts up a beacon there whose fire burns day and night, and once all burn the great fire on the keep is lit.
/// Last, the trailer fetches the guests of the feast one after another from their homes, where each asks a question
/// before getting on the bench; at the castle each one's figure goes to a chair at the table while the guest stays at
/// home. A full table holds the feast: coins, the ghost Bubáček and fireworks over the castle every night. The castle's
/// stage, the beacons, the guests seated and the feast are saved as <c>SaveData.Castle</c>.
/// </summary>
public partial class Game
{
    /// <summary>The guest whose figure rides on the trailer's bench now, or -1.</summary>
    private int castleRider = -1;

    /// <summary>The valley has the castle and its princess.</summary>
    private bool CastleWanted => world.Castle != null && world.Castellan != null;

    /// <summary>The princess shows her mark while there is something to bring and nothing is under way.</summary>
    private bool CastellanAwaits =>
        CastleWanted && Haul == null && Delivery == null && CastlePhase(world.Castle!) != CastleMode.Done;

    private HaulTrip? TripOf(Castle castle) =>
        Haul is { } trip && (trip.Castle == castle || trip.Beacon != null || trip.Guest != null) ? trip : null;

    /// <summary>What the castle waits for: its loads, the beacons' logs, the guests, or nothing more.</summary>
    private CastleMode CastlePhase(Castle castle) =>
        !castle.Complete ? CastleMode.Build
        : NextBeacon() != null ? CastleMode.Beacons
        : NextGuest(castle) >= 0 ? CastleMode.Guests
        : CastleMode.Done;

    /// <summary>The first beacon, in the order of the borders, that still waits for its logs.</summary>
    private BeaconTower? NextBeacon() => world.Beacons.FirstOrDefault(beacon => beacon is { Built: false });

    /// <summary>The first chair whose guest lives in this valley and is not yet at the table, or -1.</summary>
    private int NextGuest(Castle castle)
    {
        for (int chair = 0; chair < CastlePlans.Guests; chair++)
            if (world.CastleGuests[chair] != null && !castle.Seated(chair))
                return chair;
        return -1;
    }

    private int GuestsSeated(Castle castle) => Enumerable.Range(0, CastlePlans.Guests).Count(castle.Seated);

    /// <summary>Stone and logs are always there to load; goods must stand at their works.</summary>
    private bool CastleStepReady(Castle castle)
    {
        if (castle.Complete)
            return false;
        var load = CastlePlans.CargoOf(castle.Stage);
        return !HaulLoads.IsGoods(load) || GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;
    }

    /// <summary>The princess's panel: the castle still to be built, the beacons, or the guests of the feast.</summary>
    internal void OpenCastellan(Character princess)
    {
        if (princess.Role is not CastleRole || !CastleWanted || world.Castle is not { } castle)
            return;
        ClearChallenge();
        EnterInteraction(GameState.CastleStatus, princess);
        ShowCastle();
        string speech = CastlePhase(castle) switch
        {
            _ when Haul != null => "",
            CastleMode.Build => castle.Stage == 0 ? princess.Greeting + "\n" + Texts.CastleWanted
            : !CastleStepReady(castle) ? Texts.ZboziJesteNeniVyrobenePosluTe
            : Texts.PrivezesMiDalsiMaterial,
            CastleMode.Beacons => Texts.CastleBeaconsWanted,
            CastleMode.Guests => GuestsSeated(castle) == 0 ? Texts.CastleBeaconsLit : Texts.CastleGuestsWanted,
            _ => Texts.CastleFeastOver,
        };
        if (speech != "")
            Speak(speech, princess);
    }

    private void ShowCastle()
    {
        if (nearby is not { Role: CastleRole } princess || world.Castle is not { } castle)
            return;
        bool free = Haul == null && Delivery == null;
        var mode = CastlePhase(castle);
        bool ready = mode != CastleMode.Build || CastleStepReady(castle);
        var beacons = Enumerable
            .Range(0, CastlePlans.Beacons)
            .Where(border => world.Beacons[border] != null)
            .Select(border => ((Border)border, world.Beacons[border]!.Built))
            .ToArray();
        var guests = Enumerable
            .Range(0, CastlePlans.Guests)
            .Where(chair => world.CastleGuests[chair] != null)
            .Select(chair => (world.CastleGuests[chair]!, castle.Seated(chair)))
            .ToArray();
        ui.CastlePanel(
            princess,
            mode,
            castle.Stage,
            TripOf(castle),
            free && ready && mode != CastleMode.Done,
            free && !ready && mode == CastleMode.Build ? GoodsWorks(CastlePlans.CargoOf(castle.Stage)) : null,
            beacons,
            guests,
            world.AllPets.FirstOrDefault(pet => pet.Kind == CollectorPetKind.CastleGhost)
        );
    }

    internal void BeginCastleStart() => BeginCastleStart(false);

    /// <summary>
    /// The princess's green check mark: a question, then the trailer for the next load of the castle, the logs of the
    /// next beacon or the next guest of the feast.
    /// </summary>
    private void BeginCastleStart(bool quizzed)
    {
        if (
            State != GameState.CastleStatus
            || nearby is not { Role: CastleRole }
            || world.Castle is not { } castle
            || Haul != null
            || Delivery != null
        )
            return;
        ITripTarget? target = CastlePhase(castle) switch
        {
            CastleMode.Build when CastleStepReady(castle) => castle,
            CastleMode.Beacons => NextBeacon(),
            CastleMode.Guests => CastleGuestTarget(NextGuest(castle)),
            _ => null,
        };
        if (target == null)
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Castle);
            return;
        }
        StartTrip(target);
    }

    /// <summary>
    /// A question asked by the princess: at the sea or in the mountains it was the stone's loading question, asked from
    /// afar; otherwise there is none, so the panel closes.
    /// </summary>
    internal void CompleteCastellanQuiz(Character princess)
    {
        if (Haul is { Loaded: false } trip && trip.Asker == princess && trip.Reached(vehicle.Position))
        {
            CompleteHaulLoading();
            return;
        }
        FinishInteraction();
    }

    /// <summary>The trip of a beacon from a save: its logs are wanted once the castle stands.</summary>
    private BeaconTower? CastleBeaconTarget(int border) =>
        world.Castle is { Complete: true } && border is >= 0 and < CastlePlans.Beacons
            ? world.Beacons[border] is { Built: false } beacon
                ? beacon
                : null
            : null;

    /// <summary>The trip of a guest of the feast, to the guest's home and back: once the castle stands, for a chair still free.</summary>
    private CastleGuestTrip? CastleGuestTarget(int chair) =>
        world.Castle is { Complete: true } castle
        && chair is >= 0 and < CastlePlans.Guests
        && world.CastleGuests[chair] is { } guest
        && !castle.Seated(chair)
            ? new CastleGuestTrip(castle, chair, guest, world.CastleGuestRoads[chair])
            : null;

    /// <summary>The load is tipped out before the gate: the next part of the castle stands.</summary>
    internal void FinishCastleDump(HaulTrip trip)
    {
        var castle = trip.Castle!;
        // The stone of the sea and of the mountains has no worker whose pet it would count for.
        if (trip.Load != HaulLoad.Stones)
            world.Tally.AddRaw(trip.Load);
        castle.SetStage(trip.Step + 1);
        castle.Princess.Celebrate();
        EndHaul();
        if (castle.Complete)
        {
            AddCoins(CastlePlans.Coins);
            foreach (var beacon in world.Beacons)
                beacon?.SetBuilt(beacon.Built, true);
            CelebrateAt(castle.TopFocus, Texts.CastleBuilt(CastlePlans.Coins));
            Speak(Texts.CastleBuiltSpeech, castle.Princess);
        }
        else
        {
            CelebrateAt(castle.Focus, Texts.CastlePart(CastlePlans.Parts[trip.Step], castle.Stage, CastlePlans.Trips));
            Speak(Texts.DekujuAzBudesChtitPrivezDalsi, castle.Princess);
        }
        UpdateDeliveryGuides();
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The logs are tipped out beside the beacon: it stands and burns; once all burn, so does the keep.</summary>
    internal void FinishBeaconDump(HaulTrip trip)
    {
        var beacon = trip.Beacon!;
        world.Tally.AddRaw(trip.Load);
        beacon.SetBuilt(true, true);
        beacon.Princess.Celebrate();
        EndHaul();
        AddCoins(CastlePlans.BeaconCoins);
        int lit = world.Beacons.Count(b => b is { Built: true });
        CelebrateAt(beacon.Fire, Texts.BeaconLit(lit, world.BeaconCount, CastlePlans.BeaconCoins));
        if (NextBeacon() == null && world.Castle is { } castle)
        {
            castle.KeepFire = true;
            Speak(Texts.CastleBeaconsLit, beacon.Princess);
        }
        else
            Speak(Texts.DekujuAzBudesChtitPrivezDalsi, beacon.Princess);
        UpdateDeliveryGuides();
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>
    /// The guest gets off the bench before the gate and goes to a chair at the table; the last of them starts the feast.
    /// </summary>
    internal void FinishGuestDump(HaulTrip trip)
    {
        var guest = trip.Guest!;
        var castle = world.Castle!;
        castleRider = -1;
        trailer.SetPassenger(null);
        castle.Seat(guest.Guest, guest.Character.Figure());
        castle.Princess.Celebrate();
        EndHaul();
        AddCoins(CastlePlans.GuestCoins);
        int seated = GuestsSeated(castle);
        var (chair, _) = castle.Chair(guest.Guest);
        if (NextGuest(castle) < 0 && !castle.Fireworks)
        {
            castle.Fireworks = true;
            AddCoins(CastlePlans.FeastCoins);
            CelebrateAt(chair + Vector3.Up * 2, WithPets(Texts.CastleFeast(CastlePlans.FeastCoins), EarnPets()));
            Speak(Texts.CastleFeastSpeech, castle.Princess);
        }
        else
        {
            CelebrateAt(
                chair + Vector3.Up * 2,
                Texts.GuestSeated(guest.Character.DisplayName, seated, world.CastleGuestCount, CastlePlans.GuestCoins)
            );
            Speak(Texts.CastleGuestsWanted, castle.Princess);
        }
        UpdateDeliveryGuides();
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>
    /// The castle turns its crane, lets its drawbridge down for the car and lights its fires by night; the beacons
    /// flicker; and a guest carried to the feast sits on the trailer's bench.
    /// </summary>
    private void TickCastle(double delta)
    {
        if (world.Castle is not { } castle || State is GameState.Title)
            return;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        float night = Weather.Darkness;
        castle.Tick(dt, night, vehicle.Position);
        foreach (var beacon in world.Beacons)
            beacon?.Tick(dt, night);
        if (Haul is { Loaded: true, Guest: { } guest })
        {
            if (castleRider != guest.Guest || trailer.Passenger == null)
            {
                castleRider = guest.Guest;
                trailer.SetPassenger(guest.Character.Figure());
            }
        }
        else if (castleRider >= 0 || trailer.Passenger != null)
        {
            castleRider = -1;
            trailer.SetPassenger(null);
        }
    }

    /// <summary>
    /// Puts the castle, its beacons and its table back from a save, before the trip and the pets are restored: a valley
    /// without the works or the log yard has them finished from the start.
    /// </summary>
    private void RestoreCastle(CastleSave save)
    {
        castleRider = -1;
        trailer.SetPassenger(null);
        if (world.Castle is not { } castle)
            return;
        castle.SetStage(world.CastleBuildable ? save.Stage : CastlePlans.Trips);
        for (int border = 0; border < CastlePlans.Beacons; border++)
            world.Beacons[border]?.SetBuilt(!world.BeaconsBuildable || save.Beacons.Contains(border), castle.Complete);
        castle.KeepFire = castle.Complete && NextBeacon() == null;
        for (int chair = 0; chair < CastlePlans.Guests; chair++)
        {
            bool seated = castle.Complete && save.Guests.Contains(chair) && world.CastleGuests[chair] != null;
            castle.Seat(chair, seated ? world.CastleGuests[chair]!.Figure() : null);
        }
        castle.Fireworks = save.Feast && castle.Complete && NextGuest(castle) < 0;
        UpdatePostGuides();
    }

    /// <summary>What the castle saves: its stage, the beacons that burn, the chairs taken and the feast.</summary>
    private CastleSave CaptureCastle()
    {
        var castle = world.Castle;
        return new CastleSave
        {
            Stage = castle?.Stage ?? 0,
            Beacons = Enumerable
                .Range(0, CastlePlans.Beacons)
                .Where(border => world.Beacons[border] is { Built: true })
                .ToArray(),
            Guests =
                castle == null
                    ? System.Array.Empty<int>()
                    : Enumerable.Range(0, CastlePlans.Guests).Where(castle.Seated).ToArray(),
            Feast = castle?.Fireworks ?? false,
        };
    }
}
