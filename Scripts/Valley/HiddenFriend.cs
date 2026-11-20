using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>The three kinds of small friends who hide beside the roads. The order is part of the saved numbering.</summary>
internal enum HiddenKind
{
    Ladybug,
    Hedgehog,
    Frog,
}

/// <summary>
/// A small friend who hides beside a road: a ladybug on a leaf, a hedgehog or a frog on a lily pad. A question mark
/// grows over it when the car comes near, the lookout tower lights it with an orange beam, and when the player stops
/// close by and presses E or the space bar it goes "pšt!", hops with a few hearts and is gone from the valley (and written
/// into the notebook).
/// </summary>
internal sealed partial class HiddenFriend : Node3D
{
    /// <summary>How far away the question mark can be seen.</summary>
    private const float HintDistance = 34;

    private const float PopSeconds = 1.4f;

    private readonly Node3D body = new() { Name = "Body" };
    private readonly Node3D hearts = new() { Name = "Hearts", Visible = false };
    private readonly List<Node3D> heart = new();
    private readonly Label3D hint;
    private readonly float seed;
    private Node3D? beacon;
    private float pingTime;
    private float phase;
    private float popTime;

    internal HiddenKind Kind { get; }
    internal bool Found { get; private set; }
    internal string FriendName => Texts.HiddenName(Kind);

    internal HiddenFriend(HiddenKind kind, float yaw)
    {
        Kind = kind;
        Name = $"Hidden_{kind}";
        Rotation = new(0, yaw, 0);
        seed = yaw * 7.3f;
        AddChild(body);
        switch (kind)
        {
            case HiddenKind.Ladybug:
                BuildLadybug();
                break;
            case HiddenKind.Hedgehog:
                BuildHedgehog();
                break;
            default:
                BuildFrog();
                break;
        }
        AddChild(hearts);
        for (int i = 0; i < 6; i++)
        {
            var piece = new Node3D();
            Art.Ball(piece, Vector3.Zero, new(.16f, .14f, .06f), i % 2 == 0 ? "ff8fae" : "ffd35d");
            hearts.AddChild(piece);
            heart.Add(piece);
        }
        hint = Art.Sign(this, new(0, 2.9f, 0), "?", 70);
        hint.Modulate = new Color("ffe066");
        hint.Visible = false;
    }

    /// <summary>Lights an orange beam over the friend for a while, as the lookout tower does.</summary>
    internal void Ping(float seconds)
    {
        if (Found)
            return;
        beacon ??= Art.Beacon(this, "ffa94d");
        beacon.Visible = true;
        pingTime = seconds;
    }

    internal bool Pinged => pingTime > 0;
    internal bool HintShown => hint.Visible && !Found;

    /// <summary>The friend is found: it says "pšt!", hops and fades; a restored game skips the show.</summary>
    internal void MarkFound(bool show)
    {
        if (Found)
            return;
        Found = true;
        pingTime = 0;
        if (beacon != null)
            beacon.Visible = false;
        if (!show)
        {
            Visible = false;
            return;
        }
        popTime = PopSeconds;
        hint.Text = Texts.Pst(FriendName);
        hint.Modulate = new Color("fff5d9");
        hint.Visible = true;
        hearts.Visible = true;
    }

    internal void Restore()
    {
        Found = false;
        Visible = true;
        popTime = 0;
        pingTime = 0;
        hearts.Visible = false;
        hint.Text = "?";
        hint.Modulate = new Color("ffe066");
        hint.Visible = false;
        body.Position = Vector3.Zero;
        body.Scale = Vector3.One;
        if (beacon != null)
            beacon.Visible = false;
    }

    internal void Animate(float delta, bool running, Vector3 car)
    {
        if (!Visible)
            return;
        float distance = new Vector2(GlobalPosition.X - car.X, GlobalPosition.Z - car.Z).Length();
        if (Found)
        {
            if (running)
                AnimatePop(delta);
            return;
        }
        if (pingTime > 0 && running)
        {
            pingTime -= delta;
            if (pingTime <= 0 && beacon != null)
                beacon.Visible = false;
        }
        bool near = distance < HintDistance;
        hint.Visible = near;
        if (!near)
            return;
        if (running)
            phase += delta;
        // A friend who peeks out: a little bob, a wiggle and a question mark that grows as the car comes closer.
        body.Position = new(0, .06f * Mathf.Abs(Mathf.Sin(phase * 2.2f + seed)), 0);
        body.Rotation = new(0, .16f * Mathf.Sin(phase * 1.3f + seed), 0);
        float grow = Mathf.SmoothStep(HintDistance, HintDistance - 14, distance);
        hint.Scale = Vector3.One * (.25f + .75f * grow);
        hint.Position = new(0, 2.9f + .12f * Mathf.Sin(phase * 3 + seed), 0);
    }

    private void AnimatePop(float delta)
    {
        popTime -= delta;
        float t = 1 - Mathf.Max(popTime, 0) / PopSeconds;
        body.Position = new(0, 1.7f * Mathf.Sin(Mathf.Min(t * 2.2f, 1) * Mathf.Pi), 0);
        body.Scale = Vector3.One * Mathf.Max(0, 1 - Mathf.Max(0, t - .55f) * 2.2f);
        hint.Position = new(0, 2.9f + t * 1.6f, 0);
        hint.Modulate = new Color("fff5d9") with { A = Mathf.Clamp((1 - t) * 3, 0, 1) };
        for (int i = 0; i < heart.Count; i++)
        {
            float angle = i * Mathf.Tau / heart.Count + seed;
            float reach = t * 1.9f;
            heart[i].Position = new(Mathf.Cos(angle) * reach, 1.2f + t * 2.4f, Mathf.Sin(angle) * reach);
            heart[i].Scale = Vector3.One * Mathf.Max(0, 1 - t * t);
        }
        if (popTime <= 0)
            Visible = false;
    }

    /// <summary>A ball of the given full size (the spheres of <see cref="Art"/> are scaled by their radius).</summary>
    private MeshInstance3D Ball(Vector3 position, Vector3 size, string color) =>
        Art.Ball(body, position, size * .5f, color);

    /// <summary>A flat dark spot on a dome: it sits on the surface of an ellipsoid at <paramref name="x"/>, <paramref name="z"/>.</summary>
    private void Spot(Vector3 center, Vector3 radii, float x, float z, float size, string color)
    {
        float inside = 1 - Mathf.Pow(x / radii.X, 2) - Mathf.Pow(z / radii.Z, 2);
        if (inside <= .02f)
            return;
        float y = center.Y + radii.Y * Mathf.Sqrt(inside);
        Ball(new(center.X + x, y - .02f, center.Z + z), new(size, size * .35f, size), color);
    }

    private void Face(float y, float z, float spread, float eye, float smile)
    {
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * spread, y, z), new(eye, eye * 1.2f, eye * .75f), "34404a");
            Art.Ball(
                body,
                new(side * spread - eye * .25f, y + eye * .4f, z + eye * .55f),
                Vector3.One * eye * .3f,
                "fff5e1"
            );
            Art.Ball(
                body,
                new(side * (spread + eye * 1.6f), y - eye * .9f, z - .02f),
                new(eye * 1.3f, eye * .8f, .04f),
                "f4a29b"
            );
        }
        Ball(new(0, y - eye * 1.7f, z + .01f), new(smile, .05f, .05f), "7a4a4a");
    }

    private void BuildLadybug()
    {
        Ball(new(0, .05f, 0), new(1.7f, .1f, 1.5f), "6fb35f");
        Ball(new(0, .06f, 0), new(1.1f, .1f, .9f), "5aa04f");
        var center = new Vector3(0, .5f, -.05f);
        var radii = new Vector3(.58f, .46f, .52f);
        Ball(center, radii * 2, "d93a30");
        foreach (
            var (x, z, size) in new[]
            {
                (-.3f, -.1f, .2f),
                (.3f, -.12f, .2f),
                (0f, -.34f, .17f),
                (-.18f, .2f, .14f),
                (.2f, .22f, .14f),
            }
        )
            Spot(center, radii, x, z, size, "2b2b33");
        Ball(new(0, .4f, .45f), new(.78f, .6f, .46f), "fff1d6");
        foreach (int side in new[] { -1, 1 })
        {
            var antenna = Art.Cylinder(body, new(side * .17f, .98f, .5f), .02f, .02f, .42f, "2b2b33");
            antenna.RotationDegrees = new(-10, 0, side * -22);
            Ball(new(side * .27f, 1.17f, .51f), Vector3.One * .09f, "2b2b33");
            Ball(new(side * .35f, .12f, .2f), new(.22f, .18f, .3f), "2b2b33");
        }
        Face(.46f, .65f, .17f, .1f, .12f);
    }

    private void BuildHedgehog()
    {
        const string fur = "8a6446";
        Ball(new(0, .55f, -.1f), new(1.25f, .95f, 1.5f), fur);
        var radii = new Vector3(.62f, .47f, .75f);
        var center = new Vector3(0, .55f, -.1f);
        // Spikes over the back and the sides, leaving the face free.
        foreach (var (lift, count, turn) in new[] { (25f, 7, 0f), (55f, 9, .3f), (82f, 9, .1f) })
        {
            for (int i = 0; i < count; i++)
            {
                float around = turn + i * Mathf.Tau / count;
                var dir = new Vector3(
                    Mathf.Sin(around) * Mathf.Cos(Mathf.DegToRad(lift)),
                    Mathf.Sin(Mathf.DegToRad(lift)),
                    Mathf.Cos(around) * Mathf.Cos(Mathf.DegToRad(lift))
                );
                if (dir.Z > .35f && dir.Y < .85f)
                    continue;
                var surface = center + new Vector3(dir.X * radii.X, dir.Y * radii.Y, dir.Z * radii.Z);
                var spike = Art.Cylinder(body, surface + dir * .12f, .1f, 0, .42f, "4d3829");
                spike.Basis = new Basis(new Quaternion(Vector3.Up, dir.Normalized()));
            }
        }
        Ball(new(0, .4f, .7f), new(.62f, .5f, .55f), "f0d7b6");
        Ball(new(0, .44f, .98f), new(.17f, .13f, .12f), "34404a");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .3f, .8f, .6f), new(.2f, .22f, .12f), fur);
            Ball(new(side * .24f, .1f, .55f), new(.26f, .2f, .36f), "f0d7b6");
        }
        Face(.55f, .93f, .17f, .1f, .1f);
    }

    private void BuildFrog()
    {
        Art.Cylinder(body, new(0, .03f, 0), 1.45f, 1.45f, .06f, "86cfea");
        Art.Cylinder(body, new(0, .08f, -.05f), 1.0f, 1.0f, .05f, "5fa653");
        Ball(new(0, .55f, 0), new(1.05f, .75f, 1.0f), "6cbf5a");
        Ball(new(0, .4f, .3f), new(.74f, .5f, .55f), "d9f2a8");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .3f, 1.05f, .25f), Vector3.One * .4f, "6cbf5a");
            Ball(new(side * .3f, 1.1f, .35f), Vector3.One * .3f, "fffaf0");
            Ball(new(side * .3f, 1.1f, .5f), new(.15f, .18f, .1f), "34404a");
            Ball(new(side * .6f, .3f, -.3f), new(.38f, .32f, .7f), "5aa84b");
            Ball(new(side * .42f, .12f, .5f), new(.3f, .12f, .36f), "5aa84b");
            Ball(new(side * .44f, .42f, .52f), new(.2f, .12f, .05f), "f4a29b");
        }
        Ball(new(0, .5f, .56f), new(.52f, .05f, .06f), "7a3a3a");
    }
}
