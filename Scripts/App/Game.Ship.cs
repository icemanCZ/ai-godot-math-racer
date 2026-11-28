using System;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Ui;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The ship Albatros and the turtle island (<c>docs/SHIP_PLAN.md</c>). Mrož kapitán Vojta waits on the edge of the valley
/// above the shipyard. His panel (state <see cref="GameState.ShipStatus"/>) raises the ship on the slipway with fourteen
/// trailer trips: logs, planks and glass from the works, cloth from the farmer for the sails, and the anchor, which the
/// forge of the great line forges of the stone the trailer brings it (the anchor is loaded there and the captain asks the
/// loading question from afar). The last delivery launches the ship. A voyage after a question puts the car on its deck:
/// the steering turns the rudder, the pedals fill and furl the sails, the buoys ring as the ship passes them, and the ship
/// steers itself towards the next one when the steering is left alone. On the turtle island two turtles sit on treasures
/// for the captain's cabin; each asks a question before it crawls off to the sea. A voyage and a stay on the island are
/// not saved: the car is saved at the shipyard again. The stage, the anchor, the voyages and the cabin are saved as
/// <c>SaveData.Ship</c>.
/// </summary>
public partial class Game
{
    /// <summary>The voyage under way, or null.</summary>
    private ShipVoyage? shipVoyage;

    /// <summary>Seconds into the launch of the ship down the slipway, or -1.</summary>
    private float shipLaunch = -1;

    /// <summary>Whether the anchor was forged and waits beside the forge.</summary>
    private bool shipAnchor;

    private int shipVoyages;
    private readonly int[] shipKinds = new int[ShipPlans.Kinds];

    /// <summary>The nest whose turtle asks its question now, or -1.</summary>
    private int shipTurtle = -1;

    /// <summary>The nest whose turtle asked last; it asks again only once the car has driven away from it.</summary>
    private int turtleRested = -1;

    /// <summary>For the checks: the helm as if the player held it, instead of the steering and the pedals.</summary>
    internal HelmInput? ShipInputOverride { get; set; }

    private bool ShipWanted => world.Ship != null && world.Captain != null && world.ShipPlace != null;

    /// <summary>The ship is under sail with the car on its deck.</summary>
    internal bool ShipSailing => shipVoyage != null;

    internal ShipVoyage? ShipVoyageNow => shipVoyage;

    internal bool OnTurtleIsland => world.TurtleIslandOpen;

    internal bool ShipLaunching => shipLaunch >= 0;

    internal bool ShipAnchorForged => shipAnchor;

    internal int ShipVoyages => shipVoyages;

    internal int[] ShipKinds => (int[])shipKinds.Clone();

    /// <summary>The captain cannot be talked to while he steers the ship or launches it.</summary>
    private bool ShipHolds(Character actor) => (shipVoyage != null || shipLaunch >= 0) && actor == world.Captain;

    /// <summary>The captain shows his mark while the ship has something to build and nothing is under way.</summary>
    private bool CaptainAwaits =>
        ShipWanted
        && Haul == null
        && Delivery == null
        && shipVoyage == null
        && !world.TurtleIslandOpen
        && !world.Ship!.Complete;

    private HaulTrip? TripOf(Ship ship) =>
        Haul is { } trip && (trip.Ship == ship || trip.ShipForge != null) ? trip : null;

    /// <summary>Whether stone must go to the forge first: only the delivery of the anchor needs it, once.</summary>
    private bool ShipForgeNeeded(Ship ship) =>
        !ship.Complete && ShipPlans.CargoOf(ship.Stage) == HaulLoad.Anchor && !shipAnchor && world.ShipHasForge;

    /// <summary>The goods of the next delivery stand at their works, or the anchor at the forge; logs and cloth are always there.</summary>
    private bool ShipStepReady(Ship ship)
    {
        if (ship.Complete)
            return false;
        var load = ShipPlans.CargoOf(ship.Stage);
        return load == HaulLoad.Anchor
            ? shipAnchor
            : !HaulLoads.IsGoods(load) || GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;
    }

    private int ShipKindsFound()
    {
        int found = 0;
        foreach (int count in shipKinds)
            found += count > 0 ? 1 : 0;
        return found;
    }

    /// <summary>The captain's panel: the ship still to be built, the voyage once it floats, or the way home from the island.</summary>
    internal void OpenCaptain(Character captain)
    {
        if (captain.Role is not CaptainRole || !ShipWanted || world.Ship is not { } ship)
            return;
        ClearChallenge();
        EnterInteraction(GameState.ShipStatus, captain);
        ShowCaptain();
        string speech =
            Haul != null ? ""
            : world.TurtleIslandOpen ? OutlandTexts.ShipIslandSpeech
            : ship.Complete ? OutlandTexts.ShipSailWanted
            : ship.Stage == 0 ? captain.Greeting + "\n" + OutlandTexts.ShipWanted
            : ShipForgeNeeded(ship) ? OutlandTexts.ShipAnchorMissing
            : !ShipStepReady(ship) ? Texts.ZboziJesteNeniVyrobenePosluTe
            : Texts.PrivezesMiDalsiMaterial;
        if (speech != "")
            Speak(speech, captain);
    }

    private void ShowCaptain()
    {
        if (nearby is not { Role: CaptainRole } captain || world.Ship is not { } ship)
            return;
        bool free = Haul == null && Delivery == null && shipVoyage == null && shipLaunch < 0;
        bool forge = ShipForgeNeeded(ship);
        bool ready = ShipStepReady(ship);
        var mode =
            world.TurtleIslandOpen ? CaptainMode.Island
            : ship.Complete ? CaptainMode.Sail
            : CaptainMode.Build;
        ui.CaptainPanel(
            captain,
            mode,
            ship.Stage,
            TripOf(ship),
            free && !ship.Complete && (ready || forge),
            forge,
            shipAnchor,
            free && !ready && !forge && !ship.Complete ? GoodsWorks(ShipPlans.CargoOf(ship.Stage)) : null,
            ShipKinds,
            free && ship.Afloat
        );
    }

    internal void BeginShipStart() => BeginShipStart(false);

    /// <summary>
    /// The captain's green check mark: a question, then the trailer for the next load of the ship or, when the next
    /// delivery is the anchor and it is not forged yet, for stone for the forge.
    /// </summary>
    private void BeginShipStart(bool quizzed)
    {
        if (
            State != GameState.ShipStatus
            || nearby is not { Role: CaptainRole }
            || world.Ship is not { } ship
            || ship.Complete
            || Haul != null
            || Delivery != null
            || shipVoyage != null
        )
            return;
        ITripTarget? target =
            ShipForgeNeeded(ship) ? new ShipForgeTrip(ship)
            : ShipStepReady(ship) ? ship
            : null;
        if (target == null)
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Ship);
            return;
        }
        StartTrip(target);
    }

    /// <summary>
    /// A question asked by the captain: the anchor's loading question, asked from afar; otherwise there is none, so the
    /// panel closes.
    /// </summary>
    internal void CompleteCaptainQuiz(Character captain)
    {
        if (Haul is { Loaded: false } trip && trip.Asker == captain && trip.Reached(vehicle.Position))
        {
            CompleteHaulLoading();
            return;
        }
        FinishInteraction();
    }

    /// <summary>The anchor leaves the forge the moment it is loaded.</summary>
    private bool TakeAnchor()
    {
        if (!shipAnchor)
            return false;
        shipAnchor = false;
        ShowForgedAnchor();
        return true;
    }

    private void ShowForgedAnchor()
    {
        if (world.ForgedAnchor != null)
            world.ForgedAnchor.Visible = shipAnchor;
    }

    /// <summary>The stage of the ship, skipping the anchor in a valley whose great line has no forge.</summary>
    private int ShipStageAfter(int stage) =>
        stage == ShipPlans.AnchorStep && !world.ShipHasForge ? ShipPlans.AnchorStep + 1 : stage;

    /// <summary>The load is tipped out on the edge of the shipyard: the next part of the ship stands, the last launches it.</summary>
    internal void FinishShipDump(HaulTrip trip)
    {
        var ship = trip.Ship!;
        world.Tally.AddRaw(trip.Load);
        ship.SetStage(ShipStageAfter(trip.Step + 1));
        ship.Captain.Celebrate();
        EndHaul();
        if (ship.Complete)
        {
            AddCoins(ShipPlans.Coins);
            string? pets = EarnPets();
            CelebrateAt(ship.GlobalPosition + Vector3.Up * 6, WithPets(OutlandTexts.ShipDone(ShipPlans.Coins), pets));
            Speak(OutlandTexts.ShipLaunchSpeech, ship.Captain);
            BeginLaunch();
        }
        else
        {
            world.Shipyard?.Hammer();
            world.Shipyard?.SetLaunched(false, ship.Stage);
            CelebrateAt(
                ship.GlobalPosition + Vector3.Up * 5,
                OutlandTexts.ShipPart(ShipPlans.Parts[trip.Step], ship.Stage, ShipPlans.Trips)
            );
            Speak(Texts.DekujuAzBudesChtitPrivezDalsi, ship.Captain);
        }
        UpdateDeliveryGuides();
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The stone is tipped out before the forge: it forges, and the anchor lies beside it.</summary>
    internal void FinishShipForgeDump(HaulTrip trip)
    {
        var ship = world.Ship!;
        world.Tally.AddRaw(trip.Load);
        shipAnchor = true;
        ShowForgedAnchor();
        ship.Forge?.Forge();
        ship.Captain.Celebrate();
        EndHaul();
        CelebrateAt((ship.Forge?.Position ?? ship.GlobalPosition) + Vector3.Up * 3, OutlandTexts.ShipAnchorForged);
        Speak(OutlandTexts.ShipAnchorForgedSpeech, ship.Captain);
        UpdateDeliveryGuides();
        UpdatePostGuides();
        SaveProgress();
    }

    /// <summary>The ship slides down the slipway into the sea and turns to its anchorage.</summary>
    private void BeginLaunch()
    {
        if (world.Ship is not { } ship || world.Shipyard is not { } yard || world.ShipPlace is not { } site)
            return;
        shipLaunch = 0;
        ship.Afloat = false;
        yard.SetLaunched(true, ship.Stage);
        followCamera.ShowMoment(site.Local(0, 14) with { Y = ValleyBorders.SeaLevel + 3 }, site.Along);
    }

    /// <summary>The launch: down the slipway in three and a half seconds, then out to the anchorage, turning along the coast.</summary>
    private void TickLaunch(float dt, Ship ship, Shipyard yard, ShipSite site)
    {
        shipLaunch += dt;
        var slip = yard.SlipPose;
        var water = site.Local(0, 22);
        float sea = world.SeaSurface;
        if (shipLaunch < 3.5f)
        {
            float u = shipLaunch / 3.5f,
                eased = u * u;
            var at = slip.Position.Lerp(water with { Y = sea - Ship.Draft }, eased);
            ship.Position = at;
            ship.Rotation = new Vector3(Mathf.Lerp(slip.Pitch, 0, eased), slip.Yaw, 0);
            return;
        }
        float v = Mathf.Clamp((shipLaunch - 3.5f) / 4.5f, 0, 1),
            smooth = v * v * (3 - 2 * v);
        ship.Float(water.Lerp(site.Anchorage, smooth), Mathf.LerpAngle(site.Yaw, site.AlongYaw, smooth), sea);
        if (v >= 1)
        {
            shipLaunch = -1;
            ship.Afloat = true;
            world.RestShip();
            UpdatePostGuides();
        }
    }

    internal void BeginShipSail()
    {
        if (
            State != GameState.ShipStatus
            || nearby is not { Role: CaptainRole } captain
            || world.Ship is not { Afloat: true }
            || world.TurtleIslandOpen
            || shipVoyage != null
            || Haul != null
        )
            return;
        AskBeforeDeparture(Departure.ShipSail);
        Speak(OutlandTexts.ShipSailAsk, captain);
    }

    /// <summary>The question is solved: the voyage to the turtle island begins.</summary>
    private void BeginShipSail(bool quizzed)
    {
        if (!quizzed || world.Ship is not { Afloat: true } || Haul != null || shipVoyage != null)
            return;
        BeginVoyage(false);
    }

    /// <summary>The captain's green check mark on the island: the voyage home, without a question.</summary>
    internal void BeginShipHome()
    {
        if (
            State != GameState.ShipStatus
            || nearby is not { Role: CaptainRole }
            || !world.TurtleIslandOpen
            || shipVoyage != null
        )
            return;
        CloseInteraction();
        BeginVoyage(true);
    }

    private void BeginVoyage(bool home)
    {
        if (
            world.Ship is not { } ship
            || world.ShipPlace is not { } site
            || world.Captain is not { } captain
            || world.TurtleIsland is not { } island
        )
            return;
        if (State != GameState.Exploring)
            CloseInteraction();
        vehicle.Active = false;
        shipTurtle = -1;
        turtleRested = -1;
        world.TurtleIslandOpen = false;
        if (!home)
        {
            shipVoyages++;
            island.Lay(world.TreasuresOfVoyage(shipVoyages, shipKinds));
        }
        shipVoyage = new ShipVoyage(
            home ? site.IslandBerth : site.Anchorage,
            site.Buoys,
            home ? site.Anchorage : site.IslandBerth,
            site.AlongYaw,
            home
        );
        ship.Float(shipVoyage.Helm.Position, shipVoyage.Helm.Heading, world.SeaSurface);
        PlaceOnShip(ship, captain);
        followCamera.Ride(ship, new Vector3(0, 16, -27), new Vector3(0, -6, 40));
        Speak(home ? OutlandTexts.ShipHomeSpeech : OutlandTexts.ShipVoyageSpeech, captain);
        UpdatePostGuides();
        SaveProgress();
    }

    private void PlaceOnShip(Ship ship, Character captain)
    {
        var (seat, heading) = ship.CarSeat;
        vehicle.Carry(seat, heading);
        captain.Position = ship.HelmSpot;
        captain.Rotation = new Vector3(0, ship.Rotation.Y, 0);
    }

    /// <summary>What the player does with the helm: the steering turns the rudder, the accelerator fills the sails, the brake furls them.</summary>
    private HelmInput ReadHelm()
    {
        if (ShipInputOverride is { } given)
            return given;
        if (State != GameState.Exploring)
            return default;
        float sails =
            Mathf.Max(Input.GetActionStrength("accelerate"), Input.GetActionStrength("stick_forward"))
            - Mathf.Max(
                Mathf.Max(Input.GetActionStrength("reverse"), Input.GetActionStrength("stick_reverse")),
                Input.IsActionPressed("brake") ? 1 : 0
            );
        return new HelmInput(Input.GetAxis("left", "right"), sails);
    }

    /// <summary>
    /// The ship is launched, sails with the car on its deck, or lies at its anchorage or by the island; the buoys bob; and
    /// on the island a turtle the car comes up to asks its question.
    /// </summary>
    private void TickShip(double delta)
    {
        if (!ShipWanted || State is GameState.Title)
            return;
        var ship = world.Ship!;
        var yard = world.Shipyard!;
        var site = world.ShipPlace!;
        var island = world.TurtleIsland!;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        ship.Tick(dt, Weather.Darkness);
        yard.Tick(dt, world.SeaSurface);
        island.Tick(dt);
        if (shipLaunch >= 0)
        {
            TickLaunch(dt, ship, yard, site);
            return;
        }
        if (shipVoyage is { } voyage)
        {
            if (State == GameState.Paused)
                return;
            if (State == GameState.Exploring)
                vehicle.Active = false;
            var input = ReadHelm();
            int rang = voyage.Step(dt, input, world.ShipDepth);
            if (rang >= 0)
            {
                yard.Ring(rang);
                AddCoins(ShipPlans.BuoyCoins);
            }
            // The voyage home may be cut short with the pedals, as a ride on the train is.
            if (voyage.Home && voyage.Time > ShipPlans.HomeGrace && State == GameState.Exploring && DrivesOn())
                voyage.Dock();
            var helm = voyage.Helm;
            ship.Float(helm.Position, helm.Heading, world.SeaSurface);
            ship.SetSails(helm.Fill, helm.Rudder);
            ship.PointArrow(voyage.Docking < 0 && !voyage.Home ? voyage.Target : null);
            PlaceOnShip(ship, world.Captain!);
            if (voyage.Done)
                EndVoyage();
            return;
        }
        if (world.TurtleIslandOpen)
        {
            ship.Float(site.IslandBerth, site.AlongYaw, world.SeaSurface);
            // A car that was put back on the road has left the island.
            if (World.InsideValley(vehicle.Position))
            {
                LeaveTurtleIsland();
                return;
            }
            AskTurtle(island);
        }
        else if (ship.Afloat)
            ship.Float(site.Anchorage, site.AlongYaw, world.SeaSurface);
    }

    /// <summary>The ship docks: by the island the car drives off onto it, at home it is put back on the edge of the yard.</summary>
    private void EndVoyage()
    {
        if (shipVoyage is not { } voyage || world.ShipPlace is not { } site || world.Ship is not { } ship)
            return;
        shipVoyage = null;
        followCamera.EndRide();
        ship.PointArrow(null);
        ship.SetSails(0, 0);
        var captain = world.Captain!;
        if (voyage.Home)
        {
            world.TurtleIslandOpen = false;
            world.RestShip();
            PutCarAtYard(site);
            PutCaptainAtYard(site);
            Speak(OutlandTexts.ShipHomeEnd, captain, interrupt: false);
        }
        else
        {
            world.TurtleIslandOpen = true;
            var land = world.TurtleIsland!.GroundAt(site.IslandLanding);
            vehicle.SetPose(land + Vector3.Up * .3f, site.Yaw);
            captain.Position = world.TurtleIsland.GroundAt(site.IslandTalk);
            captain.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
            Speak(OutlandTexts.ShipIslandHello, captain, interrupt: false);
        }
        if (State == GameState.Exploring)
            vehicle.Active = true;
        UpdatePostGuides();
        SaveProgress();
    }

    private void PutCarAtYard(ShipSite site) =>
        vehicle.SetPose(world.Ground.At(site.Return.X, site.Return.Z) + Vector3.Up * .15f, site.Yaw + Mathf.Pi);

    private void PutCaptainAtYard(ShipSite site)
    {
        if (world.Captain is not { } captain)
            return;
        captain.Position = world.Ground.At(site.Talk.X, site.Talk.Z);
        captain.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
    }

    /// <summary>The island is left without the ship (the car was put back on the road): ship and captain are at the yard.</summary>
    private void LeaveTurtleIsland()
    {
        world.TurtleIslandOpen = false;
        shipTurtle = -1;
        world.RestShip();
        if (world.ShipPlace is { } site)
            PutCaptainAtYard(site);
        UpdatePostGuides();
    }

    /// <summary>A turtle the car stops by asks its question, once until the car has driven away from it.</summary>
    private void AskTurtle(TurtleIsland island)
    {
        if (turtleRested >= 0 && FlatTo(island.NestAt(turtleRested)) > ShipPlans.TurtleReach + 3)
            turtleRested = -1;
        // A question left without an answer is over once the car drives again.
        if (State == GameState.Exploring)
            shipTurtle = -1;
        if (State != GameState.Exploring || world.Captain is not { } captain)
            return;
        for (int nest = 0; nest < ShipPlans.Nests; nest++)
        {
            if (nest == turtleRested || !island.Waiting(nest) || FlatTo(island.NestAt(nest)) > ShipPlans.TurtleReach)
                continue;
            shipTurtle = nest;
            turtleRested = nest;
            nearby = captain;
            vehicle.Active = false;
            AskBeforeDeparture(Departure.ShipTurtle);
            Speak(OutlandTexts.ShipTurtleAsk, captain, interrupt: false);
            return;
        }
    }

    private float FlatTo(Vector3 p) => new Vector2(vehicle.Position.X - p.X, vehicle.Position.Z - p.Z).Length();

    /// <summary>A turtle's question is solved: it crawls off to the sea and its treasure goes into the captain's cabin.</summary>
    private void BeginShipTurtle(bool quizzed)
    {
        int nest = shipTurtle;
        shipTurtle = -1;
        if (
            quizzed
            && nest >= 0
            && world.TurtleIslandOpen
            && world.TurtleIsland is { } island
            && island.Waiting(nest)
            && world.Ship is { } ship
        )
        {
            int kind = island.KindIn(nest);
            bool first = shipKinds[kind] == 0;
            bool wasFull = ShipKindsFound() >= ShipPlans.Kinds;
            shipKinds[kind]++;
            island.Reveal(nest);
            ship.SetTreasures(shipKinds);
            int coins = ShipPlans.SouvenirCoins + (first ? ShipPlans.NewKindCoins : 0);
            AddCoins(coins);
            string message = OutlandTexts.ShipTreasure(ShipPlans.Name(kind), first, coins);
            if (!wasFull && ShipKindsFound() >= ShipPlans.Kinds)
            {
                AddCoins(ShipPlans.CabinCoins);
                message += "\n" + OutlandTexts.ShipCabinFull(ShipPlans.CabinCoins);
            }
            CelebrateAt(island.NestAt(nest) + Vector3.Up * 2, message);
            SaveProgress();
        }
        CloseInteraction();
    }

    /// <summary>
    /// Puts the ship, the anchor and the cabin back from a save, before the trip and the pets are restored: a valley
    /// without the log yard, the sawmill or the glassworks has the ship afloat from the start; the ship lies at its
    /// anchorage, or stands on the slipway, and the captain waits on the edge of the yard.
    /// </summary>
    private void RestoreShip(ShipSave save)
    {
        shipVoyage = null;
        shipLaunch = -1;
        shipTurtle = -1;
        turtleRested = -1;
        world.TurtleIslandOpen = false;
        Array.Clear(shipKinds);
        for (int kind = 0; kind < shipKinds.Length && kind < save.Kinds.Length; kind++)
            shipKinds[kind] = save.Kinds[kind];
        shipVoyages = save.Voyage;
        shipAnchor = false;
        if (world.Ship is not { } ship || world.ShipPlace is not { } site)
            return;
        int stage = world.ShipBuildable ? ShipStageAfter(save.Stage) : ShipPlans.Trips;
        ship.SetStage(stage);
        ship.Afloat = ship.Complete;
        ship.SetTreasures(shipKinds);
        ship.SetSails(0, 0);
        ship.PointArrow(null);
        shipAnchor = world.ShipBuildable && save.Anchor && stage == ShipPlans.AnchorStep;
        ShowForgedAnchor();
        world.RestShip();
        PutCaptainAtYard(site);
        UpdatePostGuides();
    }

    /// <summary>What the ship saves: its stage, the anchor at the forge, its voyages and its cabin.</summary>
    private ShipSave CaptureShip() =>
        new()
        {
            Stage = world.Ship?.Stage ?? 0,
            Anchor = shipAnchor,
            Voyage = shipVoyages,
            Kinds = (int[])shipKinds.Clone(),
        };

    /// <summary>The trip of the ship, or of stone for its anchor, from a save.</summary>
    private ITripTarget? ShipTripTarget(SavedTrip saved) =>
        saved.Index == 0 && world.Ship is { Complete: false } ship
            ? saved.Kind == TripKinds.Ship
                ? ship
                : ShipForgeNeeded(ship)
                    ? new ShipForgeTrip(ship)
                    : null
            : null;
}
