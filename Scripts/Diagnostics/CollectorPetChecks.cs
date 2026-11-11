using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MathRacer;

internal sealed partial class CollectorPetChecks(Game game, World world, Vehicle car) : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private Button PetButton(CollectorPet pet) =>
        (Button)game.FindChild($"GaragePet_{pet.Kind}", true, false);

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var pets = world.Village.CollectorPets;
            var homes = pets.ToDictionary(p => p.Kind, p => p.Position);
            Check(pets.Count == 4 && pets.All(p => p.Visible && !p.IsOwned &&
                p.Position.DistanceTo(p.Collector.Position) < 3), "all four pets wait next to their collectors");

            var almostDone = game.CaptureProgress();
            almostDone.FarmCount = Village.FarmKinds.Length - 1;
            almostDone.GardenCount = Village.FlowerNames.Length - 1;
            almostDone.GemCount = Village.GemNames.Length - 1;
            almostDone.ToyCount = Village.ToyNames.Length - 1;
            game.RestoreProgress(almostDone);
            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            Check(pets.All(p => PetButton(p) is { Disabled: true, Text: "" } button &&
                button.GetNode<TextureRect>("PetPortrait").Material is ShaderMaterial),
                "all pets show grayscale portraits without captions at eleven deliveries");
            game.FinishInteraction();

            foreach (var pet in pets)
            {
                car.SetPose(pet.Collector.Position + Vector3.Right * 2, 0);
                game.OpenChallenge(pet.Collector);
                game.SubmitAnswer(game.CurrentChallenge!.Answer);
                car.SetPose(game.Delivery!.PickupPositions[0], 0);
                game.Delivery.Tick(.25);
                Check(game.Delivery.CanDeliver && !pet.IsOwned, $"{pet.Kind}: picking up the last item leaves the pet locked");
                car.SetPose(pet.Collector.Position + Vector3.Right * 2, 0);
                game.BeginDeliveryQuestion();
                Check(game.Delivery == null && pet.IsActive && pet.Position == homes[pet.Kind],
                    $"{pet.Kind}: the final handover activates the pet beside its collector");
                game.FinishInteraction();
            }

            car.SetPose(world.Village.GarageEntrance, 0);
            game._Process(.05);
            var before = pets.ToDictionary(p => p.Kind, p => p.Position);
            car.SetPose(car.Position + Vector3.Forward * 12, 0);
            game._Process(.25);
            Check(pets.All(p => p.Visible && p.Position.DistanceTo(car.Position) < before[p.Kind].DistanceTo(car.Position)) &&
                pets.Select(p => p.Position).Distinct().Count() == 4, "all active pets run toward the car in separate positions");
            var pausedPositions = pets.Select(p => p.Position).ToArray();
            game.TogglePause();
            game._Process(.25);
            Check(pets.Select(p => p.Position).SequenceEqual(pausedPositions), "pets stop when paused");
            game.TogglePause();

            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            foreach (var pet in pets)
            {
                Check(!PetButton(pet).Disabled && PetButton(pet).GetNode<TextureRect>("PetPortrait").Material == null &&
                    PetButton(pet).HasNode("PetActive"), $"{pet.Kind}: active pets have a color portrait and a badge");
                PetButton(pet).EmitSignal(Button.SignalName.Pressed);
                Check(pet.IsOwned && !pet.IsActive && !pet.Visible && !PetButton(pet).HasNode("PetActive"),
                    $"{pet.Kind}: garage toggle hides the pet and clears the badge");
            }
            PetButton(pets[0]).EmitSignal(Button.SignalName.Pressed);
            Check(pets[0].Visible && pets.Skip(1).All(p => !p.Visible), "reactivating a pet leaves the other choices intact");

            string path = ProjectSettings.GlobalizePath("res://artifacts/collector-pet-checks/progress.json");
            var saved = game.CaptureProgress();
            Check(SaveStore.Write(saved, path), "pet preferences are written");
            game.RestoreProgress(SaveStore.Read(path) ?? throw new InvalidOperationException("Pet save was rejected."));
            Check(pets[0].IsActive && pets.Skip(1).All(p => p.IsOwned && !p.IsActive && !p.Visible), "reload restores ownership and individual toggles");

            var legacy = JsonNode.Parse(JsonSerializer.Serialize(saved))!.AsObject();
            legacy.Remove(nameof(SaveData.DisabledCollectorPets));
            File.WriteAllText(path, legacy.ToJsonString());
            game.RestoreProgress(SaveStore.Read(path) ?? throw new InvalidOperationException("Legacy save was rejected."));
            Check(pets.All(p => p.IsActive && p.Visible), "older saves with complete collections unlock the corresponding pets");
            game.RestoreProgress(almostDone);
            Check(pets.All(p => !p.IsOwned && p.Visible && p.Position == homes[p.Kind]), "loading earlier progress returns the pets to their collectors");
            GD.Print("COLLECTOR_PET_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
