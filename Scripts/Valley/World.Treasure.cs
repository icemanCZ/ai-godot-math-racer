using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

/// <summary>Where the pirate captain's treasures are buried, one place for each number of treasures found.</summary>
public partial class World
{
    /// <summary>The place where the present treasure is buried, and later its open chest; hidden between hunts.</summary>
    internal TreasureSite Treasure { get; } = new();

    /// <summary>The places worked out so far, in the order of the treasures; a place depends only on the ones before it.</summary>
    private readonly List<Vector3> treasureSpots = new();

    /// <summary>How many of the treasures before one it keeps clear of.</summary>
    private const int TreasureNeighbours = 12;

    private void CreateTreasure() => AddChild(Treasure);

    /// <summary>
    /// The place of the treasure that comes after <paramref name="found"/> others, made from the seed of the valley and the
    /// places before it only, so the same valley hides the same treasures in the same places every time and nothing has to be
    /// saved. Treasures lie in turn in directions a golden angle apart round the village and at four distances, from a short
    /// drive to a long one, on open and fairly level ground that the car can reach, clear of everything that was built, of the
    /// characters and of the places of the treasures before.
    /// </summary>
    internal Vector3 TreasureSpot(int found)
    {
        found = Math.Max(0, found);
        while (treasureSpots.Count <= found)
            treasureSpots.Add(FindTreasureSpot(treasureSpots.Count));
        return treasureSpots[found];
    }

    private Vector3 FindTreasureSpot(int found)
    {
        var center = Village.Center;
        float angle = Settings.Seed % 628 / 100f + found * 2.39996f;
        float ring = 95 + found % 4 * 45;
        float limit = WorldTuning.CorePlayableRadius - 45;
        // The gap to the treasures before shrinks only in a valley that is short of room.
        float[] gaps = { 60, 40, 24 };
        for (int pass = 0; pass < 3; pass++)
        {
            var random = new Random(Settings.Seed + 9090 + found * 7919 + pass * 104729);
            float jitter = 35 + pass * 40;
            for (int attempt = 0; attempt < 500; attempt++)
            {
                float turn = angle + (float)(random.NextDouble() - .5) * .5f * (pass + 1);
                float distance = ring + (float)(random.NextDouble() - .5) * jitter;
                var p = new Vector3(center.X + Mathf.Sin(turn) * distance, 0, center.Z + Mathf.Cos(turn) * distance);
                if (new Vector2(p.X, p.Z).Length() > limit)
                    continue;
                p = Ground.At(p.X, p.Z);
                if (IsTreasureSpotFree(p, pass == 0) && IsClearOfTreasures(p, found, gaps[pass]))
                    return p;
            }
        }
        // A cramped valley: the first free verge beside a road, which the small friends use as well.
        var fallbackRandom = new Random(Settings.Seed + 9091 + found);
        int start = fallbackRandom.Next(Routes.Samples.Count - 1);
        for (int i = 0; i < Routes.Samples.Count - 1; i++)
        {
            int index = (start + i) % (Routes.Samples.Count - 1);
            var road = Routes.Samples[index];
            var tangent = Routes.Samples[index + 1] - road;
            if (tangent.Length() < .1f)
                continue;
            var side = tangent.Normalized().Cross(Vector3.Up);
            foreach (int sign in new[] { 1, -1 })
            {
                var verge = road + side * sign * 7f;
                var p = Ground.At(verge.X, verge.Z);
                if (IsHiddenSpotFree(p, new(), 0) && IsClearOfTreasures(p, found, 24))
                    return p;
            }
        }
        throw new InvalidOperationException("No place was found for a treasure.");
    }

    private bool IsClearOfTreasures(Vector3 p, int found, float gap)
    {
        for (int i = Math.Max(0, found - TreasureNeighbours); i < found; i++)
            if (FlatDistance(p, treasureSpots[i]) < gap)
                return false;
        return true;
    }

    private bool IsTreasureSpotFree(Vector3 p, bool strict)
    {
        if (
            !IsHiddenSpotFree(p, new(), 0)
            || Routes.Distance(p) < 9
            || FlatDistance(p, Village.Center) < 70
            || Plants.NearTrunk(p, 5f)
        )
            return false;
        if (!strict)
            return true;
        // The car must be able to drive up: the ground within a few metres is nearly level.
        float low = p.Y,
            high = p.Y;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8;
            float height = Ground.Height(p.X + Mathf.Sin(angle) * 6, p.Z + Mathf.Cos(angle) * 6);
            low = Mathf.Min(low, height);
            high = Mathf.Max(high, height);
        }
        return high - low < 2.2f;
    }
}
