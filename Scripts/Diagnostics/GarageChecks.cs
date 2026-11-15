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
            Check(buttons.Length == CarLook.All.Count + CarBody.All.Count + CarAccessory.Catalogue.Count && buttons.Count(b => !b.Disabled) == 2,
                "entire catalogue contains all rewards with only starting paint and model unlocked");
            var defaults = game.CaptureProgress();
            void Press(string name) => ((Button)game.FindChild(name, true, false)).EmitSignal(Button.SignalName.Pressed);
            Check(defaults.QuizTypes == QuizType.All && !defaults.ThreeTermMath, "all four quiz types and level-one arithmetic are the defaults");
            Check(game.FindChildren("GarageQuiz_*", "Button", true, false).Cast<Button>().Count(button =>
                button.Text == "" && button.GetChildren().OfType<DeliveryPicture>().Count() == 1 && !button.Disabled) == 4,
                "garage exposes an illustrated, independently selectable control for each quiz type");
            Press("GarageQuiz_Reading");
            Press("GarageQuiz_Syllables");
            Press("GarageQuiz_Math");
            Press("GarageMathLevel2");
            Check(game.CaptureProgress() is { QuizTypes: QuizType.Letters, ThreeTermMath: true },
                "quiz toggles and the arithmetic level can be selected independently");
            game.FinishInteraction();
            game.OpenChallenge(world.Witch);
            Check(game.CurrentChallenge is LetterChallenge && game.PostcardQuestion(0) is LetterChallenge,
                "letters-only setting reaches arithmetic slots and postcards");
            game.FinishInteraction();
            game.OpenGarage();
            Press("GarageQuiz_Math");
            Press("GarageQuiz_Letters");
            game.FinishInteraction();
            game.OpenChallenge(world.Witch);
            Check(game.CurrentChallenge is MathChallenge { Third: > 0 } advanced && advanced.Answer <= 10 &&
                game.PostcardQuestion(0) is MathChallenge { Third: > 0 }, "selected level produces three-term examples in the game");
            game.RestoreProgress(defaults);
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
            game.OpenChallenge(world.Animals[0]); ChallengeChecks.Solve(game); game.SelectExpedition(false);
            var delivery = game.Delivery!;
            car.SetPose(delivery.Recipient.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion(); ChallengeChecks.Solve(game);
            var reward = game.CurrentRewards.First(r => r.IsAccessory); game.SelectAccessory(reward.Id);
            Check(game.OwnedRewards.Contains(reward) && car.Appearance.HasAccessory(reward.Id), "chosen expedition reward is owned and equipped");
            car.SetPose(world.Village.GarageEntrance, 0); game.OpenGarage(); game.ConfigureCar(reward);
            Check(!car.Appearance.HasAccessory(reward.Id) && game.OwnedRewards.Contains(reward), "garage removes accessories without losing ownership");
            game.ConfigureCar(reward);
            Check(car.Appearance.HasAccessory(reward.Id), "owned accessory can be equipped again");
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[1]); ChallengeChecks.Solve(game); game.SelectExpedition(true);
            var collection = game.Delivery;
            car.SetPose(world.Village.GarageEntrance, 0); game.OpenGarage(); game.TogglePause(); game.TogglePause(); game.FinishInteraction();
            Check(game.Delivery == collection && game.State == GameState.Exploring && car.Active, "garage and pause preserve an active expedition");
            game.OpenGarage();
            Press("GarageQuiz_Math");
            Press("GarageQuiz_Letters");
            Press("GarageQuiz_Reading");
            Check(((Button)game.FindChild("GarageQuiz_Syllables", true, false)).Disabled, "last active quiz type is locked in the garage");
            Press("GarageQuiz_Syllables");
            game.FinishInteraction();
            Check(game.CaptureProgress().QuizTypes == QuizType.Syllables && game.PostcardQuestion(0) is SyllableChallenge,
                "the last active quiz type cannot be switched off");
            foreach (var point in collection!.PickupPositions) { car.SetPose(point, 0); collection.Tick(.25); }
            car.SetPose(collection.Recipient.Position + Vector3.Right * 2, 0);
            game.BeginDeliveryQuestion();
            Check(game.State == GameState.Challenge && game.CurrentChallenge is SyllableChallenge,
                "the enabled quiz type replaces cargo counting when arithmetic is off");
            string path = ProjectSettings.GlobalizePath("res://artifacts/save-checks/handover.json");
            Check(SaveStore.Write(game.CaptureProgress(), path), "replacement handover quiz saves to disk");
            game.RestoreProgress(SaveStore.Read(path) ?? throw new InvalidOperationException("Handover save failed validation"));
            ChallengeChecks.Solve(game);
            Check(game.State == GameState.Reward && game.CurrentChallenge == null && game.CaptureProgress().DeliveryReward,
                "replacement handover quiz unlocks the delivery reward after reload");
            game.FinishInteraction();
            game.OpenChallenge(world.Animals[2]);
            Check(game.State == GameState.Challenge && game.CurrentChallenge is SyllableChallenge,
                "disabled quiz types are replaced at new expeditions as well");
            Check(world.Village.IsReserved(world.Village.GarageEntrance), "garage plot excludes plants and animal placement");
            GD.Print("GARAGE_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
