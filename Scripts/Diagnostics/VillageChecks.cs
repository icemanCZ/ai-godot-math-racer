using System;
using System.IO;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class VillageChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;

    public VillageChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private void RoundTrip(string path)
    {
        var expected = game.CaptureProgress();
        Check(SaveStore.Write(expected, path), "progress written to disk");
        var loaded = SaveStore.Read(path);
        Check(loaded != null, "village interaction passes save validation");
        game.RestoreProgress(loaded!);
        Check(
            System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress())
                == System.Text.Json.JsonSerializer.Serialize(expected),
            "house schedule position, interaction and fruit progress restore exactly"
        );
    }

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    private void DriveTo(Vector3 point)
    {
        car.SetPose(world.Ground.At(point.X, point.Z, .15f), 0);
        Frames(1);
    }

    /// <summary>
    /// A resident who wants planks or bricks: the trailer fetches them from the works' stock and tips them out in
    /// front of the house. With <paramref name="missingFirst"/> the works is empty, so the resident sends the player
    /// for the raw material first.
    /// </summary>
    private void CompleteMaterialTask(int house, int level, HaulLoad goods, string path, bool missingFirst)
    {
        var village = world.Village;
        var owner = village.Residents[house];
        var works = world.Industry[(int)HaulLoads.ProductOf(goods)];
        var trailer = game.Trailer;
        string what = $"house {house} task {level + 1} ({goods})";
        game.FinishInteraction();
        works.SetStock(missingFirst ? 0 : Works.Capacity);
        car.SetPose(owner.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(owner);
        Check(game.State == GameState.HouseMaterialOffer, $"{what}: the resident asks for goods");
        if (missingFirst)
        {
            Check(
                Has("StartMissingSupply") && !Has("StartHouseTrip") && Has("MissingGoods"),
                $"{what}: an empty works shows the raw material instead of the play button"
            );
            RoundTrip(path);
            game.BeginHouseTrip();
            Check(game.Haul == null, $"{what}: no trip starts without goods");
            game.BeginMissingSupply();
            var fetch = game.Haul as SupplyTrip;
            Check(
                fetch is { Loaded: false } && fetch.Works == works && trailer.Attached,
                $"{what}: the resident sends the empty trailer for the raw material"
            );
            DriveTo(world.SourceFor(works.Spec.Input).Position);
            Check(
                game.State == GameState.Challenge && fetch!.Asker == world.SourceWorker(works.Spec.Input),
                $"{what}: the worker at the source asks the loading question"
            );
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Loaded: true } && trailer.Load == works.Spec.Input, $"{what}: raw material loaded");
            RoundTrip(path);
            DriveTo(works.Site.Position);
            Frames(40);
            Check(
                works.Stock == Works.PerLoad && game.Haul == null && village.Level(house) == level,
                $"{what}: the works makes the goods"
            );
            car.SetPose(owner.Position + Vector3.Right * 2, 0);
            game.OpenChallenge(owner);
            Check(game.State == GameState.HouseMaterialOffer, $"{what}: back at the resident");
        }
        Check(
            Has("StartHouseTrip") && !Has("MissingGoods") && !Has("StartMissingSupply"),
            $"{what}: with goods in stock the play button sends the trailer"
        );
        game.BeginHouseTrip();
        var trip = game.Haul as HouseTrip;
        Check(
            trip is { Loaded: false } && trip.House == house && trip.Load == goods && trailer.Attached,
            $"{what}: the empty trailer is sent to the works"
        );
        Check(
            game.State == GameState.Exploring && trip!.Asker == works.Worker && trip.Contractor == owner,
            $"{what}: the works' worker loads it for the resident"
        );
        RoundTrip(path);
        car.SetPose(owner.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(owner);
        Check(
            game.State == GameState.HouseMaterialOffer
                && !Has("StartHouseTrip")
                && game.Haul is HouseTrip { House: var tripHouse }
                && tripHouse == house,
            $"{what}: talking to the resident during the trip shows it and starts no second one"
        );
        game.FinishInteraction();
        DriveTo(world.SourceFor(goods).Position);
        Check(game.State == GameState.Challenge && game.CurrentChallenge != null, $"{what}: arriving asks a question");
        if (game.CurrentChallenge is LoadChallenge load)
            Check(load.Cargo == goods && load.IsValid(), $"{what}: the loading question matches the goods");
        RoundTrip(path);
        int before = works.Stock;
        ChallengeChecks.Solve(game);
        Check(
            game.Haul is { Loaded: true } && trailer.Load == goods && works.Stock == before - Works.PerTrip,
            $"{what}: goods leave the stock when loaded"
        );
        Check(village.Level(house) == level, $"{what}: loading alone does not improve the house");
        RoundTrip(path);
        DriveTo(village.HouseDrop(house));
        Check(trailer.Dumping && !car.Active, $"{what}: the bed tips in front of the house");
        Frames(40);
        Check(
            village.Level(house) == level + 1
                && game.State == GameState.HouseReward
                && game.Haul == null
                && trailer.Load == null
                && !game.TaskActive,
            $"{what}: the delivered goods improve the house exactly once"
        );
    }

    private void GatherAll()
    {
        foreach (var point in game.Delivery!.PickupPositions)
        {
            car.SetPose(point, 0);
            game.Delivery.Tick(.25);
        }
        car.SetPose(game.Delivery.Recipient.Position + Vector3.Right * 2, 0);
        game.BeginDeliveryQuestion();
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var village = world.Village;
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(village.Square != null, "village has a public square");
            float villageHeight = world.Ground.Height(village.Center.X, village.Center.Z);
            bool levelSettlement = true;
            var flatArea = village.TerrainArea;
            for (float x = flatArea.Position.X; x <= flatArea.End.X; x += 2.5f)
            for (float z = flatArea.Position.Y; z <= flatArea.End.Y; z += 2.5f)
            {
                // River banks keep their channel profile outside the inhabited parcels.
                if (River.Distance(x, z) < River.HalfWidth(z) + 25)
                    continue;
                if (Mathf.Abs(world.Ground.Height(x, z) - villageHeight) > .025f)
                    levelSettlement = false;
            }
            Check(levelSettlement, "village and connecting streets share a level terrain platform");
            foreach (var (building, size) in village.BuildingFoundations)
            {
                var origin = building.Position;
                Check(
                    Mathf.Abs(world.Ground.Height(origin.X, origin.Z) - villageHeight) < .025f,
                    building.Name + " stands at the shared village elevation"
                );
            }
            bool squareClear = true;
            for (float x = -11; x <= 11; x += .5f)
            for (float z = -6.5f; z <= 6.5f; z += .5f)
            {
                var p = village.Square!.Transform * new Vector3(x, 0, z);
                if (
                    world.Routes.Contains(p)
                    || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 5
                    || !village.IsReserved(p)
                )
                    squareClear = false;
            }
            Check(squareClear, "square stays off roads and water and excludes generated vegetation");
            var pavingPoint = village.Square!.Transform * new Vector3(0, 0, 4);
            pavingPoint.Y = world.Ground.Height(pavingPoint.X, pavingPoint.Z) + .065f;
            var pavingRay = PhysicsRayQueryParameters3D.Create(
                pavingPoint + Vector3.Up * .2f,
                pavingPoint + Vector3.Down * .2f
            );
            var pavingHit = world.GetWorld3D().DirectSpaceState.IntersectRay(pavingRay);
            Check(
                pavingHit.Count > 0 && Mathf.Abs(pavingHit["position"].AsVector3().Y - pavingPoint.Y) < .02f,
                "square paving has a drivable collider at the visible surface"
            );
            foreach (
                var resident in village.Residents.Concat(
                    new[] { village.Farmer, village.Gardener, village.GemCollector, village.Toymaker }
                )
            )
            {
                var ray = PhysicsRayQueryParameters3D.Create(
                    resident.Position + Vector3.Up * .25f,
                    resident.Position + Vector3.Down * .25f
                );
                var hit = world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                Check(
                    hit.Count > 0 && Mathf.Abs(hit["position"].AsVector3().Y - resident.Position.Y) < .08f,
                    resident.DisplayName + " stands on the terrain or raised walkway collider"
                );
            }
            Check(village.BuildingFoundations.Count == 12, "all homes, workshops and the garage have foundations");
            foreach (var (building, size) in village.BuildingFoundations)
            {
                bool clear = true;
                bool streetsClear = true;
                for (float x = -size.X / 2; x <= size.X / 2; x += .2f)
                for (float z = -size.Y / 2; z <= size.Y / 2; z += .2f)
                {
                    var p = building.Transform * new Vector3(x, 0, z);
                    if (world.Ground.Height(p.X, p.Z) >= p.Y)
                        clear = false;
                    if (world.Routes.Contains(p))
                        streetsClear = false;
                }
                Check(
                    clear && building.GetNodeOrNull<MeshInstance3D>("Foundation") != null,
                    building.Name + " sits above terrain across its entire foundation"
                );
                Check(streetsClear, building.Name + " leaves the village streets clear");
            }
            string path = DiagnosticArtifacts.PathFor("village-checks/progress.json");
            Check(village.Residents.Count == 4 && village.Houses.Count == 4, "each house has a dedicated resident");
            Check(
                world.Animals.Length == 10
                    && Village.TaskAnimalClearance == 100
                    && world.Animals.All(a => !village.IsNearVillage(a.Position)),
                "ten standard task animals stand at least 100 metres from every village plot"
            );
            for (int seed = 1; seed <= 128; seed++)
            for (int house = 0; house < 4; house++)
            {
                var plan = Village.CreateTasks(seed, house);
                if (
                    plan.Length != 10
                    || Enum.GetValues<VillageTaskKind>().Any(kind => plan.Count(t => t == kind) != Village.TasksPerKind)
                    || !plan.SequenceEqual(Village.CreateTasks(seed, house))
                )
                    throw new InvalidOperationException("Invalid village task composition or unstable shuffle");
            }
            Check(
                !Village.CreateTasks(1, 0).SequenceEqual(Village.CreateTasks(2, 0))
                    && !Village.CreateTasks(42, 0).SequenceEqual(Village.CreateTasks(42, 1)),
                "task order varies by seed and house"
            );
            Check(
                true,
                "512 schedules contain two quizzes of each kind, two apple tasks, two of planks and two of bricks"
            );
            Check(
                world.Industry.Length == WorksCatalog.Count
                    && Village.MaterialOf(VillageTaskKind.Planks) == HaulLoad.Planks
                    && Village.MaterialOf(VillageTaskKind.Bricks) == HaulLoad.Bricks
                    && Village.MaterialOf(VillageTaskKind.Fruit) == null
                    && Village.MaterialOf(VillageTaskKind.Math) == null,
                "planks and bricks are the goods that the houses ask for"
            );
            for (int house = 0; house < 4; house++)
            {
                var drop = village.HouseDrop(house);
                var door = village.Houses[house].GlobalPosition;
                Check(
                    new Vector2(drop.X - door.X, drop.Z - door.Z).Length() is > 6 and < 12
                        && world.Routes.Distance(drop) < 5
                        && Mathf.Abs(drop.Y - world.Ground.Height(drop.X, drop.Z)) < .05f,
                    $"house {house} has a drop ring beside the road in front of it"
                );
            }
            car.SetPose(village.Residents[0].Position + Vector3.Right * 2, 0);
            game._Process(.016);
            Check(village.Residents[0].IsWaving, "resident waves at the passing player");
            game.OpenChallenge(world.Animals[0]);
            ChallengeChecks.Solve(game);
            game.SelectExpedition(true);
            var roadside = game.Delivery;
            int firstFruit = village.Tasks(0).ToList().IndexOf(VillageTaskKind.Fruit);
            village.RestoreLevels(new[] { firstFruit, 0, 0, 0 });
            game.OpenChallenge(village.Residents[0]);
            Check(
                game.State == GameState.HouseFruitOffer && game.FindChild("VillageFruitTask", true, false) != null,
                "fruit offer stays available while another expedition is active"
            );
            game.BeginVillageCollection();
            Check(
                game.Delivery != roadside && game.Delivery?.Sender == village.Residents[0],
                "village task replaces another expedition"
            );
            game.OpenChallenge(world.Animals[0]);
            game.SelectExpedition(true);
            roadside = game.Delivery;
            Check(roadside?.Sender == world.Animals[0], "a roadside order replaces the village task");
            game.FinishInteraction();
            GatherAll();
            game.SubmitAnswer(3);
            game.FinishInteraction();
            village.RestoreLevels(new int[4]);
            int remaining = game.RemainingAnimals,
                deliveries = game.RemainingDeliveries,
                owned = game.OwnedRewards.Count;
            for (int house = 0; house < 4; house++)
            {
                var owner = village.Residents[house];
                var schedule = village.Tasks(house).ToArray();
                for (int level = 0; level < 10; level++)
                {
                    car.SetPose(owner.Position + Vector3.Right * 2, 0);
                    game.OpenChallenge(owner);
                    var kind = schedule[level];
                    Check(
                        kind == VillageTaskKind.Fruit ? game.State == GameState.HouseFruitOffer
                            : Village.MaterialOf(kind) != null ? game.State == GameState.HouseMaterialOffer
                            : game.State == GameState.Challenge && game.CurrentChallenge != null,
                        $"house {house} task {level + 1} follows its shuffled {kind} schedule"
                    );
                    game.FinishInteraction();
                    game.OpenChallenge(owner);
                    Check(
                        village.Level(house) == level && village.TaskAt(house) == kind,
                        "postponing a task preserves its order and level"
                    );
                    game.TogglePause();
                    RoundTrip(path);
                    if (kind == VillageTaskKind.Fruit)
                    {
                        ((Button)game.FindChild("VillageFruitTask", true, false)).EmitSignal(
                            BaseButton.SignalName.Pressed
                        );
                        Check(
                            game.Delivery?.Sender == owner
                                && game.Delivery.Recipient == owner
                                && game.Delivery.Collected == 0,
                            "fruit task starts with an empty platform and returns to the owner"
                        );
                        game.OpenChallenge(owner);
                        Check(
                            game.State == GameState.HouseCollection && village.Level(house) == level,
                            "returning without fruit does not upgrade or open a replacement quiz"
                        );
                        RoundTrip(path);
                        game.FinishInteraction();
                        car.SetPose(game.Delivery!.PickupPositions[0], 0);
                        game.Delivery.Tick(.25);
                        game.Delivery.Tick(.25);
                        Check(game.Delivery.Collected == 1, "one fruit can only be picked up once");
                        game.TogglePause();
                        RoundTrip(path);
                        car.Reset();
                        game.TogglePause();
                        game.TogglePause();
                        Check(game.Delivery!.Collected == 1, "reset and pause preserve collected village fruit");
                        GatherAll();
                        Check(
                            village.Level(house) == level && game.CurrentChallenge is DeliveryChallenge,
                            "all fruit must be returned and counted before the upgrade"
                        );
                    }
                    if (Village.MaterialOf(kind) is { } goods)
                        CompleteMaterialTask(
                            house,
                            level,
                            goods,
                            path,
                            // The first goods task of each house meets an empty works.
                            missingFirst: level == Array.FindIndex(schedule, k => Village.MaterialOf(k) != null)
                        );
                    else
                    {
                        int answer = game.CurrentChallenge!.Choices.First(game.CurrentChallenge.Accepts);
                        game.SubmitAnswer(
                            game.CurrentChallenge.Choices.First(choice => !game.CurrentChallenge.Accepts(choice))
                        );
                        Check(
                            game.State == GameState.Challenge && village.Level(house) == level,
                            "wrong answer preserves the task without granting an upgrade"
                        );
                        RoundTrip(path);
                        var returning = game.Delivery;
                        ChallengeChecks.Solve(game);
                        Check(
                            game.State == GameState.HouseReward
                                && village.Level(house) == level + 1
                                && game.Delivery == null,
                            "completed task grants exactly one house upgrade"
                        );
                        if (returning != null)
                            Check(!returning.CargoVisible, "fruit handover removes cargo immediately");
                        game.SubmitAnswer(answer);
                        Check(village.Level(house) == level + 1, "duplicate answer grants no extra upgrade");
                    }
                    var parts = village.Houses[house].GetNode<Node3D>("Upgrades").GetChildren().Cast<Node3D>();
                    Check(parts.Count(n => n.Visible) == level + 1, "earned house additions are visible");
                    RoundTrip(path);
                    Check(schedule.SequenceEqual(village.Tasks(house)), "saved game preserves the complete task order");
                    game.FinishInteraction();
                }
                game.OpenChallenge(owner);
                Check(
                    game.State == GameState.HouseReward && !owner.RewardAvailable,
                    "completed house offers thanks without further tasks"
                );
                game.FinishInteraction();
            }
            Check(
                game.RemainingAnimals == remaining
                    && game.RemainingDeliveries == deliveries
                    && game.OwnedRewards.Count == owned,
                "village tasks preserve original animal visits, expedition counts and cosmetics"
            );
            var oldJson = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            oldJson["State"] = (int)GameState.Exploring;
            oldJson.AsObject().Remove("HouseLevels");
            File.WriteAllText(path, oldJson.ToJsonString());
            Check(
                SaveStore.Read(path)?.HouseLevels.All(l => l == 0) == true,
                "old saves start with zero house upgrades"
            );

            // ---- Saves around the goods tasks ----
            int invalidFiles = 0;
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"village-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            SaveData Accepted(Action<SaveData> change, string what)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"village-checks/valid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file), "save written");
                var read = SaveStore.Read(file);
                Check(read != null, $"accepted: {what}");
                return read!;
            }
            var plan0 = village.Tasks(0).ToList();
            var plan1 = village.Tasks(1).ToList();
            int quizLevel = plan0.FindIndex(k => Village.MaterialOf(k) == null && k != VillageTaskKind.Fruit);
            int materialLevel = plan1.FindIndex(k => Village.MaterialOf(k) != null);
            village.RestoreLevels(new[] { quizLevel, materialLevel, 0, 0 });
            var clean = game.CaptureProgress();
            Rejected(data => data.HouseTrip = 4, "a trip for a fifth house");
            Rejected(data => data.HouseTripLoaded = true, "a loaded house trip without a house");
            Rejected(data => data.HouseTrip = 0, "a house trip for a house whose next task is a quiz");
            Rejected(
                data =>
                {
                    data.HouseTrip = 1;
                    data.BuildTrip = BuildPlans.Bridge;
                },
                "a house trip together with a building trip"
            );
            Rejected(
                data =>
                {
                    data.HouseTrip = 1;
                    data.SupplyTrip = 0;
                },
                "a house trip together with a supply trip"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.HouseMaterialOffer;
                    data.Nearby = World.FirstResidentId;
                },
                "an offer of goods at a house that asks for a quiz"
            );
            Accepted(
                data =>
                {
                    data.State = GameState.HouseMaterialOffer;
                    data.Nearby = World.FirstResidentId + 1;
                },
                "an offer of goods at a house that asks for them"
            );
            var tripSave = Accepted(
                data =>
                {
                    data.HouseTrip = 1;
                    data.HouseTripLoaded = true;
                },
                "a loaded house trip"
            );
            game.RestoreProgress(tripSave);
            Check(
                game.Haul is HouseTrip { House: 1, Loaded: true }
                    && game.Trailer.Load == Village.MaterialOf(plan1[materialLevel]),
                "a loaded house trip comes back with its load on the trailer"
            );
            game.RestoreProgress(clean);
            Check(game.Haul == null && !game.TaskActive, "a save without a trip drops the trip");
            game.RestoreProgress(
                Accepted(
                    data =>
                    {
                        data.State = GameState.HouseFruitOffer;
                        data.Nearby = World.FirstResidentId;
                    },
                    "a fruit offer at a house whose task changed to something else"
                )
            );
            Check(
                game.State == GameState.Exploring && game.Haul == null,
                "that stale fruit offer goes back to driving"
            );
            car.SetPose(world.Animals[5].Position + Vector3.Right * 2, 0);
            game.OpenChallenge(world.Animals[5]);
            Check(game.State == GameState.Challenge, "a roadside quiz is open");
            var staleQuiz = game.CaptureProgress();
            staleQuiz.Nearby = World.FirstResidentId + 1;
            string staleFile = DiagnosticArtifacts.PathFor("village-checks/stale-quiz.json");
            Check(SaveStore.Write(staleQuiz, staleFile), "save written");
            var migrated = SaveStore.Read(staleFile);
            Check(
                migrated is { State: GameState.Exploring, Nearby: -1, Challenge: null },
                "a house quiz saved before the house asked for goods goes back to driving"
            );
            game.FinishInteraction();
            village.RestoreLevels(new[] { 10, 10, 10, 10 });
            village.Residents[0].GreetPlayer(village.Residents[0].Position, false);
            Check(!village.Residents[0].IsWaving, "waving stops outside exploration");
            GD.Print("VILLAGE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
