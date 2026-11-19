using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Settlement;

/// <summary>
/// One candle or lamp that the glass lamp maker blows. <paramref name="Id"/> is saved in a game and so is never
/// renamed. <paramref name="Build"/> draws one lamp around the origin of the node it is given (standing on the ground,
/// front towards +Z) and returns its glow: the node that holds everything that shines at night (the flame or the bulb,
/// a soft halo and a light of its own, which is switched off until <see cref="LampField"/> wants it).
/// </summary>
internal sealed record GlassLamp(string Id, string Name, Func<Node3D, Node3D> Build);

/// <summary>
/// The two lamps of the glass lamp maker, a candle and a lamp. Both are made of boxes, cylinders and balls with a see-through glass
/// material, never of the capsules of trunks, and has no solid body: a lamp is small enough to drive over. By day the
/// glass is clear and the flame is out; at night the glow node is shown.
/// </summary>
internal static class GlassLamps
{
    public static IReadOnlyList<GlassLamp> All { get; } =
        new GlassLamp[] { new("candle", "Svíčka", Candle), new("lamp", "Lampa", Lamp) };

    public static GlassLamp? Find(string? id) => All.FirstOrDefault(lamp => lamp.Id == id);

    private static readonly Dictionary<string, StandardMaterial3D> Materials = new();
    private static StandardMaterial3D? halo;

    /// <summary>The see-through glass of a lamp, a little shiny, in a pale tint.</summary>
    private static StandardMaterial3D Glass(string color, float alpha = .38f)
    {
        string key = $"glass:{color}:{alpha}";
        if (!Materials.TryGetValue(key, out var material))
        {
            material = new StandardMaterial3D
            {
                AlbedoColor = new Color(color).SrgbToLinear() with { A = alpha },
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = .08f,
                MetallicSpecular = 1,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            };
            Materials[key] = material;
        }
        return material;
    }

    /// <summary>The material of something that shines by itself and ignores the light around it.</summary>
    private static StandardMaterial3D Shine(string color)
    {
        string key = "shine:" + color;
        if (!Materials.TryGetValue(key, out var material))
        {
            material = new StandardMaterial3D
            {
                AlbedoColor = new Color(color).SrgbToLinear(),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            Materials[key] = material;
        }
        return material;
    }

    /// <summary>A soft round glow that always faces the camera and adds its light to what is behind it.</summary>
    private static StandardMaterial3D Halo()
    {
        if (halo != null)
            return halo;
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, .35f, 1f },
            Colors = new[] { new Color(1, .86f, .5f, .85f), new Color(1, .78f, .38f, .28f), new Color(1, .7f, .3f, 0) },
        };
        halo = new StandardMaterial3D
        {
            AlbedoTexture = new GradientTexture2D
            {
                Gradient = gradient,
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
        return halo;
    }

    private static MeshInstance3D Clear(MeshInstance3D part, string color, float alpha = .38f)
    {
        part.MaterialOverride = Glass(color, alpha);
        part.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return part;
    }

    private static MeshInstance3D Flame(Node3D glow, Vector3 position, Vector3 scale, string color)
    {
        var part = Art.Ball(glow, position, scale, color);
        part.MaterialOverride = Shine(color);
        part.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        return part;
    }

    /// <summary>
    /// The node of everything that shines: the shapes the caller adds to it, a halo of <paramref name="size"/> metres
    /// and a warm light, off until the field switches it on, so that a lamp costs nothing by day.
    /// </summary>
    private static Node3D Glow(Node3D root, Vector3 at, float size)
    {
        var glow = new Node3D { Name = "Glow", Position = at };
        root.AddChild(glow);
        glow.AddChild(
            new MeshInstance3D
            {
                Name = "Halo",
                Mesh = new QuadMesh { Size = new Vector2(size, size) },
                MaterialOverride = Halo(),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
        glow.AddChild(
            new OmniLight3D
            {
                Name = "LampLight",
                LightColor = new Color("ffd58a"),
                OmniRange = 9,
                OmniAttenuation = 1.3f,
                LightEnergy = 0,
                ShadowEnabled = false,
                Visible = false,
            }
        );
        return glow;
    }

    private static void Base(Node3D root, float radius, string color)
    {
        Art.Cylinder(root, new(0, .04f, 0), radius, radius, .08f, color);
    }

    /// <summary>A candle in a glass jar.</summary>
    private static Node3D Candle(Node3D root)
    {
        Base(root, .3f, "b9c7cc");
        Art.Cylinder(root, new(0, .22f, 0), .13f, .13f, .28f, "fff0ce");
        Art.Block(root, new(0, .4f, 0), new(.025f, .1f, .025f), "4a3a32");
        Clear(Art.Cylinder(root, new(0, .34f, 0), .27f, .27f, .52f, "dff3f7"), "dff3f7");
        Art.Cylinder(root, new(0, .62f, 0), .29f, .29f, .04f, "9eb6bd");
        var glow = Glow(root, new(0, .5f, 0), 1.7f);
        Flame(glow, Vector3.Zero, new(.1f, .2f, .1f), "ffd24a");
        Flame(glow, new(0, -.03f, 0), new(.05f, .1f, .05f), "fff6c8");
        return glow;
    }

    /// <summary>A tall garden lamp on an iron pole with a tulip-shaped shade of pink glass.</summary>
    private static Node3D Lamp(Node3D root)
    {
        Art.Cylinder(root, new(0, .06f, 0), .24f, .28f, .12f, "3f4f55");
        Art.Cylinder(root, new(0, .95f, 0), .045f, .06f, 1.7f, "3f4f55");
        Art.Block(root, new(.16f, .55f, 0), new(.3f, .08f, .12f), "6eaa75").RotationDegrees = new(0, 0, 25);
        Art.Block(root, new(-.16f, .8f, 0), new(.3f, .08f, .12f), "6eaa75").RotationDegrees = new(0, 0, -25);
        Art.Cylinder(root, new(0, 1.84f, 0), .1f, .06f, .1f, "3f4f55");
        Clear(Art.Ball(root, new(0, 2.08f, 0), new(.56f, .6f, .56f), "f4b1c6"), "f4b1c6", .42f);
        Clear(Art.Cylinder(root, new(0, 2.38f, 0), .3f, .12f, .2f, "f4b1c6"), "f4b1c6", .42f);
        var glow = Glow(root, new(0, 2.1f, 0), 2.8f);
        Flame(glow, Vector3.Zero, Vector3.One * .3f, "fff0a8");
        Flame(glow, Vector3.Zero, Vector3.One * .16f, "fffbe0");
        return glow;
    }
}
