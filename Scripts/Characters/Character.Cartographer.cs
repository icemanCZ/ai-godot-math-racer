using System.Linq;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Dresses the owl, the mapmaker of the valley (<c>docs/EXPLORER_PLAN.md</c>): feathered ear tufts, a pale face with
    /// big round eyes behind golden spectacles and a little hooked beak, a speckled pale breast, wings folded at her
    /// sides, and a satchel on a strap with rolled-up maps sticking out of it.
    /// </summary>
    internal void DressAsOwl()
    {
        const string feather = "7d5c3e",
            pale = "eedcbc",
            gold = "c9a25a";
        // An owl has no round ears, muzzle or nose of the animals; her feet are orange talons.
        foreach (var part in model.GetChildren().OfType<MeshInstance3D>())
        {
            var p = part.Position;
            bool ear = p.Y > 2.3f && p.Y < 2.5f && Mathf.Abs(Mathf.Abs(p.X) - .36f) < .01f;
            bool snout =
                Mathf.Abs(p.X) < .01f && (p.IsEqualApprox(new(0, 1.5f, .5f)) || p.IsEqualApprox(new(0, 1.58f, .76f)));
            if (ear || snout)
                part.Visible = false;
            else if (Mathf.Abs(p.Y - .24f) < .01f)
                part.MaterialOverride = Art.Mat("e8a33a");
        }
        foreach (int side in new[] { -1, 1 })
        {
            var tuft = Art.Ball(model, new(side * .42f, 2.5f, .02f), new(.13f, .34f, .11f), feather);
            tuft.RotationDegrees = new Vector3(0, 0, -side * 24);
        }
        // The face: a pale disc in front of the head, the big eyes on it, the spectacles over them and the beak.
        Art.Ball(model, new(0, 1.86f, .44f), new(.72f, .58f, .3f), pale);
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * .25f;
            Art.Ball(model, new(x, 1.95f, .7f), new(.2f, .21f, .07f), "fffdf5");
            Art.Ball(model, new(x, 1.95f, .76f), new(.11f, .12f, .03f), "2a2522");
            Art.Ball(model, new(x - .04f, 2, .79f), Vector3.One * .03f, "ffffff");
            var rim = Art.Cylinder(model, new(x, 1.95f, .78f), .24f, .24f, .03f, gold);
            rim.RotationDegrees = new Vector3(90, 0, 0);
            var glass = Art.Cylinder(model, new(x, 1.95f, .775f), .21f, .21f, .02f, "d9eef8");
            glass.RotationDegrees = new Vector3(90, 0, 0);
            glass.Transparency = .65f;
        }
        Art.Block(model, new(0, 1.96f, .8f), new(.1f, .03f, .03f), gold);
        var beak = Art.Ball(model, new(0, 1.7f, .82f), new(.09f, .15f, .09f), "e8a33a");
        beak.RotationDegrees = new Vector3(18, 0, 0);
        // A speckled pale breast and the wings folded at her sides.
        Art.Ball(model, new(0, .9f, .4f), new(.46f, .62f, .22f), pale);
        foreach (var (x, y) in new[] { (-.15f, 1.1f), (.15f, 1.1f), (0, .9f), (-.18f, .72f), (.18f, .72f), (0, .55f) })
            Art.Block(model, new(x, y, .61f), new(.1f, .04f, .02f), feather).RotationDegrees = new(0, 0, x * 120);
        foreach (int side in new[] { -1, 1 })
        {
            var wing = Art.Ball(model, new(side * .6f, .92f, -.05f), new(.24f, .64f, .42f), feather);
            wing.RotationDegrees = new Vector3(0, 0, side * 6);
            Art.Ball(model, new(side * .62f, .5f, -.1f), new(.18f, .26f, .3f), "6a4c31");
        }
        // The satchel on its strap, with two rolled maps in it.
        var strap = Art.Block(model, new(0, 1.05f, .5f), new(.12f, 1.25f, .06f), "8a5a35");
        strap.RotationDegrees = new Vector3(0, 0, -38);
        var bag = new Node3D { Name = "Satchel", Position = new(.62f, .55f, .2f) };
        model.AddChild(bag);
        Art.Block(bag, Vector3.Zero, new(.18f, .42f, .52f), "8a5a35");
        Art.Block(bag, new(.01f, .14f, 0), new(.2f, .14f, .54f), "6b4a2e");
        Art.Log(bag, new(0, .35f, .1f), new Vector3(0, 1, .25f), .06f, .55f, "f3e6c4");
        Art.Log(bag, new(.02f, .3f, -.12f), new Vector3(0, 1, -.2f), .05f, .45f, "e6d3a8");
    }
}
