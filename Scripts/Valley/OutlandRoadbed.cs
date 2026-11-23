using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The ground under the roads beyond the old valley (<see cref="Roads.GenerateOutland"/>): each road gets a smooth
/// height along its length, the ground of its landscape evened out and kept to a gentle grade, and the ground on both
/// sides eases back to the landscape. By a bridge the road comes down (or up) to the bank. Nothing changes in the old
/// valley: the grading fades out before its edge, and the ground is only asked once the old valley stands.
/// </summary>
internal sealed class OutlandRoadbed
{
    private const float CellSize = 24;

    /// <summary>The ground follows the road fully this far from its middle and the landscape again at the shoulder.</summary>
    private const float Flat = 5.5f;

    private const float Shoulder = 17;

    /// <summary>The road climbs or falls at most this much per metre.</summary>
    private const float MaxGrade = .11f;

    /// <summary>Each height along a road is the mean of the landscape this far before and after it.</summary>
    private const float Smoothing = 36;

    /// <summary>Spacing of the heights along a road.</summary>
    private const float Step = 2;

    private readonly record struct Segment(Vector2 A, Vector2 B, float HeightA, float HeightB);

    private readonly Dictionary<Vector2I, List<Segment>> cells = new();

    internal OutlandRoadbed(
        IEnumerable<Vector3[]> routes,
        Func<float, float, float> landscape,
        IReadOnlyList<OutlandLayout.Bridge> bridges
    )
    {
        var laid = new List<Segment>();
        foreach (var points in routes)
        {
            using var curve = WorldPlan.CreateCurve(points);
            float length = curve.GetBakedLength();
            var flat = new List<Vector2>();
            var raw = new List<float>();
            for (float distance = 0; ; distance = Mathf.Min(distance + Step, length))
            {
                var p = curve.SampleBaked(distance);
                flat.Add(new Vector2(p.X, p.Z));
                raw.Add(landscape(p.X, p.Z));
                if (distance >= length)
                    break;
            }
            bool closed = points[0] == points[^1];
            // Where a road meets one laid before (the ring), it takes that road's height, so that they join level.
            var heights = Profile(raw, flat, bridges, closed, Meeting(flat[0], laid), Meeting(flat[^1], laid));
            for (int i = 0; i + 1 < flat.Count; i++)
            {
                var segment = new Segment(flat[i], flat[i + 1], heights[i], heights[i + 1]);
                laid.Add(segment);
                Add(segment);
            }
        }
    }

    private static float[] Profile(
        List<float> raw,
        List<Vector2> flat,
        IReadOnlyList<OutlandLayout.Bridge> bridges,
        bool closed,
        float? startHeight,
        float? endHeight
    )
    {
        int count = raw.Count,
            reach = Mathf.RoundToInt(Smoothing / Step);
        // A closed road (the ring) goes round: its last point is its first, and its heights run on across it.
        int period = closed ? count - 1 : count;
        var heights = new float[count];
        for (int i = 0; i < period; i++)
        {
            float sum = 0;
            int used = 0;
            for (int j = i - reach; j <= i + reach; j++)
            {
                int k = closed ? ((j % period) + period) % period : j;
                if (k < 0 || k >= period)
                    continue;
                sum += raw[k];
                used++;
            }
            heights[i] = sum / used;
        }
        // By a bridge the road meets the bank, over a long enough way to keep the grade.
        for (int i = 0; i < period; i++)
            foreach (var bridge in bridges)
            {
                if (Mathf.Abs(flat[i].Y - bridge.Z) > 30)
                    continue;
                float along = Mathf.Abs(flat[i].X - bridge.CenterX);
                float weight = 1 - Mathf.SmoothStep(bridge.HalfLength + 10, bridge.HalfLength + 80, along);
                heights[i] = Mathf.Lerp(heights[i], River.Level(bridge.Z) + River.OutlandBankRise, weight);
            }
        // An end that meets another road comes to its height over the last stretch, and keeps it exactly.
        bool Pinned(int i) => (i == 0 && startHeight != null) || (i == count - 1 && endHeight != null);
        if (startHeight is float start)
            EaseTo(heights, flat, start, 0, 1);
        if (endHeight is float end)
            EaseTo(heights, flat, end, count - 1, -1);
        // The river's floodplain cuts the ground down near the water: the road keeps below it from the start.
        void UnderFloodplain()
        {
            for (int i = 0; i < period; i++)
            {
                float x = flat[i].X,
                    z = flat[i].Y;
                float floor = Mathf.Max(River.ValleyCeiling(x, z) - 2.2f, River.Level(z) + River.OutlandBankRise);
                if (!Pinned(i))
                    heights[i] = Mathf.Min(heights[i], floor);
            }
        }
        UnderFloodplain();
        // Neither way along the road may it climb too steeply; round a closed road twice, so that it holds across its start,
        // and one with a fixed end three times, so that the grade holds up to that end.
        int rounds =
            closed ? 2
            : startHeight != null || endHeight != null ? 3
            : 1;
        for (int round = 0; round < rounds; round++)
        {
            for (int n = 1; n < period + (closed ? 1 : 0); n++)
            {
                int i = n % period,
                    before = n - 1;
                float rise = MaxGrade * flat[i].DistanceTo(flat[before]);
                if (!Pinned(i))
                    heights[i] = Mathf.Clamp(heights[i], heights[before] - rise, heights[before] + rise);
            }
            for (int n = period - 2 + (closed ? 1 : 0); n >= 0; n--)
            {
                int i = n,
                    after = (n + 1) % period;
                float rise = MaxGrade * flat[i].DistanceTo(flat[after]);
                if (!Pinned(i))
                    heights[i] = Mathf.Clamp(heights[i], heights[after] - rise, heights[after] + rise);
            }
        }
        // Under the floodplain again, then only lowered where that left a step: the grade holds everywhere.
        UnderFloodplain();
        for (int round = 0; round < rounds; round++)
        {
            for (int n = 1; n < period + (closed ? 1 : 0); n++)
            {
                int i = n % period;
                if (!Pinned(i))
                    heights[i] = Mathf.Min(heights[i], heights[n - 1] + MaxGrade * flat[i].DistanceTo(flat[n - 1]));
            }
            for (int n = period - 2 + (closed ? 1 : 0); n >= 0; n--)
            {
                int after = (n + 1) % period;
                if (!Pinned(n))
                    heights[n] = Mathf.Min(heights[n], heights[after] + MaxGrade * flat[n].DistanceTo(flat[after]));
            }
        }
        if (closed)
            heights[count - 1] = heights[0];
        return heights;
    }

    /// <summary>The height of the road laid before that passes within 2 m of <paramref name="point"/>, if one does.</summary>
    private static float? Meeting(Vector2 point, List<Segment> laid)
    {
        float nearest = 2;
        float? meeting = null;
        foreach (var segment in laid)
        {
            var delta = segment.B - segment.A;
            float lengthSquared = delta.LengthSquared();
            float t = lengthSquared < .000001f ? 0 : Mathf.Clamp((point - segment.A).Dot(delta) / lengthSquared, 0, 1);
            float distance = point.DistanceTo(segment.A + delta * t);
            if (distance >= nearest)
                continue;
            nearest = distance;
            meeting = Mathf.Lerp(segment.HeightA, segment.HeightB, t);
        }
        return meeting;
    }

    /// <summary>
    /// Brings the end of a road at <paramref name="end"/> to <paramref name="height"/> over the last stretch before it
    /// (walking in <paramref name="step"/>).
    /// </summary>
    private static void EaseTo(float[] heights, List<Vector2> flat, float height, int end, int step)
    {
        float walked = 0;
        for (int i = end; i >= 0 && i < heights.Length && walked < JoinReach; i += step)
        {
            if (i != end)
                walked += flat[i].DistanceTo(flat[i - step]);
            heights[i] = Mathf.Lerp(heights[i], height, 1 - Mathf.SmoothStep(0, JoinReach, walked));
        }
    }

    /// <summary>A road that meets another comes to its height over this distance.</summary>
    private const float JoinReach = 50;

    private void Add(Segment segment)
    {
        var low = (segment.A.Min(segment.B) - Vector2.One * Shoulder) / CellSize;
        var high = (segment.A.Max(segment.B) + Vector2.One * Shoulder) / CellSize;
        for (int z = Mathf.FloorToInt(low.Y); z <= Mathf.FloorToInt(high.Y); z++)
        for (int x = Mathf.FloorToInt(low.X); x <= Mathf.FloorToInt(high.X); x++)
        {
            var key = new Vector2I(x, z);
            if (!cells.TryGetValue(key, out var list))
                cells[key] = list = new();
            list.Add(segment);
        }
    }

    /// <summary>The ground at (<paramref name="x"/>, <paramref name="z"/>) with the roads graded into <paramref name="height"/>.</summary>
    public float Apply(float x, float z, float height)
    {
        // The old valley keeps its ground: the grading begins only beyond its edge.
        float fade = Mathf.SmoothStep(
            WorldTuning.CoreRadius,
            WorldTuning.CoreRadius + 45,
            Mathf.Max(Mathf.Abs(x), Mathf.Abs(z))
        );
        if (
            fade <= 0
            || !cells.TryGetValue(
                new Vector2I(Mathf.FloorToInt(x / CellSize), Mathf.FloorToInt(z / CellSize)),
                out var list
            )
        )
            return height;
        var point = new Vector2(x, z);
        float nearest = Shoulder * Shoulder;
        float target = height;
        foreach (var segment in list)
        {
            var delta = segment.B - segment.A;
            float lengthSquared = delta.LengthSquared();
            float t = lengthSquared < .000001f ? 0 : Mathf.Clamp((point - segment.A).Dot(delta) / lengthSquared, 0, 1);
            float squared = point.DistanceSquaredTo(segment.A + delta * t);
            if (squared >= nearest)
                continue;
            nearest = squared;
            target = Mathf.Lerp(segment.HeightA, segment.HeightB, t);
        }
        float blend = 1 - Mathf.SmoothStep(Flat, Shoulder, Mathf.Sqrt(nearest));
        return Mathf.Lerp(height, target, blend * fade);
    }
}
