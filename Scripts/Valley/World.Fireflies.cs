using System;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

/// <summary>Where the fireflies of the witch's night hunt hover: anywhere on open ground near the car.</summary>
public partial class World
{
    /// <summary>The lantern and the catchable fireflies; nothing is drawn between hunts.</summary>
    internal FireflyField Sparks { get; } = new();

    private void CreateFireflies()
    {
        Sparks.FindSpot = FireflySpot;
        AddChild(Sparks);
    }

    /// <summary>
    /// A place on the ground 14 to 38 m from <paramref name="around"/> where a firefly can hover and the car can come to it:
    /// inside the playable ground, off the water, clear of the houses, the plants and the sites that are built. Roads are
    /// fine, and so are the characters. Null when none was found in thirty tries.
    /// </summary>
    internal Vector3? FireflySpot(Vector3 around, Random random)
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float turn = random.NextSingle() * Mathf.Tau;
            float distance = 14 + random.NextSingle() * 24;
            float x = around.X + Mathf.Sin(turn) * distance;
            float z = around.Z + Mathf.Cos(turn) * distance;
            var p = Ground.At(x, z);
            if (IsFireflySpotFree(p))
                return p;
        }
        return null;
    }

    internal bool IsFireflySpotFree(Vector3 p) =>
        new Vector2(p.X, p.Z).Length() <= Settings.PlayableRadius - 14
        && !Village.IsNearVillage(p)
        && !Village.IsReserved(p)
        && River.Distance(p.X, p.Z) >= River.HalfWidth(p.Z) + 6
        && !Plants.NearTrunk(p, 1.8f)
        && !InGlassworksZone(p)
        && !InFarmZone(p)
        && !InBakeryZone(p)
        && !InPlaygroundZone(p)
        && !InZooZone(p)
        && !InHunterZone(p)
        && FlatDistance(p, Tower.Position) >= 18;
}
