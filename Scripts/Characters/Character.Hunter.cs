using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// The badger's white stripe with a dark band on each side, a green hunter's hat with a band and a feather, a green
    /// waistcoat with buttons and a landing net on a long pole in the paw.
    /// </summary>
    internal void DressAsHunter()
    {
        // The badger's face.
        Art.Ball(model, new(0, 2.08f, .42f), new(.16f, .46f, .22f), "f4f1ea");
        foreach (int side in new[] { -1, 1 })
            Art.Ball(model, new(side * .2f, 2.02f, .44f), new(.11f, .42f, .17f), "2d2a28");
        // The hat.
        Art.Cylinder(model, new(0, 2.44f, 0), .74f, .74f, .07f, "4f6b3a");
        Art.Cylinder(model, new(0, 2.68f, 0), .5f, .38f, .46f, "5c7a43");
        Art.Cylinder(model, new(0, 2.52f, 0), .515f, .5f, .09f, "6b4a2e");
        Art.Ball(model, new(.4f, 2.86f, -.1f), new(.05f, .34f, .1f), "c0504d").RotationDegrees = new(0, 0, -28);
        Art.Ball(model, new(.47f, 2.8f, -.14f), new(.04f, .26f, .08f), "f2c14e").RotationDegrees = new(0, 0, -36);
        // The waistcoat.
        Art.Block(model, new(0, .98f, .45f), new(.82f, .95f, .12f), "4f6b3a");
        Art.Block(model, new(0, .98f, .52f), new(.06f, .95f, .03f), "3f5a30");
        foreach (float y in new[] { .7f, .95f, 1.2f })
            Art.Ball(model, new(.1f, y, .53f), Vector3.One * .045f, "d9b44a");
        // The landing net: a long pole, a hoop and a bag of rope.
        var pole = Art.Cylinder(model, new(-.8f, 1.55f, .35f), .04f, .04f, 2.6f, "8a6440");
        pole.RotationDegrees = new(0, 0, -8);
        var hoop = new Node3D { Name = "NetHoop", Position = new(-.98f, 2.95f, .35f) };
        model.AddChild(hoop);
        Art.Cylinder(hoop, Vector3.Zero, .36f, .36f, .05f, "6b4a2e").RotationDegrees = new(0, 0, 90);
        Art.Cylinder(hoop, Vector3.Zero, .3f, .3f, .055f, "e4cf9e").RotationDegrees = new(0, 0, 90);
        var bag = Art.Ball(hoop, new(-.02f, -.22f, 0), new(.08f, .34f, .26f), "d8c49a");
        bag.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color("d8c49a").SrgbToLinear() with { A = .55f },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
    }
}
