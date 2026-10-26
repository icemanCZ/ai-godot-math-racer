using Godot;
using System.Collections.Generic;

namespace MathRacer;

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
        world = environment; car = vehicle;
        for (int i = 0; i < 240; i++)
        {
            var kind = (WildKind)(i % 5);
            for (int attempt = 0; attempt < 300; attempt++)
            {
                Vector3 p;
                if (i < 180)
                {
                    var sample = world.Routes.Samples[rng.RandiRange(0, world.Routes.Samples.Count - 1)];
                    float angle = rng.RandfRange(0, Mathf.Tau), radius = rng.RandfRange(5, 22);
                    p = world.Ground.At(sample.X + Mathf.Cos(angle) * radius, sample.Z + Mathf.Sin(angle) * radius);
                }
                else p = world.Ground.At(rng.RandfRange(-480, 480), rng.RandfRange(-480, 480));
                if (!Safe(p, kind) || world.Routes.Distance(p) < 4.5f) continue;
                bool crowded = false;
                foreach (var other in animals)
                    if (other.Position.DistanceSquaredTo(p) < 9) { crowded = true; break; }
                if (crowded) continue;
                var animal = new WildAnimal(kind, rng.RandfRange(0, Mathf.Tau));
                AddChild(animal); animal.Position = p;
                animal.Home = p; animal.Direction = new Vector3(Mathf.Sin(animal.Phase), 0, Mathf.Cos(animal.Phase));
                animal.Animate(0, false);
                animals.Add(animal);
                if (kind == WildKind.Deer && PreviewSpot == Vector3.Zero) PreviewSpot = p;
                break;
            }
        }
        GD.Print($"WILDLIFE_READY count={animals.Count} species=5");
    }

    internal bool Safe(Vector3 p, WildKind kind)
    {
        if (new Vector2(p.X, p.Z).LengthSquared() > Mathf.Pow(Settings.PlayableRadius - 12, 2) ||
            world.Village.IsReserved(p) || world.Plants.NearTrunk(p, kind == WildKind.Deer ? 1.5f : .9f) ||
            River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 3) return false;
        foreach (var friend in world.Animals)
            if (friend.Position.DistanceSquaredTo(p) < 16) return false;
        return true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!car.Active) return;
        float dt = (float)delta;
        foreach (var animal in animals)
        {
            float distance = Flat(animal.Position - car.Position).Length();
            // Sleep distant fauna; rendering also culls its small shared meshes.
            if (distance > 145) continue;
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
            if (direction.IsZeroApprox()) direction = animal.Direction;
        }
        else
        {
            var home = Flat(animal.Home - animal.Position);
            direction = home.Length() > 16 ? home.Normalized() :
                new Vector3(Mathf.Sin(animal.Phase * .37f), 0, Mathf.Cos(animal.Phase * .29f));
        }
        float speed = fleeing ? animal.EscapeSpeed : animal.Flying ? .9f : .45f;
        if (!fleeing && !animal.Flying && Mathf.Sin(animal.Phase * .7f) < .25f) speed = 0;
        bool moved = false;
        // Probe a short corridor so runners steer around trunks, buildings and water.
        foreach (float angle in new[] { 0f, .55f, -.55f, 1.1f, -1.1f, 1.65f, -1.65f, Mathf.Pi })
        {
            var heading = direction.Rotated(Vector3.Up, angle);
            var next = animal.Position + heading * speed * dt;
            var probe = next + heading * (animal.Kind == WildKind.Deer ? 1.6f : .8f);
            if (!Safe(next, animal.Kind) || !Safe(probe, animal.Kind)) continue;
            if (fleeing && Flat(next - threat).LengthSquared() < away.LengthSquared()) continue;
            float height = world.Ground.Height(next.X, next.Z);
            if (!animal.Flying && Mathf.Abs(world.Ground.Height(probe.X, probe.Z) - height) > 1.3f) continue;
            animal.Position = new Vector3(next.X, height, next.Z);
            animal.Direction = heading;
            animal.Rotation = new Vector3(0, Mathf.LerpAngle(animal.Rotation.Y, Mathf.Atan2(heading.X, heading.Z), Mathf.Min(1, dt * 12)), 0);
            moved = speed > 0;
            break;
        }
        animal.Animate(dt, moved);
    }
}
