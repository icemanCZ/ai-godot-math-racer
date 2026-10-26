using Godot;
using System.Collections.Generic;

namespace MathRacer;

public static class Art
{
    private static readonly Dictionary<string, StandardMaterial3D> Materials = new();
    private static readonly SphereMesh Sphere = new() { Radius = 1, Height = 2, RadialSegments = 12, Rings = 6 };
    private static readonly BoxMesh Box = new();
    public static StandardMaterial3D Mat(string color)
    {
        if (!Materials.TryGetValue(color, out var m))
        {
            m = new StandardMaterial3D { AlbedoColor = new Color(color).SrgbToLinear(), Roughness = .88f };
            Materials[color] = m;
        }
        return m;
    }
    public static MeshInstance3D Ball(Node3D parent, Vector3 pos, Vector3 scale, string color)
        => Mesh(parent, Sphere, pos, scale, color);
    public static MeshInstance3D Block(Node3D parent, Vector3 pos, Vector3 scale, string color)
        => Mesh(parent, Box, pos, scale, color);
    public static MeshInstance3D Mesh(Node3D parent, Mesh mesh, Vector3 pos, Vector3 scale, string color)
    {
        var instance = new MeshInstance3D { Mesh = mesh, Position = pos, Scale = scale, MaterialOverride = Mat(color) };
        parent.AddChild(instance);
        return instance;
    }
    public static MeshInstance3D Cylinder(Node3D parent, Vector3 pos, float bottom, float top, float height, string color)
        => Mesh(parent, new CylinderMesh { BottomRadius = bottom, TopRadius = top, Height = height, RadialSegments = 10 }, pos, Vector3.One, color);
    public static void Beam(Node3D parent, Vector3 from, Vector3 to, float width, string color)
    {
        var b = Block(parent, (from + to) / 2, new Vector3(width, width, from.DistanceTo(to)), color);
        b.LookAt(parent.ToGlobal(to), Vector3.Up);
    }
    public static Label3D Sign(Node3D parent, Vector3 pos, string text, int size = 44)
    {
        var label = new Label3D
        {
            Position = pos,
            Text = text,
            FontSize = size,
            PixelSize = .014f,
            OutlineSize = 7,
            Modulate = new Color("fff5d9"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false
        };
        parent.AddChild(label);
        return label;
    }
}

