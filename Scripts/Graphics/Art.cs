using System.Collections.Generic;
using Godot;
using MathRacer.Ui;

namespace MathRacer.Graphics;

/// <summary>The kinds of surface <see cref="Art.Facade"/> draws; the numbers are the shader's <c>style</c>.</summary>
public enum FacadeStyle
{
    Plaster,
    Bricks,
    Boards,
    Siding,
    Shingles,
}

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
                // A building plot of the village: a little house with a chimney.
                "plot" => "M16 68 L66 22 L116 68 M30 58 V112 H102 V58 M54 112 V82 H78 V112 M82 30 V46 M92 30 V54",
                // The mayor, who sells decorations: a pennant on a pole.
                "flag" => "M30 118 V14 M30 20 H104 L86 44 L104 68 H30",
                // The glass lamp maker: a lamp with a tulip shade on a pole.
                "lamp" => "M66 118 V56 M44 118 H88 M38 30 Q38 12 66 12 Q94 12 94 30 L82 56 H50 Z M54 30 H78",
                // The hunter, who sends the player out to catch animals: a landing net on a pole.
                "hunter" =>
                    "M20 114 L70 62 M62 44 A26 26 0 1 1 114 44 A26 26 0 1 1 62 44 Z M70 28 L106 60 M70 60 L106 28 M88 18 V70 M62 44 H114",
                // The pirate captain, who hides treasures: a chest with a round lid and a lock.
                "treasure" =>
                    "M18 58 Q18 28 66 28 Q114 28 114 58 Z M18 58 V108 H114 V58 M18 80 H114 M56 70 H76 V92 H56 Z",
                // The playground: a swing set with a seat.
                "playground" =>
                    "M12 114 L36 24 L60 114 M72 114 L96 24 L120 114 M36 24 H96 M56 24 V84 M76 24 V84 M50 84 H82",
                // The little railway: a steam engine with a chimney and two wheels, on a rail.
                "railway" =>
                    "M12 92 V58 H62 V34 H94 V58 H120 V92 Z M26 58 V38 H44 V58 M30 108 a11 11 0 1 0 22 0 a11 11 0 1 0 -22 0 M74 108 a11 11 0 1 0 22 0 a11 11 0 1 0 -22 0 M6 122 H126",
                // The zoo: the head of an elephant with big ears, a trunk and two eyes.
                "zoo" =>
                    "M20 52 a20 28 0 1 0 0 40 M112 52 a20 28 0 1 1 0 40 M34 38 Q66 6 98 38 L98 74 Q98 96 80 100 L80 122 Q80 128 70 128 Q58 128 58 122 L58 100 Q34 96 34 74 Z M52 56 a4 4 0 1 0 8 0 a4 4 0 1 0 -8 0 M72 56 a4 4 0 1 0 8 0 a4 4 0 1 0 -8 0",
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
                // The bakery's loaf and the farmer's stalk of wheat.
                "bakery" =>
                    "M14 88 Q14 44 66 44 Q118 44 118 88 Q118 104 102 104 H30 Q14 104 14 88 Z M42 56 L54 80 M64 52 L76 80 M86 56 L98 80",
                "wheat" =>
                    "M66 120 V34 M66 34 Q48 30 46 16 M66 34 Q84 30 86 16 M66 58 Q46 54 42 40 M66 58 Q88 54 92 40 M66 82 Q46 78 42 64 M66 82 Q88 78 92 64",
                "glassworks" => "M38 18 H94 L86 70 Q84 90 66 94 Q48 90 46 70 Z M48 40 H84 M66 94 V112 M42 116 H90",
                // The post and the borders: a letter with a stamp, the lighthouse with its beam, the four borders (waves,
                // snowy peaks, a river with a boat, fir trees) and a postage stamp with a perforated edge.
                "letter" => "M14 34 H118 V102 H14 Z M88 44 H106 V64 H88 Z M28 72 H72 M28 88 H62",
                "lighthouse" =>
                    "M50 120 L58 52 H74 L82 120 Z M54 96 H78 M56 74 H76 M56 52 V38 H76 V52 M54 38 L66 26 L78 38 M80 40 L122 28 M80 48 L122 60 M52 40 L10 28 M52 48 L10 60",
                "sea" =>
                    "M14 44 Q27 30 40 44 T66 44 T92 44 T118 44 M14 72 Q27 58 40 72 T66 72 T92 72 T118 72 M14 100 Q27 86 40 100 T66 100 T92 100 T118 100",
                "mountains" =>
                    "M8 112 L46 40 L84 112 M62 112 L92 56 L124 112 M34 62 L40 58 L46 66 L52 58 L58 62 M80 78 L86 72 L92 80 L98 72 L104 78",
                "river" =>
                    "M6 34 C40 14 62 54 126 34 M6 98 C40 78 62 118 126 98 M44 62 H88 L80 74 H52 Z M66 62 V42 L80 58",
                "forest" =>
                    "M38 118 V102 M14 102 L38 18 L62 102 Z M94 118 V102 M70 102 L94 30 L118 102 Z M24 72 H52 M80 76 H108",
                "stamp" => StampPath,
                // The dragon under the mountains: a head with a horn, an eye, a nostril and a ring of smoke.
                "dragon" =>
                    "M18 104 Q22 58 62 50 L70 28 L80 50 Q104 52 112 72 Q116 86 100 90 H70 Q58 96 58 112 M88 66 h0 M104 80 h0 M110 40 a10 10 0 1 0 20 0 a10 10 0 1 0 -20 0",
                // The balloon station: an envelope with its gores, the ropes and the basket.
                "balloon" =>
                    "M66 12 C28 12 20 50 34 70 L54 94 H78 L98 70 C112 50 104 12 66 12 Z M66 12 C52 30 54 70 60 94 M66 12 C80 30 78 70 72 94 M56 104 H76 V120 H56 Z M56 94 V104 M76 94 V104",
                // The owl's map of the valley: a folded sheet of paper with a dotted way and a cross on it.
                "map" =>
                    "M16 30 L50 18 L82 30 L116 18 V102 L82 114 L50 102 L16 114 Z M50 18 V102 M82 30 V114 M28 88 Q40 70 56 76 T84 60 M92 42 L104 54 M104 42 L92 54",
                // The animal at the end of a trail of the forest keeper: a paw print, a pad and four toes.
                "paw" =>
                    "M40 98 Q40 72 66 72 Q92 72 92 98 Q92 114 66 114 Q40 114 40 98 Z M20 60 a10 12 0 1 0 20 0 a10 12 0 1 0 -20 0 M42 38 a10 12 0 1 0 20 0 a10 12 0 1 0 -20 0 M70 38 a10 12 0 1 0 20 0 a10 12 0 1 0 -20 0 M92 60 a10 12 0 1 0 20 0 a10 12 0 1 0 -20 0",
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

    /// <summary>The outline of a postage stamp: a frame with a hill in it and a ring of dots for the perforated edge.</summary>
    private static readonly string StampPath = Perforated();

    private static string Perforated()
    {
        var path = new System.Text.StringBuilder("M34 34 H98 V98 H34 Z M44 88 L60 62 L72 76 L80 66 L90 88");
        for (int i = 0; i < 7; i++)
            path.Append($" M{18 + i * 16} 18 h0 M{18 + i * 16} 114 h0");
        for (int i = 1; i < 6; i++)
            path.Append($" M18 {18 + i * 16} h0 M114 {18 + i * 16} h0");
        return path.ToString();
    }

    private static readonly Dictionary<string, ImageTexture?> Pictures = new();

    /// <summary>
    /// A coloured picture for a <c>DeliveryPicture</c> (180 x 150, the frame it draws in) of the post and the borders:
    /// "letter", "lighthouse", "sea", "mountains", "river", "forest" and "stamp", of the dragon: "dragon" and "egg", and of
    /// the balloon station: "balloon" and "star". Null for any other name, which the picture draws itself.
    /// </summary>
    public static Texture2D? Picture(string icon)
    {
        if (Pictures.TryGetValue(icon, out var cached))
            return cached;
        const string ink = "#3c5960";
        string? body = icon switch
        {
            "letter" =>
                $"<rect x='24' y='36' width='132' height='86' rx='8' fill='#fff5df' stroke='{ink}' stroke-width='6'/>"
                    + $"<path d='M28 42 L90 86 L152 42' fill='none' stroke='{ink}' stroke-width='6' stroke-linejoin='round'/>"
                    + "<rect x='118' y='46' width='26' height='30' fill='#e2563b' stroke='#fff5df' stroke-width='3'/>",
            "lighthouse" => "<path d='M90 40 L172 16 L172 64 Z M90 40 L8 16 L8 64 Z' fill='#ffe27a' opacity='.55'/>"
                + "<path d='M38 146 Q90 126 142 146 Z' fill='#9d9c95'/>"
                + $"<path d='M70 140 L78 54 H102 L110 140 Z' fill='#f4f2ec' stroke='{ink}' stroke-width='5'/>"
                + "<path d='M74 102 H106 L108 120 H72 Z M76 74 H104 L105 88 H75 Z' fill='#d23b2f'/>"
                + $"<rect x='68' y='48' width='44' height='7' fill='{ink}'/>"
                + $"<rect x='84' y='122' width='12' height='18' fill='{ink}'/>"
                + $"<rect x='79' y='28' width='22' height='20' fill='#ffe27a' stroke='{ink}' stroke-width='4'/>"
                + $"<path d='M76 30 Q90 8 104 30 Z' fill='#d23b2f' stroke='{ink}' stroke-width='4'/>",
            "sea" => "<circle cx='138' cy='34' r='18' fill='#f3c23c'/>"
                + "<rect x='10' y='60' width='160' height='80' rx='12' fill='#4fa3d1'/>"
                + "<path d='M24 82 q10 -10 20 0 t20 0 t20 0 t20 0 t20 0 t20 0 M34 104 q10 -10 20 0 t20 0 t20 0 t20 0 t20 0"
                + " M24 126 q10 -10 20 0 t20 0 t20 0 t20 0 t20 0 t20 0' fill='none' stroke='#e8f8fb' stroke-width='6'"
                + " stroke-linecap='round'/>",
            "mountains" => "<path d='M6 136 L62 36 L118 136 Z' fill='#7d8fa3'/>"
                + "<path d='M70 136 L122 54 L174 136 Z' fill='#93a5b8'/>"
                + "<path d='M62 36 L80 68 L70 62 L62 72 L54 62 L44 68 Z M122 54 L137 78 L129 72 L122 80 L115 72 L107 78 Z'"
                + " fill='#ffffff'/>",
            "river" => "<rect x='8' y='14' width='164' height='124' rx='14' fill='#9ccf7a'/>"
                + "<path d='M60 14 C30 52 130 82 100 138' fill='none' stroke='#4fa3d1' stroke-width='34'/>"
                + "<path d='M70 62 L102 86' stroke='#6b4a2e' stroke-width='4' stroke-linecap='round'/>"
                + $"<path d='M62 66 H98 L92 78 H68 Z' fill='#a7653b' stroke='{ink}' stroke-width='3'/>",
            "forest" => "<ellipse cx='90' cy='130' rx='80' ry='10' fill='#70b765'/>"
                + "<rect x='45' y='100' width='10' height='28' fill='#7a5536'/>"
                + "<rect x='117' y='96' width='12' height='32' fill='#7a5536'/>"
                + "<path d='M50 22 L74 66 H26 Z M50 44 L82 104 H18 Z' fill='#3f7a45'/>"
                + "<path d='M123 12 L150 60 H96 Z M123 38 L158 100 H88 Z' fill='#2f6a3d'/>",
            "dragon" => "<path d='M10 140 L50 70 L80 120 L112 58 L170 140 Z' fill='#93a5b8'/>"
                + "<path d='M50 70 L60 88 L50 84 L42 90 Z M112 58 L124 80 L114 76 L104 82 Z' fill='#ffffff'/>"
                + $"<path d='M30 140 Q34 92 80 84 L88 62 L98 84 Q132 86 140 108 Q144 122 124 124 H96 Q84 128 84 140 Z'"
                + $" fill='#5fae5a' stroke='{ink}' stroke-width='5' stroke-linejoin='round'/>"
                + "<path d='M88 62 L98 84 L92 84 Z' fill='#fff2d0'/>"
                + $"<circle cx='108' cy='98' r='7' fill='#ffffff' stroke='{ink}' stroke-width='3'/>"
                + $"<circle cx='110' cy='98' r='3' fill='{ink}'/>"
                + $"<circle cx='132' cy='112' r='3' fill='{ink}'/>"
                + "<circle cx='154' cy='92' r='11' fill='none' stroke='#d9dcdf' stroke-width='5'/>"
                + "<path d='M40 128 Q60 116 76 128' fill='none' stroke='#d23b2f' stroke-width='9' stroke-linecap='round'/>",
            "balloon" => "<path d='M90 8 C46 8 36 56 52 80 L76 108 H104 L128 80 C144 56 134 8 90 8 Z' fill='#e8453c'/>"
                + "<path d='M90 8 C66 12 64 68 80 108 H100 C116 68 114 12 90 8 Z' fill='#f6d03f'/>"
                + "<path d='M90 8 C82 18 82 78 86 108 H94 C98 78 98 18 90 8 Z' fill='#3f8fd8'/>"
                + "<path d='M90 8 C46 8 36 56 52 80 L76 108 H104 L128 80 C144 56 134 8 90 8 Z' fill='none'"
                + $" stroke='{ink}' stroke-width='5' stroke-linejoin='round'/>"
                + $"<path d='M78 108 L80 120 M102 108 L100 120' stroke='{ink}' stroke-width='4'/>"
                + $"<rect x='76' y='120' width='28' height='22' rx='3' fill='#b07a43' stroke='{ink}' stroke-width='4'/>",
            "star" =>
                "<path d='M90 18 L105 58 L147 59 L114 86 L125 127 L90 103 L55 127 L66 86 L33 59 L75 58 Z' fill='#ffd23f'"
                    + $" stroke='{ink}' stroke-width='5' stroke-linejoin='round'/>",
            "egg" => "<ellipse cx='90' cy='120' rx='62' ry='18' fill='#e2c27e'/>"
                + $"<ellipse cx='90' cy='78' rx='34' ry='46' fill='#f6e7c8' stroke='{ink}' stroke-width='5'/>"
                + "<circle cx='76' cy='64' r='7' fill='#7fbf6a'/><circle cx='104' cy='90' r='8' fill='#f2a14e'/>"
                + "<circle cx='98' cy='52' r='5' fill='#f2a14e'/><circle cx='78' cy='98' r='6' fill='#7fbf6a'/>"
                + "<path d='M66 22 q6 -10 0 -18 M90 18 q6 -10 0 -16 M114 22 q6 -10 0 -18' fill='none' stroke='#ff8a2a'"
                + " stroke-width='5' stroke-linecap='round'/>",
            // The perforated paper is outlined, so that a stamp shows on the cream of a panel too.
            "stamp" => $"<rect x='36' y='18' width='108' height='114' rx='4' fill='{ink}'/>"
                + "<rect x='40' y='22' width='100' height='106' fill='#fff5df' stroke='#fff5df' stroke-width='14'"
                + " stroke-dasharray='0 14' stroke-linecap='round'/>"
                + "<rect x='52' y='36' width='76' height='78' fill='#9ed5df'/>"
                + "<circle cx='110' cy='56' r='9' fill='#f3c23c'/>"
                + "<path d='M52 114 L76 76 L92 94 L106 80 L128 114 Z' fill='#70b765'/>",
            _ => null,
        };
        ImageTexture? texture = null;
        if (body != null)
        {
            var image = new Image();
            image.LoadSvgFromString(
                "<svg xmlns='http://www.w3.org/2000/svg' width='180' height='150' viewBox='0 0 180 150'>"
                    + body
                    + "</svg>",
                2
            );
            texture = ImageTexture.CreateFromImage(image);
        }
        Pictures[icon] = texture;
        return texture;
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

    private static readonly Dictionary<string, ShaderMaterial> Surfaces = new();

    /// <summary>
    /// The surface of a building's box in <paramref name="color"/> (<c>Facade.gdshader</c>): plaster with now and then
    /// a patch of bare brick, bare brick, upright boards, overlapping boards or rounded shingles for a roof panel.
    /// </summary>
    public static ShaderMaterial Facade(string color, FacadeStyle style = FacadeStyle.Plaster) =>
        Surface($"facade:{color}:{style}", "res://Shaders/Facade.gdshader", material =>
        {
            material.SetShaderParameter("wall_color", new Color(color));
            material.SetShaderParameter("style", (int)style);
        });

    /// <summary>A box of <see cref="Block"/> covered with <see cref="Facade"/>.</summary>
    public static MeshInstance3D Wall(Node3D parent, Vector3 pos, Vector3 scale, string color, FacadeStyle style)
    {
        var wall = Block(parent, pos, scale, color);
        wall.MaterialOverride = Facade(color, style);
        return wall;
    }

    /// <summary>
    /// Rounded fish-scale tiles in <paramref name="color"/> for a gabled roof with its ridge along z; the gable ends take
    /// the colour <paramref name="gable"/> of the walls.
    /// </summary>
    public static ShaderMaterial RoofTiles(string color, string gable) =>
        Surface($"roof:{color}:{gable}", "res://Shaders/RoofTiles.gdshader", material =>
        {
            material.SetShaderParameter("tile_color", new Color(color));
            material.SetShaderParameter("gable_color", new Color(gable));
        });

    /// <summary>
    /// Gives every solid mesh under <paramref name="root"/> a soft dark outline <paramref name="thickness"/> metres wide
    /// (<c>Outline.gdshader</c>, drawn as the mesh's overlay), the look of a drawn picture. See-through parts, such as
    /// glows and halos, are left without one.
    /// </summary>
    public static void Outline(Node root, float thickness = .025f, float minSize = 0)
    {
        var material = Surface($"outline:{thickness}", "res://Shaders/Outline.gdshader", outline =>
            outline.SetShaderParameter("thickness", thickness)
        );
        if (root is MeshInstance3D self)
            OutlineMesh(self, material, minSize);
        foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
            OutlineMesh((MeshInstance3D)node, material, minSize);
    }

    private static StandardMaterial3D? smokeMaterial;

    /// <summary>
    /// A thin curl of soft white smoke rising from a chimney at <paramref name="position"/>: round puffs that swell and
    /// fade as they drift up. It is a child of <paramref name="parent"/>, so it shows and hides with it.
    /// </summary>
    public static CpuParticles3D ChimneySmoke(Node3D parent, Vector3 position)
    {
        smokeMaterial ??= new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            Roughness = 1,
            CullMode = BaseMaterial3D.CullModeEnum.Back,
        };
        var fade = new Gradient();
        fade.SetColor(0, new Color(1, 1, 1, 0));
        fade.SetColor(1, new Color(.93f, .93f, .95f, 0));
        fade.AddPoint(.04f, new Color(1, 1, 1, .75f));
        fade.AddPoint(.6f, new Color(.97f, .97f, .98f, .45f));
        var swell = new Curve();
        swell.AddPoint(new Vector2(0, .65f));
        swell.AddPoint(new Vector2(1, 1));
        var smoke = new CpuParticles3D
        {
            Name = "ChimneySmoke",
            Position = position,
            Amount = 9,
            Lifetime = 4.5f,
            Preprocess = 4.5f,
            Mesh = new SphereMesh
            {
                Radius = .28f,
                Height = .56f,
                RadialSegments = 8,
                Rings = 4,
            },
            MaterialOverride = smokeMaterial,
            Direction = Vector3.Up,
            Spread = 12,
            InitialVelocityMin = .35f,
            InitialVelocityMax = .6f,
            Gravity = new Vector3(.12f, .05f, .04f),
            ScaleAmountMin = .8f,
            ScaleAmountMax = 1.4f,
            ScaleAmountCurve = swell,
            ColorRamp = fade,
            LocalCoords = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(smoke);
        return smoke;
    }

    /// <summary>Takes the outline of <see cref="Outline"/> off every mesh under <paramref name="root"/>.</summary>
    public static void ClearOutline(Node root)
    {
        foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)node).MaterialOverlay = null;
    }

    /// <summary>
    /// Outlines one mesh, unless it is see-through or shines by itself, or is smaller than <paramref name="minSize"/>
    /// metres every way (knobs and flowers, whose outlines would only clutter a building).
    /// </summary>
    private static void OutlineMesh(MeshInstance3D mesh, ShaderMaterial material, float minSize)
    {
        if (mesh.Mesh == null)
            return;
        if (mesh.MaterialOverride is BaseMaterial3D { Transparency: not BaseMaterial3D.TransparencyEnum.Disabled })
            return;
        if (mesh.MaterialOverride is BaseMaterial3D { ShadingMode: BaseMaterial3D.ShadingModeEnum.Unshaded })
            return;
        if (minSize > 0)
        {
            var size = mesh.Mesh.GetAabb().Size * mesh.GlobalTransform.Basis.Scale;
            if (Mathf.Max(size.X, Mathf.Max(size.Y, size.Z)) < minSize)
                return;
        }
        mesh.MaterialOverlay = material;
    }

    private static ShaderMaterial Surface(string key, string shader, System.Action<ShaderMaterial> setup)
    {
        if (!Surfaces.TryGetValue(key, out var material))
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>(shader) };
            setup(material);
            Surfaces[key] = material;
        }
        return material;
    }

    private static readonly Color WindowGlow = new Color("ffd77a").SrgbToLinear();
    private static readonly List<(StandardMaterial3D Material, Color Day)> Windows = new();

    /// <summary>How brightly every window glass shines, from 0 by day to 1 at night (<see cref="SetWindowLight"/>).</summary>
    public static float WindowLight { get; private set; }

    /// <summary>
    /// The glass of a window, in <paramref name="color"/> by day. Every window shares the lamp of the valley: at night
    /// <see cref="SetWindowLight"/> turns them all warm yellow at once, so no building has to switch its own lights.
    /// </summary>
    public static StandardMaterial3D WindowMat(string color)
    {
        string key = "window:" + color;
        if (!Materials.TryGetValue(key, out var material))
        {
            material = new StandardMaterial3D
            {
                AlbedoColor = new Color(color).SrgbToLinear(),
                Roughness = .88f,
                EmissionEnabled = true,
                Emission = WindowGlow,
            };
            Materials[key] = material;
            Windows.Add((material, material.AlbedoColor));
            Light(material, material.AlbedoColor, WindowLight);
        }
        return material;
    }

    /// <summary>A window pane: a block of <see cref="WindowMat"/> glass that lights up at night.</summary>
    public static MeshInstance3D Window(Node3D parent, Vector3 pos, Vector3 scale, string color)
    {
        var window = Block(parent, pos, scale, color);
        window.MaterialOverride = WindowMat(color);
        return window;
    }

    /// <summary>The material is the glass of a window (<see cref="WindowMat"/>).</summary>
    public static bool IsWindow(Material? material) => Windows.Exists(w => w.Material == material);

    /// <summary>Lights every window of the valley: 0 is the plain glass of the day, 1 a warm lamp behind each one.</summary>
    public static void SetWindowLight(float light)
    {
        light = Mathf.Clamp(light, 0, 1);
        if (light == WindowLight || (Mathf.Abs(light - WindowLight) < .01f && light is > 0 and < 1))
            return;
        WindowLight = light;
        foreach (var (material, day) in Windows)
            Light(material, day, light);
    }

    private static void Light(StandardMaterial3D material, Color day, float light)
    {
        material.AlbedoColor = day.Lerp(WindowGlow, light);
        material.EmissionEnergyMultiplier = light * 1.3f;
    }

    /// <summary>A block that shines by itself and ignores the light of the day: a window with the lamp on.</summary>
    public static MeshInstance3D Lamp(Node3D parent, Vector3 pos, Vector3 scale, string color)
    {
        string key = "lamp:" + color;
        if (!Materials.TryGetValue(key, out var material))
        {
            material = new StandardMaterial3D
            {
                AlbedoColor = new Color(color).SrgbToLinear(),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            Materials[key] = material;
        }
        var lamp = Block(parent, pos, scale, color);
        lamp.MaterialOverride = material;
        lamp.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return lamp;
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
        // Turned in the parent's own frame, so that it also turns while the parent is not yet in the scene, where LookAt
        // does nothing. A vertical beam cannot look along Up without a degenerate basis, so it turns around another axis.
        var along = to - from;
        if (along.LengthSquared() < 1e-8f)
            return;
        var up = Mathf.Abs(along.Normalized().Dot(Vector3.Up)) > .999f ? Vector3.Right : Vector3.Up;
        b.Basis = Basis.LookingAt(along, up) * Basis.FromScale(b.Scale);
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
