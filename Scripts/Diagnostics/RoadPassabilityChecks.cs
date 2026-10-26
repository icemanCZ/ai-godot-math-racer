using Godot;
using System;

namespace MathRacer;

public static class RoadPassabilityChecks
{
    public static void Run(World world)
    {
        float maxGrade = 0;
        int samples = 0;
        foreach (var points in WorldLayout.Plan.Routes)
        {
            using var curve = WorldPlan.CreateCurve(points);
            float length = curve.GetBakedLength();
            for (float distance = 0; distance <= length; distance += 1)
            {
                var p = curve.SampleBaked(distance);
                var next = curve.SampleBaked(Mathf.Min(distance + 1, length));
                var forward = (curve.SampleBaked(Mathf.Min(distance + .2f, length)) -
                    curve.SampleBaked(Mathf.Max(0, distance - .2f))).Normalized();
                var side = forward.Cross(Vector3.Up);
                foreach (float lane in new[] { -2.2f, 0, 2.2f })
                {
                    var position = p + side * lane;
                    float Ground(Vector3 point) => River.OnBridge(point.X, point.Z)
                        ? River.BridgeSurfaceHeight(point.X) : world.Ground.Height(point.X, point.Z);
                    float ground = Ground(position);
                    if (next.DistanceTo(p) > .1f)
                    {
                        float grade = Mathf.Abs(Ground(next + side * lane) - ground) / next.DistanceTo(p);
                        maxGrade = Mathf.Max(maxGrade, grade);
                        if (grade > RoadGrading.MaxGrade) throw new InvalidOperationException($"Road too steep: seed={Settings.Seed} position={position} grade={grade}");
                    }
                    if (!River.OnBridge(position.X, position.Z) &&
                        (!world.Routes.Contains(position) || world.Routes.SurfaceHeight(position, ground) < ground + .04f))
                        throw new InvalidOperationException($"Road surface ends or is buried: seed={Settings.Seed} position={position} offset={distance} lane={lane} contains={world.Routes.Contains(position)} ground={ground} surface={world.Routes.SurfaceHeight(position, ground)} start={points[0]} next={points[1]}");
                    samples++;
                }
            }
        }
        GD.Print($"ROAD_PASSABILITY_PASSED seed={Settings.Seed} samples={samples} max_grade={maxGrade:F3}");
        float maxBank = 0;
        for (float z = -Settings.WorldRadius + Settings.TerrainStep; z < Settings.WorldRadius - Settings.TerrainStep; z += 5)
            foreach (int side in new[] { -1, 1 })
            {
                float x = River.CenterX(z) + side * (River.HalfWidth(z) + 5);
                float bank = world.Ground.Height(x, z) - River.Level(z);
                maxBank = Mathf.Max(maxBank, bank);
                if (bank > 2.5f) throw new InvalidOperationException($"River forms a narrow canyon: seed={Settings.Seed}, x={x}, z={z}, bank={bank}");
            }
        GD.Print($"RIVER_VALLEY_CHECKS_PASSED seed={Settings.Seed} max_bank={maxBank:F3}");
    }
}
