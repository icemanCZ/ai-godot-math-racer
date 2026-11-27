using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Dresses the lioness princess of the castle (<c>docs/CASTLE_PLAN.md</c>): a thick mane round her face, a golden
    /// crown with red stones between her ears, a long red cape with an ermine collar and a tail with a dark tuft.
    /// </summary>
    internal void DressAsPrincess()
    {
        const string mane = "c27a2e",
            gold = "f2c14e",
            red = "b8322b";
        // The mane: a ring of tufts round the head, behind the face.
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            var tuft = Art.Ball(
                model,
                new(Mathf.Sin(a) * .72f, 1.82f + Mathf.Cos(a) * .66f, -.12f),
                new(.32f, .32f, .3f),
                mane
            );
            tuft.Rotation = new Vector3(0, 0, -a);
        }
        Art.Ball(model, new(0, 1.8f, -.32f), new(.86f, .8f, .4f), mane);
        // The crown with its points and red stones.
        Art.Cylinder(model, new(0, 2.56f, 0), .38f, .42f, .22f, gold);
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5;
            var point = new Vector3(Mathf.Sin(a) * .36f, 2.76f, Mathf.Cos(a) * .36f);
            Art.Cylinder(model, point, .07f, .01f, .2f, gold);
            Art.Ball(model, point + Vector3.Up * .11f, Vector3.One * .06f, gold);
            Art.Ball(
                model,
                new(Mathf.Sin(a) * .41f, 2.56f, Mathf.Cos(a) * .41f),
                new(.07f, .07f, .05f),
                i == 0 ? red : "4f8fd6"
            );
        }
        // The cape down her back, its ermine collar, and a golden clasp in front.
        var cape = Art.Block(model, new(0, .95f, -.5f), new(1.25f, 1.5f, .1f), red);
        cape.RotationDegrees = new Vector3(-8, 0, 0);
        Art.Block(model, new(0, .2f, -.6f), new(1.3f, .12f, .14f), "8f2420");
        Art.Ball(model, new(0, 1.42f, 0), new(.7f, .14f, .62f), "fbf6ee");
        foreach (float x in new[] { -.4f, 0f, .4f })
            Art.Ball(model, new(x, 1.42f, -.5f), Vector3.One * .05f, "1d1d1d");
        Art.Ball(model, new(0, 1.34f, .55f), new(.13f, .13f, .06f), gold);
        // The tail with its tuft.
        var tail = Art.Cylinder(model, new(.45f, .5f, -.75f), .07f, .05f, .9f, "e0a95a");
        tail.RotationDegrees = new Vector3(-55, 0, -20);
        Art.Ball(model, new(.62f, .82f, -1.12f), new(.17f, .2f, .17f), "6b3d1f");
    }

    /// <summary>
    /// A copy of how the character looks, without its marker, name or role: the guest that rides on the bench of the
    /// trailer and sits at the castle's table while the character stays at home.
    /// </summary>
    internal Node3D Figure()
    {
        var figure = (Node3D)model.Duplicate();
        figure.Name = "Figure" + DisplayName.Replace(' ', '_');
        figure.Position = Vector3.Zero;
        figure.Rotation = Vector3.Zero;
        figure.Scale = Vector3.One;
        return figure;
    }
}
