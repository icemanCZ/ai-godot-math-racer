using Godot;
using System.Collections.Generic;

namespace MathRacer;

public partial class Roads : Node3D
{
    public const float MainWidth = 7;
    // Branch attachment points: blend directional marks into one shared dirt surface here.
    private static readonly Vector2[] Junctions =
    {
        new(-18, -25), new(64, -62), new(35, 28),
        new(-74, -88), new(66, 52), new(150, 61)
    };
    private const float BucketSize = 12;
    private readonly Dictionary<Vector2I, List<Vector2[]>> occupied = new();
    private readonly Dictionary<Vector2I, List<Vector3[]>> surfaceTriangles = new();
    private Curve3D mainRoute = null!;
    private readonly ShaderMaterial roadMaterial = new()
    {
        Shader = GD.Load<Shader>("res://Shaders/RoadSurface.gdshader")
    };
    public readonly List<Vector3> Samples = new();
    public (Vector3 Center, Vector3 Forward) SampleMain(float distance)
    {
        float length = mainRoute.GetBakedLength();
        float offset = Mathf.Clamp(distance, 0, length);
        var center = mainRoute.SampleBaked(offset);
        var forward = (mainRoute.SampleBaked(Mathf.Min(offset + .2f, length)) - mainRoute.SampleBaked(Mathf.Max(offset - .2f, 0))).Normalized();
        return (center, forward);
    }
    public void Generate(Terrain terrain)
    {
        mainRoute = Make(new[] { new Vector3(0, 0, 8), new Vector3(-18, 0, -25), new Vector3(-55, 0, -50), new Vector3(-74, 0, -88), new Vector3(-35, 0, -118), new Vector3(24, 0, -100), new Vector3(64, 0, -62), new Vector3(70, 0, -10), new Vector3(35, 0, 28), new Vector3(0, 0, 8) }, MainWidth, terrain);
        Make(new[] { new Vector3(-18, 0, -25), new Vector3(12, 0, -40), new Vector3(40, 0, -53), new Vector3(64, 0, -62) }, 5, terrain);
        Make(new[] { new Vector3(35, 0, 28), new Vector3(66, 0, 52), new Vector3(91, 0, 46), new Vector3(139, 0, 46), new Vector3(150, 0, 61) }, 5, terrain);
        Make(new[] { new Vector3(-74, 0, -88), new Vector3(-210, 0, -180), new Vector3(-370, 0, -240), new Vector3(-400, 0, 0), new Vector3(-270, 0, 240), new Vector3(-70, 0, 360), new Vector3(55, 0, 270), new Vector3(65, 0, 110), new Vector3(66, 0, 52) }, 6, terrain);
        Make(new[] { new Vector3(150, 0, 61), new Vector3(310, 0, 210), new Vector3(420, 0, 40), new Vector3(350, 0, -220), new Vector3(190, 0, -380), new Vector3(155, 0, -170), new Vector3(150, 0, 61) }, 6, terrain);
    }
    private Curve3D Make(Vector3[] points, float width, Terrain terrain)
    {
        var curve = new Curve3D();
        for (int i = 0; i < points.Length; i++)
        {
            bool closed = points[0] == points[^1];
            var tangent = closed && (i == 0 || i == points.Length - 1)
                ? (points[1] - points[^2]) * .19f
                : (points[Mathf.Min(i + 1, points.Length - 1)] - points[Mathf.Max(i - 1, 0)]) * .19f;
            curve.AddPoint(points[i], -tangent, tangent);
        }
        var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
        var footprints = new List<Vector2[]>();
        float length = curve.GetBakedLength();
        bool StartsAtJunction() => System.Array.Exists(Junctions, j => j.DistanceTo(new Vector2(points[0].X, points[0].Z)) < .1f);
        bool EndsAtJunction() => System.Array.Exists(Junctions, j => j.DistanceTo(new Vector2(points[^1].X, points[^1].Z)) < .1f);
        bool flareStart = StartsAtJunction(), flareEnd = EndsAtJunction();
        for (float distance = 0; distance < length; distance += 1.4f)
        {
            var p = curve.SampleBaked(distance); var q = curve.SampleBaked(Mathf.Min(distance + 1.4f, length));
            Samples.Add(p);
            // The wooden deck provides the route surface over the open channel.
            bool CoveredByDeck(Vector3 point) => Mathf.Abs(point.Z - River.BridgeZ) < 3.4f && Mathf.Abs(point.X - River.CenterX(River.BridgeZ)) < 17;
            if (CoveredByDeck(p) && CoveredByDeck(q)) continue;
            Vector3 Side(float offset)
            {
                // Gently widen branch mouths, rounding the abrupt right-angle shoulders.
                float mouth = Mathf.Max(flareStart ? 1 - Mathf.SmoothStep(0, 10, offset) : 0,
                    flareEnd ? 1 - Mathf.SmoothStep(0, 10, length - offset) : 0);
                return (curve.SampleBaked(Mathf.Min(offset + .15f, length)) - curve.SampleBaked(Mathf.Max(offset - .15f, 0))).Normalized().Cross(Vector3.Up) * (width / 2 + mouth * 2.5f);
            }
            var right = Side(distance);
            var nextRight = Side(Mathf.Min(distance + 1.4f, length));
            Vector2 Flat(Vector3 v) => new(v.X, v.Z);
            var footprint = new[] { Flat(p - right), Flat(p + right), Flat(q + nextRight), Flat(q - nextRight) };
            footprints.Add(footprint);
            // Cut away earlier routes instead of stacking nearly coplanar ribbons at joins.
            var blockers = new HashSet<Vector2[]>();
            foreach (var bucket in Buckets(footprint))
                if (occupied.TryGetValue(bucket, out var entries))
                    foreach (var entry in entries) blockers.Add(entry);
            for (int lane = 0; lane < 8; lane++)
            {
                float l = -1 + lane * .25f, r = l + .25f;
                var a = terrain.At(p.X + right.X * l, p.Z + right.Z * l, .13f);
                var b = terrain.At(p.X + right.X * r, p.Z + right.Z * r, .13f);
                var c = terrain.At(q.X + nextRight.X * l, q.Z + nextRight.Z * l, .13f);
                var d = terrain.At(q.X + nextRight.X * r, q.Z + nextRight.Z * r, .13f);
                // Across-road position and distance in metres keep wheel tracks aligned on bends.
                void Vertex(Vector3 position, float across, float along)
                {
                    float fade = 1;
                    foreach (var junction in Junctions)
                        fade = Mathf.Min(fade, Mathf.SmoothStep(6, 13, Flat(position).DistanceTo(junction)));
                    st.SetColor(new Color(fade, 0, 0));
                    st.SetUV(new Vector2(across, along));
                    st.AddVertex(position);
                }
                void Triangle(Vector3 v0, Vector3 v1, Vector3 v2, Vector2 uv0, Vector2 uv1, Vector2 uv2)
                {
                    var x0 = Flat(v0); var x1 = Flat(v1); var x2 = Flat(v2);
                    var pieces = new List<Vector2[]> { new[] { x0, x1, x2 } };
                    foreach (var blocker in blockers)
                    {
                        var remaining = new List<Vector2[]>();
                        foreach (var piece in pieces)
                            remaining.AddRange(Geometry2D.ClipPolygons(piece, blocker));
                        pieces = remaining;
                        if (pieces.Count == 0) break;
                    }
                    float area = (x1 - x0).Cross(x2 - x0);
                    if (Mathf.Abs(area) < .000001f) return;
                    foreach (var piece in pieces)
                    {
                        var indices = Geometry2D.TriangulatePolygon(piece);
                        for (int i = 0; i < indices.Length; i += 3)
                        {
                            // Match the original ribbon winding regardless of clipping output order.
                            int j1 = indices[i + 1], j2 = indices[i + 2];
                            if ((piece[j1] - piece[indices[i]]).Cross(piece[j2] - piece[indices[i]]) * area < 0)
                                (j1, j2) = (j2, j1);
                            var triangle = new[] { Emit(piece[indices[i]]), Emit(piece[j1]), Emit(piece[j2]) };
                            foreach (var bucket in Buckets(new[] { Flat(triangle[0]), Flat(triangle[1]), Flat(triangle[2]) }))
                            {
                                if (!surfaceTriangles.TryGetValue(bucket, out var entries)) surfaceTriangles[bucket] = entries = new();
                                entries.Add(triangle);
                            }
                        }
                    }
                    Vector3 Emit(Vector2 x)
                    {
                        float u = (x - x0).Cross(x2 - x0) / area;
                        float v = (x1 - x0).Cross(x - x0) / area;
                        var uv = uv0 + (uv1 - uv0) * u + (uv2 - uv0) * v;
                        var position = terrain.At(x.X, x.Y, .13f);
                        Vertex(position, uv.X, uv.Y);
                        return position;
                    }
                }
                float nextDistance = Mathf.Min(distance + 1.4f, length);
                Triangle(a, b, c, new(l, distance), new(r, distance), new(l, nextDistance));
                Triangle(b, d, c, new(r, distance), new(r, nextDistance), new(l, nextDistance));
            }
        }
        st.GenerateNormals();
        AddChild(new MeshInstance3D { Mesh = st.Commit(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = roadMaterial });
        foreach (var footprint in footprints)
            foreach (var bucket in Buckets(footprint))
            {
                if (!occupied.TryGetValue(bucket, out var entries)) occupied[bucket] = entries = new();
                entries.Add(footprint);
            }
        return curve;
    }
    private static IEnumerable<Vector2I> Buckets(Vector2[] polygon)
    {
        var min = polygon[0]; var max = min;
        foreach (var p in polygon) { min = min.Min(p); max = max.Max(p); }
        for (int z = Mathf.FloorToInt(min.Y / BucketSize); z <= Mathf.FloorToInt(max.Y / BucketSize); z++)
            for (int x = Mathf.FloorToInt(min.X / BucketSize); x <= Mathf.FloorToInt(max.X / BucketSize); x++)
                yield return new Vector2I(x, z);
    }
    public bool Contains(Vector3 p)
    {
        var flat = new Vector2(p.X, p.Z);
        var bucket = new Vector2I(Mathf.FloorToInt(p.X / BucketSize), Mathf.FloorToInt(p.Z / BucketSize));
        if (occupied.TryGetValue(bucket, out var entries))
            foreach (var polygon in entries)
                if (Geometry2D.IsPointInPolygon(flat, polygon)) return true;
        return false;
    }
    // Roads span terrain triangle boundaries, so their rendered height can differ
    // from terrain.Height + .13 between vertices. Sample the actual road mesh.
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
            if (u >= -.00001f && v >= -.00001f && u + v <= 1.00001f)
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

