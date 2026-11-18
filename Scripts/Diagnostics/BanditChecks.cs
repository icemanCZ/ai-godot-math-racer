using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace MathRacer;

public partial class BanditChecks(Game game, World world, Vehicle car) : Node
{
    public override async void _Ready()
    {
        try
        {
            await Frames(2);
            Check(game.BanditSiteCount >= 2, "the generated valley has multiple separated forest approaches");
            var initial = game.CaptureProgress();
            initial.Quiz.Types = QuizType.Math | QuizType.Reading;
            initial.Quiz.ThreeTermMath = true;
            game.RestoreProgress(initial);
            game.OpenChallenge(world.Animals[0]);
            ChallengeChecks.Solve(game);
            game.SelectExpedition(false);
            int sender = game.CaptureProgress().Task.Sender;

            await Approach(0);
            var reading = game.CurrentChallenge as ReadingChallenge;
            Check(
                IsQuestionAt(0, 0)
                    && !car.Active
                    && reading != null
                    && reading.IsValid()
                    && game.FindChildren("*", "", true, false).OfType<WordPicture>().Count() == 3,
                "a moving car triggers the encounter and the next enabled quiz in the rotation"
            );
            game.SubmitAnswer(reading!.Choices.First(choice => !reading.Accepts(choice)));
            Check(IsQuestionAt(0, 0), "wrong answer leaves the same question open");
            game.TogglePause();
            string path = DiagnosticArtifacts.PathFor($"bandit-checks/{Settings.Seed}/progress.json");
            Check(SaveStore.Write(game.CaptureProgress(), path), "paused encounter saves to disk");
            game.RestoreProgress(
                SaveStore.Read(path) ?? throw new InvalidOperationException("Bandit save failed validation.")
            );
            Check(
                IsQuestionAt(0, 0)
                    && game.CurrentChallenge is ReadingChallenge restored
                    && restored.Word == reading.Word
                    && restored.Pictures.SequenceEqual(reading.Pictures),
                "restore resumes the same question with the same pictures"
            );
            ChallengeChecks.Solve(game);
            Check(
                IsQuestionAt(0, 1) && game.CurrentChallenge is MathChallenge { Third: > 0 },
                "second question follows the saved rotation and the selected arithmetic level"
            );
            ChallengeChecks.Solve(game);
            Check(IsQuestionAt(0, 2), "two solves leave the final question");
            ChallengeChecks.Solve(game);
            Check(
                game.State == GameState.Exploring && car.Active && game.CaptureProgress().Task.Sender == sender,
                "third solve sends the bandits away and resumes the expedition"
            );
            await Approach(1);
            Check(game.State == GameState.Exploring, "a different site remains quiet during the cooldown");
            game._Process(36);
            await Approach(0);
            Check(game.State == GameState.Exploring, "the previous site is skipped after the cooldown");
            await Approach(1);
            Check(IsQuestionAt(1, 0), "a new forest site triggers the next encounter");
            game.FinishInteraction();
            Check(game.State == GameState.Exploring && car.Active, "back leaves an encounter without losing the cargo");
            GD.Print("BANDIT_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            Input.ActionRelease("accelerate");
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private bool IsQuestionAt(int site, int solved)
    {
        var save = game.CaptureProgress();
        return game.State == GameState.Challenge
            && save.Nearby == Game.BanditActorId
            && save.Bandits.Site == site
            && save.Bandits.Solved == solved;
    }

    private async Task Approach(int index)
    {
        var (road, along) = game.BanditApproach(index);
        var start = road - along * 6;
        start.Y = world.Routes.SurfaceHeight(start, world.Ground.Height(start.X, start.Z)) + .15f;
        car.SetPose(start, Mathf.Atan2(-along.X, -along.Z));
        Input.ActionPress("accelerate");
        await Frames(24);
        Input.ActionRelease("accelerate");
        await Frames(100);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
}
