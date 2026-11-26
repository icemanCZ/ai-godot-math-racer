using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The footprints of the trail being followed (<c>docs/TRACKING_PLAN.md</c>), all in one MultiMesh. Only a stretch of
/// them shows: a few behind the furthest print the car has reached and <see cref="Ahead"/> in front of it, fading out
/// towards its end, so that the trail has to be followed rather than seen whole from afar. The next few prints glow
/// golden, so that a child sees where to go on, by day and by night, and golden sparkles hover over the next stretch,
/// above the grass that hides the prints from afar. Each animal has prints of its own shape, drawn larger than life so
/// that they are seen from the car.
/// </summary>
public partial class TrackingPrints : Node3D
{
    /// <summary>How many prints in front of the furthest one reached are shown.</summary>
    public const int Ahead = 40;

    /// <summary>How many prints behind the furthest one reached are still shown.</summary>
    public const int Behind = 8;

    /// <summary>How many prints in front of the furthest one reached glow.</summary>
    public const int Glowing = 4;

    /// <summary>Over how many prints at the far end of the shown stretch they fade out.</summary>
    private const int Fade = 12;

    /// <summary>How much larger than life the prints are drawn.</summary>
    private const float PrintScale = 3.2f;

    /// <summary>How many sparkles hover over the prints ahead, and about how far apart, in metres.</summary>
    private const int Sparkles = 6;

    private const float SparkleSpacing = 4f;

    /// <summary>How high over the ground the sparkles hover, above the grass.</summary>
    private const float SparkleHeight = 1f;

    private static readonly Color Mud = new("4f3320");
    private static readonly Color Glow = new("ffd54a");

    private static readonly Dictionary<TrackingAnimal, Mesh> Meshes = new();
    private static StandardMaterial3D? material;

    private readonly MultiMeshInstance3D instance = new()
    {
        Name = "Prints",
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    private readonly MultiMeshInstance3D sparkles = new()
    {
        Name = "Sparkles",
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Visible = false,
    };

    private TrackingTrail? trail;
    private int progress = int.MinValue;
    private float time;

    public override void _Ready()
    {
        AddChild(instance);
        sparkles.Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new QuadMesh { Size = Vector2.One * 1.6f },
            InstanceCount = Sparkles,
        };
        sparkles.MaterialOverride = SparkleMaterial();
        AddChild(sparkles);
    }

    /// <summary>The trail shown, or null.</summary>
    internal TrackingTrail? Trail => trail;

    /// <summary>How far apart one print lies from the next along a trail of the animal.</summary>
    internal static float Stride(TrackingAnimal animal) =>
        animal switch
        {
            TrackingAnimal.Hare => 2.8f,
            TrackingAnimal.Fox => 1.25f,
            TrackingAnimal.Deer => 1.45f,
            TrackingAnimal.Badger => 1.15f,
            TrackingAnimal.Boar => 1.3f,
            _ => 1.4f,
        };

    /// <summary>How far to each side of the trail's middle the left and the right prints lie; 0 for the hare's groups.</summary>
    internal static float Straddle(TrackingAnimal animal) =>
        animal switch
        {
            TrackingAnimal.Hare => 0,
            TrackingAnimal.Fox => .1f,
            TrackingAnimal.Deer => .22f,
            TrackingAnimal.Badger => .3f,
            TrackingAnimal.Boar => .26f,
            _ => .22f,
        };

    /// <summary>
    /// Shows the trail with the furthest print the car has reached (-1 before the first); null hides every print. The
    /// prints are laid out anew only when the trail changes.
    /// </summary>
    internal void Show(TrackingTrail? shown, int reached)
    {
        if (shown != trail)
        {
            trail = shown;
            progress = int.MinValue;
            Lay();
        }
        if (trail == null || reached == progress)
            return;
        progress = reached;
        for (int i = 0; i < trail.Prints.Length; i++)
            instance.Multimesh.SetInstanceColor(i, ColorOf(i));
    }

    /// <summary>Whether a print of the trail shown can be seen now, for the checks.</summary>
    internal bool Shows(int index) => trail != null && ColorOf(index).A > 0;

    /// <summary>The glowing prints ahead pulse, and the sparkles over them bob and twinkle one after another.</summary>
    internal void Tick(float delta)
    {
        sparkles.Visible = trail != null;
        if (trail == null)
            return;
        time += delta;
        for (int i = progress + 1; i <= progress + Glowing; i++)
            if (i >= 0 && i < trail.Prints.Length)
                instance.Multimesh.SetInstanceColor(i, ColorOf(i));
        int every = Mathf.Max(1, Mathf.RoundToInt(SparkleSpacing / Stride(trail.Animal)));
        var multimesh = sparkles.Multimesh;
        for (int s = 0; s < Sparkles; s++)
        {
            int index = Mathf.Max(progress, 0) + (s + 1) * every;
            if (index >= trail.Prints.Length)
            {
                multimesh.SetInstanceColor(s, Colors.Transparent);
                continue;
            }
            float wave = Mathf.Sin(time * 3 - s * 1.1f);
            var position = trail.Prints[index].Position + Vector3.Up * (SparkleHeight + .12f * wave);
            multimesh.SetInstanceTransform(
                s,
                new Transform3D(Basis.Identity.Scaled(Vector3.One * (.85f + .2f * wave)), position)
            );
            // The nearest sparkle is the brightest; the further ones fade, like the prints.
            multimesh.SetInstanceColor(s, new Color(1, 1, 1, (.75f + .25f * wave) * (1 - s * .11f)));
        }
    }

    /// <summary>A soft golden glow that always faces the camera.</summary>
    private static StandardMaterial3D SparkleMaterial() =>
        new()
        {
            AlbedoTexture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Offsets = new[] { 0f, .18f, .5f, 1f },
                    Colors = new[]
                    {
                        new Color(1, 1, .9f, 1),
                        new Color(1, .9f, .45f, .95f),
                        new Color(1, .8f, .3f, .3f),
                        new Color(1, .75f, .25f, 0),
                    },
                },
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(.5f, .5f),
                FillTo = new Vector2(1, .5f),
                Width = 64,
                Height = 64,
            },
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            BillboardKeepScale = true,
            DisableReceiveShadows = true,
        };

    private Color ColorOf(int index)
    {
        int ahead = index - progress;
        if (ahead < -Behind || ahead > Ahead)
            return Mud with { A = 0 };
        float alpha = ahead > Ahead - Fade ? (Ahead - ahead + 1) / (float)(Fade + 1) : 1;
        if (ahead >= 1 && ahead <= Glowing)
        {
            float pulse = .55f + .45f * Mathf.Sin(time * 4 - ahead * .9f);
            return Mud.Lerp(Glow, pulse) with { A = alpha };
        }
        return Mud with { A = alpha * (ahead < 0 ? .7f : .92f) };
    }

    private void Lay()
    {
        if (trail == null)
        {
            instance.Visible = false;
            return;
        }
        var prints = trail.Prints;
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = MeshOf(trail.Animal),
            InstanceCount = prints.Length,
        };
        for (int i = 0; i < prints.Length; i++)
        {
            var print = prints[i];
            var up = print.Normal;
            var forward = (print.Forward - up * print.Forward.Dot(up)).Normalized();
            var right = up.Cross(forward);
            // A left foot is the right one mirrored.
            float mirror = print.Side < 0 ? -1 : 1;
            var basis = new Basis(right * PrintScale * mirror, up * PrintScale, forward * PrintScale);
            multimesh.SetInstanceTransform(i, new Transform3D(basis, print.Position));
            multimesh.SetInstanceColor(i, Mud with { A = 0 });
        }
        instance.Multimesh = multimesh;
        instance.MaterialOverride = Material();
        instance.Visible = true;
    }

    private static StandardMaterial3D Material() =>
        material ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = Colors.White,
        };

    /// <summary>The flat shape of one print of the animal (for the hare, a group of four), toes towards +Z, life size.</summary>
    internal static Mesh MeshOf(TrackingAnimal animal)
    {
        if (Meshes.TryGetValue(animal, out var cached))
            return cached;
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        const int segments = 14;
        foreach (var (x, z, rx, rz, turn) in Ovals(animal))
        {
            var middle = new Vector3(x, 0, z);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.Tau / segments,
                    b = (i + 1) * Mathf.Tau / segments;
                var p = new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz).Rotated(Vector3.Up, turn);
                var q = new Vector3(Mathf.Cos(b) * rx, 0, Mathf.Sin(b) * rz).Rotated(Vector3.Up, turn);
                tool.SetNormal(Vector3.Up);
                tool.AddVertex(middle);
                tool.SetNormal(Vector3.Up);
                tool.AddVertex(middle + q);
                tool.SetNormal(Vector3.Up);
                tool.AddVertex(middle + p);
            }
        }
        var mesh = tool.Commit();
        Meshes[animal] = mesh;
        return mesh;
    }

    /// <summary>
    /// The ovals one print of the animal is made of, life size in metres: the middle across (X) and along (Z, towards the
    /// toes), the half widths and the turn about Y. The book of tracks draws its pictures from them too.
    /// </summary>
    internal static (float X, float Z, float Rx, float Rz, float Turn)[] Ovals(TrackingAnimal animal) =>
        animal switch
        {
            // The long hind feet land side by side ahead of the small front feet, one behind the other.
            TrackingAnimal.Hare => new[]
            {
                (-.07f, .16f, .045f, .12f, 0f),
                (.07f, .16f, .045f, .12f, 0f),
                (.01f, -.09f, .035f, .045f, 0f),
                (-.02f, -.24f, .035f, .045f, 0f),
            },
            // A narrow oval: a pad, two toes in front and two at the sides, and tiny claw marks.
            TrackingAnimal.Fox => new[]
            {
                (0f, -.025f, .04f, .033f, 0f),
                (-.022f, .045f, .016f, .022f, 0f),
                (.022f, .045f, .016f, .022f, 0f),
                (-.048f, .012f, .015f, .02f, .4f),
                (.048f, .012f, .015f, .02f, -.4f),
                (-.022f, .074f, .006f, .009f, 0f),
                (.022f, .074f, .006f, .009f, 0f),
            },
            // Two pointed halves of the hoof, like a heart upside down.
            TrackingAnimal.Deer => new[] { (-.024f, 0f, .021f, .055f, -.12f), (.024f, 0f, .021f, .055f, .12f) },
            // A broad pad, five toes in a row and long claws in front of them.
            TrackingAnimal.Badger => new[]
            {
                (0f, -.02f, .06f, .035f, 0f),
                (-.06f, .028f, .013f, .016f, 0f),
                (-.03f, .034f, .013f, .016f, 0f),
                (0f, .04f, .013f, .016f, 0f),
                (.03f, .034f, .013f, .016f, 0f),
                (.06f, .028f, .013f, .016f, 0f),
                (-.066f, .068f, .006f, .017f, 0f),
                (-.033f, .074f, .006f, .017f, 0f),
                (0f, .08f, .006f, .017f, 0f),
                (.033f, .074f, .006f, .017f, 0f),
                (.066f, .068f, .006f, .017f, 0f),
            },
            // Two rounder halves of the hoof and the two dew claws behind them.
            TrackingAnimal.Boar => new[]
            {
                (-.027f, .01f, .024f, .05f, -.08f),
                (.027f, .01f, .024f, .05f, .08f),
                (-.045f, -.07f, .012f, .014f, 0f),
                (.045f, -.07f, .012f, .014f, 0f),
            },
            // The lynx: a round print with a big pad and four round toes, and no claws.
            _ => new[]
            {
                (0f, -.025f, .05f, .042f, 0f),
                (-.05f, .035f, .02f, .023f, 0f),
                (-.018f, .06f, .02f, .024f, 0f),
                (.018f, .06f, .02f, .024f, 0f),
                (.05f, .035f, .02f, .023f, 0f),
            },
        };
}
