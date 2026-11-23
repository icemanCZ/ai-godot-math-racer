using System;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The four natural borders round the valley (<c>docs/MAP_EXPANSION_PLAN.md</c>): mountains in the north (−z), where both
/// rivers rise; the sea in the south (+z), into which they flow; a wide border river on the side of the valley's own
/// river (<see cref="WorldPlan.Side"/>), with meadows and hills beyond it; and wooded hills on the other side. Every
/// shape is exactly zero nearer the middle than <see cref="Untouched"/>, so the old valley keeps its ground to the bit.
/// </summary>
public sealed class ValleyBorders
{
    /// <summary>Nothing changes nearer the middle than this along either axis.</summary>
    public const float Untouched = 700;

    /// <summary>The level of the sea, the same as the river's where it reaches the coast.</summary>
    public const float SeaLevel = -10;

    /// <summary>How far the coast lies from the middle on average; its bays and headlands wave 61 m nearer and further.</summary>
    private const float CoastMiddle = 1210;
    private const float CoastWaves = 61;

    /// <summary>The nearest and the furthest the waterline of the sea comes to the middle.</summary>
    public float CoastMinimum => CoastMiddle - CoastWaves;
    public float CoastMaximum => CoastMiddle + CoastWaves;

    /// <summary>The beach runs this far inland from the waterline on average; it is narrower by the headlands.</summary>
    private const float BeachWidth = 45;

    /// <summary>The middle of the border river, before its bends (56 m either way), and half its width.</summary>
    private const float BorderRiverMiddle = 1215;
    public const float BorderRiverHalfWidth = 24;

    /// <summary>Where the forest begins on average, before its edge waves 37 m in and out.</summary>
    private const float ForestEdgeMiddle = 1175;

    /// <summary>No tree of the borders stands nearer than this to the edge of the valley.</summary>
    private const float TreeClearance = 6;

    /// <summary>The rivers rise in the mountains here: north of it their beds close up into the slope.</summary>
    public const float SpringZ = -1260;
    private const float SpringLength = 140;

    private readonly int side;
    private readonly float coastPhase;
    private readonly float borderRiverPhase;
    private readonly float forestPhase;
    private readonly float footPhase;
    private readonly FastNoiseLite ridges;
    private readonly FastNoiseLite hills;

    internal ValleyBorders(int seed, int side)
    {
        this.side = side;
        var random = new Random(seed + 7717);
        coastPhase = (float)random.NextDouble() * Mathf.Tau;
        borderRiverPhase = (float)random.NextDouble() * Mathf.Tau;
        forestPhase = (float)random.NextDouble() * Mathf.Tau;
        footPhase = (float)random.NextDouble() * Mathf.Tau;
        ridges = new FastNoiseLite
        {
            Seed = seed + 7718,
            Frequency = .0042f,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            FractalOctaves = 4,
        };
        hills = new FastNoiseLite
        {
            Seed = seed + 7719,
            Frequency = .006f,
            FractalOctaves = 3,
        };
    }

    /// <summary>Distance from the middle towards the border river (positive on its side).</summary>
    private float RiverSide(float x) => side * x;

    /// <summary>
    /// Where the waterline of the sea lies at <paramref name="x"/>: wide bays and headlands, smaller coves on them. The
    /// waves are bounded, so that the water never comes nearer than <see cref="CoastMinimum"/>.
    /// </summary>
    public float Coast(float x) =>
        CoastMiddle
        + 38 * Mathf.Sin(x * .0037f + coastPhase)
        + 16 * Mathf.Sin(x * .0093f - coastPhase * 1.3f)
        + 7 * Mathf.Sin(x * .023f + coastPhase * 2.1f);

    /// <summary>How wide the beach is at <paramref name="x"/>: wide in the bays, narrow under the headlands.</summary>
    private float BeachWidthAt(float x) => BeachWidth + .35f * (Coast(x) - CoastMiddle);

    /// <summary>The middle of the border river at <paramref name="z"/>, as a distance towards its side: it meanders.</summary>
    private float BorderRiverAt(float z) =>
        BorderRiverMiddle
        + 40 * Mathf.Sin(z * .0042f + borderRiverPhase)
        + 16 * Mathf.Sin(z * .0107f - borderRiverPhase * 1.7f);

    /// <summary>Where the forest begins at <paramref name="z"/>, as a distance towards its side.</summary>
    private float ForestEdge(float z) =>
        ForestEdgeMiddle + 25 * Mathf.Sin(z * .0063f + forestPhase) + 12 * Mathf.Sin(z * .017f - forestPhase);

    /// <summary>
    /// How much nearer (negative, at most 30 m, so that their foot stays drivable at the edge of the valley) or further
    /// (up to 80 m) the mountains rise at <paramref name="x"/>.
    /// </summary>
    private float FootShift(float x) =>
        25 + 38 * Mathf.Sin(x * .0047f + footPhase) + 17 * Mathf.Sin(x * .013f - footPhase);

    /// <summary>The x of the middle of the border river at <paramref name="z"/>.</summary>
    public float BorderRiverX(float z) => side * BorderRiverAt(z);

    /// <summary>The water of the border river runs as the valley's river does, down to the sea.</summary>
    public static float BorderRiverLevel(float z) => River.Level(z);

    public float BorderRiverDistance(float x, float z) => Mathf.Abs(RiverSide(x) - BorderRiverAt(z));

    /// <summary>How fully a river is cut at <paramref name="z"/>: 1 below its spring, 0 up in the mountains.</summary>
    public static float RiverBedWeight(float z) =>
        z >= SpringZ ? 1 : 1 - Mathf.SmoothStep(SpringZ, SpringZ - SpringLength, z);

    /// <summary>The ground of the borders on top of the natural <paramref name="height"/>.</summary>
    public float Shape(float x, float z, float height)
    {
        if (Mathf.Abs(x) < Untouched && Mathf.Abs(z) < Untouched)
            return height;
        float riverSide = RiverSide(x);
        // Mountains in the north, high and ridged, rising from foothills.
        float north = -z;
        if (north > Untouched)
        {
            float shift = FootShift(x);
            float foot = Mathf.SmoothStep(930 + shift, 1420 + shift, north);
            float ridge = (ridges.GetNoise2D(x, z) + 1) * .5f;
            // Far to either side, towards the horizon, the range sinks into hills instead of ending against the sky.
            float range = 1 - .8f * Mathf.SmoothStep(1900, 2900, Mathf.Abs(x));
            height += foot * foot * (110 + 95 * ridge) * range + Mathf.SmoothStep(Untouched, 1000, north) * 6 * ridge;
        }
        // Wooded hills on the forest side, and hills beyond the border river.
        float rolling = (hills.GetNoise2D(x, z) + 1) * .5f;
        if (-riverSide > Untouched)
        {
            float edge = ForestEdge(z);
            height += Mathf.SmoothStep(edge - 95, edge + 420, -riverSide) * (22 + 30 * rolling);
        }
        float beyond = riverSide - BorderRiverAt(z);
        if (beyond > 0)
            height += Mathf.SmoothStep(40, 420, beyond) * (18 + 34 * rolling);
        // The land falls to a beach and under the sea.
        if (z > Untouched)
        {
            float shore = z - Coast(x),
                beach = BeachWidthAt(x);
            float weight = Mathf.SmoothStep(-260, -beach, shore);
            float ground =
                shore < 0
                    ? SeaLevel + 1.6f * Mathf.Min(1, -shore / beach)
                    : SeaLevel - 9 * Mathf.SmoothStep(0, 160, shore);
            height = Mathf.Lerp(height, ground, weight);
        }
        // The border river cuts its valley from the mountains down into the sea.
        if (riverSide > Untouched)
            height = BorderRiverBed(riverSide, z, height);
        return height;
    }

    /// <summary>A wide floodplain like the valley's river (<see cref="River.ShapeTerrain"/>), with a broader channel.</summary>
    private float BorderRiverBed(float riverSide, float z, float natural)
    {
        float distance = Mathf.Abs(riverSide - BorderRiverAt(z));
        float width = BorderRiverHalfWidth,
            level = BorderRiverLevel(z);
        float slopeDistance = Mathf.Max(0, distance - width - 8);
        float ceiling = level + .8f + slopeDistance * .1f + slopeDistance * slopeDistance * .0006f;
        float capped = Mathf.Min(natural, ceiling);
        float bank = Mathf.Max(capped, level + .8f);
        float shaped = capped;
        if (distance < width + 8)
            shaped = Mathf.Lerp(level - 1.8f, bank, Mathf.SmoothStep(0, width + 8, distance));
        else if (distance < width + 20)
            shaped = Mathf.Lerp(bank, capped, Mathf.SmoothStep(width + 8, width + 20, distance));
        // A river only cuts: where the land is lower than its banks, by the sea, it raises no dykes.
        return Mathf.Lerp(natural, Mathf.Min(shaped, natural), RiverBedWeight(z));
    }

    /// <summary>How sandy the ground is: 1 on the beach and under the sea, 0 inland.</summary>
    public float Sand(float x, float z)
    {
        if (z < Untouched)
            return 0;
        // The line between the beach and the grass is not drawn with a ruler.
        float beach = BeachWidthAt(x) + 6 * Mathf.Sin(x * .061f + coastPhase) + 3 * Mathf.Sin(x * .19f);
        return Mathf.SmoothStep(-beach - 12, -beach + 6, z - Coast(x));
    }

    /// <summary>Whether the grass of the meadows grows here, which it does not on the beach.</summary>
    public bool IsSand(float x, float z) => Sand(x, z) > .5f;

    /// <summary>
    /// Whether a tree of the border forest may stand here: in the forest on its side, thinly in the foothills and on the
    /// hills beyond the border river, never on the beach, in water or high in the mountains.
    /// </summary>
    public float ForestDensity(float x, float z, float height)
    {
        // The trees have no collision, so they keep their crowns out of the valley that is driven in.
        if (ValleyBounds.Contains(x, z, -TreeClearance))
            return 0;
        float riverSide = RiverSide(x);
        if (z > Untouched && z - Coast(x) > -BeachWidthAt(x) - 25)
            return 0;
        if (height > 105 || height < SeaLevel + 2)
            return 0;
        float density = 0;
        // The forest thickens over 45 m from its edge, and a few lone trees stand in front of it.
        float edge = ForestEdge(z);
        if (-riverSide > edge - 30)
            density = Mathf.Max(.06f, Mathf.SmoothStep(edge, edge + 45, -riverSide));
        if (riverSide - BorderRiverAt(z) > BorderRiverHalfWidth + 30)
            density = Mathf.Max(density, .22f);
        if (-z > 1150 + FootShift(x) * .6f)
            density = Mathf.Max(density, .3f * (1 - Mathf.SmoothStep(70, 105, height)));
        return density;
    }

    /// <summary>The colour of the ground of the borders over the meadow's <paramref name="meadow"/> colour (linear).</summary>
    public Color Tint(float x, float z, float height, float upright, Color meadow)
    {
        if (Mathf.Abs(x) < Untouched && Mathf.Abs(z) < Untouched)
            return meadow;
        var color = meadow;
        float rolling = (hills.GetNoise2D(x * 2.3f, z * 2.3f) + 1) * .5f;
        float edge = ForestEdge(z);
        if (-RiverSide(x) > edge - 95)
            color = color.Lerp(ForestFloor, Mathf.SmoothStep(edge - 95, edge + 5, -RiverSide(x)) * .75f);
        // Bare rock high in the mountains and on every steep face; the hills of the other sides stay green.
        float rock = Mathf.Max(
            Mathf.SmoothStep(55, 95, height + rolling * 20) * Mathf.SmoothStep(930, 1100, -z - FootShift(x)),
            Mathf.SmoothStep(.86f, .7f, upright) * Mathf.SmoothStep(20, 45, height)
        );
        color = color.Lerp(Rock.Lerp(RockDark, rolling), rock);
        color = color.Lerp(Snow, Mathf.SmoothStep(150, 178, height + rolling * 25));
        return color.Lerp(Beach, Sand(x, z));
    }

    private static readonly Color ForestFloor = new Color("4f7d47").SrgbToLinear();
    private static readonly Color Rock = new Color("9b9790").SrgbToLinear();
    private static readonly Color RockDark = new Color("7c776f").SrgbToLinear();
    private static readonly Color Snow = new Color("f3f6fa").SrgbToLinear();
    private static readonly Color Beach = new Color("e3cf98").SrgbToLinear();
}
