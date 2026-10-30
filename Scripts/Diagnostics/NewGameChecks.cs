using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class NewGameChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private static int stage;
    private static int oldSeed;
    private static WorldPlan? oldPlan;
    public NewGameChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
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
            var ui = game.GetChildren().OfType<GameUi>().Single();
            if (stage > 0)
            {
                Check(Settings.Seed != oldSeed && !ReferenceEquals(oldPlan, WorldLayout.Plan), "new game rebuilds the world with a fresh seed");
                Check(game.State == GameState.Exploring && car.Active, "new game starts driving automatically");
                Check(game.RemainingAnimals == World.QuestAnimalCount && game.RemainingDeliveries == World.QuestAnimalCount && game.OwnedRewards.Count == 2 &&
                    game.Delivery == null && game.CurrentChallenge == null && car.Appearance.CurrentModelId == "classic",
                    "new game clears tasks, expeditions and cosmetics");
                Check(game.CaptureProgress().Seed == Settings.Seed, "new progress uses the new seed");
                if (stage == 2) { GD.Print("NEW_GAME_CHECKS_PASSED"); GetTree().Quit(); return; }
            }
            if (stage == 0)
            {
                Check(game.State == GameState.Title, "new game is available from title");
                Check(game.FindChildren("*", "Label", true, false).Cast<Label>().Any(l => l.Text == $"Seed údolí: {Settings.Seed}"),
                    "title displays the current seed");
            }
            else
            {
                game.OpenChallenge(world.Animals[0]); game.SubmitAnswer(game.CurrentChallenge!.Answer); game.SelectExpedition(true);
                game.OwnedRewards.Add(new("buggy", false, true)); car.Appearance.ApplyModel("buggy");
                game.TogglePause();
                Check(game.State == GameState.Paused, "new game is available during a paused expedition");
            }
            oldSeed = Settings.Seed; oldPlan = WorldLayout.Plan; stage++;
            ((Button)ui.FindChild("NewGame", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
