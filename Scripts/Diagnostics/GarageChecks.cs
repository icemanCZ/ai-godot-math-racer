using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class GarageChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public GarageChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
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
            car.SetPose(world.Village.GarageEntrance, 0); game.OpenGarage();
            Check(game.State == GameState.Garage && !car.Active, "garage stops driving at the village entrance");
            var buttons = game.FindChildren("Garage_*", "Button", true, false).Cast<Button>().ToArray();
            Check(buttons.Length == CarLook.All.Count + CarBody.All.Count + CarAccessory.All.Count && buttons.Count(b => !b.Disabled) == 2,
                "entire catalogue contains all rewards with only starting paint and model unlocked");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var ui = game.GetChildren().OfType<GameUi>().Single();
            ui._Input(new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 1 }); ui._Process(.4);
            ui._Input(new InputEventJoypadMotion { Axis = JoyAxis.RightY, AxisValue = 0 });
            Check(((ScrollContainer)game.FindChild("GarageScroll", true, false)).ScrollVertical > 0,
                "right stick scrolls the catalogue even when rewards are locked");
            game.ConfigureCar(new("buggy", false, true));
            Check(car.Appearance.CurrentModelId == "classic", "locked model cannot be equipped");
            game.TogglePause(); game.TogglePause();
            Check(game.State == GameState.Garage, "pause resumes garage");
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[0]); game.SubmitAnswer(game.CurrentChallenge!.Answer); game.SelectExpedition(false);
            var delivery = game.Delivery!;
            car.SetPose(delivery.Recipient.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion(); game.SubmitAnswer(game.CurrentChallenge!.Answer);
            var reward = game.CurrentRewards.First(r => r.IsAccessory); game.SelectAccessory(reward.Id);
            Check(game.OwnedRewards.Contains(reward) && car.Appearance.HasAccessory(reward.Id), "chosen expedition reward is owned and equipped");
            car.SetPose(world.Village.GarageEntrance, 0); game.OpenGarage(); game.ConfigureCar(reward);
            Check(!car.Appearance.HasAccessory(reward.Id) && game.OwnedRewards.Contains(reward), "garage removes accessories without losing ownership");
            game.ConfigureCar(reward);
            Check(car.Appearance.HasAccessory(reward.Id), "owned accessory can be equipped again");
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[1]); game.SubmitAnswer(game.CurrentChallenge!.Answer); game.SelectExpedition(true);
            var collection = game.Delivery;
            car.SetPose(world.Village.GarageEntrance, 0); game.OpenGarage(); game.TogglePause(); game.TogglePause(); game.FinishInteraction();
            Check(game.Delivery == collection && game.State == GameState.Exploring && car.Active, "garage and pause preserve an active expedition");
            Check(world.Village.IsReserved(world.Village.GarageEntrance), "garage plot excludes plants and animal placement");
            GD.Print("GARAGE_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
