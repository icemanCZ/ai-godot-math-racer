using Godot;
using System.Collections.Generic;

namespace MathRacer;

public static class Art
{
    private static readonly Dictionary<string, StandardMaterial3D> Materials = new();
    private static readonly SphereMesh Sphere = new() { Radius = 1, Height = 2, RadialSegments = 12, Rings = 6 };
    private static readonly BoxMesh Box = new();
    private static readonly Dictionary<string, ImageTexture> RoleIcons = new();
    public static Sprite3D RoleTaskIcon(Node3D parent, string icon)
    {
        if (!RoleIcons.TryGetValue(icon, out var texture))
        {
            // Vector outlines avoid platform-dependent, full-color emoji glyphs.
            string path = icon switch
            {
                "basket" => "M34 56 L49 27 Q66 9 83 27 L98 56 M22 56 H110 L98 109 H34 Z M49 71 L53 95 M66 71 V95 M83 71 L79 95",
                "hand" => "M40 69 V35 Q40 25 50 25 Q57 25 57 35 V58 V22 Q57 12 66 12 Q75 12 75 22 V58 V29 Q75 19 84 19 Q93 19 93 29 V61 V43 Q93 33 102 33 Q111 33 111 43 V80 Q111 111 79 115 Q61 117 49 103 L24 75 Q17 65 25 59 Q32 54 40 69 Z",
                _ => "M81 29 A15 15 0 1 1 51 29 A15 15 0 1 1 81 29 Z M44 111 V77 L31 88 L22 77 L46 53 H86 L110 77 L101 88 L88 77 V111 H73 V88 H59 V111 Z"
            };
            string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='132' height='132' viewBox='0 0 132 132'>" +
                "<g fill='none' stroke-linecap='round' stroke-linejoin='round'>" +
                $"<path d='{path}' stroke='#241a10' stroke-width='15'/>" +
                $"<path d='{path}' stroke='#ffeb3b' stroke-width='7'/></g></svg>";
            var image = new Image();
            image.LoadSvgFromString(svg, 2);
            texture = ImageTexture.CreateFromImage(image);
            RoleIcons[icon] = texture;
        }
        var marker = new Sprite3D
        {
            Texture = texture,
            PixelSize = .00055f,
            Offset = new Vector2(0, 132),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FixedSize = true,
            Shaded = false,
            NoDepthTest = false
        };
        parent.AddChild(marker);
        return marker;
    }
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
    public static Label3D TaskMarker(Node3D parent, Vector3 pos, string text, bool destination = false)
    {
        var marker = Sign(parent, pos, text, 132);
        marker.FixedSize = true;
        // Fixed-size symbols grow in world space with distance. Keep the whole
        // symbol above its anchor so its lower half cannot sink into the ground.
        marker.VerticalAlignment = VerticalAlignment.Bottom;
        marker.PixelSize = .0011f;
        marker.Modulate = new Color(destination ? "63e7e0" : "ffeb3b");
        marker.OutlineSize = 16;
        marker.OutlineModulate = new Color("241a10");
        return marker;
    }
}

