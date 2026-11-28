using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The lighthouse on the shore beyond the end of the road to the sea (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phases 3
/// and 4), raised by <see cref="Stages"/> deliveries: the footing on the rock, the round stone plinth, three storeys of
/// the white tower with red bands, the stairs and the gallery, the keeper's brick lodge at the foot and at last the
/// lantern with its glass, red dome and vane. A finished lighthouse that is <see cref="Lit"/> shines at night: the lamp
/// glows, a small light falls round the top and a beam of two opposite cones turns over the sea and the valley.
/// </summary>
/// <remarks>
/// The node stands at <see cref="LighthouseSite.Foot"/>, turned by <see cref="LighthouseSite.Yaw"/>; the door and the
/// lodge's front face local -Z, towards the road. All the parts are made at once and <see cref="SetStage"/> only shows
/// and hides them, so it may be called before the node is in the tree and as often as needed. The solid parts block the
/// car only once their stage is built. The beam has no visibility range and casts no shadow: it is seen from the whole
/// valley.
/// </remarks>
public partial class Lighthouse : Node3D
{
    /// <summary>The deliveries that build the lighthouse.</summary>
    internal const int Stages = 8;

    private const float StoreyHeight = 6.4f;
    private const float BottomRadius = 3.0f,
        TopRadius = 1.9f;

    /// <summary>How long one turn of the beam takes, in seconds.</summary>
    private const float TurnTime = 8;

    private const float BeamLength = 120;

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D[] parts = new Node3D[Stages + 1];
    private readonly List<CollisionShape3D>[] solids = new List<CollisionShape3D>[Stages + 1];
    private readonly float baseY;
    private readonly float towerTop;
    private Node3D sign = null!;
    private Node3D beam = null!;
    private MeshInstance3D glow = null!;
    private OmniLight3D lamp = null!;
    private ShaderMaterial beamMaterial = null!;
    private StandardMaterial3D glowMaterial = null!;
    private float beamAngle;
    private float shownNight = -1;

    internal LighthouseSite Site { get; }

    /// <summary>The deliveries the lighthouse has had, 0 to <see cref="Stages"/>.</summary>
    internal int Stage { get; private set; }

    internal bool Complete => Stage >= Stages;

    /// <summary>Whether the lamp is switched on; it shines only when the lighthouse is complete and it is dark.</summary>
    internal bool Lit { get; set; }

    /// <summary>Whether the beam is shining now (after the last <see cref="Tick"/>).</summary>
    internal bool Shining => beam.Visible;

    /// <summary>A point on the gallery at the top, on the side away from the road, for the camera to look out from.</summary>
    internal Vector3 TopFocus => Position + basis * new Vector3(0, towerTop + 1.7f, TopRadius + .6f);

    /// <summary>The middle of the tower, for the camera of a celebration.</summary>
    internal Vector3 Focus => Position + Vector3.Up * (baseY + StoreyHeight * 1.5f);

    internal Lighthouse(LighthouseSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "Lighthouse";
        Position = site.Foot;
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        for (int i = 0; i <= Stages; i++)
        {
            parts[i] = new Node3D { Name = "LighthouseStage" + i };
            AddChild(parts[i]);
            solids[i] = new List<CollisionShape3D>();
        }
        // The ground round the tower's footing, which is not levelled.
        float low = float.MaxValue,
            high = float.MinValue;
        for (int i = 0; i <= 12; i++)
        {
            float a = i * Mathf.Tau / 12;
            float h = i == 12 ? Floor(0, 0) : Floor(Mathf.Sin(a) * 4, Mathf.Cos(a) * 4);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        float footTop = high + .5f;
        baseY = footTop + 1.76f;
        towerTop = baseY + StoreyHeight * 3;
        BuildPlan();
        BuildFooting(low, footTop);
        BuildPlinth(footTop);
        for (int storey = 0; storey < 3; storey++)
            BuildStorey(storey);
        BuildGallery();
        BuildLodge();
        BuildLantern();
        SetStage(0);
    }

    /// <summary>Height above the node of the ground under a point of the lighthouse's frame.</summary>
    private float Floor(float x, float z)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    /// <summary>The tower's radius at a height above the plinth.</summary>
    private static float RadiusAt(float height) =>
        BottomRadius + (TopRadius - BottomRadius) * height / (StoreyHeight * 3);

    /// <summary>A solid shape that blocks the car once <paramref name="stage"/> is built.</summary>
    private void Solid(int stage, Vector3 center, Shape3D shape)
    {
        var body = Obstacles.Add(parts[stage], center, shape);
        solids[stage].Add((CollisionShape3D)body.GetChild(0));
    }

    /// <summary>Raises the lighthouse to a number of deliveries: at once from a save, or after a load is tipped out.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Stages);
        parts[0].Visible = Stage == 0;
        for (int i = 1; i <= Stages; i++)
        {
            parts[i].Visible = i <= Stage;
            foreach (var shape in solids[i])
                shape.Disabled = i > Stage;
        }
        sign.Visible = !Complete;
        if (!Complete)
            Shine(false);
    }

    /// <summary>
    /// Stage 0: a circle of stakes and string where the tower will stand, and the sign of the site, which stays until the
    /// lantern is on.
    /// </summary>
    private void BuildPlan()
    {
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            float x = Mathf.Sin(a) * 3.4f,
                z = Mathf.Cos(a) * 3.4f;
            float y = Floor(x, z);
            Art.Cylinder(parts[0], new(x, y + .35f, z), .06f, .05f, .9f, "a77a59");
            Art.Ball(parts[0], new(x, y + .82f, z), new(.1f, .06f, .1f), "e25b45");
        }
        var ring = Art.Mesh(
            parts[0],
            new TorusMesh
            {
                InnerRadius = 3.38f,
                OuterRadius = 3.42f,
                Rings = 24,
                RingSegments = 4,
            },
            new(0, Floor(0, 0) + .6f, 0),
            Vector3.One,
            "f3ead2"
        );
        ring.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        sign = new Node3D { Name = "LighthouseSign", Position = new(-4.2f, Floor(-4.2f, -5.6f), -5.6f) };
        AddChild(sign);
        foreach (float x in new[] { -.75f, .75f })
            Art.Cylinder(sign, new(x, .75f, 0), .07f, .07f, 1.9f, "7a5536");
        Art.Block(sign, new(0, 1.5f, 0), new(2.0f, .65f, .1f), "e9d3a2");
        Art.Block(sign, new(0, 1.5f, .02f), new(2.1f, .75f, .06f), "7a5536");
        Art.Sign(sign, new(0, 1.52f, -.1f), OutlandTexts.SignLighthouse, 34);
    }

    /// <summary>Stage 1: a rough footing of stones set into the rock, from below the lowest ground to above the highest.</summary>
    private void BuildFooting(float low, float top)
    {
        float bottom = low - 1.2f;
        Art.Cylinder(parts[1], new(0, (bottom + top) / 2, 0), 4.1f, 3.9f, top - bottom, "8a857e");
        for (int i = 0; i < 9; i++)
        {
            float a = (i + .3f) * Mathf.Tau / 9;
            // The lodge stands on the +X side; the rocks keep off it.
            if (Mathf.Sin(a) > .75f)
                continue;
            float x = Mathf.Sin(a) * 4.1f,
                z = Mathf.Cos(a) * 4.1f;
            float size = .5f + (i * 37 % 10) / 25f;
            Art.Ball(
                parts[1],
                new(x, Mathf.Max(Floor(x, z) + .15f, top - .4f), z),
                new(size * 1.2f, size * .7f, size),
                i % 2 == 0 ? "7f7a74" : "9a958e"
            );
        }
        Solid(1, new(0, (bottom + top) / 2, 0), new CylinderShape3D { Radius = 4.1f, Height = top - bottom });
    }

    /// <summary>Stage 2: the round plinth of dressed stone with a pale band and the steps up to the door.</summary>
    private void BuildPlinth(float footTop)
    {
        float height = baseY - footTop - .16f;
        Art.Cylinder(parts[2], new(0, footTop + height / 2, 0), 3.6f, 3.4f, height, "b3aca4");
        Art.Cylinder(parts[2], new(0, baseY - .08f, 0), 3.55f, 3.55f, .16f, "d8d2c6");
        Art.Block(parts[2], new(0, footTop + .4f, -3.95f), new(1.8f, .8f, .7f), "a39d94");
        Art.Block(parts[2], new(0, footTop + (baseY - footTop) / 2, -3.5f), new(1.8f, baseY - footTop, .6f), "a39d94");
        Solid(2, new(0, footTop + height / 2, 0), new CylinderShape3D { Radius = 3.6f, Height = height });
    }

    /// <summary>
    /// Stages 3 to 5: a storey of the tapering tower, white with a red band and a window that lights up at night. The
    /// first one has the porch of the door towards the road.
    /// </summary>
    private void BuildStorey(int storey)
    {
        var part = parts[3 + storey];
        float y0 = baseY + storey * StoreyHeight;
        float r0 = RadiusAt(storey * StoreyHeight),
            r1 = RadiusAt((storey + 1) * StoreyHeight);
        const float white = 4.2f;
        float rw = RadiusAt(storey * StoreyHeight + white);
        Art.Cylinder(part, new(0, y0 + white / 2, 0), r0, rw, white, "f4f2ec");
        Art.Cylinder(part, new(0, y0 + white + (StoreyHeight - white) / 2, 0), rw, r1, StoreyHeight - white, "d23b2f");
        float wy = y0 + (storey == 0 ? 3.4f : 2.2f);
        float angle = storey == 0 ? .8f : Mathf.Pi + (storey == 1 ? .5f : -.6f);
        float wr = RadiusAt(wy - baseY) + .02f;
        var window = Art.Window(
            part,
            new(Mathf.Sin(angle) * wr, wy, Mathf.Cos(angle) * wr),
            new(.6f, .95f, .12f),
            "3a4650"
        );
        window.Rotation = new(0, angle, 0);
        Solid(3 + storey, new(0, y0 + StoreyHeight / 2, 0), new CylinderShape3D { Radius = r0, Height = StoreyHeight });
        if (storey > 0)
            return;
        // The porch: two white cheeks and a lintel round a dark doorway, under a little red roof.
        float front = -r0;
        Art.Block(part, new(0, y0 + 1.2f, front - .03f), new(1.3f, 2.4f, .1f), "2b211c");
        foreach (int side in new[] { -1, 1 })
            Art.Block(part, new(side * .8f, y0 + 1.3f, front - .45f), new(.3f, 2.6f, 1.0f), "f4f2ec");
        Art.Block(part, new(0, y0 + 2.75f, front - .45f), new(1.9f, .4f, 1.0f), "f4f2ec");
        Art.Block(part, new(0, y0 + 3.02f, front - .55f), new(2.1f, .14f, 1.3f), "d23b2f");
        Solid(3, new(0, y0 + 1.5f, front - .45f), new BoxShape3D { Size = new(1.9f, 3.0f, 1.0f) });
    }

    /// <summary>
    /// Stage 6: the wooden stairs, seen through the doorway, and the gallery round the top on a white corbel, with a
    /// railing of posts and two rails.
    /// </summary>
    private void BuildGallery()
    {
        var part = parts[6];
        float front = -BottomRadius;
        for (int step = 0; step < 4; step++)
        {
            float h = (step + 1) * .4f;
            Art.Block(part, new(0, baseY + h / 2, front - .82f + step * .2f), new(1.1f, h, .2f), "c39770");
        }
        Art.Cylinder(part, new(.45f, baseY + 1.3f, front - .5f), .03f, .03f, 1.4f, "8a6440").RotationDegrees = new(
            -50,
            0,
            0
        );
        Art.Cylinder(part, new(0, towerTop - .45f, 0), TopRadius + .95f, TopRadius, .9f, "f4f2ec");
        Art.Cylinder(part, new(0, towerTop + .12f, 0), TopRadius + 1.0f, TopRadius + 1.0f, .24f, "2f3a40");
        float rail = TopRadius + .9f;
        for (int i = 0; i < 14; i++)
        {
            float a = i * Mathf.Tau / 14;
            Art.Cylinder(
                part,
                new(Mathf.Sin(a) * rail, towerTop + .75f, Mathf.Cos(a) * rail),
                .04f,
                .04f,
                1.1f,
                "2f3a40"
            );
        }
        foreach (float h in new[] { .7f, 1.25f })
            Art.Mesh(
                part,
                new TorusMesh
                {
                    InnerRadius = rail - .04f,
                    OuterRadius = rail + .04f,
                    Rings = 28,
                    RingSegments = 4,
                },
                new(0, towerTop + h, 0),
                Vector3.One,
                "2f3a40"
            );
    }

    /// <summary>Stage 7: the keeper's little lodge of bricks at the foot of the tower, on a plinth of its own.</summary>
    private void BuildLodge()
    {
        var part = parts[7];
        const float cx = 6.4f,
            cz = .6f,
            w = 3.8f,
            d = 3.4f,
            height = 2.4f;
        float low = float.MaxValue,
            high = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
        {
            float h = Floor(cx + dx * (w + .4f), cz + dz * (d + .4f));
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        float y = high + .3f,
            bottom = low - .4f;
        Art.Block(part, new(cx, (y + bottom) / 2, cz), new(w + .4f, y - bottom, d + .4f), "9d968c");
        Art.Wall(part, new(cx, y + height / 2, cz), new(w, height, d), "b5533b", FacadeStyle.Bricks);
        float front = cz - d / 2;
        Art.Block(part, new(cx + .8f, y + 1.0f, front - .05f), new(.9f, 2.0f, .1f), "4a5a68");
        Art.Ball(part, new(cx + 1.1f, y + 1.0f, front - .12f), Vector3.One * .05f, "f2c14e");
        Art.Block(part, new(cx - .8f, y + 1.5f, front - .03f), new(1.0f, .95f, .06f), "f3ead2");
        Art.Window(part, new(cx - .8f, y + 1.5f, front - .07f), new(.78f, .74f, .06f), "a9d6ef");
        var roof = Art.Mesh(part, Prism, new(cx, y + height + .7f, cz), new(d + .7f, 1.4f, w + .6f), "6b6f73");
        roof.RotationDegrees = new(0, 90, 0);
        Art.Block(part, new(cx - 1.0f, y + height + 1.1f, cz + .6f), new(.5f, 1.6f, .5f), "8f3f2e");
        Solid(
            7,
            new(cx, (bottom + y + height) / 2, cz),
            new BoxShape3D { Size = new(w + .4f, y + height - bottom, d + .4f) }
        );
    }

    private static readonly PrismMesh Prism = new();

    /// <summary>
    /// Stage 8: the lantern on the gallery: a red drum, glass panes between dark mullions round the lamp, a red dome with a
    /// gilt ball and a vane, and the parts that shine at night (the glow, the light and the beam), hidden until lit.
    /// </summary>
    private void BuildLantern()
    {
        var part = parts[8];
        float y = towerTop + .24f;
        float r = TopRadius - .3f;
        Art.Cylinder(part, new(0, y + .3f, 0), r + .1f, r + .1f, .6f, "d23b2f");
        const float glassHeight = 2.2f;
        float glassMiddle = y + .6f + glassHeight / 2;
        var glass = Art.Cylinder(part, new(0, glassMiddle, 0), r, r, glassHeight, "bfe6f2");
        glass.Name = "LanternGlass";
        glass.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color("bfe6f2").SrgbToLinear() with { A = .28f },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = .1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        glass.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            Art.Block(
                part,
                new(Mathf.Sin(a) * r, glassMiddle, Mathf.Cos(a) * r),
                new(.09f, glassHeight, .09f),
                "2f3a40"
            ).Rotation = new(0, a, 0);
        }
        float top = y + .6f + glassHeight;
        Art.Cylinder(part, new(0, top + .08f, 0), r + .2f, r + .2f, .16f, "2f3a40");
        Art.Mesh(
            part,
            new SphereMesh
            {
                Radius = 1,
                Height = 1,
                IsHemisphere = true,
                RadialSegments = 14,
                Rings = 5,
            },
            new(0, top + .16f, 0),
            new(r + .15f, 1.1f, r + .15f),
            "d23b2f"
        );
        Art.Ball(part, new(0, top + 1.32f, 0), Vector3.One * .22f, "f2c14e");
        Art.Cylinder(part, new(0, top + 1.85f, 0), .03f, .03f, 1.0f, "2f3a40");
        Art.Block(part, new(0, top + 2.05f, .25f), new(.05f, .05f, .9f), "2f3a40");
        Art.Block(part, new(0, top + 2.05f, -.15f), new(.04f, .3f, .3f), "2f3a40");
        Art.Ball(part, new(0, top + 2.05f, .72f), new(.06f, .06f, .14f), "2f3a40");
        // The lamp: a pale bulb by day, a glowing ball at night.
        Art.Ball(part, new(0, glassMiddle, 0), Vector3.One * .42f, "efe6c4");
        glowMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("fff1b8"),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        glow = Art.Ball(part, new(0, glassMiddle, 0), Vector3.One * .55f, "fff1b8");
        glow.Name = "LanternGlow";
        glow.MaterialOverride = glowMaterial;
        glow.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        lamp = new OmniLight3D
        {
            Name = "LanternLight",
            Position = new(0, glassMiddle, 0),
            OmniRange = 25,
            LightColor = new Color("ffe3a0"),
            ShadowEnabled = false,
        };
        part.AddChild(lamp);
        beamMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/LighthouseBeam.gdshader") };
        beamMaterial.SetShaderParameter("beam_length", BeamLength);
        beam = new Node3D { Name = "LighthouseBeam", Position = new(0, glassMiddle, 0) };
        part.AddChild(beam);
        var cone = new CylinderMesh
        {
            TopRadius = .45f,
            BottomRadius = 7,
            Height = BeamLength,
            RadialSegments = 16,
            Rings = 1,
            CapTop = false,
            CapBottom = false,
        };
        foreach (int side in new[] { -1, 1 })
        {
            // The cone's top (+Y) is turned to the lamp and its axis laid along ±Z.
            var mesh = new MeshInstance3D
            {
                Name = side < 0 ? "BeamBack" : "BeamFront",
                Mesh = cone,
                MaterialOverride = beamMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new(0, 0, side * BeamLength / 2),
                Rotation = new(-side * Mathf.Pi / 2, 0, 0),
            };
            beam.AddChild(mesh);
        }
        Shine(false);
    }

    private void Shine(bool on)
    {
        beam.Visible = on;
        glow.Visible = on;
        lamp.Visible = on;
        if (!on)
            shownNight = -1;
    }

    /// <summary>
    /// Turns the beam and sets the strength of the light for the darkness of the sky, <paramref name="night"/> from 0 by
    /// day to 1 at full night. Call it every frame; it does nothing by day or while the lamp is off.
    /// </summary>
    internal void Tick(double delta, float night)
    {
        bool on = Complete && Lit && night > .01f;
        if (on != beam.Visible)
            Shine(on);
        if (!on)
            return;
        beamAngle = (beamAngle + (float)delta * Mathf.Tau / TurnTime) % Mathf.Tau;
        beam.Rotation = new(0, beamAngle, 0);
        night = Mathf.Clamp(night, 0, 1);
        if (Mathf.Abs(night - shownNight) < .01f)
            return;
        shownNight = night;
        beamMaterial.SetShaderParameter("intensity", night);
        lamp.LightEnergy = 1.8f * night;
        var warm = new Color("efe6c4").Lerp(new Color("fff6c8"), night);
        float bright = 1 + .6f * night;
        glowMaterial.AlbedoColor = new Color(warm.R * bright, warm.G * bright, warm.B * bright);
    }
}
