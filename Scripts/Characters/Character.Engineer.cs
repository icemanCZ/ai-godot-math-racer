using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Dresses the badger engine driver of the great line (<c>docs/MAINLINE_PLAN.md</c>): the two dark stripes of a badger
    /// down his white face, a blue cap with a visor and a brass badge, blue overalls with a red neckerchief, and a watch
    /// on a chain.
    /// </summary>
    internal void DressAsEngineer()
    {
        const string cap = "2f5d8a",
            brass = "e0b04a";
        // The badger's stripes: a white blaze between two dark bands from the nose over the eyes to the ears.
        foreach (float x in new[] { -.3f, .3f })
        {
            var band = Art.Block(model, new(x, 1.9f, .5f), new(.2f, .1f, .5f), "2b2b2e");
            band.RotationDegrees = new Vector3(0, 0, x > 0 ? -10 : 10);
        }
        Art.Block(model, new(0, 1.98f, .54f), new(.2f, .06f, .45f), "f4f1ea");
        // The cap and its visor.
        Art.Cylinder(model, new(0, 2.5f, -.02f), .66f, .6f, .28f, cap);
        Art.Block(model, new(0, 2.38f, .58f), new(.8f, .06f, .4f), "1f3f5e");
        Art.Ball(model, new(0, 2.54f, .6f), new(.1f, .09f, .04f), brass);
        // The overalls with two buttons and a neckerchief.
        Art.Block(model, new(0, .85f, .52f), new(1.05f, 1.1f, .12f), "3a6fa8");
        foreach (float x in new[] { -.28f, .28f })
            Art.Ball(model, new(x, 1.2f, .6f), Vector3.One * .05f, brass);
        Art.Block(model, new(0, 1.4f, .5f), new(.62f, .22f, .22f), "d23b2f");
        // A watch on a chain.
        Art.Ball(model, new(.3f, .82f, .62f), new(.1f, .1f, .04f), brass);
        Art.Beam(model, new(.3f, .9f, .6f), new(0, 1.2f, .6f), .02f, brass);
    }
}
