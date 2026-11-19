using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Garage;

public sealed record CarAccessory(string Id, string Name)
{
    public static IReadOnlyList<CarAccessory> All { get; } =
        System.Array.AsReadOnly(
            new[]
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
                new CarAccessory("spare", "Rezervní kolo"),
            }
        );

    public static CarAccessory ValleyMap { get; } = new("valley-map", "Mapka údolí");

    /// <summary>The sack of flour that the finished mill brings.</summary>
    public static CarAccessory FlourSack { get; } = new("flour-sack", "Pytel mouky");

    /// <summary>The binoculars that the finished lookout tower brings.</summary>
    public static CarAccessory Binoculars { get; } = new("binoculars", "Dalekohled");

    /// <summary>The bunch of balloons that the finished playground brings.</summary>
    public static CarAccessory Balloons { get; } = new("balloons", "Balónky");

    public static IReadOnlyList<CarAccessory> Catalogue { get; } =
        System.Array.AsReadOnly(All.Concat(new[] { ValleyMap, FlourSack, Binoculars, Balloons }).ToArray());

    internal static void PlaceValleyMap(Node3D sticker, string model)
    {
        // The sticker faces backwards and is tipped by the angle of the surface it lies on; on the toys it lies on
        // the deck of the tank, the back wall of the cab, the cap of the saucer or beside the pilot.
        var (position, tilt) = model switch
        {
            "pickup" => (new Vector3(-.23f, 1.62f, .60f), 0f),
            "wagon" => (new(-.23f, 1.62f, 1.29f), 0f),
            "buggy" => (new(0, 1.95f, .798f), 0f),
            "plane" => (new(0, 1.64f, .5f), 0f),
            "tank" => (new(0, 1.28f, .98f), -90f),
            "train" => (new(-.58f, 1.3f, 1.375f), 0f),
            "rocket" => (new(0, 1.945f, .025f), -90f),
            "ufo" => (new(0, 1.35f, .73f), -56f),
            "classic" => (new(-.23f, 1.62f, .93f), -34f),
            _ => (new(-.23f, 1.62f, .93f), 0f),
        };
        sticker.Position = position;
        sticker.RotationDegrees = new Vector3(tilt, 0, 0);
    }

    /// <summary>
    /// How far an accessory moves so that it sits on a model that is not shaped like the car it was drawn for: the
    /// flag on the plane's tail fin, the sack on the tank's rear deck, the spare wheel on the back wall of the cab.
    /// Everything that is not listed stays where it was drawn.
    /// </summary>
    internal static Vector3 Offset(string model, string accessory)
    {
        bool roof = accessory is "rack" or "suitcase" or "surfboard" or "bow";
        bool bonnet = accessory is "flower" or "binoculars";
        static Vector3 Up(float y) => new(0, y, 0);
        return model switch
        {
            "plane" => accessory switch
            {
                "spare" => new(0, .11f, -.25f),
                "exhaust" => new(0, .42f, -1.08f),
                "antenna" => new(.5f, -.35f, -.5f),
                "flag" => new(-.63f, 0, .35f),
                "flour-sack" => new(-.34f, -.35f, -.05f),
                "balloons" => new(.34f, -.45f, .3f),
                "lights" => new(0, .9f, 1.1f),
                "bumper" => new(0, 0, .55f),
                _ when bonnet => Up(-.12f),
                _ => Vector3.Zero,
            },
            "tank" => accessory switch
            {
                "spare" => new(0, 0, -.2f),
                "antenna" => Up(-.6f),
                "flour-sack" => Up(-.7f),
                "balloons" => Up(-.85f),
                "horns" => Up(-.3f),
                _ when roof => Up(-.2f),
                _ => Vector3.Zero,
            },
            "train" => accessory switch
            {
                "spare" or "exhaust" => new(0, 0, -.28f),
                "flour-sack" => Up(.25f),
                "balloons" => Up(.1f),
                "horns" => new(0, .1f, .9f),
                _ when roof => Up(.1f),
                _ when bonnet => Up(.38f),
                _ => Vector3.Zero,
            },
            "rocket" => accessory switch
            {
                "spare" => new(0, .21f, -.3f),
                "exhaust" => new(0, 0, -.5f),
                "antenna" => new(.35f, -.45f, -.25f),
                "balloons" => Up(-.4f),
                "lights" => new(0, .2f, .2f),
                "horns" => Up(-.75f),
                _ when roof => Up(-.25f),
                _ when bonnet => Up(.38f),
                _ => Vector3.Zero,
            },
            "ufo" => accessory switch
            {
                "spoiler" => new(0, 0, -.5f),
                "spare" => new(0, .1f, -.45f),
                "exhaust" => new(0, .55f, -.55f),
                "antenna" => new(.2f, -.5f, -.4f),
                "lights" or "bumper" => new(0, 0, .4f),
                "horns" => Up(-.7f),
                _ when roof => Up(-.45f),
                _ => Vector3.Zero,
            },
            _ => Vector3.Zero,
        };
    }

    public Node3D Create(Node3D parent)
    {
        var root = new Node3D { Name = "Attachment_" + Id, Visible = false };
        parent.AddChild(root);
        switch (Id)
        {
            case "valley-map":
                PlaceValleyMap(root, "classic");
                Art.Block(root, Vector3.Zero, new(.34f, .17f, .012f), "fff0ce");
                Art.Block(root, new(0, 0, .008f), new(.305f, .138f, .007f), "8cc681");
                var river = Art.Block(root, new(.02f, 0, .014f), new(.029f, .14f, .004f), "55b8dc");
                river.RotateZ(-.35f);
                Art.Block(root, new(0, .014f, .018f), new(.095f, .025f, .006f), "c28a50");
                Art.Ball(root, new(-.088f, -.024f, .016f), new(.025f, .025f, .004f), "ee805e");
                break;
            case "flour-sack":
                // A plump sack tied at the neck, standing behind the roof.
                Art.Ball(root, new(.34f, 2.2f, 1.05f), new(.34f, .4f, .3f), "f1e9d6");
                Art.Ball(root, new(.34f, 2.5f, 1.05f), new(.13f, .1f, .12f), "e0d3b4");
                Art.Block(root, new(.34f, 2.4f, 1.05f), new(.2f, .04f, .18f), "b8895f");
                Art.Block(root, new(.34f, 2.28f, .89f), new(.16f, .06f, .02f), "c4623f");
                break;
            case "balloons":
                // Five balloons on strings tied behind the roof, on the other side from the sack of flour.
                var tie = new Vector3(-.34f, 2.15f, 1.0f);
                (string Color, float X, float Y, float Z)[] bunch =
                {
                    ("e2563b", -.05f, .95f, 0f),
                    ("f2c14e", .3f, 1.15f, .1f),
                    ("4f8fb8", -.35f, 1.2f, .05f),
                    ("5aa86f", .1f, 1.5f, -.08f),
                    ("d9718f", -.2f, 1.6f, .12f),
                };
                foreach (var (color, x, y, z) in bunch)
                {
                    Art.Beam(root, tie, tie + new Vector3(x, y - .26f, z), .012f, "f5f0e6");
                    Art.Ball(root, tie + new Vector3(x, y, z), new(.2f, .25f, .2f), color);
                    Art.Ball(root, tie + new Vector3(x, y - .27f, z), new(.03f, .03f, .03f), color);
                }
                break;
            case "binoculars":
                // Two lenses and a bridge, pinned to the bonnet like the flower on the other side.
                root.Position = new Vector3(.43f, 1.32f, -1.02f);
                foreach (float side in new[] { -1f, 1f })
                {
                    var tube = Art.Cylinder(root, new(side * .09f, 0, 0), .075f, .075f, .3f, "344555");
                    tube.Rotation = new(Mathf.Pi / 2, 0, 0);
                    Art.Ball(root, new(side * .09f, 0, -.16f), new(.065f, .065f, .02f), "9ed5df");
                }
                Art.Block(root, new(0, .01f, .0f), new(.1f, .04f, .12f), "5e493d");
                break;
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
                    Art.Ball(
                        root,
                        new(-.72f + Mathf.Sin(angle) * .12f, 2.58f + Mathf.Cos(angle) * .12f, 1.12f),
                        new(.085f, .085f, .04f),
                        "ffd04b"
                    );
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
                    Art.Ball(
                        root,
                        new(Mathf.Cos(angle) * .22f, 0, Mathf.Sin(angle) * .22f),
                        new(.16f, .07f, .16f),
                        "fff5df"
                    );
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
                var arrays = new Godot.Collections.Array();
                arrays.Resize((int)Mesh.ArrayType.Max);
                arrays[(int)Mesh.ArrayType.Vertex] = new Vector3[]
                {
                    new(0, 1.49f, 0),
                    new(.62f, 1.26f, 0),
                    new(0, 1.03f, 0),
                };
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
