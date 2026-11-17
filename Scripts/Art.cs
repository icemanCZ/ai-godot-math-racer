using System.Collections.Generic;
using Godot;

namespace MathRacer;

public static class Art
{
    private static readonly Dictionary<string, StandardMaterial3D> Materials = new();
    private static readonly SphereMesh Sphere = new()
    {
        Radius = 1,
        Height = 2,
        RadialSegments = 12,
        Rings = 6,
    };
    private static readonly BoxMesh Box = new();
    private static readonly Dictionary<string, ImageTexture> RoleIcons = new();

    public static Sprite3D RoleTaskIcon(Node3D parent, string icon)
    {
        if (!RoleIcons.TryGetValue(icon, out var texture))
        {
            // Vector outlines avoid platform-dependent, full-color emoji glyphs.
            string path = icon switch
            {
                "envelope" =>
                    "M29 33 H103 Q110 33 110 40 V96 Q110 103 103 103 H29 Q22 103 22 96 V40 Q22 33 29 33 Z M24 40 L61 70 Q66 74 71 70 L108 40",
                "basket" =>
                    "M34 56 L49 27 Q66 9 83 27 L98 56 M22 56 H110 L98 109 H34 Z M49 71 L53 95 M66 71 V95 M83 71 L79 95",
                "hand" =>
                    "M40 69 V35 Q40 25 50 25 Q57 25 57 35 V58 V22 Q57 12 66 12 Q75 12 75 22 V58 V29 Q75 19 84 19 Q93 19 93 29 V61 V43 Q93 33 102 33 Q111 33 111 43 V80 Q111 111 79 115 Q61 117 49 103 L24 75 Q17 65 25 59 Q32 54 40 69 Z",
                "cloud" =>
                    "M35 99 C22 99 15 91 15 80 C15 68 24 60 36 60 C39 44 51 34 66 34 C81 34 93 44 96 58 C108 58 117 67 117 79 C117 91 108 99 96 99 Z",
                "trailer" =>
                    "M14 40 H96 V82 H14 Z M96 70 H122 M24 96 A12 12 0 1 1 48 96 A12 12 0 1 1 24 96 Z M62 96 A12 12 0 1 1 86 96 A12 12 0 1 1 62 96 Z",
                "sorcerer" =>
                    "M23 99 Q66 112 109 99 L97 91 H35 Z M35 91 L62 23 Q66 14 71 23 L97 91 M52 75 L80 75 M66 45 V62 M58 53 H74",
                // Driving courses: the Rainbow Gates (nested arches) and the Hay Bale Slalom (cones and a winding line).
                "gates" =>
                    "M16 112 V70 A50 50 0 0 1 116 70 V112 M34 112 V72 A32 32 0 0 1 98 72 V112 M52 112 V74 A14 14 0 0 1 80 74 V112",
                "slalom" =>
                    "M14 114 H118 M24 114 L34 62 L44 114 M56 114 L66 62 L76 114 M88 114 L98 62 L108 114 M12 40 Q34 16 50 40 T82 40 T114 40",
                // Building sites: the bridge, the windmill, the lookout tower and the jetty.
                "bridge" =>
                    "M12 76 H120 M12 76 Q66 14 120 76 M30 55 V76 M48 41 V76 M66 36 V76 M84 41 V76 M102 55 V76 M12 104 Q30 94 48 104 T84 104 T120 104",
                "mill" =>
                    "M48 114 L56 62 H76 L84 114 Z M54 62 L66 46 L78 62 M66 46 L36 20 M66 46 L98 28 M66 46 L96 74 M66 46 L34 72 M62 114 V96 H70 V114",
                "tower" =>
                    "M46 114 L55 54 H77 L86 114 Z M48 54 H84 M54 54 V38 H78 V54 M50 38 L66 18 L82 38 Z M62 84 H70 V100 H62 Z",
                "jetty" =>
                    "M12 56 H120 M22 56 V98 M52 56 V98 M82 56 V98 M112 56 V98 M10 112 Q24 102 38 112 T66 112 T94 112 T122 112",
                // Raw material sources: the quarry, the clay pit, the sand pit and the log yard.
                "quarry" => "M12 106 L38 52 L58 80 L82 34 L120 106 Z M38 52 L46 106 M82 34 L72 106 M58 80 L64 106",
                "clay" => "M90 14 L56 82 M50 70 L76 86 L62 114 Q42 114 38 92 Z M12 118 Q32 108 52 118",
                "sand" => "M10 108 Q44 30 66 66 Q88 30 122 108 Z M30 92 Q42 80 54 92 M76 92 Q88 80 100 92",
                "logs" =>
                    "M21 96 A15 15 0 1 1 51 96 A15 15 0 1 1 21 96 Z M51 96 A15 15 0 1 1 81 96 A15 15 0 1 1 51 96 Z M81 96 A15 15 0 1 1 111 96 A15 15 0 1 1 81 96 Z M36 66 A15 15 0 1 1 66 66 A15 15 0 1 1 36 66 Z M66 66 A15 15 0 1 1 96 66 A15 15 0 1 1 66 66 Z M51 36 A15 15 0 1 1 81 36 A15 15 0 1 1 51 36 Z",
                // Works: the sawmill's blade, the brickworks' wall and the gravel works' hopper.
                "sawmill" =>
                    "M66 20 L75 33 L89 26 L90 42 L106 43 L99 57 L112 66 L99 75 L106 89 L90 90 L89 106 L75 99 L66 112 L57 99 L43 106 L42 90 L26 89 L33 75 L20 66 L33 57 L26 43 L42 42 L43 26 L57 33 Z M76 66 A10 10 0 1 1 56 66 A10 10 0 1 1 76 66 Z",
                "brickworks" =>
                    "M16 34 H116 V100 H16 Z M16 56 H116 M16 78 H116 M46 34 V56 M86 34 V56 M31 56 V78 M66 56 V78 M101 56 V78 M46 78 V100 M86 78 V100",
                "gravelworks" =>
                    "M22 22 H110 L84 70 H48 Z M48 70 V90 H84 V70 M20 118 A8 6 0 1 1 36 118 A8 6 0 1 1 20 118 Z M50 118 A8 6 0 1 1 66 118 A8 6 0 1 1 50 118 Z M80 118 A8 6 0 1 1 96 118 A8 6 0 1 1 80 118 Z",
                _ =>
                    "M81 29 A15 15 0 1 1 51 29 A15 15 0 1 1 81 29 Z M44 111 V77 L31 88 L22 77 L46 53 H86 L110 77 L101 88 L88 77 V111 H73 V88 H59 V111 Z",
            };
            string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' width='132' height='132' viewBox='0 0 132 132'>"
                + "<g fill='none' stroke-linecap='round' stroke-linejoin='round'>"
                + $"<path d='{path}' stroke='#241a10' stroke-width='15'/>"
                + $"<path d='{path}' stroke='#ffeb3b' stroke-width='7'/></g></svg>";
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
            NoDepthTest = false,
            Layers = SignLayer,
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

    public static MeshInstance3D Ball(Node3D parent, Vector3 pos, Vector3 scale, string color) =>
        Mesh(parent, Sphere, pos, scale, color);

    public static MeshInstance3D Block(Node3D parent, Vector3 pos, Vector3 scale, string color) =>
        Mesh(parent, Box, pos, scale, color);

    public static MeshInstance3D Mesh(Node3D parent, Mesh mesh, Vector3 pos, Vector3 scale, string color)
    {
        var instance = new MeshInstance3D
        {
            Mesh = mesh,
            Position = pos,
            Scale = scale,
            MaterialOverride = Mat(color),
        };
        parent.AddChild(instance);
        return instance;
    }

    public static MeshInstance3D Cylinder(
        Node3D parent,
        Vector3 pos,
        float bottom,
        float top,
        float height,
        string color
    ) =>
        Mesh(
            parent,
            new CylinderMesh
            {
                BottomRadius = bottom,
                TopRadius = top,
                Height = height,
                RadialSegments = 10,
            },
            pos,
            Vector3.One,
            color
        );

    /// <summary>A round log lying along <paramref name="axis"/> (in the parent's space) with pale cut ends.</summary>
    public static MeshInstance3D Log(
        Node3D parent,
        Vector3 center,
        Vector3 axis,
        float radius,
        float length,
        string color
    )
    {
        var log = Cylinder(parent, center, radius, radius, length, color);
        log.Quaternion = new Quaternion(Vector3.Up, axis.Normalized());
        foreach (float end in new[] { -1f, 1f })
            Cylinder(log, new Vector3(0, end * (length / 2 + .005f), 0), radius * .9f, radius * .9f, .02f, "dcb887");
        return log;
    }

    /// <summary>A tall, glowing, see-through column that marks a place from far away; it starts hidden.</summary>
    public static Node3D Beacon(Node3D parent, string color)
    {
        var beacon = new Node3D { Name = "Beacon", Visible = false };
        parent.AddChild(beacon);
        var column = Cylinder(beacon, new(0, 6, 0), .16f, .16f, 12, color);
        column.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(color).SrgbToLinear() with { A = .55f },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        column.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return beacon;
    }

    public static void Beam(Node3D parent, Vector3 from, Vector3 to, float width, string color)
    {
        var b = Block(parent, (from + to) / 2, new Vector3(width, width, from.DistanceTo(to)), color);
        b.LookAt(parent.ToGlobal(to), Vector3.Up);
    }

    internal const uint SignLayer = 1 << 1;

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
            NoDepthTest = false,
            Layers = SignLayer,
        };
        parent.AddChild(label);
        return label;
    }

    public static TaskMarker TaskMarker(Node3D parent, Vector3 pos, string text, bool destination = false)
    {
        // Fixed-size symbols grow in world space with distance. Keep the whole
        // symbol above its anchor so its lower half cannot sink into the ground.
        var marker = new TaskMarker
        {
            AnchorPosition = pos,
            Position = pos,
            Text = text,
            FontSize = 132,
            FixedSize = true,
            VerticalAlignment = VerticalAlignment.Bottom,
            PixelSize = .0011f,
            Modulate = new Color(destination ? "63e7e0" : "ffeb3b"),
            OutlineSize = 16,
            OutlineModulate = new Color("241a10"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = false,
            Layers = SignLayer,
        };
        parent.AddChild(marker);
        return marker;
    }
}
