using Godot;
using MathRacer.Graphics;

namespace MathRacer.Buildings;

/// <summary>
/// A small fishing boat with a cabin whose windows glow, which sails slowly up and down far out at sea at night
/// (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 4). It keeps to a loop round a fixed straight path: out along one side
/// of it, round in a wide turn at the end, back along the other side and round again. It has no collision and is placed
/// in the valley's frame (<see cref="Node3D.TopLevel"/>), whatever its parent.
/// </summary>
public partial class NightBoat : Node3D
{
    private const float Speed = 4;

    /// <summary>The radius of the turn at each end, and so how far each way the boat keeps off the path.</summary>
    private const float TurnRadius = 7;

    private readonly Vector3 from;
    private readonly Vector3 along;
    private readonly Vector3 side;
    private readonly float length;
    private readonly float loop;
    private readonly StandardMaterial3D windows;
    private float travelled;
    private float time;
    private float shownNight = -1;

    /// <summary>How far above its level the sea is now, with the tide (0 at high tide, negative at low tide).</summary>
    internal float TideLift { get; set; }

    /// <summary>
    /// A boat that sails round the path from <paramref name="from"/> to <paramref name="to"/>; the height of
    /// <paramref name="from"/> is the water's surface (<see cref="MathRacer.Valley.ValleyBorders.SeaLevel"/>).
    /// </summary>
    internal NightBoat(Vector3 from, Vector3 to)
    {
        Name = "NightBoat";
        TopLevel = true;
        Visible = false;
        this.from = from;
        var flat = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        length = flat.Length();
        along = length > .01f ? flat / length : Vector3.Back;
        side = new Vector3(along.Z, 0, -along.X);
        loop = 2 * length + Mathf.Tau * TurnRadius;
        windows = new StandardMaterial3D
        {
            AlbedoColor = new Color("2a3440").SrgbToLinear(),
            EmissionEnabled = true,
            Emission = new Color("ffc860").SrgbToLinear(),
            EmissionEnergyMultiplier = 0,
        };
        Build();
        Place();
    }

    /// <summary>The boat's bow is local +Z.</summary>
    private void Build()
    {
        Art.Block(this, new(0, .2f, -.2f), new(1.9f, .8f, 4.2f), "2e4a6b");
        Art.Ball(this, new(0, .2f, 1.9f), new(.95f, .4f, 1.1f), "2e4a6b");
        Art.Block(this, new(0, .56f, -.2f), new(1.94f, .1f, 4.22f), "f4f2ec");
        Art.Block(this, new(0, .63f, -.1f), new(1.7f, .05f, 4.1f), "a77a52");
        Art.Block(this, new(0, 1.26f, -.8f), new(1.4f, 1.2f, 1.6f), "f4f2ec");
        Art.Block(this, new(0, 1.92f, -.8f), new(1.6f, .12f, 1.8f), "c8322a");
        foreach (
            var (position, size) in new[]
            {
                (new Vector3(0, 1.42f, .02f), new Vector3(1.0f, .42f, .05f)),
                (new Vector3(0, 1.42f, -1.62f), new Vector3(.7f, .42f, .05f)),
                (new Vector3(-.71f, 1.42f, -.8f), new Vector3(.05f, .42f, 1.1f)),
                (new Vector3(.71f, 1.42f, -.8f), new Vector3(.05f, .42f, 1.1f)),
            }
        )
        {
            var pane = Art.Block(this, position, size, "2a3440");
            pane.Name = "CabinWindow";
            pane.MaterialOverride = windows;
        }
        Art.Cylinder(this, new(0, 2.4f, .9f), .06f, .05f, 3.6f, "7a4b32");
        Art.Block(this, new(0, 1.9f, .2f), new(.05f, .05f, 1.4f), "7a4b32").RotationDegrees = new(-20, 0, 0);
        Art.Lamp(this, new(0, 4.25f, .9f), Vector3.One * .14f, "fff6d8");
        Art.Lamp(this, new(0, 1.1f, -2.32f), Vector3.One * .1f, "ffe3a0");
    }

    /// <summary>
    /// Sails on and bobs, and shows the boat only when <paramref name="night"/> (0 by day, 1 at full night) is above .3,
    /// with its windows the brighter the darker it is. Call it every frame.
    /// </summary>
    internal void Tick(double delta, float night)
    {
        bool show = night > .3f;
        if (Visible != show)
            Visible = show;
        if (!show)
            return;
        float dt = (float)delta;
        time += dt;
        travelled = (travelled + Speed * dt) % loop;
        if (Mathf.Abs(night - shownNight) > .02f)
        {
            shownNight = night;
            float light = Mathf.Clamp((night - .3f) / .5f, 0, 1);
            windows.AlbedoColor = new Color("2a3440").SrgbToLinear().Lerp(new Color("ffd77a").SrgbToLinear(), light);
            windows.EmissionEnergyMultiplier = 2.2f * light;
        }
        Place();
    }

    /// <summary>Puts the boat at <see cref="travelled"/> metres round its loop, facing the way it sails.</summary>
    private void Place()
    {
        float s = travelled,
            r = TurnRadius,
            half = Mathf.Pi * r;
        var to = from + along * length;
        Vector3 p,
            heading;
        if (s < length)
        {
            p = from + side * r + along * s;
            heading = along;
        }
        else if (s < length + half)
        {
            float a = (s - length) / r;
            p = to + (side * Mathf.Cos(a) + along * Mathf.Sin(a)) * r;
            heading = along * Mathf.Cos(a) - side * Mathf.Sin(a);
        }
        else if (s < 2 * length + half)
        {
            p = to - side * r - along * (s - length - half);
            heading = -along;
        }
        else
        {
            float a = (s - 2 * length - half) / r;
            p = from - (side * Mathf.Cos(a) + along * Mathf.Sin(a)) * r;
            heading = side * Mathf.Sin(a) - along * Mathf.Cos(a);
        }
        Position = new Vector3(p.X, from.Y + TideLift + .05f + Mathf.Sin(time * 1.4f) * .08f, p.Z);
        Rotation = new Vector3(
            Mathf.Sin(time * 1.1f) * .03f,
            Mathf.Atan2(heading.X, heading.Z),
            Mathf.Sin(time * .8f) * .05f
        );
    }
}
