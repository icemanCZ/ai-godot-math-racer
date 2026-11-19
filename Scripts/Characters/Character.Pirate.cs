using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// A black three-cornered hat with a skull, an eye patch, a red scarf and a little red parrot on the shoulder, and a
    /// rolled treasure map in the paw.
    /// </summary>
    internal void DressAsPirate()
    {
        // The hat: a crown and two turned-up brims.
        Art.Cylinder(model, new(0, 2.5f, 0), .62f, .5f, .34f, "2b2b33");
        Art.Block(model, new(0, 2.34f, .1f), new(1.5f, .08f, .8f), "2b2b33");
        foreach (float x in new[] { -.7f, .7f })
            Art.Ball(model, new(x, 2.46f, .1f), new(.26f, .2f, .5f), "2b2b33");
        Art.Ball(model, new(0, 2.5f, .5f), new(.15f, .13f, .05f), "f5f0e0");
        Art.Block(model, new(0, 2.5f, .53f), new(.2f, .04f, .03f), "2b2b33");
        // The eye patch with its string.
        Art.Ball(model, new(.36f, 1.93f, .7f), new(.26f, .26f, .08f), "1f1f26");
        Art.Block(model, new(0, 2.12f, .62f), new(1.1f, .04f, .04f), "1f1f26").RotationDegrees = new(0, 0, 14);
        // The scarf.
        Art.Cylinder(model, new(0, 1.28f, 0), .45f, .47f, .18f, "d9432f");
        // The parrot: a body, a head, a beak, a tail and a wing.
        var parrot = new Node3D { Name = "Parrot", Position = new(-.62f, 2.0f, .02f) };
        model.AddChild(parrot);
        Art.Ball(parrot, new(0, 0, 0), new(.26f, .34f, .26f), "e9362e");
        Art.Ball(parrot, new(0, .26f, .06f), new(.2f, .2f, .2f), "e9362e");
        Art.Ball(parrot, new(0, .24f, .2f), new(.08f, .1f, .1f), "f2c14e");
        Art.Ball(parrot, new(.1f, .3f, .14f), new(.05f, .05f, .03f), "ffffff");
        Art.Ball(parrot, new(0, -.3f, -.12f), new(.1f, .4f, .06f), "2f77d1");
        Art.Ball(parrot, new(-.16f, .0f, 0), new(.07f, .26f, .2f), "f2b33d");
        // The rolled map.
        var map = Art.Cylinder(model, new(.92f, 1.1f, .38f), .13f, .13f, .7f, "f1deb0");
        map.RotationDegrees = new(0, 0, 70);
        Art.Cylinder(model, new(.92f, 1.1f, .38f), .15f, .15f, .08f, "c0504d").RotationDegrees = new(0, 0, 70);
    }
}
