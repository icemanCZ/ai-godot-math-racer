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
/// The great railway across the valley (<c>docs/MAINLINE_PLAN.md</c>). Jezevec strojvůdce Kuba waits on the platform of the
/// hub station at the edge of the old valley. His panel (state <see cref="GameState.MainLineStatus"/>) raises the line with
/// seventeen trailer trips, each tipped out where the track laid so far ends: gravel, planks, bricks and glass from the
/// works, and the rails of the third stretch of each arm, which the forge by the mountain slope makes of the stone the
/// trailer brings it (the rails are loaded there and the engine driver asks the loading question from afar). A finished
/// arm carries the car: a question for the ticket, then the car stands on the flatbed of the train, which runs to the
/// terminal station and back; a save made on the train or at a terminal puts the car and the driver at the hub again.
/// The line's stage and the rails in the forge are saved as <c>SaveData.MainLine</c>.
/// </summary>
public partial class Game
{
    /// <summary>The arm the train is running on now, or -1.</summary>
    private int lineRide = -1;

    private float lineRideTime;
    private float lineRideDuration;
    private bool lineRideHome;
    private float lineRideFrom,
        lineRideTo;
    private bool lineWhistled;

    /// <summary>The arm whose terminal the car and the driver stand at after a ride, or -1 at the hub.</summary>
    private int lineAt = -1;

    /// <summary>The arm chosen for the ride whose ticket question is under way, or -1.</summary>
    private int linePending = -1;

    private bool LineWanted => world.MainLine != null && world.Engineer != null;

    /// <summary>The train is running with the car on it.</summary>
    internal bool LineRiding => lineRide >= 0;

    /// <summary>The car and the driver stand at the terminal of this arm after a ride, or -1.</summary>
    internal int LineAtTerminal => lineAt;

    /// <summary>The driver cannot be talked to while he drives the train with the car aboard.</summary>
    private bool LineHolds(Character actor) => lineRide >= 0 && actor == world.Engineer;

    /// <summary>The driver shows his mark while the line has something to build and nothing is under way.</summary>
    private bool EngineerAwaits =>
        LineWanted && Haul == null && Delivery == null && lineRide < 0 && lineAt < 0 && !world.MainLine!.Complete;

    private HaulTrip? TripOf(MainLine line) =>
        Haul is { } trip && (trip.Line == line || trip.ForgeTrip != null) ? trip : null;

    /// <summary>Whether the rails for the next delivery wait in the forge: only the delivery of rails needs them.</summary>
    private bool ForgeNeeded(MainLine line) =>
        !line.Complete && MainLinePlans.CargoOf(line.Stage) == HaulLoad.Rails && line.Forge.Rails <= 0;

    /// <summary>The goods of the next delivery stand at their works, or its rails in the forge; gravel and the like are always there.</summary>
    private bool LineStepReady(MainLine line)
    {
        if (line.Complete)
            return false;
        var load = MainLinePlans.CargoOf(line.Stage);
        return load == HaulLoad.Rails
            ? line.Forge.Rails > 0
            : !HaulLoads.IsGoods(load) || GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;
    }

    /// <summary>The engine driver's panel: the line still to be built, the forge, and the arms that carry the car.</summary>
    internal void OpenEngineer(Character engineer)
    {
        if (engineer.Role is not EngineerRole || !LineWanted || world.MainLine is not { } line)
            return;
        ClearChallenge();
        EnterInteraction(GameState.MainLineStatus, engineer);
        ShowEngineer();
        string speech =
            Haul != null ? ""
            : lineAt >= 0 ? Texts.LineHomeSpeech
            : line.Complete ? Texts.LineAllDone
            : line.Stage == 0 ? engineer.Greeting + "\n" + Texts.LineWanted
            : ForgeNeeded(line) ? Texts.LineRailsMissing
            : !LineStepReady(line) ? Texts.ZboziJesteNeniVyrobenePosluTe
            : Texts.PrivezesMiDalsiMaterial;
        if (speech != "")
            Speak(speech, engineer);
    }

    private void ShowEngineer()
    {
        if (nearby is not { Role: EngineerRole } engineer || world.MainLine is not { } line)
            return;
        bool free = Haul == null && Delivery == null && lineRide < 0;
        bool forge = ForgeNeeded(line);
        bool ready = LineStepReady(line);
        var arms = Enumerable
            .Range(0, MainLinePlans.Arms)
            .Select(arm => MainLinePlans.ArmDone(line.Stage, arm))
            .ToArray();
        ui.EngineerPanel(
            engineer,
            line.Stage,
            TripOf(line),
            free && lineAt < 0 && !line.Complete && (ready || forge),
            forge,
            free && lineAt < 0 && !ready && !forge && !line.Complete
                ? GoodsWorks(MainLinePlans.CargoOf(line.Stage))
                : null,
            line.Forge.Rails,
            arms,
            free ? lineAt : -2,
            world.AllPets.FirstOrDefault(pet => pet.Kind == CollectorPetKind.Mole)
        );
    }

    internal void BeginMainLineStart() => BeginMainLineStart(false);

    /// <summary>
    /// The driver's green check mark: a question, then the trailer for the next load of the line or, when the next delivery is
    /// rails and the forge has none, for stone for the forge.
    /// </summary>
    private void BeginMainLineStart(bool quizzed)
    {
        if (
            State != GameState.MainLineStatus
            || nearby is not { Role: EngineerRole }
            || world.MainLine is not { } line
            || line.Complete
            || Haul != null
            || Delivery != null
            || lineAt >= 0
            || lineRide >= 0
        )
            return;
        ITripTarget? target =
            ForgeNeeded(line) ? new LineForgeTrip(line)
            : LineStepReady(line) ? line
            : null;
        if (target == null)
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.MainLine);
            return;
        }
        StartTrip(target);
    }

    /// <summary>
    /// A question asked by the engine driver: the rails' loading question, asked from afar; otherwise there is none, so the
    /// panel closes.
    /// </summary>
    internal void CompleteEngineerQuiz(Character engineer)
    {
        if (Haul is { Loaded: false } trip && trip.Asker == engineer && trip.Reached(vehicle.Position))
        {
            CompleteHaulLoading();
            return;
        }
        FinishInteraction();
    }

    /// <summary>The rails come out of the forge's stack the moment they are loaded.</summary>
    private bool TakeRail()
    {
        if (world.LineForge is not { Rails: > 0 } forge)
            return false;
        forge.SetRails(forge.Rails - 1);
        return true;
    }

    /// <summary>The load is tipped out where the track ends, or before a station: the next part of the line stands.</summary>
    internal void FinishMainLineDump(HaulTrip trip)
    {
        var line = trip.Line!;
        var plan = MainLinePlans.Step(trip.Step);
        world.Tally.AddRaw(trip.Load);
        line.SetStage(trip.Step + 1);
        line.Engineer.Celebrate();
        EndHaul();
        string? pets = null;
        if (plan.Part == LinePart.HubStation)
        {
            AddCoins(MainLinePlans.HubCoins);
            CelebrateAt(line.Focus(trip.Step), Texts.LineHubDone(MainLinePlans.HubCoins));
            Speak(Texts.LineHubReady, line.Engineer);
        }
        else if (plan.Part == LinePart.Lamps)
        {
            AddCoins(MainLinePlans.ArmCoins);
            if (line.Complete)
            {
                AddCoins(MainLinePlans.Coins);
                pets = EarnPets();
                CelebrateAt(
                    line.Focus(trip.Step),
                    WithPets(Texts.LineDone(MainLinePlans.ArmCoins + MainLinePlans.Coins), pets)
                );
                Speak(Texts.LineAllDone, line.Engineer);
            }
            else
            {
                CelebrateAt(line.Focus(trip.Step), Texts.LineArmDone(plan.Arm, MainLinePlans.ArmCoins));
                Speak(Texts.DekujuAzBudesChtitPrivezDalsi, line.Engineer);
            }
        }
        else
        {
            CelebrateAt(
                line.Focus(trip.Step),
                Texts.LinePart(MainLinePlans.Parts[trip.Step], line.Stage, MainLinePlans.Trips)
            );
            Speak(Texts.DekujuAzBudesChtitPrivezDalsi, line.Engineer);
        }
        UpdateDeliveryGuides();
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The stone is tipped out before the forge: it forges, and the rails are there once it has.</summary>
    internal void FinishForgeDump(HaulTrip trip)
    {
        var line = world.MainLine!;
        var forge = line.Forge;
        world.Tally.AddRaw(trip.Load);
        forge.SetRails(forge.Rails + MainLinePlans.RailsPerStone);
        forge.Forge();
        line.Engineer.Celebrate();
        EndHaul();
        CelebrateAt(forge.Position + Vector3.Up * 3, Texts.LineForged(forge.Rails));
        Speak(Texts.LineForgedSpeech, line.Engineer);
        UpdateDeliveryGuides();
        UpdatePostGuides();
        SaveProgress();
    }

    internal void BeginMainLineRide(int arm)
    {
        if (State != GameState.MainLineStatus || nearby is not { Role: EngineerRole } engineer || lineRide >= 0)
            return;
        if (world.MainLine is not { } line || !MainLinePlans.ArmDone(line.Stage, arm) || lineAt >= 0 || Haul != null)
            return;
        linePending = arm;
        AskBeforeDeparture(Departure.MainLineRide);
        Speak(Texts.LineRideAsk, engineer);
    }

    /// <summary>The ticket is solved: the ride begins. A save made in the question has lost the arm, so the first finished one is taken.</summary>
    private void BeginMainLineRide(bool quizzed)
    {
        if (world.MainLine is not { } line)
            return;
        int arm =
            linePending >= 0 && MainLinePlans.ArmDone(line.Stage, linePending)
                ? linePending
                : Enumerable.Range(0, MainLinePlans.Arms).FirstOrDefault(a => MainLinePlans.ArmDone(line.Stage, a), -1);
        linePending = -1;
        if (arm < 0 || !quizzed || Haul != null)
            return;
        BeginRide(arm, false);
    }

    /// <summary>The terminal's green check mark: the way back to the hub, without a question.</summary>
    internal void BeginMainLineHome()
    {
        if (State != GameState.MainLineStatus || nearby is not { Role: EngineerRole } || lineAt < 0 || lineRide >= 0)
            return;
        BeginRide(lineAt, true);
    }

    private void BeginRide(int arm, bool home)
    {
        if (world.MainLine is not { } line || world.Engineer is not { } engineer)
            return;
        if (State != GameState.Exploring)
            CloseInteraction();
        vehicle.Active = false;
        lineRide = arm;
        lineRideTime = 0;
        lineRideHome = home;
        lineWhistled = false;
        var route = home ? line.RideBack(arm) : line.RideTo(arm);
        float hubMiddle = line.HubTrack.Length * .5f;
        lineRideFrom = home ? 18 : hubMiddle;
        lineRideTo = home ? route.Length - hubMiddle : route.Length - 18;
        lineRideDuration = Mathf.Clamp(
            Mathf.Abs(lineRideTo - lineRideFrom) / MainLinePlans.TrainSpeed,
            6,
            MainLinePlans.LongestRide
        );
        PlaceOnTrain(line, route, lineRideFrom, 0, engineer);
        followCamera.Ride(line.Train, new Vector3(5, 6.5f, -17), new Vector3(0, -1, 26));
        line.Train.Whistle();
        Speak(home ? Texts.LineHomeSpeech : Texts.LineRideSpeech(arm), engineer);
        UpdatePostGuides();
        SaveProgress();
    }

    private void PlaceOnTrain(MainLine line, LineRoute route, float distance, float travel, Character engineer)
    {
        line.Train.Run(route, distance, travel);
        var (seat, heading) = line.Train.CarSeat;
        vehicle.Carry(seat, heading);
        engineer.Position = line.Train.CabSeat;
        engineer.Rotation = new Vector3(0, heading, 0);
    }

    /// <summary>The train stops: at a terminal the car is put down on the apron, at the hub on its own.</summary>
    private void EndRide()
    {
        if (lineRide < 0 || world.MainLine is not { } line || world.Engineer is not { } engineer)
            return;
        int arm = lineRide;
        lineRide = -1;
        followCamera.EndRide();
        var site = line.Site;
        if (lineRideHome)
        {
            lineAt = -1;
            line.ParkAtHub();
            var back = site.Hub.Return;
            vehicle.SetPose(world.Ground.At(back.X, back.Z) + Vector3.Up * .15f, FacingRoad(site.Hub));
            var talk = line.TalkAtHub();
            engineer.Position = talk;
            engineer.Rotation = new Vector3(0, site.Hub.Yaw + (site.Hub.Side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2), 0);
            Speak(Texts.LineHomeEnd, engineer, interrupt: false);
        }
        else
        {
            lineAt = arm;
            line.ParkAtTerminal(arm);
            var terminal = site.Terminals[arm];
            var back = terminal.Return;
            vehicle.SetPose(world.Ground.At(back.X, back.Z) + Vector3.Up * .15f, FacingRoad(terminal));
            engineer.Position = line.TalkAt(arm);
            engineer.Rotation = new Vector3(0, terminal.Yaw + (terminal.Side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2), 0);
            Speak(Texts.LineRideEnd(arm), engineer, interrupt: false);
        }
        if (State == GameState.Exploring)
            vehicle.Active = true;
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The way the car faces when it is put down: from the platform towards the road.</summary>
    private static float FacingRoad(LineStationSite station)
    {
        var way = station.Right * station.Side;
        return Mathf.Atan2(way.X, way.Z);
    }

    /// <summary>
    /// The train runs with the car and the driver on it; the forge hammers; and a driver left at a terminal goes back to the
    /// hub (the train runs home empty) once the car has driven off.
    /// </summary>
    private void TickMainLine(double delta)
    {
        if (!LineWanted || State is GameState.Title)
            return;
        var line = world.MainLine!;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        float night = Weather.Darkness;
        line.Tick(dt, night);
        line.Forge.Tick(dt, night);
        if (lineRide >= 0 && State != GameState.Paused)
        {
            if (State == GameState.Exploring)
                vehicle.Active = false;
            lineRideTime += dt;
            var route = lineRideHome ? line.RideBack(lineRide) : line.RideTo(lineRide);
            float u = Mathf.Clamp(lineRideTime / lineRideDuration, 0, 1);
            float eased = u * u * (3 - 2 * u);
            float distance = Mathf.Lerp(lineRideFrom, lineRideTo, eased);
            PlaceOnTrain(line, route, distance, distance - lineRideFrom, world.Engineer!);
            if (!lineWhistled && u > .45f)
            {
                lineWhistled = true;
                line.Train.Whistle();
            }
            bool skip = lineRideTime > MainLinePlans.RideGrace && State == GameState.Exploring && DrivesOn();
            if (skip || u >= 1)
            {
                PlaceOnTrain(line, route, lineRideTo, lineRideTo - lineRideFrom, world.Engineer!);
                EndRide();
            }
        }
        else if (lineAt >= 0 && lineRide < 0 && line.Site.Terminals[lineAt] is { } terminal)
        {
            var d = vehicle.Position - terminal.Position;
            if (new Vector2(d.X, d.Z).Length() > 90)
                SendEngineerHome(line);
        }
    }

    /// <summary>The driver and the train wait at the hub again.</summary>
    private void SendEngineerHome(MainLine line)
    {
        lineAt = -1;
        line.ParkAtHub();
        if (world.Engineer is { } engineer && lineRide < 0)
        {
            engineer.Position = line.TalkAtHub();
            engineer.Rotation = new Vector3(
                0,
                line.Site.Hub.Yaw + (line.Site.Hub.Side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2),
                0
            );
        }
        UpdatePostGuides();
    }

    /// <summary>
    /// Puts the line and the forge back from a save, before the trip and the pets are restored: a valley without the
    /// works, the log yard or the glassworks has the line finished from the start; the train and the driver wait at the hub.
    /// </summary>
    private void RestoreMainLine(MainLineSave save)
    {
        lineRide = -1;
        lineAt = -1;
        linePending = -1;
        followCamera.EndRide();
        if (world.MainLine is not { } line)
            return;
        line.SetStage(world.MainLineBuildable ? save.Stage : MainLinePlans.Trips);
        line.Forge.SetRails(world.MainLineBuildable ? save.Rails : 0);
        line.ParkAtHub();
        if (world.Engineer is { } engineer)
        {
            engineer.Position = line.TalkAtHub();
            engineer.Rotation = new Vector3(
                0,
                line.Site.Hub.Yaw + (line.Site.Hub.Side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2),
                0
            );
        }
        UpdatePostGuides();
    }

    /// <summary>What the line saves: its stage and the rails in the forge.</summary>
    private MainLineSave CaptureMainLine() =>
        new() { Stage = world.MainLine?.Stage ?? 0, Rails = world.LineForge?.Rails ?? 0 };

    /// <summary>The trip of the line, or of stone for its forge, from a save.</summary>
    private ITripTarget? MainLineTripTarget(SavedTrip saved) =>
        saved.Index == 0 && world.MainLine is { Complete: false } line
            ? saved.Kind == TripKinds.MainLine
                ? line
                : new LineForgeTrip(line)
            : null;
}
