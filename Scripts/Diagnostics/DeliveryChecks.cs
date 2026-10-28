using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MathRacer;

public partial class DeliveryChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public DeliveryChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    private async Task Frames()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
    private void Press(string name)
    {
        var button = game.FindChild(name, true, false) as Button;
        if (button == null || button.Disabled) throw new InvalidOperationException("Missing button " + name);
        button.EmitSignal(Button.SignalName.Pressed);
    }
    public override async void _Ready()
    {
        try
        {
            await Frames();
            game.OfferDelivery(world.Animals[0]);
            Check(game.State == GameState.Exploring && game.Delivery == null, "expedition stays locked until the original question is solved");
            game.OpenChallenge(world.Animals[0]); game.SubmitAnswer(game.CurrentChallenge!.Answer);
            Check(game.State == GameState.ExpeditionChoice && game.CurrentRewards.Count == 0, "correct answer opens task choice before any reward");
            game.TogglePause(); game.TogglePause();
            Check(game.State == GameState.ExpeditionChoice, "pause preserves task choice");
            Check(game.FindChildren("*", "", true, false).OfType<CarLookPreview>().Any(p => p.ExpeditionCargo == "apple" && p.CargoAmount == 0),
                "collection choice shows an empty platform");
            Press("LeaveExpeditionChoice");
            Check(game.RemainingAnimals == 29 && game.Delivery == null, "postponing task preserves the solved original question");
            foreach (var sender in world.Animals)
            {
                if (sender.RewardAvailable)
                {
                    game.OpenChallenge(sender);
                    Check(game.CurrentChallenge is MathChallenge or LetterChallenge, "original arithmetic and picture tasks remain primary");
                    game.SubmitAnswer(game.CurrentChallenge!.Answer);
                    Check(game.State == GameState.ExpeditionChoice && game.CurrentRewards.Count == 0,
                        "original question unlocks task choice without granting cosmetics");
                    game.TogglePause(); game.TogglePause();
                }
                else game.OfferDelivery(sender); // This original task was exercised during an earlier expedition below.
                Check(game.State == GameState.ExpeditionChoice && game.Delivery == null &&
                    game.FindChild("ChooseDelivery", true, false) is Button && game.FindChild("ChooseCollection", true, false) is Button,
                    "every animal offers both delivery and collection");
                bool collection = Array.IndexOf(world.Animals, sender) % 2 == 1;
                Press(collection ? "ChooseCollection" : "ChooseDelivery");
                Check(game.State == GameState.Exploring && game.Delivery != null && car.Active && game.FindChild("AcceptDelivery", true, false) == null,
                    "selecting a task starts driving immediately without a confirmation dialog");
                game.TogglePause(); game.TogglePause();
                int before = game.RemainingAnimals;
                int deliveriesBefore = game.RemainingDeliveries;
                var delivery = game.Delivery!;
                bool recipientAvailable = delivery.Recipient.RewardAvailable;
                Check(delivery.Sender == sender && delivery.Collection == collection && (delivery.Collection ? delivery.Recipient == sender : delivery.Recipient != sender) && delivery.Amount is >= 1 and <= 3 && delivery.CargoVisible,
                    "delivery or collection expedition has correct recipient and cargo: " + sender.DisplayName);
                Check(game.State == GameState.Exploring && car.Active && game.RemainingAnimals == before, "accepting does not consume visit");
                game.OfferDelivery(delivery.Recipient);
                Check(game.Delivery == delivery && game.State == GameState.Exploring, "cannot replace active delivery");
                foreach (var body in CarBody.All) car.Appearance.ApplyModel(body.Id);
                car.Reset(); game.TogglePause(); game.TogglePause();
                Check(game.Delivery == delivery && delivery.CargoVisible, "reset, model swaps and pause preserve cargo and destination");
                if (sender == world.Animals[0])
                {
                    var originalPose = car.Position;
                    car.SetPose(world.Routes.Samples.MaxBy(p => p.DistanceSquaredTo(delivery.Destination)), 0);
                    delivery.Tick(.25);
                    var arrows = delivery.GetChildren().OfType<MeshInstance3D>().Where(a => a.Visible).ToArray();
                    Check(arrows.Length > 1, "long route displays multiple road arrows");
                    var transforms = arrows.Select(a => a.Transform).ToArray();
                    car.SetPose(arrows[^1].Position, 0); delivery.Tick(.25);
                    Check(arrows.Select((a, i) => a.Transform == transforms[i]).All(same => same) && arrows.Any(a => !a.Visible),
                        "driving hides passed arrows without moving or rotating their anchors");
                    car.SetPose(world.Routes.Samples.MaxBy(p => p.DistanceSquaredTo(delivery.Destination)), 0);
                    delivery.Tick(.25);
                    Check(arrows.All(a => a.Visible) && arrows.Select((a, i) => a.Transform == transforms[i]).All(same => same),
                        "returning reveals the same arrows in their original places");
                    int start = world.Routes.Samples.IndexOf(world.Routes.Samples.MaxBy(p => p.DistanceSquaredTo(delivery.Destination)));
                    for (int step = 0; step < 12; step++)
                    {
                        car.SetPose(world.Routes.Samples[Math.Min(start + step, world.Routes.Samples.Count - 1)], 0);
                        delivery.Tick(.25);
                        var visible = delivery.GetChildren().OfType<MeshInstance3D>().Where(a => a.Visible).ToArray();
                        Check(visible.All(a => world.Routes.NavigationAnchors.Any(anchor =>
                            new Vector2(a.Position.X - anchor.Position.X, a.Position.Z - anchor.Position.Z).LengthSquared() < .0001f)),
                            "road arrows use road-defined anchors while nearest route samples change");
                    }
                    car.SetPose(originalPose, 0); delivery.Tick(.25);
                    var other = world.Animals.Last(a => a.RewardAvailable && a != delivery.Recipient);
                    game.OpenChallenge(other); game.SubmitAnswer(game.CurrentChallenge!.Answer);
                    Check(game.State == GameState.Exploring && game.CurrentRewards.Count == 0, "solving another question preserves active expedition and postpones its task choice");
                    game.TogglePause(); game.TogglePause();
                    Check(game.Delivery == delivery && delivery.CargoVisible && game.State == GameState.Exploring,
                        "solving another original question preserves active cargo");
                    before = game.RemainingAnimals;
                }
                if (delivery.Collection)
                {
                    car.SetPose(sender.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
                    Check(game.State == GameState.Exploring && delivery.Collected == 0, "cannot deliver apples before collecting them");
                    for (int i = 0; i < delivery.PickupPositions.Length; i++)
                    {
                        car.SetPose(delivery.PickupPositions[i] + Vector3.Up * .2f, 0);
                        game.TogglePause(); await Frames();
                        Check(delivery.Collected == i, "paused exploration does not collect apples");
                        game.TogglePause(); await Frames();
                        Check(delivery.Collected == i + 1, "driving to an apple loads exactly one item");
                        car.Reset(); await Frames();
                        Check(delivery.Collected == i + 1, "reset preserves collected apples");
                    }
                    Check(delivery.CanDeliver && delivery.Destination == sender.Position, "three apples switch navigation back to requester");
                }
                car.SetPose(delivery.Recipient.Position + Vector3.Right * 2, 0); await Frames();
                Check(game.State == GameState.Challenge && !car.Active && game.CurrentChallenge is DeliveryChallenge,
                    "arrival automatically opens picture counting task");
                Check(Enumerable.Range(1, 3).All(n => (game.FindChild($"Answer{n}", true, false) as Button)?.Text == n.ToString()),
                    "three numbered answer choices");
                int answer = game.CurrentChallenge!.Answer;
                int wrong = game.CurrentChallenge.Choices.First(x => x != answer);
                Press($"Answer{wrong}"); game.TogglePause(); game.TogglePause();
                Check(game.State == GameState.Challenge && ((Button)game.FindChild($"Answer{wrong}", true, false)).Disabled,
                    "wrong answer remains disabled after pause");
                Press("LeaveChallenge"); await Frames();
                Check(game.State == GameState.Exploring && game.Delivery == delivery && game.RemainingAnimals == before,
                    "leaving counting task preserves delivery without automatic reopening");
                game.BeginDeliveryQuestion(); Press($"Answer{answer}");
                Check(game.State == GameState.Reward && game.RemainingAnimals == before && game.RemainingDeliveries == deliveriesBefore - 1 &&
                    delivery.Recipient.RewardAvailable == recipientAvailable, "expedition completion is independent of original tasks");
                Check(game.FindChild("DeliveryReward", true, false) == null, "expedition rewards cannot chain into another expedition");
                Check(game.CurrentRewards.Count == 4 && game.CurrentRewards.Count(r => r.IsAccessory) == 2 &&
                    game.CurrentRewards.Distinct().Count() == 4, "completed task offers paint, vehicle and two different upgrades");
                Check(game.CurrentRewards.All(r => game.FindChild(r.ButtonName, true, false) is Button b && b.Text == "" &&
                    b.GetChildren().OfType<DeliveryPicture>().Any(p => p.Icon == (r.IsModel ? "vehicle" : r.IsAccessory ? "upgrade" : "paint"))),
                    "all reward buttons show category symbols instead of captions");
                game.TogglePause(); game.TogglePause();
                Check(game.State == GameState.Reward && game.Delivery == delivery, "pause retains delivery reward");
                if (before % 2 == 0) Press(game.CurrentRewards[0].ButtonName); else Press("KeepLook");
                await Frames();
                Check(game.State == GameState.Exploring && game.Delivery == null && car.FindChild("DeliveryCargo", true, false) == null,
                    "reward closes delivery and removes cargo");
                game.OfferDelivery(sender);
                Check(game.State == GameState.Exploring, "completed delivery cannot repeat");
            }
            Check(game.RemainingAnimals == 0 && game.RemainingDeliveries == 0 && car.Active, "all original tasks and both expedition types finish independently");
            GD.Print("DELIVERY_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("DELIVERY_CHECKS_FAILED: " + e); GetTree().Quit(1); }
    }
}
