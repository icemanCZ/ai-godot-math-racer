using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Ui;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The ferry to the bird island (<c>docs/FERRY_PLAN.md</c>). Volavka pozorovatelka Klárka waits at the landing beside the
/// homestead of the river border. Her panel (state <see cref="GameState.FerryStatus"/>) first builds the landing, its
/// pier and the punt with five trailer trips of logs and planks; then a question sends the car down the border river on
/// the punt to the island before its mouth, with her poling at the stern. On the island the car drives about, and her
/// panel there opens the telescope of the hide or takes the punt home. Through the telescope the player turns the view
/// to find the four birds of the crossing hidden in the reeds round the island: a bird kept in the middle for a moment
/// is written into the atlas, with coins, more for the first of a kind, and the full atlas brings the kingfisher
/// Modroušek. The landing's stage, the crossings, the birds of the crossing seen and the birds of each kind are saved as
/// <c>SaveData.Ferry</c>; a crossing and a stay on the island are not.
/// </summary>
public partial class Game
{
    /// <summary>How long into a crossing the pedals do not end it yet, so that the press that started it does not.</summary>
    private const float FerryGrace = 1.5f;

    /// <summary>How fast the steering turns the telescope, in radians a second.</summary>
    private const float TelescopeTurn = .55f;

    /// <summary>How long the telescope stays open after the last bird of a crossing is seen.</summary>
    private const float TelescopeLinger = 3;

    private FerryLog? ferryLog;
    private BirdIsland? ferryLogFor;
    private int birdsLaidVisit = -1;

    /// <summary>Seconds into a crossing, or less than 0 while the punt is moored.</summary>
    private float ferryRide = -1;

    /// <summary>Whether the crossing under way goes home from the island.</summary>
    private bool ferryHome;

    private bool telescopeOn;
    private float telescopeTime;
    private float telescopeLinger = -1;
    private int spotCandidate = -1;
    private float spotProgress;

    /// <summary>The atlas of the birds of this valley, made anew when the valley is.</summary>
    internal FerryLog Ferry
    {
        get
        {
            if (ferryLog == null || !ReferenceEquals(ferryLogFor, world.BirdIsland))
            {
                ferryLog = new FerryLog();
                ferryLogFor = world.BirdIsland;
                birdsLaidVisit = -1;
            }
            return ferryLog;
        }
    }

    /// <summary>The birds of the crossing that hide round the island now.</summary>
    private IslandBird[] BirdsNow { get; set; } = System.Array.Empty<IslandBird>();

    /// <summary>The valley has the landing, the island and the heron.</summary>
    private bool FerryWanted =>
        world.Birdwatcher != null && world.FerryLanding != null && world.BirdIsland != null && world.FerryRoute != null;

    /// <summary>The heron shows her mark while the punt is still to be built and until the atlas is full.</summary>
    private bool BirdwatcherAwaits =>
        FerryWanted && ferryRide < 0 && (world.FerryLanding is { Complete: false } ? Haul == null : !Ferry.Complete);

    internal bool FerrySailing => ferryRide >= 0;

    internal bool OnBirdIsland => world.IslandOpen;

    internal bool TelescopeOpen => telescopeOn;

    /// <summary>The heron cannot be talked to while she poles the punt or the player looks through the telescope.</summary>
    private bool FerryHolds(Character actor) => (ferryRide >= 0 || telescopeOn) && actor == world.Birdwatcher;

    private HaulTrip? TripOf(FerryLanding landing) => Haul != null && Haul.Ferry == landing ? Haul : null;

    /// <summary>Logs come from the log yard; planks must stand at the sawmill.</summary>
    private bool FerryStepReady(FerryLanding landing)
    {
        if (landing.Complete)
            return false;
        var load = FerryPlans.CargoOf(landing.Stage);
        return !HaulLoads.IsGoods(load) || GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;
    }

    private void LayBirds()
    {
        var log = Ferry;
        if (world.BirdIsland is not { } island || birdsLaidVisit == log.Visit)
            return;
        birdsLaidVisit = log.Visit;
        BirdsNow = world.BirdsOfVisit(log.Visit);
        island.Lay(BirdsNow);
        island.KindsFound = log.KindsFound;
    }

    /// <summary>The heron's panel: the landing still to be built, the atlas and the crossing, or the island's buttons.</summary>
    internal void OpenBirdwatcher(Character heron)
    {
        if (
            heron.Role is not BirdwatcherRole
            || !FerryWanted
            || FerryHolds(heron)
            || world.FerryLanding is not { } landing
        )
            return;
        ClearChallenge();
        EnterInteraction(GameState.FerryStatus, heron);
        ShowBirdwatcher();
        var log = Ferry;
        string speech =
            OnBirdIsland
                ? (BirdsNow.All(bird => log.IsSpotted(bird.Number)) ? Texts.FerryIslandAllSeen : Texts.FerryIslandHello)
            : !landing.Complete
                ? (
                    Haul != null ? ""
                    : landing.Stage == 0 ? heron.Greeting + "\n" + Texts.FerryWanted
                    : !FerryStepReady(landing) ? Texts.ZboziJesteNeniVyrobenePosluTe
                    : Texts.PrivezesMiDalsiMaterial
                )
            : log.Complete ? Texts.FerryAtlasFull
            : log.Visit == 0 ? Texts.FerryReady
            : Texts.FerrySailAgain(FerryPlans.Kinds - log.KindsFound);
        if (speech != "")
            Speak(speech, heron);
    }

    private void ShowBirdwatcher()
    {
        if (nearby is not { Role: BirdwatcherRole } heron || world.FerryLanding is not { } landing)
            return;
        var log = Ferry;
        bool free = Haul == null;
        bool ready = FerryStepReady(landing);
        var mode =
            OnBirdIsland ? BirdwatcherMode.Island
            : landing.Complete ? BirdwatcherMode.Home
            : BirdwatcherMode.Build;
        ui.BirdwatcherPanel(
            heron,
            mode,
            landing.Stage,
            TripOf(landing),
            free && ready,
            free && !ready && !landing.Complete ? GoodsWorks(FerryPlans.CargoOf(landing.Stage)) : null,
            log.Kinds,
            BirdsNow.Select(bird => (bird.Kind, log.IsSpotted(bird.Number))).ToArray(),
            world.AllPets.FirstOrDefault(pet => pet.Kind == CollectorPetKind.Kingfisher),
            Delivery == null && Haul == null
        );
    }

    internal void BeginFerryStart() => BeginFerryStart(false);

    /// <summary>
    /// The heron's green check mark at the landing: a question, then the trailer for the next load of the landing, or,
    /// once the punt is finished, the crossing to the island.
    /// </summary>
    private void BeginFerryStart(bool quizzed)
    {
        if (
            State != GameState.FerryStatus
            || nearby is not { Role: BirdwatcherRole } heron
            || world.FerryLanding is not { } landing
            || OnBirdIsland
            || Haul != null
        )
            return;
        if (!landing.Complete)
        {
            if (!FerryStepReady(landing))
                return;
            if (!quizzed)
            {
                AskBeforeDeparture(Departure.Ferry);
                return;
            }
            StartTrip(landing);
            return;
        }
        if (Delivery != null)
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.FerrySail);
            Speak(Texts.FerrySailQuestion, heron);
            return;
        }
        BeginCrossing(false);
    }

    private void BeginFerrySail(bool quizzed)
    {
        if (world.FerryLanding is { Complete: true })
            BeginFerryStart(quizzed);
    }

    /// <summary>A question asked by the heron that is not before a trip: there is none, so the panel closes.</summary>
    internal void CompleteBirdwatcherQuiz(Character heron) => FinishInteraction();

    /// <summary>The load is tipped out at the landing: the next part of it, or of the punt, stands.</summary>
    internal void FinishFerryDump(HaulTrip trip)
    {
        var landing = trip.Ferry!;
        landing.SetStage(trip.Step + 1);
        landing.Heron.Celebrate();
        if (landing.Complete)
        {
            EndHaul();
            AddCoins(FerryPlans.Coins);
            CelebrateAt(landing.Punt.Position + Vector3.Up * 2, Texts.FerryBuilt(FerryPlans.Coins));
            Speak(Texts.FerryBuiltSpeech, landing.Heron);
            UpdateDeliveryGuides();
            UpdatePostGuides();
            SaveProgress();
            return;
        }
        CelebrateAt(landing.Focus, Texts.FerryPart(FerryPlans.Parts[trip.Step], landing.Stage, FerryPlans.Trips));
        EndHaul();
        Speak(Texts.DekujuAzBudesChtitPrivezDalsi, landing.Heron);
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The island's green check mark: the camera goes up into the hide and looks through the telescope.</summary>
    internal void BeginTelescope()
    {
        if (
            State != GameState.FerryStatus
            || nearby is not { Role: BirdwatcherRole } heron
            || !OnBirdIsland
            || world.FerryPlace is not { } site
            || telescopeOn
        )
            return;
        CloseInteraction();
        vehicle.Active = false;
        telescopeOn = true;
        telescopeTime = 0;
        telescopeLinger = -1;
        spotCandidate = -1;
        spotProgress = 0;
        // It first looks the way of a bird not yet seen, or out to sea.
        int target = NearestUnseenBird(site.Eye, 0);
        float yaw = target >= 0 ? YawTo(site.Eye, world.BirdIsland!.HeadOf(target)) : 0;
        followCamera.ShowTelescope(site.Eye, yaw + .5f, FerryPlans.TelescopeFov);
        world.BirdIsland!.LookingThrough = true;
        Speak(
            BirdsNow.All(bird => Ferry.IsSpotted(bird.Number)) ? Texts.FerryIslandAllSeen : Texts.FerryTelescopeHow,
            heron
        );
    }

    /// <summary>The island's punt button: the heron poles the car home to the landing, without a question.</summary>
    internal void BeginFerryHome()
    {
        if (
            State != GameState.FerryStatus
            || nearby is not { Role: BirdwatcherRole }
            || !OnBirdIsland
            || ferryRide >= 0
        )
            return;
        CloseInteraction();
        BeginCrossing(true);
    }

    /// <summary>The car stands on the punt and the heron poles it down the river to the island, or home again.</summary>
    private void BeginCrossing(bool home)
    {
        if (world.FerryLanding is not { } landing || world.Birdwatcher is not { } heron)
            return;
        if (State != GameState.Exploring)
            CloseInteraction();
        vehicle.Active = false;
        ferryRide = 0;
        ferryHome = home;
        world.IslandOpen = false;
        if (!home)
        {
            Ferry.NextVisit();
            LayBirds();
        }
        PlaceOnPunt(landing, heron);
        followCamera.Ride(landing.Punt, new Vector3(5, 7, -13), new Vector3(0, -2, 30));
        Speak(home ? Texts.FerryHomeSpeech : Texts.FerrySailSpeech, heron);
        UpdatePostGuides();
        SaveProgress();
    }

    private void PlaceOnPunt(FerryLanding landing, Character heron)
    {
        var (seat, heading) = landing.CarSeat;
        vehicle.Carry(seat, heading);
        heron.Position = landing.HeronSeat;
        heron.Rotation = new Vector3(0, landing.Punt.Rotation.Y, 0);
    }

    /// <summary>The punt lands: at the island the car drives off onto it, at home it is put back at the landing.</summary>
    private void EndCrossing()
    {
        if (ferryRide < 0 || world.FerryLanding is not { } landing || world.FerryPlace is not { } site)
            return;
        ferryRide = -1;
        followCamera.EndRide();
        var heron = landing.Heron;
        if (ferryHome)
        {
            landing.ParkAtHome();
            world.IslandOpen = false;
            vehicle.SetPose(world.Ground.At(site.Return.X, site.Return.Z) + Vector3.Up * .15f, site.Yaw);
            heron.Position = world.Ground.At(site.Talk.X, site.Talk.Z);
            heron.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
            Speak(Texts.FerryHomeEnd, heron, interrupt: false);
        }
        else
        {
            landing.ParkAtIsland(world.SeaSurface);
            world.IslandOpen = true;
            vehicle.SetPose(site.IslandLanding + Vector3.Up * .3f, Mathf.Pi);
            heron.Position = site.IslandTalk;
            heron.Rotation = new Vector3(0, Mathf.Pi, 0);
            Speak(Texts.FerryIslandHello, heron, interrupt: false);
        }
        if (State == GameState.Exploring)
            vehicle.Active = true;
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The birds' clumps of reeds: the bird of the crossing not yet seen whose head is nearest a direction.</summary>
    private int NearestUnseenBird(Vector3 eye, float yaw)
    {
        if (world.BirdIsland is not { } island)
            return -1;
        int best = -1;
        float nearest = float.MaxValue;
        foreach (var bird in BirdsNow)
        {
            if (Ferry.IsSpotted(bird.Number))
                continue;
            float turn = Mathf.Abs(Mathf.Wrap(YawTo(eye, island.HeadOf(bird.Number)) - yaw, -Mathf.Pi, Mathf.Pi));
            if (turn < nearest)
            {
                nearest = turn;
                best = bird.Number;
            }
        }
        return best;
    }

    private static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>
    /// The punt carries the car and the heron along its way; the swimmers ride the tide; and through the telescope the
    /// steering turns the view, a bird kept in its middle for a moment is seen, and the pedals close it.
    /// </summary>
    private void TickFerry(double delta)
    {
        if (State is GameState.Title || !FerryWanted)
        {
            ui.TelescopeHud(false, 0, 0, System.Array.Empty<(int, bool)>());
            return;
        }
        LayBirds();
        var island = world.BirdIsland!;
        var landing = world.FerryLanding!;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        island.Tick(dt, world.SeaSurface);
        island.KindsFound = Ferry.KindsFound;
        if (ferryRide >= 0 && State != GameState.Paused)
        {
            if (State == GameState.Exploring)
                vehicle.Active = false;
            ferryRide += dt;
            var route = world.FerryRoute!;
            bool skip = ferryRide > FerryGrace && State == GameState.Exploring && DrivesOn();
            if (skip || ferryRide >= route.Duration)
            {
                landing.Sail(route.Length, world.SeaSurface, ferryHome);
                PlaceOnPunt(landing, landing.Heron);
                EndCrossing();
            }
            else
            {
                landing.Sail(route.DistanceAt(ferryRide), world.SeaSurface, ferryHome);
                PlaceOnPunt(landing, landing.Heron);
            }
        }
        else if (OnBirdIsland && ferryRide < 0)
        {
            landing.ParkAtIsland(world.SeaSurface);
            // A car that was put back on the road (stuck against the hide) has left the island.
            if (World.InsideValley(vehicle.Position) && world.FerryPlace is { } site)
            {
                world.IslandOpen = false;
                landing.ParkAtHome();
                landing.Heron.Position = world.Ground.At(site.Talk.X, site.Talk.Z);
                landing.Heron.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
            }
        }
        TickTelescope(dt);
    }

    private void TickTelescope(float dt)
    {
        if (!telescopeOn || world.FerryPlace is not { } site || world.BirdIsland is not { } island)
        {
            ui.TelescopeHud(false, 0, 0, System.Array.Empty<(int, bool)>());
            return;
        }
        var birds = BirdsNow.Select(bird => (bird.Kind, Ferry.IsSpotted(bird.Number))).ToArray();
        if (State != GameState.Exploring)
        {
            ui.TelescopeHud(State == GameState.Paused || State == GameState.Reward, 0, 0, birds);
            return;
        }
        vehicle.Active = false;
        telescopeTime += dt;
        if (telescopeTime > .5f && TelescopeLeaves())
        {
            EndTelescope();
            return;
        }
        if (telescopeLinger >= 0)
        {
            telescopeLinger += dt;
            if (telescopeLinger > TelescopeLinger)
            {
                EndTelescope();
                return;
            }
        }
        float steer = Input.GetAxis("left", "right");
        var stick = Input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
        followCamera.SteerPanorama((-steer - stick.X) * TelescopeTurn * dt, -stick.Y * TelescopeTurn * .6f * dt);
        var look = -followCamera.GlobalBasis.Z;
        float yaw = Mathf.Atan2(look.X, look.Z);
        // The bird nearest the middle of the view, if it is near enough to be seen.
        int candidate = -1;
        float nearest = FerryPlans.SpotAngle;
        foreach (var bird in BirdsNow)
        {
            if (Ferry.IsSpotted(bird.Number))
                continue;
            var to = (island.HeadOf(bird.Number) - followCamera.GlobalPosition).Normalized();
            float angle = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(look.Dot(to), -1, 1)));
            if (angle < nearest)
            {
                nearest = angle;
                candidate = bird.Number;
            }
        }
        spotProgress =
            candidate >= 0 && candidate == spotCandidate ? spotProgress + dt
            : candidate >= 0 ? dt
            : 0;
        spotCandidate = candidate;
        if (candidate >= 0 && spotProgress >= FerryPlans.SpotSeconds)
        {
            SpotBird(BirdsNow.First(bird => bird.Number == candidate));
            spotProgress = 0;
            spotCandidate = -1;
            birds = BirdsNow.Select(bird => (bird.Kind, Ferry.IsSpotted(bird.Number))).ToArray();
        }
        // Which way to turn for the nearest bird still hidden: -1 to the left, 1 to the right, 0 when it is in view.
        int next = NearestUnseenBird(site.Eye, yaw);
        int hint = 0;
        if (next >= 0)
        {
            float turn = Mathf.Wrap(YawTo(site.Eye, island.HeadOf(next)) - yaw, -Mathf.Pi, Mathf.Pi);
            if (Mathf.Abs(turn) > Mathf.DegToRad(FerryPlans.TelescopeFov * .7f))
                hint = turn > 0 ? -1 : 1;
        }
        ui.TelescopeHud(true, hint, spotProgress / FerryPlans.SpotSeconds, birds);
    }

    /// <summary>The pedals close the telescope; the steering only turns it.</summary>
    private static bool TelescopeLeaves()
    {
        foreach (var action in new[] { "accelerate", "reverse", "brake", "stick_forward", "stick_reverse" })
            if (Input.IsActionPressed(action))
                return true;
        return false;
    }

    /// <summary>A bird of the crossing is seen through the telescope: it jumps up and goes into the atlas.</summary>
    private void SpotBird(IslandBird bird)
    {
        var log = Ferry;
        bool newKind = log.Spot(bird.Number, bird.Kind);
        world.BirdIsland?.Cheer(bird.Number);
        if (world.BirdIsland != null)
            world.BirdIsland.KindsFound = log.KindsFound;
        bool full = newKind && log.Complete;
        int coins =
            FerryPlans.CoinsPerBird + (newKind ? FerryPlans.NewKindCoins : 0) + (full ? FerryPlans.AtlasCoins : 0);
        AddCoins(coins);
        string name = FerryPlans.Name(bird.Kind);
        string message = newKind
            ? Texts.FerryNewBird(name, log.KindsFound, FerryPlans.Kinds)
            : Texts.FerryBirdAgain(name);
        if (full)
            message += "\n" + Texts.FerryAtlasDone(FerryPlans.AtlasCoins);
        string? pets = newKind ? EarnPets() : null;
        var head = world.BirdIsland?.HeadOf(bird.Number) ?? Vector3.Zero;
        CelebrateAt(head + Vector3.Up, WithPets(message, pets));
        Speak(message + "\n" + Texts.CoinsGained(coins));
        if (BirdsNow.All(b => log.IsSpotted(b.Number)))
            telescopeLinger = 0;
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The camera comes down from the hide and goes back behind the car.</summary>
    private void EndTelescope()
    {
        if (!telescopeOn)
            return;
        telescopeOn = false;
        followCamera.EndPanorama();
        if (world.BirdIsland != null)
            world.BirdIsland.LookingThrough = false;
        ui.TelescopeHud(false, 0, 0, System.Array.Empty<(int, bool)>());
        if (State == GameState.Exploring)
            vehicle.Active = true;
        if (BirdsNow.All(bird => Ferry.IsSpotted(bird.Number)))
            Speak(Texts.FerryIslandAllSeen, world.Birdwatcher, interrupt: false);
    }

    /// <summary>
    /// Where the car is saved: a crossing and a stay on the island are not, so a car on the punt or the island is saved
    /// as standing at the landing again.
    /// </summary>
    private (Vector3 Position, float Heading) CarPoseToSave()
    {
        if ((ferryRide >= 0 || OnBirdIsland) && world.FerryPlace is { } site)
            return (world.Ground.At(site.Return.X, site.Return.Z) + Vector3.Up * .15f, site.Yaw);
        // A ride on the train is not saved either: the car stands on the apron of the hub again.
        if (lineRide >= 0 && world.MainLinePlace is { } line)
        {
            var back = line.Hub.Return;
            return (world.Ground.At(back.X, back.Z) + Vector3.Up * .15f, FacingRoad(line.Hub));
        }
        return (vehicle.Position, vehicle.Heading);
    }

    /// <summary>
    /// Puts the landing and the atlas back from a save, before the trip and the pets are restored: the punt is moored at
    /// home, the heron at her landing and the birds of the crossing in their reeds.
    /// </summary>
    private void RestoreFerry(int stage, int visit, int[] spotted, int[] kinds)
    {
        ferryRide = -1;
        if (telescopeOn)
        {
            telescopeOn = false;
            followCamera.EndPanorama();
        }
        if (world.BirdIsland != null)
            world.BirdIsland.LookingThrough = false;
        world.IslandOpen = false;
        Ferry.Restore(visit, spotted, kinds);
        birdsLaidVisit = -1;
        if (world.FerryLanding is { } landing && world.FerryPlace is { } site)
        {
            landing.SetStage(world.FerryBuildable ? stage : FerryPlans.Trips);
            landing.ParkAtHome();
            landing.Heron.Position = world.Ground.At(site.Talk.X, site.Talk.Z);
            landing.Heron.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
        }
        LayBirds();
        UpdatePostGuides();
    }

    /// <summary>Whether the corner panel of the telescope shows, for the checks.</summary>
    internal bool TelescopeHudShown => ui.TelescopeHudShown;
}
