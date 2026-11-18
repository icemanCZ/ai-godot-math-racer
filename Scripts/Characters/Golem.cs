using System;
using System.Linq;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

internal static class GolemModel
{
    internal static void Part(Node3D parent, int index, Vector3 position)
    {
        var piece = new Node3D { Position = position, Scale = Vector3.One * .65f };
        parent.AddChild(piece);
        Art.Block(
            piece,
            Vector3.Zero,
            index == 0 ? new(1, .8f, .65f)
                : index == 1 ? new(.75f, .7f, .7f)
                : new(.35f, .75f, .4f),
            "9b9c91"
        );
        Art.Block(piece, new(0, .08f, -.36f), new(.18f, .25f, .04f), "75d7c2");
    }

    internal static Node3D Create()
    {
        var golem = new Node3D { Name = "GolemPet" };
        Art.Block(golem, new(0, 1, 0), new(1.15f, 1.15f, .7f), "92978e");
        Art.Block(golem, new(0, 1.95f, 0), new(.9f, .8f, .75f), "aaa99c");
        foreach (float x in new[] { -.25f, .25f })
            Art.Ball(golem, new(x, 2, -.39f), new(.09f, .11f, .06f), "78e6d0");
        Art.Block(golem, new(0, 1.15f, -.36f), new(.2f, .3f, .04f), "78e6d0");
        foreach (float x in new[] { -.72f, .72f })
            Art.Block(golem, new(x, 1, 0), new(.3f, .9f, .35f), "858b82");
        foreach (float x in new[] { -.32f, .32f })
        {
            var leg = new Node3D { Name = x < 0 ? "LeftLeg" : "RightLeg", Position = new(x, .5f, 0) };
            golem.AddChild(leg);
            Art.Block(leg, new(0, -.22f, 0), new(.36f, .55f, .48f), "777f78");
        }
        return golem;
    }
}
