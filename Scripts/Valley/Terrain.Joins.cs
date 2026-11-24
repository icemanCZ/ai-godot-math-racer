using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

/// <summary>
/// The ground where a join leaves an old road for the ring. An old road is cut into the slope beside it, so a join that
/// leaves it straight out climbed the bank of the cut: up to six metres in ten. Here the first stretch of each join gets
/// a ramp of its own, from the old road's level up a gentle grade to the ground beyond the bank, and a narrow strip of
/// the ground is cut or filled to it. The strip keeps inside the clearance that every join keeps from what is built and
/// from the trees (<c>JoinClearance</c>), stays off the old road it leaves and off the river, and fades out well inside
/// the old valley, so nothing that stands moves and the land beyond it stays as it was. The ground at every point of the
/// 25 m grid, where the fingerprints of the world read it (<c>Tests/Worlds</c>), keeps its height: the ramp is led
/// through such a point at the point's own height.
/// </summary>
public partial class Terrain
{
    /// <summary>The ground follows the ramp fully this far from its middle and is left alone from the shoulder on.</summary>
    private const float JoinFlat = 3.5f,
        JoinShoulder = 7;

    /// <summary>The strip begins this far along the join, beyond the edge of the old road, and is full a little later.</summary>
    private const float JoinLeave = 2,
        JoinFull = 4;

    /// <summary>How far along the join the ramp runs at most, and how near the old valley's 500 m grid it may come.</summary>
    private const float JoinRamp = 80,
        JoinInside = 480;

    /// <summary>How far from the old road the bank of its cut is looked for.</summary>
    private const float JoinBank = 24;

    /// <summary>
    /// The ground is cut no nearer the edge of an old road than this and follows the ramp fully a little further out.
    /// It is raised only a step of the ground's grid further out, so that the ground under the road, which is drawn
    /// between the grid's points, never rises over the road's surface.
    /// </summary>
    private const float JoinRoadKeep = 0,
        JoinRoadFree = 1.5f,
        JoinRaiseKeep = JoinRoadKeep + Settings.TerrainStep,
        JoinRaiseFree = JoinRoadFree + Settings.TerrainStep;

    private const float JoinStep = 2,
        JoinSmoothing = 12,
        JoinMaxGrade = .15f,
        FingerprintGrid = 25;

    /// <summary>Cuts and fills the ground under the start of every join; called before the meshes are built.</summary>
    /// <param name="roadEdge">The signed distance to the edge of the old roads, negative on a road.</param>
    internal void GradeJoins(IEnumerable<Vector3[]> joins, Func<Vector2, float> roadEdge)
    {
        if (heights == null)
            return;
        var changes = new Dictionary<Vector2I, (float Weight, float Height)>();
        foreach (var points in joins)
            GradeJoin(points, roadEdge, changes);
        foreach (var (node, change) in changes)
            heights[node.X, node.Y] = Mathf.Lerp(heights[node.X, node.Y], change.Height, change.Weight);
        if (changes.Count > 0)
            meshesStale = true;
    }

    private static Vector2 NodePosition(int x, int z) =>
        new(x * Settings.TerrainStep - WorldTuning.CoreRadius, z * Settings.TerrainStep - WorldTuning.CoreRadius);

    private static bool OnFingerprintGrid(Vector2 p)
    {
        static bool OnLine(float c) => Mathf.Abs(c - Mathf.Round(c / FingerprintGrid) * FingerprintGrid) < .01f;
        return OnLine(p.X) && OnLine(p.Y);
    }

    private void GradeJoin(
        Vector3[] points,
        Func<Vector2, float> roadEdge,
        Dictionary<Vector2I, (float Weight, float Height)> changes
    )
    {
        using var curve = WorldPlan.CreateCurve(points);
        float length = Mathf.Min(curve.GetBakedLength(), JoinRamp);
        var flat = new List<Vector2>();
        var raw = new List<float>();
        for (float distance = 0; distance <= length; distance += JoinStep)
        {
            var p = curve.SampleBaked(distance);
            if (Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > JoinInside)
                break;
            flat.Add(new Vector2(p.X, p.Z));
            raw.Add(Height(p.X, p.Z));
        }
        // The ramp is as long as the bank beside the old road asks for at a gentle grade, and a little more.
        float bank = 0;
        for (int i = 0; i < raw.Count && i * JoinStep <= JoinBank; i++)
            bank = Mathf.Max(bank, Mathf.Abs(raw[i] - raw[0]));
        int keep = Mathf.CeilToInt(Mathf.Clamp(bank / JoinMaxGrade + JoinBank / 2, JoinBank, JoinRamp) / JoinStep) + 1;
        if (raw.Count > keep)
        {
            flat.RemoveRange(keep, flat.Count - keep);
            raw.RemoveRange(keep, raw.Count - keep);
        }
        if (flat.Count < 8)
            return;
        float reach = JoinShoulder + JoinStep;
        var low = flat[0];
        var high = flat[0];
        foreach (var p in flat)
        {
            low = low.Min(p);
            high = high.Max(p);
        }
        int count = heights!.GetLength(0) - 1;
        int Index(float coordinate) =>
            Mathf.Clamp(Mathf.RoundToInt((coordinate + WorldTuning.CoreRadius) / Settings.TerrainStep), 0, count);
        // The nearest point of the ramp's middle to a node: how far it is and how far along it lies.
        (float Distance, int Segment, float T) Nearest(Vector2 node)
        {
            (float Distance, int Segment, float T) best = (float.PositiveInfinity, 0, 0);
            for (int i = 0; i + 1 < flat.Count; i++)
            {
                var delta = flat[i + 1] - flat[i];
                float t = Mathf.Clamp((node - flat[i]).Dot(delta) / Mathf.Max(delta.LengthSquared(), .0001f), 0, 1);
                float distance = node.DistanceTo(flat[i] + delta * t);
                if (distance < best.Distance)
                    best = (distance, i, t);
            }
            return best;
        }
        var nodes = new List<(Vector2I Key, float Distance, int Segment, float T)>();
        var pins = new Dictionary<int, float>();
        for (int z = Index(low.Y - reach); z <= Index(high.Y + reach); z++)
        for (int x = Index(low.X - reach); x <= Index(high.X + reach); x++)
        {
            var position = NodePosition(x, z);
            var nearest = Nearest(position);
            if (nearest.Distance >= JoinShoulder)
                continue;
            if (OnFingerprintGrid(position))
            {
                // A point under the middle is passed at its own height; one at the shoulder is only left alone.
                if (nearest.Distance <= JoinFlat)
                    pins[Mathf.RoundToInt(nearest.Segment + nearest.T)] = heights[x, z];
            }
            else
                nodes.Add((new Vector2I(x, z), nearest.Distance, nearest.Segment, nearest.T));
        }
        // The ramp leaves the old road at the road's own height.
        int onRoad = 0;
        while (onRoad < flat.Count - 1 && roadEdge(flat[onRoad]) < JoinRoadFree)
            onRoad++;
        var profile = JoinProfile(raw, pins, onRoad);
        float end = (flat.Count - 1) * JoinStep;
        foreach (var (key, distance, segment, t) in nodes)
        {
            var position = NodePosition(key.X, key.Y);
            float along = (segment + t) * JoinStep;
            float target = Mathf.Lerp(profile[segment], profile[segment + 1], t),
                edge = roadEdge(position);
            bool raised = target > heights[key.X, key.Y];
            float weight =
                (1 - Mathf.SmoothStep(JoinFlat, JoinShoulder, distance))
                * Mathf.SmoothStep(JoinLeave, JoinFull, along)
                * (1 - Mathf.SmoothStep(end - JoinFull, end, along))
                * (
                    raised
                        ? Mathf.SmoothStep(JoinRaiseKeep, JoinRaiseFree, edge)
                        : Mathf.SmoothStep(JoinRoadKeep, JoinRoadFree, edge)
                )
                * Mathf.SmoothStep(4, 10, River.Distance(position.X, position.Y) - River.HalfWidth(position.Y));
            if (weight <= 0)
                continue;
            if (!changes.TryGetValue(key, out var change) || change.Weight < weight)
                changes[key] = (weight, target);
        }
    }

    /// <summary>
    /// The heights along a ramp: the ground smoothed, then brought to the old road's level at its start, to the ground
    /// at its end and to the points of the fingerprints' grid it passes, and held to a gentle grade between them.
    /// </summary>
    private static float[] JoinProfile(List<float> raw, Dictionary<int, float> pins, int onRoad)
    {
        int count = raw.Count,
            reach = Mathf.RoundToInt(JoinSmoothing / JoinStep);
        var heights = new float[count];
        for (int i = 0; i < count; i++)
        {
            float sum = 0;
            int used = 0;
            for (int j = Mathf.Max(0, i - reach); j <= Mathf.Min(count - 1, i + reach); j++)
            {
                sum += raw[j];
                used++;
            }
            heights[i] = sum / used;
        }
        float start = raw[0],
            finish = raw[^1];
        for (int i = 0; i < count; i++)
        {
            float fromStart = i * JoinStep,
                fromEnd = (count - 1 - i) * JoinStep;
            heights[i] = Mathf.Lerp(heights[i], start, 1 - Mathf.SmoothStep(0, 30, fromStart));
            heights[i] = Mathf.Lerp(heights[i], finish, 1 - Mathf.SmoothStep(0, 20, fromEnd));
        }
        // The ramp keeps the ground's height up to the old road's edge and meets the ground at its end.
        var fixedAt = new HashSet<int> { count - 1 };
        for (int i = 0; i <= Mathf.Max(onRoad, Mathf.FloorToInt(JoinFull / JoinStep)) && i < count - 1; i++)
        {
            heights[i] = raw[i];
            fixedAt.Add(i);
        }
        heights[^1] = finish;
        foreach (var (index, height) in pins)
        {
            int at = Mathf.Clamp(index, 0, count - 1);
            heights[at] = height;
            fixedAt.Add(at);
        }
        // Between two fixed heights the grade is the gentle one, or the even one that the two heights ask for.
        var fixedOrder = new List<int>(fixedAt);
        fixedOrder.Sort();
        for (int k = 0; k + 1 < fixedOrder.Count; k++)
        {
            int from = fixedOrder[k],
                to = fixedOrder[k + 1];
            float rise = Mathf.Max(
                JoinMaxGrade * JoinStep,
                Mathf.Abs(heights[to] - heights[from]) / (to - from) * 1.001f
            );
            for (int round = 0; round < 2; round++)
            {
                for (int i = from + 1; i < to; i++)
                    heights[i] = Mathf.Clamp(heights[i], heights[i - 1] - rise, heights[i - 1] + rise);
                for (int i = to - 1; i > from; i--)
                    heights[i] = Mathf.Clamp(heights[i], heights[i + 1] - rise, heights[i + 1] + rise);
            }
        }
        return heights;
    }
}
