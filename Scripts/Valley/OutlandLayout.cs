using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The fixed frame of the land between the old valley and the borders (<c>docs/MAP_EXPANSION_PLAN.md</c>, phase 4): a
/// ring road round the old valley, rounded like a square, and the two bridges where it crosses the valley's river, in the
/// north and in the south. It comes from the seed alone, with random numbers of its own, so the river and the ground can
/// know the bridges before anything else of the outland is placed; the roads that join the ring to the old roads and run
/// out to the borders are laid by <see cref="World"/> once the old valley stands.
/// </summary>
public sealed class OutlandLayout
{
    /// <summary>A bridge of the ring over the valley's river: its deck runs across the river (along x) at <see cref="Z"/>.</summary>
    public readonly record struct Bridge(float CenterX, float Z, float HalfLength, float Top)
    {
        /// <summary>Half the width of the deck, across the road.</summary>
        public const float HalfWidth = 3.8f;

        public bool Covers(float x, float z, float margin = 0) =>
            MathF.Abs(x - CenterX) < HalfLength + margin && MathF.Abs(z - Z) < HalfWidth + margin;
    }

    /// <summary>How far the ring keeps from the middle at its sides, where it is straight enough for a bridge.</summary>
    public float RingRadius { get; }

    private readonly float wavePhase;
    private readonly float turn;
    private readonly WorldPlan plan;
    private Bridge[]? bridges;

    internal OutlandLayout(int seed, WorldPlan plan)
    {
        this.plan = plan;
        var random = new Random(seed + 7740);
        RingRadius = 780 + (float)random.NextDouble() * 60;
        wavePhase = (float)random.NextDouble() * Mathf.Tau;
        turn = (float)random.NextDouble() * Mathf.Tau;
    }

    /// <summary>The bridges in the north and the south, each square across the river where the ring crosses it.</summary>
    public IReadOnlyList<Bridge> Bridges => bridges ??= new[] { BridgeAt(-RingRadius), BridgeAt(RingRadius) };

    private Bridge BridgeAt(float z) =>
        new(plan.CenterX(z), z, River.HalfWidth(z) + 7, River.Level(z) + River.OutlandDeckRise);

    /// <summary>The bridge whose deck lies under the point, if any.</summary>
    public Bridge? BridgeAt(float x, float z, float margin = 0)
    {
        foreach (var bridge in Bridges)
            if (bridge.Covers(x, z, margin))
                return bridge;
        return null;
    }

    /// <summary>
    /// The points of the ring, closed (the first is also the last), going round once. Its sides bulge and dent a little,
    /// but not where it crosses the river: there it runs straight across on four points, the middle two at the ends of
    /// the bridge, so that the deck lies square to the road.
    /// </summary>
    public List<Vector3> Ring()
    {
        // Points about 33 m apart, evenly: a curve through points far apart and points close together loops.
        const int count = 160;
        var points = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            float angle = turn + i * Mathf.Tau / count;
            float c = Mathf.Cos(angle),
                s = Mathf.Sin(angle);
            // A superellipse: round in the corners, nearly straight along the sides.
            float shape = Mathf.Pow(Mathf.Pow(Mathf.Abs(c), 4) + Mathf.Pow(Mathf.Abs(s), 4), -.25f);
            // The sides wave in and out, least where the ring meets the river in the north and the south.
            float wave = c * c * (.035f * Mathf.Sin(3 * angle + wavePhase) + .02f * Mathf.Sin(5 * angle - wavePhase));
            float radius = RingRadius * shape * (1 + wave);
            points.Add(new Vector3(c * radius, 0, s * radius));
        }
        foreach (var bridge in Bridges)
        {
            // Leave out the ring's points near the crossing and lay the straight piece in their place.
            int first = -1;
            for (int i = 0; i < points.Count; i++)
                if (Mathf.Sign(points[i].Z) == Mathf.Sign(bridge.Z) && Mathf.Abs(points[i].X - bridge.CenterX) < 80)
                {
                    points.RemoveAt(i);
                    first = first < 0 ? i : first;
                    i--;
                }
            if (first < 0)
                first = NearestIndex(points, new Vector3(bridge.CenterX, 0, bridge.Z));
            // Going round with the angle, x grows in the south (z > 0) and falls in the north.
            float direction = bridge.Z > 0 ? -1 : 1;
            float approach = bridge.HalfLength + 22;
            var straight = new[]
            {
                new Vector3(bridge.CenterX - direction * approach, 0, bridge.Z),
                new Vector3(bridge.CenterX - direction * bridge.HalfLength, 0, bridge.Z),
                new Vector3(bridge.CenterX + direction * bridge.HalfLength, 0, bridge.Z),
                new Vector3(bridge.CenterX + direction * approach, 0, bridge.Z),
            };
            points.InsertRange(first, straight);
        }
        points.Add(points[0]);
        return points;
    }

    private static int NearestIndex(List<Vector3> points, Vector3 target)
    {
        int best = 0;
        for (int i = 1; i < points.Count; i++)
            if (points[i].DistanceSquaredTo(target) < points[best].DistanceSquaredTo(target))
                best = i;
        return best;
    }
}
