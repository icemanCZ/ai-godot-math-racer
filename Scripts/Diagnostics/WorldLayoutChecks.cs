using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public static class WorldLayoutChecks
{
    public static void ValidatePlan(WorldPlan plan, int seed)
    {
        var links = new Dictionary<Vector3, HashSet<Vector3>>();
        foreach (var route in plan.Routes)
            for (int i = 1; i < route.Length; i++)
            {
                if (!links.ContainsKey(route[i - 1])) links[route[i - 1]] = new();
                if (!links.ContainsKey(route[i])) links[route[i]] = new();
                links[route[i - 1]].Add(route[i]); links[route[i]].Add(route[i - 1]);
            }
        var visited = new HashSet<Vector3>();
        var pending = new Stack<Vector3>(); pending.Push(plan.Routes[0][0]);
        while (pending.TryPop(out var point))
            if (visited.Add(point)) foreach (var neighbour in links[point]) pending.Push(neighbour);
        if (visited.Count != links.Count || links.Values.Any(neighbours => neighbours.Count < 2))
            throw new InvalidOperationException($"Disconnected road or dead end: seed={seed}");
        if (!links.TryGetValue(plan.VillageJunction, out var streets) || streets.Count != 4)
            throw new InvalidOperationException($"Village must have a four-way crossroads: seed={seed}");
        foreach (var route in plan.Routes)
        {
            using var curve = WorldPlan.CreateCurve(route);
            for (float distance = 0; distance < curve.GetBakedLength(); distance += 2)
            {
                var p = curve.SampleBaked(distance);
                if (p.Length() > Settings.PlayableRadius - 10)
                    throw new InvalidOperationException($"Road leaves world boundary: seed={seed}, position={p}");
                float riverDistance = Mathf.Abs(p.X - plan.CenterX(p.Z));
                if (riverDistance < 9 && Mathf.Abs(p.Z - plan.BridgeZ) > 3)
                    throw new InvalidOperationException($"Road crosses water outside bridge: seed={seed}, position={p}");
            }
        }
        for (float z = -Settings.WorldRadius; z < Settings.WorldRadius; z += 2)
            if (Mathf.Abs(plan.CenterX(z + 2) - plan.CenterX(z)) > .441f)
                throw new InvalidOperationException($"River bend is too sharp: seed={seed}, z={z}");
    }

    public static void Run(Action<bool, string> check)
    {
        var signatures = new HashSet<string>();
        var sides = new HashSet<int>();
        var bendCounts = new HashSet<int>();
        double selectedCost = 0, originalCost = 0;
        for (int seed = 1; seed <= 128; seed++)
        {
            var plan = new WorldPlan(seed);
            var repeat = new WorldPlan(seed);
            string Signature(WorldPlan p) => string.Join(";", p.Routes.SelectMany(route => route).Select(point => point.ToString())) + p.BridgeZ;
            if (Signature(plan) != Signature(repeat)) throw new InvalidOperationException("World layout is not reproducible.");
            signatures.Add(Signature(plan)); sides.Add(plan.Side); bendCounts.Add(plan.Routes[2].Length);
            ValidatePlan(plan, seed);
            for (float z = -Settings.WorldRadius; z <= Settings.WorldRadius; z += 8)
            {
                float originalX = plan.Side * (plan.RiverOffset + 12 * Mathf.Sin(z * .018f + plan.RiverPhase) +
                    6 * Mathf.Sin(z * .043f - plan.RiverPhase));
                float water = -.8f - z * .008f;
                float selected = Mathf.Max(0, plan.NaturalHeight(plan.CenterX(z), z) - water);
                float original = Mathf.Max(0, plan.NaturalHeight(originalX, z) - water);
                selectedCost += selected * selected;
                originalCost += original * original;
            }
        }
        check(signatures.Count == 128 && sides.Count == 2 && bendCounts.Count > 1,
            "128 reproducible distinct layouts have connected roads, no dead ends, valid bridges and gradual river bends");
        check(selectedCost < originalCost,
            "terrain-aware river routes avoid higher ground better than the previous terrain-independent curves");
        GD.Print($"RIVER_ROUTE_CHECKS_PASSED terrain_cost_ratio={selectedCost / originalCost:F3}");
    }
}
