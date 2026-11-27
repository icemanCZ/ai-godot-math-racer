using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The owl's stand beside the road where it leaves the old valley (<c>docs/EXPLORER_PLAN.md</c>): a big map of the valley
/// on an easel facing the road, with the mountains, the sea, the rivers and a dotted way drawn on it, a little table
/// with a rolled-up map and a compass, her tall perch with a roof against the rain, and a sign by the road. The node
/// stands at <see cref="CartographerSite.Road"/>, turned by <see cref="CartographerSite.Yaw"/>, so its frame is the
/// stand's (+Z from the road onto it). Every part stands on the ground at its own spot.
/// </summary>
public partial class CartographerStand : Node3D
{
    private readonly Terrain ground;
    private readonly Basis basis;

    internal CartographerStand(CartographerSite site, Terrain ground)
    {
        this.ground = ground;
        Position = ground.At(site.Road.X, site.Road.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        BuildEasel(2.6f, 9.5f);
        BuildTable(-1.2f, 11.6f);
        BuildPerch(-5, 12.5f);
        BuildSign(5.6f, 5.6f);
    }

    private float Floor(float x, float z)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    private Vector3 At(float x, float z, float lift = 0) => new(x, Floor(x, z) + lift, z);

    /// <summary>The big map on its easel, leaning back a little and facing the road.</summary>
    private void BuildEasel(float x, float z)
    {
        var foot = At(x, z);
        foreach (int side in new[] { -1, 1 })
        {
            var leg = Art.Block(this, foot + new Vector3(side * 1.25f, 1.5f, 0), new(.14f, 3.2f, .14f), "7a5236");
            leg.RotationDegrees = new Vector3(-8, 0, side * -4);
        }
        var back = Art.Block(this, foot + new Vector3(0, 1.4f, .7f), new(.12f, 3, .12f), "7a5236");
        back.RotationDegrees = new Vector3(18, 0, 0);
        var board = new Node3D
        {
            Name = "Map",
            Position = foot + new Vector3(0, 2.3f, -.12f),
            RotationDegrees = new Vector3(-8, 180, 0),
        };
        AddChild(board);
        // The paper, its frame, and what is drawn on it: the meadows, the mountains at the top, the sea at the bottom,
        // the river down the middle, the border river at one side, the forest at the other, and a red dotted way.
        Art.Block(board, Vector3.Zero, new(2.9f, 2.1f, .08f), "8a5a35");
        Art.Block(board, new(0, 0, .045f), new(2.7f, 1.9f, .02f), "f3e6c4");
        Art.Block(board, new(0, 0, .055f), new(2.1f, 1.3f, .01f), "a9d48a");
        for (int i = 0; i < 4; i++)
        {
            var peak = Art.Block(board, new(-.9f + i * .6f, .74f, .06f), new(.36f, .36f, .01f), "8d8a83");
            peak.RotationDegrees = new Vector3(0, 0, 45);
            Art.Block(board, new(-.9f + i * .6f, .87f, .065f), new(.14f, .1f, .01f), "fbfbf6").RotationDegrees =
                new Vector3(0, 0, 45);
        }
        Art.Block(board, new(0, -.78f, .06f), new(2.6f, .3f, .01f), "6fb2d9");
        Art.Block(board, new(.08f, 0, .062f), new(.1f, 1.45f, .01f), "6fb2d9").RotationDegrees = new(0, 0, 6);
        Art.Block(board, new(1.12f, 0, .062f), new(.14f, 1.45f, .01f), "6fb2d9");
        foreach (float y in new[] { -.45f, -.1f, .25f })
            Art.Ball(board, new(-1.1f, y, .065f), new(.12f, .12f, .01f), "3e7a3a");
        for (int i = 0; i < 6; i++)
            Art.Ball(
                board,
                new(-.7f + i * .24f, -.35f + Mathf.Sin(i * 1.2f) * .2f, .068f),
                new(.05f, .05f, .01f),
                "c0392b"
            );
        Art.Block(board, new(.72f, .1f, .07f), new(.22f, .05f, .01f), "c0392b").RotationDegrees = new(0, 0, 45);
        Art.Block(board, new(.72f, .1f, .07f), new(.22f, .05f, .01f), "c0392b").RotationDegrees = new(0, 0, -45);
        Obstacles.Box(this, foot + Vector3.Up * 1.3f, new(2.9f, 2.6f, .9f));
    }

    /// <summary>A little round table with a rolled-up map, a compass and an ink pot with a feather.</summary>
    private void BuildTable(float x, float z)
    {
        var foot = At(x, z);
        Art.Cylinder(this, foot + Vector3.Up * .5f, .14f, .14f, 1, "7a5236");
        Art.Cylinder(this, foot + Vector3.Up * 1.03f, .8f, .8f, .08f, "a77a59");
        Art.Log(this, foot + new Vector3(-.2f, 1.16f, .1f), new Vector3(1, 0, .3f), .09f, .9f, "f3e6c4");
        Art.Cylinder(this, foot + new Vector3(.35f, 1.1f, -.25f), .2f, .2f, .05f, "c9a25a");
        Art.Cylinder(this, foot + new Vector3(.35f, 1.13f, -.25f), .16f, .16f, .02f, "fff5df");
        Art.Block(this, foot + new Vector3(.35f, 1.15f, -.25f), new(.03f, .01f, .24f), "c0392b");
        Art.Cylinder(this, foot + new Vector3(-.4f, 1.13f, -.35f), .08f, .07f, .14f, "2b3d5c");
        Art.Block(this, foot + new Vector3(-.36f, 1.35f, -.35f), new(.04f, .4f, .1f), "fbfbf6").RotationDegrees =
            new Vector3(0, 0, -15);
        Obstacles.Trunk(this, foot, .8f, 1.1f);
    }

    /// <summary>The owl's perch: a tall post with a crossbar to sit on and a little pointed roof over it.</summary>
    private void BuildPerch(float x, float z)
    {
        var foot = At(x, z);
        Art.Cylinder(this, foot + Vector3.Up * 2.5f, .2f, .16f, 5, "6b4a2e");
        Art.Block(this, foot + Vector3.Up * 4.2f, new(1.6f, .14f, .16f), "6b4a2e");
        foreach (int side in new[] { -1, 1 })
        {
            var roof = Art.Block(this, foot + new Vector3(side * .45f, 5.35f, 0), new(1.1f, .1f, 1.2f), "3b78c9");
            roof.RotationDegrees = new Vector3(0, 0, -side * 35);
        }
        Art.Ball(this, foot + Vector3.Up * 5.75f, Vector3.One * .14f, "f3c23c");
        // A lantern hangs from the crossbar.
        Art.Beam(this, foot + new Vector3(.7f, 4.15f, 0), foot + new Vector3(.7f, 3.75f, 0), .03f, "3b3433");
        Art.Lamp(this, foot + new Vector3(.7f, 3.6f, 0), new(.22f, .3f, .22f), "ffd77a");
        Obstacles.Trunk(this, foot, .25f, 5);
    }

    /// <summary>The sign by the road, with a picture of a map and the word for it.</summary>
    private void BuildSign(float x, float z)
    {
        var foot = At(x, z);
        Art.Cylinder(this, foot + Vector3.Up * 1.2f, .09f, .09f, 2.4f, "7a5236");
        var board = Art.Block(this, foot + Vector3.Up * 2.25f, new(1.5f, .8f, .1f), "f3e6c4");
        board.RotationDegrees = new Vector3(0, 180, 0);
        Art.Block(this, foot + new Vector3(0, 2.25f, -.06f), new(1.1f, .5f, .02f), "a9d48a");
        Art.Block(this, foot + new Vector3(0, 2.38f, -.07f), new(1.1f, .12f, .02f), "8d8a83");
        Art.Block(this, foot + new Vector3(0, 2.08f, -.07f), new(1.1f, .12f, .02f), "6fb2d9");
        Art.Sign(this, foot + Vector3.Up * 3, Texts.SignMaps, 32);
        Obstacles.Trunk(this, foot, .1f, 2.4f);
    }
}
