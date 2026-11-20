using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The gravel verge along the drivable roads: a band that runs from the edge of a road out over the meadow, eases down
/// from the road's height to the ground and fades from packed gravel through scattered pebbles into the grass
/// (<c>RoadVerge.gdshader</c>). It is drawn only: it is no part of the drivable surface, so driving, placement and the
/// world fingerprints do not see it. The road shader draws the same gravel on its own last metre, so the strip runs on
/// across the edge without a seam.
/// </summary>
internal static class Verges
{
    /// <summary>How far the verge reaches out from the edge of a road.</summary>
    internal const float Width = 1.3f;

    /// <summary>How deep into a road the road shader is told the distance to its edge (the blue of its vertex colour).</summary>
    internal const float EdgeDepth = 1.2f;

    /// <summary>The verge's outer rim lies this high over the ground; its inner rim is at the road's own height.</summary>
    private const float OuterLift = .035f;

    /// <summary>The colour that tells the road shader how far a vertex is from the road's edge.</summary>
    internal static float Depth(float distance) => Mathf.Clamp(-distance / EdgeDepth, 0, 1);

    /// <summary>
    /// A corner of the mesh grid: <see cref="Distance"/> is the signed distance to the road's edge (negative on the
    /// road) and the verge is kept only where <see cref="Keep"/> is not negative (off the bridge's deck). Where a
    /// field way begins, <see cref="Fade"/> falls from 1 to 0 and the gravel dissolves into it instead of being cut off.
    /// </summary>
    internal readonly record struct Corner(Vector2 Position, float Distance, float Keep, float Fade = 1);

    /// <summary>How the verge fades out at <paramref name="distance"/> from a field way (negative on it).</summary>
    internal static float FadeNear(float distance) => Mathf.SmoothStep(-.15f, 1.1f, distance);

    /// <summary>The verge goes on this far over a field way, where it has faded out.</summary>
    internal const float FadeReach = .2f;

    internal static ShaderMaterial CreateMaterial() =>
        new() { Shader = GD.Load<Shader>("res://Shaders/RoadVerge.gdshader") };

    /// <summary>Whether a triangle of the grid can hold any verge at all; most of the grid holds none.</summary>
    internal static bool Touches(Corner a, Corner b, Corner c) =>
        !(a.Distance < 0 && b.Distance < 0 && c.Distance < 0)
        && !(a.Distance > Width && b.Distance > Width && c.Distance > Width)
        && !(a.Keep < 0 && b.Keep < 0 && c.Keep < 0);

    /// <summary>
    /// Adds the part of a grid triangle that lies in the verge, draped over the terrain from <paramref name="roadLift"/>
    /// at the road's edge down to <see cref="OuterLift"/> at its rim. Returns how many triangles it added.
    /// </summary>
    internal static int Emit(SurfaceTool surface, Terrain terrain, float roadLift, Corner a, Corner b, Corner c)
    {
        if (!Touches(a, b, c))
            return 0;
        var polygon = new List<Corner>(6) { a, b, c };
        polygon = Clip(polygon, corner => corner.Distance);
        polygon = Clip(polygon, corner => Width - corner.Distance);
        polygon = Clip(polygon, corner => corner.Keep);
        int count = 0;
        for (int i = 1; i + 1 < polygon.Count; i++)
        {
            Corner p = polygon[0],
                q = polygon[i],
                r = polygon[i + 1];
            if (Mathf.Abs((q.Position - p.Position).Cross(r.Position - p.Position)) < .000001f)
                continue;
            Add(p);
            Add(q);
            Add(r);
            count++;
        }
        return count;

        void Add(Corner corner)
        {
            float across = Mathf.Clamp(corner.Distance / Width, 0, 1);
            // A rounded shoulder: level with the road at first, then easing down to the meadow.
            float lift = Mathf.Lerp(roadLift, OuterLift, Mathf.SmoothStep(0, .7f, across));
            surface.SetColor(new Color(across, Mathf.Clamp(corner.Fade, 0, 1), 0));
            surface.AddVertex(terrain.At(corner.Position.X, corner.Position.Y, lift));
        }
    }

    /// <summary>Keeps the part of a convex polygon where <paramref name="value"/> is not negative.</summary>
    private static List<Corner> Clip(List<Corner> polygon, System.Func<Corner, float> value)
    {
        if (polygon.Count == 0)
            return polygon;
        var result = new List<Corner>(polygon.Count + 1);
        for (int i = 0; i < polygon.Count; i++)
        {
            var p = polygon[i];
            var q = polygon[(i + 1) % polygon.Count];
            float vp = value(p),
                vq = value(q);
            if (vp >= 0)
                result.Add(p);
            if ((vp < 0) != (vq < 0))
            {
                float t = vp / (vp - vq);
                result.Add(
                    new Corner(
                        p.Position.Lerp(q.Position, t),
                        Mathf.Lerp(p.Distance, q.Distance, t),
                        Mathf.Lerp(p.Keep, q.Keep, t),
                        Mathf.Lerp(p.Fade, q.Fade, t)
                    )
                );
            }
        }
        return result;
    }
}
