using System.Collections.Generic;
using Godot;

namespace MathRacer.Characters;

/// <summary>
/// The way the car has just driven, as points about half a metre apart. The companions walk along it, each at a
/// distance of its own behind the car, so a turn of the car reaches a pet only when the pet comes to the bend, the
/// way ducklings follow their mother, instead of swinging the whole group around with the car.
/// </summary>
internal sealed class CompanionPath
{
    private const float Spacing = .5f;

    /// <summary>A step of the car longer than this is a jump (a reset or a teleport), and the old way is forgotten.</summary>
    private const float Jump = 8;

    /// <summary>The points kept by default, more than the longest trail of pets needs.</summary>
    private const int DefaultPoints = 220;

    /// <summary>The points of the way, the oldest first.</summary>
    private readonly List<Vector3> points = new();

    private readonly int maxPoints;

    /// <param name="maxPoints">How many points are kept: half a metre each, so 220 keep the last 110 m.</param>
    internal CompanionPath(int maxPoints = DefaultPoints) => this.maxPoints = maxPoints;

    internal void Clear() => points.Clear();

    /// <summary>Notes where the car is; <paramref name="back"/> is the direction the car's rear faces.</summary>
    internal void Record(Vector3 car, Vector3 back)
    {
        back.Y = 0;
        if (points.Count > 0 && Flat(points[^1] - car).LengthSquared() > Jump * Jump)
            points.Clear();
        // A car that reverses over its own way takes back the points that are now in front of it, so the way always
        // leaves the car backwards.
        while (points.Count > 0 && Flat(points[^1] - car).Dot(back) < -.01f)
            points.RemoveAt(points.Count - 1);
        if (points.Count == 0 || Flat(points[^1] - car).LengthSquared() >= Spacing * Spacing)
            points.Add(car);
        if (points.Count > maxPoints)
            points.RemoveRange(0, points.Count - maxPoints);
    }

    /// <summary>
    /// The point <paramref name="distance"/> metres back along the way from <paramref name="car"/>, and in
    /// <paramref name="forward"/> the direction the car drove there. Past the oldest point the way goes on straight,
    /// and a car that has not driven yet leaves a straight way out of its rear.
    /// </summary>
    internal Vector3 Sample(Vector3 car, Vector3 back, float distance, out Vector3 forward)
    {
        var from = car;
        back.Y = 0;
        var direction = back.LengthSquared() > 1e-6f ? back.Normalized() : Vector3.Back;
        for (int i = points.Count - 1; i >= 0; i--)
        {
            var to = points[i];
            var step = Flat(to - from);
            float length = step.Length();
            if (length < .001f)
                continue;
            direction = step / length;
            if (length >= distance)
            {
                forward = -direction;
                return from + (to - from) * (distance / length);
            }
            distance -= length;
            from = to;
        }
        forward = -direction;
        return from + direction * distance;
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
