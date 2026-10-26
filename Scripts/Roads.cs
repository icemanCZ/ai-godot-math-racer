using Godot;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public partial class Roads : Node3D
{
    public const float MainWidth = 7;
    private const float BucketSize = 12;
    private const float MeshStep = .75f;
    private readonly record struct Segment(Vector2 A, Vector2 B, float Width, float Along);
    private readonly record struct FieldPoint(Vector2 Position, float Distance, Vector2 Uv, float Marks);
    private readonly Dictionary<Vector2I, List<Segment>> segments = new();
    private readonly Dictionary<Vector2I, List<Vector3[]>> surfaceTriangles = new();
    private Curve3D mainRoute = null!;
    private Vector2[] junctions = System.Array.Empty<Vector2>();
    private readonly ShaderMaterial roadMaterial = new() { Shader = GD.Load<Shader>("res://Shaders/RoadSurface.gdshader") };
    public readonly List<Vector3> Samples = new();

    public (Vector3 Center, Vector3 Forward) SampleMain(float distance)
    {
        float length = mainRoute.GetBakedLength();
        float offset = Mathf.Clamp(distance, 0, length);
        var center = mainRoute.SampleBaked(offset);
        var forward = (mainRoute.SampleBaked(Mathf.Min(offset + .2f, length)) -
            mainRoute.SampleBaked(Mathf.Max(offset - .2f, 0))).Normalized();
        return (center, forward);
    }

    public void Generate(Terrain terrain)
    {
        var routes = WorldLayout.Plan.Routes;
        junctions = routes.SelectMany(route => route).GroupBy(point => point).Where(group => group.Count() > 1)
            .Select(group => new Vector2(group.Key.X, group.Key.Z)).ToArray();
        mainRoute = WorldPlan.CreateCurve(routes[0]);
        for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
        {
            using var curve = WorldPlan.CreateCurve(routes[routeIndex]);
            float length = curve.GetBakedLength();
            float baseWidth = routeIndex == 0 ? MainWidth / 2 : 3;
            for (float distance = 0; distance < length; distance += 1.4f)
            {
                var p = curve.SampleBaked(distance);
                var q = curve.SampleBaked(Mathf.Min(distance + 1.4f, length));
                Samples.Add(p);
                float mouth = Mathf.Max(1 - Mathf.SmoothStep(0, 10, distance),
                    1 - Mathf.SmoothStep(0, 10, length - distance));
                var segment = new Segment(new Vector2(p.X, p.Z), new Vector2(q.X, q.Z), baseWidth + mouth * 2.5f, distance);
                foreach (var bucket in Buckets(segment.A.Min(segment.B) - Vector2.One * 8,
                    segment.A.Max(segment.B) + Vector2.One * 8))
                {
                    if (!segments.TryGetValue(bucket, out var entries)) segments[bucket] = entries = new();
                    entries.Add(segment);
                }
            }
        }
        BuildSurface(terrain);
    }

    private FieldPoint SampleField(Vector2 point)
    {
        float best = 1000;
        Vector2 uv = Vector2.Zero;
        var key = new Vector2I(Mathf.FloorToInt(point.X / BucketSize), Mathf.FloorToInt(point.Y / BucketSize));
        if (segments.TryGetValue(key, out var entries))
            foreach (var segment in entries)
            {
                var delta = segment.B - segment.A;
                float lengthSquared = delta.LengthSquared();
                float t = lengthSquared < .000001f ? 0 : Mathf.Clamp((point - segment.A).Dot(delta) / lengthSquared, 0, 1);
                float signed = point.DistanceTo(segment.A + delta * t) - segment.Width;
                if (signed >= best) continue;
                best = signed;
                float length = Mathf.Sqrt(lengthSquared);
                uv = new Vector2(length > .0001f ? (point - segment.A).Cross(delta / length) / segment.Width : 0,
                    segment.Along + t * length);
            }
        float marks = 1;
        foreach (var junction in junctions)
        {
            float distance = point.DistanceTo(junction);
            best = Mathf.Min(best, distance - 7);
            marks = Mathf.Min(marks, Mathf.SmoothStep(7, 14, distance));
        }
        // A rectangular opening meets the wooden deck; ramps cover its ends.
        float deck = Mathf.Max(Mathf.Abs(point.X - River.CenterX(River.BridgeZ)) - 18.9f,
            Mathf.Abs(point.Y - River.BridgeZ) - 3.7f);
        best = Mathf.Max(best, -deck);
        return new FieldPoint(point, best, uv, marks);
    }

    private void BuildSurface(Terrain terrain)
    {
        // Sample the UNION once. Adjacent triangles share field values and contour edges,
        // so intersecting routes cannot cut holes out of one another.
        var cache = new Dictionary<Vector2I, FieldPoint>();
        FieldPoint Sample(int x, int z)
        {
            var key = new Vector2I(x, z);
            if (!cache.TryGetValue(key, out var point))
                cache[key] = point = SampleField(new Vector2(x * MeshStep, z * MeshStep));
            return point;
        }
        const int cellsPerBucket = 16;
        foreach (var chunk in segments.Keys.GroupBy(key => new Vector2I(Mathf.FloorToInt(key.X / 4f), Mathf.FloorToInt(key.Y / 4f))))
        {
            var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
            int count = 0;
            foreach (var bucket in chunk)
              for (int z = bucket.Y * cellsPerBucket; z < (bucket.Y + 1) * cellsPerBucket; z++)
                for (int x = bucket.X * cellsPerBucket; x < (bucket.X + 1) * cellsPerBucket; x++)
                {
                    var a = Sample(x, z); var b = Sample(x + 1, z);
                    var c = Sample(x, z + 1); var d = Sample(x + 1, z + 1);
                    Emit(a, b, c); Emit(b, d, c);
                }
            void Emit(FieldPoint a, FieldPoint b, FieldPoint c)
            {
                if (a.Distance >= 0 && b.Distance >= 0 && c.Distance >= 0) return;
                var polygon = new List<FieldPoint>(4);
                var input = new[] { a, b, c };
                for (int i = 0; i < 3; i++)
                {
                    var p = input[i]; var q = input[(i + 1) % 3];
                    if (p.Distance <= 0) polygon.Add(p);
                    if ((p.Distance < 0) != (q.Distance < 0))
                    {
                        float t = p.Distance / (p.Distance - q.Distance);
                        polygon.Add(new FieldPoint(p.Position.Lerp(q.Position, t), 0,
                            p.Uv.Lerp(q.Uv, t), Mathf.Lerp(p.Marks, q.Marks, t)));
                    }
                }
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    var points = new[] { polygon[0], polygon[i], polygon[i + 1] };
                    float area = (points[1].Position - points[0].Position).Cross(points[2].Position - points[0].Position);
                    if (Mathf.Abs(area) < .000001f) continue;
                    var triangle = new Vector3[3];
                    for (int vertex = 0; vertex < 3; vertex++)
                    {
                        var point = points[vertex];
                        triangle[vertex] = terrain.At(point.Position.X, point.Position.Y, .13f);
                        surface.SetColor(new Color(point.Marks, 0, 0));
                        surface.SetUV(point.Uv); surface.AddVertex(triangle[vertex]);
                    }
                    count++;
                    var min = points[0].Position.Min(points[1].Position).Min(points[2].Position);
                    var max = points[0].Position.Max(points[1].Position).Max(points[2].Position);
                    foreach (var key in Buckets(min, max))
                    {
                        if (!surfaceTriangles.TryGetValue(key, out var entries)) surfaceTriangles[key] = entries = new();
                        entries.Add(triangle);
                    }
                }
            }
            if (count == 0) continue;
            surface.GenerateNormals();
            AddChild(new MeshInstance3D { Mesh = surface.Commit(), MaterialOverride = roadMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        }
    }

    private static IEnumerable<Vector2I> Buckets(Vector2 min, Vector2 max)
    {
        for (int z = Mathf.FloorToInt(min.Y / BucketSize); z <= Mathf.FloorToInt(max.Y / BucketSize); z++)
            for (int x = Mathf.FloorToInt(min.X / BucketSize); x <= Mathf.FloorToInt(max.X / BucketSize); x++)
                yield return new Vector2I(x, z);
    }

    public bool Contains(Vector3 p) => !float.IsNegativeInfinity(SurfaceHeight(p, float.NegativeInfinity));

    public float SurfaceHeight(Vector3 p, float groundHeight)
    {
        float height = groundHeight;
        var flat = new Vector2(p.X, p.Z);
        var bucket = new Vector2I(Mathf.FloorToInt(p.X / BucketSize), Mathf.FloorToInt(p.Z / BucketSize));
        if (!surfaceTriangles.TryGetValue(bucket, out var entries)) return height;
        foreach (var triangle in entries)
        {
            var a = new Vector2(triangle[0].X, triangle[0].Z);
            var b = new Vector2(triangle[1].X, triangle[1].Z);
            var c = new Vector2(triangle[2].X, triangle[2].Z);
            float area = (b - a).Cross(c - a);
            if (Mathf.Abs(area) < .000001f) continue;
            float u = (flat - a).Cross(c - a) / area;
            float v = (b - a).Cross(flat - a) / area;
            if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f)
                height = Mathf.Max(height, triangle[0].Y + u * (triangle[1].Y - triangle[0].Y) + v * (triangle[2].Y - triangle[0].Y));
        }
        return height;
    }

    public float Distance(Vector3 p)
    {
        float nearest = float.MaxValue;
        foreach (var sample in Samples) nearest = Mathf.Min(nearest, new Vector2(p.X - sample.X, p.Z - sample.Z).Length());
        return nearest;
    }
}
