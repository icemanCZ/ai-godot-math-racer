using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The lookout tower: a stone foundation, four brick storeys, a ladder with landings, a wooden platform with a
/// railing and a roof, and at last the stone tip with a flag.
/// </summary>
public partial class Lookout : SitedProject
{
    private const float CenterZ = 12;
    private const float StoreyHeight = 3;

    private float topY;

    /// <summary>Height of the platform above the ring.</summary>
    internal float PlatformHeight => topY;

    internal override Vector3 Focus => Site.Position + Site.Inward * CenterZ + Vector3.Up * 6;

    internal Lookout(BuildSite site, Terrain ground)
        : base(site, ground)
    {
        Name = "Lookout";
    }

    private protected override void Build()
    {
        const float z = CenterZ;
        float y = Pad(Stages[0], 0, z, 8, 8, "8f8f8a");
        // 0-2 gravel: three layers of stone foundation, each smaller than the last, with gravel heaps.
        for (int layer = 0; layer < 3; layer++)
        {
            Art.Block(
                Stages[layer],
                new(0, y + .15f + layer * .3f, z),
                new(6.8f - layer * .6f, .3f, 6.8f - layer * .6f),
                layer == 0 ? "9d9c95"
                    : layer == 1 ? "a3a39d"
                    : "b4b3ab"
            );
            foreach (float x in new[] { -3.6f, 3.6f })
                Art.Ball(Stages[layer], new(x, y + .2f, z - 1.5f + layer * 1.5f), new(.9f, .4f, .9f), "b9b4a5");
        }
        Art.Block(Stages[2], new(0, y + .3f, z - 3.6f), new(2.2f, .3f, .9f), "b4b3ab");
        float baseY = y + .9f;
        // 3-6 bricks: four storeys, each a little narrower, with corner posts, a band and windows.
        for (int storey = 0; storey < 4; storey++)
        {
            var stage = Stages[3 + storey];
            float width = 4.6f - storey * .25f;
            float middle = baseY + StoreyHeight / 2 + storey * StoreyHeight;
            Art.Block(stage, new(0, middle, z), new(width, StoreyHeight, width), storey % 2 == 0 ? "b5533b" : "a24a33");
            Art.Block(stage, new(0, middle + StoreyHeight / 2, z), new(width + .3f, .2f, width + .3f), "e0c9a0");
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                Art.Block(
                    stage,
                    new(sx * width / 2, middle, z + sz * width / 2),
                    new(.4f, StoreyHeight, .4f),
                    "9b4a35"
                );
            foreach (float side in new[] { -1f, 1f })
            {
                Art.Window(stage, new(0, middle, z + side * (width / 2 + .02f)), new(.9f, 1.2f, .12f), "3a2a26");
                Art.Window(stage, new(side * (width / 2 + .02f), middle, z), new(.12f, 1.2f, .9f), "3a2a26");
            }
            Solid(3 + storey, new(0, middle, z), new(width, StoreyHeight, width));
        }
        topY = baseY + StoreyHeight * 4;
        // 7 planks: a ladder up the front with a wooden landing at every storey.
        float frontZ = z - 2.3f - .1f;
        foreach (float x in new[] { -.4f, .4f })
            Art.Cylinder(
                Stages[7],
                new(x + 1.2f, baseY + StoreyHeight * 2, frontZ - .15f),
                .06f,
                .06f,
                StoreyHeight * 4,
                "a77a59"
            );
        for (int rung = 0; rung < StoreyHeight * 4 * 2; rung++)
            Art.Block(Stages[7], new(1.2f, baseY + .3f + rung * .5f, frontZ - .15f), new(.9f, .06f, .08f), "c39770");
        for (int storey = 0; storey < 4; storey++)
            Art.Block(
                Stages[7],
                new(1.2f, baseY + StoreyHeight * (storey + 1), frontZ - .45f),
                new(1.3f, .12f, .9f),
                "c39770"
            );
        // 8 planks: the platform with its railing and four roof posts.
        Art.Block(Stages[8], new(0, topY + .12f, z), new(5.6f, .25f, 5.6f), "c39770");
        for (int i = 0; i < 5; i++)
        {
            float offset = -2.4f + i * 1.2f;
            foreach (float edge in new[] { -2.6f, 2.6f })
            {
                Art.Cylinder(Stages[8], new(offset, topY + .75f, z + edge), .07f, .07f, 1.2f, "a77a59");
                Art.Cylinder(Stages[8], new(edge, topY + .75f, z + offset), .07f, .07f, 1.2f, "a77a59");
            }
        }
        foreach (float edge in new[] { -2.6f, 2.6f })
        {
            Art.Beam(Stages[8], new(-2.6f, topY + 1.3f, z + edge), new(2.6f, topY + 1.3f, z + edge), .12f, "e2bb8b");
            Art.Beam(Stages[8], new(edge, topY + 1.3f, z - 2.6f), new(edge, topY + 1.3f, z + 2.6f), .12f, "e2bb8b");
        }
        foreach (float sx in new[] { -2.4f, 2.4f })
        foreach (float sz in new[] { -2.4f, 2.4f })
            Art.Cylinder(Stages[8], new(sx, topY + 1.9f, z + sz), .09f, .09f, 3.4f, "8a6a4a");
        // 9 capstone: the pointed stone roof with a flag. It is on a layer of its own, which the camera leaves out while it
        // stands on the platform: seen from under it the roof would cover half of the sky.
        foreach (
            var part in new[]
            {
                Art.Cylinder(Stages[9], new(0, topY + 5.2f, z), 3.7f, .08f, 2.8f, "9d9c95"),
                Art.Ball(Stages[9], new(0, topY + 6.7f, z), new(.28f, .28f, .28f), "dcd9c8"),
                Art.Cylinder(Stages[9], new(0, topY + 7.5f, z), .04f, .04f, 1.6f, "a77a59"),
                Art.Block(Stages[9], new(.5f, topY + 7.9f, z), new(1, .55f, .04f), "e2563b"),
            }
        )
            part.Layers = RoofLayer;
    }

    /// <summary>The render layer of the roof, which the camera on the platform does not draw.</summary>
    internal const uint RoofLayer = 1 << 2;
}
