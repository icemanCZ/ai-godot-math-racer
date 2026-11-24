using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Dresses the balloonist of the balloon station (<c>docs/BALLOON_PLAN.md</c>), a fox: a brown leather flying cap with
    /// flying goggles pushed up on it, a leather jacket, a long red scarf whose end flutters, and a brass telescope in her
    /// paw, for watching the balloon from the ground.
    /// </summary>
    /// <summary>
    /// Seen from afar, her marker rides <paramref name="lift"/> higher, on top of her balloon, so that it does not hide the
    /// balloon; near her it comes down above her head.
    /// </summary>
    internal void RaiseFarMarker(float lift) => farMarkerLift = lift;

    internal void DressAsBalloonist()
    {
        // The flying cap, close over the head, and its two ear flaps.
        Art.Ball(model, new(0, 2.22f, -.04f), new(.84f, .46f, .66f), "7a5236");
        foreach (int side in new[] { -1, 1 })
            Art.Ball(model, new(side * .4f, 1.98f, .12f), new(.1f, .24f, .18f), "6a4630");
        // The goggles pushed up on the cap: two round lenses in brass rims on a dark strap.
        Art.Block(model, new(0, 2.36f, .36f), new(.7f, .07f, .2f), "3b2f28").RotationDegrees = new(-38, 0, 0);
        foreach (int side in new[] { -1, 1 })
        {
            var rim = Art.Cylinder(model, new(side * .16f, 2.4f, .44f), .13f, .13f, .08f, "c9a25a");
            rim.RotationDegrees = new(52, 0, 0);
            var lens = Art.Cylinder(model, new(side * .16f, 2.41f, .47f), .095f, .095f, .05f, "9fd3f0");
            lens.RotationDegrees = new(52, 0, 0);
        }
        // The leather jacket, the scarf round the neck and its long end.
        Coat("8a5a35");
        Collar("d9432f");
        var end = Art.Block(model, new(.24f, 1.02f, .6f), new(.17f, .6f, .05f), "d9432f");
        end.RotationDegrees = new(0, 0, 12);
        Art.Block(model, new(.3f, .7f, .61f), new(.18f, .05f, .052f), "f6d03f").RotationDegrees = new(0, 0, 12);
        foreach (float y in new[] { .62f, .88f, 1.14f })
            Art.Ball(model, new(-.08f, y, .6f), Vector3.One * .045f, "c9a25a");
        // The brass telescope, held up beside her.
        var scope = new Node3D
        {
            Name = "Telescope",
            Position = new(-.78f, 1.25f, .3f),
            RotationDegrees = new(-30, 0, -10),
        };
        model.AddChild(scope);
        Art.Cylinder(scope, Vector3.Zero, .06f, .07f, .5f, "c9a25a");
        Art.Cylinder(scope, new(0, .36f, 0), .08f, .085f, .26f, "b08a3e");
        Art.Cylinder(scope, new(0, -.29f, 0), .045f, .045f, .1f, "3b2f28");
    }
}
