using System;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Buildings;

/// <summary>
/// The fireworks over the <see cref="Castle"/> on the nights after the feast: three bursts that are sent up one after
/// another at random places above the keep, each in a colour of its own.
/// </summary>
internal sealed class CastleFireworks
{
    private static readonly string[] FireworkColors = { "ff5a4a", "ffd23f", "5ad1ff", "9cf06a", "ff7ad8" };

    private readonly Burst[] bursts = new Burst[3];
    private readonly RandomNumberGenerator sparkle = new() { Seed = 4711 };
    private float nextBurst = 1;

    internal CastleFireworks(Node3D parent)
    {
        for (int i = 0; i < bursts.Length; i++)
            bursts[i] = new Burst(parent);
    }

    /// <summary>Whether a burst is in the sky now.</summary>
    internal bool Bursting => Array.Exists(bursts, burst => burst.Active);

    /// <summary>Moves the bursts in the sky and, while <paramref name="on"/>, sends up the next one above the keep.</summary>
    internal void Tick(float delta, bool on, float keepTop)
    {
        foreach (var burst in bursts)
            burst.Tick(delta);
        if (!on)
            return;
        nextBurst -= delta;
        if (nextBurst > 0)
            return;
        nextBurst = 1.1f + sparkle.Randf() * 1.4f;
        foreach (var burst in bursts)
            if (!burst.Active)
            {
                var at = new Vector3(
                    sparkle.RandfRange(-14, 14),
                    keepTop + 10 + sparkle.RandfRange(0, 12),
                    sparkle.RandfRange(-12, 14)
                );
                burst.Start(at, FireworkColors[sparkle.RandiRange(0, FireworkColors.Length - 1)]);
                break;
            }
    }

    /// <summary>One burst of the fireworks: fourteen sparks that fly out from a point, sink a little and fade.</summary>
    private sealed class Burst
    {
        private const int Sparks = 14;
        private const float Life = 1.7f;
        private readonly Node3D root = new() { Name = "Burst", Visible = false };
        private readonly MeshInstance3D[] sparks = new MeshInstance3D[Sparks];
        private readonly Vector3[] directions = new Vector3[Sparks];
        private readonly StandardMaterial3D material = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        private float age = -1;

        internal bool Active => age >= 0;

        internal Burst(Node3D parent)
        {
            parent.AddChild(root);
            for (int i = 0; i < Sparks; i++)
            {
                // Spread evenly over a sphere, by the golden angle.
                float y = 1 - 2 * (i + .5f) / Sparks;
                float r = Mathf.Sqrt(1 - y * y);
                float a = i * 2.39996f;
                directions[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                sparks[i] = Art.Ball(root, Vector3.Zero, Vector3.One * .45f, "ffffff");
                sparks[i].MaterialOverride = material;
                sparks[i].CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            }
        }

        internal void Start(Vector3 at, string color)
        {
            root.Position = at;
            root.Visible = true;
            material.AlbedoColor = new Color(color);
            age = 0;
        }

        internal void Tick(float delta)
        {
            if (age < 0)
                return;
            age += delta;
            if (age >= Life)
            {
                age = -1;
                root.Visible = false;
                return;
            }
            float t = age / Life;
            float reach = 7 * (1 - (1 - t) * (1 - t));
            for (int i = 0; i < Sparks; i++)
                sparks[i].Position = directions[i] * reach + Vector3.Down * (2.5f * t * t);
            material.AlbedoColor = material.AlbedoColor with { A = 1 - t * t };
        }
    }
}
