using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class WeatherChecks(Game game, World world, Vehicle car) : Node
{
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var weather = game.Weather;
            var puddles = weather.GetChildren().OfType<Puddles>().Single();
            var rain = weather.GetChildren().OfType<MultiMeshInstance3D>().Single();
            var bolt = weather.GetChildren().OfType<MeshInstance3D>().Single();
            weather.Tick(239);
            Check(weather.Current == WeatherKind.Clear && !rain.Visible && !puddles.Visible, "Clear for the first four minutes");
            weather.Tick(182);
            Check(weather.Current != WeatherKind.Clear, "Automatic weather starts by the seventh minute");
            weather.Tick(61);
            Check(weather.Current == WeatherKind.Clear, "Automatic weather returns to clear within a minute");

            var witch = world.Witch;
            Check(!world.Village.TerrainArea.HasPoint(new Vector2(witch.Position.X, witch.Position.Z)) &&
                world.Routes.Distance(witch.Position) < Settings.InteractionRange, "Witch is behind the village and reachable from the road");
            car.SetPose(witch.Position + witch.Basis.Z * 3 + Vector3.Up * .2f, 0);
            game._Process(.016);
            game._UnhandledInput(new InputEventAction { Action = "talk", Pressed = true });
            Check(game.CurrentChallenge is MathChallenge && game.State == GameState.Challenge, "Normal interaction opens a math challenge");
            game.SubmitAnswer(game.CurrentChallenge!.Choices.First(a => a != game.CurrentChallenge.Answer));
            Check(game.State == GameState.Challenge && weather.Current == WeatherKind.Clear, "Wrong answer leaves the weather unchanged");
            game.SubmitAnswer(game.CurrentChallenge.Answer);
            Check(game.State == GameState.WeatherChoice, "Correct answer unlocks weather choice");
            string path = ProjectSettings.GlobalizePath($"res://artifacts/weather-checks/{Settings.Seed}/progress.json");
            Check(SaveStore.Write(game.CaptureProgress(), path), "Weather choice can be saved");
            var progress = SaveStore.Read(path);
            Check(progress?.State == GameState.WeatherChoice, "Weather choice passes save validation");
            game.RestoreProgress(progress!);
            game.SelectWeather(WeatherKind.Storm);
            Check(game.State == GameState.Exploring && weather.Current == WeatherKind.Storm, "Storm starts and returns the player to driving");
            for (int step = 0; step < 70 && !bolt.Visible; step++) weather.Tick(.1f);
            Check(bolt.Visible && rain.Visible && puddles.Visible, "Storm produces lightning, rain and puddles");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            weather.Tick(8);

            var (patch, forward) = world.Routes.NavigationAnchors[1];
            var approach = patch - forward * 7;
            approach.Y = world.Routes.SurfaceHeight(approach, world.Ground.Height(approach.X, approach.Z));
            car.SetPose(approach + Vector3.Up * .15f, Mathf.Atan2(-forward.X, -forward.Z));
            Input.ActionPress("accelerate");
            bool splashed = false;
            for (int frame = 0; frame < 150 && !splashed; frame++)
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                splashed = puddles.GetChildren().OfType<CpuParticles3D>().Any(emitter => emitter.Emitting);
            }
            Input.ActionRelease("accelerate");
            Check(splashed, "Driving through a puddle emits water at the wheels");
            weather.Begin(WeatherKind.Wind);
            weather.Tick(3);
            Check(!rain.Visible && puddles.Visible, "Wind stops the rain while puddles remain");
            weather.Tick(36);
            Check(!puddles.Visible, "Puddles dry out after the rain");
            game.RestoreProgress(progress!);
            var beforeBack = weather.Current;
            game._Input(new InputEventAction { Action = "back", Pressed = true });
            Check(game.State == GameState.Exploring && weather.Current == beforeBack, "Back leaves the weather choice without casting");
            GD.Print("WEATHER_CHECKS_OK");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            Input.ActionRelease("accelerate");
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
}
