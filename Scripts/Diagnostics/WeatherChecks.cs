using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

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
            Check(
                world.Plants.TreeBatchesShareWindRoots(),
                "trunks, foliage and small tree details share one wind origin per tree"
            );
            weather.Tick(239);
            Check(
                weather.Current == WeatherKind.Clear && !rain.Visible && !puddles.Visible,
                "Clear for the first four minutes"
            );
            weather.Tick(182);
            Check(weather.Current != WeatherKind.Clear, "Automatic weather starts by the seventh minute");
            weather.Tick(61);
            Check(weather.Current == WeatherKind.Clear, "Automatic weather returns to clear within a minute");

            var witch = world.Witch;
            Check(
                !world.Village.TerrainArea.HasPoint(new Vector2(witch.Position.X, witch.Position.Z))
                    && world.Routes.Distance(witch.Position) < Settings.InteractionRange,
                "Witch is behind the village and reachable from the road"
            );
            car.SetPose(witch.Position + witch.Basis.Z * 3 + Vector3.Up * .2f, 0);
            game._Process(.016);
            game._UnhandledInput(new InputEventAction { Action = "talk", Pressed = true });
            Check(
                game.CurrentChallenge is MathChallenge && game.State == GameState.Challenge,
                "Normal interaction opens a math challenge"
            );
            game.SubmitAnswer(game.CurrentChallenge!.Choices.First(choice => !game.CurrentChallenge.Accepts(choice)));
            Check(
                game.State == GameState.Challenge && weather.Current == WeatherKind.Clear,
                "Wrong answer leaves the weather unchanged"
            );
            ChallengeChecks.Solve(game);
            Check(game.State == GameState.WeatherChoice, "Correct answer unlocks weather choice");
            string path = DiagnosticArtifacts.PathFor($"weather-checks/{Settings.Seed}/progress.json");
            Check(SaveStore.Write(game.CaptureProgress(), path), "Weather choice can be saved");
            var progress = SaveStore.Read(path);
            Check(progress?.State == GameState.WeatherChoice, "Weather choice passes save validation");
            game.RestoreProgress(progress!);
            game.SelectWeather(WeatherKind.Storm);
            Check(
                game.State == GameState.Exploring && weather.Current == WeatherKind.Storm,
                "Storm starts and returns the player to driving"
            );
            for (int step = 0; step < 70 && !bolt.Visible; step++)
                weather.Tick(.1f);
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
            CheckEveningAndNight(weather);
            game.RestoreProgress(progress!);
            var beforeBack = weather.Current;
            game._Input(new InputEventAction { Action = "back", Pressed = true });
            Check(
                game.State == GameState.Exploring && weather.Current == beforeBack,
                "Back leaves the weather choice without casting"
            );
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

    /// <summary>The evening and the night: only the witch brings them, and they light the lamps of the valley.</summary>
    private void CheckEveningAndNight(Weather weather)
    {
        var sky = world.Atmosphere.GetNode<Node3D>("NightSky");
        var headlights = car.GetNode<SpotLight3D>("Headlights");
        var fireflies = weather.GetNode<Node3D>("Night");
        Check(
            !sky.Visible && !headlights.Visible && !fireflies.Visible,
            "By day there are no stars, lights or fireflies"
        );

        // Two finished houses, one of them given glasses by the glassworks, and a plot that is not built yet.
        world.Village.RestoreLevels(new[] { 10, 10, 0, 0 });
        world.Village.RestoreGlazed(new[] { true, false, false, false });
        weather.Begin(WeatherKind.Evening);
        weather.Tick(3);
        Check(!sky.Visible, "Evening has no stars yet");
        weather.Tick(10);
        Check(
            headlights.Visible && fireflies.Visible,
            "Evening turns on the headlights and brings the first fireflies"
        );
        Check(!weather.StarsOut, "Evening is not deep enough for the stargazer's pictures");
        Check(
            world.Village.Houses[0].GetNodeOrNull<Node3D>("WindowLights") is { Visible: true },
            "A house with glasses has lit windows in the evening"
        );
        Check(
            world.Village.Houses[1].GetNodeOrNull<Node3D>("WindowLights") is not { Visible: true },
            "A finished house without glasses stays dark"
        );

        weather.Begin(WeatherKind.Night);
        weather.Tick(15);
        Check(sky.Visible && weather.StarsOut, "Night brings the stars and the moon");
        Check(sky.GetNode<MultiMeshInstance3D>("Stars").Multimesh.InstanceCount > 100, "The night sky has stars");
        var picture = Constellation.All[0];
        world.Atmosphere.ShowConstellation(picture, 0, 1);
        Check(
            sky.GetNode<Node3D>("Constellation").GetChildCount() == picture.Stars.Length + picture.Lines.Length / 2,
            "A constellation is a star for every point and a line for every pair"
        );
        Check(
            Constellation.All.All(c => c.Lines.Length % 2 == 0 && c.Lines.All(i => i >= 0 && i < c.Stars.Length)),
            "Every constellation joins stars that exist"
        );

        weather.Begin(WeatherKind.Clear);
        weather.Tick(30);
        Check(
            !sky.Visible && !headlights.Visible && !fireflies.Visible,
            "Morning puts out the stars, the headlights and the fireflies"
        );
        Check(
            world.Village.Houses[0].GetNodeOrNull<Node3D>("WindowLights") is { Visible: false },
            "Morning puts out the lamps in the windows"
        );
        world.Village.RestoreLevels(new[] { 0, 0, 0, 0 });
        world.Village.RestoreGlazed(null);
    }

    private static void Check(bool ok, string message)
    {
        if (!ok)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }
}
