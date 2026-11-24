using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Buildings;

/// <summary>
/// The hot-air balloon of the balloon station (<c>docs/BALLOON_PLAN.md</c>): a wicker basket, the burner on its frame and
/// a tall rainbow envelope of twelve gores, some 25 m high, so that it is seen from all over the valley. On its field it
/// floats on its ropes and sways a little, and now and then the burner roars and its flame lights the envelope from
/// inside; by night the envelope glows in its own colours like a lantern. In flight the game moves the whole node; its
/// origin is the bottom of the basket and +Z is where the wind carries it.
/// </summary>
public partial class HotAirBalloon : Node3D
{
    /// <summary>Where the envelope begins above the bottom of the basket, how tall it is and how wide at its widest.</summary>
    internal const float MouthHeight = 3.5f,
        EnvelopeHeight = 21,
        EnvelopeRadius = 9.2f;

    private const float MouthRadius = 1.15f;
    private const int Gores = 12,
        GoreSteps = 4,
        Rings = 30;

    private static readonly string[] GoreColors = { "e8453c", "f39a35", "f6d03f", "5cb85c", "3f8fd8", "8e5cc8" };

    private readonly Node3D swing = new() { Name = "Swing" };
    private readonly Node3D flame = new() { Name = "BurnerFlame", Visible = false };
    private readonly List<MeshInstance3D> tongues = new();
    private readonly ShaderMaterial envelope;

    /// <summary>
    /// The envelope's fabric: the colour of each gore, lit as usual, and a glow of the same colour, a little warmer, that
    /// the burner's light shining through the fabric gives it.
    /// </summary>
    private const string EnvelopeShader = """
        shader_type spatial;
        render_mode cull_disabled;

        uniform float glow = 0.0;

        void fragment() {
            ALBEDO = COLOR.rgb;
            ROUGHNESS = 0.72;
            EMISSION = (COLOR.rgb + vec3(0.25, 0.12, 0.02)) * glow;
        }
        """;
    private float time;
    private float flare;
    private float idle = 2.5f;

    /// <summary>Whether the balloon is in the air, when it no longer sways on its ropes but drifts.</summary>
    internal bool Flying { get; set; }

    /// <summary>Whether the burner's flame shows at the moment, for the checks.</summary>
    internal bool Flaming => flame.Visible;

    /// <summary>The middle of the envelope, for the camera and the celebrations.</summary>
    internal Vector3 Focus => GlobalTransform * new Vector3(0, MouthHeight + EnvelopeHeight * .55f, 0);

    internal HotAirBalloon()
    {
        Name = "HotAirBalloon";
        AddChild(swing);
        BuildBasket();
        BuildBurner();
        envelope = new ShaderMaterial { Shader = new Shader { Code = EnvelopeShader } };
        var shell = new MeshInstance3D
        {
            Name = "Envelope",
            Mesh = Envelope(),
            MaterialOverride = envelope,
        };
        swing.AddChild(shell);
        // The ropes from the mouth of the envelope down to the burner's frame.
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.Tau / 6 + .26f;
            var top = new Vector3(Mathf.Cos(a) * MouthRadius, MouthHeight + .1f, Mathf.Sin(a) * MouthRadius);
            var foot = new Vector3(Mathf.Cos(a) * .5f, 2.62f, Mathf.Sin(a) * .5f);
            Rod(swing, foot, top, .035f, "5a4632");
        }
    }

    /// <summary>
    /// A thin rod or rope from <paramref name="from"/> to <paramref name="to"/> in the parent's space, set up without the
    /// parent being in the tree yet (<see cref="Art.Beam"/> needs it there).
    /// </summary>
    internal static MeshInstance3D Rod(Node3D parent, Vector3 from, Vector3 to, float width, string color)
    {
        var rod = Art.Block(parent, Vector3.Zero, Vector3.One, color);
        Stretch(rod, from, to, width);
        return rod;
    }

    /// <summary>Stretches a rod made by <see cref="Rod"/> between two other points.</summary>
    internal static void Stretch(Node3D rod, Vector3 from, Vector3 to, float width)
    {
        var along = to - from;
        var up = Mathf.Abs(along.Normalized().Dot(Vector3.Up)) > .999f ? Vector3.Right : Vector3.Up;
        rod.Transform = new Transform3D(
            Basis.LookingAt(along, up) * Basis.FromScale(new Vector3(width, width, along.Length())),
            (from + to) / 2
        );
    }

    /// <summary>A square wicker basket with a padded rim, woven bands and four poles up to the burner's frame.</summary>
    private void BuildBasket()
    {
        Art.Block(swing, new(0, .52f, 0), new(1.5f, 1.04f, 1.5f), "b07a43");
        foreach (float y in new[] { .2f, .48f, .76f })
            Art.Block(swing, new(0, y, 0), new(1.54f, .09f, 1.54f), "8f5f30");
        Art.Block(swing, new(0, 1.07f, 0), new(1.62f, .14f, 1.62f), "6b4a2e");
        foreach (int x in new[] { -1, 1 })
        foreach (int z in new[] { -1, 1 })
            Rod(swing, new(x * .7f, 1.1f, z * .7f), new(x * .46f, 2.6f, z * .46f), .05f, "4a4f57");
    }

    /// <summary>The burner on its frame above the basket, the gas bottle in a corner and the flame that shows when it burns.</summary>
    private void BuildBurner()
    {
        Art.Block(swing, new(0, 2.6f, 0), new(1.02f, .07f, 1.02f), "4a4f57");
        Art.Cylinder(swing, new(0, 2.78f, 0), .34f, .3f, .34f, "b8bec6");
        Art.Cylinder(swing, new(0, 2.98f, 0), .2f, .16f, .1f, "7d848c");
        Art.Cylinder(swing, new(.42f, .62f, .42f), .16f, .16f, .9f, "d8d23a");
        swing.AddChild(flame);
        flame.Position = new(0, 3.05f, 0);
        foreach (
            var (radius, height, color, alpha) in new[] { (.32f, 1.7f, "ff7a1a", .75f), (.18f, 1.2f, "ffe07a", .9f) }
        )
        {
            var tongue = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    BottomRadius = radius,
                    TopRadius = 0,
                    Height = height,
                    RadialSegments = 10,
                },
                Position = new(0, height / 2, 0),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(color).SrgbToLinear() with { A = alpha },
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            flame.AddChild(tongue);
            tongues.Add(tongue);
        }
    }

    /// <summary>The radius of the envelope a part <paramref name="t"/> of the way from its mouth to its crown.</summary>
    private static float Profile(float t)
    {
        const float widest = .6f;
        if (t >= widest)
        {
            float u = (t - widest) / (1 - widest);
            return EnvelopeRadius * Mathf.Sqrt(Mathf.Max(0, 1 - u * u));
        }
        float v = Mathf.Pow(t / widest, 1.2f);
        return MouthRadius + (EnvelopeRadius - MouthRadius) * Mathf.Sin(v * Mathf.Pi / 2);
    }

    /// <summary>
    /// The envelope: a surface of revolution of <see cref="Profile"/>, twelve gores of the rainbow's colours (each slightly
    /// rounded out between its seams), open at the mouth.
    /// </summary>
    private static ArrayMesh Envelope()
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        int around = Gores * GoreSteps;
        Vector3 Point(int step, float t)
        {
            float angle = step * Mathf.Tau / around;
            float within = step % GoreSteps / (float)GoreSteps;
            float bulge = 1 + .028f * Mathf.Sin(within * Mathf.Pi);
            float r = Profile(t) * bulge;
            return new Vector3(Mathf.Cos(angle) * r, MouthHeight + t * EnvelopeHeight, Mathf.Sin(angle) * r);
        }
        Vector3 Normal(int step, float t)
        {
            float angle = step * Mathf.Tau / around;
            if (t > .995f)
                return Vector3.Up;
            float slope = (Profile(Mathf.Min(1, t + .01f)) - Profile(Mathf.Max(0, t - .01f))) / (.02f * EnvelopeHeight);
            return new Vector3(Mathf.Cos(angle), -slope, Mathf.Sin(angle)).Normalized();
        }
        for (int gore = 0; gore < Gores; gore++)
        {
            var color = new Color(GoreColors[gore % GoreColors.Length]).SrgbToLinear();
            for (int k = 0; k < GoreSteps; k++)
            {
                int a = gore * GoreSteps + k,
                    b = a + 1;
                // The seam at the end of a gore gets the next gore's bulge of 0, as its own start has.
                int bEnd = k == GoreSteps - 1 ? b % around : b;
                for (int ring = 0; ring < Rings; ring++)
                {
                    float t0 = ring / (float)Rings,
                        t1 = (ring + 1) / (float)Rings;
                    // Clockwise seen from outside, which is the front in Godot (the outline draws the back faces).
                    var quad = new[]
                    {
                        (Point(a, t0), Normal(a, t0)),
                        (Point(bEnd, t1), Normal(bEnd, t1)),
                        (Point(a, t1), Normal(a, t1)),
                        (Point(a, t0), Normal(a, t0)),
                        (Point(bEnd, t0), Normal(bEnd, t0)),
                        (Point(bEnd, t1), Normal(bEnd, t1)),
                    };
                    foreach (var (p, n) in quad)
                    {
                        vertices.Add(p);
                        normals.Add(n);
                        // A little darker towards the mouth, as the cloth there is in shade.
                        colors.Add(color.Darkened(.18f * (1 - Mathf.Min(1, (p.Y - MouthHeight) / 5))));
                    }
                }
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    /// <summary>Holds the balloon on its ropes again, upright, with the bottom of the basket at <paramref name="foot"/>.</summary>
    internal void Rest(Vector3 foot, float yaw)
    {
        Flying = false;
        Transform = new Transform3D(new Basis(Vector3.Up, yaw), foot);
        swing.Rotation = Vector3.Zero;
    }

    /// <summary>
    /// Sways the envelope, lets the burner roar (in flight while <paramref name="burning"/>, on the field now and then) and
    /// makes the envelope glow by night like a lantern, brighter with every roar of it.
    /// </summary>
    internal void Tick(float delta, float darkness, bool burning)
    {
        time += delta;
        if (!Flying)
        {
            idle -= delta;
            if (idle < -1.3f)
                idle = 5.5f + 2.5f * (.5f + .5f * Mathf.Sin(time * .37f));
            burning = idle < 0;
            swing.Rotation = new Vector3(.025f * Mathf.Sin(time * .7f), 0, .03f * Mathf.Sin(time * .53f + 1));
        }
        else
            swing.Rotation = new Vector3(.02f * Mathf.Sin(time * 1.1f), 0, .02f * Mathf.Sin(time * .8f));
        flare = Mathf.MoveToward(flare, burning ? 1 : 0, delta * (burning ? 5 : 1.6f));
        if (delta > 0)
            flame.Visible = burning;
        if (flame.Visible)
            for (int i = 0; i < tongues.Count; i++)
                tongues[i].Scale = new Vector3(1, 1 + .18f * Mathf.Sin(time * (17 + i * 6) + i), 1);
        envelope.SetShaderParameter("glow", darkness * (.45f + 1.1f * flare) + .15f * flare);
    }
}
