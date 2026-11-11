using Godot;
using System.Linq;

namespace MathRacer;

public partial class Game
{
    private void ToggleCollectorPet(CollectorPetKind kind)
    {
        if (State != GameState.Garage) return;
        var pet = world.Village.CollectorPets.First(p => p.Kind == kind);
        if (!pet.IsOwned) return;
        pet.Disabled = !pet.Disabled;
        PlaceCollectorPet(pet);
        ShowGarage();
        SaveProgress();
    }

    private Vector3 CollectorPetTarget(CollectorPet pet) => vehicle.Position + vehicle.Basis * pet.TrailOffset;

    private void PlaceCollectorPet(CollectorPet pet)
    {
        pet.Visible = !pet.IsOwned || pet.IsActive;
        if (pet.IsActive)
        {
            var target = CollectorPetTarget(pet);
            target.Y = CompanionSurface(target);
            pet.Place(target, vehicle.Heading + Mathf.Pi);
        }
        else pet.ReturnHome();
    }

    private void TickCollectorPets(float delta)
    {
        if (State is GameState.Title or GameState.Paused) return;
        foreach (var pet in world.Village.CollectorPets)
        {
            if (!pet.IsOwned)
            {
                pet.Animate(delta, 0);
                continue;
            }
            if (!pet.IsActive || State != GameState.Exploring) continue;
            var target = CollectorPetTarget(pet);
            var offset = target - pet.Position;
            offset.Y = 0;
            float distance = offset.Length();
            if (distance > 35)
            {
                PlaceCollectorPet(pet);
                continue;
            }
            float speed = Mathf.Clamp((distance - .25f) * 3.4f, 0, 24);
            if (speed > 0)
            {
                var position = pet.Position + offset / distance * Mathf.Min(distance, speed * delta);
                position.Y = CompanionSurface(position);
                pet.Position = position;
                float yaw = Mathf.Atan2(offset.X, offset.Z);
                pet.Rotation = new(0, Mathf.LerpAngle(pet.Rotation.Y, yaw, 1 - Mathf.Exp(-12 * delta)), 0);
            }
            pet.Animate(delta, speed);
        }
    }
}
