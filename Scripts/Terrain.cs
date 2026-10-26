using Godot;

namespace MathRacer;

public partial class Terrain : Node3D
{
    private readonly FastNoiseLite noise = new() { Seed = Settings.Seed, Frequency = .013f, FractalOctaves = 3 };
    private float RawHeight(float x, float z)
    {
        float natural = noise.GetNoise2D(x, z) * 7f + Mathf.Sin(x * .023f) * 2.4f;
        // A calm, gently undulating central valley keeps all routes accessible.
        return River.ShapeTerrain(x, z, natural * .65f + Mathf.SmoothStep(130, 250, new Vector2(x, z).Length()) * (14 + 11 * Mathf.Sin(x * .012f) * Mathf.Cos(z * .009f) + 8 * Mathf.Sin((x + z) * .017f)));
    }
    // Interpolate the same triangles as the collider, rather than sampling noise between vertices.
    public float Height(float x, float z)
    {
        const float step = Settings.TerrainStep;
        float gx = (x + Settings.WorldRadius) / step, gz = (z + Settings.WorldRadius) / step;
        float ix = Mathf.Floor(gx), iz = Mathf.Floor(gz), u = gx - ix, v = gz - iz;
        float px = ix * step - Settings.WorldRadius, pz = iz * step - Settings.WorldRadius;
        float a = RawHeight(px, pz), b = RawHeight(px + step, pz), c = RawHeight(px, pz + step), d = RawHeight(px + step, pz + step);
        return u + v <= 1 ? a + (b - a) * u + (c - a) * v : d + (c - d) * (1 - u) + (b - d) * (1 - v);
    }
    public Vector3 At(float x, float z, float lift = 0) => new(x, Height(x, z) + lift, z);
    public void Generate()
    {
        int count = Mathf.RoundToInt(Settings.WorldRadius * 2 / Settings.TerrainStep);
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

