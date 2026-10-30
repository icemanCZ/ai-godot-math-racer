using Godot;
using System;
using System.Text.Json;
using System.Linq;

namespace MathRacer;

public partial class EventChecks(Game game, World world, Vehicle car) : Node
{
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
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
                for (float distance = 0; distance <= 92; distance += 4)
                {
                    var point = events.Sample(course, events.StartDistances[course] + distance).Center;
                    Check(world.Village.BuildingFoundations.All(b => new Vector2(point.X - b.Building.Position.X, point.Z - b.Building.Position.Z).Length() >= DrivingEvents.VillageClearance), "entire event stays at least 160 metres from village buildings");
                }
                Check(events.StartMarkers[course].Text == "☺" && events.StartMarkers[course].FixedSize && !events.StartMarkers[course].NoDepthTest,
                    "event has smile marker with same fixed screen size and occlusion as task markers");
                car.SetPose(events.Points[course, 0], 0); game._Process(.016);
                game._UnhandledInput(new InputEventAction { Action = "talk", Pressed = true });
                Check(events.Active == course, "event can start through normal interaction away from residents");
                events.Tick(events.Points[course, 3]);
                Check(events.Next == 0, "out-of-order gate does not advance event");
                events.Tick(events.Points[course, 0]);
                Check(events.Next == 1, "start gate advances event");
                var saved = game.CaptureProgress();
                string path = ProjectSettings.GlobalizePath($"res://artifacts/event-checks/{Settings.Seed}/progress.json");
                Check(SaveStore.Write(saved, path), "event checkpoint written to disk");
                var restored = SaveStore.Read(path)!;
                Check(restored != null, "event checkpoint passes save validation");
                game.RestoreProgress(restored!);
                Check(events.Active == course && events.Next == 1, "active event checkpoint restored");
                car.SetPose(events.Points[course, 1], 0);
                game.TogglePause();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Check(events.Next == 1, "pause freezes checkpoint progress");
                game.TogglePause();
                int result = -1;
                for (int i = 1; i < DrivingEvents.GateCount; i++) result = events.Tick(events.Points[course, i]);
                Check(result == course && events.Active == -1 && events.Wins[course] == 1, "course completes once");
                events.Tick(events.Points[course, 5]);
                Check(events.Wins[course] == 1, "standing in finish cannot repeat reward");
                events.Begin(course); events.Cancel();
                Check(events.Active == -1 && events.Next == 0 && events.Wins[course] == 1, "cancel keeps earned completions");
            }
            for (int course = 0; course < 2; course++)
                foreach (int gate in new[] { 0, DrivingEvents.GateCount - 1 })
                {
                    var point = game.Events.Points[course, gate]; car.SetPose(point + Vector3.Up * .2f, 0);
                    for (int frame = 0; frame < 120; frame++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                    Check(car.Position.DistanceTo(point) < 3, "event start and finish support real vehicle physics");
                }
            var counts = game.CaptureProgress(); game.Events.Cancel();
            game.RestoreProgress(JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(counts))!);
            Check(game.Events.Wins.All(w => w == 1), "completed event counts restore");
            var owner = world.Animals[0];
            var delivery = new Expedition(world, car, owner, false); game.AddChild(delivery);
            var cargo = delivery.DetachCargo();
            Check(cargo != null && delivery.DetachCargo() == null, "cargo transfers ownership exactly once");
            var fx = new Celebration(); game.AddChild(fx); fx.Setup(owner.Position, cargo, owner.Position + Vector3.Up);
            delivery.ReleaseCargo(); delivery.QueueFree();
            fx.Tick(1.4);
            Check(cargo!.GlobalPosition.DistanceTo(owner.Position + Vector3.Up) < .01f, "actual cargo arrives at recipient");
            fx.Tick(1.7); Check(fx.Finished, "celebration has bounded duration"); fx.QueueFree();
            var resident = world.Village.Residents[0];
            int mathStep = world.Village.Tasks(0).ToList().IndexOf(VillageTaskKind.Math);
            world.Village.RestoreLevels(new[] { mathStep, 0, 0, 0 });
            car.SetPose(resident.Position + Vector3.Right * 2, 0);
            game.OpenChallenge(resident); game.SubmitAnswer(game.CurrentChallenge!.Answer);
            Check(game.CelebrationShowing && game.State == GameState.HouseReward, "house upgrade starts celebration before reward panel");
            game.TogglePause(); game._Process(4);
            Check(game.CelebrationShowing, "pause freezes celebration duration");
            game.TogglePause(); game._Process(3.1);
            Check(!game.CelebrationShowing && game.State == GameState.HouseReward, "celebration returns to house reward");
            game.FinishInteraction();
            GD.Print("EVENT_CHECKS_OK"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
}
