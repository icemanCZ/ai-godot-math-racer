using Godot;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Continuous roadbed shared by intersecting routes, before visible terrain and collision are built.</summary>
public sealed class RoadGrading
{
    private const float CellSize = 24;
    private const float Shoulder = 14;
    public const float MaxGrade = .65f;
    private readonly Dictionary<Vector2I, List<(Vector2 A, Vector2 B)>> cells = new();
    private readonly WorldPlan plan;

    public RoadGrading(WorldPlan plan)
    {
        this.plan = plan;
        foreach (var points in plan.Routes)
        {
            using var curve = WorldPlan.CreateCurve(points);
            float length = curve.GetBakedLength();
            for (float distance = 0; distance < length; distance += 2)
            {
                var p = curve.SampleBaked(distance); var q = curve.SampleBaked(Mathf.Min(distance + 2, length));
                var a = new Vector2(p.X, p.Z); var b = new Vector2(q.X, q.Z);
                var min = (a.Min(b) - Vector2.One * Shoulder) / CellSize;
                var max = (a.Max(b) + Vector2.One * Shoulder) / CellSize;
                for (int z = Mathf.FloorToInt(min.Y); z <= Mathf.FloorToInt(max.Y); z++)
                    for (int x = Mathf.FloorToInt(min.X); x <= Mathf.FloorToInt(max.X); x++)
                    {
                        var key = new Vector2I(x, z);
                        if (!cells.TryGetValue(key, out var segments)) cells[key] = segments = new();
                        segments.Add((a, b));
                    }
            }
        }
    }

    public float Apply(float x, float z, float natural)
    {
        var key = new Vector2I(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize));
        if (!cells.TryGetValue(key, out var segments)) return natural;
        var point = new Vector2(x, z);
        float nearestSquared = Shoulder * Shoulder;
        foreach (var (a, b) in segments)
        {
            var delta = b - a;
            float t = delta.LengthSquared() < .000001f ? 0 : Mathf.Clamp((point - a).Dot(delta) / delta.LengthSquared(), 0, 1);
            nearestSquared = Mathf.Min(nearestSquared, point.DistanceSquaredTo(a + delta * t));
        }
        float blend = 1 - Mathf.SmoothStep(7.5f, Shoulder, Mathf.Sqrt(nearestSquared));
        // Follow most of the original hills; soften only a quarter of their height variation.
        float height = 12 * (1 - Mathf.Exp(-(x * x + z * z) / 100000f)) +
            .6f * Mathf.Sin(x * .009f + plan.HillPhase) * Mathf.Cos(z * .011f - plan.HillPhase);
        height = Mathf.Lerp(height, natural, .75f);
        float approach = (1 - Mathf.SmoothStep(23, 110, Mathf.Abs(x - plan.CenterX(plan.BridgeZ)))) *
            (1 - Mathf.SmoothStep(7, 75, Mathf.Abs(z - plan.BridgeZ)));
        float bankHeight = -.8f - plan.BridgeZ * .008f + 1 - .32f;
        height = Mathf.Lerp(height, bankHeight, approach);
        return Mathf.Lerp(natural, height, blend);
    }
}
