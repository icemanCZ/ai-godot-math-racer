using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The stone quarry on the coast (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 3), where the trailer is loaded with
/// stone for the lighthouse: a grey rocky outcrop with a freshly cut face and a bench at its foot, a few cut blocks, two
/// heaps of broken stone, a sign and a wheelbarrow. The node stands at <see cref="CoastQuarrySite.Position"/>, turned by
/// <see cref="CoastQuarrySite.Yaw"/>; the side towards the road (local -Z) is left open for the car and its trailer:
/// nothing solid stands at local z below 1 m within 3.5 m of the middle.
/// </summary>
public partial class CoastQuarry : Node3D
{
    private readonly Terrain ground;
    private readonly Basis basis;

    internal CoastQuarrySite Site { get; }

    /// <summary>The cut face of the outcrop, for the camera.</summary>
    internal Vector3 Focus => Position + basis * new Vector3(.6f, 2, 3.6f);

    internal CoastQuarry(CoastQuarrySite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "CoastQuarry";
        Position = ground.At(site.Position.X, site.Position.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        BuildOutcrop();
        Heap(-5.0f, .2f);
        Heap(5.2f, -.2f);
        BuildSign();
        BuildWheelbarrow();
    }

    /// <summary>A point of the quarry's frame on the ground, lifted by <paramref name="lift"/>.</summary>
    private Vector3 At(float x, float z, float lift = 0)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return new Vector3(x, ground.Height(p.X, p.Z) - Position.Y + lift, z);
    }

    /// <summary>
    /// The outcrop: three big weathered rocks and a smaller one, sunk into the ground, with a pale flat face cut into the
    /// middle one and a bench of stone at its foot, and three cut blocks waiting in front.
    /// </summary>
    private void BuildOutcrop()
    {
        foreach (
            var (x, z, size, color) in new[]
            {
                (-2.8f, 4.2f, new Vector3(2.2f, 2.0f, 1.8f), "8d8a85"),
                (1.0f, 5.2f, new Vector3(2.6f, 2.8f, 1.8f), "9a968f"),
                (4.0f, 3.6f, new Vector3(1.8f, 1.6f, 1.7f), "7f7b75"),
                (-.9f, 2.9f, new Vector3(1.3f, .9f, 1.0f), "a7a29a"),
            }
        )
        {
            var center = At(x, z, size.Y * .45f);
            Art.Ball(this, center, size, color);
            Art.Ball(this, center + new Vector3(size.X * .25f, size.Y * .45f, -size.Z * .1f), size * .45f, "b3aea6");
            Obstacles.Rock(this, center, size * .85f);
        }
        var foot = At(.8f, 3.5f);
        Art.Block(this, foot + new Vector3(0, 1.5f, .1f), new(2.8f, 3.0f, .9f), "c4bfb6");
        Art.Block(this, foot + new Vector3(0, .35f, -.45f), new(2.8f, 1.0f, .8f), "b8b3ab");
        foreach (float y in new[] { 1.1f, 2.0f })
            Art.Block(this, foot + new Vector3(0, y, -.37f), new(2.7f, .04f, .04f), "9d988f");
        Obstacles.Box(this, foot + new Vector3(0, 1.4f, 0), new(2.8f, 2.8f, 1.4f));
        foreach (var (x, z, turn) in new[] { (-2.4f, 1.6f, .3f), (2.6f, 1.5f, -.4f), (2.0f, 2.3f, .9f) })
        {
            var block = Art.Block(this, At(x, z, .3f), new(.8f, .6f, .6f), "c4bfb6");
            block.Rotation = new(0, turn, 0);
        }
    }

    /// <summary>A heap of broken stone: a low mound and a few loose stones on it.</summary>
    private void Heap(float x, float z)
    {
        var foot = At(x, z);
        Art.Ball(this, foot + Vector3.Up * .15f, new(1.4f, .75f, 1.2f), "9d9c95");
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.Tau / 6 + x;
            float r = i % 2 == 0 ? .6f : .9f;
            var stone = foot + new Vector3(Mathf.Sin(a) * r, .55f - r * .3f, Mathf.Cos(a) * r);
            Art.Ball(this, stone, Vector3.One * (.24f + i % 3 * .06f), i % 2 == 0 ? "b4b3ab" : "8f8a84");
        }
        Obstacles.Rock(this, foot + Vector3.Up * .3f, new(1.3f, .6f, 1.1f));
    }

    /// <summary>A board on two posts at the side of the way in, with the word "LOM".</summary>
    private void BuildSign()
    {
        var foot = At(-6.0f, -3.2f);
        foreach (float dx in new[] { -.6f, .6f })
            Art.Cylinder(this, foot + new Vector3(dx, .75f, 0), .07f, .07f, 1.9f, "7a5536");
        Art.Block(this, foot + new Vector3(0, 1.5f, 0), new(1.6f, .6f, .1f), "e9d3a2");
        Art.Block(this, foot + new Vector3(0, 1.5f, .02f), new(1.7f, .7f, .06f), "7a5536");
        Art.Sign(this, foot + new Vector3(0, 1.52f, -.1f), Texts.SignQuarry, 36);
        Obstacles.Box(this, foot + Vector3.Up * .9f, new(1.5f, 1.8f, .3f));
    }

    /// <summary>A wheelbarrow with a metal tray, a wheel at the front, two handles and two legs.</summary>
    private void BuildWheelbarrow()
    {
        var barrow = new Node3D
        {
            Name = "Wheelbarrow",
            Position = At(5.4f, -2.8f),
            Rotation = new(0, -.5f, 0),
        };
        AddChild(barrow);
        var tray = Art.Block(barrow, new(0, .62f, .05f), new(.72f, .34f, .95f), "6b8fa3");
        tray.RotationDegrees = new(-8, 0, 0);
        Art.Block(barrow, new(0, .8f, .05f), new(.78f, .05f, 1.0f), "56707a");
        Art.Ball(barrow, new(0, .76f, .05f), new(.3f, .08f, .4f), "a39d94");
        var wheel = Art.Cylinder(barrow, new(0, .25f, .72f), .25f, .25f, .1f, "3b3f45");
        wheel.RotationDegrees = new(0, 0, 90);
        foreach (int side in new[] { -1, 1 })
        {
            var handle = Art.Cylinder(barrow, new(side * .3f, .55f, -.45f), .03f, .03f, 1.3f, "a77a59");
            handle.RotationDegrees = new(78, 0, 0);
            Art.Block(barrow, new(side * .28f, .22f, -.25f), new(.05f, .44f, .05f), "56707a");
        }
        Obstacles.Box(barrow, new(0, .45f, 0), new(.8f, .9f, 1.6f));
    }
}
