using Godot;
using System;
using System.Linq;

namespace MathRacer;

internal static class WildlifeChecks
{
    internal static void Run(World world, Vehicle car, Action<bool, string> check)
    {
        var wildlife = world.Wildlife;
        check(wildlife.Animals.Count == 240 && wildlife.Animals.Select(a => a.Kind).Distinct().Count() == 5, "240 wild animals across five species");
        check(wildlife.Animals.All(a => wildlife.Safe(a.Position, a.Kind)), "wildlife stays out of water, buildings, trunks and friend clearings");
        foreach (WildKind kind in Enum.GetValues<WildKind>())
        {
            var animal = wildlife.Animals.First(a => a.Kind == kind);
            var original = animal.Position;
            var rotation = animal.Rotation;
            float phase = animal.Phase;
            // Find an unobstructed escape corridor in the actual generated terrain.
            Vector3 heading = Vector3.Zero;
            for (int turn = 0; turn < 24; turn++)
            {
                var candidate = Vector3.Back.Rotated(Vector3.Up, turn * Mathf.Tau / 24);
                if (Enumerable.Range(1, 20).All(i => wildlife.Safe(original + candidate * i * .5f, kind))) { heading = candidate; break; }
            }
            check(heading != Vector3.Zero, $"{kind} has an escape corridor");
            var threat = original - heading * 5;
            for (int i = 0; i < 60; i++) wildlife.Step(animal, 1f / 60, threat, Vector3.Zero);
            check(animal.Fear > 0 && animal.Position.DistanceTo(threat) > original.DistanceTo(threat) + 3, $"{kind} flees from nearby car");
            check(wildlife.Safe(animal.Position, kind) && Mathf.Abs(animal.Position.Y - world.Ground.Height(animal.Position.X, animal.Position.Z)) < .01f, $"{kind} follows safe terrain");
            foreach (float direction in new[] { -1f, 1f })
            {
                animal.Position = original; animal.Fear = 0;
                wildlife.Step(animal, 1f / 60, original - heading * 23 * direction, heading * 22 * direction);
                check(animal.Fear > 0, $"{kind} anticipates approaching car in either driving direction");
            }
            for (int i = 0; i < 180; i++) wildlife.Step(animal, 1f / 60, original + Vector3.Right * 1000, Vector3.Zero);
            check(animal.Fear == 0, $"{kind} calms down after car leaves");
            animal.Position = original; animal.Rotation = rotation; animal.Phase = phase; animal.Fear = 0; animal.Animate(0, false);
        }
        bool active = car.Active;
        car.Active = false;
        var positions = wildlife.Animals.Select(a => a.Position).ToArray();
        wildlife._PhysicsProcess(1);
        check(wildlife.Animals.Select(a => a.Position).SequenceEqual(positions), "pause and question dialogs freeze wildlife");
        car.Active = active;
        GD.Print("WILDLIFE_CHECKS_PASSED");
    }
}
