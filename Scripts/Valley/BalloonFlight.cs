using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The way the wind carries the hot-air balloon (<c>docs/BALLOON_PLAN.md</c>): from the launch field out towards the
/// middle of the valley in a long loop and back onto the field. The player cannot steer a balloon, only make it climb or
/// sink, so the way is fixed and only the height above it is the player's (<see cref="BalloonRide"/>). It is a smooth
/// curve (Catmull-Rom) measured on the flat, so that the balloon drifts at an even speed, with a floor that keeps it
/// clear of the hills and comes down to the field at both ends; it is made from the ground alone, without random numbers.
/// </summary>
internal sealed class BalloonFlight
{
    /// <summary>How fast the wind carries the balloon, in metres a second on the flat.</summary>
    public const float Speed = 10.5f;

    /// <summary>The stars that hang along the way, at the heights of <see cref="StarHeight"/>.</summary>
    public const int Stars = 8;

    /// <summary>How far above its floor the player may take the balloon.</summary>
    public const float Highest = 56;

    /// <summary>How high above the highest ground near the way the floor lies, away from the field.</summary>
    private const float Clearance = 14;

    /// <summary>Over how many metres from the field the floor climbs to its clearance, and comes down again at the end.</summary>
    private const float Ramp = 90;

    private const int Samples = 500;

    /// <summary>Over how many samples on either side a hill lifts the floor, smoothly.</summary>
    private const int LiftSpread = 30;

    /// <summary>
    /// The heights of the stars above the floor, in the order they hang along the way; every flight starts three further
    /// on (<see cref="StarHeight"/>), so that the next flight asks for other climbs.
    /// </summary>
    private static readonly float[] Pattern = { 12, 27, 42, 22, 48, 31, 15, 38 };

    private readonly List<Vector3> points = new();
    private readonly Vector3[] samples = new Vector3[Samples + 1];
    private readonly float[] lengths = new float[Samples + 1];

    /// <summary>Where the basket stands on the field before the take-off and after the landing.</summary>
    internal Vector3 Pad { get; }

    /// <summary>The length of the way on the flat, in metres.</summary>
    internal float Length => lengths[Samples];

    /// <summary>How long the wind takes to carry the balloon round, without the slow start and landing.</summary>
    internal float Duration => Length / Speed;

    internal BalloonFlight(BalloonSite site, Terrain ground, Vector3 middle)
    {
        Pad = ground.At(site.Pad.X, site.Pad.Z);
        var forward = (middle - Pad) with { Y = 0 };
        float away = forward.Length();
        forward = away > 1 ? forward / away : site.Inward;
        var right = Vector3.Up.Cross(forward);
        float reach = Mathf.Clamp(away * .42f, 230, 320);
        var pad = Pad with { Y = 0 };
        points.Add(pad);
        points.Add(pad + forward * 40 + right * 6);
        points.Add(pad + forward * reach * .4f + right * reach * .3f);
        points.Add(pad + forward * reach * .8f + right * reach * .32f);
        points.Add(pad + forward * reach);
        points.Add(pad + forward * reach * .8f - right * reach * .32f);
        points.Add(pad + forward * reach * .4f - right * reach * .3f);
        points.Add(pad + forward * 40 - right * 6);
        points.Add(pad);
        for (int i = 0; i <= Samples; i++)
            samples[i] = Curve(i / (float)Samples);
        for (int i = 1; i <= Samples; i++)
            lengths[i] = lengths[i - 1] + samples[i].DistanceTo(samples[i - 1]);
        // The floor: above the highest ground round each point, lifted smoothly over a hill, and down to the field at
        // both ends.
        var high = new float[Samples + 1];
        for (int i = 0; i <= Samples; i++)
        {
            var p = samples[i];
            float h = ground.Height(p.X, p.Z);
            foreach (var (dx, dz) in new[] { (25f, 0f), (-25f, 0f), (0f, 25f), (0f, -25f) })
                h = Mathf.Max(h, ground.Height(p.X + dx, p.Z + dz));
            high[i] = h + Clearance;
        }
        for (int i = 0; i <= Samples; i++)
        {
            float floor = float.MinValue;
            for (int j = Mathf.Max(0, i - LiftSpread); j <= Mathf.Min(Samples, i + LiftSpread); j++)
                floor = Mathf.Max(
                    floor,
                    high[j] - (high[j] - high[i]) * (.5f - .5f * Mathf.Cos(Mathf.Pi * (j - i) / (LiftSpread + 1)))
                );
            float end = Mathf.Min(lengths[i], Length - lengths[i]);
            samples[i].Y = Mathf.Lerp(Pad.Y, Mathf.Max(floor, Pad.Y), Mathf.SmoothStep(0, Ramp, end));
        }
    }

    /// <summary>A point of the curve on the flat, <paramref name="u"/> from 0 (the field) to 1 (the field again).</summary>
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

    /// <summary>The point of the floor that lies <paramref name="distance"/> metres along the way.</summary>
    internal Vector3 At(float distance)
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

    /// <summary>Where the wind carries the balloon at <paramref name="distance"/>, on the flat.</summary>
    internal Vector3 Heading(float distance)
    {
        var ahead = (At(distance + 4) - At(distance - 4)) with { Y = 0 };
        return ahead.LengthSquared() > .0001f ? ahead.Normalized() : Vector3.Forward;
    }

    /// <summary>How far along the way star <paramref name="star"/> hangs: from a sixth of the way to past four fifths.</summary>
    internal float StarDistance(int star) => Length * (.17f + star * .094f);

    /// <summary>How high above the floor star <paramref name="star"/> of the flight numbered <paramref name="flight"/> hangs.</summary>
    internal static float StarHeight(int star, int flight) => Pattern[(star + flight * 3) % Pattern.Length];

    /// <summary>Where star <paramref name="star"/> of the flight numbered <paramref name="flight"/> hangs, at the basket's middle.</summary>
    internal Vector3 StarAt(int star, int flight) =>
        At(StarDistance(star)) + Vector3.Up * (StarHeight(star, flight) + BalloonRide.BasketMiddle);

    /// <summary>How low over the ground the floor comes away from the field, for the checks.</summary>
    internal float LowestClearance(Terrain ground)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i <= Samples; i++)
            if (Mathf.Min(lengths[i], Length - lengths[i]) > Ramp)
                lowest = Mathf.Min(lowest, samples[i].Y - ground.Height(samples[i].X, samples[i].Z));
        return lowest;
    }
}
