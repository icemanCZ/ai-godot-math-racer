using Godot;

namespace MathRacer;

// Simple solid shapes independent of the batched decorative meshes.
internal static class Obstacles
{
    internal static StaticBody3D Add(Node3D parent, Vector3 center, Shape3D shape, Basis? basis = null)
    {
        var body = new StaticBody3D { Transform = new Transform3D(basis ?? Basis.Identity, center) };
        body.AddToGroup("solid_obstacles");
        parent.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = shape });
        body.AddToGroup(shape is CapsuleShape3D ? "obstacle_trunks" : shape is ConvexPolygonShape3D ? "obstacle_rocks" : "obstacle_boxes");
        return body;
    }

    internal static void Box(Node3D parent, Vector3 center, Vector3 size, Basis? basis = null)
        => Add(parent, center, new BoxShape3D { Size = size }, basis);

    internal static StaticBody3D Trunk(Node3D parent, Vector3 foot, float radius, float height)
        => Add(parent, foot + Vector3.Up * (height / 2), new CapsuleShape3D { Radius = radius, Height = height });

    internal static void Rock(Node3D parent, Vector3 center, Vector3 size)
    {
        // Upright sides prevent the rounded ground contact from treating rocks as
        // tiny ramps. The octagonal footprint stays inside the visible silhouette.
        var points = new Vector3[16];
        for (int ring = 0; ring < 2; ring++)
        for (int side = 0; side < 8; side++)
        {
            float angle = side * Mathf.Tau / 8;
            points[ring * 8 + side] = new Vector3(.85f * Mathf.Cos(angle),
                ring == 0 ? -1 : 1, .85f * Mathf.Sin(angle)) * size;
        }
        Add(parent, center, new ConvexPolygonShape3D { Points = points });
    }
}
