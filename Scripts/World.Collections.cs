using Godot;
using System;
using System.Collections.Generic;

namespace MathRacer;

public partial class World
{
    // Reuse the large clearings around roadside friends. The entire pickup and the
    // car's approach fit inside their vegetation exclusion, beyond the road edge.
    private Vector3[] CreateSpecialPickupSpots()
    {
        var spots = new List<Vector3>();
        for (int slot = 0; slot < MathRacer.Village.FarmKinds.Length + MathRacer.Village.FlowerNames.Length + MathRacer.Village.GemNames.Length + MathRacer.Village.ToyNames.Length; slot++)
        {
            bool found = false;
            for (int attempt = 0; attempt < Animals.Length && !found; attempt++)
            {
                var friend = Animals[(slot * 7 + 4 + attempt) % Animals.Length].Position;
                Vector3 road = Routes.Samples[0]; float best = float.PositiveInfinity;
                foreach (var sample in Routes.Samples)
                    if (sample.DistanceSquaredTo(friend) < best) { best = sample.DistanceSquaredTo(friend); road = sample; }
                var outward = new Vector3(friend.X - road.X, 0, friend.Z - road.Z).Normalized();
                var tangent = outward.Cross(Vector3.Up);
                foreach (int side in new[] { 1, -1 })
                {
                    var p = friend + outward * 1.8f + tangent * side * 2.3f;
                    p = Ground.At(p.X, p.Z);
                    if (Routes.Distance(p) < 7.5f || Village.IsReserved(p) ||
                        River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 7 || River.OnBridge(p.X, p.Z) ||
                        new Vector2(p.X, p.Z).Length() > Settings.PlayableRadius - 10 ||
                        spots.Exists(other => other.DistanceTo(p) < 5)) continue;
                    bool onRoad = Routes.Contains(p);
                    for (int i = 0; i < 12 && !onRoad; i++)
                    {
                        float angle = Mathf.Tau * i / 12;
                        onRoad = Routes.Contains(p + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 2.2f);
                    }
                    if (onRoad) continue;
                    spots.Add(p); found = true; break;
                }
            }
            if (!found) throw new InvalidOperationException("No clear off-road expedition pickup spot found.");
        }
        return spots.ToArray();
    }
}
