using System.Linq;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Dresses the heron who watches birds (<c>docs/FERRY_PLAN.md</c>): a white face with a black stripe over the eye and
    /// a long black plume behind, a long yellow beak, a pale streaked front, grey wings folded at her sides, yellow feet,
    /// a green hat with a wide brim and a pair of binoculars on a strap round her neck.
    /// </summary>
    internal void DressAsHeron()
    {
        const string grey = "9aa6b2",
            pale = "eef1f3",
            yellow = "e8c23a";
        // A heron has no round ears, muzzle or nose of the animals; her feet are yellow.
        foreach (var part in model.GetChildren().OfType<MeshInstance3D>())
        {
            var p = part.Position;
            bool ear = p.Y > 2.3f && p.Y < 2.5f && Mathf.Abs(Mathf.Abs(p.X) - .36f) < .01f;
            bool snout =
                Mathf.Abs(p.X) < .01f && (p.IsEqualApprox(new(0, 1.5f, .5f)) || p.IsEqualApprox(new(0, 1.58f, .76f)));
            if (ear || snout)
                part.Visible = false;
            else if (Mathf.Abs(p.Y - .24f) < .01f)
                part.MaterialOverride = Art.Mat(yellow);
        }
        // The face: white in front of the head, a black stripe from the eye back to the plume, and the long beak.
        Art.Ball(model, new(0, 1.86f, .42f), new(.66f, .56f, .3f), pale);
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * .22f;
            Art.Ball(model, new(x, 1.96f, .66f), new(.13f, .13f, .06f), "f4e27a");
            Art.Ball(model, new(x, 1.96f, .69f), new(.07f, .08f, .03f), "1d1d1d");
            Art.Ball(model, new(x - .02f, 1.99f, .705f), Vector3.One * .02f, "ffffff");
            var stripe = Art.Block(model, new(side * .3f, 2.02f, .3f), new(.05f, .07f, .5f), "222222");
            stripe.RotationDegrees = new Vector3(-8, 0, 0);
        }
        var beak = Art.Block(model, new(0, 1.74f, 1.02f), new(.13f, .11f, .8f), yellow);
        beak.RotationDegrees = new Vector3(6, 0, 0);
        Art.Block(model, new(0, 1.7f, 1.0f), new(.1f, .03f, .76f), "c99f22").RotationDegrees = new Vector3(6, 0, 0);
        var plume = Art.Block(model, new(0, 2.05f, -.55f), new(.06f, .06f, .7f), "222222");
        plume.RotationDegrees = new Vector3(-22, 0, 0);
        // The hat with its wide brim, worn over the plume's root.
        Art.Cylinder(model, new(0, 2.42f, 0), .62f, .62f, .05f, "5f7f3f");
        Art.Cylinder(model, new(0, 2.56f, 0), .34f, .38f, .28f, "6f9147");
        Art.Cylinder(model, new(0, 2.47f, 0), .385f, .385f, .06f, "3f5a2a");
        // A pale front with dark streaks, and the grey wings folded at her sides.
        Art.Ball(model, new(0, .95f, .4f), new(.44f, .62f, .22f), pale);
        foreach (float y in new[] { 1.18f, 1.0f, .82f, .64f })
            Art.Block(model, new(0, y, .61f), new(.05f, .1f, .02f), "6f7a84");
        foreach (int side in new[] { -1, 1 })
        {
            var wing = Art.Ball(model, new(side * .6f, .95f, -.08f), new(.24f, .66f, .46f), grey);
            wing.RotationDegrees = new Vector3(0, 0, side * 6);
            Art.Ball(model, new(side * .62f, .52f, -.18f), new(.18f, .28f, .32f), "7f8b97");
        }
        // The binoculars on their strap.
        var strap = Art.Block(model, new(0, 1.32f, .52f), new(.7f, .04f, .04f), "5a3d26");
        strap.RotationDegrees = new Vector3(0, 0, 0);
        foreach (int side in new[] { -1, 1 })
        {
            var tube = Art.Cylinder(model, new(side * .09f, 1.14f, .66f), .07f, .07f, .24f, "2f2f2f");
            tube.RotationDegrees = new Vector3(90, 0, 0);
            Art.Cylinder(tube, new Vector3(0, .13f, 0), .075f, .075f, .03f, "6f9fc9");
        }
        Art.Block(model, new(0, 1.14f, .66f), new(.1f, .05f, .1f), "2f2f2f");
    }
}
