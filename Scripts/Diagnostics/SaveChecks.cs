using Godot;
using System;
using System.IO;
using System.Linq;

namespace MathRacer;

public partial class SaveChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public SaveChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
    public override void _Ready()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath("res://artifacts/save-checks/progress.json");
            void RoundTrip()
            {
                var expected = game.CaptureProgress();
                Check(SaveStore.Write(expected, path), "save written atomically");
                var loaded = SaveStore.Read(path)!;
                Check(loaded != null && loaded.Seed == Settings.Seed, "world seed survives disk round trip");
                game.RestoreProgress(loaded!);
                Check(System.Text.Json.JsonSerializer.Serialize(expected) == System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress()),
                    "full progress and interaction survive restoration: " + expected.State);
            }
            car.SetPose(world.Animals[0].Position + Vector3.Right * 2, .7f);
            game.OpenChallenge(world.Animals[0]);
            game.SubmitAnswer(game.CurrentChallenge!.Choices.First(a => a != game.CurrentChallenge.Answer));
            RoundTrip();
            game.SubmitAnswer(game.CurrentChallenge!.Answer); RoundTrip();
            game.SelectExpedition(true);
            car.SetPose(game.Delivery!.PickupPositions[1], 1.2f); game.Delivery.Tick(.25);
            Check(game.Delivery.Collected >= 1, "collection in progress"); RoundTrip();
            Check(game.Delivery!.GetChildren().OfType<Node3D>().Count(n => n.Name.ToString().StartsWith("CollectibleApple") && !n.Visible) == game.Delivery.Collected,
                "picked fruit stays hidden after restore");
            foreach (var point in game.Delivery.PickupPositions) { car.SetPose(point, 0); game.Delivery.Tick(.25); }
            car.SetPose(game.Delivery.Recipient.Position + Vector3.Right * 2, .5f);
            game.BeginDeliveryQuestion(); RoundTrip();
            game.SubmitAnswer(game.CurrentChallenge!.Answer); RoundTrip();
            var reward = game.CurrentRewards.First(r => r.IsModel); game.SelectModel(reward.Id);
            game.OwnedRewards.Add(new("wheels", true)); car.Appearance.ToggleAccessory("wheels");
            RoundTrip();
            Check(game.OwnedRewards.Contains(reward) && car.Appearance.CurrentModelId == reward.Id && car.Appearance.BigWheels,
                "earned rewards and equipped appearance restored");
            game.OpenChallenge(world.Animals[1]); RoundTrip();
            game.TogglePause(); RoundTrip();
            Check(game.State == GameState.Challenge, "paused session restores the underlying question");
            game.FinishInteraction(); car.SetPose(world.Village.GarageEntrance, .2f); game.OpenGarage(); RoundTrip();
            var backup = File.ReadAllText(path + ".bak");
            File.WriteAllText(path, "{broken");
            Check(SaveStore.Read(path) != null, "damaged primary save falls back to backup");
            File.WriteAllText(path + ".bak", "{}");
            Check(SaveStore.Read(path) == null, "invalid saves are safely ignored");
            File.WriteAllText(path, backup);
            var legacy = game.CaptureProgress();
            legacy.Animals = legacy.Animals.Concat(Enumerable.Repeat(true, 20)).ToArray();
            legacy.Unlocked = new[] { 0, 12, 29 }; legacy.Completed = new[] { 1, 22 };
            legacy.State = GameState.Exploring; legacy.Nearby = 30;
            legacy.Sender = -1; legacy.Picked = Array.Empty<bool>(); legacy.DeliveryReward = false;
            Check(SaveStore.Write(legacy, path), "legacy thirty-animal save written");
            var migrated = SaveStore.Read(path)!;
            Check(migrated != null && migrated.Animals.Length == 10 && migrated.Unlocked.SequenceEqual(new[] { 0 }) &&
                migrated.Completed.SequenceEqual(new[] { 1 }) && migrated.Nearby == 30,
                "old saves retain ten animal records and stable village IDs");
            game.RestoreProgress(migrated!);
            Check(game.RemainingAnimals <= 10, "migrated progress restores in the ten-animal world");
            legacy.State = GameState.Challenge; legacy.Nearby = 22;
            legacy.Math = new ChallengeGenerator(42).Next();
            Check(SaveStore.Write(legacy, path), "removed animal question saved");
            migrated = SaveStore.Read(path)!;
            Check(migrated is { State: GameState.Exploring, Nearby: -1, Math: null }, "removed animal interaction returns safely to driving");
            legacy.State = GameState.Exploring; legacy.Nearby = -1; legacy.Math = null;
            legacy.Sender = 22; legacy.Collection = true; legacy.Picked = new[] { true, false, false };
            Check(SaveStore.Write(legacy, path), "removed animal expedition saved");
            migrated = SaveStore.Read(path)!;
            Check(migrated is { Sender: -1, State: GameState.Exploring } && migrated.Picked.Length == 0 &&
                migrated.Owned.SequenceEqual(legacy.Owned), "removed sender cancels only its expedition and preserves earned rewards");
            var beforeLeveling = game.CaptureProgress();
            var villageSpot = world.Village.Center;
            float villageGround = world.Ground.Height(villageSpot.X, villageSpot.Z);
            beforeLeveling.Position = new[] { villageSpot.X, villageGround - 3, villageSpot.Z };
            game.RestoreProgress(beforeLeveling);
            Check(Mathf.Abs(car.Position.X - villageSpot.X) < .001f && Mathf.Abs(car.Position.Z - villageSpot.Z) < .001f &&
                car.Position.Y >= villageGround, "old village position below the leveled terrain restores above ground without moving horizontally");
            GD.Print("SAVE_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
