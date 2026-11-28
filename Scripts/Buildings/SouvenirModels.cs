using Godot;
using MathRacer.Graphics;

namespace MathRacer.Buildings;

/// <summary>
/// The eight treasures of the captain's cabin (<see cref="ShipPlans.Names"/>) as small models, for the shelf on the
/// ship's deck and the nests of the turtle island: a bottle with a letter, a compass, a ship's bell, a telescope, a
/// coconut, a pineapple, a coral and a chest of ducats. Each stands on y = 0 and is about <c>size</c> high.
/// </summary>
internal static class SouvenirModels
{
    internal static void Build(Node3D parent, int kind, float size)
    {
        var s = Vector3.One * size;
        switch (kind)
        {
            case 0:
                Art.Ball(parent, new Vector3(0, .32f, 0) * size, new Vector3(.5f, .62f, .5f) * size, "6fb08a");
                Art.Cylinder(parent, new Vector3(0, .75f, 0) * size, .1f * size, .1f * size, .3f * size, "6fb08a");
                Art.Block(parent, new Vector3(0, .9f, 0) * size, new Vector3(.14f, .1f, .14f) * size, "a47a4a");
                Art.Block(parent, new Vector3(0, .36f, 0) * size, new Vector3(.3f, .34f, .52f) * size, "f6eed6");
                break;
            case 1:
                Art.Cylinder(parent, new Vector3(0, .1f, 0) * size, .45f * size, .45f * size, .2f * size, "e0b04a");
                Art.Cylinder(parent, new Vector3(0, .2f, 0) * size, .37f * size, .37f * size, .04f * size, "f6eed6");
                Art.Block(parent, new Vector3(0, .24f, .12f) * size, new Vector3(.08f, .04f, .3f) * size, "d23b2f");
                Art.Block(parent, new Vector3(0, .24f, -.12f) * size, new Vector3(.08f, .04f, .3f) * size, "3c5960");
                break;
            case 2:
                Art.Cylinder(parent, new Vector3(0, .4f, 0) * size, .4f * size, .22f * size, .6f * size, "e0b04a");
                Art.Ball(parent, new Vector3(0, .72f, 0) * size, new Vector3(.24f, .14f, .24f) * size, "e0b04a");
                Art.Ball(parent, new Vector3(0, .06f, 0) * size, new Vector3(.12f, .12f, .12f) * size, "8a6a2a");
                break;
            case 3:
                var tube = Art.Cylinder(
                    parent,
                    new Vector3(0, .2f, 0) * size,
                    .16f * size,
                    .1f * size,
                    .95f * size,
                    "8a6040"
                );
                tube.RotationDegrees = new Vector3(90, 0, 0);
                var ring = Art.Cylinder(
                    parent,
                    new Vector3(0, .2f, .4f) * size,
                    .18f * size,
                    .18f * size,
                    .1f * size,
                    "e0b04a"
                );
                ring.RotationDegrees = new Vector3(90, 0, 0);
                break;
            case 4:
                Art.Ball(parent, new Vector3(0, .4f, 0) * size, s * .8f, "6a4a30");
                foreach (float x in new[] { -.12f, 0f, .12f })
                    Art.Ball(parent, new Vector3(x, .62f, .3f) * size, s * .08f, "3a2a1e");
                break;
            case 5:
                Art.Ball(parent, new Vector3(0, .35f, 0) * size, new Vector3(.55f, .7f, .55f) * size, "e8b33a");
                for (int i = 0; i < 5; i++)
                {
                    var leaf = Art.Block(
                        parent,
                        new Vector3(0, .85f, 0) * size,
                        new Vector3(.08f, .45f, .16f) * size,
                        "5c9a3e"
                    );
                    leaf.RotationDegrees = new Vector3(18, i * 72, 0);
                }
                break;
            case 6:
                foreach (
                    var (x, z, tilt) in new[]
                    {
                        (0f, 0f, 0f),
                        (.18f, .05f, 28f),
                        (-.16f, -.04f, -30f),
                        (.02f, .16f, 20f),
                    }
                )
                {
                    var branch = Art.Cylinder(
                        parent,
                        new Vector3(x, .38f, z) * size,
                        .07f * size,
                        .1f * size,
                        .75f * size,
                        "ef7a6a"
                    );
                    branch.RotationDegrees = new Vector3(tilt * .5f, 0, tilt);
                }
                break;
            default:
                Art.Block(parent, new Vector3(0, .25f, 0) * size, new Vector3(.8f, .5f, .55f) * size, "8a5a30");
                Art.Block(parent, new Vector3(0, .58f, 0) * size, new Vector3(.82f, .16f, .57f) * size, "6b4022");
                Art.Block(parent, new Vector3(0, .4f, .29f) * size, new Vector3(.14f, .2f, .04f) * size, "e0b04a");
                foreach (float x in new[] { -.2f, 0f, .2f })
                    Art.Cylinder(parent, new Vector3(x, .7f, 0) * size, .1f * size, .1f * size, .05f * size, "f3c23c");
                break;
        }
    }
}
