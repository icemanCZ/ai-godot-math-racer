using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// Off-network ways draped over the terrain: dirt field paths to the quarry and pits, and the roads that
/// lead to both ends of the beaver's bridge. They are visual only and never change the road network,
/// so seeded placement and navigation stay as they were.
/// </summary>
public partial class Tracks : Node3D
{
    internal enum Kind
    {
        Road,
        Path,
    }

    private const float MeshStep = .75f;

    /// <summary>A straight piece, or a round pad when <see cref="A"/> equals <see cref="B"/>.</summary>
    private readonly record struct Piece(Vector2 A, Vector2 B, float Width, float Along, Kind Kind);

    /// <summary>
    /// <see cref="Yard"/> is 1 on round pads, where no wheel ruts are drawn; <see cref="Mouth"/> rises to 1 where a
    /// field path meets a road.
    /// </summary>
    private readonly record struct FieldPoint(
        Vector2 Position,
        float Distance,
        Vector2 Uv,
        float Yard,
        float Mouth = 0
    );

    /// <summary>How much wider a field path opens where it meets a road, on each side.</summary>
    private const float MouthFlare = 1.9f;

    /// <summary>How far from the road's edge a field path begins to open out.</summary>
    private const float MouthReach = 4.5f;

    /// <summary>The road network, which the field paths open out to; known once <see cref="Build"/> has run.</summary>
    private Roads? routes;

    /// <summary>How far a field path opens out at <paramref name="roadDistance"/> from the road's edge: a rounded bell.</summary>
    private static float Flare(float roadDistance)
    {
        float opening = 1 - Mathf.SmoothStep(0, MouthReach, roadDistance);
        return MouthFlare * opening * opening;
    }

    private readonly List<Piece> pieces = new();

    internal int Count => pieces.Count;

    /// <summary>Adds a straight way from <paramref name="from"/> to <paramref name="to"/>.</summary>
    internal void Line(Vector3 from, Vector3 to, float halfWidth, Kind kind, float along = 0) =>
        pieces.Add(new Piece(new(from.X, from.Z), new(to.X, to.Z), halfWidth, along, kind));

    /// <summary>Adds a round widening, such as a loading yard.</summary>
    internal void Pad(Vector3 center, float radius, Kind kind) =>
        pieces.Add(new Piece(new(center.X, center.Z), new(center.X, center.Z), radius, 0, kind));

    /// <summary>
    /// Whether <paramref name="p"/> is on a field way: <c>Field(p, null).Distance &lt; 0</c>, which is so exactly when
    /// one piece is nearer than its width. The grass asks for every blade, so this skips the texture coordinates and
    /// works in floats (see <see cref="FlatMath"/>).
    /// </summary>
    public bool Contains(Vector3 p)
    {
        float px = p.X,
            pz = p.Z;
        foreach (var piece in pieces)
        {
            float ax = piece.A.X,
                az = piece.A.Y;
            float dx = piece.B.X - ax,
                dz = piece.B.Y - az;
            float lengthSquared = dx * dx + dz * dz;
            float t =
                lengthSquared < .000001f ? 0 : FlatMath.Clamp(((px - ax) * dx + (pz - az) * dz) / lengthSquared, 0, 1);
            if (FlatMath.Length(px - (ax + dx * t), pz - (az + dz * t)) - piece.Width < 0)
                return true;
        }
        return false;
    }

    private FieldPoint Field(Vector2 point, Kind? kind, float flare = 0)
    {
        float best = 1000;
        var uv = Vector2.Zero;
        float yard = 0;
        foreach (var piece in pieces)
        {
            if (kind != null && piece.Kind != kind)
                continue;
            var delta = piece.B - piece.A;
            float lengthSquared = delta.LengthSquared();
            float t = lengthSquared < .000001f ? 0 : Mathf.Clamp((point - piece.A).Dot(delta) / lengthSquared, 0, 1);
            float width = piece.Width + flare;
            float signed = point.DistanceTo(piece.A + delta * t) - width;
            if (signed >= best)
                continue;
            best = signed;
            yard = lengthSquared < .000001f ? 1 : 0;
            float length = Mathf.Sqrt(lengthSquared);
            uv =
                length > .0001f
                    ? new Vector2((point - piece.A).Cross(delta / length) / width, piece.Along + t * length)
                    : new Vector2(point.DistanceTo(piece.A) / width, 0);
        }
        return new FieldPoint(point, best, uv, yard);
    }

    /// <summary>
    /// The signed distance from <paramref name="point"/> to the nearest way of any kind (negative on one), as
    /// <c>Field(point, null).Distance</c> without the texture coordinates: it is asked for every vertex of the roads.
    /// </summary>
    internal float Distance(Vector2 point) => DistanceTo(point.X, point.Y, null);

    /// <summary>
    /// The signed distance from <paramref name="p"/> to the edge of the roads among the ways (negative on one); the
    /// grass leaves their verges bare as it does the network's.
    /// </summary>
    internal float RoadDistance(Vector3 p) => DistanceTo(p.X, p.Z, Kind.Road);

    private float DistanceTo(float px, float pz, Kind? kind)
    {
        float best = float.MaxValue;
        foreach (var piece in pieces)
        {
            if (kind != null && piece.Kind != kind)
                continue;
            float ax = piece.A.X,
                az = piece.A.Y;
            float dx = piece.B.X - ax,
                dz = piece.B.Y - az;
            float lengthSquared = dx * dx + dz * dz;
            float t =
                lengthSquared < .000001f ? 0 : FlatMath.Clamp(((px - ax) * dx + (pz - az) * dz) / lengthSquared, 0, 1);
            best = System.Math.Min(best, FlatMath.Length(px - (ax + dx * t), pz - (az + dz * t)) - piece.Width);
        }
        return best;
    }

    /// <summary>
    /// Builds the gravel verge (<see cref="Verges"/>) along the roads among the ways. It stops where the road network
    /// or a field path begins, as the network's own verge stops at these ways.
    /// </summary>
    internal void BuildVerges(Terrain terrain, Roads routes)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var piece in pieces)
            if (piece.Kind == Kind.Road)
            {
                min = min.Min(piece.A.Min(piece.B) - Vector2.One * (piece.Width + Verges.Width + 1));
                max = max.Max(piece.A.Max(piece.B) + Vector2.One * (piece.Width + Verges.Width + 1));
            }
        if (min.X > max.X)
            return;
        var corners = new Dictionary<Vector2I, Verges.Corner>();
        Verges.Corner Corner(int x, int z)
        {
            var key = new Vector2I(x, z);
            if (corners.TryGetValue(key, out var corner))
                return corner;
            var point = new Vector2(x * MeshStep, z * MeshStep);
            float distance = Field(point, Kind.Road).Distance;
            if (distance > Verges.Width + 1)
                return corners[key] = new Verges.Corner(point, distance, 1);
            float path = DistanceTo(point.X, point.Y, Kind.Path);
            return corners[key] = new Verges.Corner(
                point,
                distance,
                Mathf.Min(routes.FieldDistance(x, z), path + Verges.FadeReach),
                Verges.FadeNear(path)
            );
        }
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        int count = 0;
        for (int z = Mathf.FloorToInt(min.Y / MeshStep); z <= Mathf.CeilToInt(max.Y / MeshStep); z++)
        for (int x = Mathf.FloorToInt(min.X / MeshStep); x <= Mathf.CeilToInt(max.X / MeshStep); x++)
        {
            var a = Corner(x, z);
            var b = Corner(x + 1, z);
            var c = Corner(x, z + 1);
            var d = Corner(x + 1, z + 1);
            count += Verges.Emit(surface, terrain, RoadLift, a, b, c);
            count += Verges.Emit(surface, terrain, RoadLift, b, d, c);
        }
        if (count == 0)
            return;
        surface.GenerateNormals();
        AddChild(
            new MeshInstance3D
            {
                Name = "RoadVerges",
                Mesh = surface.Commit(),
                MaterialOverride = Verges.CreateMaterial(),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
    }

    private const float RoadLift = .12f;

    /// <summary>
    /// Where the meadow's grass may not grow near a field path's mouth, which opens out wider than the path itself
    /// (<see cref="Contains"/>, which placement asks, keeps to the path): the signed distance to the opened-out path.
    /// </summary>
    internal float MouthDistance(Vector3 p, float roadDistance) => MouthDistance(p.X, p.Z, roadDistance);

    /// <summary>
    /// The signed distance to the nearest way as it is drawn, with the field paths opened out at their mouths, at
    /// <paramref name="roadDistance"/> from the road's edge. The roads' gravel fades out against it.
    /// </summary>
    internal float OpenedDistance(Vector2 point, float roadDistance) =>
        System.Math.Min(DistanceTo(point.X, point.Y, Kind.Road), MouthDistance(point.X, point.Y, roadDistance));

    private float MouthDistance(float x, float z, float roadDistance) =>
        roadDistance > MouthReach ? DistanceTo(x, z, Kind.Path) : DistanceTo(x, z, Kind.Path) - Flare(roadDistance);

    public void Build(Terrain terrain, Roads routes)
    {
        this.routes = routes;
        BuildKind(
            terrain,
            Kind.Road,
            RoadLift,
            new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/RoadSurface.gdshader") }
        );
        BuildKind(
            terrain,
            Kind.Path,
            .11f,
            new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/FieldPath.gdshader") }
        );
    }

    private void BuildKind(Terrain terrain, Kind kind, float lift, Material material)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        foreach (var piece in pieces)
            if (piece.Kind == kind)
            {
                min = min.Min(piece.A.Min(piece.B) - Vector2.One * (piece.Width + MouthFlare + 1));
                max = max.Max(piece.A.Max(piece.B) + Vector2.One * (piece.Width + MouthFlare + 1));
            }
        if (min.X > max.X)
            return;
        var cache = new Dictionary<Vector2I, FieldPoint>();
        FieldPoint Sample(int x, int z)
        {
            var key = new Vector2I(x, z);
            if (cache.TryGetValue(key, out var point))
                return point;
            var position = new Vector2(x * MeshStep, z * MeshStep);
            if (kind != Kind.Path || routes == null)
                return cache[key] = Field(position, kind);
            // A field path opens out into a rounded mouth where it meets a road, and there turns into the road's dirt.
            float roadDistance = routes.FieldDistance(x, z);
            float flare = roadDistance > MouthReach ? 0 : Flare(roadDistance);
            float mouth = 1 - Mathf.SmoothStep(0, MouthReach * .8f, roadDistance);
            return cache[key] = Field(position, kind, flare) with { Mouth = mouth };
        }
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        int count = 0;
        int firstX = Mathf.FloorToInt(min.X / MeshStep),
            lastX = Mathf.CeilToInt(max.X / MeshStep);
        int firstZ = Mathf.FloorToInt(min.Y / MeshStep),
            lastZ = Mathf.CeilToInt(max.Y / MeshStep);
        // A cell only produces triangles when one of its corners lies inside a piece, and the pieces cover a small part
        // of their bounding box. Mark the cells around each piece and leave the rest of the box alone.
        int columns = lastX - firstX + 1;
        var near = new bool[columns * (lastZ - firstZ + 1)];
        foreach (var piece in pieces)
        {
            if (piece.Kind != kind)
                continue;
            float reach = piece.Width + (kind == Kind.Path ? MouthFlare : 0);
            var low = piece.A.Min(piece.B) - Vector2.One * reach;
            var high = piece.A.Max(piece.B) + Vector2.One * reach;
            // A corner inside the piece belongs to the cell on either side of it, hence one cell more on every side.
            int fromX = Mathf.Max(firstX, Mathf.FloorToInt(low.X / MeshStep) - 2),
                toX = Mathf.Min(lastX, Mathf.CeilToInt(high.X / MeshStep) + 1);
            int fromZ = Mathf.Max(firstZ, Mathf.FloorToInt(low.Y / MeshStep) - 2),
                toZ = Mathf.Min(lastZ, Mathf.CeilToInt(high.Y / MeshStep) + 1);
            for (int markZ = fromZ; markZ <= toZ; markZ++)
            for (int markX = fromX; markX <= toX; markX++)
                near[(markZ - firstZ) * columns + markX - firstX] = true;
        }
        for (int z = firstZ; z <= lastZ; z++)
        for (int x = firstX; x <= lastX; x++)
        {
            if (!near[(z - firstZ) * columns + x - firstX])
                continue;
            var a = Sample(x, z);
            var b = Sample(x + 1, z);
            var c = Sample(x, z + 1);
            var d = Sample(x + 1, z + 1);
            Emit(a, b, c);
            Emit(b, d, c);
        }
        // Same contour clipping as the road network, so the edges follow the field smoothly.
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
                            Mathf.Lerp(p.Yard, q.Yard, t),
                            Mathf.Lerp(p.Mouth, q.Mouth, t)
                        )
                    );
                }
            }
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                var points = new[] { polygon[0], polygon[i], polygon[i + 1] };
                float area = (points[1].Position - points[0].Position).Cross(points[2].Position - points[0].Position);
                if (Mathf.Abs(area) < .000001f)
                    continue;
                foreach (var point in points)
                {
                    // The road shader draws ruts by red (yards get none) and the gravel of the edge by blue; the
                    // field path shader reads the yards from green and a path's mouth from blue.
                    float blue = kind == Kind.Road ? Verges.Depth(point.Distance) : point.Mouth;
                    surface.SetColor(new Color(1 - point.Yard, point.Yard, blue));
                    surface.SetUV(point.Uv);
                    surface.AddVertex(terrain.At(point.Position.X, point.Position.Y, lift));
                }
                count++;
            }
        }
        if (count == 0)
            return;
        surface.GenerateNormals();
        AddChild(
            new MeshInstance3D
            {
                Name = kind + "Tracks",
                Mesh = surface.Commit(),
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
    }
}
