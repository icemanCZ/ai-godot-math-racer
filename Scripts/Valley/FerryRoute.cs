using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The way of the punt (<c>docs/FERRY_PLAN.md</c>): from the end of the landing's pier out to the middle of the border
/// river, down the middle of it with the flow as far as its mouth, and on over the sea to the island's pier. It is made
/// from the borders alone, without random numbers, and measured by its length, so that the punt glides at an even pace:
/// slowly away from the pier, fast down the river and slowly in again. It floats on whichever water is higher, the river's
/// or the sea's with the tide.
/// </summary>
internal sealed class FerryRoute
{
    /// <summary>The shortest and the longest a crossing takes, in seconds, however long the river is.</summary>
    private const float ShortestCrossing = 20,
        LongestCrossing = 40;

    /// <summary>The pace the crossing is worked out from, in metres a second.</summary>
    private const float Pace = 30;

    private const float SampleStep = 2;

    private readonly List<Vector3> samples = new();
    private readonly List<float> lengths = new();

    /// <summary>The length of the way in metres.</summary>
    internal float Length => lengths[^1];

    /// <summary>How long a crossing takes, in seconds.</summary>
    internal float Duration => Mathf.Clamp(Length / Pace, ShortestCrossing, LongestCrossing);

    /// <summary>The points of the way on the flat, every two metres, from the landing to the island.</summary>
    internal IReadOnlyList<Vector3> Samples => samples;

    internal FerryRoute(IReadOnlyList<Vector3> corners)
    {
        // A smooth curve through the corners (Catmull-Rom), sampled finely and then every two metres along its length.
        var fine = new List<Vector3>();
        for (int i = 0; i + 1 < corners.Count; i++)
        {
            var p0 = corners[Mathf.Max(0, i - 1)];
            var p1 = corners[i];
            var p2 = corners[i + 1];
            var p3 = corners[Mathf.Min(corners.Count - 1, i + 2)];
            int steps = Mathf.Max(2, Mathf.CeilToInt(p1.DistanceTo(p2) / .5f));
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps;
                fine.Add(
                    .5f
                        * (
                            2 * p1
                            + (-p0 + p2) * t
                            + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                            + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t
                        )
                );
            }
        }
        fine.Add(corners[^1]);
        samples.Add(fine[0] with { Y = 0 });
        lengths.Add(0);
        float travelled = 0,
            next = SampleStep;
        for (int i = 1; i < fine.Count; i++)
        {
            var a = fine[i - 1] with { Y = 0 };
            var b = fine[i] with { Y = 0 };
            float piece = a.DistanceTo(b);
            while (piece > 0 && travelled + piece >= next)
            {
                samples.Add(a.Lerp(b, (next - travelled) / piece));
                lengths.Add(next);
                next += SampleStep;
            }
            travelled += piece;
        }
        if (samples[^1].DistanceTo(fine[^1] with { Y = 0 }) > .01f)
        {
            samples.Add(fine[^1] with { Y = 0 });
            lengths.Add(travelled);
        }
    }

    /// <summary>The water the punt floats on at a point: the border river's own level or the sea's, whichever is higher.</summary>
    internal static float WaterAt(Vector3 p, float seaSurface) =>
        Mathf.Max(ValleyBorders.BorderRiverLevel(p.Z), seaSurface);

    /// <summary>How far along the way the punt is <paramref name="seconds"/> into a crossing: slow, fast and slow.</summary>
    internal float DistanceAt(float seconds)
    {
        float t = Mathf.Clamp(seconds / Duration, 0, 1);
        // Easing in and out over the first and the last tenth, gliding evenly between.
        const float ease = .1f;
        float pace = 1 / (1 - ease);
        float s =
            t < ease ? pace * t * t / (2 * ease)
            : t > 1 - ease ? 1 - pace * (1 - t) * (1 - t) / (2 * ease)
            : pace * (t - ease / 2);
        return Length * Mathf.Clamp(s, 0, 1);
    }

    /// <summary>
    /// Where the punt is <paramref name="distance"/> along the way, on the water of <paramref name="seaSurface"/>, and the
    /// way it heads (the rotation about Y that turns +Z ahead); going home it runs the way backwards.
    /// </summary>
    internal (Vector3 Position, float Yaw) At(float distance, float seaSurface, bool home)
    {
        float along = home ? Length - distance : distance;
        along = Mathf.Clamp(along, 0, Length);
        int index = Mathf.Clamp(Mathf.FloorToInt(along / SampleStep), 0, samples.Count - 2);
        float span = lengths[index + 1] - lengths[index];
        float t = span > 0 ? Mathf.Clamp((along - lengths[index]) / span, 0, 1) : 0;
        var p = samples[index].Lerp(samples[index + 1], t);
        // The heading is taken a few metres ahead and behind, so that it turns smoothly through the bends.
        var ahead = Point(along + 4);
        var behind = Point(along - 4);
        var direction = home ? behind - ahead : ahead - behind;
        float yaw = direction.LengthSquared() > .0001f ? Mathf.Atan2(direction.X, direction.Z) : 0;
        return (p with { Y = WaterAt(p, seaSurface) }, yaw);
    }

    private Vector3 Point(float along)
    {
        along = Mathf.Clamp(along, 0, Length);
        int index = Mathf.Clamp(Mathf.FloorToInt(along / SampleStep), 0, samples.Count - 2);
        float span = lengths[index + 1] - lengths[index];
        float t = span > 0 ? Mathf.Clamp((along - lengths[index]) / span, 0, 1) : 0;
        return samples[index].Lerp(samples[index + 1], t);
    }
}
