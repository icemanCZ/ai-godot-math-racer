using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

public partial class Terrain : Node3D
{
    private readonly FastNoiseLite noise = new()
    {
        Seed = Settings.Seed,
        Frequency = .013f,
        FractalOctaves = 3,
    };
    private readonly RoadGrading roadbed = new(WorldLayout.Plan);
    private float[,]? heights;
    private Rect2? villageArea;
    private float villageHeight;
    private readonly ShaderMaterial meadowMaterial = new()
    {
        Shader = GD.Load<Shader>("res://Shaders/MeadowGround.gdshader"),
    };

    /// <summary>
    /// A straight cut or fill that gives a way a gentle, even slope: the ground is pulled to the height that runs
    /// from <see cref="HeightA"/> at <see cref="A"/> to <see cref="HeightB"/> at <see cref="B"/> within
    /// <see cref="HalfWidth"/> and eases back to the natural ground over <see cref="Shoulder"/>. With
    /// <see cref="A"/> equal to <see cref="B"/> it is a level round platform.
    /// </summary>
    internal readonly record struct GradedPiece(
        Vector2 A,
        Vector2 B,
        float HeightA,
        float HeightB,
        float HalfWidth,
        float Shoulder
    );

    private readonly List<GradedPiece> graded = new();
    private Func<float, float, float>? roadDistance;

    /// <summary>Graded pieces leave the ground this close to a road alone.</summary>
    private const float RoadMargin = 7;

    private float RawHeight(float x, float z)
    {
        float landscape = WorldLayout.Plan.Borders.Shape(x, z, WorldLayout.Plan.NaturalHeight(x, z));
        float height = roadbed.Apply(x, z, landscape);
        if (outlandRoadbed != null)
            height = outlandRoadbed.Apply(x, z, height);
        if (villageArea is Rect2 area)
        {
            float dx = Mathf.Max(area.Position.X - x, Mathf.Max(0, x - area.End.X));
            float dz = Mathf.Max(area.Position.Y - z, Mathf.Max(0, z - area.End.Y));
            float blend = 1 - Mathf.SmoothStep(0, 80, new Vector2(dx, dz).Length());
            height = Mathf.Lerp(height, villageHeight, blend);
        }
        return River.ShapeTerrain(x, z, GradedHeight(x, z, height));
    }

    /// <summary>Cell size of the grid that tells which graded pieces can reach a point.</summary>
    private const float GradedCell = 32;

    private readonly record struct GradedReach(float Reach, Vector2 Middle, float FarSquared);

    private readonly List<GradedReach> gradedReach = new();
    private readonly Dictionary<Vector2I, List<int>> gradedCells = new();

    private static Vector2I GradedCellOf(float x, float z) =>
        new(Mathf.FloorToInt(x / GradedCell), Mathf.FloorToInt(z / GradedCell));

    /// <summary>Indexes graded pieces added since the last call by the cells their reach touches.</summary>
    private void IndexGraded()
    {
        for (int i = gradedReach.Count; i < graded.Count; i++)
        {
            var piece = graded[i];
            float reach = piece.HalfWidth + piece.Shoulder;
            float far = piece.A.DistanceTo(piece.B) / 2 + reach;
            gradedReach.Add(new GradedReach(reach, (piece.A + piece.B) / 2, far * far));
            var low = GradedCellOf(
                Mathf.Min(piece.A.X, piece.B.X) - reach - 1,
                Mathf.Min(piece.A.Y, piece.B.Y) - reach - 1
            );
            var high = GradedCellOf(
                Mathf.Max(piece.A.X, piece.B.X) + reach + 1,
                Mathf.Max(piece.A.Y, piece.B.Y) + reach + 1
            );
            // Indices are appended in order, so a cell lists its pieces in the order of graded.
            for (int z = low.Y; z <= high.Y; z++)
            for (int x = low.X; x <= high.X; x++)
            {
                var cell = new Vector2I(x, z);
                if (!gradedCells.TryGetValue(cell, out var entries))
                    gradedCells[cell] = entries = new();
                entries.Add(i);
            }
        }
    }

    private float GradedHeight(float x, float z, float height)
    {
        if (graded.Count == 0 || !gradedCells.TryGetValue(GradedCellOf(x, z), out var candidates))
            return height;
        var point = new Vector2(x, z);
        // Only the piece nearest to the point shapes it: pieces that lie next to each other along a way would otherwise
        // pull the centre line towards the heights of their neighbours.
        float nearest = float.MaxValue;
        float design = 0;
        float blend = 0;
        foreach (int index in candidates)
        {
            var piece = graded[index];
            var reachInfo = gradedReach[index];
            float reach = reachInfo.Reach;
            if (point.DistanceSquaredTo(reachInfo.Middle) >= reachInfo.FarSquared)
                continue;
            var delta = piece.B - piece.A;
            float lengthSquared = delta.LengthSquared();
            float t = lengthSquared < .000001f ? 0 : Mathf.Clamp((point - piece.A).Dot(delta) / lengthSquared, 0, 1);
            float distance = point.DistanceTo(piece.A + delta * t);
            if (distance >= reach || distance >= nearest)
                continue;
            nearest = distance;
            design = Mathf.Lerp(piece.HeightA, piece.HeightB, t);
            blend = 1 - Mathf.SmoothStep(piece.HalfWidth, reach, distance);
        }
        if (blend <= 0)
            return height;
        // The roads were laid on the ground as it was: it stays untouched beside them and where a way crosses one.
        if (roadDistance != null)
            blend *= Mathf.SmoothStep(RoadMargin, RoadMargin + 4, roadDistance(x, z));
        return Mathf.Lerp(height, design, blend);
    }

    /// <summary>
    /// Cuts the given ways and platforms into the terrain and recomputes the heights. The visuals and collision are
    /// only built by <see cref="BuildMeshes"/>, once the heights are final. Call it before anything is placed on the
    /// ground the pieces change.
    /// </summary>
    internal void Grade(IEnumerable<GradedPiece> pieces, Func<float, float, float> roadDistance)
    {
        this.roadDistance = roadDistance;
        int before = graded.Count;
        graded.AddRange(pieces);
        if (graded.Count == before)
            return;
        IndexGraded();
        Generate();
    }

    internal void LevelVillage(Rect2 area, float height)
    {
        villageArea = area;
        villageHeight = height;
        Generate();
    }

    // Interpolate the same triangles as the collider, rather than sampling noise between vertices.
    public float Height(float x, float z)
    {
        const float step = Settings.TerrainStep;
        float gx = (x + WorldTuning.CoreRadius) / step,
            gz = (z + WorldTuning.CoreRadius) / step;
        float ix = System.MathF.Floor(gx),
            iz = System.MathF.Floor(gz),
            u = gx - ix,
            v = gz - iz;
        float px = ix * step - WorldTuning.CoreRadius,
            pz = iz * step - WorldTuning.CoreRadius;
        float Sample(int xIndex, int zIndex) =>
            heights != null
            && xIndex >= 0
            && zIndex >= 0
            && xIndex < heights.GetLength(0)
            && zIndex < heights.GetLength(1)
                ? heights[xIndex, zIndex]
                : OutlandHeight(xIndex, zIndex);
        float a = Sample((int)ix, (int)iz),
            b = Sample((int)ix + 1, (int)iz),
            c = Sample((int)ix, (int)iz + 1),
            d = Sample((int)ix + 1, (int)iz + 1);
        return u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
    }

    public Vector3 At(float x, float z, float lift = 0) => new(x, Height(x, z) + lift, z);

    /// <summary>
    /// Computes the height cache. The meshes and colliders wait for <see cref="BuildMeshes"/>: the terrain is levelled
    /// and graded several times while the world is planned, and only the final heights need to be drawn.
    /// </summary>
    public void Generate()
    {
        int count = Mathf.RoundToInt(WorldTuning.CoreRadius * 2 / Settings.TerrainStep);
        heights = new float[count + 1, count + 1];
        for (int z = 0; z <= count; z++)
        for (int x = 0; x <= count; x++)
            heights[x, z] = RawHeight(
                x * Settings.TerrainStep - WorldTuning.CoreRadius,
                z * Settings.TerrainStep - WorldTuning.CoreRadius
            );
        // The land round the core is worked out again from the final core, once, by BuildMeshes.
        outland = null;
        outlandRoadbed = null;
        meshesStale = true;
    }

    private bool meshesStale;

    private static readonly Color GrassLow = new("6caa68");
    private static readonly Color GrassHigh = new("a6c67c");
    private static readonly Color ShoreSand = new("dcc995");

    /// <summary>Builds the visuals and collision from the current heights, replacing any earlier ones.</summary>
    public void BuildMeshes()
    {
        if (!meshesStale || heights == null)
            return;
        meshesStale = false;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.Free();
        }
        int count = heights.GetLength(0) - 1;
        // Neighbouring triangles share their corners, so the colour of each grid point is worked out once.
        var colors = new Color[count + 1, count + 1];
        for (int z = 0; z <= count; z++)
        for (int x = 0; x <= count; x++)
            colors[x, z] = VertexColor(
                x * Settings.TerrainStep - WorldTuning.CoreRadius,
                z * Settings.TerrainStep - WorldTuning.CoreRadius
            );
        const int chunk = 32;
        for (int cz = 0; cz < count; cz += chunk)
        for (int cx = 0; cx < count; cx += chunk)
            GenerateChunk(colors, cx, cz, Mathf.Min(cx + chunk, count), Mathf.Min(cz + chunk, count));
        BuildOutland();
    }

    private Color VertexColor(float x, float z)
    {
        float tint = (noise.GetNoise2D(x * 3 + 1000, z * 3) + 1) * .5f;
        var grass = GrassLow.Lerp(GrassHigh, tint);
        float shore = 1 - Mathf.SmoothStep(River.HalfWidth(z) + 2, River.HalfWidth(z) + 5, River.Distance(x, z));
        return grass.Lerp(ShoreSand, shore).SrgbToLinear();
    }

    private void GenerateChunk(Color[,] colors, int startX, int startZ, int endX, int endZ)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const float step = Settings.TerrainStep;
        for (int z = startZ; z < endZ; z++)
        for (int x = startX; x < endX; x++)
        {
            float px = -WorldTuning.CoreRadius + x * step,
                pz = -WorldTuning.CoreRadius + z * step;
            Vector3 a = At(px, pz),
                b = At(px + step, pz),
                c = At(px, pz + step),
                d = At(px + step, pz + step);
            Color ca = colors[x, z],
                cb = colors[x + 1, z],
                cc = colors[x, z + 1],
                cd = colors[x + 1, z + 1];
            Add(a, ca);
            Add(b, cb);
            Add(c, cc);
            Add(b, cb);
            Add(d, cd);
            Add(c, cc);
        }
        void Add(Vector3 p, Color color)
        {
            surface.SetColor(color);
            surface.AddVertex(p);
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
        var visual = new MeshInstance3D { Mesh = mesh, MaterialOverride = meadowMaterial };
        AddChild(visual);
        var body = new StaticBody3D();
        AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
    }
}
