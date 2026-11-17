using System;
using System.Linq;
using System.Text.Json;
using Godot;

namespace MathRacer;

public partial class EventChecks(Game game, World world, Vehicle car) : Node
{
    private static void Check(bool ok, string message)
    {
        if (!ok)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    /// <summary>Drives a whole course at <paramref name="gateSeconds"/> per gate through the event's own clock.</summary>
    private static int Run(DrivingEvents events, int course, float gateSeconds)
    {
        events.Begin(course);
        events.Tick(events.Points[course, 0]);
        int result = -1;
        for (int i = 1; i < DrivingEvents.GateCount; i++)
        {
            events.Advance(gateSeconds);
            result = events.Tick(events.Points[course, i]);
        }
        return result;
    }

    private async System.Threading.Tasks.Task GhostChecks()
    {
        var events = game.Events;
        for (int course = 0; course < 2; course++)
        {
            float pace = events.GhostTime(course);
            Check(pace is > 15 and < 120, "pace ghost takes a sensible time");
            for (float time = 0; time <= pace + 2; time += 1)
            {
                var pose = events.GhostPose(course, time);
                Check(
                    world.Routes.Distance(pose.Position) < 3 && float.IsFinite(pose.Heading),
                    "ghost stays on the road"
                );
            }
            Check(
                events.GhostPose(course, 0).Position.DistanceTo(events.Points[course, 0]) < 1
                    && events
                        .GhostPose(course, pace + 5)
                        .Position.DistanceTo(events.Points[course, DrivingEvents.GateCount - 1]) < 1,
                "ghost starts at the first gate and finishes at the last"
            );
            events.Begin(course);
            Check(events.GhostVisible, "ghost appears during a run");
            events.Advance(5);
            Check(events.Elapsed == 0, "race clock waits for the start gate");
            events.Tick(events.Points[course, 0]);
            events.Advance(2);
            events.Tick(events.Points[course, 1]);
            Check(!float.IsNaN(events.Gap), "gap to the ghost is known after a gate");
            events.Cancel();
            Check(!events.GhostVisible && events.Elapsed == 0 && events.RunSplits.Count == 0, "cancel clears the race");

            Check(Run(events, course, 2) == course && events.Result.BeatGhost, "a fast run beats the pace ghost");
            var record = events.BestSplits[course];
            Check(
                DrivingEvents.ValidSplits(record) && events.GhostTime(course) < pace,
                "the record replaces the ghost"
            );
            var saved = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(game.CaptureProgress()))!;
            game.Events.Cancel();
            game.RestoreProgress(saved);
            Check(events.BestSplits[course].SequenceEqual(record), "the record survives saving");
            Run(events, course, 3);
            Check(
                !events.Result.BeatGhost && events.BestSplits[course].SequenceEqual(record),
                "a slower run keeps the record"
            );
            Run(events, course, 1.5f);
            Check(events.Result.BeatGhost && events.GhostTime(course) < record[^1], "a faster run improves the record");
        }
        // Saves from before the ghost race have a run under way but no clock or gate times.
        var old = game.CaptureProgress();
        old.EventActive = 0;
        old.EventNext = 3;
        old.EventSplits = Array.Empty<float>();
        old.EventBest = null!;
        string path = DiagnosticArtifacts.PathFor($"event-checks/{Settings.Seed}/old-run.json");
        Check(SaveStore.Write(old, path), "old-style event save written");
        var migrated = SaveStore.Read(path);
        Check(
            migrated != null && migrated.EventActive == -1 && migrated.EventNext == 0 && migrated.EventBest.Length == 2,
            "a run without a clock is dropped when an old save is read"
        );
        var damaged = game.CaptureProgress();
        damaged.EventBest = new[] { new float[] { 3, 2, 1 }, Array.Empty<float>() };
        Check(SaveStore.Write(damaged, path), "damaged record written");
        Check(
            SaveStore.Read(path)?.EventBest.All(best => best.Length == 0) ?? false,
            "a damaged record falls back to the pace ghost"
        );
        // A finish through the game itself pays the course coins plus the bonus for beating the ghost.
        var events0 = game.Events;
        events0.Cancel();
        game.RestoreProgress(JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(game.CaptureProgress()))!);
        int coins = game.Coins;
        events0.Begin(0);
        for (int i = 0; i < DrivingEvents.GateCount; i++)
        {
            car.SetPose(events0.Points[0, i], 0);
            game._Process(.05);
            if (i < DrivingEvents.GateCount - 1)
                game._Process(.05);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(
            events0.Active == -1
                && events0.Result.BeatGhost
                && game.Coins - coins == Game.CourseCoins + Game.GhostCoins,
            "beating the ghost pays the bonus coins"
        );
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            for (int course = 0; course < 2; course++)
            {
                var events = game.Events;
                for (int i = 0; i < DrivingEvents.GateCount; i++)
                    Check(world.Routes.Distance(events.Points[course, i]) < 2, "event gate stays on road");
                GD.Print(
                    $"Course {course}: {events.CourseLength(course):0} m, gate spacing {events.GateSpacing[course]:0.0} m"
                );
                Check(events.CourseLength(course) >= 100, "course is long enough to be worth racing");
                for (float distance = 0; distance <= events.CourseLength(course); distance += 4)
                {
                    var point = events.Sample(course, events.StartDistances[course] + distance).Center;
                    Check(
                        world.Village.BuildingFoundations.All(b =>
                            new Vector2(point.X - b.Building.Position.X, point.Z - b.Building.Position.Z).Length()
                            >= DrivingEvents.VillageClearance
                        ),
                        "entire event stays at least 160 metres from village buildings"
                    );
                }
                Check(
                    events.StartMarkers[course].Text == ""
                        && events.StartMarkers[course].GetChildren().OfType<Sprite3D>().Count() == 1
                        && events.StartMarkers[course].FixedSize
                        && !events.StartMarkers[course].NoDepthTest,
                    "event has an icon marker with same fixed screen size and occlusion as task markers"
                );
                car.SetPose(events.Points[course, 0], 0);
                game._Process(.016);
                game._UnhandledInput(new InputEventAction { Action = "talk", Pressed = true });
                Check(events.Active == course, "event can start through normal interaction away from residents");
                events.Tick(events.Points[course, 3]);
                Check(events.Next == 0, "out-of-order gate does not advance event");
                events.Tick(events.Points[course, 0]);
                Check(events.Next == 1, "start gate advances event");
                var saved = game.CaptureProgress();
                string path = DiagnosticArtifacts.PathFor($"event-checks/{Settings.Seed}/progress.json");
                Check(SaveStore.Write(saved, path), "event checkpoint written to disk");
                var restored = SaveStore.Read(path)!;
                Check(restored != null, "event checkpoint passes save validation");
                game.RestoreProgress(restored!);
                Check(events.Active == course && events.Next == 1, "active event checkpoint restored");
                Check(events.RunSplits.Count == 1, "run clock checkpoint restored");
                car.SetPose(events.Points[course, 1], 0);
                game.TogglePause();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(events.Next == 1, "pause freezes checkpoint progress");
                game.TogglePause();
                int result = -1;
                for (int i = 1; i < DrivingEvents.GateCount; i++)
                {
                    events.Advance(5);
                    result = events.Tick(events.Points[course, i]);
                }
                Check(result == course && events.Active == -1 && events.Wins[course] == 1, "course completes once");
                Check(
                    !events.Result.BeatGhost && events.BestSplits[course].Length == 0,
                    "a run slower than the pace ghost leaves the pace ghost in place"
                );
                events.Tick(events.Points[course, 5]);
                Check(events.Wins[course] == 1, "standing in finish cannot repeat reward");
                events.Begin(course);
                events.Cancel();
                Check(
                    events.Active == -1 && events.Next == 0 && events.Wins[course] == 1,
                    "cancel keeps earned completions"
                );
            }
            for (int course = 0; course < 2; course++)
                foreach (int gate in new[] { 0, DrivingEvents.GateCount - 1 })
                {
                    var point = game.Events.Points[course, gate];
                    car.SetPose(point + Vector3.Up * .2f, 0);
                    for (int frame = 0; frame < 120; frame++)
                        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    Check(car.Position.DistanceTo(point) < 3, "event start and finish support real vehicle physics");
                }
            var counts = game.CaptureProgress();
            game.Events.Cancel();
            game.RestoreProgress(JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(counts))!);
            Check(game.Events.Wins.All(w => w == 1), "completed event counts restore");
            await GhostChecks();
            var owner = world.Animals[0];
            var delivery = new Expedition(world, car, owner, false);
            game.AddChild(delivery);
            var cargo = delivery.DetachCargo();
            Check(cargo != null && delivery.DetachCargo() == null, "cargo transfers ownership exactly once");
            var fx = new Celebration();
            game.AddChild(fx);
            fx.Setup(owner.Position, cargo, owner.Position + Vector3.Up);
            delivery.ReleaseCargo();
            delivery.QueueFree();
            fx.Tick(1.4);
            Check(
                cargo!.GlobalPosition.DistanceTo(owner.Position + Vector3.Up) < .01f,
                "actual cargo arrives at recipient"
            );
            fx.Tick(1.7);
            Check(fx.Finished, "celebration has bounded duration");
            fx.QueueFree();
            var resident = world.Village.Residents[0];
            int mathStep = world.Village.Tasks(0).ToList().IndexOf(VillageTaskKind.Math);
            world.Village.RestoreLevels(new[] { mathStep, 0, 0, 0 });
            car.SetPose(resident.Position + Vector3.Right * 2, 0);
            game.OpenChallenge(resident);
            ChallengeChecks.Solve(game);
            Check(
                game.CelebrationShowing && game.State == GameState.HouseReward,
                "house upgrade starts celebration before reward panel"
            );
            game.TogglePause();
            game._Process(4);
            Check(game.CelebrationShowing, "pause freezes celebration duration");
            game.TogglePause();
            game._Process(3.1);
            Check(
                !game.CelebrationShowing && game.State == GameState.HouseReward,
                "celebration returns to house reward"
            );
            game.FinishInteraction();
            GD.Print("EVENT_CHECKS_OK");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
