using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The six enclosures of the <see cref="Zoo"/> and their animals: the fences, walls, signs and roofs of the pens
/// and the penguins, monkeys, elephant, giraffe, flamingos and pandas, each built into the stage of its delivery.
/// The zoo keeps the animals and moves them; this class only puts them together.
/// </summary>
internal sealed class ZooPens
{
    private readonly Zoo zoo;

    private readonly Node3D[] stages;

    /// <summary>The rope swing of the monkeys, which the zoo swings.</summary>
    internal Node3D? Swing { get; private set; }

    /// <summary>The elephant's trunk, which rises to spray.</summary>
    internal Node3D? Trunk { get; private set; }

    /// <summary>The drops of the elephant's spray.</summary>
    internal List<MeshInstance3D> Spray { get; } = new();

    internal ZooPens(Zoo zoo, Node3D[] stages)
    {
        this.zoo = zoo;
        this.stages = stages;
    }

    /// <summary>Builds every enclosure into the stage of its first delivery and its animals into the second.</summary>
    internal void Build()
    {
        BuildPathAndPenguins(stages[1]);
        BuildMonkeys(stages[3]);
        BuildElephant(stages[5]);
        BuildGiraffe(stages[7]);
        BuildFlamingos(stages[9]);
        BuildPandas(stages[11]);
    }

    private float PenPad(Node3D stage, int pen, string color) =>
        zoo.Pad(stage, Zoo.Pens[pen].X, Zoo.Pens[pen].Z, Zoo.PenWidth, Zoo.PenDepth, color);

    /// <summary>The enclosure as a whole blocks the car; its animals cannot be driven over.</summary>
    private void PenSolid(int pen, float top) =>
        zoo.Solid(2 * pen, new(Zoo.Pens[pen].X, top + .6f, Zoo.Pens[pen].Z), new(Zoo.PenWidth, 1.2f, Zoo.PenDepth));

    private static void Eyes(Node3D parent, float x, float y, float z, float radius = .045f)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Ball(parent, new(side * x, y, z), new(radius, radius * 1.2f, radius * .8f), "1b1d26");
            Art.Ball(
                parent,
                new(side * x - radius * .3f, y + radius * .4f, z + radius * .5f),
                Vector3.One * radius * .35f,
                "ffffff"
            );
        }
    }

    private static Node3D Pivot(Node3D parent, Vector3 position)
    {
        var pivot = new Node3D { Position = position };
        parent.AddChild(pivot);
        return pivot;
    }

    /// <summary>A low fence of posts and two rails all round an enclosure.</summary>
    private void Fence(Node3D stage, int pen, float top, string post, string rail)
    {
        var (cx, cz) = Zoo.Pens[pen];
        float x0 = cx - Zoo.PenWidth / 2 + .15f,
            x1 = cx + Zoo.PenWidth / 2 - .15f,
            z0 = cz - Zoo.PenDepth / 2 + .15f,
            z1 = cz + Zoo.PenDepth / 2 - .15f;
        Vector3[] corners =
        {
            new(x0, top, z0),
            new(x1, top, z0),
            new(x1, top, z1),
            new(x0, top, z1),
            new(x0, top, z0),
        };
        for (int side = 0; side < 4; side++)
        {
            var from = corners[side];
            var to = corners[side + 1];
            int count = Mathf.CeilToInt(from.DistanceTo(to) / 1.6f);
            for (int i = 0; i < count; i++)
            {
                var a = from.Lerp(to, i / (float)count);
                var b = from.Lerp(to, (i + 1) / (float)count);
                Art.Cylinder(stage, a + Vector3.Up * .5f, .06f, .07f, 1.0f, post);
                Art.Beam(stage, a + Vector3.Up * .38f, b + Vector3.Up * .38f, .06f, rail);
                Art.Beam(stage, a + Vector3.Up * .82f, b + Vector3.Up * .82f, .06f, rail);
            }
        }
    }

    /// <summary>A low wall of bricks or stones all round an enclosure.</summary>
    private void Wall(Node3D stage, int pen, float top, string color, string cap, float height = .75f)
    {
        var (cx, cz) = Zoo.Pens[pen];
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Block(
                stage,
                new(cx + side * (Zoo.PenWidth / 2 - .15f), top + height / 2, cz),
                new(.3f, height, Zoo.PenDepth - .1f),
                color
            );
            Art.Block(
                stage,
                new(cx + side * (Zoo.PenWidth / 2 - .15f), top + height + .04f, cz),
                new(.42f, .08f, Zoo.PenDepth),
                cap
            );
            Art.Block(
                stage,
                new(cx, top + height / 2, cz + side * (Zoo.PenDepth / 2 - .15f)),
                new(Zoo.PenWidth - .1f, height, .3f),
                color
            );
            Art.Block(
                stage,
                new(cx, top + height + .04f, cz + side * (Zoo.PenDepth / 2 - .15f)),
                new(Zoo.PenWidth, .08f, .42f),
                cap
            );
        }
        // Courses of bricks: thin darker strips on the sides.
        foreach (float y in new[] { .25f, .5f })
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Block(
                stage,
                new(cx + side * (Zoo.PenWidth / 2 - .15f), top + y, cz),
                new(.32f, .03f, Zoo.PenDepth - .08f),
                "a9563f"
            );
            Art.Block(
                stage,
                new(cx, top + y, cz + side * (Zoo.PenDepth / 2 - .15f)),
                new(Zoo.PenWidth - .08f, .03f, .32f),
                "a9563f"
            );
        }
    }

    /// <summary>A signboard on a post at the path side of an enclosure.</summary>
    private void PenSign(Node3D stage, int pen, float top)
    {
        var (cx, cz) = Zoo.Pens[pen];
        float side = cx < 0 ? 1 : -1;
        float x = cx + side * (Zoo.PenWidth / 2 + .3f);
        float z = cz - Zoo.PenDepth / 2 + .6f;
        Art.Cylinder(stage, new(x, top + .65f, z), .06f, .07f, 1.3f, "8a5f3f");
        Art.Block(stage, new(x, top + 1.5f, z), new(.08f, .55f, 1.5f), "f2d9a4");
        Art.Block(stage, new(x, top + 1.5f, z), new(.1f, .45f, 1.38f), "6dbb5f");
        var label = Art.Sign(stage, new(x + side * .3f, top + 1.5f, z), ZooPlans.Signs[pen], 22);
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
        label.Rotation = new Vector3(0, side > 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2, 0);
        label.DoubleSided = false;
    }

    /// <summary>The yaw with which an animal in a pen looks towards the path and the gate.</summary>
    private static float Facing(int pen) => pen % 2 == 0 ? 2.3f : -2.3f;

    private static void Roof(
        Node3D parent,
        float cx,
        float cz,
        float halfWidth,
        float halfDepth,
        float low,
        float ridge,
        string color
    )
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] v =
        {
            new(cx - halfWidth, low, cz - halfDepth),
            new(cx + halfWidth, low, cz - halfDepth),
            new(cx, ridge, cz - halfDepth),
            new(cx - halfWidth, low, cz + halfDepth),
            new(cx + halfWidth, low, cz + halfDepth),
            new(cx, ridge, cz + halfDepth),
        };
        int[] indices = { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 2, 5, 4, 2, 4, 1 };
        foreach (int i in indices)
            st.AddVertex(v[i]);
        st.GenerateNormals();
        var roof = Art.Mesh(parent, st.Commit(), Vector3.Zero, Vector3.One, color);
        var material = (StandardMaterial3D)Art.Mat(color).Duplicate();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        roof.MaterialOverride = material;
    }

    private static MeshInstance3D Water(Node3D parent, float x, float y, float z, float radius)
    {
        Art.Cylinder(parent, new(x, y + .02f, z), radius + .25f, radius + .25f, .1f, "e6f1f5");
        return Art.Cylinder(parent, new(x, y + .06f, z), radius, radius, .1f, "78c8e6");
    }

    // ----- stage 1: the path, the ticket booth and the penguins ---------------------------------------------------

    private void BuildPathAndPenguins(Node3D stage)
    {
        var frame = stages[0];
        int index = 0;
        for (float z = 1.0f; z < Zoo.PathEnd + 1; z += 2.3f)
        {
            float top = zoo.Highest(0, z + 1.15f, 3.2f, 2.4f);
            Art.Block(
                frame,
                new(0, top - .6f, z + 1.15f),
                new(3.2f, 1.2f, 2.3f),
                index++ % 2 == 0 ? "bdb7a6" : "b3ad9c"
            );
            Art.Ball(frame, new(index % 2 == 0 ? -1.3f : 1.3f, top + .02f, z + 1.2f), new(.28f, .07f, .22f), "8f8a7d");
        }
        // The ticket booth beside the gate: a cabin, a striped awning and a window.
        float booth = zoo.Pad(frame, 4.2f, 1.9f, 2.4f, 2.2f, "c9c2ad");
        Art.Block(frame, new(4.2f, booth + 1.0f, 2.1f), new(1.8f, 2.0f, 1.5f), "f2d9a4");
        Art.Block(frame, new(4.2f, booth + 2.1f, 1.9f), new(2.3f, .14f, 2.0f), "d9432f");
        for (int i = 0; i < 4; i++)
            Art.Block(
                frame,
                new(3.5f + i * .47f, booth + 1.78f, 1.0f),
                new(.46f, .08f, .5f),
                i % 2 == 0 ? "d9432f" : "fff5df"
            );
        Art.Window(frame, new(4.2f, booth + 1.3f, 1.34f), new(1.0f, .7f, .06f), "4f8fb8");
        Art.Block(frame, new(4.2f, booth + .98f, 1.28f), new(1.2f, .08f, .22f), "c39771");
        zoo.Solid(0, new(4.2f, booth + 1.0f, 2.0f), new(2.0f, 2.0f, 1.8f));

        // The penguins' pool: a rim of stones, a pool, snowy rocks, a bucket of fish and four penguins.
        const int pen = 0;
        var (cx, cz) = Zoo.Pens[pen];
        float top0 = PenPad(stages[2 * pen], pen, "d4e2e8");
        Wall(stages[2 * pen], pen, top0, "aeb8bd", "d6dde0", .6f);
        Water(stages[2 * pen], cx + .5f, top0, cz + .3f, 2.45f);
        foreach (
            var (x, z, h, c) in new[]
            {
                (-3.0f, 2.4f, .8f, "eaf4f8"),
                (-2.1f, 2.9f, .6f, "cfe3ee"),
                (-3.4f, 1.2f, .5f, "cfe3ee"),
                (2.9f, 2.7f, .7f, "eaf4f8"),
                (3.6f, 1.9f, .5f, "cfe3ee"),
            }
        )
            Art.Ball(stage, new(cx + x, top0 + h * .4f, cz + z), new(.8f, h, .7f), c);
        // A ramp of ice at the back.
        var ramp = Art.Block(stage, new(cx + .3f, top0 + .5f, cz + 3.1f), new(2.2f, .2f, 2.4f), "dff0f7");
        ramp.Rotation = new Vector3(-.4f, 0, 0);
        var bucket = new Vector3(cx + 3.7f, top0, cz - 2.7f);
        Art.Cylinder(stage, bucket + Vector3.Up * .22f, .3f, .24f, .44f, "d9432f");
        foreach (float dx in new[] { -.1f, .12f })
            Art.Ball(stage, bucket + new Vector3(dx, .46f, 0), new(.2f, .05f, .08f), "b8c7d1");
        PenSign(stages[2 * pen], pen, top0);
        Fence(stages[2 * pen], pen, top0 + .6f, "8a5f3f", "c39771");
        PenSolid(pen, top0);
        Penguin(stage, new(cx - 3.1f, top0, cz - .9f), 0, 0, 1.0f, new(0, 0, 1), 1.3f, 1.1f);
        Penguin(stage, new(cx + 3.7f, top0, cz + .3f), 0, 2.1f, 1.0f, new(0, 0, 1), 1.2f, .9f);
        Penguin(stage, new(cx + .2f, top0, cz - 3.0f), 0, 4.0f, .85f, Vector3.Right, 1.4f, 1.2f);
        Penguin(stage, new(cx - 3.0f, top0 + .5f, cz + 2.3f), Mathf.Pi, 1.0f, .9f, Vector3.Right, 0, 1);
    }

    private void Penguin(
        Node3D parent,
        Vector3 home,
        float yaw,
        float phase,
        float scale,
        Vector3 dir,
        float amp,
        float speed
    )
    {
        var beast = zoo.NewBeast(1, parent, home, yaw, scale, phase, dir, amp, speed);
        var root = beast.Root;
        Art.Ball(root, new(0, .55f, 0), new(.34f, .52f, .3f), "2c3140");
        Art.Ball(root, new(0, .5f, .1f), new(.25f, .4f, .22f), "fbfbf4");
        Art.Ball(root, new(0, 1.14f, .02f), new(.25f, .24f, .24f), "2c3140");
        Art.Ball(root, new(0, 1.1f, .15f), new(.17f, .14f, .1f), "fbfbf4");
        Eyes(root, .09f, 1.18f, .23f, .04f);
        Art.Block(root, new(0, 1.08f, .33f), new(.12f, .05f, .16f), "f2a33a");
        var flippers = new List<(Node3D Pivot, float Side)>();
        foreach (float side in new[] { -1f, 1f })
        {
            var flipper = Pivot(root, new(side * .34f, .85f, 0));
            Art.Ball(flipper, new(side * .04f, -.2f, 0), new(.06f, .27f, .14f), "2c3140");
            flippers.Add((flipper, side));
            Art.Block(root, new(side * .13f, .03f, .1f), new(.17f, .06f, .28f), "f2a33a");
        }
        beast.Animate = t =>
        {
            float walk = beast.Amp > 0 ? 1 : 0;
            beast.Roll = Mathf.Sin(t * beast.Speed * 7 + beast.Phase) * .13f * walk;
            beast.Lift = Mathf.Abs(Mathf.Sin(t * beast.Speed * 3.5f + beast.Phase)) * .04f * walk;
            foreach (var (flipper, side) in flippers)
                flipper.Rotation = new Vector3(
                    0,
                    0,
                    side * (.2f + Mathf.Sin(t * 5 + beast.Phase) * (walk > 0 ? .12f : .5f))
                );
        };
    }

    // ----- stage 2: the monkeys ----------------------------------------------------------------------------------

    private void BuildMonkeys(Node3D stage)
    {
        const int pen = 1;
        var (cx, cz) = Zoo.Pens[pen];
        float top = PenPad(stages[2 * pen], pen, "a9d08a");
        Fence(stages[2 * pen], pen, top, "8a5f3f", "c39771");
        PenSign(stages[2 * pen], pen, top);
        // The climbing frame: four posts, beams across, a platform with a ladder and a rope swing.
        float fx = cx + .4f,
            fz = cz + .6f;
        foreach (float x in new[] { -1.8f, 1.8f })
        foreach (float z in new[] { -1.0f, 1.0f })
            Art.Cylinder(stage, new(fx + x, top + 1.7f, fz + z), .1f, .12f, 3.4f, "a77a59");
        foreach (float z in new[] { -1.0f, 1.0f })
            Art.Beam(stage, new(fx - 1.9f, top + 3.4f, fz + z), new(fx + 1.9f, top + 3.4f, fz + z), .14f, "b0855c");
        foreach (float x in new[] { -1.8f, 1.8f })
            Art.Beam(stage, new(fx + x, top + 3.4f, fz - 1.0f), new(fx + x, top + 3.4f, fz + 1.0f), .12f, "b0855c");
        Art.Block(stage, new(fx + 1.0f, top + 1.7f, fz), new(1.6f, .14f, 2.1f), "c39771");
        foreach (float z in new[] { -.7f, .7f })
            Art.Beam(stage, new(fx + 2.2f, top, fz + z), new(fx + 1.8f, top + 1.7f, fz + z), .07f, "a77a59");
        for (int i = 1; i <= 4; i++)
            Art.Block(stage, new(fx + 2.2f - i * .1f, top + i * .34f, fz), new(.08f, .06f, 1.4f), "e2bb8b");
        // The rope swing, with the first monkey holding on.
        Swing = Pivot(stage, new(fx - .6f, top + 3.4f, fz));
        Art.Beam(Swing, Vector3.Zero, new(0, -1.1f, 0), .05f, "e8d9b0");
        Art.Block(Swing, new(0, -1.15f, 0), new(.4f, .08f, .3f), "c39771");
        var swinger = Monkey(stage, Swing, new(0, -2.5f, 0), 0, .95f, 0, raised: true);
        swinger.Amp = 0;
        // The second monkey hops on the platform, the third eats beside a crate of bananas.
        var hopper = Monkey(stage, stage, new(fx + 1.0f, top + 1.77f, fz), Facing(pen), .85f, 1.4f, raised: false);
        var hopAnimate = hopper.Animate;
        hopper.Animate = t =>
        {
            hopAnimate?.Invoke(t);
            hopper.Lift = Mathf.Max(0, Mathf.Sin(t * 3.3f)) * .35f;
        };
        Monkey(stage, stage, new(cx - 3.0f, top, cz - 2.3f), Facing(pen) + .4f, .8f, 3.1f, raised: false);
        Art.Block(stage, new(cx - 3.0f, top + .25f, cz - 1.2f), new(.95f, .5f, .7f), "c39771");
        for (int i = 0; i < 3; i++)
            Art.Beam(
                stage,
                new(cx - 3.3f + i * .3f, top + .55f, cz - 1.3f),
                new(cx - 3.2f + i * .3f, top + .85f, cz - 1.0f),
                .12f,
                "f5d33b"
            );
        // A tree with bananas on the side.
        Art.Cylinder(stage, new(cx + 3.4f, top + 1.4f, cz - 2.6f), .22f, .3f, 2.8f, "8a5f3f");
        foreach (var (x, y, z, r) in new[] { (0f, 3.2f, 0f, 1.2f), (.8f, 3.0f, .5f, .9f), (-.8f, 3.0f, -.4f, .9f) })
            Art.Ball(stage, new(cx + 3.4f + x, top + y, cz - 2.6f + z), new(r, r * .7f, r), "5aa86f");
        PenSolid(pen, top);
    }

    private Zoo.Beast Monkey(
        Node3D stage,
        Node3D parent,
        Vector3 home,
        float yaw,
        float scale,
        float phase,
        bool raised
    )
    {
        var beast = zoo.NewBeast(3, parent, home, yaw, scale, phase);
        var root = beast.Root;
        const string fur = "8a5a3b",
            skin = "e9cfa4";
        Art.Ball(root, new(0, .72f, 0), new(.28f, .36f, .24f), fur);
        Art.Ball(root, new(0, .68f, .1f), new(.19f, .26f, .14f), "d8b48a");
        Art.Ball(root, new(0, 1.25f, .04f), Vector3.One * .24f, fur);
        Art.Ball(root, new(0, 1.2f, .17f), new(.16f, .14f, .08f), skin);
        Art.Ball(root, new(0, 1.15f, .24f), new(.05f, .03f, .03f), "3a2a22");
        Art.Ball(root, new(0, 1.09f, .23f), new(.07f, .02f, .02f), "7a4a4a");
        Eyes(root, .08f, 1.27f, .24f, .035f);
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Ball(root, new(side * .27f, 1.3f, 0), new(.08f, .09f, .04f), skin);
            Art.Ball(root, new(side * .14f, .2f, .05f), new(.09f, .2f, .1f), fur);
            Art.Ball(root, new(side * .14f, .04f, .13f), new(.09f, .04f, .12f), skin);
        }
        // A long tail that curls up behind.
        for (int i = 0; i < 7; i++)
            Art.Ball(
                root,
                new(0, .4f + i * .1f, -.27f - Mathf.Sin(i * .45f) * .16f),
                Vector3.One * (.055f - i * .002f),
                fur
            );
        var arms = new List<(Node3D Pivot, float Side)>();
        foreach (float side in new[] { -1f, 1f })
        {
            var arm = Pivot(root, new(side * .3f, .98f, 0));
            Art.Ball(arm, new(0, -.25f, 0), new(.07f, .28f, .07f), fur);
            Art.Ball(arm, new(0, -.54f, 0), Vector3.One * .085f, skin);
            arm.Rotation = new Vector3(0, 0, raised ? -side * (Mathf.Pi - .45f) : side * .2f);
            arms.Add((arm, side));
        }
        if (!raised)
            beast.Animate = t =>
            {
                foreach (var (arm, side) in arms)
                    arm.Rotation = new Vector3(
                        Mathf.Sin(t * 4 + beast.Phase + side) * .5f,
                        0,
                        side * (.2f + Mathf.Sin(t * 2 + beast.Phase) * .15f)
                    );
                beast.Pitch = Mathf.Sin(t * 2 + beast.Phase) * .05f;
            };
        return beast;
    }

    // ----- stage 3: the elephant ---------------------------------------------------------------------------------

    private void BuildElephant(Node3D stage)
    {
        const int pen = 2;
        var (cx, cz) = Zoo.Pens[pen];
        float top = PenPad(stages[2 * pen], pen, "e6d6a6");
        Wall(stages[2 * pen], pen, top, "c9795c", "e2b79b");
        PenSign(stages[2 * pen], pen, top);
        // A brick elephant house at the back with a dark doorway and a red roof.
        float hz = cz + 2.5f;
        Art.Block(stage, new(cx - .6f, top + 1.2f, hz + 1.0f), new(4.0f, 2.4f, .3f), "c9795c");
        foreach (float side in new[] { -1f, 1f })
            Art.Block(stage, new(cx - .6f + side * 1.85f, top + 1.2f, hz), new(.3f, 2.4f, 2.3f), "c9795c");
        Art.Block(stage, new(cx - .6f - 1.2f, top + 1.2f, hz - 1.0f), new(1.7f, 2.4f, .3f), "c9795c");
        Art.Block(stage, new(cx - .6f + 1.2f, top + 1.2f, hz - 1.0f), new(1.7f, 2.4f, .3f), "c9795c");
        Art.Block(stage, new(cx - .6f, top + 2.1f, hz - 1.0f), new(1.0f, .6f, .3f), "c9795c");
        Art.Block(stage, new(cx - .6f, top + .9f, hz + .9f), new(1.0f, 1.8f, .06f), "3a2a22");
        foreach (float y in new[] { .55f, 1.1f, 1.65f })
            Art.Block(stage, new(cx - .6f, top + y, hz - 1.0f), new(4.0f, .03f, .32f), "a9563f");
        Roof(stage, cx - .6f, hz, 2.3f, 1.35f, top + 2.35f, top + 3.3f, "b5533b");
        // The pool, hay and a red ball to play with.
        Water(stages[2 * pen], cx + 2.4f, top, cz - 1.5f, 1.35f);
        Art.Log(stage, new(cx - 3.4f, top + .35f, cz + 2.9f), Vector3.Right, .36f, 1.0f, "e2c36a");
        Art.Log(stage, new(cx - 3.2f, top + .35f, cz + 1.9f), Vector3.Right, .36f, 1.0f, "e8cc78");
        Art.Ball(stage, new(cx - 2.6f, top + .45f, cz - 2.3f), Vector3.One * .45f, "d9432f");
        Art.Ball(stage, new(cx - 2.6f, top + .45f, cz - 2.3f), new(.46f, .12f, .46f), "fff5df");
        PenSolid(pen, top);

        var beast = zoo.NewBeast(2 * pen + 1, stage, new(cx + .2f, top, cz - .4f), Facing(pen), 1.05f, 0.2f);
        var root = beast.Root;
        const string grey = "a3a8b2",
            dark = "9aa0aa";
        Art.Ball(root, new(0, 1.5f, 0), new(.95f, .9f, 1.25f), grey);
        Art.Ball(root, new(0, 1.95f, 1.15f), new(.62f, .62f, .6f), grey);
        Art.Ball(root, new(0, 2.35f, 1.05f), new(.45f, .3f, .4f), grey);
        Eyes(root, .36f, 2.08f, 1.68f, .06f);
        var ears = new List<(Node3D Pivot, float Side)>();
        foreach (float side in new[] { -1f, 1f })
        {
            var ear = Pivot(root, new(side * .56f, 2.1f, .85f));
            Art.Ball(ear, new(side * .1f, -.1f, -.05f), new(.07f, .52f, .46f), dark);
            Art.Ball(ear, new(side * .15f, -.1f, -.03f), new(.04f, .38f, .34f), "e9b6b0");
            ears.Add((ear, side));
            Art.Beam(root, new(side * .3f, 1.55f, 1.6f), new(side * .4f, 1.3f, 2.15f), .1f, "f6efdc");
            foreach (float back in new[] { -.8f, .8f })
            {
                Art.Cylinder(root, new(side * .55f, .5f, back), .28f, .24f, 1.0f, dark);
                Art.Ball(root, new(side * .55f, .06f, back + .06f), new(.26f, .08f, .3f), "d8d0c4");
            }
        }
        Art.Beam(root, new(0, 1.7f, -1.2f), new(0, .95f, -1.45f), .08f, grey);
        Art.Ball(root, new(0, .9f, -1.47f), new(.07f, .12f, .07f), "6b6f78");
        // The trunk hangs from a pivot in front of the face and rises to spray.
        Trunk = Pivot(root, new(0, 1.75f, 1.65f));
        for (int i = 0; i < 5; i++)
            Art.Ball(Trunk, new(0, -.3f * i - .1f, .04f * i), Vector3.One * (.24f - i * .022f), grey);
        for (int i = 0; i < 12; i++)
        {
            var drop = Art.Ball(root, Vector3.Zero, Vector3.One * .09f, "8fd6f2");
            drop.Visible = false;
            Spray.Add(drop);
        }
        var trunkPivot = Trunk;
        beast.Animate = t =>
        {
            float u = (t / 7f + beast.Phase) % 1f;
            float raised = Mathf.SmoothStep(0.1f, 0.22f, u) - Mathf.SmoothStep(0.72f, 0.84f, u);
            float angle =
                -raised * 1.15f + (1 - raised) * Mathf.Sin(t * 1.3f) * .08f + Mathf.Sin(t * 5) * .04f * raised;
            trunkPivot.Rotation = new Vector3(angle, 0, 0);
            zoo.Spraying = raised > .9f;
            // The spray runs out of the tip of the trunk along its direction and falls in an arc.
            var tip = new Vector3(0, 1.75f - 1.5f * Mathf.Cos(angle), 1.65f - 1.5f * Mathf.Sin(angle));
            var direction = new Vector3(0, -Mathf.Cos(angle), -Mathf.Sin(angle));
            for (int i = 0; i < Spray.Count; i++)
            {
                float s = (t * 1.4f + i / (float)Spray.Count) % 1f;
                float tau = s * .95f;
                var p = tip + direction * (3.6f * tau) + new Vector3(0, 1.4f * tau - 4.9f * tau * tau, 0);
                Spray[i].Visible = zoo.Spraying && p.Y > .05f;
                Spray[i].Position = p;
            }
            foreach (var (ear, side) in ears)
                ear.Rotation = new Vector3(0, side * (.15f + Mathf.Sin(t * 2.2f + side) * .2f), 0);
            beast.Roll = Mathf.Sin(t * .9f) * .02f;
        };
    }

    // ----- stage 4: the giraffe ----------------------------------------------------------------------------------

    private void BuildGiraffe(Node3D stage)
    {
        const int pen = 3;
        var (cx, cz) = Zoo.Pens[pen];
        float top = PenPad(stages[2 * pen], pen, "b5d98a");
        Fence(stages[2 * pen], pen, top, "8a5f3f", "c39771");
        PenSign(stages[2 * pen], pen, top);
        // A feeding tower of planks with a stair, a deck and a basket of leaves at the height of the giraffe's head.
        float tx = cx + 2.6f,
            tz = cz + 2.3f;
        foreach (float x in new[] { -.9f, .9f })
        foreach (float z in new[] { -.9f, .9f })
            Art.Cylinder(stage, new(tx + x, top + 1.9f, tz + z), .1f, .12f, 3.8f, "a77a59");
        Art.Block(stage, new(tx, top + 2.3f, tz), new(2.2f, .14f, 2.2f), "c39771");
        foreach (float z in new[] { -1.0f, 1.0f })
            Art.Block(stage, new(tx, top + 2.85f, tz + z), new(2.2f, .06f, .06f), "e2bb8b");
        Roof(stage, tx, tz, 1.5f, 1.4f, top + 3.75f, top + 4.5f, "6c8fd4");
        for (int i = 0; i < 6; i++)
            Art.Block(
                stage,
                new(tx - 1.7f + i * .0f - .1f * i, top + .2f + i * .38f, tz - 1.15f + i * .0f),
                new(.9f, .06f, .4f),
                "e2bb8b"
            );
        Art.Cylinder(stage, new(tx - 1.15f, top + 3.35f, tz - .3f), .38f, .3f, .3f, "c39771");
        foreach (var (x, z) in new[] { (-.15f, 0f), (.15f, .1f), (0f, -.15f) })
            Art.Ball(stage, new(tx - 1.15f + x, top + 3.58f, tz - .3f + z), new(.2f, .12f, .2f), "6dbb5f");
        // The tree beside it and a water trough.
        Art.Cylinder(stage, new(cx - 3.2f, top + 2.1f, cz + 2.7f), .24f, .34f, 4.2f, "8a5f3f");
        foreach (var (x, y, z, r) in new[] { (0f, 4.6f, 0f, 1.4f), (.9f, 4.2f, .5f, 1.0f), (-.9f, 4.3f, -.4f, 1.0f) })
            Art.Ball(stage, new(cx - 3.2f + x, top + y, cz + 2.7f + z), new(r, r * .7f, r), "5aa86f");
        Art.Block(stage, new(cx - 2.4f, top + .3f, cz - 2.6f), new(2.0f, .6f, .8f), "c39771");
        Art.Block(stage, new(cx - 2.4f, top + .55f, cz - 2.6f), new(1.7f, .1f, .55f), "78c8e6");
        PenSolid(pen, top);

        var beast = zoo.NewBeast(2 * pen + 1, stage, new(cx - .4f, top, cz - .3f), Facing(pen) + .3f, .95f, .6f);
        var root = beast.Root;
        const string coat = "f2c14e",
            spot = "b57b2e";
        foreach (float side in new[] { -1f, 1f })
        foreach (float back in new[] { -.6f, .6f })
        {
            Art.Cylinder(root, new(side * .3f, .85f, back), .1f, .13f, 1.7f, coat);
            Art.Ball(root, new(side * .3f, .06f, back + .03f), new(.12f, .07f, .15f), "6b4a2e");
        }
        Art.Ball(root, new(0, 1.95f, 0), new(.42f, .5f, .85f), coat);
        foreach (
            var (x, y, z) in new[] { (.37f, 2.05f, .3f), (-.37f, 2.0f, -.2f), (.35f, 1.8f, -.3f), (-.36f, 1.85f, .35f) }
        )
            Art.Ball(root, new(x, y, z), new(.06f, .17f, .2f), spot);
        Art.Beam(root, new(0, 2.15f, -.8f), new(0, 1.2f, -.95f), .06f, coat);
        Art.Ball(root, new(0, 1.15f, -.97f), new(.06f, .13f, .06f), "6b4a2e");
        // The neck rises from the shoulder and nods; the head sits on top of it.
        var neck = Pivot(root, new(0, 2.25f, .55f));
        Art.Beam(neck, Vector3.Zero, new(0, .95f, .3f), .36f, coat);
        Art.Beam(neck, new(0, .95f, .3f), new(0, 1.85f, .6f), .27f, coat);
        foreach (var (y, z) in new[] { (.3f, .08f), (.8f, .25f), (1.35f, .42f) })
        foreach (float side in new[] { -1f, 1f })
            Art.Ball(neck, new(side * .17f, y, z), new(.05f, .11f, .09f), spot);
        for (int i = 0; i < 6; i++)
            Art.Ball(neck, new(0, .15f + i * .3f, -.1f + i * .1f - .05f), new(.05f, .1f, .06f), "8a5a3b");
        var head = Pivot(neck, new(0, 1.95f, .65f));
        Art.Ball(head, new(0, 0, .08f), new(.2f, .2f, .3f), coat);
        Art.Ball(head, new(0, -.06f, .4f), new(.13f, .11f, .14f), "f6d98f");
        Art.Ball(head, new(0, -.04f, .53f), new(.05f, .04f, .03f), "3a2a22");
        Eyes(head, .17f, .07f, .2f, .04f);
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Cylinder(head, new(side * .09f, .3f, -.02f), .03f, .03f, .24f, coat);
            Art.Ball(head, new(side * .09f, .45f, -.02f), Vector3.One * .06f, "8a5a3b");
            Art.Ball(head, new(side * .21f, .13f, -.06f), new(.12f, .05f, .06f), coat);
        }
        beast.Animate = t =>
        {
            // Mostly a slow sway; every ten seconds she reaches up to the basket and chews.
            float u = (t / 10f + beast.Phase) % 1f;
            float reach = Mathf.SmoothStep(.55f, .65f, u) - Mathf.SmoothStep(.85f, .95f, u);
            neck.Rotation = new Vector3(
                Mathf.Sin(t * .8f) * .06f - reach * .35f,
                -reach * .5f + Mathf.Sin(t * .5f) * .08f,
                0
            );
            head.Rotation = new Vector3(reach * .5f + Mathf.Sin(t * 8) * .06f * reach, 0, Mathf.Sin(t * 1.1f) * .05f);
            beast.Roll = Mathf.Sin(t * .7f) * .015f;
        };
    }

    // ----- stage 5: the flamingos --------------------------------------------------------------------------------

    private void BuildFlamingos(Node3D stage)
    {
        const int pen = 4;
        var (cx, cz) = Zoo.Pens[pen];
        float top = PenPad(stages[2 * pen], pen, "bfe0a0");
        Wall(stages[2 * pen], pen, top, "c9795c", "e2b79b", .6f);
        PenSign(stages[2 * pen], pen, top);
        Water(stages[2 * pen], cx, top, cz, 3.1f);
        // A brick bridge over the pond: eight steps up and down, with parapets of bricks.
        const int steps = 9;
        for (int i = 0; i < steps; i++)
        {
            float u = i / (float)(steps - 1);
            float x = cx - 3.5f + u * 7.0f;
            float h = Mathf.Sin(u * Mathf.Pi) * .8f;
            Art.Block(stage, new(x, top + h + .1f, cz + 1.6f), new(.95f, .22f, 1.6f), "c9795c");
            Art.Block(stage, new(x, top + h * .5f - .15f, cz + 1.6f), new(.95f, h + .1f, 1.2f), "b5654a");
            foreach (float side in new[] { -.78f, .78f })
                Art.Block(stage, new(x, top + h + .38f, cz + 1.6f + side), new(.95f, .3f, .14f), "e2b79b");
        }
        // Reeds and a little island of stones in the pond.
        for (int i = 0; i < 9; i++)
        {
            float a = i * .75f + .3f;
            float rx = cx + Mathf.Cos(a) * 3.3f,
                rz = cz + Mathf.Sin(a) * 3.0f;
            if (Mathf.Abs(rx - cx) > Zoo.PenWidth / 2 - .5f || Mathf.Abs(rz - cz) > Zoo.PenDepth / 2 - .4f)
                continue;
            float h = 1.0f + (i % 3) * .3f;
            Art.Cylinder(stage, new(rx, top + h / 2, rz), .03f, .035f, h, "6bb866");
            Art.Ball(stage, new(rx, top + h, rz), new(.05f, .16f, .05f), "8a5f3f");
        }
        Art.Ball(stage, new(cx - .2f, top + .15f, cz - .3f), new(1.0f, .22f, .9f), "b9b4a5");
        PenSolid(pen, top);
        Flamingo(stage, new(cx - 1.0f, top, cz - .5f), 1.2f, 0, true);
        Flamingo(stage, new(cx + 1.4f, top, cz - 1.0f), -1.8f, 1.9f, true);
        Flamingo(stage, new(cx + .1f, top, cz + .3f), 2.6f, 4.1f, false);
    }

    private void Flamingo(Node3D parent, Vector3 home, float yaw, float phase, bool oneLeg)
    {
        var beast = zoo.NewBeast(9, parent, home, yaw, 1.05f, phase);
        var root = beast.Root;
        const string pink = "f48fb1";
        Art.Ball(root, new(0, 1.1f, 0), new(.24f, .24f, .4f), pink);
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Ball(root, new(side * .2f, 1.12f, -.04f), new(.05f, .17f, .28f), "f7a8c4");
            Art.Ball(root, new(side * .21f, 1.0f, -.28f), new(.04f, .08f, .1f), "2c3140");
        }
        Art.Ball(root, new(0, 1.02f, -.42f), new(.1f, .1f, .15f), pink);
        Art.Beam(root, new(.07f, 1.0f, 0), new(.07f, .03f, .02f), .045f, "e8658c");
        Art.Block(root, new(.07f, .02f, .08f), new(.16f, .04f, .2f), "e8658c");
        if (oneLeg)
        {
            Art.Beam(root, new(-.07f, .98f, 0), new(-.07f, .6f, .1f), .045f, "e8658c");
            Art.Beam(root, new(-.07f, .6f, .1f), new(-.07f, .5f, -.08f), .04f, "e8658c");
        }
        else
        {
            Art.Beam(root, new(-.07f, 1.0f, 0), new(-.07f, .03f, .02f), .045f, "e8658c");
            Art.Block(root, new(-.07f, .02f, .08f), new(.16f, .04f, .2f), "e8658c");
        }
        // The neck is an S of balls on a pivot at the front of the body; it bends down to the water.
        var neck = Pivot(root, new(0, 1.22f, .3f));
        foreach (
            var (y, z, r) in new[]
            {
                (.08f, .05f, .08f),
                (.2f, .12f, .07f),
                (.34f, .1f, .065f),
                (.47f, .03f, .06f),
                (.56f, -.03f, .06f),
            }
        )
            Art.Ball(neck, new(0, y, z), Vector3.One * r, pink);
        Art.Ball(neck, new(0, .64f, .04f), new(.08f, .08f, .09f), pink);
        Art.Block(neck, new(0, .61f, .17f), new(.045f, .05f, .14f), "f6e5d6");
        Art.Block(neck, new(0, .58f, .26f), new(.04f, .05f, .08f), "2c3140");
        Eyes(neck, .045f, .67f, .08f, .018f);
        beast.Animate = t =>
        {
            float u = (t * .12f + phase / 6f) % 1f;
            float dip = Mathf.SmoothStep(.0f, .1f, u) - Mathf.SmoothStep(.18f, .3f, u);
            neck.Rotation = new Vector3(dip * 2.0f + Mathf.Sin(t * 1.1f + phase) * .07f, 0, 0);
            beast.Roll = Mathf.Sin(t * .8f + phase) * .03f;
            beast.Pitch = dip * .08f;
        };
    }

    // ----- stage 6: the pandas -----------------------------------------------------------------------------------

    private void BuildPandas(Node3D stage)
    {
        const int pen = 5;
        var (cx, cz) = Zoo.Pens[pen];
        float top = PenPad(stages[2 * pen], pen, "a9d08a");
        Fence(stages[2 * pen], pen, top, "8a5f3f", "c39771");
        PenSign(stages[2 * pen], pen, top);
        // The bamboo grove at the back: stalks of different heights with rings and tufts of leaves.
        for (int i = 0; i < 18; i++)
        {
            float x = cx - 4.0f + i * .47f + (i % 3) * .1f,
                z = cz + 2.2f + (i % 4) * .42f;
            float h = 2.6f + ((i * 7) % 5) * .5f;
            Art.Cylinder(stage, new(x, top + h / 2, z), .06f, .07f, h, "6bb866");
            for (int ring = 1; ring < 5; ring++)
                Art.Cylinder(stage, new(x, top + ring * h / 5, z), .08f, .08f, .03f, "4f9a4f");
            Art.Ball(stage, new(x, top + h, z), new(.4f, .1f, .25f), "7ccf7a").Rotation = new Vector3(0, i * .7f, .2f);
            Art.Ball(stage, new(x, top + h - .5f, z), new(.3f, .08f, .2f), "6dbb6f").Rotation = new Vector3(
                0,
                i * 1.1f,
                -.2f
            );
        }
        // A wooden hut open to the front with a roof of planks, a log to climb and a hanging ball.
        float hx = cx + 2.7f,
            hz = cz - .6f;
        foreach (float x in new[] { -1.0f, 1.0f })
        foreach (float z in new[] { -.9f, .9f })
            Art.Cylinder(stage, new(hx + x, top + 1.1f, hz + z), .09f, .11f, 2.2f, "a77a59");
        Art.Block(stage, new(hx, top + 1.1f, hz + .95f), new(2.1f, 2.0f, .1f), "c39771");
        Roof(stage, hx, hz, 1.4f, 1.2f, top + 2.15f, top + 3.0f, "8c5a46");
        Art.Log(stage, new(cx - 2.6f, top + .35f, cz - 2.0f), new(1, 0, .4f), .32f, 1.6f, "8a5f3f");
        var ball = Pivot(stage, new(cx - 1.0f, top + 3.0f, cz - 1.9f));
        Art.Beam(ball, Vector3.Zero, new(0, -1.5f, 0), .03f, "e8d9b0");
        Art.Ball(ball, new(0, -1.75f, 0), Vector3.One * .28f, "e2563b");
        PenSolid(pen, top);

        Panda(stage, new(cx - .6f, top, cz + .5f), Facing(pen) + .2f, 1.0f, 0f, mother: true);
        Panda(stage, new(cx + 1.5f, top, cz - 2.0f), Facing(pen) - .4f, .55f, 2.2f, mother: false);
    }

    private void Panda(Node3D parent, Vector3 home, float yaw, float scale, float phase, bool mother)
    {
        var beast = zoo.NewBeast(11, parent, home, yaw, scale, phase);
        var root = beast.Root;
        const string white = "f8f8f3",
            black = "23252b";
        Art.Ball(root, new(0, .65f, 0), new(.55f, .52f, .5f), white);
        Art.Ball(root, new(0, .92f, .04f), new(.58f, .16f, .44f), black);
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Ball(root, new(side * .4f, .2f, .05f), new(.2f, .16f, .34f), black);
            Art.Ball(root, new(side * .4f, .06f, .26f), new(.15f, .06f, .12f), "3a3d45");
        }
        var head = Pivot(root, new(0, 1.05f, .15f));
        Art.Ball(head, new(0, .3f, 0), new(.45f, .4f, .4f), white);
        foreach (float side in new[] { -1f, 1f })
        {
            Art.Ball(head, new(side * .34f, .62f, -.04f), Vector3.One * .14f, black);
            var patch = Art.Ball(head, new(side * .17f, .35f, .3f), new(.1f, .075f, .04f), black);
            patch.Rotation = new Vector3(0, 0, side * .5f);
            Art.Ball(head, new(side * .17f, .36f, .335f), Vector3.One * .025f, "ffffff");
        }
        Art.Ball(head, new(0, .17f, .33f), new(.15f, .1f, .1f), "f1ecdd");
        Art.Ball(head, new(0, .24f, .42f), new(.07f, .05f, .04f), black);
        Art.Ball(head, new(0, .1f, .41f), new(.06f, .015f, .02f), "7a4a4a");
        // The arms hold a bamboo stalk in front of the mouth.
        var bamboo = Pivot(root, new(0, .8f, .55f));
        Art.Beam(bamboo, new(-.05f, -.15f, 0), new(.12f, .5f, .05f), .075f, "6dbb5f");
        Art.Ball(bamboo, new(.12f, .52f, .05f), new(.14f, .04f, .08f), "7ccf7a");
        var arms = new List<(Node3D Pivot, float Side)>();
        foreach (float side in new[] { -1f, 1f })
        {
            var arm = Pivot(root, new(side * .42f, .85f, .15f));
            Art.Ball(arm, new(0, -.02f, .22f), new(.14f, .15f, .3f), black);
            arm.Rotation = new Vector3(-.5f, 0, side * .05f);
            arms.Add((arm, side));
        }
        beast.Animate = t =>
        {
            // Chewing: the head and the stalk bob. Every so often the panda rolls over for fun.
            float u = (t / (mother ? 12f : 7f) + phase) % 1f;
            float roll = Mathf.SmoothStep(.8f, .97f, u);
            head.Rotation = new Vector3(Mathf.Sin(t * 7 + phase) * .06f - .05f, Mathf.Sin(t * .9f + phase) * .15f, 0);
            bamboo.Position = new Vector3(0, .8f + Mathf.Sin(t * 7 + phase) * .015f, .55f);
            foreach (var (arm, side) in arms)
                arm.Rotation = new Vector3(-.5f + Mathf.Sin(t * 7 + phase) * .05f, 0, side * .05f);
            beast.Pitch = roll * Mathf.Tau * (mother ? 1 : -1);
            beast.Lift = Mathf.Sin(roll * Mathf.Pi) * (mother ? .5f : .3f);
            bamboo.Visible = roll < .02f;
        };
    }
}
