using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Vehicles;

namespace MathRacer.Valley;

// Ambient fauna is independent of the roadside friends and their rewards.
public partial class Wildlife : Node3D
{
    private readonly RandomNumberGenerator rng = new() { Seed = (ulong)Settings.Seed + 3127 };
    private readonly List<WildAnimal> animals = new();
    internal IReadOnlyList<WildAnimal> Animals => animals;
    private World world = null!;
    private Vehicle car = null!;
    public Vector3 PreviewSpot { get; private set; }

    public void Generate(World environment, Vehicle vehicle)
    {
        world = environment;
        car = vehicle;
        for (int i = 0; i < 240; i++)
        {
            var kind = (WildKind)(i % 5);
            for (int attempt = 0; attempt < 300; attempt++)
            {
                Vector3 p;
                if (i < 180)
                {
                    var sample = world.Routes.Samples[rng.RandiRange(0, world.Routes.Samples.Count - 1)];
                    float angle = rng.RandfRange(0, Mathf.Tau),
                        radius = rng.RandfRange(5, 22);
                    p = world.Ground.At(sample.X + Mathf.Cos(angle) * radius, sample.Z + Mathf.Sin(angle) * radius);
                }
                else
                    p = world.Ground.At(rng.RandfRange(-480, 480), rng.RandfRange(-480, 480));
                if (!SafeToPlace(p, kind) || world.Routes.Distance(p) < 4.5f)
                    continue;
                bool crowded = false;
                foreach (var other in animals)
                    if (other.Position.DistanceSquaredTo(p) < 9)
                    {
                        crowded = true;
                        break;
                    }
                if (crowded)
                    continue;
                Add(kind, p, rng.RandfRange(0, Mathf.Tau));
                if (kind == WildKind.Deer && PreviewSpot == Vector3.Zero)
                    PreviewSpot = p;
                break;
            }
        }
        GenerateOutland();
        GD.Print($"WILDLIFE_READY count={animals.Count} outland={OutlandCount} species=5");
    }

    private void Add(WildKind kind, Vector3 p, float phase)
    {
        var animal = new WildAnimal(kind, phase);
        AddChild(animal);
        animal.Position = p;
        animal.Home = p;
        animal.Direction = new Vector3(Mathf.Sin(animal.Phase), 0, Mathf.Cos(animal.Phase));
        animal.Animate(0, false);
        animals.Add(animal);
    }

    /// <summary>How many of the animals live beyond the old valley; they come after those in it.</summary>
    internal int OutlandCount { get; private set; }

    /// <summary>
    /// The animals of the land round the old valley, as many for its area as in the old valley, from a random generator
    /// of their own after the old valley's animals: most of them near its roads, the rest anywhere.
    /// </summary>
    private void GenerateOutland()
    {
        var random = new RandomNumberGenerator { Seed = (ulong)Settings.Seed + 3128 };
        var samples = world.OutlandRoutes.Samples;
        float half = ValleyBounds.HalfSize;
        int count = Mathf.RoundToInt(240 * (Mathf.Pow(half / 480, 2) - 1));
        // The animals already placed, by cells of 3 m, so that none is put next to another.
        var cells = new HashSet<Vector2I>();
        static Vector2I Cell(Vector3 p) => new(Mathf.FloorToInt(p.X / 3), Mathf.FloorToInt(p.Z / 3));
        for (int i = 0; i < count; i++)
        {
            var kind = (WildKind)(i % 5);
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Vector3 p;
                if (i % 4 != 3 && samples.Count > 0)
                {
                    var sample = samples[random.RandiRange(0, samples.Count - 1)];
                    float angle = random.RandfRange(0, Mathf.Tau),
                        radius = random.RandfRange(5, 22);
                    p = world.Ground.At(sample.X + Mathf.Cos(angle) * radius, sample.Z + Mathf.Sin(angle) * radius);
                }
                else
                    p = world.Ground.At(random.RandfRange(-half, half), random.RandfRange(-half, half));
                if (
                    new Vector2(p.X, p.Z).Length() <= WorldTuning.CorePlayableRadius
                    || !Safe(p, kind)
                    || world.OutlandRoutes.Distance(p) < 4.5f
                    || world.Routes.Distance(p) < 4.5f
                    || cells.Contains(Cell(p))
                )
                    continue;
                cells.Add(Cell(p));
                Add(kind, p, random.RandfRange(0, Mathf.Tau));
                OutlandCount++;
                break;
            }
        }
    }

    /// <summary>
    /// Where an animal may be put when the valley is made: only inside the old valley, so that the animals there keep
    /// their places however far the valley reaches (<see cref="WorldTuning.CorePlayableRadius"/>).
    /// </summary>
    private bool SafeToPlace(Vector3 p, WildKind kind) =>
        new Vector2(p.X, p.Z).LengthSquared() <= Mathf.Pow(WorldTuning.CorePlayableRadius - 12, 2) && Safe(p, kind);

    /// <summary>Where an animal may stand or run.</summary>
    internal bool Safe(Vector3 p, WildKind kind)
    {
        if (
            !ValleyBounds.Contains(p.X, p.Z, 12)
            || world.Village.IsReserved(p)
            || world.Plants.NearTrunk(p, kind == WildKind.Deer ? 1.5f : .9f)
            || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 3
            || OutOfTheValley(p)
        )
            return false;
        foreach (var friend in world.Animals)
            if (friend.Position.DistanceSquaredTo(p) < 16)
                return false;
        return true;
    }

    /// <summary>Whether a point is in the border river, the sea or on the beach, where no animal goes.</summary>
    private static bool OutOfTheValley(Vector3 p)
    {
        var borders = WorldLayout.Plan.Borders;
        return borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 3
            || borders.IsSand(p.X, p.Z)
            || p.Y < ValleyBorders.SeaLevel + 1;
    }

    /// <summary>Wild animals nearer the car than this have the characters' soft outline.</summary>
    private const float OutlineDistance = 45;

    private readonly HashSet<WildAnimal> outlined = new();
    private float outlineCheck;

    /// <summary>
    /// Outlines the animals near the car and takes the outline off those that are left behind: there are hundreds of
    /// them, and the outline is drawn as a second pass of every part.
    /// </summary>
    private void UpdateOutlines()
    {
        foreach (var animal in animals)
        {
            bool near = Flat(animal.Position - car.Position).Length() < OutlineDistance;
            if (near == outlined.Contains(animal))
                continue;
            if (near)
            {
                Art.Outline(animal, .02f);
                outlined.Add(animal);
            }
            else
            {
                Art.ClearOutline(animal);
                outlined.Remove(animal);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!car.Active)
            return;
        float dt = (float)delta;
        outlineCheck -= dt;
        if (outlineCheck <= 0)
        {
            outlineCheck = .5f;
            UpdateOutlines();
        }
        foreach (var animal in animals)
        {
            float distance = Flat(animal.Position - car.Position).Length();
            // Sleep distant fauna, and hide it: its parts are not drawn beyond 140 m anyway, and there are some twenty
            // thousand of them for the renderer to look through every frame.
            bool near = distance <= 145;
            if (animal.Visible != near)
                animal.Visible = near;
            if (!near)
                continue;
            Step(animal, dt, car.Position, car.Velocity);
        }
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    internal void Step(WildAnimal animal, float dt, Vector3 threat, Vector3 threatVelocity)
    {
        var away = Flat(animal.Position - threat);
        var motion = Flat(threatVelocity);
        // Look ahead along actual velocity, including reversing and sliding.
        var predicted = Flat(animal.Position - threat - motion * .65f);
        float alert = animal.Kind == WildKind.Butterfly ? 10 : 17;
        bool danger = away.Length() < alert || predicted.Length() < alert;
        animal.Fear = danger ? 2.5f : Mathf.Max(0, animal.Fear - dt);
        bool fleeing = animal.Fear > 0;
        animal.Phase += dt;
        Vector3 direction;
        if (fleeing)
        {
            direction = (away.Normalized() + predicted.Normalized() * .6f).Normalized();
            if (direction.IsZeroApprox())
                direction = animal.Direction;
        }
        else
        {
            var home = Flat(animal.Home - animal.Position);
            direction =
                home.Length() > 16
                    ? home.Normalized()
                    : new Vector3(Mathf.Sin(animal.Phase * .37f), 0, Mathf.Cos(animal.Phase * .29f));
        }
        float speed =
            fleeing ? animal.EscapeSpeed
            : animal.Flying ? .9f
            : .45f;
        if (!fleeing && !animal.Flying && Mathf.Sin(animal.Phase * .7f) < .25f)
            speed = 0;
        bool moved = false;
        // Probe a short corridor so runners steer around trunks, buildings and water.
        foreach (float angle in new[] { 0f, .55f, -.55f, 1.1f, -1.1f, 1.65f, -1.65f, Mathf.Pi })
        {
            var heading = direction.Rotated(Vector3.Up, angle);
            var next = animal.Position + heading * speed * dt;
            var probe = next + heading * (animal.Kind == WildKind.Deer ? 1.6f : .8f);
            if (!Safe(next, animal.Kind) || !Safe(probe, animal.Kind))
                continue;
            if (fleeing && Flat(next - threat).LengthSquared() < away.LengthSquared())
                continue;
            float height = world.Ground.Height(next.X, next.Z);
            if (!animal.Flying && Mathf.Abs(world.Ground.Height(probe.X, probe.Z) - height) > 1.3f)
                continue;
            animal.Position = new Vector3(next.X, height, next.Z);
            animal.Direction = heading;
            animal.Rotation = new Vector3(
                0,
                Mathf.LerpAngle(animal.Rotation.Y, Mathf.Atan2(heading.X, heading.Z), Mathf.Min(1, dt * 12)),
                0
            );
            moved = speed > 0;
            break;
        }
        animal.Animate(dt, moved);
    }
}
