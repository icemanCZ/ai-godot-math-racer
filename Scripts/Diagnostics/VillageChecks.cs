using Godot;
using System;
using System.Linq;
using System.IO;

namespace MathRacer;

public partial class VillageChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public VillageChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
    private void RoundTrip(string path)
    {
        var expected = game.CaptureProgress();
        Check(SaveStore.Write(expected, path), "progress written to disk");
        var loaded = SaveStore.Read(path);
        Check(loaded != null, "village interaction passes save validation");
        game.RestoreProgress(loaded!);
        Check(System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress()) == System.Text.Json.JsonSerializer.Serialize(expected),
            "house schedule position, interaction and fruit progress restore exactly");
    }
    private void GatherAll()
    {
        foreach (var point in game.Delivery!.PickupPositions) { car.SetPose(point, 0); game.Delivery.Tick(.25); }
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
                    if (River.Distance(x, z) < River.HalfWidth(z) + 25) continue;
                    if (Mathf.Abs(world.Ground.Height(x, z) - villageHeight) > .025f) levelSettlement = false;
                }
            Check(levelSettlement, "village and connecting streets share a level terrain platform");
            foreach (var (building, size) in village.BuildingFoundations)
            {
                var origin = building.Position;
                Check(Mathf.Abs(world.Ground.Height(origin.X, origin.Z) - villageHeight) < .025f,
                    building.Name + " stands at the shared village elevation");
            }
            bool squareClear = true;
            for (float x = -11; x <= 11; x += .5f)
                for (float z = -6.5f; z <= 6.5f; z += .5f)
                {
                    var p = village.Square!.Transform * new Vector3(x, 0, z);
                    if (world.Routes.Contains(p) || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 5 || !village.IsReserved(p)) squareClear = false;
                }
            Check(squareClear, "square stays off roads and water and excludes generated vegetation");
            var pavingPoint = village.Square!.Transform * new Vector3(0, 0, 4);
            pavingPoint.Y = world.Ground.Height(pavingPoint.X, pavingPoint.Z) + .065f;
            var pavingRay = PhysicsRayQueryParameters3D.Create(pavingPoint + Vector3.Up * .2f, pavingPoint + Vector3.Down * .2f);
            var pavingHit = world.GetWorld3D().DirectSpaceState.IntersectRay(pavingRay);
            Check(pavingHit.Count > 0 && Mathf.Abs(pavingHit["position"].AsVector3().Y - pavingPoint.Y) < .02f,
                "square paving has a drivable collider at the visible surface");
            foreach (var resident in village.Residents.Concat(new[] { village.Farmer, village.Gardener, village.GemCollector, village.Toymaker }))
            {
                var ray = PhysicsRayQueryParameters3D.Create(resident.Position + Vector3.Up * .25f, resident.Position + Vector3.Down * .25f);
                var hit = world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                Check(hit.Count > 0 && Mathf.Abs(hit["position"].AsVector3().Y - resident.Position.Y) < .08f,
                    resident.DisplayName + " stands on the terrain or raised walkway collider");
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
                        if (world.Ground.Height(p.X, p.Z) >= p.Y) clear = false;
                        if (world.Routes.Contains(p)) streetsClear = false;
                    }
                Check(clear && building.GetNodeOrNull<MeshInstance3D>("Foundation") != null,
                    building.Name + " sits above terrain across its entire foundation");
                Check(streetsClear, building.Name + " leaves the village streets clear");
            }
            string path = ProjectSettings.GlobalizePath("res://artifacts/village-checks/progress.json");
            Check(village.Residents.Count == 4 && village.Houses.Count == 4, "each house has a dedicated resident");
            Check(world.Animals.Length == 10 && Village.TaskAnimalClearance == 100 && world.Animals.All(a => !village.IsNearVillage(a.Position)), "ten standard task animals stand at least 100 metres from every village plot");
            for (int seed = 1; seed <= 128; seed++)
                for (int house = 0; house < 4; house++)
                {
                    var plan = Village.CreateTasks(seed, house);
                    if (plan.Length != 10 || Enum.GetValues<VillageTaskKind>().Any(kind => plan.Count(t => t == kind) is < 3 or > 4) ||
                        !plan.SequenceEqual(Village.CreateTasks(seed, house)))
                        throw new InvalidOperationException("Invalid village task composition or unstable shuffle");
                }
            Check(!Village.CreateTasks(1, 0).SequenceEqual(Village.CreateTasks(2, 0)) &&
                !Village.CreateTasks(42, 0).SequenceEqual(Village.CreateTasks(42, 1)), "task order varies by seed and house");
            Check(true, "512 schedules contain three tasks of each type plus one random type");
            car.SetPose(village.Residents[0].Position + Vector3.Right * 2, 0); game._Process(.016);
            Check(village.Residents[0].IsWaving, "resident waves at the passing player");
            game.OpenChallenge(world.Animals[0]); game.SubmitAnswer(game.CurrentChallenge!.Answer); game.SelectExpedition(true);
            var roadside = game.Delivery;
            int firstFruit = village.Tasks(0).ToList().IndexOf(VillageTaskKind.Fruit);
            village.RestoreLevels(new[] { firstFruit, 0, 0, 0 });
            game.OpenChallenge(village.Residents[0]);
            Check(game.State == GameState.HouseFruitOffer && game.FindChild("VillageFruitTask", true, false) == null, "fruit offer waits while another expedition is active");
            game.BeginVillageCollection(); Check(game.Delivery == roadside, "village task cannot replace another expedition");
            game.FinishInteraction(); GatherAll(); game.SubmitAnswer(3); game.FinishInteraction();
            village.RestoreLevels(new int[4]);
            int remaining = game.RemainingAnimals, deliveries = game.RemainingDeliveries, owned = game.OwnedRewards.Count;
            for (int house = 0; house < 4; house++)
            {
                var owner = village.Residents[house];
                var schedule = village.Tasks(house).ToArray();
                for (int level = 0; level < 10; level++)
                {
                    car.SetPose(owner.Position + Vector3.Right * 2, 0); game.OpenChallenge(owner);
                    var kind = schedule[level];
                    Check(kind == VillageTaskKind.Fruit ? game.State == GameState.HouseFruitOffer :
                        game.State == GameState.Challenge && (kind == VillageTaskKind.Math ? game.CurrentChallenge is MathChallenge : game.CurrentChallenge is LetterChallenge),
                        $"house {house} task {level + 1} follows its shuffled {kind} schedule");
                    game.FinishInteraction(); game.OpenChallenge(owner);
                    Check(village.Level(house) == level && village.TaskAt(house) == kind, "postponing a task preserves its order and level");
                    game.TogglePause(); RoundTrip(path);
                    if (kind == VillageTaskKind.Fruit)
                    {
                        ((Button)game.FindChild("VillageFruitTask", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
                        Check(game.Delivery?.Sender == owner && game.Delivery.Recipient == owner && game.Delivery.Collected == 0, "fruit task starts with an empty platform and returns to the owner");
                        game.OpenChallenge(owner);
                        Check(game.State == GameState.HouseCollection && village.Level(house) == level, "returning without fruit does not upgrade or open a replacement quiz");
                        RoundTrip(path); game.FinishInteraction();
                        car.SetPose(game.Delivery!.PickupPositions[0], 0); game.Delivery.Tick(.25); game.Delivery.Tick(.25);
                        Check(game.Delivery.Collected == 1, "one fruit can only be picked up once");
                        game.TogglePause(); RoundTrip(path);
                        car.Reset(); game.TogglePause(); game.TogglePause();
                        Check(game.Delivery!.Collected == 1, "reset and pause preserve collected village fruit");
                        GatherAll();
                        Check(village.Level(house) == level && game.CurrentChallenge is DeliveryChallenge, "all fruit must be returned and counted before the upgrade");
                    }
                    int answer = game.CurrentChallenge!.Answer;
                    game.SubmitAnswer(game.CurrentChallenge.Choices.First(c => c != answer));
                    Check(game.State == GameState.Challenge && village.Level(house) == level, "wrong answer preserves the task without granting an upgrade");
                    RoundTrip(path);
                    var returning = game.Delivery;
                    game.SubmitAnswer(answer);
                    Check(game.State == GameState.HouseReward && village.Level(house) == level + 1 && game.Delivery == null,
                        "completed task grants exactly one house upgrade");
                    if (returning != null) Check(!returning.CargoVisible, "fruit handover removes cargo immediately");
                    game.SubmitAnswer(answer); Check(village.Level(house) == level + 1, "duplicate answer grants no extra upgrade");
                    var parts = village.Houses[house].GetNode<Node3D>("Upgrades").GetChildren().Cast<Node3D>();
                    Check(parts.Count(n => n.Visible) == level + 1, "earned house additions are visible");
                    RoundTrip(path);
                    Check(schedule.SequenceEqual(village.Tasks(house)), "saved game preserves the complete task order");
                    game.FinishInteraction();
                }
                game.OpenChallenge(owner);
                Check(game.State == GameState.HouseReward && !owner.RewardAvailable, "completed house offers thanks without further tasks");
                game.FinishInteraction();
            }
            Check(game.RemainingAnimals == remaining && game.RemainingDeliveries == deliveries && game.OwnedRewards.Count == owned,
                "village tasks preserve original animal visits, expedition counts and cosmetics");
            var oldJson = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
            oldJson["State"] = (int)GameState.Exploring; oldJson.AsObject().Remove("HouseLevels");
            File.WriteAllText(path, oldJson.ToJsonString());
            Check(SaveStore.Read(path)?.HouseLevels.All(l => l == 0) == true, "old saves start with zero house upgrades");
            village.Residents[0].GreetPlayer(village.Residents[0].Position, false);
            Check(!village.Residents[0].IsWaving, "waving stops outside exploration");
            GD.Print("VILLAGE_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
