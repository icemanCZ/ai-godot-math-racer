using Godot;
using System;
using System.Collections.Generic;

namespace MathRacer;

public static class WorldLayout
{
    private static WorldPlan? plan;
    private static int planSeed;
    public static WorldPlan Plan
    {
        get
        {
            if (plan == null || planSeed != Settings.Seed)
            {
                plan = new WorldPlan(Settings.Seed);
                planSeed = Settings.Seed;
            }
            return plan;
        }
    }
    public static float HillPhase => Plan.HillPhase;
    public static float ForestPhase => Plan.ForestPhase;
}

/// <summary>A seeded road graph and river, generated together so the crossing stays connected.</summary>
public sealed class WorldPlan
{
    public static Curve3D CreateCurve(Vector3[] points)
    {
        var curve = new Curve3D { BakeInterval = .25f };
        bool closed = points[0] == points[^1];
        for (int i = 0; i < points.Length; i++)
        {
            var tangent = closed && (i == 0 || i == points.Length - 1)
                ? (points[1] - points[^2]) * .19f
                : (points[Math.Min(i + 1, points.Length - 1)] - points[Math.Max(i - 1, 0)]) * .19f;
            curve.AddPoint(points[i], -tangent, tangent);
        }
        return curve;
    }
    public int Side { get; }
    public float BridgeZ { get; }
    public float RiverOffset { get; }
    public float RiverPhase { get; }
    public float HillPhase { get; }
    public float ForestPhase { get; }
    public Vector3[][] Routes { get; }
    public Vector3 VillageJunction { get; }
    private readonly FastNoiseLite landscapeNoise;
    private readonly RiverCourse riverCourse;
    public float CenterX(float z) => Side * RiverX(z);
    private float RiverX(float z) => riverCourse.At(z);
    private float PreferredRiverX(float z) => RiverOffset + 12 * Mathf.Sin(z * .018f + RiverPhase) + 6 * Mathf.Sin(z * .043f - RiverPhase);
    public float NaturalHeight(float x, float z)
    {
        float natural = landscapeNoise.GetNoise2D(x, z) * 7 + Mathf.Sin(x * .023f) * 2.4f;
        return natural * .65f + Mathf.SmoothStep(130, 250, new Vector2(x, z).Length()) *
            (24 + 7 * Mathf.Sin(x * .012f + HillPhase) * Mathf.Cos(z * .009f - HillPhase) +
            5 * Mathf.Sin((x + z) * .017f + HillPhase));
    }

    public WorldPlan(int seed)
    {
        var random = new Random(seed);
        float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
        Vector3 Point(float x, float z) => new(x, 0, z);
        Side = random.Next(2) == 0 ? -1 : 1;
        BridgeZ = Range(25, 100);
        RiverOffset = Range(115, 155);
        RiverPhase = Range(0, Mathf.Tau);
        HillPhase = Range(0, Mathf.Tau);
        ForestPhase = Range(0, Mathf.Tau);
        landscapeNoise = new FastNoiseLite { Seed = seed, Frequency = .013f, FractalOctaves = 3 };
        riverCourse = new RiverCourse((x, z) => NaturalHeight(Side * x, z), PreferredRiverX);

        var main = new[]
        {
            Point(0, 8), Point(Range(-22, 20), Range(-38, -25)),
            Point(Range(-105, -55), Range(-90, -55)), Point(Range(-155, -125), Range(-205, -185)),
            Point(Range(-110, -80), Range(-275, -245)), Point(Range(30, 45), Range(-205, -185)),
            Point(Range(30, 55), Range(-65, -25)), Point(Range(15, 45), Range(35, 70)), Point(0, 8)
        };
        // A straight village high street gives the settlement a readable centre.
        // Keep the generated countryside beyond it, with a cross street on both sides.
        main[1] = Point(0, -32);
        main[2] = Point(0, -82);
        var villageMain = new List<Vector3>(main);
        villageMain.Insert(3, Point(0, -132));
        VillageJunction = Point(0, -82);
        var routes = new List<Vector3[]> { villageMain.ToArray() };
        float crossing = RiverX(BridgeZ);
        var bridgeEnd = Point(crossing + 40, BridgeZ);
        routes.Add(new[] { main[7], Point(crossing - 40, BridgeZ), Point(crossing - 20, BridgeZ),
            Point(crossing + 20, BridgeZ), bridgeEnd });

        // Both banks have generated loops with variable numbers of bends.
        var west = new List<Vector3> { main[3] };
        int westCount = random.Next(6, 10);
        for (int i = 0; i < westCount; i++)
        {
            float angle = -Mathf.Pi / 2 - i * Mathf.Pi * 1.4f / (westCount - 1) + Range(-.1f, .1f);
            west.Add(Point(-240 + Mathf.Cos(angle) * Range(145, 195), Mathf.Sin(angle) * Range(235, 345)));
        }
        west.Add(main[7]);
        routes.Add(west.ToArray());

        var east = new List<Vector3> { bridgeEnd };
        int eastCount = random.Next(6, 10);
        for (int i = 0; i < eastCount; i++)
        {
            float angle = Mathf.Pi + (i + 1) * Mathf.Tau / (eastCount + 1) + Range(-.08f, .08f);
            float z = BridgeZ * .4f + Mathf.Sin(angle) * Range(225, 310);
            float x = Mathf.Max(315 + Mathf.Cos(angle) * Range(105, 145), RiverX(z) + 38);
            var p = Point(x, z);
            if (p.Length() > 480) p = p.Normalized() * 480;
            east.Add(p);
        }
        east.Add(bridgeEnd);
        routes.Add(east.ToArray());
        // Both arms are through streets, joined to the existing road network.
        routes.Add(new[] { main[3], Point(-65, -82), VillageJunction,
            Point(28, -82), main[6] });

        Routes = routes.ToArray();
        foreach (var route in Routes)
            for (int i = 0; i < route.Length; i++) route[i] = new Vector3(route[i].X * Side, 0, route[i].Z);
    }
}
