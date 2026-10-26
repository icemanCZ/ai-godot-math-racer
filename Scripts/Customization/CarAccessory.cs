using Godot;
using System.Collections.Generic;

namespace MathRacer;

public sealed record CarAccessory(string Id, string Name)
{
    public static IReadOnlyList<CarAccessory> All { get; } = System.Array.AsReadOnly(new[]
    {
        new CarAccessory("flower", "Kytička"),
        new CarAccessory("bow", "Mašle"),
        new CarAccessory("flag", "Vlaječka"),
        new CarAccessory("wheels", "Větší kola"),
        new CarAccessory("spoiler", "Zadní křídlo"),
        new CarAccessory("rack", "Střešní nosič"),
        new CarAccessory("suitcase", "Cestovní kufr"),
        new CarAccessory("surfboard", "Surfovací prkno"),
        new CarAccessory("lights", "Přídavné světlomety"),
        new CarAccessory("bumper", "Ochranný rám"),
        new CarAccessory("antenna", "Hvězdná anténa"),
        new CarAccessory("horns", "Dvojitá houkačka"),
        new CarAccessory("exhaust", "Sportovní výfuky"),
        new CarAccessory("spare", "Rezervní kolo")
    });

    public Node3D Create(Node3D parent)
    {
        var root = new Node3D { Name = "Attachment_" + Id, Visible = false };
        parent.AddChild(root);
        switch (Id)
        {
            case "spoiler":
                foreach (float side in new[] { -1f, 1f })
                    Art.Block(root, new(side * .61f, 1.47f, 1.32f), new(.09f, .42f, .10f), "344555");
                Art.Block(root, new(0, 1.70f, 1.32f), new(1.88f, .10f, .32f), "48bfc2");
                break;
            case "rack":
                foreach (float side in new[] { -1f, 1f })
                {
                    Art.Block(root, new(side * .52f, 2.15f, .18f), new(.055f, .16f, .83f), "fff0ce");
                    foreach (float z in new[] { -.18f, .54f })
                        Art.Block(root, new(0, 2.12f, z), new(1.13f, .06f, .055f), "344555");
                }
                break;
            case "suitcase":
                foreach (float z in new[] { .04f, .47f })
                    Art.Block(root, new(-.28f, 2.08f, z), new(.35f, .10f, .06f), "344555");
                Art.Block(root, new(-.28f, 2.31f, .26f), new(.40f, .39f, .63f), "c08049");
                foreach (float z in new[] { .04f, .47f })
                    Art.Block(root, new(-.28f, 2.51f, z), new(.41f, .025f, .06f), "ffe3a3");
                Art.Block(root, new(-.28f, 2.55f, .26f), new(.19f, .07f, .05f), "5e493d");
                break;
            case "surfboard":
                foreach (float z in new[] { -.30f, .60f })
                    Art.Block(root, new(.28f, 2.15f, z), new(.30f, .21f, .07f), "344555");
                Art.Ball(root, new(.28f, 2.31f, .18f), new(.22f, .065f, 1.11f), "ffba50");
                Art.Block(root, new(.28f, 2.376f, .18f), new(.065f, .008f, 1.76f), "fff5df");
                break;
            case "lights":
                foreach (float x in new[] { -.30f, .30f })
                {
                    Art.Ball(root, new(x, 1.03f, -1.58f), new(.17f, .17f, .09f), "344555");
                    Art.Ball(root, new(x, 1.03f, -1.65f), new(.135f, .135f, .04f), "fff0a1");
                }
                break;
            case "bumper":
                foreach (float x in new[] { -.89f, .89f })
                    Art.Cylinder(root, new(x, .80f, -1.70f), .045f, .045f, .52f, "c6dce3");
                Art.Block(root, new(0, 1.06f, -1.70f), new(1.84f, .08f, .08f), "c6dce3");
                break;
            case "antenna":
                Art.Cylinder(root, new(-.72f, 1.88f, 1.12f), .018f, .018f, 1.35f, "344555");
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.Tau / 5;
                    Art.Ball(root, new(-.72f + Mathf.Sin(angle) * .12f, 2.58f + Mathf.Cos(angle) * .12f, 1.12f), new(.085f, .085f, .04f), "ffd04b");
                }
                break;
            case "horns":
                foreach (float side in new[] { -1f, 1f })
                {
                    var horn = Art.Cylinder(root, new(side * .69f, 2.14f, -.13f), .11f, .045f, .36f, "ffce58");
                    horn.Rotation = new(-Mathf.Pi / 2, 0, 0);
                    Art.Ball(root, new(side * .69f, 2.14f, -.32f), new(.075f, .075f, .018f), "76583a");
                }
                break;
            case "exhaust":
                foreach (float side in new[] { -1f, 1f })
                {
                    var pipe = Art.Cylinder(root, new(side * .50f, .43f, 1.59f), .085f, .085f, .36f, "c6dce3");
                    pipe.Rotation = new(Mathf.Pi / 2, 0, 0);
                    Art.Ball(root, new(side * .50f, .43f, 1.78f), new(.065f, .065f, .014f), "344555");
                }
                break;
            case "spare":
                var tyre = Art.Cylinder(root, new(0, 1.01f, 1.69f), .34f, .34f, .18f, "263440");
                tyre.Rotation = new(Mathf.Pi / 2, 0, 0);
                var rim = Art.Cylinder(root, new(0, 1.01f, 1.79f), .21f, .21f, .025f, "fff0ce");
                rim.Rotation = new(Mathf.Pi / 2, 0, 0);
                Art.Ball(root, new(0, 1.01f, 1.82f), new(.07f, .07f, .025f), "48bfc2");
                break;
            case "flower":
                // A flat flower pinned to the bonnet, visible from the driving camera.
                root.Position = new Vector3(-.43f, 1.29f, -1.02f);
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.Tau / 6;
                    Art.Ball(root, new(Mathf.Cos(angle) * .22f, 0, Mathf.Sin(angle) * .22f), new(.16f, .07f, .16f), "fff5df");
                }
                Art.Ball(root, new(0, .055f, 0), new(.14f, .08f, .14f), "ffca38");
                Art.Ball(root, new(.22f, -.04f, .26f), new(.11f, .04f, .22f), "55ab76");
                break;
            case "bow":
                root.Position = new Vector3(0, 2.10f, .19f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var loop = Art.Ball(root, new(side * .25f, .07f, 0), new(.29f, .16f, .2f), "d968b1");
                    loop.Rotation = new Vector3(0, 0, side * .25f);
                    var ribbon = Art.Block(root, new(side * .15f, -.06f, .23f), new(.18f, .045f, .36f), "e98ac6");
                    ribbon.Rotation = new Vector3(0, side * -.3f, 0);
                }
                Art.Ball(root, Vector3.Zero, new(.13f, .15f, .15f), "ffe4f2");
                break;
            case "flag":
                root.Position = new Vector3(.63f, 1.1f, 1.05f);
                Art.Cylinder(root, new(0, .77f, 0), .025f, .025f, 1.54f, "fff5df");
                Art.Ball(root, new(0, 1.56f, 0), new(.065f, .065f, .065f), "ffca38");
                var mesh = new ArrayMesh();
                var arrays = new Godot.Collections.Array(); arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[] { new(0, 1.49f, 0), new(.62f, 1.26f, 0), new(0, 1.03f, 0) };
                mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                var material = (StandardMaterial3D)Art.Mat("5bc5bd").Duplicate();
                material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
                var pennant = Art.Mesh(root, mesh, Vector3.Zero, Vector3.One, "5bc5bd");
                pennant.MaterialOverride = material;
                break;
        }
        return root;
    }
}
