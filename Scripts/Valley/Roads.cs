using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer.Valley;

public partial class Roads : Node3D
{
    public const float MainWidth = 7;
    private const float BucketSize = 12;
    private const float MeshStep = .75f;

    /// <summary>The road surface lies this high over the ground.</summary>
    private const float RoadLift = .13f;
    private const int NavigationAnchorStep = 12;

    /// <summary><see cref="Middle"/> and <see cref="HalfLength"/> bound the distance to the segment from below.</summary>
    private readonly record struct Segment(
        Vector2 A,
        Vector2 B,
        float Width,
        float Along,
        Vector2 Middle,
        float HalfLength
    );

    /// <summary><see cref="Deck"/> is negative inside the opening the bridge's wooden deck fills.</summary>
    private readonly record struct FieldPoint(Vector2 Position, float Distance, Vector2 Uv, float Marks, float Deck);

    private readonly Dictionary<Vector2I, List<Segment>> segments = new();
    private readonly List<Segment> allSegments = new();

    /// <summary>
    /// A drivable surface triangle. <see cref="MinBucket"/> and <see cref="MaxBucket"/> are the coarse buckets its
    /// footprint touches: a point only sees the triangles of its own bucket, as it always has.
    /// </summary>
    private readonly record struct SurfaceTriangle(
        Vector3 A,
        Vector3 B,
        Vector3 C,
        Vector2I MinBucket,
        Vector2I MaxBucket
    );

    /// <summary>Cell size of the grid that finds the triangles near a point; the triangles are under a metre wide.</summary>
    private const float TriangleCell = 1.5f;

    private const float TriangleCellMargin = .05f;

    private readonly Dictionary<Vector2I, List<SurfaceTriangle>> surfaceTriangles = new();
    private Curve3D mainRoute = null!;
    private Vector2[] junctions = System.Array.Empty<Vector2>();
    private readonly ShaderMaterial roadMaterial = new()
    {
        Shader = GD.Load<Shader>("res://Shaders/RoadSurface.gdshader"),
    };
    public readonly List<Vector3> Samples = new();
    internal readonly List<(Vector3 Position, Vector3 Forward)> NavigationAnchors = new();
    internal readonly List<int> SampleAnchors = new();

    public (Vector3 Center, Vector3 Forward) SampleMain(float distance)
    {
        float length = mainRoute.GetBakedLength();
        float offset = Mathf.Clamp(distance, 0, length);
        var center = mainRoute.SampleBaked(offset);
        var forward = (
            mainRoute.SampleBaked(Mathf.Min(offset + .2f, length)) - mainRoute.SampleBaked(Mathf.Max(offset - .2f, 0))
        ).Normalized();
        return (center, forward);
    }

    public void Generate(Terrain terrain)
    {
        var routes = WorldLayout.Plan.Routes;
        junctions = routes
            .SelectMany(route => route)
            .GroupBy(point => point)
            .Where(group => group.Count() > 1)
            .Select(group => new Vector2(group.Key.X, group.Key.Z))
            .ToArray();
        mainRoute = WorldPlan.CreateCurve(routes[0]);
        for (int routeIndex = 0; routeIndex < routes.Length; routeIndex++)
        {
            int sampleStart = Samples.Count;
            using var curve = WorldPlan.CreateCurve(routes[routeIndex]);
            float length = curve.GetBakedLength();
            float baseWidth = routeIndex == 0 ? MainWidth / 2 : 3;
            for (float distance = 0; distance < length; distance += 1.4f)
            {
                var p = curve.SampleBaked(distance);
                var q = curve.SampleBaked(Mathf.Min(distance + 1.4f, length));
                Samples.Add(p);
                float mouth = Mathf.Max(
                    1 - Mathf.SmoothStep(0, 10, distance),
                    1 - Mathf.SmoothStep(0, 10, length - distance)
                );
                var segmentStart = new Vector2(p.X, p.Z);
                var segmentEnd = new Vector2(q.X, q.Z);
                var segment = new Segment(
                    segmentStart,
                    segmentEnd,
                    baseWidth + mouth * 2.5f,
                    distance,
                    (segmentStart + segmentEnd) / 2,
                    segmentStart.DistanceTo(segmentEnd) / 2
                );
                allSegments.Add(segment);
                foreach (
                    var bucket in Buckets(
                        segment.A.Min(segment.B) - Vector2.One * 8,
                        segment.A.Max(segment.B) + Vector2.One * 8
                    )
                )
                {
                    if (!segments.TryGetValue(bucket, out var entries))
                        segments[bucket] = entries = new();
                    entries.Add(segment);
                }
            }
            // Fixed road markings: route searches must never determine their spacing or position.
            int anchorStart = NavigationAnchors.Count;
            for (int i = sampleStart; i < Samples.Count; i += NavigationAnchorStep)
            {
                var forward = Samples[Mathf.Min(i + 1, Samples.Count - 1)] - Samples[Mathf.Max(i - 1, sampleStart)];
                forward.Y = 0;
                NavigationAnchors.Add((Samples[i], forward.Normalized()));
            }
            int lastAnchor = NavigationAnchors.Count - 1;
            for (int i = sampleStart; i < Samples.Count; i++)
                SampleAnchors.Add(
                    Mathf.Min(
                        anchorStart + Mathf.RoundToInt((i - sampleStart) / (float)NavigationAnchorStep),
                        lastAnchor
                    )
                );
        }
        BuildSurface(terrain);
    }

    /// <summary>The mesh cells that can hold road (or, for the verge, road and verge); see <see cref="MarkCells"/>.</summary>
    private sealed class CellMask
    {
        public bool[] Cells = System.Array.Empty<bool>();
        public int X,
            Z,
            Columns,
            Rows;

        public bool Has(int x, int z)
        {
            int column = x - X,
                row = z - Z;
            return column >= 0 && column < Columns && row >= 0 && row < Rows && Cells[row * Columns + column];
        }
    }

    private CellMask roadCells = new();

    /// <summary>
    /// Marks every mesh cell that can contain road, widened by <paramref name="extra"/>: a corner of such a cell lies
    /// within a segment's width (plus the extra) of the segment, or within the junction radius of a junction. Any other
    /// cell has no corner inside, so it produces no triangles and its corners need not be sampled.
    /// </summary>
    private CellMask MarkCells(float extra)
    {
        const float junctionRadius = 7;
        const float slack = .05f;
        var mask = new CellMask();
        var low = new Vector2(float.MaxValue, float.MaxValue);
        var high = new Vector2(float.MinValue, float.MinValue);
        foreach (var segment in allSegments)
        {
            low = low.Min(segment.A.Min(segment.B) - Vector2.One * (segment.Width + extra + slack));
            high = high.Max(segment.A.Max(segment.B) + Vector2.One * (segment.Width + extra + slack));
        }
        foreach (var junction in junctions)
        {
            low = low.Min(junction - Vector2.One * (junctionRadius + extra + slack));
            high = high.Max(junction + Vector2.One * (junctionRadius + extra + slack));
        }
        if (low.X > high.X)
            return mask;
        mask.X = Mathf.FloorToInt(low.X / MeshStep) - 1;
        mask.Z = Mathf.FloorToInt(low.Y / MeshStep) - 1;
        mask.Columns = Mathf.CeilToInt(high.X / MeshStep) - mask.X + 2;
        mask.Rows = Mathf.CeilToInt(high.Y / MeshStep) - mask.Z + 2;
        mask.Cells = new bool[mask.Columns * mask.Rows];
        void Mark(Vector2 from, Vector2 to, float reach)
        {
            // A corner inside the reach belongs to the cell on either side of it, hence one cell more on each side.
            var min = from.Min(to) - Vector2.One * (reach + slack);
            var max = from.Max(to) + Vector2.One * (reach + slack);
            int firstX = Mathf.FloorToInt(min.X / MeshStep) - 1,
                lastX = Mathf.CeilToInt(max.X / MeshStep);
            int firstZ = Mathf.FloorToInt(min.Y / MeshStep) - 1,
                lastZ = Mathf.CeilToInt(max.Y / MeshStep);
            for (int z = firstZ; z <= lastZ; z++)
            for (int x = firstX; x <= lastX; x++)
                mask.Cells[(z - mask.Z) * mask.Columns + x - mask.X] = true;
        }
        foreach (var segment in allSegments)
            Mark(segment.A, segment.B, segment.Width + extra);
        foreach (var junction in junctions)
            Mark(junction, junction, junctionRadius + extra);
        return mask;
    }

    private static WorldPlan? cachedFieldPlan;
    private static Dictionary<Vector2I, FieldPoint>? cachedField;

    /// <summary>Drops the sampled field once the valley is generated; a new valley would not match it anyway.</summary>
    internal static void ReleaseFieldCache()
    {
        cachedField = null;
        cachedFieldPlan = null;
    }

    /// <summary>
    /// The signed distance to the union of the roads at a point of the mesh grid, with the texture coordinates of the
    /// nearest segment. It is asked for hundreds of thousands of points, so it is written in floats; every step is the
    /// Godot vector operation it replaces (see <see cref="FlatMath"/>), so the surface comes out the same.
    /// </summary>
    private FieldPoint SampleField(Vector2 point)
    {
        float px = point.X,
            pz = point.Y;
        float best = 1000;
        Vector2 uv = Vector2.Zero;
        var key = new Vector2I((int)System.MathF.Floor(px / BucketSize), (int)System.MathF.Floor(pz / BucketSize));
        if (segments.TryGetValue(key, out var entries))
            foreach (var segment in entries)
            {
                // The segment cannot be nearer than its middle minus its half length. When even that is further than
                // the best so far (with a centimetre to spare for rounding), the full test would skip it as well.
                float reach = best + segment.Width + segment.HalfLength + .01f;
                float mx = px - segment.Middle.X,
                    mz = pz - segment.Middle.Y;
                if (reach > 0 && mx * mx + mz * mz >= reach * reach)
                    continue;
                float ax = segment.A.X,
                    az = segment.A.Y;
                float dx = segment.B.X - ax,
                    dz = segment.B.Y - az;
                float lengthSquared = dx * dx + dz * dz;
                float fromX = px - ax,
                    fromZ = pz - az;
                float t =
                    lengthSquared < .000001f ? 0 : FlatMath.Clamp((fromX * dx + fromZ * dz) / lengthSquared, 0, 1);
                float signed = FlatMath.Length(px - (ax + dx * t), pz - (az + dz * t)) - segment.Width;
                if (signed >= best)
                    continue;
                best = signed;
                float length = System.MathF.Sqrt(lengthSquared);
                // (point - A).Cross(delta / length) / Width.
                uv = new Vector2(
                    length > .0001f ? (fromX * (dz / length) - fromZ * (dx / length)) / segment.Width : 0,
                    segment.Along + t * length
                );
            }
        float marks = 1;
        foreach (var junction in junctions)
        {
            float distance = FlatMath.Length(px - junction.X, pz - junction.Y);
            best = System.Math.Min(best, distance - 7);
            if (distance < 14)
                marks = System.Math.Min(marks, Mathf.SmoothStep(7, 14, distance));
        }
        // A rectangular opening meets the wooden deck; ramps cover its ends.
        float deck = System.Math.Max(
            System.MathF.Abs(px - River.CenterX(River.BridgeZ)) - 18.9f,
            System.MathF.Abs(pz - River.BridgeZ) - 3.7f
        );
        best = System.Math.Max(best, -deck);
        return new FieldPoint(point, best, uv, marks, deck);
    }

    private void BuildSurface(Terrain terrain)
    {
        // Sample the UNION once. Adjacent triangles share field values and contour edges,
        // so intersecting routes cannot cut holes out of one another.
        // The field depends on the road plan alone, not on the terrain, so the second generation of a valley
        // (after the village is levelled) reuses every point the first one sampled.
        var plan = WorldLayout.Plan;
        if (cachedFieldPlan != plan || cachedField == null)
        {
            cachedField = new Dictionary<Vector2I, FieldPoint>();
            cachedFieldPlan = plan;
        }
        roadCells = MarkCells(0);
        const int cellsPerBucket = 16;
        foreach (
            var chunk in segments.Keys.GroupBy(key => new Vector2I(
                Mathf.FloorToInt(key.X / 4f),
                Mathf.FloorToInt(key.Y / 4f)
            ))
        )
        {
            var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            int count = 0;
            foreach (var bucket in chunk)
                for (int z = bucket.Y * cellsPerBucket; z < (bucket.Y + 1) * cellsPerBucket; z++)
                for (int x = bucket.X * cellsPerBucket; x < (bucket.X + 1) * cellsPerBucket; x++)
                {
                    if (!roadCells.Has(x, z))
                        continue;
                    var a = Sample(x, z);
                    var b = Sample(x + 1, z);
                    var c = Sample(x, z + 1);
                    var d = Sample(x + 1, z + 1);
                    Emit(a, b, c);
                    Emit(b, d, c);
                }
            void Emit(FieldPoint a, FieldPoint b, FieldPoint c)
            {
                if (a.Distance >= 0 && b.Distance >= 0 && c.Distance >= 0)
                    return;
                var polygon = new List<FieldPoint>(4);
                var input = new[] { a, b, c };
                for (int i = 0; i < 3; i++)
                {
                    var p = input[i];
                    var q = input[(i + 1) % 3];
                    if (p.Distance <= 0)
                        polygon.Add(p);
                    if ((p.Distance < 0) != (q.Distance < 0))
                    {
                        float t = p.Distance / (p.Distance - q.Distance);
                        polygon.Add(
                            new FieldPoint(
                                p.Position.Lerp(q.Position, t),
                                0,
                                p.Uv.Lerp(q.Uv, t),
                                Mathf.Lerp(p.Marks, q.Marks, t),
                                Mathf.Lerp(p.Deck, q.Deck, t)
                            )
                        );
                    }
                }
                for (int i = 1; i + 1 < polygon.Count; i++)
                {
                    var points = new[] { polygon[0], polygon[i], polygon[i + 1] };
                    float area = (points[1].Position - points[0].Position).Cross(
                        points[2].Position - points[0].Position
                    );
                    if (Mathf.Abs(area) < .000001f)
                        continue;
                    var triangle = new Vector3[3];
                    for (int vertex = 0; vertex < 3; vertex++)
                    {
                        var point = points[vertex];
                        triangle[vertex] = terrain.At(point.Position.X, point.Position.Y, RoadLift);
                        // Blue tells the shader how near the edge is, where the verge's gravel begins.
                        surface.SetColor(new Color(point.Marks, 0, Verges.Depth(point.Distance)));
                        surface.SetUV(point.Uv);
                        surface.AddVertex(triangle[vertex]);
                    }
                    count++;
                    var min = points[0].Position.Min(points[1].Position).Min(points[2].Position);
                    var max = points[0].Position.Max(points[1].Position).Max(points[2].Position);
                    var entry = new SurfaceTriangle(
                        triangle[0],
                        triangle[1],
                        triangle[2],
                        BucketOf(min.X, min.Y),
                        BucketOf(max.X, max.Y)
                    );
                    var cellLow = TriangleCellOf(min.X - TriangleCellMargin, min.Y - TriangleCellMargin);
                    var cellHigh = TriangleCellOf(max.X + TriangleCellMargin, max.Y + TriangleCellMargin);
                    for (int cellZ = cellLow.Y; cellZ <= cellHigh.Y; cellZ++)
                    for (int cellX = cellLow.X; cellX <= cellHigh.X; cellX++)
                    {
                        var key = new Vector2I(cellX, cellZ);
                        if (!surfaceTriangles.TryGetValue(key, out var entries))
                            surfaceTriangles[key] = entries = new();
                        entries.Add(entry);
                    }
                }
            }
            if (count == 0)
                continue;
            surface.GenerateNormals();
            AddChild(
                new MeshInstance3D
                {
                    Mesh = surface.Commit(),
                    MaterialOverride = roadMaterial,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
        }
    }

    /// <summary>The field at a corner of the mesh grid, sampled once while the valley is generated.</summary>
    private FieldPoint Sample(int x, int z)
    {
        if (cachedField == null)
            return SampleField(new Vector2(x * MeshStep, z * MeshStep));
        var key = new Vector2I(x, z);
        if (!cachedField.TryGetValue(key, out var point))
            cachedField[key] = point = SampleField(new Vector2(x * MeshStep, z * MeshStep));
        return point;
    }

    /// <summary>
    /// The signed distance to the edge of the roads at a corner of the mesh grid (negative on a road), which the field
    /// ways of <see cref="Tracks"/> share, so their verges stop where a road begins.
    /// </summary>
    internal float FieldDistance(int x, int z) => Sample(x, z).Distance;

    /// <summary>
    /// The signed distance from <paramref name="p"/> to the edge of the roads: negative on a road, positive beside it.
    /// The grass asks it to leave the verge's gravel bare.
    /// </summary>
    internal float EdgeDistance(Vector3 p) => SampleField(new Vector2(p.X, p.Z)).Distance;

    /// <summary>
    /// Builds the gravel verge (<see cref="Verges"/>) along every road, once the ground beside the roads is final and
    /// the field ways are known. The verge leaves out the bridge's deck and fades out where a field way begins.
    /// </summary>
    internal void BuildVerges(Terrain terrain, Tracks? tracks)
    {
        if (tracks != null)
            foreach (var child in GetChildren())
                if (child is MeshInstance3D road)
                    FadeEdgeAtWays(road, tracks);
        var mask = MarkCells(Verges.Width);
        var material = Verges.CreateMaterial();
        var container = new Node3D { Name = "Verges" };
        AddChild(container);
        // Chunks of 48 cells (36 m) keep each mesh small enough to be culled away when it is out of sight.
        const int chunkCells = 48;
        var surfaces = new Dictionary<Vector2I, (SurfaceTool Surface, int Count)>();
        Verges.Corner Corner(int x, int z)
        {
            var point = Sample(x, z);
            if (tracks == null || point.Distance > Verges.Width + 1)
                return new Verges.Corner(point.Position, point.Distance, point.Deck);
            float way = tracks.OpenedDistance(point.Position, point.Distance);
            return new Verges.Corner(
                point.Position,
                point.Distance,
                Mathf.Min(point.Deck, way + Verges.FadeReach),
                Verges.FadeNear(way)
            );
        }
        for (int row = 0; row < mask.Rows; row++)
        for (int column = 0; column < mask.Columns; column++)
        {
            if (!mask.Cells[row * mask.Columns + column])
                continue;
            int x = mask.X + column,
                z = mask.Z + row;
            var a = Corner(x, z);
            var b = Corner(x + 1, z);
            var c = Corner(x, z + 1);
            var d = Corner(x + 1, z + 1);
            if (!Verges.Touches(a, b, c) && !Verges.Touches(b, d, c))
                continue;
            var key = new Vector2I(Mathf.FloorToInt(x / (float)chunkCells), Mathf.FloorToInt(z / (float)chunkCells));
            if (!surfaces.TryGetValue(key, out var entry))
            {
                entry = (new SurfaceTool(), 0);
                entry.Surface.Begin(Mesh.PrimitiveType.Triangles);
            }
            entry.Count += Verges.Emit(entry.Surface, terrain, RoadLift, a, b, c);
            entry.Count += Verges.Emit(entry.Surface, terrain, RoadLift, b, d, c);
            surfaces[key] = entry;
        }
        foreach (var (surface, count) in surfaces.Values)
        {
            if (count == 0)
                continue;
            surface.GenerateNormals();
            container.AddChild(
                new MeshInstance3D
                {
                    Mesh = surface.Commit(),
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
        }
    }

    /// <summary>
    /// Tells the road shader that the mouth of a field way is no edge, so the gravel of the road's last metre does not
    /// run across it. Only the vertex colours change; the triangles stay as they are.
    /// </summary>
    private static void FadeEdgeAtWays(MeshInstance3D road, Tracks tracks)
    {
        if (road.Mesh is not ArrayMesh mesh || mesh.GetSurfaceCount() != 1)
            return;
        var arrays = mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
        bool changed = false;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (colors[i].B >= 1)
                continue;
            var flat = new Vector2(vertices[i].X, vertices[i].Z);
            float way = tracks.OpenedDistance(flat, -colors[i].B * Verges.EdgeDepth);
            if (way > 1.2f)
                continue;
            colors[i].B = Mathf.Max(colors[i].B, 1 - Verges.FadeNear(way));
            changed = true;
        }
        if (!changed)
            return;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        var rebuilt = new ArrayMesh();
        rebuilt.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        road.Mesh = rebuilt;
    }

    private static IEnumerable<Vector2I> Buckets(Vector2 min, Vector2 max)
    {
        for (int z = Mathf.FloorToInt(min.Y / BucketSize); z <= Mathf.FloorToInt(max.Y / BucketSize); z++)
        for (int x = Mathf.FloorToInt(min.X / BucketSize); x <= Mathf.FloorToInt(max.X / BucketSize); x++)
            yield return new Vector2I(x, z);
    }

    public bool Contains(Vector3 p) => !float.IsNegativeInfinity(SurfaceHeight(p, float.NegativeInfinity));

    private static Vector2I BucketOf(float x, float z) =>
        new((int)System.MathF.Floor(x / BucketSize), (int)System.MathF.Floor(z / BucketSize));

    private static Vector2I TriangleCellOf(float x, float z) =>
        new((int)System.MathF.Floor(x / TriangleCell), (int)System.MathF.Floor(z / TriangleCell));

    public float SurfaceHeight(Vector3 p, float groundHeight)
    {
        float height = groundHeight;
        var flat = new Vector2(p.X, p.Z);
        var bucket = BucketOf(p.X, p.Z);
        if (!surfaceTriangles.TryGetValue(TriangleCellOf(p.X, p.Z), out var entries))
            return height;
        foreach (var triangle in entries)
        {
            if (
                bucket.X < triangle.MinBucket.X
                || bucket.X > triangle.MaxBucket.X
                || bucket.Y < triangle.MinBucket.Y
                || bucket.Y > triangle.MaxBucket.Y
            )
                continue;
            // The Vector2 cross products, in floats (see FlatMath): the grass asks for every blade.
            float abX = triangle.B.X - triangle.A.X,
                abZ = triangle.B.Z - triangle.A.Z;
            float acX = triangle.C.X - triangle.A.X,
                acZ = triangle.C.Z - triangle.A.Z;
            float apX = flat.X - triangle.A.X,
                apZ = flat.Y - triangle.A.Z;
            float area = abX * acZ - abZ * acX;
            if (System.MathF.Abs(area) < .000001f)
                continue;
            float u = (apX * acZ - apZ * acX) / area;
            float v = (abX * apZ - abZ * apX) / area;
            if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f)
                height = System.Math.Max(
                    height,
                    triangle.A.Y + u * (triangle.B.Y - triangle.A.Y) + v * (triangle.C.Y - triangle.A.Y)
                );
        }
        return height;
    }

    // Grid of sample indices for Distance(), kept as flat arrays; rebuilt whenever Samples has grown.
    private const float SampleCellSize = 16;
    private int[] cellStarts = System.Array.Empty<int>();
    private int[] cellSamples = System.Array.Empty<int>();
    private float[] sampleX = System.Array.Empty<float>();
    private float[] sampleZ = System.Array.Empty<float>();
    private int indexedSamples = -1;
    private Vector2I sampleCellMin,
        sampleCellMax;
    private int sampleColumns;

    private static Vector2I SampleCell(float x, float z) =>
        new((int)System.MathF.Floor(x / SampleCellSize), (int)System.MathF.Floor(z / SampleCellSize));

    private void IndexSamples()
    {
        sampleCellMin = new Vector2I(int.MaxValue, int.MaxValue);
        sampleCellMax = new Vector2I(int.MinValue, int.MinValue);
        for (int i = 0; i < Samples.Count; i++)
        {
            var cell = SampleCell(Samples[i].X, Samples[i].Z);
            sampleCellMin = sampleCellMin.Min(cell);
            sampleCellMax = sampleCellMax.Max(cell);
        }
        indexedSamples = Samples.Count;
        sampleX = new float[Samples.Count];
        sampleZ = new float[Samples.Count];
        if (Samples.Count == 0)
        {
            cellStarts = System.Array.Empty<int>();
            cellSamples = System.Array.Empty<int>();
            return;
        }
        sampleColumns = sampleCellMax.X - sampleCellMin.X + 1;
        int rows = sampleCellMax.Y - sampleCellMin.Y + 1;
        cellStarts = new int[sampleColumns * rows + 1];
        var cellOf = new int[Samples.Count];
        for (int i = 0; i < Samples.Count; i++)
        {
            sampleX[i] = Samples[i].X;
            sampleZ[i] = Samples[i].Z;
            var cell = SampleCell(sampleX[i], sampleZ[i]);
            cellOf[i] = (cell.Y - sampleCellMin.Y) * sampleColumns + cell.X - sampleCellMin.X;
            cellStarts[cellOf[i] + 1]++;
        }
        for (int cell = 0; cell < sampleColumns * rows; cell++)
            cellStarts[cell + 1] += cellStarts[cell];
        cellSamples = new int[Samples.Count];
        var fill = (int[])cellStarts.Clone();
        for (int i = 0; i < Samples.Count; i++)
            cellSamples[fill[cellOf[i]]++] = i;
    }

    /// <summary>
    /// Flat distance to the nearest road sample. Searches grid rings outwards and returns exactly the value
    /// of a full scan, so seeded placement stays unchanged.
    /// </summary>
    public float Distance(Vector3 p)
    {
        if (indexedSamples != Samples.Count)
            IndexSamples();
        if (Samples.Count == 0)
            return float.MaxValue;
        // The squares are compared and the root taken once: the root of the smallest square is the smallest root.
        float nearestSquared = float.MaxValue;
        var center = SampleCell(p.X, p.Z);
        int lastRing = System.Math.Max(
            System.Math.Max(System.Math.Abs(center.X - sampleCellMin.X), System.Math.Abs(center.X - sampleCellMax.X)),
            System.Math.Max(System.Math.Abs(center.Y - sampleCellMin.Y), System.Math.Abs(center.Y - sampleCellMax.Y))
        );
        for (int ring = 0; ring <= lastRing; ring++)
        {
            for (int z = center.Y - ring; z <= center.Y + ring; z++)
            {
                // Inner rows of a ring only contribute their two edge cells.
                int step = z == center.Y - ring || z == center.Y + ring ? 1 : System.Math.Max(1, 2 * ring);
                if (z < sampleCellMin.Y || z > sampleCellMax.Y)
                    continue;
                for (int x = center.X - ring; x <= center.X + ring; x += step)
                {
                    if (x < sampleCellMin.X || x > sampleCellMax.X)
                        continue;
                    int cell = (z - sampleCellMin.Y) * sampleColumns + x - sampleCellMin.X;
                    for (int k = cellStarts[cell]; k < cellStarts[cell + 1]; k++)
                    {
                        int i = cellSamples[k];
                        float dx = p.X - sampleX[i],
                            dz = p.Z - sampleZ[i];
                        float squared = dx * dx + dz * dz;
                        if (squared < nearestSquared)
                            nearestSquared = squared;
                    }
                }
            }
            // Every unvisited cell is at least `ring` whole cells away from p along X or Z.
            if (nearestSquared != float.MaxValue && System.MathF.Sqrt(nearestSquared) <= ring * SampleCellSize)
                break;
        }
        return nearestSquared == float.MaxValue ? float.MaxValue : System.MathF.Sqrt(nearestSquared);
    }
}
