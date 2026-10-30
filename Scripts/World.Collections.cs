using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public partial class World
{
    internal readonly List<(Vector3 Road, Vector3 Pickup)> PickupApproaches = new();

    private Vector3[] CreateSpecialPickupSpots()
    {
        var random = new Random(Settings.Seed + 1753);
        var friends = Animals.Concat(Village.Residents).ToArray();
        var spots = new List<Vector3>();
        int count = MathRacer.Village.FarmKinds.Length + MathRacer.Village.FlowerNames.Length +
            MathRacer.Village.GemNames.Length + MathRacer.Village.ToyNames.Length;
        for (int slot = 0; slot < count; slot++)
        {
            bool found = false;
            for (int attempt = 0; attempt < 4096 && !found; attempt++)
            {
                int index = random.Next(Routes.Samples.Count - 1);
                var road = Routes.Samples[index];
                var forward = Routes.Samples[index + 1] - road; forward.Y = 0;
                if (forward.LengthSquared() < .01f || forward.LengthSquared() > 9) continue;
                var outward = forward.Normalized().Cross(Vector3.Up) * (random.Next(2) == 0 ? -1 : 1);
                var p = road + outward * (18 + (float)random.NextDouble() * 27);
                p = Ground.At(p.X, p.Z);
                if (Routes.Distance(p) < 16 || friends.Any(a => FlatDistance(a.Position, p) < 28) ||
                    spots.Exists(other => FlatDistance(other, p) < 22)) continue;
                // Validate a car-width approach before reserving it from trees, bushes and rocks.
                bool clear = true;
                int steps = Mathf.CeilToInt(FlatDistance(road, p) / 2);
                for (int step = 0; step <= steps && clear; step++)
                {
                    var center = road.Lerp(p, step / (float)steps);
                    foreach (float lateral in new[] { -3.5f, 0, 3.5f })
                    {
                        var q = center + forward.Normalized() * lateral;
                        if (new Vector2(q.X, q.Z).Length() > Settings.PlayableRadius - 10 ||
                            Village.IsReserved(q) ||
                            River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 8 || River.OnBridge(q.X, q.Z) ||
                            Mathf.Abs(Ground.Height(q.X + 1, q.Z) - Ground.Height(q.X - 1, q.Z)) > 1 ||
                            Mathf.Abs(Ground.Height(q.X, q.Z + 1) - Ground.Height(q.X, q.Z - 1)) > 1)
                        { clear = false; break; }
                    }
                }
                if (!clear) continue;
                spots.Add(p); PickupApproaches.Add((road, p)); found = true;
            }
            if (!found) throw new InvalidOperationException("No clear off-road expedition pickup spot found.");
        }
        return spots.ToArray();
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private bool IsPickupApproachReserved(Vector3 point)
    {
        var p = new Vector2(point.X, point.Z);
        foreach (var (road, pickup) in PickupApproaches)
        {
            var a = new Vector2(road.X, road.Z); var b = new Vector2(pickup.X, pickup.Z);
            var edge = b - a;
            float t = Mathf.Clamp((p - a).Dot(edge) / edge.LengthSquared(), 0, 1);
            if (p.DistanceSquaredTo(a + edge * t) < 4.5f * 4.5f) return true;
        }
        return false;
    }
}
