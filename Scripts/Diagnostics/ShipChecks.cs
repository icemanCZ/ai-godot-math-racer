using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The ship Albatros and the turtle island (<c>docs/SHIP_PLAN.md</c>): the shipyard is planned on a beach of the coast
/// clear of everything, the island out at sea and the buoys of the way in deep water; fourteen trailer trips build the
/// ship on the slipway, the anchor is forged of stone at the forge of the great line and loaded there, where the captain
/// asks the question, and the last load launches the ship; a voyage carries the car along the buoys to the island, where
/// two turtles give a treasure each for a question, and home again; a save made at sea or on the island puts the car at
/// the shipyard; the damaged saves are turned down and a save from before the ship loads.
/// </summary>
public partial class ShipChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public ShipChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("ship-checks/progress.json");
            var trailer = game.Trailer;
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                foreach (var rule in SaveRules.All.Where(rule => rule.Invalid(saved)))
                    GD.Print($"BROKEN RULE: {rule.Name}");
                Check(SaveRules.All.All(rule => !rule.Invalid(saved)), $"save accepted: {what}");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save read back: {what}");
                game.RestoreProgress(loaded!);
                string before = JsonSerializer.Serialize(saved),
                    after = JsonSerializer.Serialize(game.CaptureProgress());
                if (before != after)
                {
                    int at = 0;
                    while (at < Math.Min(before.Length, after.Length) && before[at] == after[at])
                        at++;
                    int from = Math.Max(0, at - 120);
                    GD.Print($"SAVED:    {before.Substring(from, Math.Min(240, before.Length - from))}");
                    GD.Print($"RESTORED: {after.Substring(from, Math.Min(240, after.Length - from))}");
                }
                Check(before == after, $"the ship restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"ship-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Near(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            // ---- The places ----
            var site = world.ShipPlace;
            var ship = world.Ship;
            var yard = world.Shipyard;
            var island = world.TurtleIsland;
            var captain = world.Captain;
            Check(
                site != null && ship != null && yard != null && island != null && captain != null,
                "the valley has the shipyard, the ship, the turtle island and the captain"
            );
            GD.Print($"SHIP {world.ShipPlanNote}");
            Check(captain!.Role is CaptainRole && ship!.Captain == captain, "the captain keeps the ship");
            Check(world.ActorAt(World.CaptainId) == captain, "the captain has his actor ID");
            Check(world.ShipBuildable && world.ShipHasForge, "the works, the log yard and the forge are there");
            Check(
                World.InsideValley(site!.Edge) && World.InsideValley(site.Drop) && World.InsideValley(site.Talk),
                "the yard's ground, its drop and the captain's place are inside the valley"
            );
            Check(
                Math.Min(world.Routes.Distance(site.Drop), world.OutlandRoutes.Distance(site.Drop)) < 480,
                "a road leads near the yard"
            );
            Check(
                !World.InsideValley(site.Island) && Flat(site.Island, site.Waterline) > 240,
                "the island lies far out at sea"
            );
            Check(site.Buoys.Length >= 4, $"the way has {site.Buoys.Length} buoys");
            Check(
                site.Buoys.All(b => world.ShipDepth(b) > ShipPlans.LeastDepth)
                    && world.ShipDepth(site.Anchorage) > ShipPlans.LeastDepth,
                "the anchorage and every buoy lie in deep water"
            );
            Check(
                world.ShipDepth(site.Island) < 0 && world.ShipDepth(site.Edge) < 0,
                "the island and the land are not water for the ship"
            );
            Check(
                world.InShipZone(site.Edge)
                    && world.InShipZone(site.Talk)
                    && !world.InShipZone(site.Edge - site.Seaward * 60),
                "the yard keeps the plants off its ground and only there"
            );
            Check(
                world.LighthousePlace is not { } light || Flat(light.Foot, site.Waterline) > 140,
                "the yard keeps away from the lighthouse"
            );

            // ---- A saved game is made like the valley ----
            Check(
                ship.Stage == 0 && !ship.KeelShown && yard!.ScaffoldShown && !ship.Afloat,
                "at first only the slipway"
            );
            Frames(1);
            Check(captain.Guided, "the captain shows his mark");
            Rejected(data => data.Ship = null!, "no section of the ship");
            Rejected(data => data.Ship.Stage = -1, "a ship with a negative stage");
            Rejected(data => data.Ship.Stage = ShipPlans.Trips + 1, "a ship with fifteen loads");
            Rejected(data => data.Ship.Anchor = true, "an anchor forged before it is wanted");
            Rejected(data => data.Ship.Voyage = 1, "a voyage before the ship floats");
            Rejected(data => data.Ship.Kinds = new int[ShipPlans.Kinds - 1], "a cabin with seven kinds");
            Rejected(
                data =>
                {
                    data.Ship.Stage = ShipPlans.Trips;
                    data.Ship.Voyage = 1;
                    data.Ship.Kinds[0] = 3;
                },
                "three treasures from one voyage"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.ShipStatus;
                    data.Nearby = World.HunterId;
                },
                "the captain's panel at the hunter"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Ship, Index = 1 },
                "a trip for a second ship"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.ShipForge, Index = 0 },
                "stone for the anchor before the anchor is wanted"
            );

            // ---- Fourteen loads, and stone for the anchor ----
            int coinsBefore = game.Coins;
            for (int step = 0; step < ShipPlans.Trips; step++)
            {
                var cargo = ShipPlans.CargoOf(step);
                bool save = step is 0 or 3 or ShipPlans.AnchorStep or ShipPlans.FirstSailStep;
                foreach (var works in world.AllWorks)
                    works.SetStock(Works.Capacity);
                Near(site.Talk);
                game.OpenCaptain(captain);
                Check(
                    game.State == GameState.ShipStatus && Has("ShipLoads"),
                    $"the captain offers load {step + 1} of the ship"
                );
                if (cargo == HaulLoad.Anchor)
                {
                    var forge = world.LineForge!;
                    // The anchor is not forged: the green check mark sends stone to the forge first.
                    Check(Has("ShipForge") && Has("StartShipTrip"), "the captain offers stone for the anchor");
                    if (save)
                        RoundTrip("the captain's panel before the anchor");
                    game.BeginShipStart();
                    Check(game.State == GameState.Challenge, "stone for the anchor asks its quiz first");
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Loaded: false, ShipForge: not null } stone
                            && stone.Load == HaulLoad.Stones
                            && trailer.Attached,
                        "the empty trailer goes for stone"
                    );
                    if (save)
                        RoundTrip("a trip for stone for the anchor");
                    Near(game.Haul!.Source.Position);
                    Frames(1);
                    Check(game.State == GameState.Challenge, "the quarry asks the loading question");
                    ChallengeChecks.Solve(game);
                    Near(forge.Drop);
                    Frames(1);
                    Check(trailer.Dumping, "the bed tips before the forge");
                    Frames(40);
                    Check(
                        game.ShipAnchorForged && game.Haul == null && ship.Stage == step,
                        "the forge forges the anchor of the stone"
                    );
                    Check(world.ForgedAnchor is { Visible: true } && forge.Forging, "the anchor lies by the forge");
                    RoundTrip("the anchor at the forge");
                    Near(site.Talk);
                    game.OpenCaptain(captain);
                }
                else if (cargo == HaulLoad.Planks && step == 3)
                {
                    // Without planks the captain shows what the sawmill needs and sends the player for the logs.
                    foreach (var works in world.AllWorks)
                        works.SetStock(0);
                    game.OpenCaptain(captain);
                    Check(
                        !Has("StartShipTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                        "the captain shows what is missing when there are no planks"
                    );
                    game.BeginMissingSupply();
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Loaded: false, Works: not null },
                        "the captain sends the trailer for the raw material"
                    );
                    var noTrip = game.CaptureProgress();
                    noTrip.Task.Trip = null;
                    game.RestoreProgress(noTrip);
                    foreach (var works in world.AllWorks)
                        works.SetStock(Works.Capacity);
                    Near(site.Talk);
                    game.OpenCaptain(captain);
                }
                Check(Has("StartShipTrip"), $"load {step + 1}: the green check mark is there");
                if (save)
                    RoundTrip("the captain's panel");
                game.BeginShipStart();
                Check(game.State == GameState.Challenge, "the trip asks its quiz first");
                if (save)
                    RoundTrip("the quiz before a ship trip");
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Ship == ship && trip.Step == step && trailer.Attached,
                    $"load {step + 1} sends the empty trailer"
                );
                Check(trip!.Load == cargo, $"the trailer goes for {cargo}");
                Frames(1);
                Check(!captain.Guided, "the captain's mark is gone while the trailer is away");
                if (save)
                    RoundTrip($"a trip for the ship ({step + 1})");
                var source = game.Haul!.Source;
                if (cargo == HaulLoad.Anchor)
                    Check(
                        Flat(source.Position, world.LineForge!.Drop) < 1 && trip.Asker == captain,
                        "the anchor is loaded at the forge and the captain asks the question"
                    );
                if (cargo == HaulLoad.Fabric)
                    Check(trip.Asker == world.Village.Farmer, "the farmer gives the cloth for the sails");
                Near(source.Position);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the loading question");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (cargo == HaulLoad.Anchor)
                    Check(
                        !game.ShipAnchorForged && world.ForgedAnchor is { Visible: false },
                        "the anchor leaves the forge"
                    );
                if (save)
                    RoundTrip($"a full trailer for the ship ({step + 1})");
                Check(Flat(game.Haul.Destination, site.Drop) < 1, "the load goes to the edge of the yard");
                Near(site.Drop);
                Frames(1);
                Check(trailer.Dumping, "the bed tips at the yard");
                Frames(40);
                Check(ship.Stage == step + 1, $"load {step + 1} builds the {ShipPlans.Parts[step]}");
                Check(game.Haul == null && !game.TaskActive, "the next load waits for the player");
                Check(
                    ship.KeelShown
                        && ship.RibsShown == step >= 2
                        && ship.StrakesShown == Math.Clamp(step - 2, 0, ShipPlans.Rows)
                        && ship.DeckShown == step >= 7
                        && ship.CabinShown == step >= 8
                        && ship.AnchorShown == step >= ShipPlans.AnchorStep
                        && ship.MastsShown == step >= 11
                        && ship.SailsShown == step >= 13,
                    $"the ship shows what {step + 1} loads build"
                );
                if (step < ShipPlans.Trips - 1)
                    Check(game.Coins <= coinsBefore, "no payment before the ship is finished");
            }
            Check(ship.Complete && game.ShipLaunching, "the fourteenth load finishes the ship and launches it");
            Check(game.Coins >= coinsBefore + ShipPlans.Coins, "the ship pays when it is finished");
            var onSlip = ship.GlobalPosition;
            Frames(100);
            Check(
                !game.ShipLaunching && ship.Afloat && !yard!.ScaffoldShown,
                "the ship slides down the slipway and floats"
            );
            Check(
                Flat(ship.GlobalPosition, site.Anchorage) < 2 && Flat(onSlip, site.Anchorage) > 40,
                "the launched ship lies at its anchorage"
            );
            Check(
                Mathf.Abs(ship.GlobalPosition.Y - (world.SeaSurface - Ship.Draft)) < .5f,
                "the ship floats on the water"
            );
            Frames(1);
            Check(!captain.Guided, "the captain's mark is gone once the ship is finished");
            RoundTrip("a finished ship");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Ship, Index = 0 },
                "a trip to a ship that has no load left"
            );

            // ---- The voyage to the turtle island ----
            Near(site.Talk);
            game.OpenCaptain(captain);
            Check(Has("ShipCabin") && Has("ShipSailButton"), "the finished ship offers the voyage and the cabin");
            game.BeginShipSail();
            Check(game.State == GameState.Challenge, "the voyage asks a question first");
            RoundTrip("the question before the voyage");
            game.OpenCaptain(captain);
            game.BeginShipSail();
            ChallengeChecks.Solve(game);
            Check(game.ShipSailing && !car.Active && game.ShipVoyages == 1, "the ship sets sail with the car aboard");
            Check(island!.Waiting(0) || island.Waiting(1) || island.Waiting(2), "the turtles sit in their nests");
            Check(
                Enumerable.Range(0, ShipPlans.Nests).Count(island.Waiting) == ShipPlans.PerVoyage,
                "two turtles wait on the island"
            );
            // The steering turns the ship, the brake furls the sails.
            var voyage = game.ShipVoyageNow!;
            float heading = voyage.Helm.Heading;
            game.ShipInputOverride = new HelmInput(1, -1);
            Frames(20);
            Check(
                Mathf.Abs(Mathf.Wrap(voyage.Helm.Heading - heading, -Mathf.Pi, Mathf.Pi)) > .2f,
                "the steering turns the ship"
            );
            Check(voyage.Helm.Fill < .3f && ship.Fill < .3f, "the brake furls the sails");
            game.ShipInputOverride = new HelmInput(0, 1);
            Frames(40);
            Check(
                voyage.Helm.Fill > .9f && voyage.Helm.Autopilot,
                $"the accelerator fills the sails, the ship steers itself (fill {voyage.Helm.Fill:0.00}, idle {voyage.Helm.Idle:0.0}, shoal {voyage.Helm.Shoal})"
            );
            Check(Flat(car.Position, ship.GlobalPosition) < 6, "the car rides on the deck");
            Check(Flat(captain.Position, ship.GlobalPosition) < 14, "the captain stands at the wheel");
            float shallowest = float.MaxValue;
            for (int i = 0; i < 3000 && game.ShipSailing; i++)
            {
                Frames(1);
                if (game.ShipVoyageNow is { Docking: < 0 } now)
                    shallowest = Mathf.Min(shallowest, world.ShipDepth(now.Helm.Position));
            }
            Check(!game.ShipSailing && game.OnTurtleIsland, "the ship docks at the turtle island");
            Check(voyage.Rung >= 3, $"the buoys ring on the way ({voyage.Rung} of {site.Buoys.Length})");
            Check(shallowest > 0, $"the ship never sails onto land (shallowest {shallowest:0.0} m)");
            Check(car.Active && world.OnTurtleIsland(car.Position), "the car drives off onto the island");
            Check(Flat(ship.GlobalPosition, site.IslandBerth) < 2, "the ship lies at the island's pier");
            Check(
                world.SeaDepth(site.Island) < 0 && float.IsInfinity(world.SeaDepth(site.Anchorage)),
                "only the island is open to the car"
            );

            // ---- The turtles ----
            int found = 0;
            for (int nest = 0; nest < ShipPlans.Nests; nest++)
            {
                if (!island.Waiting(nest))
                    continue;
                int kind = island.KindIn(nest);
                int before = game.ShipKinds[kind];
                int coins = game.Coins;
                car.SetPose(island.GroundAt(island.NestAt(nest)) + Vector3.Up * .3f, 0);
                Frames(1);
                Check(game.State == GameState.Challenge, $"the turtle of nest {nest} asks its question");
                ChallengeChecks.Solve(game);
                Check(
                    !island.Waiting(nest) && game.ShipKinds[kind] == before + 1,
                    $"the turtle leaves a {ShipPlans.Name(kind)}"
                );
                Check(game.Coins >= coins + ShipPlans.SouvenirCoins, "a treasure brings coins");
                Check(game.State == GameState.Exploring && car.Active, "the car drives on");
                found++;
                Frames(60);
            }
            Check(
                found == ShipPlans.PerVoyage && game.ShipKinds.Sum() == ShipPlans.PerVoyage,
                "two treasures a voyage"
            );
            Check(
                game.ShipKinds.Count(k => k > 0) == ShipPlans.PerVoyage,
                "the first voyage brings two kinds, one of them new"
            );
            // A save on the island puts the car at the shipyard.
            {
                var saved = game.CaptureProgress();
                var at = new Vector3(saved.Vehicle.Position[0], saved.Vehicle.Position[1], saved.Vehicle.Position[2]);
                Check(Flat(at, site.Return) < 1, "a save on the island puts the car at the shipyard");
                Check(SaveRules.All.All(rule => !rule.Invalid(saved)), "a save on the island is accepted");
            }

            // ---- Home ----
            car.SetPose(island.GroundAt(site.IslandTalk) + Vector3.Up * .3f, 0);
            game.OpenCaptain(captain);
            Check(
                game.State == GameState.ShipStatus && Has("ShipHomeSail"),
                "on the island the captain offers the way home"
            );
            game.BeginShipHome();
            Check(game.ShipSailing && game.ShipVoyageNow!.Home, "the ship sails home without a question");
            game.ShipInputOverride = new HelmInput(0, 0);
            for (int i = 0; i < 3000 && game.ShipSailing; i++)
                Frames(1);
            Check(!game.ShipSailing && !game.OnTurtleIsland, "the ship comes home");
            Check(Flat(car.Position, site.Return) < 2 && car.Active, "the car stands at the shipyard");
            Check(Flat(ship.GlobalPosition, site.Anchorage) < 2, "the ship lies at its anchorage again");
            Check(Flat(captain.Position, site.Talk) < 1, "the captain waits at the yard");
            RoundTrip("after a voyage");

            // ---- A save made at sea ----
            Near(site.Talk);
            game.OpenCaptain(captain);
            game.BeginShipSail();
            ChallengeChecks.Solve(game);
            Frames(30);
            Check(game.ShipSailing, "at sea again");
            {
                var saved = game.CaptureProgress();
                var at = new Vector3(saved.Vehicle.Position[0], saved.Vehicle.Position[1], saved.Vehicle.Position[2]);
                Check(Flat(at, site.Return) < 1, "a save at sea puts the car at the shipyard");
                game.RestoreProgress(saved);
                Check(!game.ShipSailing && !game.OnTurtleIsland, "the loaded game has no voyage");
                Check(Flat(ship.GlobalPosition, site.Anchorage) < 2, "the ship lies at its anchorage");
                Check(game.ShipVoyages == 2, "the voyage was counted");
            }
            game.ShipInputOverride = null;

            // ---- The full cabin ----
            {
                var data = game.CaptureProgress();
                data.Ship.Voyage = 10;
                data.Ship.Kinds = Enumerable.Repeat(1, ShipPlans.Kinds).ToArray();
                game.RestoreProgress(data);
                Check(ship.CatShown, "with every treasure the cat Kormidlík sits on the bow");
                RoundTrip("a full cabin");
            }
            Near(Vector3.Zero);

            // ---- A save from before the ship ----
            {
                var legacy = game.CaptureProgress();
                string file = DiagnosticArtifacts.PathFor("ship-checks/legacy.json");
                var node = JsonSerializer.SerializeToNode(legacy)!.AsObject();
                node.Remove("Ship");
                File.WriteAllText(file, node.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read != null && read.Ship.Stage == 0 && read.Ship.Voyage == 0 && read.Ship.Kinds.Sum() == 0,
                    "a save from before the ship loads without it"
                );
                game.RestoreProgress(read!);
                Check(
                    world.Ship!.Stage == 0 && !world.Ship.Afloat && yard!.ScaffoldShown,
                    "and the ship starts from nothing"
                );
            }
            GD.Print("SHIP_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
