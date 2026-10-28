using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class GardenChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public GardenChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
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
            string path = ProjectSettings.GlobalizePath("res://artifacts/garden-checks/progress.json");
            void RoundTrip()
            {
                var expected = game.CaptureProgress();
                Check(SaveStore.Write(expected, path), "garden progress saved");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, "garden progress passes validation");
                game.RestoreProgress(loaded!);
                Check(System.Text.Json.JsonSerializer.Serialize(expected) == System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress()),
                    "garden quiz, cargo and flower beds restore exactly");
            }
            var village = world.Village;
            foreach (var bed in village.GetChildren().OfType<Node3D>().Where(n => n.Name.ToString().StartsWith("FlowerBed")))
                foreach (float x in new[] { -1.5f, 0, 1.5f })
                    foreach (float z in new[] { -1.5f, 0, 1.5f })
                        Check(!world.Routes.Contains(bed.ToGlobal(new(x, 0, z))), "planted flower beds stay off every road");
            foreach (var spot in world.SpecialPickupSpots)
            {
                Check(!world.Routes.Contains(spot) && world.Routes.Distance(spot) >= 7.5f, "animal and flower pickup spots are beyond road surfaces");
                Check(Mathf.Abs(spot.Y - world.Ground.Height(spot.X, spot.Z)) < .01f && !world.Plants.NearTrunk(spot, 2.8f), "pickup is grounded and its approach is clear of trees");
            }
            Check(village.Houses.Count == 4 && village.Residents.Count == 4 && village.Gardener.IsGardener, "garden adds a dedicated resident without replacing houses");
            Check(world.Animals.All(a => !village.IsNearVillage(a.Position)), "task animals leave garden and village clear");
            // An existing expedition must finish before the gardener can start another.
            game.OpenChallenge(world.Animals[0]); game.SubmitAnswer(game.CurrentChallenge!.Answer); game.SelectExpedition(false);
            var existing = game.Delivery;
            game.OpenChallenge(village.Gardener); game.SubmitAnswer(1);
            Check(game.State == GameState.GardenStatus && game.Delivery == existing, "gardener cannot replace an active expedition");
            RoundTrip(); game.FinishInteraction();
            car.SetPose(game.Delivery!.Recipient.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
            game.SubmitAnswer(game.CurrentChallenge!.Answer); game.FinishInteraction();
            int visits = game.RemainingAnimals, deliveries = game.RemainingDeliveries;
            for (int i = 0; i < Village.FlowerNames.Length; i++)
            {
                car.SetPose(village.Gardener.Position + Vector3.Right * 2, 0); game.OpenChallenge(village.Gardener);
                Check(game.State == GameState.Challenge && (i % 2 == 0 ? game.CurrentChallenge is MathChallenge : game.CurrentChallenge is LetterChallenge),
                    "each departure requires alternating math or letters");
                game.SubmitAnswer(game.CurrentChallenge!.Choices.First(a => a != game.CurrentChallenge.Answer));
                Check(game.Delivery == null && village.GardenCount == i, "wrong answer cannot start a trip or award an flower");
                RoundTrip(); game.FinishInteraction(); game.OpenChallenge(village.Gardener);
                Check(game.Delivery == null && game.CurrentChallenge != null, "postponing the quiz does not unlock departure");
                game.SubmitAnswer(game.CurrentChallenge!.Answer);
                Check(game.State == GameState.Exploring && game.Delivery is { Garden: true, Collected: 0, Amount: 1 }, "correct answer starts one flower expedition");
                RoundTrip();
                game.OpenChallenge(village.Gardener);
                Check(game.State == GameState.GardenStatus && village.GardenCount == i, "empty return cannot add an flower");
                game.FinishInteraction();
                var destination = game.Delivery!.PickupPositions[0];
                var closestRoad = world.Routes.Samples.OrderBy(p => p.DistanceSquaredTo(destination)).First();
                car.SetPose(closestRoad, 0); game.Delivery.Tick(.25);
                Check(game.Delivery.Collected == 0, "passing on the road does not collect an off-road flower");
                game.OpenChallenge(village.Farmer);
                Check(game.State == GameState.FarmStatus && game.Delivery.Garden, "farm cannot replace the gardener expedition");
                game.FinishInteraction();
                car.SetPose(game.Delivery!.PickupPositions[0], 0); game.Delivery.Tick(.25); game.Delivery.Tick(.25);
                Check(game.Delivery.Collected == 1 && game.Delivery.CanDeliver && game.Delivery.CargoVisible, "flower caught once and visible on car");
                RoundTrip();
                car.Reset(); Check(game.Delivery!.Collected == 1, "reset preserves caught flower");
                car.SetPose(village.Gardener.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
                Check(game.State == GameState.GardenStatus && village.GardenCount == i + 1 && game.Delivery == null,
                    "returning adds exactly one flower to flower beds and removes cargo");
                game.BeginDeliveryQuestion(); Check(village.GardenCount == i + 1, "handover cannot duplicate flowers");
                RoundTrip(); game.FinishInteraction();
            }
            game.OpenChallenge(village.Gardener);
            Check(game.State == GameState.GardenStatus && game.CurrentChallenge == null && !village.Gardener.RewardAvailable, "full garden thanks player without extra trips");
            Check(game.RemainingAnimals == visits && game.RemainingDeliveries == deliveries, "garden preserves original quest progress");
            GD.Print("GARDEN_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
