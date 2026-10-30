using Godot;
using System.Collections.Generic;

namespace MathRacer;

public partial class Village
{
    // Permanent scenery is separate from earned upgrades and uses no random state.
    // Each material is baked into one mesh per building, rather than one draw per tile.
    private void AddVillageDetails()
    {
        for (int i = 0; i < Houses.Count; i++)
        {
            var detail = DetailRoot(Houses[i]);
            BuildingTrim(detail, 7, 6, 4.2f);
            CottageRoof(detail, i);
            DoorDetails(detail, 0, 3.16f, 2.5f);
            foreach (float x in new[] { -2.15f, 2.15f })
                WindowDetails(detail, new(x, 2.5f, 3.34f), 1.12f, 1.12f);
            foreach (int side in new[] { -1, 1 })
            {
                var wall = new Node3D { Position = new(side * 3.56f, 0, 0), Rotation = new(0, side * Mathf.Pi / 2, 0) };
                detail.AddChild(wall);
                foreach (float x in new[] { -1.55f, 1.55f })
                {
                    Art.Block(wall, new(x, 2.4f, 0), new(1.4f, 1.5f, .12f), "fff1d2");
                    Art.Block(wall, new(x, 2.4f, .08f), new(1.15f, 1.25f, .08f), "88c7d8");
                    WindowDetails(wall, new(x, 2.4f, .14f), 1.15f, 1.25f);
                }
            }
            WallLantern(detail, new(.99f, 2.45f, 3.35f));
            // A small ceramic house number leaves the reward door handle untouched.
            Art.Block(detail, new(-.98f, 1.65f, 3.12f), new(.38f, .42f, .08f), "fff1d2");
            var number = Art.Sign(Houses[i], new(-.98f, 1.65f, 3.2f), (i + 1).ToString(), 28);
            number.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
            number.PixelSize = .009f; number.Modulate = new Color("526f71"); number.OutlineSize = 0;
            BakeDetails(detail);
        }

        for (int i = 0; i < Shops.Count; i++)
        {
            var detail = DetailRoot(Shops[i]);
            BuildingTrim(detail, 8, 6, 3.8f);
            FlatRoof(detail, 8.8f, 7, 4.28f, "b76e5c");
            DoorDetails(detail, -2.3f, 3.16f, 2.6f);
            WindowDetails(detail, new(.9f, 2, 3.26f), 3.4f, 1.4f);
            Art.Block(detail, new(.9f, 1.42f, 3.34f), new(3.45f, .12f, .3f), "c39871");
            for (int item = 0; item < 3; item++)
            {
                var display = new Node3D { Position = new(-.2f + item * 1.08f, 1.5f, 3.38f), Scale = Vector3.One * .52f };
                detail.AddChild(display); Expedition.AddCargo(display, Vector3.Zero, TradeCargo[i]);
            }
            for (int stripe = 0; stripe < 8; stripe++)
            {
                string color = stripe % 2 == 0 ? new[] { "f4c57c", "83cbb7", "eab0ca" }[i] : "fff4df";
                Art.Block(detail, new(-3.5f + stripe, 3.03f, 4.36f), new(.98f, .3f, .09f), color);
                Art.Ball(detail, new(-3.5f + stripe, 2.9f, 4.36f), new(.48f, .13f, .06f), color);
            }
            WallLantern(detail, new(-3.48f, 2.25f, 3.3f));
            BakeDetails(detail);
        }

        var farm = DetailRoot(FarmBuilding);
        BuildingTrim(farm, 8, 6, 4);
        for (int i = 0; i < 17; i++)
        {
            float x = -3.8f + i * .475f;
            Art.Block(farm, new(x, 1.9f, 3.025f), new(.035f, 3.8f, .025f), "b75c48");
            Art.Block(farm, new(x, 1.9f, -3.025f), new(.035f, 3.8f, .025f), "b75c48");
        }
        Art.Block(farm, new(0, 3.15f, 3.22f), new(4.5f, .2f, .2f), "fff1d2");
        foreach (float x in new[] { -.2f, .2f })
            Art.Block(farm, new(x, 1.5f, 3.27f), new(.12f, .4f, .1f), "526f71");
        foreach (int side in new[] { -1, 1 })
        {
            for (int i = 0; i < 9; i++)
            {
                var seam = Art.Block(farm, new(side * 2.1f, 5.17f, -3.2f + i * .8f), new(5.1f, .055f, .055f), "96795a");
                seam.RotationDegrees = new(0, 0, side * -28);
            }
        }
        WallLantern(farm, new(-2.65f, 2.8f, 3.3f));
        BakeDetails(farm);

        foreach (var building in new[] { GardenBuilding, MuseumBuilding, ToyBuilding })
        {
            bool garden = building == GardenBuilding;
            var detail = DetailRoot(building);
            BuildingTrim(detail, garden ? 6 : 7, garden ? 5 : 5.5f, garden ? 3.4f : 3.6f);
            FlatRoof(detail, garden ? 7 : 8, garden ? 6 : 6.5f, garden ? 3.75f : 4.1f,
                garden ? "c39871" : building == ToyBuilding ? "ca6f79" : "649eaf");
            DoorDetails(detail, garden ? -1 : -1.5f, garden ? 2.66f : 2.91f, garden ? 2.4f : 2.6f);
            WindowDetails(detail, new(garden ? 1.4f : 1.3f, 2, garden ? 2.8f : 3.05f), garden ? 1.2f : 1.7f, garden ? 1.1f : 1.2f);
            WallLantern(detail, new(garden ? -2.35f : -2.65f, 2.5f, garden ? 2.8f : 3.05f));
            BakeDetails(detail);
        }

        GarageDetails();
        SquareDetails();
    }

    private static Node3D DetailRoot(Node3D parent)
    {
        var root = new Node3D { Name = "ArchitecturalDetails" }; parent.AddChild(root); return root;
    }

    private static void BuildingTrim(Node3D parent, float width, float depth, float height)
    {
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(parent, new(0, .3f, side * (depth / 2 + .035f)), new(width, .6f, .1f), "c5b598");
            Art.Block(parent, new(0, height - .12f, side * (depth / 2 + .06f)), new(width + .12f, .2f, .16f), "fff1d2");
            Art.Block(parent, new(side * (width / 2 + .035f), .3f, 0), new(.1f, .6f, depth), "c5b598");
            Art.Block(parent, new(side * (width / 2 + .06f), height - .12f, 0), new(.16f, .2f, depth), "fff1d2");
            foreach (int corner in new[] { -1, 1 })
                Art.Block(parent, new(side * (width / 2 - .08f), height / 2, corner * (depth / 2 + .07f)), new(.22f, height, .18f), "fff1d2");
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < (int)width; i++)
                {
                    float x = -width / 2 + .4f + i + row * .25f;
                    Art.Block(parent, new(x, .15f + row * .3f, side * (depth / 2 + .095f)), new(.78f, .23f, .035f), (i + row) % 3 == 0 ? "dfd4bd" : "b7ab96");
                }
        }
    }

    private static void CottageRoof(Node3D parent, int house)
    {
        string[] colors = { "d85f4b", "c97555", "bb5c50", "d88459" };
        // Raised courses and staggered joints describe individual terracotta tiles.
        foreach (int side in new[] { -1, 1 })
        {
            for (int row = 0; row < 7; row++)
            {
                float x = side * (.28f + row * .56f);
                float y = 6.7f - Mathf.Abs(x) * .675f + .065f;
                var course = Art.Block(parent, new(x, y, 0), new(.12f, .065f, 7.22f), colors[house]);
                course.Rotation = new(0, 0, -side * Mathf.Atan(.675f));
                for (int tile = 0; tile < 10; tile++)
                {
                    float z = -3.3f + tile * .72f + row % 2 * .22f;
                    if (z > 3.5f) continue;
                    var joint = Art.Block(parent, new(x, y + .007f, z), new(.62f, .035f, .028f), colors[house]);
                    joint.Rotation = new(0, 0, -side * Mathf.Atan(.675f));
                }
            }
            foreach (float z in new[] { -3.64f, 3.64f })
                Art.Beam(parent, new(0, 6.75f, z), new(side * 4.04f, 4.02f, z), .18f, "fff1d2");
            Art.Block(parent, new(side * 3.9f, 4.02f, 0), new(.22f, .16f, 7.4f), "a87b61");
            foreach (float z in new[] { -2.6f, 0f, 2.6f })
                Art.Beam(parent, new(side * 3.45f, 3.5f, z), new(side * 3.85f, 4, z), .13f, "a87b61");
        }
        Art.Cylinder(parent, new(0, 6.76f, 0), .13f, .13f, 7.35f, "f0a17a").Rotation = new(Mathf.Pi / 2, 0, 0);
        Art.Block(parent, new(2, 6.87f, 0), new(1.08f, .18f, 1.2f), "b76e5c");
        Art.Block(parent, new(2, 6.98f, 0), new(.57f, .04f, .72f), "725742");
        for (int row = 0; row < 5; row++)
        {
            float y = 5.55f + row * .26f;
            Art.Block(parent, new(2, y, .505f), new(.86f, .035f, .025f), "c39871");
            Art.Block(parent, new(2 + (row % 2 == 0 ? -.14f : .2f), y + .12f, .51f), new(.035f, .22f, .025f), "c39871");
        }
        // The attic window itself remains an earned upgrade.
        Art.Beam(parent, new(-2.9f, 4.35f, 3.63f), new(2.9f, 4.35f, 3.63f), .14f, "a87b61");
    }

    private static void DoorDetails(Node3D parent, float x, float z, float height)
    {
        foreach (float side in new[] { -.76f, .76f })
            Art.Block(parent, new(x + side, height / 2, z), new(.14f, height + .12f, .16f), "fff1d2");
        Art.Block(parent, new(x, height + .06f, z), new(1.65f, .16f, .18f), "fff1d2");
        for (int i = 0; i < 5; i++)
            Art.Block(parent, new(x - .51f + i * .255f, height * .4f, z + .02f), new(.025f, height * .72f, .035f), "916c51");
        Art.Block(parent, new(x, height * .78f, z + .035f), new(.75f, .55f, .05f), "fff1d2");
        Art.Block(parent, new(x, height * .78f, z + .07f), new(.59f, .39f, .04f), "88c7d8");
    }

    private static void WindowDetails(Node3D parent, Vector3 center, float width, float height)
    {
        Art.Block(parent, center, new(.075f, height, .06f), "fff1d2");
        Art.Block(parent, center, new(width, .075f, .06f), "fff1d2");
        Art.Block(parent, center + new Vector3(0, -height / 2 - .13f, .035f), new(width + .37f, .16f, .32f), "fff1d2");
        // Pale inset reflections read as glass without transparency sorting.
        var reflection = Art.Block(parent, center + new Vector3(-width * .23f, height * .2f, .005f), new(width * .12f, height * .32f, .018f), "ccebf0");
        reflection.Rotation = new(0, 0, -.35f);
    }

    private static void WallLantern(Node3D parent, Vector3 p)
    {
        Art.Block(parent, p + new Vector3(0, .2f, -.12f), new(.14f, .35f, .15f), "526f71");
        Art.Block(parent, p, new(.28f, .4f, .25f), "ffe3a0");
        foreach (float x in new[] { -.16f, .16f })
            Art.Block(parent, p + new Vector3(x, 0, .14f), new(.045f, .45f, .045f), "526f71");
        Art.Cylinder(parent, p + Vector3.Up * .27f, .27f, .03f, .17f, "526f71");
        Art.Block(parent, p + Vector3.Down * .23f, new(.38f, .07f, .34f), "526f71");
    }

    private static void FlatRoof(Node3D parent, float width, float depth, float y, string color)
    {
        for (int row = 0; row < (int)(depth / .55f); row++)
            Art.Block(parent, new(0, y, -depth / 2 + .3f + row * .55f), new(width - .15f, .055f, .08f), color);
        foreach (int side in new[] { -1, 1 })
            Art.Block(parent, new(0, y - .18f, side * depth / 2), new(width, .14f, .12f), "fff1d2");
    }

    private static void BakeDetails(Node3D root)
    {
        var batches = new Dictionary<Material, SurfaceTool>();
        var meshes = root.FindChildren("*", "MeshInstance3D", true, false);
        foreach (var child in meshes)
        {
            var instance = (MeshInstance3D)child;
            if (instance.Mesh == null || instance.MaterialOverride == null) continue;
            if (!batches.TryGetValue(instance.MaterialOverride, out var surface))
            {
                surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
                batches.Add(instance.MaterialOverride, surface);
            }
            var local = root.GlobalTransform.AffineInverse() * instance.GlobalTransform;
            surface.AppendFrom(instance.Mesh, 0, local);
        }
        foreach (var child in root.GetChildren()) { root.RemoveChild(child); child.QueueFree(); }
        foreach (var batch in batches)
            root.AddChild(new MeshInstance3D { Name = "DetailBatch", Mesh = batch.Value.Commit(), MaterialOverride = batch.Key });
    }

    private void GarageDetails()
    {
        var detail = DetailRoot(GarageBuilding);
        FlatRoof(detail, 8.5f, 7, 4.33f, "c39871");
        foreach (float x in new[] { -3.7f, 3.7f })
        {
            Art.Block(detail, new(x, 1.8f, 3.07f), new(.55f, 3.6f, .16f), "fff1d2");
            WallLantern(detail, new(x, 2.85f, 3.3f));
        }
        // Tools and a workbench sit against the back wall, clear of the entrance.
        Art.Block(detail, new(0, 1, -2.35f), new(4.8f, .18f, .65f), "c39871");
        foreach (float x in new[] { -2f, 2f })
            Art.Block(detail, new(x, .5f, -2.35f), new(.14f, 1, .5f), "526f71");
        Art.Block(detail, new(0, 2.15f, -2.54f), new(4.4f, 1.55f, .1f), "a87b61");
        for (int i = 0; i < 7; i++)
        {
            float x = -1.65f + i * .55f;
            Art.Block(detail, new(x, 2.15f, -2.42f), new(.075f, .65f, .08f), "d9e3dc");
            Art.Block(detail, new(x, 2.48f, -2.42f), new(.25f, .12f, .08f), i % 2 == 0 ? "d9e3dc" : "edb45e");
        }
        BakeDetails(detail);
        var workbench = new Node3D { Name = "Workbench" };
        GarageBuilding.AddChild(workbench);
        Obstacles.Box(workbench, new(0, .55f, -2.35f), new(4.8f, 1.1f, .65f));
    }

    private void SquareDetails()
    {
        foreach (var node in Square.GetChildren())
        {
            if (node is not Node3D prop || (prop.Name != "FruitStall" && prop.Name != "BakeryStall" && prop.Name != "SquareBench" && prop.Name != "Fountain")) continue;
            var detail = DetailRoot(prop);
            if (prop.Name == "Fountain")
            {
                // Thin arcing cascades connect the two pools, inside the basin collider.
                for (int stream = 0; stream < 8; stream++)
                {
                    float angle = stream * Mathf.Tau / 8;
                    var direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                    var previous = direction * .92f + Vector3.Up * 1.74f;
                    for (int segment = 1; segment <= 8; segment++)
                    {
                        float t = segment / 8f;
                        var next = direction * (.92f + .65f * t) + Vector3.Up * (1.74f + .25f * t - 1.65f * t * t);
                        Art.Beam(detail, previous, next, .045f, stream % 2 == 0 ? "b3e8e5" : "c6f1e9");
                        previous = next;
                    }
                    Art.Ball(detail, direction * 1.57f + Vector3.Up * .37f, new(.15f, .025f, .15f), "c6f1e9");
                }
            }
            else if (prop.Name == "SquareBench")
            {
                foreach (float x in new[] { -1.15f, 1.15f })
                {
                    Art.Block(detail, new(x, .94f, .07f), new(.12f, .12f, .85f), "526f71");
                    foreach (float y in new[] { 1.05f, 1.35f })
                        Art.Ball(detail, new(x, y, -.425f), Vector3.One * .045f, "526f71");
                }
            }
            else
            {
                for (int i = 0; i < 10; i++)
                    Art.Block(detail, new(-1.52f + i * .34f, .66f, .77f), new(.035f, 1.15f, .04f), "916c51");
                foreach (float x in new[] { -1.1f, 0f, 1.1f })
                {
                    foreach (float side in new[] { -.49f, .49f })
                        Art.Block(detail, new(x + side, 1.6f, 0), new(.065f, .22f, 1.1f), "c39871");
                    Art.Block(detail, new(x, 1.6f, -.53f), new(1, .22f, .065f), "c39871");
                    Art.Block(detail, new(x, 1.6f, .53f), new(1, .22f, .065f), "c39871");
                }
            }
            BakeDetails(detail);
        }
    }
}
