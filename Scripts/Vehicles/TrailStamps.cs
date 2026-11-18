using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Garage;
using MathRacer.Valley;

namespace MathRacer.Vehicles;

/// <summary>
/// The flowers, hearts or stars that the rear wheels leave when a stamp trail is bought in the garage: one stamp for
/// about every metre and a quarter, on one side and then the other. Each pops up, stays a while and shrinks away. The
/// plain tyre marks are <see cref="WheelTracks"/>; this takes their place while a stamp trail is chosen.
/// </summary>
public partial class TrailStamps : MultiMeshInstance3D
{
    private const float Lifetime = 16;
    private const float Spacing = 1.25f;
    private const int Capacity = 480;
    private const float Radius = .5f;

    private readonly record struct Stamp(Vector3 Position, float Yaw, float Born);

    private readonly Queue<Stamp> stamps = new();
    private string style = "";
    private float time;
    private Vector3 last;
    private bool connected;
    private int placed;

    internal int Count => stamps.Count;
    internal string Style => style;

    public override void _Ready()
    {
        TopLevel = true;
        GlobalTransform = Transform3D.Identity;
        CastShadow = ShadowCastingSetting.Off;
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = Capacity,
        };
        Multimesh.VisibleInstanceCount = 0;
    }

    /// <summary>Chooses what is stamped; a different trail clears the stamps that are still on the ground.</summary>
    private void Choose(string id)
    {
        if (id == style)
            return;
        style = id;
        stamps.Clear();
        connected = false;
        Multimesh.VisibleInstanceCount = 0;
        if (id == "")
            return;
        Multimesh.Mesh = BuildMesh(id);
    }

    internal void Update(float dt, Vehicle car, Terrain terrain, Roads roads, string trail)
    {
        time += dt;
        Choose(trail == CarTrail.Plain.Id ? "" : trail);
        while (stamps.Count > 0 && time - stamps.Peek().Born > Lifetime)
            stamps.Dequeue();
        if (style != "")
            Lay(car, terrain, roads);
        Write();
    }

    private void Lay(Vehicle car, Terrain terrain, Roads roads)
    {
        if (!car.IsOnFloor())
        {
            connected = false;
            return;
        }
        var wheels = new[] { car.ToGlobal(new Vector3(-.9875f, 0, .96f)), car.ToGlobal(new Vector3(.9875f, 0, .96f)) };
        var middle = (wheels[0] + wheels[1]) / 2;
        bool wet = wheels.Any(p => Submerged(p, terrain));
        if (wet)
        {
            connected = false;
            return;
        }
        if (!connected)
        {
            last = middle;
            connected = true;
            return;
        }
        if (FlatDistance(middle, last) < Spacing)
            return;
        last = middle;
        var wheel = wheels[placed++ % 2];
        float height = roads.SurfaceHeight(wheel, terrain.Height(wheel.X, wheel.Z));
        if (River.OnBridge(wheel.X, wheel.Z))
            height = Mathf.Max(height, River.BridgeSurfaceHeight(wheel.X));
        stamps.Enqueue(
            new Stamp(new Vector3(wheel.X, height + .06f, wheel.Z), car.Heading + Mathf.Sin(placed * 2.1f) * .3f, time)
        );
        while (stamps.Count > Capacity)
            stamps.Dequeue();
    }

    /// <summary>Writes the transform of every stamp: a quick pop to full size and a slow shrink at the end.</summary>
    private void Write()
    {
        int i = 0;
        foreach (var stamp in stamps)
        {
            float age = time - stamp.Born;
            float grow = Mathf.Min(1, age / .25f);
            grow = 1 - Mathf.Pow(1 - grow, 3);
            float fade = Mathf.Clamp((Lifetime - age) / 2.5f, 0, 1);
            float size = Radius * grow * fade;
            var basis = new Basis(Vector3.Up, stamp.Yaw).Scaled(Vector3.One * size);
            Multimesh.SetInstanceTransform(i++, new Transform3D(basis, stamp.Position));
        }
        Multimesh.VisibleInstanceCount = i;
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private static bool Submerged(Vector3 p, Terrain terrain) =>
        !River.OnBridge(p.X, p.Z)
        && River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 2
        && terrain.Height(p.X, p.Z) < River.Level(p.Z);

    /// <summary>One flat mesh with the pieces of the shape, each piece a little higher than the one before it.</summary>
    internal static ArrayMesh BuildMesh(string id)
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var normals = new List<Vector3>();
        var indices = new List<int>();
        float layer = 0;
        foreach (var piece in TrailShapes.Of(id))
        {
            int first = vertices.Count;
            foreach (var point in piece.Outline)
            {
                // The outline's +Y is the front of the stamp, which is -Z on the ground.
                vertices.Add(new Vector3(point.X, layer, point.Y));
                colors.Add(piece.Color);
                normals.Add(Vector3.Up);
            }
            var triangles = Geometry2D.TriangulatePolygon(piece.Outline);
            // Triangulation winds one way or the other; the material shows both faces, so either is fine.
            foreach (int index in triangles)
                indices.Add(first + index);
            layer += .012f;
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(
            0,
            new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                Roughness = 1,
            }
        );
        return mesh;
    }
}
