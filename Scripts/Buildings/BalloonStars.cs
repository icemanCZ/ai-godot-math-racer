using System.Collections.Generic;
using Godot;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The golden stars that hang along the balloon's way during a flight (<c>docs/BALLOON_PLAN.md</c>), each at its own height
/// (<see cref="BalloonFlight.StarAt"/>), turning slowly in a soft glow. A star the basket reaches bursts and flies off; a
/// star the balloon has passed fades. They show only during a flight; nothing of them is saved.
/// </summary>
public partial class BalloonStars : Node3D
{
    private const float Radius = 2.4f,
        Thickness = .6f;

    private sealed class Star
    {
        public Node3D Node = null!;
        public Vector3 Home;
        public float Burst = -1;
        public float Fade = -1;
    }

    private readonly List<Star> stars = new();
    private float time;

    internal BalloonStars()
    {
        Name = "BalloonStars";
        Visible = false;
        var mesh = StarMesh();
        var gold = new StandardMaterial3D
        {
            AlbedoColor = new Color("ffd23f").SrgbToLinear(),
            Roughness = .35f,
            Metallic = .2f,
            EmissionEnabled = true,
            Emission = new Color("ffb820"),
            EmissionEnergyMultiplier = 1.1f,
        };
        var halo = new StandardMaterial3D
        {
            AlbedoTexture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Offsets = new[] { 0f, .4f, 1f },
                    Colors = new[]
                    {
                        new Color(1, .92f, .55f, .8f),
                        new Color(1, .85f, .4f, .25f),
                        new Color(1, .8f, .3f, 0),
                    },
                },
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(.5f, .5f),
                FillTo = new Vector2(1, .5f),
                Width = 64,
                Height = 64,
            },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            DisableReceiveShadows = true,
        };
        for (int i = 0; i < BalloonFlight.Stars; i++)
        {
            var node = new Node3D { Name = $"Star{i}" };
            AddChild(node);
            node.AddChild(
                new MeshInstance3D
                {
                    Name = "Glow",
                    Mesh = new QuadMesh { Size = Vector2.One * 11 },
                    MaterialOverride = halo,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
            node.AddChild(
                new MeshInstance3D
                {
                    Name = "Shape",
                    Mesh = mesh,
                    MaterialOverride = gold,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
            stars.Add(new Star { Node = node });
        }
    }

    /// <summary>A five-pointed star, flat and a little thick, standing upright in its XY plane.</summary>
    private static ArrayMesh StarMesh()
    {
        var outline = new Vector3[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.Pi / 2 + i * Mathf.Pi / 5;
            float r = i % 2 == 0 ? Radius : Radius * .45f;
            outline[i] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
        }
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var front = Vector3.Back * (Thickness / 2);
        var back = Vector3.Forward * (Thickness / 2);
        for (int i = 0; i < 10; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % 10];
            // The two faces, each a fan from a raised middle, and the rim between them.
            foreach (var (middle, side, clockwise) in new[] { (front * 1.6f, front, true), (back * 1.6f, back, false) })
            {
                var p = clockwise ? new[] { middle, b + side, a + side } : new[] { middle, a + side, b + side };
                var normal = (p[1] - p[0]).Cross(p[2] - p[0]).Normalized() * -1;
                foreach (var v in p)
                {
                    surface.SetNormal(normal);
                    surface.AddVertex(v);
                }
            }
            var rim = (b - a).Cross(Vector3.Back).Normalized();
            foreach (var v in new[] { a + front, b + front, b + back, a + front, b + back, a + back })
            {
                surface.SetNormal(rim);
                surface.AddVertex(v);
            }
        }
        return surface.Commit();
    }

    /// <summary>Hangs the stars of a flight along its way and shows them.</summary>
    internal void Show(BalloonFlight way, int flight)
    {
        time = 0;
        for (int i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            star.Home = way.StarAt(i, flight);
            star.Burst = -1;
            star.Fade = -1;
            star.Node.Position = star.Home;
            star.Node.Scale = Vector3.One;
            star.Node.Visible = true;
        }
        Visible = true;
    }

    internal void TakeDown()
    {
        Visible = false;
        foreach (var star in stars)
            star.Node.Visible = false;
    }

    /// <summary>Where star <paramref name="index"/> hangs, for the checks and the celebrations.</summary>
    internal Vector3 StarPosition(int index) => stars[index].Home;

    /// <summary>Whether star <paramref name="index"/> is still to be seen (not burst and not faded).</summary>
    internal bool Shows(int index) => Visible && stars[index].Node.Visible && stars[index].Burst < 0;

    /// <summary>The basket has reached a star: it bursts and flies up.</summary>
    internal void Catch(int index)
    {
        if (stars[index].Burst < 0)
            stars[index].Burst = 0;
    }

    /// <summary>The balloon has passed a star without catching it: it fades away.</summary>
    internal void Miss(int index)
    {
        if (stars[index].Burst < 0 && stars[index].Fade < 0)
            stars[index].Fade = 0;
    }

    /// <summary>Turns and bobs the stars, and plays the bursts and the fading.</summary>
    internal void Tick(float delta)
    {
        if (!Visible)
            return;
        time += delta;
        for (int i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            if (!star.Node.Visible)
                continue;
            var node = star.Node;
            if (star.Burst >= 0)
            {
                star.Burst += delta;
                float t = star.Burst / .7f;
                node.Position = star.Home + Vector3.Up * (t * 5);
                node.Scale = Vector3.One * Mathf.Max(.01f, (1 + t * 1.2f) * (1 - t));
                node.Rotation = new Vector3(0, time * 9, 0);
                if (t >= 1)
                    node.Visible = false;
                continue;
            }
            if (star.Fade >= 0)
            {
                star.Fade += delta;
                node.Scale = Vector3.One * Mathf.Max(.01f, 1 - star.Fade / 1.2f);
                if (star.Fade >= 1.2f)
                    node.Visible = false;
            }
            node.Position = star.Home + Vector3.Up * (.35f * Mathf.Sin(time * 1.6f + i));
            node.GetChild<Node3D>(1).Rotation = new Vector3(0, time * 1.4f + i, 0);
        }
    }
}
