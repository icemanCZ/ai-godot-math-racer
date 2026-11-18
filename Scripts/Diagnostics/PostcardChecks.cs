using System;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class PostcardChecks(Game game, World world, Vehicle car) : Node
{
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var initial = game.CaptureProgress();
            Require(
                game.PostcardQuestion(0) is SyllableChallenge
                    && game.PostcardQuestion(1) is LetterChallenge
                    && game.PostcardQuestion(2) is ReadingChallenge,
                "postcards use the available word activities by default"
            );
            initial.Quiz.Types = QuizType.Math | QuizType.Syllables;
            game.RestoreProgress(initial);
            var owner = world.Animals[0];
            game.OpenChallenge(owner);
            ChallengeChecks.Solve(game);
            game.SelectExpedition(false);
            int sender = game.CaptureProgress().Task.Sender;
            Require(sender >= 0, "normal expedition starts before the postcard trip");

            string path = DiagnosticArtifacts.PathFor($"postcard-checks/{Settings.Seed}/progress.json");
            for (int i = 0; i < Game.PostcardCount; i++)
            {
                car.SetPose(game.PostcardStop(i) + Vector3.Up * .2f, 0);
                Input.ActionPress("accelerate");
                for (int tick = 0; tick < 12; tick++)
                    car._PhysicsProcess(1.0 / 60);
                Input.ActionRelease("accelerate");
                Require(car.Speed > 1, "car has not stopped at the viewpoint");
                game._Process(.016);
                var key = i % 2 == 0 ? Key.E : Key.Space;
                game._UnhandledInput(
                    new InputEventKey
                    {
                        Keycode = key,
                        PhysicalKeycode = key,
                        Pressed = true,
                    }
                );
                Require(
                    game.State == GameState.PostcardAlbum && !car.Active,
                    "E and Space can open the camera while driving"
                );
                game.TogglePause();
                game._Process(2);
                Require(game.CaptureProgress().Postcards.Cards[i] == null, "pause holds the camera shot");
                game.TogglePause();
                game._Process(2);
                Require(
                    game.CaptureProgress().Postcards.Cards[i] is { Stamped: false },
                    "camera adds an unstamped postcard"
                );
                var question = (SyllableChallenge)game.PostcardQuestion(i)!;
                string wrongChoice = $"Answer{question.Choices.First(choice => !question.Accepts(choice))}";
                var choice = Find(wrongChoice);
                Require(
                    choice.Text.Length > 0 && question.Choices.All(index => Find($"Answer{index}") != null),
                    "postcard has six syllable choices"
                );
                Press(wrongChoice);
                Require(Find(wrongChoice).Disabled, "wrong syllable is marked and disabled");
                Require(
                    game.CaptureProgress().Postcards.Cards[i] is { Stamped: false },
                    "wrong answer leaves the postcard unstamped"
                );
                if (question.Syllables.Count > 1)
                {
                    int correct = question.Choices.Last(question.Accepts);
                    Press($"Answer{correct}");
                    Require(
                        game.CaptureProgress().Postcards.Cards[i] is { Stamped: false },
                        "a partial word does not grant a stamp"
                    );
                    Require(SaveStore.Write(game.CaptureProgress(), path), "partial postcard saves to disk");
                    game.RestoreProgress(
                        SaveStore.Read(path)
                            ?? throw new InvalidOperationException("Partial postcard save failed validation")
                    );
                    Require(
                        Find($"Answer{correct}").Disabled && Find(wrongChoice).Disabled,
                        "reload keeps the marked syllables"
                    );
                }
                game.FinishInteraction();
            }

            var square = world.Village.Square;
            car.SetPose(square.ToGlobal(new Vector3(0, .2f, 4.4f)), 0);
            game._Process(.016);
            Talk();
            Require(game.State == GameState.PostcardAlbum, "album is accessible by the fountain");
            var reward = new CarReward(CarAccessory.ValleyMap.Id, true);
            for (int i = 0; i < Game.PostcardCount; i++)
            {
                Press($"Postcard{i}");
                var question = game.PostcardQuestion(i)!;
                foreach (int choice in question.Choices.Where(question.Accepts).Reverse())
                    if (!Find($"Answer{choice}").Disabled)
                        Press($"Answer{choice}");
                Require(
                    game.CaptureProgress().Postcards.Cards[i] is { Stamped: false }
                        && !Find("ConfirmSyllables").Disabled,
                    "selecting the last syllable leaves the card unstamped"
                );
                Require(SaveStore.Write(game.CaptureProgress(), path), "a complete unconfirmed word saves to disk");
                game.RestoreProgress(
                    SaveStore.Read(path)
                        ?? throw new InvalidOperationException("Complete unconfirmed word failed validation")
                );
                Press("ConfirmSyllables");
                Require(
                    game.CaptureProgress().Postcards.Cards[i] is { Stamped: true },
                    "confirming the completed word stamps the selected postcard"
                );
                Require(
                    game.OwnedRewards.Contains(reward) == (i + 1 >= Game.PostcardRewardStamps),
                    "sticker unlocks with the third stamp"
                );
                Press("PostcardBack");
            }
            var save = game.CaptureProgress();
            Require(SaveStore.Write(save, path), "album saves to disk");
            game.RestoreProgress(
                SaveStore.Read(path) ?? throw new InvalidOperationException("Postcard save failed validation")
            );
            Require(
                game.State == GameState.PostcardAlbum
                    && game.CaptureProgress().Postcards.Cards.All(card => card?.Stamped == true),
                "album and stamps restore"
            );
            Require(game.CaptureProgress().Task.Sender == sender, "original delivery survives album and reload");
            Require(car.Appearance.HasAccessory(reward.Id), "sticker is still equipped after reload");
            game.FinishInteraction();
            save.Postcards.Cards = save.Postcards.Cards.Take(Game.OriginalPostcardCount).ToArray();
            save.Postcards.Page = 2;
            Require(SaveStore.Write(save, path), "original three-card save written");
            game.RestoreProgress(
                SaveStore.Read(path) ?? throw new InvalidOperationException("Original postcard save failed validation")
            );
            var expanded = game.CaptureProgress();
            Require(
                expanded.Postcards.Page == 2
                    && expanded.Postcards.Cards.Take(Game.OriginalPostcardCount).All(card => card?.Stamped == true)
                    && expanded.Postcards.Cards.Skip(Game.OriginalPostcardCount).All(card => card == null)
                    && car.Appearance.HasAccessory(reward.Id),
                "original save preserves old stamps, selected postcard and sticker"
            );
            game.FinishInteraction();
            car.SetPose(world.Village.GarageEntrance, 0);
            Talk();
            var sticker = Find("Garage_Accessory_valley-map");
            Require(sticker.Visible && !sticker.Disabled, "the sticker is selectable in the garage");
            GD.Print("POSTCARD_CHECKS_OK");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void Talk() => game._UnhandledInput(new InputEventAction { Action = "talk", Pressed = true });

    private Button Find(string name) => (Button)game.FindChild(name, true, false);

    private void Press(string name) => Find(name).EmitSignal(Button.SignalName.Pressed);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
}
