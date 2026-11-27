using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Buildings;

namespace MathRacer.Valley;

/// <summary>
/// One stage of the ferrywoman's rally: from the arch beside the road to one border to the arch beside the road to the
/// next, the way round the ring, with its gates. The distances are along the ring from its first point and grow past
/// its length where a stage goes over its closing point (<see cref="RallyCourse.At"/> takes them as they are).
/// </summary>
internal sealed record RallyStage(Border From, Border To, float Start, float Finish, float[] Gates)
{
    public float Length => Finish - Start;

    public string Name => RallyPlans.StageName(From, To);
}

/// <summary>
/// The ferrywoman's rally round the land beyond the old valley (<c>docs/RALLY_PLAN.md</c>) without scene nodes: the
/// middle of the ring road as points 2 m apart on its surface, the same way round as the ring was laid, and the four
/// stages between the roads to the borders. It is made once from the roads alone, without random numbers.
/// </summary>
internal sealed class RallyCourse
{
    /// <summary>The points of the middle of the ring are this far apart.</summary>
    public const float Step = 2;

    /// <summary>Half the width of a gate, across the three lanes; a lane is a third of it.</summary>
    public const float GateHalfWidth = 6;

    /// <summary>Half the width of one lane of a gate.</summary>
    public const float LaneHalfWidth = GateHalfWidth / 3;

    private readonly Vector3[] points;

    public RallyCourse(Vector3[] points, IReadOnlyList<RallyStage> stages)
    {
        this.points = points;
        Stages = stages;
        Length = points.Length * Step;
    }

    /// <summary>The length of the ring, once round.</summary>
    public float Length { get; }

    /// <summary>The stages, in the order they are driven, each beginning where the one before it ends.</summary>
    public IReadOnlyList<RallyStage> Stages { get; }

    /// <summary>The points of the middle of the ring, for the checks and the fingerprint.</summary>
    public IReadOnlyList<Vector3> Points => points;

    /// <summary>The point of the middle of the ring at a distance along it, which may lie past its length.</summary>
    public Vector3 At(float distance)
    {
        float d = Mathf.PosMod(distance, Length) / Step;
        int i = (int)MathF.Floor(d);
        float t = d - i;
        return points[i % points.Length].Lerp(points[(i + 1) % points.Length], t);
    }

    /// <summary>Which way the ring runs at a distance along it, level.</summary>
    public Vector3 Forward(float distance)
    {
        var delta = (At(distance + Step * 2) - At(distance - Step * 2)) with { Y = 0 };
        return delta.LengthSquared() < .0001f ? Vector3.Forward : delta.Normalized();
    }

    /// <summary>To the right of the way the ring runs, level.</summary>
    public Vector3 Right(float distance) => Forward(distance).Cross(Vector3.Up);

    /// <summary>
    /// Where a point is beside the ring: the distance along it nearest to <paramref name="near"/> within
    /// <paramref name="window"/> metres either way, how far to the right of the middle it is (negative to the left) and
    /// how far from the middle on the level.
    /// </summary>
    public (float Along, float Across, float Off) Project(Vector3 p, float near, float window)
    {
        int count = Mathf.CeilToInt(window / Step);
        int middle = Mathf.FloorToInt(near / Step);
        float best = float.MaxValue,
            along = near;
        for (int k = -count; k <= count; k++)
        {
            int i = middle + k;
            var a = points[Mathf.PosMod(i, points.Length)];
            var b = points[Mathf.PosMod(i + 1, points.Length)];
            float dx = b.X - a.X,
                dz = b.Z - a.Z;
            float lengthSquared = Mathf.Max(dx * dx + dz * dz, .0001f);
            float t = Mathf.Clamp(((p.X - a.X) * dx + (p.Z - a.Z) * dz) / lengthSquared, 0, 1);
            float ex = a.X + dx * t - p.X,
                ez = a.Z + dz * t - p.Z;
            float distance = ex * ex + ez * ez;
            if (distance < best)
            {
                best = distance;
                along = (i + t) * Step;
            }
        }
        var right = Right(along);
        var at = At(along);
        float across = (p.X - at.X) * right.X + (p.Z - at.Z) * right.Z;
        return (along, across, MathF.Sqrt(best));
    }

    /// <summary>The lane of a gate a point lies in, -1 left, 0 the middle and 1 right, or null beyond the gate.</summary>
    public static int? LaneAt(float across) =>
        MathF.Abs(across) > GateHalfWidth + .8f ? null
        : across < -LaneHalfWidth ? -1
        : across > LaneHalfWidth ? 1
        : 0;
}
