using System.Collections.Generic;
using Godot;

namespace MathRacer;

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

    /// <summary><see cref="Yard"/> is 1 on round pads, where no wheel ruts are drawn.</summary>
    private readonly record struct FieldPoint(Vector2 Position, float Distance, Vector2 Uv, float Yard);

    private readonly List<Piece> pieces = new();

    internal int Count => pieces.Count;

    /// <summary>Adds a straight way from <paramref name="from"/> to <paramref name="to"/>.</summary>
    internal void Line(Vector3 from, Vector3 to, float halfWidth, Kind kind, float along = 0) =>
        pieces.Add(new Piece(new(from.X, from.Z), new(to.X, to.Z), halfWidth, along, kind));

    /// <summary>Adds a round widening, such as a loading yard.</summary>
    internal void Pad(Vector3 center, float radius, Kind kind) =>
        pieces.Add(new Piece(new(center.X, center.Z), new(center.X, center.Z), radius, 0, kind));

    public bool Contains(Vector3 p) => Field(new Vector2(p.X, p.Z), null).Distance < 0;

    private FieldPoint Field(Vector2 point, Kind? kind)
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
            float signed = point.DistanceTo(piece.A + delta * t) - piece.Width;
            if (signed >= best)
                continue;
            best = signed;
            yard = lengthSquared < .000001f ? 1 : 0;
            float length = Mathf.Sqrt(lengthSquared);
            uv =
                length > .0001f
                    ? new Vector2((point - piece.A).Cross(delta / length) / piece.Width, piece.Along + t * length)
                    : new Vector2(point.DistanceTo(piece.A) / piece.Width, 0);
        }
        return new FieldPoint(point, best, uv, yard);
    }

    public void Build(Terrain terrain)
    {
        BuildKind(
            terrain,
            Kind.Road,
            .12f,
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
                min = min.Min(piece.A.Min(piece.B) - Vector2.One * (piece.Width + 1));
                max = max.Max(piece.A.Max(piece.B) + Vector2.One * (piece.Width + 1));
            }
        if (min.X > max.X)
            return;
        var cache = new Dictionary<Vector2I, FieldPoint>();
        FieldPoint Sample(int x, int z)
        {
            var key = new Vector2I(x, z);
            if (!cache.TryGetValue(key, out var point))
                cache[key] = point = Field(new Vector2(x * MeshStep, z * MeshStep), kind);
            return point;
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
            var low = piece.A.Min(piece.B) - Vector2.One * piece.Width;
            var high = piece.A.Max(piece.B) + Vector2.One * piece.Width;
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
                            Mathf.Lerp(p.Yard, q.Yard, t)
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
                    // The road shader draws ruts and verges by red; yards get neither.
                    surface.SetColor(new Color(1 - point.Yard, point.Yard, 0));
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
