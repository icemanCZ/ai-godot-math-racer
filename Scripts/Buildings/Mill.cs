using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The windmill: a stone foundation, three courses of brick wall, a wooden gallery, a conical roof, the axle with
/// the sail frames and finally the sails of cloth. A finished mill turns its sails.
/// </summary>
public partial class Mill : SitedProject
{
    private const float CenterZ = 12;
    private Node3D? sails;
    private Vector3 hub;

    /// <summary>Turns of the sails per second, set from the wind; zero until the mill is finished.</summary>
    internal float SailSpeed { get; set; } = .35f;

    internal override Vector3 Focus => Site.Position + Site.Inward * CenterZ + Vector3.Up * 5;

    internal Mill(BuildSite site, Terrain ground)
        : base(site, ground)
    {
        Name = "Mill";
    }

    /// <summary>The angle of the sails, in turns; used by checks.</summary>
    internal float SailAngle => sails?.Rotation.Z ?? 0;

    private protected override void Build()
    {
        const float z = CenterZ;
        float y = Pad(Stages[0], 0, z, 9, 9, "8f8f8a");
        // 0 gravel: a gravel bed around the mill's footprint.
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6;
            Art.Ball(
                Stages[0],
                new(Mathf.Cos(angle) * 4.2f, y + .25f, z + Mathf.Sin(angle) * 4.2f),
                new(1.2f, .5f, 1.2f),
                i % 2 == 0 ? "b9b4a5" : "8f8a7d"
            );
        }
        // 1 gravel: the stone foundation ring.
        Art.Cylinder(Stages[1], new(0, y + .3f, z), 3.6f, 3.6f, .6f, "9d9c95");
        Art.Cylinder(Stages[1], new(0, y + .65f, z), 3.35f, 3.35f, .12f, "b4b3ab");
        float baseY = y + .6f;
        // 2-4 bricks: three courses of wall, each a little narrower, with bands, windows and the door.
        float[] bottoms = { 3.1f, 2.85f, 2.6f };
        float[] tops = { 2.85f, 2.6f, 2.4f };
        float[] heights = { 2.4f, 2.6f, 2.4f };
        float level = baseY;
        for (int course = 0; course < 3; course++)
        {
            var stage = Stages[2 + course];
            Art.Cylinder(
                stage,
                new(0, level + heights[course] / 2, z),
                bottoms[course],
                tops[course],
                heights[course],
                course % 2 == 0 ? "b5533b" : "a24a33"
            );
            level += heights[course];
            Art.Cylinder(stage, new(0, level, z), tops[course] + .08f, tops[course] + .08f, .14f, "e0c9a0");
            if (course > 0)
                foreach (float angle in new[] { -.7f, 0, .7f, Mathf.Pi })
                    Art.Window(
                        stage,
                        new(
                            Mathf.Sin(angle) * (tops[course] + .02f),
                            level - heights[course] / 2,
                            z - Mathf.Cos(angle) * (tops[course] + .02f)
                        ),
                        new(.7f, .9f, .12f),
                        "3a2a26"
                    ).Rotation = new Vector3(0, -angle, 0);
            Solid(
                2 + course,
                new(0, level - heights[course] / 2, z),
                new(tops[course] * 2, heights[course], tops[course] * 2)
            );
        }
        Art.Block(Stages[2], new(0, baseY + .9f, z - 3.05f), new(1.2f, 1.8f, .16f), "3a2a26");
        float topY = level;
        // 5 planks: the wooden gallery round the top and the door frame.
        Art.Cylinder(Stages[5], new(0, topY + .1f, z), 3.1f, 3.1f, .2f, "a77a59");
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.Tau / 12;
            Art.Cylinder(
                Stages[5],
                new(Mathf.Cos(angle) * 3, topY + .7f, z + Mathf.Sin(angle) * 3),
                .06f,
                .06f,
                1.2f,
                "c39770"
            );
        }
        Art.Block(Stages[5], new(0, baseY + 1.05f, z - 3.1f), new(1.6f, 2.2f, .1f), "a77a59");
        // 6 planks: the frame of the roof.
        float roofBase = topY + .2f;
        Art.Cylinder(Stages[6], new(0, roofBase + 1.5f, z), 3, .08f, 3, "7a4b32");
        // 7 planks: the roof boards, a weather vane on top.
        Art.Cylinder(Stages[7], new(0, roofBase + 1.55f, z), 3.15f, .1f, 3.1f, "8a5a3c");
        Art.Cylinder(Stages[7], new(0, roofBase + 3.5f, z), .03f, .03f, .9f, "4f5961");
        Art.Block(Stages[7], new(.25f, roofBase + 3.8f, z), new(.5f, .2f, .04f), "4f5961");
        // 8 planks: the axle and the four sail frames turn together on a pivot in front of the wall.
        hub = new Vector3(0, topY - 1.1f, z - 3.5f);
        sails = new Node3D { Name = "Sails", Position = hub };
        AddChild(sails);
        Stages[8].Reparent(sails, false);
        Stages[9].Reparent(sails, false);
        Art.Log(Stages[8], new(0, 0, .9f), Vector3.Back, .22f, 1.8f, "7a4b32");
        Art.Ball(Stages[8], new(0, 0, -.1f), new(.5f, .5f, .5f), "5a4a3a");
        for (int arm = 0; arm < 4; arm++)
        {
            float angle = arm * Mathf.Pi / 2;
            var along = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            Art.Beam(Stages[8], along * .3f, along * 6.2f, .22f, "8a5a3c");
            for (int slat = 0; slat < 4; slat++)
            {
                var spot = along * (1.4f + slat * 1.2f);
                var slatBlock = Art.Block(Stages[8], spot, new(.1f, 1.3f, .1f), "a77a59");
                slatBlock.Rotation = new Vector3(0, 0, angle);
            }
            // 9 fabric: the cloth stretched over the outer part of each frame.
            var cloth = Art.Block(
                Stages[9],
                along * 3.8f + new Vector3(-along.Y, along.X, 0) * .75f,
                new(4.6f, 1.3f, .04f),
                "f1e9d6"
            );
            cloth.Rotation = new Vector3(0, 0, angle);
        }
    }

    public override void _Process(double delta)
    {
        if (sails != null && Complete)
            sails.RotateZ((float)delta * SailSpeed * Mathf.Tau);
    }
}
