using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The mountain slope beside the road to the mountains (<c>docs/DRAGON_PLAN.md</c>, phase 2), where the trailer is loaded
/// for the dragon's nest: a rock with scree at its foot, a sandy bank, a few fallen trunks, a sign and a pick. The node
/// stands at <see cref="MountainSlopeSite.Position"/>, turned by <see cref="MountainSlopeSite.Yaw"/>; the side towards
/// the road (local -Z) is left open for the car and its trailer: nothing solid stands at local z below 1 m within 3.5 m
/// of the middle.
/// </summary>
public partial class MountainSlope : Node3D
{
    private readonly Terrain ground;
    private readonly Basis basis;

    internal MountainSlopeSite Site { get; }

    /// <summary>The rock and the bank, for the camera.</summary>
    internal Vector3 Focus => Position + basis * new Vector3(0, 1.6f, 3.6f);

    internal MountainSlope(MountainSlopeSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "MountainSlope";
        Position = ground.At(site.Position.X, site.Position.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        BuildRock();
        BuildSand();
        BuildTrunks();
        BuildSign();
    }

    /// <summary>A point of the slope's frame on the ground, lifted by <paramref name="lift"/>.</summary>
    private Vector3 At(float x, float z, float lift = 0)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return new Vector3(x, ground.Height(p.X, p.Z) - Position.Y + lift, z);
    }

    /// <summary>A grey rock at the back with a heap of broken stone at its foot.</summary>
    private void BuildRock()
    {
        foreach (
            var (x, z, size, color) in new[]
            {
                (1.6f, 5.4f, new Vector3(2.6f, 2.6f, 1.9f), "8d8a85"),
                (-1.2f, 5.9f, new Vector3(2.0f, 3.2f, 1.6f), "7f7b75"),
            }
        )
        {
            var center = At(x, z, size.Y * .45f);
            Art.Ball(this, center, size, color);
            Art.Ball(this, center + new Vector3(size.X * .2f, size.Y * .5f, -size.Z * .2f), size * .35f, "eef2f5");
            Obstacles.Rock(this, center, size * .85f);
        }
        var foot = At(1.2f, 3.0f);
        Art.Ball(this, foot + Vector3.Up * .1f, new(1.6f, .7f, 1.1f), "9d9c95");
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.Tau / 7 + .4f;
            float r = i % 2 == 0 ? .7f : 1.1f;
            var stone = foot + new Vector3(Mathf.Sin(a) * r, .5f - r * .25f, Mathf.Cos(a) * r * .7f);
            Art.Ball(this, stone, Vector3.One * (.22f + i % 3 * .06f), i % 2 == 0 ? "b4b3ab" : "8f8a84");
        }
        Obstacles.Rock(this, foot + Vector3.Up * .3f, new(1.4f, .6f, 1.0f));
    }

    /// <summary>The sandy bank of a little stream on one side, with a spade stuck in it.</summary>
    private void BuildSand()
    {
        var bank = At(-4.6f, 2.4f);
        Art.Ball(this, bank + Vector3.Up * .05f, new(1.8f, .7f, 1.5f), "e8cf8f");
        Art.Ball(this, bank + new Vector3(.9f, .25f, -.4f), new(.8f, .4f, .7f), "dcbd78");
        var stream = Art.Block(this, At(-6.4f, 1.0f, .03f), new(.9f, .04f, 5.5f), "6fb3d9");
        stream.Rotation = new Vector3(0, .25f, 0);
        var spade = new Node3D { Position = bank + new Vector3(.3f, .7f, .2f), Rotation = new(.2f, .4f, -.15f) };
        AddChild(spade);
        Art.Cylinder(spade, new(0, .5f, 0), .03f, .03f, 1.1f, "a77a59");
        Art.Block(spade, new(0, -.15f, 0), new(.26f, .34f, .03f), "8a9399");
        Obstacles.Rock(this, bank + Vector3.Up * .25f, new(1.6f, .5f, 1.3f));
    }

    /// <summary>Fallen trunks on the other side, one across another, with their branches lopped.</summary>
    private void BuildTrunks()
    {
        foreach (
            var (x, z, turn, lift) in new[]
            {
                (5.0f, 2.4f, .2f, .32f),
                (5.3f, 3.1f, -.35f, .32f),
                (5.1f, 2.7f, .05f, .9f),
            }
        )
        {
            var direction = new Vector3(Mathf.Sin(turn), 0, Mathf.Cos(turn));
            var log = Art.Log(this, At(x, z, lift), direction, .32f, 4.2f, "7d4f2e");
            Art.Cylinder(log, new(.3f, .6f, 0), .07f, .05f, .5f, "6a4024").Rotation = new(0, 0, -1.1f);
        }
        Obstacles.Box(this, At(5.1f, 2.7f, .6f), new(1.9f, 1.3f, 4.4f), new Basis(Vector3.Up, .05f));
    }

    /// <summary>A board on two posts at the side of the way in, with the word "STRÁŇ".</summary>
    private void BuildSign()
    {
        var foot = At(-5.6f, -3.2f);
        foreach (float dx in new[] { -.6f, .6f })
            Art.Cylinder(this, foot + new Vector3(dx, .75f, 0), .07f, .07f, 1.9f, "7a5536");
        Art.Block(this, foot + new Vector3(0, 1.5f, 0), new(1.6f, .6f, .1f), "e9d3a2");
        Art.Block(this, foot + new Vector3(0, 1.5f, .02f), new(1.7f, .7f, .06f), "7a5536");
        Art.Sign(this, foot + new Vector3(0, 1.52f, -.1f), OutlandTexts.SignSlope, 36);
        Obstacles.Box(this, foot + Vector3.Up * .9f, new(1.5f, 1.8f, .3f));
    }
}
