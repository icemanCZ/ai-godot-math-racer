using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The balloon station (<c>docs/BALLOON_PLAN.md</c>) beside a road of the land beyond the old valley: a landing target of
/// red and white rings on the field, above which the balloon (<see cref="HotAirBalloon"/>, a node of its own) floats on
/// four ropes, high enough to rise over the trees and be seen from afar, a little wooden hut with a striped awning, a windsock, gas bottles and sandbags, and a sign by the road.
/// The node stands at <see cref="BalloonSite.Road"/>, turned by <see cref="BalloonSite.Yaw"/>, so its frame is the
/// station's (+Z from the road onto the field). The ground is not levelled: every part stands on the ground at its spot.
/// </summary>
public partial class BalloonStation : Node3D
{
    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D tethers = new() { Name = "Tethers" };
    private readonly Node3D sock = new() { Name = "Windsock" };
    private readonly CollisionShape3D basketShape;
    private readonly StaticBody3D basketBody;
    private readonly List<(MeshInstance3D Rope, Vector3 Stake, Vector3 Corner)> ropes = new();
    private float time;

    /// <summary>How high above the field the bottom of the basket floats on its ropes while the balloon waits.</summary>
    internal const float Hover = 9;

    /// <summary>How fast the balloon rises on its ropes after a landing, in metres a second.</summary>
    private const float RopeRise = 1.1f;

    /// <summary>How high above the field the bottom of the basket is now, 0 just after a landing.</summary>
    internal float Lift { get; private set; } = Hover;

    internal BalloonSite Site { get; }

    /// <summary>The balloonist who stands between the road and the field.</summary>
    internal Character? Balloonist { get; set; }

    /// <summary>The balloon that stands on the field and flies.</summary>
    internal HotAirBalloon? Balloon { get; set; }

    /// <summary>The flights the player has made and the stars caught in all, for the rooster (<see cref="BalloonPlans"/>).</summary>
    internal int Flights { get; set; }

    internal int Stars { get; set; }

    /// <summary>The balloon is in the air: the ropes lie on the ground and the field is free.</summary>
    internal bool Tethered => tethers.Visible;

    internal BalloonStation(BalloonSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "BalloonStation";
        Position = ground.At(site.Road.X, site.Road.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        BuildTarget();
        BuildHut(new Vector2(-9.5f, 31));
        BuildWindsock(new Vector2(9.5f, 31.5f));
        BuildSupplies();
        BuildSign(new Vector2(7, 3.8f));
        AddChild(tethers);
        var stakes = new[] { new Vector2(-5.5f, -5.5f), new(5.5f, -5.5f), new(-5.5f, 5.5f), new(5.5f, 5.5f) };
        foreach (var stake in stakes)
        {
            var foot = At(stake.X, BalloonSite.PadAlong + stake.Y);
            Art.Cylinder(tethers, foot + Vector3.Up * .25f, .07f, .05f, .5f, "6b4a2e");
            var corner =
                At(0, BalloonSite.PadAlong)
                + new Vector3(Mathf.Sign(stake.X) * .76f, 1.08f, Mathf.Sign(stake.Y) * .76f);
            var from = foot + Vector3.Up * .45f;
            ropes.Add((HotAirBalloon.Rod(tethers, from, corner, .045f, "e9dcc0"), from, corner));
        }
        basketBody = Obstacles.Add(
            this,
            At(0, BalloonSite.PadAlong) + Vector3.Up * .55f,
            new BoxShape3D { Size = new(1.7f, 1.1f, 1.7f) }
        );
        basketShape = basketBody.GetChild<CollisionShape3D>(0);
        Raise(Hover);
    }

    /// <summary>The bottom of the basket on its ropes, <see cref="Lift"/> above the middle of the field.</summary>
    internal Vector3 BalloonAnchor => BalloonFoot + Vector3.Up * Lift;

    /// <summary>The balloon floats on its ropes at once, after a load or a flight ended early.</summary>
    internal void Moor() => Raise(Hover);

    /// <summary>The balloon has come down onto the field; it rises on its ropes again bit by bit.</summary>
    internal void TouchDown() => Raise(0);

    /// <summary>Lifts the ropes, the basket's collision and the balloon to <paramref name="lift"/> above the field.</summary>
    private void Raise(float lift)
    {
        Lift = lift;
        var up = Vector3.Up * lift;
        foreach (var (rope, stake, corner) in ropes)
            HotAirBalloon.Stretch(rope, stake, corner + up, .045f);
        basketBody.Position = At(0, BalloonSite.PadAlong) + Vector3.Up * (.55f + lift);
        if (Balloon is { Flying: false } balloon)
            balloon.Rest(BalloonAnchor, BalloonYaw);
    }

    /// <summary>Height above the node of the ground under a point of the station's frame.</summary>
    private float Floor(float x, float z)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    /// <summary>A point of the station on the ground, lifted by <paramref name="lift"/>.</summary>
    private Vector3 At(float x, float z, float lift = 0) => new(x, Floor(x, z) + lift, z);

    /// <summary>Where the basket stands on the field, on the ground, and the way the balloon faces there.</summary>
    internal Vector3 BalloonFoot => ground.At(Site.Pad.X, Site.Pad.Z);

    internal float BalloonYaw => Site.Yaw;

    /// <summary>The middle of the field a little above it, for the celebrations.</summary>
    internal Vector3 Focus => BalloonFoot + Vector3.Up * 6;

    /// <summary>
    /// The landing target: rings of red and white round a yellow middle that lie on the ground as it is, each a strip of
    /// its own so that the colours keep sharp edges.
    /// </summary>
    private void BuildTarget()
    {
        var bands = new (float Inner, float Outer, string Color)[]
        {
            (0, 1.4f, "f6d03f"),
            (1.4f, 3.2f, "f4f1e6"),
            (3.2f, 5, "e2483d"),
            (5, 6.8f, "f4f1e6"),
            (6.8f, BalloonSite.FieldRadius, "e2483d"),
        };
        const int around = 48;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var (inner, outer, hex) in bands)
        {
            var color = new Color(hex).SrgbToLinear();
            int steps = Mathf.Max(1, Mathf.CeilToInt(outer - inner));
            for (int s = 0; s < steps; s++)
            {
                float r0 = Mathf.Lerp(inner, outer, s / (float)steps),
                    r1 = Mathf.Lerp(inner, outer, (s + 1) / (float)steps);
                for (int i = 0; i < around; i++)
                {
                    float a0 = i * Mathf.Tau / around,
                        a1 = (i + 1) * Mathf.Tau / around;
                    Vector3 P(float r, float a)
                    {
                        float x = Mathf.Cos(a) * r,
                            z = BalloonSite.PadAlong + Mathf.Sin(a) * r;
                        return At(x, z, .06f);
                    }
                    // Clockwise seen from above, which is the front in Godot.
                    foreach (var v in new[] { P(r0, a0), P(r1, a0), P(r1, a1), P(r0, a0), P(r1, a1), P(r0, a1) })
                    {
                        surface.SetColor(color);
                        surface.SetNormal(Vector3.Up);
                        surface.AddVertex(v);
                    }
                }
            }
        }
        AddChild(
            new MeshInstance3D
            {
                Name = "LandingTarget",
                Mesh = surface.Commit(),
                MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = .95f },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
    }

    /// <summary>The hut where the balloonist keeps her maps: boards, a striped awning over the window and a sign on the roof.</summary>
    private void BuildHut(Vector2 c)
    {
        var size = new Vector2(3.6f, 3);
        float low = float.MaxValue,
            high = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
        {
            float h = Floor(c.X + dx * size.X, c.Y + dz * size.Y);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        float y = high + .25f,
            bottom = low - .4f;
        Art.Block(this, new(c.X, (y + bottom) / 2, c.Y), new(size.X + .3f, y - bottom, size.Y + .3f), "9d968c");
        const float height = 2.5f;
        var walls = Art.Block(this, new(c.X, y + height / 2, c.Y), new(size.X, height, size.Y), "c98f55");
        walls.MaterialOverride = Art.Facade("c98f55", FacadeStyle.Boards);
        // A flat roof leaning back, with a rim.
        var roof = Art.Block(this, new(c.X, y + height + .18f, c.Y), new(size.X + .6f, .2f, size.Y + .6f), "7a4f33");
        roof.RotationDegrees = new(-6, 0, 0);
        float front = c.Y - size.Y / 2;
        // The window towards the field, with its striped awning, and the door.
        Art.Window(this, new(c.X - .6f, y + 1.45f, front - .03f), new(1.3f, .8f, .06f), "f4f1e6");
        for (int i = 0; i < 5; i++)
        {
            var stripe = Art.Block(
                this,
                new(c.X - .6f - .6f + i * .3f, y + 2.05f, front - .38f),
                new(.3f, .05f, .8f),
                i % 2 == 0 ? "e2483d" : "f4f1e6"
            );
            stripe.RotationDegrees = new(-22, 0, 0);
        }
        Art.Block(this, new(c.X + 1.05f, y + 1.0f, front - .04f), new(.9f, 2.0f, .07f), "5b3a24");
        Art.Ball(this, new(c.X + .78f, y + 1.0f, front - .1f), Vector3.One * .06f, "f2c14e");
        var sign = Art.Sign(this, new(c.X, y + height + 1, c.Y - .4f), OutlandTexts.SignBalloons, 64);
        sign.Modulate = new Color("fff5d9");
        // A small balloon painted on a board above the door: a ball of colour and a basket.
        Art.Block(this, new(c.X, y + height + .75f, front - .2f), new(2.6f, .7f, .08f), "f4f1e6");
        Obstacles.Box(
            this,
            new(c.X, (bottom + y + height) / 2, c.Y),
            new(size.X + .4f, y + height - bottom, size.Y + .4f)
        );
    }

    /// <summary>A tall pole with an orange and white windsock that shows where the wind will carry the balloon.</summary>
    private void BuildWindsock(Vector2 c)
    {
        var foot = At(c.X, c.Y);
        Art.Cylinder(this, foot + Vector3.Up * 3.5f, .09f, .07f, 7, "dfe3e6");
        Art.Ball(this, foot + Vector3.Up * 7.05f, Vector3.One * .14f, "e2483d");
        Obstacles.Box(this, foot + Vector3.Up * 1, new(.4f, 2, .4f));
        AddChild(sock);
        sock.Position = foot + Vector3.Up * 6.6f;
        for (int i = 0; i < 4; i++)
        {
            float r0 = .42f - i * .07f,
                r1 = .42f - (i + 1) * .07f;
            var ring = Art.Cylinder(sock, new(0, 0, .45f + i * .9f), r0, r1, .9f, i % 2 == 0 ? "f08a3c" : "f4f1e6");
            ring.RotationDegrees = new(90, 0, 0);
        }
        Art.Cylinder(sock, Vector3.Zero, .44f, .44f, .06f, "4a4f57").RotationDegrees = new(90, 0, 0);
    }

    /// <summary>Gas bottles in a rack by the hut and a heap of sandbags on the other side of the field.</summary>
    private void BuildSupplies()
    {
        for (int i = 0; i < 3; i++)
        {
            var foot = At(-6.6f + i * .55f, 27.5f);
            Art.Cylinder(this, foot + Vector3.Up * .45f, .22f, .22f, .9f, i == 1 ? "b8bec6" : "d8d23a");
            Art.Ball(this, foot + Vector3.Up * .92f, new(.22f, .12f, .22f), i == 1 ? "b8bec6" : "d8d23a");
        }
        Art.Block(this, At(-6.05f, 27.85f, .5f), new(1.9f, .08f, .08f), "6b4a2e");
        Obstacles.Box(this, At(-6.05f, 27.5f, .5f), new(1.9f, 1, .7f));
        var bags = new[]
        {
            (4.6f, 27.6f, .2f),
            (5.3f, 27.5f, .2f),
            (6f, 27.7f, .2f),
            (4.95f, 27.55f, .5f),
            (5.65f, 27.6f, .5f),
        };
        foreach (var (x, z, lift) in bags)
            Art.Ball(this, At(x, z, lift), new(.36f, .2f, .26f), "d9c08f");
    }

    /// <summary>A sign by the road with a balloon on it, on two posts.</summary>
    private void BuildSign(Vector2 c)
    {
        var foot = At(c.X, c.Y);
        foreach (int side in new[] { -1, 1 })
            Art.Cylinder(this, foot + new Vector3(side * .7f, 1.1f, 0), .06f, .06f, 2.2f, "6b4a2e");
        Art.Block(this, foot + Vector3.Up * 1.9f, new(1.9f, .8f, .08f), "f4f1e6");
        Art.Sign(this, foot + new Vector3(0, 2.75f, 0), OutlandTexts.SignBalloons, 44);
        Obstacles.Box(this, foot + Vector3.Up * 1.1f, new(1.8f, 2.2f, .3f));
    }

    /// <summary>The balloon leaves the field (the ropes are let go, the field is free) or stands on it again.</summary>
    internal void SetFlying(bool flying)
    {
        tethers.Visible = !flying;
        basketShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, flying);
    }

    /// <summary>The windsock swings in the wind, and after a landing the balloon rises on its ropes again.</summary>
    internal void Tick(float delta)
    {
        time += delta;
        if (tethers.Visible && Lift < Hover && delta > 0)
            Raise(Mathf.MoveToward(Lift, Hover, delta * RopeRise));
        sock.Rotation = new Vector3(
            -.12f + .06f * Mathf.Sin(time * 1.3f),
            .4f + .25f * Mathf.Sin(time * .45f),
            .05f * Mathf.Sin(time * 2.1f)
        );
    }
}
