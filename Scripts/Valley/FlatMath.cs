using System;

namespace MathRacer.Valley;

/// <summary>
/// Plain-float forms of the few Godot vector and Mathf operations that the world searches repeat hundreds of thousands
/// of times. Outside an exported game Godot's own methods run unoptimised, one call each, which made the planning of a
/// valley several times slower. Each method computes exactly what the Godot method computes, in the same order, so
/// the valley comes out the same, bit for bit; <c>unit-checks</c> compares them on random input.
/// </summary>
internal static class FlatMath
{
    /// <summary><c>new Vector2(x, z).Length()</c>.</summary>
    public static float Length(float x, float z) => MathF.Sqrt(x * x + z * z);

    /// <summary><c>Mathf.Lerp(from, to, weight)</c>.</summary>
    public static float Lerp(float from, float to, float weight) => from + (to - from) * weight;

    /// <summary><c>Mathf.Clamp(value, min, max)</c>.</summary>
    public static float Clamp(float value, float min, float max) =>
        value < min ? min
        : value > max ? max
        : value;

    /// <summary><c>a.Dot(b)</c> for two Vector3.</summary>
    public static float Dot(float ax, float ay, float az, float bx, float by, float bz) => ax * bx + ay * by + az * bz;

    /// <summary>
    /// Whether <paramref name="p"/> lies on a site's ground, the rectangle from <paramref name="alongMin"/> to
    /// <paramref name="alongMax"/> along <paramref name="inward"/> and within <paramref name="acrossMax"/> across it, as
    /// <c>offset.Dot(inward)</c> and <c>offset.Dot(Vector3.Up.Cross(inward))</c> give it for the flat offset from
    /// <paramref name="origin"/>.
    /// </summary>
    public static bool InRectangle(
        float px,
        float pz,
        float originX,
        float originZ,
        float inwardX,
        float inwardY,
        float inwardZ,
        float alongMin,
        float alongMax,
        float acrossMax
    )
    {
        float ox = px - originX,
            oz = pz - originZ;
        float along = Dot(ox, 0, oz, inwardX, inwardY, inwardZ);
        // Vector3.Up.Cross(inward), term by term.
        float rightX = 1f * inwardZ - 0f * inwardY,
            rightY = 0f * inwardX - 0f * inwardZ,
            rightZ = 0f * inwardY - 1f * inwardX;
        float across = Dot(ox, 0, oz, rightX, rightY, rightZ);
        return along >= alongMin && along <= alongMax && MathF.Abs(across) <= acrossMax;
    }

    /// <summary>
    /// <c>p.DistanceTo(a + (b - a) * t)</c>, with <c>t = Mathf.Clamp((p - a).Dot(b - a) / (b - a).LengthSquared(), 0, 1)</c>:
    /// the flat distance from (px, pz) to the segment from (ax, az) to (bx, bz).
    /// </summary>
    public static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
    {
        float dx = bx - ax,
            dz = bz - az;
        float t = Clamp(((px - ax) * dx + (pz - az) * dz) / (dx * dx + dz * dz), 0, 1);
        return Length(px - (ax + dx * t), pz - (az + dz * t));
    }
}
