using System;
using System.Text.Json;
using Godot;

namespace MathRacer;

/// <summary>
/// <c>--load-save-checks --seed=N --save-file=PATH</c>: loads one save file (a copy of a player's save, never the
/// live one), restores it into the world of its seed, checks that the game captures the same save back and lets the
/// game run. It prints only what the save is, not what is in it. It is not part of the test run.
/// </summary>
public partial class LoadSaveChecks : Node
{
    private readonly Game game;

    public LoadSaveChecks(Game game, World world, Vehicle car) => this.game = game;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = LaunchOptions.Value("--save-file")
                ?? throw new ArgumentException("--save-file=PATH is needed");
            var data = SaveStore.Read(path);
            Check(data != null, "the game accepts the save file");
            Check(data!.Seed == Settings.Seed, $"the save is of the world that was generated (seed {data.Seed})");
            GD.Print(
                $"SAVE: version {data.Version}, state {data.State}, trip {(data.Task.Trip == null ? "none" : data.Task.Trip.Kind + " " + data.Task.Trip.Index + (data.Task.Trip.Loaded ? " loaded" : ""))}, expedition {(data.Task.Sender >= 0 ? "yes" : "no")}, departure {data.Quiz.Departure}"
            );
            game.RestoreProgress(data);
            Check(
                JsonSerializer.Serialize(data) == JsonSerializer.Serialize(game.CaptureProgress()),
                "restoring and capturing again gives the same save"
            );
            for (int i = 0; i < 60; i++)
                game._Process(.1);
            string again = DiagnosticArtifacts.PathFor("load-save-checks/after.json");
            Check(
                SaveStore.Write(game.CaptureProgress(), again) && SaveStore.Read(again) != null,
                "the game accepts its own save after six seconds"
            );
            GD.Print("LOAD_SAVE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
