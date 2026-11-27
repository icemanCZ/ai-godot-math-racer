using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

/// <summary>
/// The water of the borders (<see cref="ValleyBorders"/>): the sea in the south out to the horizon, and the border river
/// from its spring in the mountains to its mouth. Both use the river's water, whose vertex colour carries the depth for
/// the shallow colour and the foam along the shore.
/// </summary>
public partial class BorderWater : Node3D
{
    /// <summary>The sea reaches this far, past the edge of the scenery and on to the horizon.</summary>
    private const float Horizon = 3600;

    /// <summary>The fine grid of the sea near the coast, where its depth and foam show.</summary>
    private const float ShoreStep = 20;

    public void Generate(Terrain ground)
    {
        var shader = GD.Load<Shader>("res://Shaders/RiverWater.gdshader");
        var sea = new ShaderMaterial { Shader = shader };
        // The sea barely flows.
        sea.SetShaderParameter("flow_speed", .06f);
        seaWater = AddWater(Sea(ground), sea);
        AddWater(BorderRiver(ground), new ShaderMaterial { Shader = shader });
    }

    private MeshInstance3D? seaWater;

    /// <summary>
    /// The tide (<see cref="Tide"/>): the sea lies <paramref name="offset"/> above its level, which is 0 at high tide and
    /// down to <see cref="Tide.Range"/> below it at low tide. The border river keeps its own level.
    /// </summary>
    internal void SetTide(float offset)
    {
        if (seaWater != null)
            seaWater.Position = new Vector3(0, offset, 0);
    }

    private MeshInstance3D AddWater(Mesh mesh, Material material)
    {
        var water = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(water);
        return water;
    }

    private static Color Depth(float depth) => new(Mathf.Clamp(depth, 0, 3) / 3, 0, 0);

    private static Mesh Sea(Terrain ground)
    {
        var borders = WorldLayout.Plan.Borders;
        float level = ValleyBorders.SeaLevel;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd)
        {
            foreach (var (point, color) in new[] { (a, ca), (b, cb), (c, cc), (b, cb), (d, cd), (c, cc) })
            {
                surface.SetColor(color);
                surface.SetNormal(Vector3.Up);
                surface.AddVertex(point);
            }
        }
        // Near the coast a fine grid follows the depth of the ground.
        float edge = WorldTuning.SceneryRadius,
            from = Mathf.Floor((borders.CoastMinimum - 80) / ShoreStep) * ShoreStep;
        int columns = Mathf.RoundToInt(edge * 2 / ShoreStep),
            rows = Mathf.RoundToInt((edge - from) / ShoreStep);
        var depths = new Color[columns + 1, rows + 1];
        for (int row = 0; row <= rows; row++)
        for (int column = 0; column <= columns; column++)
        {
            float x = -edge + column * ShoreStep,
                z = from + row * ShoreStep;
            depths[column, row] = Depth(level - ground.Height(x, z));
        }
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            float x = -edge + column * ShoreStep,
                z = from + row * ShoreStep;
            Quad(
                new(x, level, z),
                new(x + ShoreStep, level, z),
                new(x, level, z + ShoreStep),
                new(x + ShoreStep, level, z + ShoreStep),
                depths[column, row],
                depths[column + 1, row],
                depths[column, row + 1],
                depths[column + 1, row + 1]
            );
        }
        // Beyond the scenery the sea is deep, in a few large pieces.
        var deep = Depth(3);
        void Plain(float x0, float z0, float x1, float z1) =>
            Quad(
                new(x0, level, z0),
                new(x1, level, z0),
                new(x0, level, z1),
                new(x1, level, z1),
                deep,
                deep,
                deep,
                deep
            );
        Plain(-Horizon, edge, Horizon, Horizon);
        Plain(-Horizon, from, -edge, edge);
        Plain(edge, from, Horizon, edge);
        return surface.Commit();
    }

    private static Mesh BorderRiver(Terrain ground)
    {
        var borders = WorldLayout.Plan.Borders;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const int strips = 12;
        const float step = 2;
        float half = ValleyBorders.BorderRiverHalfWidth + 3;
        float start = ValleyBorders.SpringZ - 140,
            end = borders.CoastMaximum + 40;
        int rows = Mathf.CeilToInt((end - start) / step);
        // Each point once: beyond the fine outland the height is worked out from the landscape itself.
        var points = new Vector3[rows + 1, strips + 1];
        var colors = new Color[rows + 1, strips + 1];
        for (int row = 0; row <= rows; row++)
        for (int strip = 0; strip <= strips; strip++)
        {
            float z = start + row * step,
                across = -1 + strip * 2f / strips;
            var point = new Vector3(borders.BorderRiverX(z) + across * half, ValleyBorders.BorderRiverLevel(z), z);
            points[row, strip] = point;
            colors[row, strip] = Depth(point.Y - ground.Height(point.X, point.Z));
        }
        void Add(int row, int strip)
        {
            surface.SetColor(colors[row, strip]);
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(points[row, strip]);
        }
        for (int row = 0; row < rows; row++)
        for (int strip = 0; strip < strips; strip++)
        {
            Add(row, strip);
            Add(row, strip + 1);
            Add(row + 1, strip);
            Add(row, strip + 1);
            Add(row + 1, strip + 1);
            Add(row + 1, strip);
        }
        return surface.Commit();
    }
}
