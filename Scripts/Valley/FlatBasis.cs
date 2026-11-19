using System;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The rotations and scalings the trees are drawn with, written out in floats like <see cref="FlatMath"/>: a valley
/// draws some two hundred thousand plant parts, and outside an exported game each Godot <see cref="Basis"/> method is an
/// unoptimised call. Each method computes exactly what the Godot method it names computes, in the same order;
/// <c>unit-checks</c> compares them on random input.
/// </summary>
internal static class FlatBasis
{
    /// <summary><c>new Basis(axis, angle)</c> for a normalised axis.</summary>
    public static Basis AxisAngle(Vector3 axis, float angle)
    {
        float xx = axis.X * axis.X,
            yy = axis.Y * axis.Y,
            zz = axis.Z * axis.Z;
        (float sin, float cos) = MathF.SinCos(angle);
        Basis b = default;
        b.Row0.X = xx + cos * (1.0f - xx);
        b.Row1.Y = yy + cos * (1.0f - yy);
        b.Row2.Z = zz + cos * (1.0f - zz);
        float t = 1.0f - cos;
        float xyzt = axis.X * axis.Y * t;
        float zyxs = axis.Z * sin;
        b.Row0.Y = xyzt - zyxs;
        b.Row1.X = xyzt + zyxs;
        xyzt = axis.X * axis.Z * t;
        zyxs = axis.Y * sin;
        b.Row0.Z = xyzt + zyxs;
        b.Row2.X = xyzt - zyxs;
        xyzt = axis.Y * axis.Z * t;
        zyxs = axis.X * sin;
        b.Row1.Z = xyzt - zyxs;
        b.Row2.Y = xyzt + zyxs;
        return b;
    }

    /// <summary><c>left * right</c>.</summary>
    public static Basis Multiply(in Basis left, in Basis right)
    {
        Basis b = default;
        b.Row0 = Row(left.Row0, right);
        b.Row1 = Row(left.Row1, right);
        b.Row2 = Row(left.Row2, right);
        return b;
    }

    // One row of left * right: the row against each column of right.
    private static Vector3 Row(Vector3 row, in Basis right)
    {
        Vector3 v = default;
        v.X = right.Row0.X * row.X + right.Row1.X * row.Y + right.Row2.X * row.Z;
        v.Y = right.Row0.Y * row.X + right.Row1.Y * row.Y + right.Row2.Y * row.Z;
        v.Z = right.Row0.Z * row.X + right.Row1.Z * row.Y + right.Row2.Z * row.Z;
        return v;
    }

    /// <summary><c>basis.Rotated(axis, angle)</c>.</summary>
    public static Basis Rotated(in Basis basis, Vector3 axis, float angle) => Multiply(AxisAngle(axis, angle), basis);

    /// <summary><c>basis.ScaledLocal(scale)</c>: every row times the scale, component by component.</summary>
    public static Basis ScaledLocal(in Basis basis, Vector3 scale)
    {
        Basis b = default;
        b.Row0.X = basis.Row0.X * scale.X;
        b.Row0.Y = basis.Row0.Y * scale.Y;
        b.Row0.Z = basis.Row0.Z * scale.Z;
        b.Row1.X = basis.Row1.X * scale.X;
        b.Row1.Y = basis.Row1.Y * scale.Y;
        b.Row1.Z = basis.Row1.Z * scale.Z;
        b.Row2.X = basis.Row2.X * scale.X;
        b.Row2.Y = basis.Row2.Y * scale.Y;
        b.Row2.Z = basis.Row2.Z * scale.Z;
        return b;
    }

    /// <summary><c>basis * v</c>.</summary>
    public static Vector3 Times(in Basis basis, Vector3 v)
    {
        Vector3 r = default;
        r.X = basis.Row0.X * v.X + basis.Row0.Y * v.Y + basis.Row0.Z * v.Z;
        r.Y = basis.Row1.X * v.X + basis.Row1.Y * v.Y + basis.Row1.Z * v.Z;
        r.Z = basis.Row2.X * v.X + basis.Row2.Y * v.Y + basis.Row2.Z * v.Z;
        return r;
    }
}
