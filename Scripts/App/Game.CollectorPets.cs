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

    /// <summary>How long the car stands before the companions take it as parked and start to stroll around it.</summary>
    private const float ParkDelay = .65f;

    /// <summary>How close to the middle of the car a companion may come.</summary>
    private const float CarKeepOut = 2.4f;

    private struct CompanionWander
    {
        internal Vector3? Goal;
        internal float Pause;

        /// <summary>Whether this companion has noticed that the car is parked (each notices at a moment of its own).</summary>
        internal bool Started;

        /// <summary>Where a resting companion looks; null is the car.</summary>
        internal Vector3? Look;

        /// <summary>How the companion moves now; it speeds up, slows down and turns gradually, never at once.</summary>
        internal Vector3 Velocity;
    }

    private readonly CompanionWander[] companionWanders = new CompanionWander[GolemWanderIndex + 1];
    private readonly RandomNumberGenerator companionRandom = new() { Seed = (ulong)Settings.Seed + 710 };
    private readonly CompanionPath companionPath = new();
    private float companionStopTime;
    private float companionClock;

    private bool CompanionsParked => companionStopTime >= ParkDelay;

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
        world.Tally.Restore(data.Hauls.Raw, data.Hauls.Works, data.Hauls.Wheat, data.Hauls.Baked);
        world.Hidden.Restore(data.Hidden.Found);
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

    /// <summary>How far back along the car's way a pet walks: its place in the trail, behind the trailer if there is one.</summary>
    private float CollectorPetColumn(CollectorPet pet) => pet.TrailOffset.Z + TrailerTrailShift;

    private Vector3 CollectorPetTarget(CollectorPet pet, out Vector3 forward) =>
        CompanionTrailSpot((int)pet.Kind, pet.TrailOffset.X, CollectorPetColumn(pet), out forward);

    /// <summary>
    /// Where a companion walks while the car drives: <paramref name="column"/> metres back along the way the car drove
    /// and <paramref name="side"/> metres to the right of it, swaying a little in a rhythm of its own. On a bridge or a
    /// ford the companions close up into a narrow file.
    /// </summary>
    private Vector3 CompanionTrailSpot(int index, float side, float column, out Vector3 forward)
    {
        float behind =
            column
            + .3f * Mathf.Sin(companionClock * (.45f + .4f * CompanionTrait(index, 1)) + 6 * CompanionTrait(index, 2));
        var spot = companionPath.Sample(vehicle.Position, vehicle.Basis.Z, Mathf.Max(behind, 2.5f), out forward);
        side +=
            .22f * Mathf.Sin(companionClock * (.6f + .5f * CompanionTrait(index, 3)) + 6 * CompanionTrait(index, 4));
        if (River.OnBridge(spot.X, spot.Z) || world.OnCrossing(spot.X, spot.Z))
            side *= .3f;
        return spot + forward.Cross(Vector3.Up) * side;
    }

    /// <summary>
    /// A number from 0 to 1 that is always the same for a companion and a <paramref name="salt"/>, so that each has a
    /// pace, a sway and habits of its own.
    /// </summary>
    private static float CompanionTrait(int index, int salt) =>
        Mathf.PosMod(Mathf.Sin(index * 12.9898f + salt * 78.233f) * 43758.547f, 1);

    private void PlaceCollectorPet(CollectorPet pet)
    {
        pet.Visible = !pet.IsOwned || pet.IsActive;
        if (pet.IsActive)
        {
            var target = CollectorPetTarget(pet, out var forward);
            target.Y = CompanionSurface(target);
            pet.Place(target, Mathf.Atan2(forward.X, forward.Z));
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
            int index = (int)pet.Kind;
            var target = CompanionTarget(
                index,
                pet.Position,
                CollectorPetTarget(pet, out _),
                CollectorPetColumn(pet),
                delta
            );
            if (FlatDistanceSquared(target, pet.Position) > 35 * 35)
            {
                PlaceCollectorPet(pet);
                continue;
            }
            var position = StepCompanion(index, pet.Position, target, delta, 4.8f, 24, 3.4f);
            position.Y = CompanionSurface(position);
            pet.Position = position;
            pet.Rotation = new(0, CompanionYaw(index, pet.Position, pet.Rotation.Y, 0, delta), 0);
            pet.Animate(delta, companionWanders[index].Velocity.Length());
        }
    }

    /// <summary>
    /// Moves a companion one frame towards <paramref name="target"/> and returns where it gets to. It walks at up to
    /// <paramref name="walk"/> around a parked car (running when it is far behind) and runs at up to
    /// <paramref name="run"/> after a driving car, keeping pace with it; it speeds up, brakes and turns gradually, and
    /// it never walks into the car.
    /// </summary>
    private Vector3 StepCompanion(
        int index,
        Vector3 position,
        Vector3 target,
        float delta,
        float walk,
        float run,
        float gain
    )
    {
        ref var state = ref companionWanders[index];
        var offset = target - position;
        offset.Y = 0;
        float distance = offset.Length();
        float pace = .88f + .24f * CompanionTrait(index, 5);
        bool parked = CompanionsParked;
        float cap = parked ? Mathf.Lerp(walk, run, Mathf.Clamp((distance - 8) / 14, 0, 1)) : run;
        float follow = parked ? 0 : Mathf.Abs(vehicle.Speed) * .85f;
        var desired =
            distance < .3f ? Vector3.Zero : offset / distance * Mathf.Min(follow + (distance - .3f) * gain, cap) * pace;
        bool braking = desired.LengthSquared() < state.Velocity.LengthSquared();
        float rate =
            (
                braking ? 40
                : parked ? 10
                : 26
            ) * pace;
        state.Velocity = state.Velocity.MoveToward(desired, rate * delta);
        var next = position + state.Velocity * delta;
        var fromCar = next - vehicle.Position;
        fromCar.Y = 0;
        float near = fromCar.Length();
        if (near < CarKeepOut && near > .001f)
        {
            var outward = fromCar / near;
            next += outward * (CarKeepOut - near);
            float inward = state.Velocity.Dot(outward);
            if (inward < 0)
                state.Velocity -= outward * inward;
        }
        return next;
    }

    /// <summary>
    /// Turns a companion gradually: to where it goes while it moves and, while it rests by the parked car, to the car
    /// or to something it watches. <paramref name="facing"/> is the turn of a model that does not face +Z.
    /// </summary>
    private float CompanionYaw(int index, Vector3 position, float yaw, float facing, float delta)
    {
        ref var state = ref companionWanders[index];
        var velocity = state.Velocity;
        Vector3 toward;
        float rate;
        if (velocity.LengthSquared() > .04f)
        {
            toward = velocity;
            rate = 10;
        }
        else if (CompanionsParked)
        {
            toward = (state.Look ?? vehicle.Position) - position;
            rate = 2.2f;
        }
        else
            return yaw;
        if (toward.X * toward.X + toward.Z * toward.Z < .01f)
            return yaw;
        return Mathf.LerpAngle(yaw, Mathf.Atan2(toward.X, toward.Z) + facing, 1 - Mathf.Exp(-rate * delta));
    }

    private void TickCompanionStop(float delta)
    {
        companionPath.Record(vehicle.Position, vehicle.Basis.Z);
        if (State != GameState.Exploring)
            return;
        companionClock += delta;
        if (Mathf.Abs(vehicle.Speed) < .55f)
            companionStopTime = Mathf.Min(ParkDelay, companionStopTime + delta);
        else if (companionStopTime > 0)
        {
            companionStopTime = 0;
            // The strolls end, but each companion keeps its momentum as it turns to follow the car again.
            for (int i = 0; i < companionWanders.Length; i++)
                companionWanders[i] = new() { Velocity = companionWanders[i].Velocity };
        }
    }

    /// <summary>
    /// Where a companion heads: its <paramref name="trail"/> spot while the car drives and, once the car is parked, a
    /// stroll around it. Each strolls in a ring of its own whose size follows its place in the trail
    /// (<paramref name="column"/>), so the pets at the back of a long trail stroll too, a little farther out.
    /// </summary>
    private Vector3 CompanionTarget(int index, Vector3 current, Vector3 trail, float column, float delta)
    {
        if (!CompanionsParked)
            return trail;
        ref var wander = ref companionWanders[index];
        float inner = Mathf.Max(3.4f, column * .5f - 1);
        float outer = inner + 2.6f;
        var radial = current - vehicle.Position;
        radial.Y = 0;
        float distance = radial.Length();
        if (!wander.Started)
        {
            // Each notices at a moment of its own that the car has stopped, looks at it, and only then sets off.
            wander.Started = true;
            wander.Pause = .1f + 1.5f * CompanionTrait(index, 7);
        }
        if (distance > outer + 1.5f)
        {
            wander.Goal = null;
            return vehicle.Position + radial / distance * (inner + outer) / 2;
        }
        if (wander.Goal is Vector3 goal)
        {
            if (
                FlatDistanceSquared(current, goal) > .32f
                && FlatDistanceSquared(vehicle.Position, goal) < (outer + 1.5f) * (outer + 1.5f)
            )
                return goal;
            wander.Goal = null;
            float glance = companionRandom.RandfRange(-Mathf.Pi, Mathf.Pi);
            wander.Look =
                companionRandom.Randf() < .55f ? null : current + new Vector3(Mathf.Sin(glance), 0, Mathf.Cos(glance));
        }
        wander.Pause -= delta;
        if (wander.Pause > 0)
            return current;

        float heading = Mathf.Atan2(radial.X, radial.Z);
        float lazy = .7f + .8f * CompanionTrait(index, 8);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float radius = companionRandom.RandfRange(inner, outer);
            float spread = Mathf.Min(1.45f, 4.5f / radius);
            float angle = heading + companionRandom.RandfRange(-spread, spread);
            var target = vehicle.Position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
            if (River.OnBridge(vehicle.Position.X, vehicle.Position.Z))
                target.Z = Mathf.Clamp(target.Z, River.BridgeZ - 2.1f, River.BridgeZ + 2.1f);
            else if (world.OnCrossing(vehicle.Position.X, vehicle.Position.Z) && world.Ford is { } ford)
                target.Z = Mathf.Clamp(target.Z, ford.Site.Z - 2.1f, ford.Site.Z + 2.1f);
            if (FlatDistanceSquared(current, target) < 2 || !ClearCompanionPath(current, target))
                continue;
            wander.Goal = target;
            wander.Pause = companionRandom.RandfRange(.45f, 2.1f) * lazy;
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
