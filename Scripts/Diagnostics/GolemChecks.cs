using Godot;
using System;
using System.Linq;
using System.Text.Json;
namespace MathRacer;
public partial class GolemChecks : Node
{
    private readonly Game game; private readonly World world; private readonly Vehicle car;
    public GolemChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); GD.Print("PASS: " + message); }
    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = ProjectSettings.GlobalizePath("res://artifacts/golem-checks/progress.json");
            void RoundTrip()
            {
                var saved = game.CaptureProgress(); Check(SaveStore.Write(saved, path), "save written");
                var loaded = SaveStore.Read(path); Check(loaded != null, "save accepted");
                game.RestoreProgress(loaded!);
                Check(JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()), "golem progress restores");
            }
            car.SetPose(world.Sorcerer.Position + Vector3.Right * 2, 0); game.OpenSorcerer();
            Check(game.State == GameState.SorcererStatus && !game.GolemOwned, "sorcerer offers quest"); RoundTrip();
            game.BeginGolemQuest(); Check(game.Delivery is { Golem: true, Amount: 6, Collected: 0 }, "six part quest starts");
            for (int i = 0; i < 6; i++)
            {
                var point = game.Delivery!.PickupPositions[i]; car.SetPose(point, 0); game.Delivery.Tick(.2); game.Delivery.Tick(.2);
                Check(game.Delivery.Collected == i + 1, "part collected once");
                if (i == 2) RoundTrip();
            }
            Check(game.Delivery!.CanDeliver && !game.GolemOwned, "reward waits for handover"); RoundTrip();
            car.SetPose(world.Sorcerer.Position + Vector3.Right * 2, 0); game.BeginDeliveryQuestion();
            Check(game.Delivery == null && game.GolemOwned && game.GolemActive, "handover grants active pet"); RoundTrip();
            game.FinishInteraction();
            var pet = (Node3D)world.FindChild("GolemPet", true, false);
            car.SetPose(pet.Position + Vector3.Right * 20, 0);
            float before = pet.Position.DistanceTo(car.Position); game._Process(.25);
            Check(pet.Visible && pet.Position.DistanceTo(car.Position) < before, "pet runs toward the car");
            car.SetPose(world.Village.GarageEntrance, 0); game.OpenGarage();
            Check(game.FindChild("GaragePet_Golem", true, false) is Button { Disabled: false }, "pet available in garage");
            game.ToggleGolem(); Check(!game.GolemActive && game.GolemOwned && !pet.Visible, "pet can be disabled"); RoundTrip();
            game.ToggleGolem(); Check(game.GolemActive, "pet can be reactivated"); RoundTrip();
            GD.Print("GOLEM_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
