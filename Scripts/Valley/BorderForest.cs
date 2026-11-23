using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

/// <summary>
/// The trees of the borders (<see cref="ValleyBorders.ForestDensity"/>): the deep forest on its side, and thinner
/// groves on the hills beyond the border river and in the foothills. They stand beyond the edge of the valley, where no
/// car comes, so they are simple shapes in a few large batches without collision, and they have random numbers of their
/// own: the plants of the old valley (<see cref="Vegetation"/>) know nothing of them.
/// </summary>
public partial class BorderForest : Node3D
{
    /// <summary>The trees stand on a jittered grid of this spacing, where the density lets them.</summary>
    private const float Spacing = 7.5f;

    /// <summary>The trees are batched by squares of this size, so that the batches behind the camera are left out.</summary>
    private const float BlockSize = 320;

    /// <summary>Beyond this distance from the camera a batch is drawn as simple shapes: crowns of a few faces, no trunks.</summary>
    private const float DetailRange = 300;

    /// <summary>A tree stands this much lower than the ground at its middle, for the coarse ground of the scenery.</summary>
    private const float Sink = .6f;

    internal int TreeCount { get; private set; }
    private readonly List<Vector3> feet = new();

    /// <summary>Where the trees stand, for the checks.</summary>
    internal IReadOnlyList<Vector3> Feet => feet;

    public void Generate(Terrain ground)
    {
        var borders = WorldLayout.Plan.Borders;
        var random = new Random(Settings.Seed + 7720);
        var blocks = new Dictionary<(Vector2I Block, bool Conifer), List<Transform3D>>();
        var tints = new Dictionary<(Vector2I Block, bool Conifer), List<Color>>();
        float edge = WorldTuning.SceneryRadius - 4;
        for (float z = -edge; z <= edge; z += Spacing)
        for (float x = -edge; x <= edge; x += Spacing)
        {
            if (Mathf.Abs(x) < ValleyBorders.Untouched && Mathf.Abs(z) < ValleyBorders.Untouched)
                continue;
            float px = x + ((float)random.NextDouble() - .5f) * Spacing * .9f,
                pz = z + ((float)random.NextDouble() - .5f) * Spacing * .9f;
            float chance = (float)random.NextDouble();
            float rough = borders.ForestDensity(px, pz, 50);
            if (rough <= 0 || chance > rough)
                continue;
            float height = ground.Height(px, pz);
            if (chance > borders.ForestDensity(px, pz, height))
                continue;
            // Conifers higher up and in the north, broad-leaved trees in the lowlands.
            bool conifer = random.NextDouble() < .35f + Mathf.SmoothStep(20, 80, height) * .6f;
            float size = .8f + (float)random.NextDouble() * .7f;
            var foot = new Vector3(px, height - Sink, pz);
            var basis = Basis
                .FromEuler(new Vector3(0, (float)random.NextDouble() * Mathf.Tau, 0))
                .Scaled(Vector3.One * size);
            var key = (new Vector2I(Mathf.FloorToInt(px / BlockSize), Mathf.FloorToInt(pz / BlockSize)), conifer);
            if (!blocks.TryGetValue(key, out var list))
            {
                blocks[key] = list = new();
                tints[key] = new();
            }
            list.Add(new Transform3D(basis, foot));
            float shade = .82f + (float)random.NextDouble() * .3f;
            tints[key].Add(new Color(shade, shade * (.95f + (float)random.NextDouble() * .1f), shade));
            feet.Add(foot);
        }
        TreeCount = feet.Count;
        var material = TreeMaterial();
        var near = new Dictionary<bool, Mesh> { [true] = Conifer(), [false] = Broadleaf() };
        var far = new Dictionary<bool, Mesh> { [true] = DistantConifer(), [false] = DistantBroadleaf() };
        foreach (var (key, transforms) in blocks)
        {
            MultiMesh Batch(Mesh mesh)
            {
                var multimesh = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    UseColors = true,
                    Mesh = mesh,
                    InstanceCount = transforms.Count,
                };
                for (int i = 0; i < transforms.Count; i++)
                {
                    multimesh.SetInstanceTransform(i, transforms[i]);
                    multimesh.SetInstanceColor(i, tints[key][i]);
                }
                return multimesh;
            }
            AddChild(
                new MultiMeshInstance3D
                {
                    Name = "Forest",
                    Multimesh = Batch(near[key.Conifer]),
                    MaterialOverride = material,
                    VisibilityRangeEnd = DetailRange,
                    VisibilityRangeEndMargin = 20,
                }
            );
            AddChild(
                new MultiMeshInstance3D
                {
                    Name = "DistantForest",
                    Multimesh = Batch(far[key.Conifer]),
                    MaterialOverride = material,
                    VisibilityRangeBegin = DetailRange,
                    VisibilityRangeBeginMargin = 20,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
        }
        GD.Print($"BORDER_FOREST_READY trees={TreeCount} batches={blocks.Count}");
    }

    /// <summary>The material of these trees: the colours of their meshes, tinted per tree.</summary>
    internal static StandardMaterial3D TreeMaterial() =>
        new()
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 1,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

    private static Mesh Conifer()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Cone(surface, 0, 2.2f, .28f, .2f, 6, new Color("6b4a32"));
        Cone(surface, 1.6f, 6.2f, 2.6f, 0, 8, new Color("2f6b45"));
        Cone(surface, 4.6f, 9.4f, 1.9f, 0, 8, new Color("3a7a4f"));
        surface.GenerateNormals();
        return surface.Commit();
    }

    private static Mesh Broadleaf()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Cone(surface, 0, 3.4f, .35f, .25f, 6, new Color("71503a"));
        Ball(surface, new Vector3(0, 5.2f, 0), new Vector3(3, 2.6f, 3), new Color("4f9a4c"));
        Ball(surface, new Vector3(.9f, 6.4f, .4f), new Vector3(1.9f, 1.7f, 1.9f), new Color("5fae57"));
        surface.GenerateNormals();
        return surface.Commit();
    }

    /// <summary>A conifer far away: one cone of five faces, as tall and wide as the near one.</summary>
    internal static Mesh DistantConifer()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Cone(surface, 1.2f, 9.4f, 2.5f, 0, 5, new Color("33714a"));
        surface.GenerateNormals();
        return surface.Commit();
    }

    /// <summary>A broad-leaved tree far away: one crown of a few faces.</summary>
    internal static Mesh DistantBroadleaf()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Ball(surface, new Vector3(.3f, 5.5f, .1f), new Vector3(3.2f, 3, 3.2f), new Color("55a250"), 5, 3);
        surface.GenerateNormals();
        return surface.Commit();
    }

    /// <summary>A frustum (a cone when <paramref name="top"/> is 0) from <paramref name="bottomY"/> to <paramref name="topY"/>.</summary>
    private static void Cone(
        SurfaceTool surface,
        float bottomY,
        float topY,
        float bottom,
        float top,
        int sides,
        Color color
    )
    {
        surface.SetColor(color);
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.Tau / sides,
                a1 = (i + 1) * Mathf.Tau / sides;
            Vector3 Ring(float angle, float radius, float y) =>
                new(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            Vector3 b0 = Ring(a0, bottom, bottomY),
                b1 = Ring(a1, bottom, bottomY),
                t0 = Ring(a0, top, topY),
                t1 = Ring(a1, top, topY);
            surface.AddVertex(b0);
            surface.AddVertex(t0);
            surface.AddVertex(b1);
            surface.AddVertex(b1);
            surface.AddVertex(t0);
            surface.AddVertex(t1);
        }
    }

    /// <summary>An ellipsoid of <paramref name="sides"/> sides and <paramref name="rings"/> rings.</summary>
    private static void Ball(
        SurfaceTool surface,
        Vector3 center,
        Vector3 radius,
        Color color,
        int sides = 8,
        int rings = 4
    )
    {
        surface.SetColor(color);
        Vector3 Point(int ring, int side)
        {
            float polar = ring * Mathf.Pi / rings,
                around = side * Mathf.Tau / sides;
            return center
                + new Vector3(
                    Mathf.Sin(polar) * Mathf.Cos(around) * radius.X,
                    Mathf.Cos(polar) * radius.Y,
                    Mathf.Sin(polar) * Mathf.Sin(around) * radius.Z
                );
        }
        for (int ring = 0; ring < rings; ring++)
        for (int side = 0; side < sides; side++)
        {
            Vector3 a = Point(ring, side),
                b = Point(ring, side + 1),
                c = Point(ring + 1, side),
                d = Point(ring + 1, side + 1);
            surface.AddVertex(a);
            surface.AddVertex(b);
            surface.AddVertex(c);
            surface.AddVertex(b);
            surface.AddVertex(d);
            surface.AddVertex(c);
        }
    }
}
