using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

public partial class World
{
    internal readonly List<(Vector3 Road, Vector3 Pickup)> PickupApproaches = new();

    private Vector3[] CreateSpecialPickupSpots()
    {
        var random = new Random(Settings.Seed + 1753);
        var friends = Animals.Concat(Village.Residents).Append(Witch).Append(Sorcerer).ToArray();
        var spots = new List<Vector3>();
        int count =
            MathRacer.Settlement.Village.FarmKinds.Length
            + MathRacer.Settlement.Village.FlowerNames.Length
            + MathRacer.Settlement.Village.GemNames.Length
            + MathRacer.Settlement.Village.ToyNames.Length;
        for (int slot = 0; slot < count; slot++)
        {
            bool found = false;
            for (int attempt = 0; attempt < 4096 && !found; attempt++)
            {
                int index = random.Next(Routes.Samples.Count - 1);
                var road = Routes.Samples[index];
                var forward = Routes.Samples[index + 1] - road;
                forward.Y = 0;
                if (forward.LengthSquared() < .01f || forward.LengthSquared() > 9)
                    continue;
                var outward = forward.Normalized().Cross(Vector3.Up) * (random.Next(2) == 0 ? -1 : 1);
                var p = road + outward * (18 + (float)random.NextDouble() * 37);
                p = Ground.At(p.X, p.Z);
                if (
                    Routes.Distance(p) < 16
                    || friends.Any(a => FlatDistance(a.Position, p) < 28)
                    || spots.Exists(other => FlatDistance(other, p) < 22)
                )
                    continue;
                // Validate a car-width approach before reserving it from trees, bushes and rocks.
                bool clear = true;
                int steps = Mathf.CeilToInt(FlatDistance(road, p) / 2);
                for (int step = 0; step <= steps && clear; step++)
                {
                    var center = road.Lerp(p, step / (float)steps);
                    foreach (float lateral in new[] { -3.5f, 0, 3.5f })
                    {
                        var q = center + forward.Normalized() * lateral;
                        if (
                            new Vector2(q.X, q.Z).Length() > Settings.PlayableRadius - 10
                            || Village.IsReserved(q)
                            || River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 8
                            || River.OnBridge(q.X, q.Z)
                            || Mathf.Abs(Ground.Height(q.X + 1, q.Z) - Ground.Height(q.X - 1, q.Z)) > 1
                            || Mathf.Abs(Ground.Height(q.X, q.Z + 1) - Ground.Height(q.X, q.Z - 1)) > 1
                        )
                        {
                            clear = false;
                            break;
                        }
                    }
                }
                if (!clear)
                    continue;
                spots.Add(p);
                PickupApproaches.Add((road, p));
                found = true;
            }
            if (!found)
                throw new InvalidOperationException("No clear off-road expedition pickup spot found.");
        }
        return spots.ToArray();
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private ApproachIndex? pickupIndex;
    private int pickupIndexCount = -1;

    private bool IsPickupApproachReserved(Vector3 point)
    {
        // The pickup approaches are only ever appended to, so their count tells whether the index is current.
        if (pickupIndex == null || pickupIndexCount != PickupApproaches.Count)
        {
            pickupIndex = new ApproachIndex(PickupApproaches.Select(pair => (pair.Road, pair.Pickup)).ToArray(), 4.5f);
            pickupIndexCount = PickupApproaches.Count;
        }
        return pickupIndex.NearClamped(point) || IsHaulApproachReserved(point);
    }

    /// <summary>
    /// Straight approaches with their edges and bounds worked out once. A point farther than the radius from a
    /// segment's bounds cannot be within the radius of the segment, so most tests end at the bounds.
    /// </summary>
    private sealed class ApproachIndex
    {
        private readonly Vector2[] from;
        private readonly Vector2[] edge;
        private readonly float[] lengthSquared;
        private readonly float[] minX,
            maxX,
            minZ,
            maxZ;
        private readonly float radius;

        public ApproachIndex(IEnumerable<(Vector3 Road, Vector3 Spot)> segments, float radius)
        {
            var list = segments.ToArray();
            this.radius = radius;
            from = new Vector2[list.Length];
            edge = new Vector2[list.Length];
            lengthSquared = new float[list.Length];
            minX = new float[list.Length];
            maxX = new float[list.Length];
            minZ = new float[list.Length];
            maxZ = new float[list.Length];
            // The bounds grow by a little more than the radius so rounding can never cut a segment that is in reach.
            float margin = radius + .1f;
            for (int i = 0; i < list.Length; i++)
            {
                var a = new Vector2(list[i].Road.X, list[i].Road.Z);
                var b = new Vector2(list[i].Spot.X, list[i].Spot.Z);
                from[i] = a;
                edge[i] = b - a;
                lengthSquared[i] = edge[i].LengthSquared();
                minX[i] = Mathf.Min(a.X, b.X) - margin;
                maxX[i] = Mathf.Max(a.X, b.X) + margin;
                minZ[i] = Mathf.Min(a.Y, b.Y) - margin;
                maxZ[i] = Mathf.Max(a.Y, b.Y) + margin;
            }
        }

        /// <summary>The pickup approaches' test: the point is nearer than the radius to the clamped projection.</summary>
        public bool NearClamped(Vector3 point)
        {
            var p = new Vector2(point.X, point.Z);
            for (int i = 0; i < from.Length; i++)
            {
                if (p.X < minX[i] || p.X > maxX[i] || p.Y < minZ[i] || p.Y > maxZ[i])
                    continue;
                float t = Mathf.Clamp((p - from[i]).Dot(edge[i]) / lengthSquared[i], 0, 1);
                if (p.DistanceSquaredTo(from[i] + edge[i] * t) < radius * radius)
                    return true;
            }
            return false;
        }

        /// <summary>The haul approaches' test, which treats a (nearly) zero-length segment as a point.</summary>
        public bool NearSegmentDistance(Vector3 point)
        {
            var p = new Vector2(point.X, point.Z);
            for (int i = 0; i < from.Length; i++)
            {
                if (p.X < minX[i] || p.X > maxX[i] || p.Y < minZ[i] || p.Y > maxZ[i])
                    continue;
                float t =
                    lengthSquared[i] < .0001f ? 0 : Mathf.Clamp((p - from[i]).Dot(edge[i]) / lengthSquared[i], 0, 1);
                if (p.DistanceTo(from[i] + edge[i] * t) < radius)
                    return true;
            }
            return false;
        }
    }
}
