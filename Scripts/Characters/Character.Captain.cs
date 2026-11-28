using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Dresses the walrus captain of the ship Albatros (<c>docs/SHIP_PLAN.md</c>): two long white tusks and a bushy
    /// moustache, a navy captain's cap with a white top and a golden anchor, a navy coat with two rows of golden buttons
    /// and a pipe.
    /// </summary>
    internal void DressAsCaptain()
    {
        const string navy = "23365a",
            gold = "e0b04a",
            ivory = "f4efe0";
        // The moustache and the tusks.
        Art.Ball(model, new(0, 1.86f, .6f), new(.62f, .2f, .22f), "c9b8a0");
        foreach (float x in new[] { -.16f, .16f })
        {
            var tusk = Art.Cylinder(model, new(x, 1.62f, .64f), .05f, .02f, .5f, ivory);
            tusk.RotationDegrees = new Vector3(-12, 0, x > 0 ? -6 : 6);
        }
        // The cap: a white top over a navy band, a black visor and a golden anchor.
        Art.Cylinder(model, new(0, 2.44f, -.02f), .64f, .62f, .18f, navy);
        Art.Cylinder(model, new(0, 2.58f, -.04f), .74f, .7f, .1f, "f6f4ee");
        Art.Block(model, new(0, 2.36f, .58f), new(.78f, .06f, .38f), "1d1d22");
        Art.Block(model, new(0, 2.47f, .63f), new(.05f, .16f, .03f), gold);
        Art.Block(model, new(0, 2.41f, .63f), new(.16f, .04f, .03f), gold);
        // The coat with two rows of buttons.
        Art.Block(model, new(0, .9f, .5f), new(1.08f, 1.2f, .12f), navy);
        foreach (float x in new[] { -.2f, .2f })
        foreach (float y in new[] { .65f, .95f, 1.25f })
            Art.Ball(model, new(x, y, .58f), Vector3.One * .06f, gold);
        // A pipe in the corner of his mouth.
        Art.Beam(model, new(.24f, 1.78f, .66f), new(.46f, 1.72f, .78f), .05f, "5a3a24");
        Art.Cylinder(model, new(.48f, 1.8f, .8f), .07f, .06f, .16f, "5a3a24");
    }
}
