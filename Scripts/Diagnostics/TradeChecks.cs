using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;

namespace MathRacer;

public partial class TradeChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public TradeChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = ProjectSettings.GlobalizePath("res://artifacts/trade-checks/progress.json");
            void RoundTrip()
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "trade progress written");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, "trade save accepted");
                game.RestoreProgress(loaded!);
                Check(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()), "trade state restores exactly");
            }
            int animals = game.RemainingAnimals, deliveries = game.RemainingDeliveries;
            Check(world.Village.Traders.Count == 3 && world.Village.Shops.Count == 3, "three distinct village trades generated");
            foreach (var shop in world.Village.Shops)
                for (float x = -4.5f; x <= 4.5f; x += 1.5f)
                    for (float z = -3.5f; z <= 6; z += 1.5f)
                        Check(!world.Routes.Contains(shop.ToGlobal(new(x, 0, z))), "shop and entrance stay off roads");
            for (int trip = 0; trip < 6; trip++)
            {
                var owner = world.Village.Traders[trip % 3];
                car.SetPose(owner.Position + Vector3.Right * 2, 0); game.OpenChallenge(owner);
                Check(game.CurrentChallenge is MathChallenge && game.State == GameState.Challenge, "every trade trip requires arithmetic");
                game.SubmitAnswer(game.CurrentChallenge!.Choices.First(a => a != game.CurrentChallenge.Answer));
                Check(game.Delivery == null, "wrong answer cannot load cargo");
                game.TogglePause(); RoundTrip();
                game.FinishInteraction(); game.OpenChallenge(owner);
                Check(game.CurrentChallenge is MathChallenge && game.Delivery == null, "abandoned question cannot unlock departure");
                game.SubmitAnswer(game.CurrentChallenge!.Answer);
                Check(game.Delivery is { Collection: false, CargoVisible: true } && game.State == GameState.Exploring &&
                    game.Delivery.Cargo == Village.TradeCargo[owner.TradeIndex], "correct answer immediately loads the trade cargo");
                var target = game.Delivery!.Recipient;
                int amount = game.Delivery.Amount, owned = game.OwnedRewards.Count;
                RoundTrip();
                Check(game.Delivery!.Recipient == target && game.Delivery.Amount == amount, "restored delivery keeps its destination and amount");
                foreach (var trader in world.Village.Traders)
                {
                    game.OpenChallenge(trader);
                    Check(game.State == GameState.TradeStatus && game.CurrentChallenge == null && game.Delivery!.Sender == owner, "active trip blocks all new trade departures");
                    RoundTrip(); game.FinishInteraction();
                }
                car.SetPose(owner.Position, 0); game.BeginDeliveryQuestion();
                Check(game.State == GameState.Exploring && game.OwnedRewards.Count == owned, "no reward before reaching customer");
                car.SetPose(target.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
                Check(game.State == GameState.Reward && game.OwnedRewards.Count == owned &&
                    game.CurrentRewards.Count == 3 && game.CurrentRewards.Count(r => r.IsAccessory) == 1 &&
                    game.CurrentRewards.Count(r => r.IsModel) == 1, "arrival offers one upgrade, paint and vehicle without granting early");
                game.TogglePause(); RoundTrip();
                game.BeginDeliveryQuestion();
                var reward = game.CurrentRewards[trip % 3];
                if (reward.IsModel) game.SelectModel(reward.Id);
                else if (reward.IsAccessory) game.SelectAccessory(reward.Id);
                else game.SelectLook(reward.Id);
                Check(game.Delivery == null && game.OwnedRewards.Contains(reward) && game.State == GameState.Exploring,
                    "chosen reward enters garage and unloads cargo");
                Check(world.Village.TradeCounts[owner.TradeIndex] == trip / 3 + 1, "completion counted once and trader offers repeat trips");
                RoundTrip();
            }
            Check(game.RemainingAnimals == animals && game.RemainingDeliveries == deliveries, "trade jobs preserve roadside progression");
            var legacy = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!;
            legacy.AsObject().Remove("TradeCounts"); File.WriteAllText(path, legacy.ToJsonString());
            Check(SaveStore.Read(path)?.TradeCounts.SequenceEqual(new int[3]) == true, "older saves default to zero trade trips");
            GD.Print("TRADE_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
