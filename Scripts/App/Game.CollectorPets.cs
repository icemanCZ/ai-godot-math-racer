using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Valley;

namespace MathRacer.App;

public partial class Game
{
    /// <summary>The golem wanders after the pets, so its slot is the one past the last pet kind.</summary>
    private static readonly int GolemWanderIndex = System.Enum.GetValues<CollectorPetKind>().Length;

    private struct CompanionWander
    {
        internal Vector3? Goal;
        internal float Pause;
    }

    private readonly CompanionWander[] companionWanders = new CompanionWander[GolemWanderIndex + 1];
    private readonly RandomNumberGenerator companionRandom = new() { Seed = (ulong)Settings.Seed + 710 };
    private float companionStopTime;

    /// <summary>The pets of the world that the player already owns, so that only a new one is announced.</summary>
    private readonly HashSet<CollectorPetKind> earnedPets = new();

    /// <summary>
    /// Announces the pets of the world that were earned just now (a count reached its goal or a building was finished):
    /// each appears beside the car. Returns the text for the celebration, or null when there is no new pet.
    /// </summary>
    private string? EarnPets()
    {
        string? text = null;
        foreach (var pet in world.WorldPets)
        {
            if (!pet.IsOwned || !earnedPets.Add(pet.Kind))
                continue;
            PlaceCollectorPet(pet);
            text = text == null ? Texts.NewPet(pet.DisplayName) : $"{text}  ·  {pet.DisplayName}";
        }
        return text;
    }

    /// <summary>Adds the text of a new pet under a celebration message, if the player earned one.</summary>
    private static string WithPets(string message, string? pets) => pets == null ? message : $"{message}\n{pets}";

    /// <summary>Takes the pets of the world from a restored game: who is owned, who was switched off, where each stands.</summary>
    private void RestoreWorldPets(SaveData data)
    {
        world.Tally.Restore(data.Hauls.Raw, data.Hauls.Works);
        earnedPets.Clear();
        foreach (var pet in world.WorldPets)
        {
            pet.Disabled = pet.IsOwned && data.Companions.DisabledPets.Contains(pet.Kind.ToString());
            PlaceCollectorPet(pet);
            if (pet.IsOwned)
                earnedPets.Add(pet.Kind);
        }
    }

    private void ToggleCollectorPet(CollectorPetKind kind)
    {
        if (State != GameState.Garage)
            return;
        var pet = world.AllPets.First(p => p.Kind == kind);
        if (!pet.IsOwned)
            return;
        pet.Disabled = !pet.Disabled;
        PlaceCollectorPet(pet);
        ShowGarage();
        SaveProgress();
    }

    private Vector3 CollectorPetTarget(CollectorPet pet) =>
        vehicle.Position + vehicle.Basis * (pet.TrailOffset + Vector3.Back * TrailerTrailShift);

    private void PlaceCollectorPet(CollectorPet pet)
    {
        pet.Visible = !pet.IsOwned || pet.IsActive;
        if (pet.IsActive)
        {
            var target = CollectorPetTarget(pet);
            target.Y = CompanionSurface(target);
            pet.Place(target, vehicle.Heading + Mathf.Pi);
            companionWanders[(int)pet.Kind] = default;
        }
        else
            pet.ReturnHome();
    }

    private void TickCollectorPets(float delta)
    {
        if (State is GameState.Title or GameState.Paused)
            return;
        foreach (var pet in world.AllPets)
        {
            if (!pet.IsOwned)
            {
                pet.Animate(delta, 0);
                continue;
            }
            if (!pet.IsActive || State != GameState.Exploring)
                continue;
            var target = CompanionTarget((int)pet.Kind, pet.Position, CollectorPetTarget(pet), delta);
            var offset = target - pet.Position;
            offset.Y = 0;
            float distance = offset.Length();
            if (distance > 35)
            {
                PlaceCollectorPet(pet);
                continue;
            }
            float speed = Mathf.Clamp((distance - .25f) * 3.4f, 0, companionStopTime >= .65f ? 4.8f : 24);
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

    private void TickCompanionStop(float delta)
    {
        if (State != GameState.Exploring)
            return;
        if (Mathf.Abs(vehicle.Speed) < .55f)
            companionStopTime = Mathf.Min(.65f, companionStopTime + delta);
        else if (companionStopTime > 0)
        {
            companionStopTime = 0;
            System.Array.Clear(companionWanders);
        }
    }

    private Vector3 CompanionTarget(int index, Vector3 current, Vector3 trail, float delta)
    {
        if (companionStopTime < .65f || current.DistanceTo(vehicle.Position) > 12)
            return trail;
        ref var wander = ref companionWanders[index];
        if (wander.Goal is Vector3 goal)
        {
            if (FlatDistanceSquared(current, goal) > .32f && FlatDistanceSquared(vehicle.Position, goal) < 7.5f * 7.5f)
                return goal;
            wander.Goal = null;
        }
        wander.Pause -= delta;
        if (wander.Pause > 0)
            return current;

        var radial = current - vehicle.Position;
        float heading = Mathf.Atan2(radial.X, radial.Z);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = heading + companionRandom.RandfRange(-1.45f, 1.45f);
            float radius = companionRandom.RandfRange(3.4f, 6);
            var target = vehicle.Position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
            if (River.OnBridge(vehicle.Position.X, vehicle.Position.Z))
                target.Z = Mathf.Clamp(target.Z, River.BridgeZ - 2.1f, River.BridgeZ + 2.1f);
            else if (world.OnCrossing(vehicle.Position.X, vehicle.Position.Z) && world.Ford is { } ford)
                target.Z = Mathf.Clamp(target.Z, ford.Site.Z - 2.1f, ford.Site.Z + 2.1f);
            if (FlatDistanceSquared(current, target) < 2 || !ClearCompanionPath(current, target))
                continue;
            wander.Goal = target;
            wander.Pause = companionRandom.RandfRange(.45f, 2.1f);
            return target;
        }
        wander.Pause = .4f;
        return current;
    }

    private bool ClearCompanionPath(Vector3 from, Vector3 to)
    {
        for (int step = 1; step <= 8; step++)
        {
            var p = from.Lerp(to, step / 8f);
            if (FlatDistanceSquared(p, vehicle.Position) < 5.3f || world.Plants.NearTrunk(p, .95f))
                return false;
            if (!world.OnCrossing(p.X, p.Z) && River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + .55f)
                return false;
        }
        return true;
    }

    private static float FlatDistanceSquared(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).LengthSquared();
}
