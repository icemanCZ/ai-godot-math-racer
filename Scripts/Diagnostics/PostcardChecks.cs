using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class PostcardChecks(Game game, World world, Vehicle car) : Node
{
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var owner = world.Animals[0];
            game.OpenChallenge(owner);
            game.SubmitAnswer(game.CurrentChallenge!.Answer);
            game.SelectExpedition(false);
            int sender = game.CaptureProgress().Sender;
            Require(sender >= 0, "normal expedition starts before the postcard trip");

            int[] correct = { 1, 2, 0, 1, 2, 0 };
            for (int i = 0; i < Game.PostcardCount; i++)
            {
                car.SetPose(game.PostcardStop(i) + Vector3.Up * .2f, 0);
                game._Process(.016);
                Talk();
                Require(game.State == GameState.PostcardAlbum && !car.Active, "E opens the camera and parks the car");
                game.TogglePause();
                game._Process(2);
                Require(game.CaptureProgress().Postcards[i] == null, "pause holds the camera shot");
                game.TogglePause();
                game._Process(2);
                Require(game.CaptureProgress().Postcards[i] is { Stamped: false }, "camera adds an unstamped postcard");
                string wrongChoice = $"PostcardAnswer{(correct[i] + 1) % 3}";
                var choice = Find(wrongChoice);
                Require(choice.Text == "" && choice.GetChild(0) is DeliveryPicture, "the answer uses a picture button without a text label");
                Press(wrongChoice);
                Require(Find(wrongChoice).Disabled, "wrong picture is dimmed and disabled");
                Require(game.CaptureProgress().Postcards[i] is { Stamped: false }, "wrong answer leaves the postcard unstamped");
                game.FinishInteraction();
            }

            var square = world.Village.Square;
            car.SetPose(square.ToGlobal(new Vector3(0, .2f, 4.4f)), 0);
            game._Process(.016);
            Talk();
            Require(game.State == GameState.PostcardAlbum, "album is accessible by the fountain");
            var reward = new CarReward(CarAccessory.ValleyMap.Id, true);
            for (int i = 0; i < correct.Length; i++)
            {
                Press($"Postcard{i}");
                Press($"PostcardAnswer{correct[i]}");
                Require(game.CaptureProgress().Postcards[i] is { Stamped: true }, "right answer stamps the selected postcard");
                Require(game.OwnedRewards.Contains(reward) == (i + 1 >= Game.PostcardRewardStamps), "sticker unlocks with the third stamp");
                Press("PostcardBack");
            }
            var save = game.CaptureProgress();
            string path = ProjectSettings.GlobalizePath($"res://artifacts/postcard-checks/{Settings.Seed}/progress.json");
            Require(SaveStore.Write(save, path), "album saves to disk");
            game.RestoreProgress(SaveStore.Read(path) ?? throw new InvalidOperationException("Postcard save failed validation"));
            Require(game.State == GameState.PostcardAlbum && game.CaptureProgress().Postcards.All(card => card?.Stamped == true),
                "album and stamps restore");
            Require(game.CaptureProgress().Sender == sender, "original delivery survives album and reload");
            Require(car.Appearance.HasAccessory(reward.Id), "sticker is still equipped after reload");
            game.FinishInteraction();
            save.Postcards = save.Postcards.Take(Game.OriginalPostcardCount).ToArray();
            save.PostcardPage = 2;
            Require(SaveStore.Write(save, path), "original three-card save written");
            game.RestoreProgress(SaveStore.Read(path) ?? throw new InvalidOperationException("Original postcard save failed validation"));
            var expanded = game.CaptureProgress();
            Require(expanded.PostcardPage == 2 && expanded.Postcards.Take(Game.OriginalPostcardCount).All(card => card?.Stamped == true) &&
                expanded.Postcards.Skip(Game.OriginalPostcardCount).All(card => card == null) && car.Appearance.HasAccessory(reward.Id),
                "original save preserves old stamps, selected postcard and sticker");
            game.FinishInteraction();
            car.SetPose(world.Village.GarageEntrance, 0);
            Talk();
            var sticker = Find("Garage_Accessory_valley-map");
            Require(sticker.Visible && !sticker.Disabled, "the sticker is selectable in the garage");
            GD.Print("POSTCARD_CHECKS_OK");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }

    private void Talk() => game._UnhandledInput(new InputEventAction { Action = "talk", Pressed = true });
    private Button Find(string name) => (Button)game.FindChild(name, true, false);
    private void Press(string name) => Find(name).EmitSignal(Button.SignalName.Pressed);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
}
