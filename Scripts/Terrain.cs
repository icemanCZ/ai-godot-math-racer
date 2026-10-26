using Godot;

namespace MathRacer;

public partial class Terrain : Node3D
{
    private readonly FastNoiseLite noise = new() { Seed = Settings.Seed, Frequency = .013f, FractalOctaves = 3 };
    private readonly RoadGrading roadbed = new(WorldLayout.Plan);
    private float[,]? heights;
    private float RawHeight(float x, float z)
    {
        float landscape = WorldLayout.Plan.NaturalHeight(x, z);
        return River.ShapeTerrain(x, z, roadbed.Apply(x, z, landscape));
    }
    // Interpolate the same triangles as the collider, rather than sampling noise between vertices.
    public float Height(float x, float z)
    {
        const float step = Settings.TerrainStep;
        float gx = (x + Settings.WorldRadius) / step, gz = (z + Settings.WorldRadius) / step;
        float ix = Mathf.Floor(gx), iz = Mathf.Floor(gz), u = gx - ix, v = gz - iz;
        float px = ix * step - Settings.WorldRadius, pz = iz * step - Settings.WorldRadius;
        float Sample(int xIndex, int zIndex) => heights != null && xIndex >= 0 && zIndex >= 0 && xIndex < heights.GetLength(0) && zIndex < heights.GetLength(1)
            ? heights[xIndex, zIndex] : RawHeight(xIndex * step - Settings.WorldRadius, zIndex * step - Settings.WorldRadius);
        float a = Sample((int)ix, (int)iz), b = Sample((int)ix + 1, (int)iz), c = Sample((int)ix, (int)iz + 1), d = Sample((int)ix + 1, (int)iz + 1);
        return u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
    }
    public Vector3 At(float x, float z, float lift = 0) => new(x, Height(x, z) + lift, z);
    public void Generate()
    {
        int count = Mathf.RoundToInt(Settings.WorldRadius * 2 / Settings.TerrainStep);
        heights = new float[count + 1, count + 1];
        for (int z = 0; z <= count; z++)
            for (int x = 0; x <= count; x++)
                heights[x, z] = RawHeight(x * Settings.TerrainStep - Settings.WorldRadius, z * Settings.TerrainStep - Settings.WorldRadius);
        const int chunk = 32;
        for (int cz = 0; cz < count; cz += chunk)
            for (int cx = 0; cx < count; cx += chunk)
                GenerateChunk(cx, cz, Mathf.Min(cx + chunk, count), Mathf.Min(cz + chunk, count));
    }
    private void GenerateChunk(int startX, int startZ, int endX, int endZ)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        const float step = Settings.TerrainStep;
        for (int z = startZ; z < endZ; z++)
            for (int x = startX; x < endX; x++)
            {
                float px = -Settings.WorldRadius + x * step, pz = -Settings.WorldRadius + z * step;
                Vector3 a = At(px, pz), b = At(px + step, pz), c = At(px, pz + step), d = At(px + step, pz + step);
                Add(a); Add(b); Add(c); Add(b); Add(d); Add(c);
            }
        void Add(Vector3 p)
        {
            float tint = (noise.GetNoise2D(p.X * 3 + 1000, p.Z * 3) + 1) * .5f;
            var grass = new Color("48b95c").Lerp(new Color("8bd653"), tint);
            float shore = 1 - Mathf.SmoothStep(River.HalfWidth(p.Z) + 2, River.HalfWidth(p.Z) + 5, River.Distance(p.X, p.Z));
            surface.SetColor(grass.Lerp(new Color("dcc995"), shore).SrgbToLinear());
            surface.AddVertex(p);
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        var visual = new MeshInstance3D { Mesh = mesh, MaterialOverride = material };
        AddChild(visual);
        var body = new StaticBody3D(); AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
    }
}

