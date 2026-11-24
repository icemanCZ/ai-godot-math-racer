using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The dragon's flight round the valley with the player on his back (<c>docs/DRAGON_PLAN.md</c>, phase 4). He takes off
/// from his ledge towards the valley, climbs, flies once round its middle high above the woods and the village, and comes
/// back down onto his ledge. The way is a smooth closed curve (Catmull-Rom through a ring of points), measured by its
/// length so that he flies at an even speed; it is made from the ground alone, without random numbers.
/// </summary>
internal sealed class DragonFlight
{
    /// <summary>How long the whole flight takes, in seconds.</summary>
    public const float Duration = 55;

    /// <summary>The radius of the ring round the middle of the valley.</summary>
    private const float Radius = 520;

    /// <summary>How high above the highest ground near it every point of the ring lies.</summary>
    private const float Clearance = 70;

    /// <summary>How long taking off and landing take, while he turns from his rest into the flight and back.</summary>
    private const float Settle = 3;

    private const int Samples = 600;

    /// <summary>Beyond this distance from the ledge the flight is never lower than <see cref="LowestOver"/> above the ground.</summary>
    private const float LedgeReach = 120,
        LowestOver = 35;

    /// <summary>Over how many samples on either side a lift over a hill fades out.</summary>
    private const int LiftSpread = 40;

    private readonly Transform3D rest;
    private readonly List<Vector3> points = new();
    private readonly Vector3[] samples = new Vector3[Samples + 1];
    private readonly float[] lengths = new float[Samples + 1];

    /// <summary>The length of the flight in metres.</summary>
    internal float Length => lengths[Samples];

    internal DragonFlight(Transform3D rest, Terrain ground)
    {
        this.rest = rest;
        var home = rest.Origin;
        var forward = rest.Basis.Z with { Y = 0 };
        forward = forward.LengthSquared() > .01f ? forward.Normalized() : Vector3.Back;
        Vector3 Lifted(Vector3 p, float clearance)
        {
            float high = ground.Height(p.X, p.Z);
            foreach (var (dx, dz) in new[] { (40f, 0f), (-40f, 0f), (0f, 40f), (0f, -40f) })
                high = Mathf.Max(high, ground.Height(p.X + dx, p.Z + dz));
            return p with { Y = high + clearance };
        }
        points.Add(home);
        points.Add(home + forward * 25 + Vector3.Up * 10);
        points.Add(Lifted(home + forward * 110, 45));
        // The ring round the middle, starting from the side of the lair and turning to the right.
        float start = Mathf.Atan2(home.X, home.Z);
        float turn = forward.Cross(-home with { Y = 0 }).Y >= 0 ? 1 : -1;
        const int ring = 10;
        for (int i = 1; i < ring; i++)
        {
            float a = start + turn * i * Mathf.Tau / ring;
            points.Add(Lifted(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * Radius, Clearance));
        }
        points.Add(Lifted(home + forward * 130, 50));
        points.Add(home + forward * 30 + Vector3.Up * 12);
        points.Add(home);
        for (int i = 0; i <= Samples; i++)
            samples[i] = Curve(i / (float)Samples);
        // A hill between two points of the ring would come too close: the curve is lifted over it, smoothly.
        var lift = new float[Samples + 1];
        for (int i = 0; i <= Samples; i++)
        {
            var p = samples[i];
            if (new Vector2(p.X - home.X, p.Z - home.Z).Length() > LedgeReach)
                lift[i] = Mathf.Max(0, ground.Height(p.X, p.Z) + LowestOver - p.Y);
        }
        for (int i = 0; i <= Samples; i++)
        {
            float raise = 0;
            for (int j = Mathf.Max(0, i - LiftSpread); j <= Mathf.Min(Samples, i + LiftSpread); j++)
                raise = Mathf.Max(raise, lift[j] * (.5f + .5f * Mathf.Cos(Mathf.Pi * (j - i) / (LiftSpread + 1))));
            samples[i].Y += raise;
        }
        // Measure the curve.
        for (int i = 1; i <= Samples; i++)
            lengths[i] = lengths[i - 1] + samples[i].DistanceTo(samples[i - 1]);
    }

    /// <summary>A point of the curve, <paramref name="u"/> from 0 (the ledge) to 1 (the ledge again), by its control points.</summary>
    private Vector3 Curve(float u)
    {
        int segments = points.Count - 1;
        float f = Mathf.Clamp(u, 0, 1) * segments;
        int i = Mathf.Min((int)f, segments - 1);
        float t = f - i;
        var p0 = points[Mathf.Max(i - 1, 0)];
        var p1 = points[i];
        var p2 = points[i + 1];
        var p3 = points[Mathf.Min(i + 2, segments)];
        return p1.CubicInterpolate(p2, p0, p3, t);
    }

    /// <summary>The point that lies <paramref name="distance"/> metres along the flight.</summary>
    private Vector3 At(float distance)
    {
        distance = Mathf.Clamp(distance, 0, Length);
        int lo = 0,
            hi = Samples;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (lengths[mid] < distance)
                lo = mid;
            else
                hi = mid;
        }
        float span = lengths[hi] - lengths[lo];
        return samples[lo].Lerp(samples[hi], span > 0 ? (distance - lengths[lo]) / span : 0);
    }

    /// <summary>
    /// Where the dragon is <paramref name="seconds"/> into the flight: on the curve, looking along it and leaning into
    /// the turns; at the start and the end he turns between his rest on the ledge and the flight.
    /// </summary>
    internal Transform3D Pose(float seconds)
    {
        float t = Mathf.Clamp(seconds / Duration, 0, 1);
        // Slow off the ledge and onto it again, even in between.
        float eased =
            t < .08f ? t * t / .16f
            : t > .92f ? .92f - (1 - t) * (1 - t) / .16f
            : t - .04f;
        eased /= .92f;
        float distance = Mathf.Clamp(eased, 0, 1) * Length;
        var here = At(distance);
        var ahead = At(distance + 6) - here;
        var further = At(distance + 30) - At(distance + 6);
        if (ahead.LengthSquared() < .001f)
            ahead = rest.Basis.Z;
        var direction = ahead.Normalized();
        // Climbing and diving tilt the body less than the curve does.
        direction = (direction with { Y = direction.Y * .5f }).Normalized();
        var flat = direction with { Y = 0 };
        var flatFurther = further with { Y = 0 };
        float lean = 0;
        if (flat.LengthSquared() > .001f && flatFurther.LengthSquared() > .001f)
            lean = Mathf.Clamp(
                -flat.Normalized().SignedAngleTo(flatFurther.Normalized(), Vector3.Up) * 1.6f,
                -.6f,
                .6f
            );
        var basis = Basis.LookingAt(direction, Vector3.Up, true) * new Basis(Vector3.Back, lean);
        var pose = new Transform3D(basis.Orthonormalized(), here);
        float settle = Mathf.Min(seconds, Duration - seconds);
        if (settle < Settle)
        {
            float blend = Mathf.SmoothStep(0, 1, Mathf.Max(0, settle) / Settle);
            pose = rest.InterpolateWith(pose, blend);
        }
        return pose;
    }

    /// <summary>How low over the ground the flight comes away from the ledge, for the checks.</summary>
    internal float LowestClearance(Terrain ground)
    {
        float lowest = float.MaxValue;
        foreach (var p in samples)
            if (new Vector2(p.X - rest.Origin.X, p.Z - rest.Origin.Z).Length() > LedgeReach)
                lowest = Mathf.Min(lowest, p.Y - ground.Height(p.X, p.Z));
        return lowest;
    }
}
