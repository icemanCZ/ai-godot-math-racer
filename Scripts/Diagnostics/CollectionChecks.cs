using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MathRacer;

public partial class CollectionChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public CollectionChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var village = world.Village;
            string path = ProjectSettings.GlobalizePath("res://artifacts/collection-checks/progress.json");
            void RoundTrip()
            {
                var expected = game.CaptureProgress();
                Check(SaveStore.Write(expected, path), "collection progress saved");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, "collection save validated");
                game.RestoreProgress(loaded!);
                Check(JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(game.CaptureProgress()), "quiz, cargo and both collections restore exactly");
            }
            foreach (var building in new[] { village.MuseumBuilding, village.ToyBuilding })
                for (float x = -4; x <= 16; x += 2)
                    for (float z = -6; z <= 6; z += 2)
                        Check(!world.Routes.Contains(building.ToGlobal(new(x, 0, z))), "collection building and display grounds stay off roads");
            Check(world.SpecialPickupSpots.Length == 48, "all four residents have twelve off-road targets");
            foreach (var spot in world.SpecialPickupSpots)
            {
                Check(!world.Routes.Contains(spot) && world.Routes.Distance(spot) >= 7.5f && !world.Plants.NearTrunk(spot, 2.8f), "pickup stays off-road and clear of trees");
                Check(world.Routes.Distance(spot) >= 16 && world.Animals.Concat(village.Residents).All(a =>
                    new Vector2(a.Position.X - spot.X, a.Position.Z - spot.Z).Length() >= 28),
                    "pickup lies deeper in the landscape and away from every quest animal and resident");
            }
            foreach (var (road, pickup) in world.PickupApproaches)
                for (float t = 0; t <= 1; t += .05f)
                    Check(!world.Plants.NearTrunk(road.Lerp(pickup, t), 3.5f), "off-road pickup has a clear driving approach");
            int visits = game.RemainingAnimals, deliveries = game.RemainingDeliveries;
            foreach (bool toys in new[] { false, true })
            {
                var owner = toys ? village.Toymaker : village.GemCollector;
                for (int i = 0; i < Village.CollectionNames(toys).Length; i++)
                {
                    car.SetPose(owner.Position + Vector3.Right * 2, 0); game.OpenChallenge(owner);
                    Check(game.State == GameState.Challenge && (i % 2 == 0 ? game.CurrentChallenge is MathChallenge : game.CurrentChallenge is LetterChallenge), "each departure requires math or letters");
                    game.SubmitAnswer(game.CurrentChallenge!.Choices.First(a => a != game.CurrentChallenge.Answer));
                    Check(game.Delivery == null && village.CollectionCount(toys) == i, "wrong answer never starts a trip");
                    game.TogglePause(); RoundTrip();
                    game.FinishInteraction(); game.OpenChallenge(owner);
                    Check(game.CurrentChallenge != null && game.Delivery == null, "abandoned quiz never unlocks departure");
                    game.SubmitAnswer(game.CurrentChallenge!.Answer);
                    Check(game.State == GameState.Exploring && game.Delivery is { Collected: 0, Amount: 1 } && game.Delivery.Sender == owner, "correct answer starts one collection trip");
                    RoundTrip();
                    foreach (var other in new[] { village.Farmer, village.Gardener, village.GemCollector, village.Toymaker })
                    {
                        game.OpenChallenge(other);
                        Check(game.CurrentChallenge == null && game.Delivery!.Sender == owner && village.CollectionCount(toys) == i, "active expedition blocks another trip and empty handover");
                        RoundTrip(); game.FinishInteraction();
                    }
                    var pickup = game.Delivery!.PickupPositions[0];
                    car.SetPose(world.Routes.Samples.OrderBy(p => p.DistanceSquaredTo(pickup)).First(), 0); game.Delivery.Tick(.25);
                    Check(game.Delivery.Collected == 0, "driving on road cannot collect the item");
                    car.SetPose(pickup, 0); game.Delivery.Tick(.25); game.Delivery.Tick(.25);
                    Check(game.Delivery.Collected == 1 && game.Delivery.CargoVisible && game.Delivery.CanDeliver, "item loads once and is visible on the car");
                    game.TogglePause(); RoundTrip(); car.Reset();
                    Check(game.Delivery!.Collected == 1, "reset preserves cargo");
                    var expedition = game.Delivery;
                    car.SetPose(owner.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
                    Check(game.State == GameState.CollectionStatus && game.Delivery == null && village.CollectionCount(toys) == i + 1 && !expedition.CargoVisible, "return moves one item from car to display");
                    game.BeginDeliveryQuestion(); game.SubmitAnswer(1);
                    Check(village.CollectionCount(toys) == i + 1, "duplicate handover cannot add another item");
                    RoundTrip(); game.FinishInteraction();
                }
                game.OpenChallenge(owner);
                Check(game.State == GameState.CollectionStatus && game.CurrentChallenge == null && !owner.RewardAvailable, "completed collection thanks the player");
                RoundTrip(); game.FinishInteraction();
            }
            Check(village.GemCount == 12 && village.ToyCount == 12 && village.FarmCount == 0 && village.GardenCount == 0 &&
                game.RemainingAnimals == visits && game.RemainingDeliveries == deliveries, "all four collections and roadside quests remain independent");
            var oldSave = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!;
            oldSave.AsObject().Remove("GemCount"); oldSave.AsObject().Remove("ToyCount");
            File.WriteAllText(path, oldSave.ToJsonString());
            Check(SaveStore.Read(path) is { GemCount: 0, ToyCount: 0 }, "older saves start with empty new collections");
            GD.Print("COLLECTION_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
