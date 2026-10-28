using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class FarmChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public FarmChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
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
            string path = ProjectSettings.GlobalizePath("res://artifacts/farm-checks/progress.json");
            void RoundTrip()
            {
                var expected = game.CaptureProgress();
                Check(SaveStore.Write(expected, path), "farm progress saved");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, "farm progress passes validation");
                game.RestoreProgress(loaded!);
                Check(System.Text.Json.JsonSerializer.Serialize(expected) == System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress()),
                    "farm quiz, cargo and enclosure restore exactly");
            }
            var village = world.Village;
            Check(village.Houses.Count == 4 && village.Residents.Count == 4 && village.Farmer.IsFarmer, "farm adds a dedicated resident without replacing houses");
            Check(world.Animals.All(a => !village.IsNearVillage(a.Position)), "task animals leave farm and village clear");
            // An existing expedition must finish before the farmer can start another.
            game.OpenChallenge(world.Animals[0]); game.SubmitAnswer(game.CurrentChallenge!.Answer); game.SelectExpedition(false);
            var existing = game.Delivery;
            game.OpenChallenge(village.Farmer); game.SubmitAnswer(1);
            Check(game.State == GameState.FarmStatus && game.Delivery == existing, "farmer cannot replace an active expedition");
            RoundTrip(); game.FinishInteraction();
            car.SetPose(game.Delivery!.Recipient.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
            game.SubmitAnswer(game.CurrentChallenge!.Answer); game.FinishInteraction();
            int visits = game.RemainingAnimals, deliveries = game.RemainingDeliveries;
            for (int i = 0; i < Village.FarmKinds.Length; i++)
            {
                car.SetPose(village.Farmer.Position + Vector3.Right * 2, 0); game.OpenChallenge(village.Farmer);
                Check(game.State == GameState.Challenge && (i % 2 == 0 ? game.CurrentChallenge is MathChallenge : game.CurrentChallenge is LetterChallenge),
                    "each departure requires alternating math or letters");
                game.SubmitAnswer(game.CurrentChallenge!.Choices.First(a => a != game.CurrentChallenge.Answer));
                Check(game.Delivery == null && village.FarmCount == i, "wrong answer cannot start a trip or award an animal");
                RoundTrip(); game.FinishInteraction(); game.OpenChallenge(village.Farmer);
                Check(game.Delivery == null && game.CurrentChallenge != null, "postponing the quiz does not unlock departure");
                game.SubmitAnswer(game.CurrentChallenge!.Answer);
                Check(game.State == GameState.Exploring && game.Delivery is { Farm: true, Collected: 0, Amount: 1 }, "correct answer starts one animal expedition");
                RoundTrip();
                game.OpenChallenge(village.Farmer);
                Check(game.State == GameState.FarmStatus && village.FarmCount == i, "empty return cannot add an animal");
                game.FinishInteraction();
                var destination = game.Delivery!.PickupPositions[0];
                Check(!world.Routes.Contains(destination) && world.Routes.Distance(destination) >= 7.5f, "catchable animal waits off the road");
                car.SetPose(world.Routes.Samples.OrderBy(p => p.DistanceSquaredTo(destination)).First(), 0); game.Delivery.Tick(.25);
                Check(game.Delivery.Collected == 0, "passing on the road does not catch the animal");
                car.SetPose(game.Delivery!.PickupPositions[0], 0); game.Delivery.Tick(.25); game.Delivery.Tick(.25);
                Check(game.Delivery.Collected == 1 && game.Delivery.CanDeliver && game.Delivery.CargoVisible, "animal caught once and visible on car");
                RoundTrip();
                car.Reset(); Check(game.Delivery!.Collected == 1, "reset preserves caught animal");
                car.SetPose(village.Farmer.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
                Check(game.State == GameState.FarmStatus && village.FarmCount == i + 1 && game.Delivery == null,
                    "returning adds exactly one animal to enclosure and removes cargo");
                game.BeginDeliveryQuestion(); Check(village.FarmCount == i + 1, "handover cannot duplicate animals");
                RoundTrip(); game.FinishInteraction();
            }
            game.OpenChallenge(village.Farmer);
            Check(game.State == GameState.FarmStatus && game.CurrentChallenge == null && !village.Farmer.RewardAvailable, "full farm thanks player without extra trips");
            Check(game.RemainingAnimals == visits && game.RemainingDeliveries == deliveries, "farm preserves original quest progress");
            GD.Print("FARM_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
