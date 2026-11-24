using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Graphics;

namespace MathRacer.Ui;

/// <summary>Font-independent instructions and portraits for children who cannot read.</summary>
public partial class DeliveryPicture : Control
{
    public string Icon { get; init; } = "";
    public Character? Character { get; init; }
    public int Count { get; init; } = 1;
    public int FilledCount { get; set; } = -1;
    public float Direction { get; set; }

    /// <summary>Material drawn on the loading question's trailer: stones, soil or sand.</summary>
    public string Cargo { get; set; } = "stones";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    private void Oval(Vector2 p, Vector2 radius, Color color) =>
        DrawColoredPolygon(
            Enumerable
                .Range(0, 40)
                .Select(i =>
                    p + new Vector2(Mathf.Cos(i * Mathf.Tau / 40) * radius.X, Mathf.Sin(i * Mathf.Tau / 40) * radius.Y)
                )
                .ToArray(),
            color
        );

    public override void _Draw()
    {
        float scale = Mathf.Min(Size.X / 180, Size.Y / 150);
        DrawSetTransform((Size - new Vector2(180, 150) * scale) / 2, 0, Vector2.One * scale);
        Color ink = new("3c5960"),
            green = new("70b765");
        if (Character != null)
        {
            var fur = new Color(Character.FurColor);
            foreach (int sign in new[] { -1, 1 })
            {
                Oval(new(90 + sign * 31, Character.Kind == 0 ? 35 : 48), new(17, Character.Kind == 0 ? 33 : 20), fur);
                Oval(
                    new(90 + sign * 31, Character.Kind == 0 ? 35 : 48),
                    new(8, Character.Kind == 0 ? 23 : 10),
                    new("eab4ad")
                );
            }
            Oval(new(90, 88), new(58, 49), fur);
            Oval(new(90, 111), new(30, 20), new("fff0d9"));
            foreach (int sign in new[] { -1, 1 })
            {
                Oval(new(90 + sign * 24, 83), new(12, 15), Colors.White);
                Oval(new(90 + sign * 24, 85), new(5, 8), ink);
            }
            Oval(new(90, 104), new(9, 6), ink);
            DrawArc(new(90, 109), 14, .15f, Mathf.Pi - .15f, 20, ink, 3, true);
        }
        else if (Icon is "carrot" or "apple" or "honey" or "dots" or "bread" or "parcel" or "cake")
        {
            for (int i = 0; i < Count; i++)
            {
                float x = 90 + (i - (Count - 1) / 2f) * 51;
                if (Icon == "carrot")
                {
                    DrawColoredPolygon(
                        new Vector2[] { new(x - 17, 53), new(x + 19, 60), new(x - 10, 119) },
                        new Color("f59242")
                    );
                    for (int j = -1; j <= 1; j++)
                        DrawLine(new(x, 56), new(x + j * 13, 30), green, 7, true);
                }
                else if (Icon == "apple")
                {
                    Oval(new(x - 9, 86), new(17, 26), new("ed776b"));
                    Oval(new(x + 9, 86), new(17, 26), new("ed776b"));
                    DrawLine(new(x, 68), new(x + 3, 48), ink, 4, true);
                    Oval(new(x + 14, 54), new(12, 5), green);
                }
                else if (Icon == "honey")
                {
                    DrawStyleBox(
                        new StyleBoxFlat
                        {
                            BgColor = new("f1bb43"),
                            CornerRadiusTopLeft = 10,
                            CornerRadiusTopRight = 10,
                            CornerRadiusBottomLeft = 12,
                            CornerRadiusBottomRight = 12,
                        },
                        new(x - 22, 60, 44, 52)
                    );
                    DrawRect(new(x - 24, 51, 48, 13), new Color("a27452"));
                    Oval(new(x, 87), new(12, 15), new("fff5df"));
                }
                else if (Icon == "bread")
                {
                    Oval(new(x, 85), new(24, 17), new("d99b52"));
                    for (int cut = -1; cut <= 1; cut++)
                        DrawLine(new(x + cut * 12 - 3, 74), new(x + cut * 12 + 3, 85), new Color("ffe1a4"), 4, true);
                }
                else if (Icon == "parcel")
                {
                    DrawRect(new(x - 22, 62, 44, 44), new Color("bf9565"));
                    DrawRect(new(x - 5, 62, 10, 44), new Color("fff0cf"));
                    DrawRect(new(x - 22, 77, 44, 8), new Color("fff0cf"));
                }
                else if (Icon == "cake")
                {
                    DrawRect(new(x - 22, 70, 44, 36), new Color("e9a0be"));
                    Oval(new(x, 70), new(22, 9), new("fff2dc"));
                    DrawCircle(new(x, 56), 8, new Color("ed586e"));
                }
                else
                {
                    DrawCircle(new(x, 75), 18, ink);
                    DrawCircle(new(x, 75), 14, new Color("fff5df"));
                }
                if (FilledCount >= 0)
                {
                    DrawCircle(new(x, 136), 8, ink);
                    DrawCircle(new(x, 136), 5, i < FilledCount ? green : new Color("fff5df"));
                }
            }
        }
        else if (Icon is "trailer" or "trailer-load")
        {
            // A side view of the trailer. The loading question lays its places out in two tidy rows
            // inside the bed: loaded pieces look like the cargo, empty places are dark hollows.
            bool question = Icon == "trailer-load";
            var wood = new Color(question ? "c9a06f" : "c0573e");
            var rim = new Color(question ? "e6c593" : "e08a6f");
            var hollow = new Color("8a6641");
            Oval(new(88, 136), new(70, 7), new Color(0, 0, 0, .13f));
            DrawLine(new(18, 100), new(2, 108), ink, 5, true);
            DrawCircle(new(2, 108), 4, ink);
            DrawRect(new Rect2(12, 24, 156, 84), ink);
            DrawRect(new Rect2(15, 27, 150, 78), wood);
            DrawRect(new Rect2(15, 27, 150, 8), rim);
            DrawRect(new Rect2(15, 97, 150, 8), wood.Darkened(.14f));
            DrawRect(new Rect2(21, 39, 138, 56), hollow);
            DrawRect(new Rect2(21, 39, 138, 6), hollow.Darkened(.18f));
            foreach (int x in new[] { 52, 128 })
            {
                DrawCircle(new(x, 112), 19, ink);
                DrawCircle(new(x, 112), 14, new Color("4b6068"));
                DrawCircle(new(x, 112), 7, new Color("d8d2c4"));
            }
            if (question)
                for (int i = 0; i < Count; i++)
                {
                    var p = new Vector2(38 + (i % 5) * 26, 56 + (i / 5) * 28);
                    if (i >= FilledCount)
                    {
                        DrawArc(p, 10, 0, Mathf.Tau, 24, hollow.Darkened(.3f), 2.5f, true);
                        continue;
                    }
                    switch (Cargo)
                    {
                        case "soil":
                            Oval(p, new(12, 9), new("6f4b33"));
                            Oval(p + new Vector2(-2, -3), new(8, 5), new("8a6142"));
                            DrawCircle(p + new Vector2(5, 2), 2, new Color("57392a"));
                            break;
                        case "sand":
                            Oval(p, new(12, 9), new("c9aa66"));
                            Oval(p + new Vector2(-2, -3), new(8, 5), new("e8d296"));
                            DrawCircle(p + new Vector2(5, 2), 1.6f, new Color("b0914f"));
                            break;
                        case "logs":
                            Oval(p, new(12, 11), new("7a4f33"));
                            Oval(p, new(8, 7), new("dcb887"));
                            DrawArc(p, 4, 0, Mathf.Tau, 16, new("a77a59"), 1.5f, true);
                            break;
                        case "planks":
                            DrawRect(new Rect2(p - new Vector2(12, 8), new Vector2(24, 16)), new Color("a77a59"));
                            DrawRect(new Rect2(p - new Vector2(10, 6), new Vector2(20, 12)), new Color("d9b27a"));
                            DrawLine(p + new Vector2(-8, 0), p + new Vector2(8, 0), new Color("b8895f"), 1.5f);
                            break;
                        case "bricks":
                            DrawRect(new Rect2(p - new Vector2(12, 8), new Vector2(24, 16)), new Color("8a3f2d"));
                            DrawRect(new Rect2(p - new Vector2(10, 6), new Vector2(20, 12)), new Color("c4623f"));
                            DrawRect(new Rect2(p - new Vector2(8, 4), new Vector2(16, 3)), new Color("e0a384"));
                            break;
                        case "glass":
                            DrawColoredPolygon(
                                new Vector2[]
                                {
                                    p + new Vector2(-9, -10),
                                    p + new Vector2(9, -10),
                                    p + new Vector2(6, 10),
                                    p + new Vector2(-6, 10),
                                },
                                new Color("5b8a99")
                            );
                            DrawColoredPolygon(
                                new Vector2[]
                                {
                                    p + new Vector2(-7, -8),
                                    p + new Vector2(7, -8),
                                    p + new Vector2(5, 8),
                                    p + new Vector2(-5, 8),
                                },
                                new Color("cfeaf4")
                            );
                            break;
                        case "flour":
                            Oval(p, new(10, 11), new("d8d0c0"));
                            Oval(p + new Vector2(0, 1), new(8, 9), new("f6f2ea"));
                            Oval(p + new Vector2(0, -10), new(4, 3), new("d8d0c0"));
                            break;
                        case "wheat":
                            DrawLine(p + new Vector2(0, 11), p + new Vector2(0, -8), new Color("c9b45a"), 2.5f, true);
                            Oval(p + new Vector2(0, -7), new(5, 9), new("f2c64a"));
                            DrawLine(p + new Vector2(0, -7), p + new Vector2(0, -15), new Color("f7dc7a"), 1.5f, true);
                            break;
                        case "bread":
                            Oval(p, new(13, 10), new("b8732a"));
                            Oval(p + new Vector2(-1, -2), new(10, 7), new("e0a04a"));
                            DrawLine(p + new Vector2(-4, -4), p + new Vector2(-1, 2), new Color("b8732a"), 1.5f, true);
                            DrawLine(p + new Vector2(2, -5), p + new Vector2(5, 1), new Color("b8732a"), 1.5f, true);
                            break;
                        case "gravel":
                            Oval(p, new(12, 9), new("8f8a7d"));
                            Oval(p + new Vector2(-2, -2), new(9, 6), new("b9b4a5"));
                            DrawCircle(p + new Vector2(5, 2), 2.5f, new Color("d3cebf"));
                            break;
                        default:
                            Oval(p, new(12, 10), new("77756f"));
                            Oval(p + new Vector2(-2, -2), new(10, 8), new("8f8f8a"));
                            Oval(p + new Vector2(-4, -4), new(4, 3), new("c4c3bb"));
                            break;
                    }
                }
        }
        else if (Icon is "stones" or "soil" or "sand")
        {
            var (main, shade) = Icon switch
            {
                "stones" => (new Color("8f8f8a"), new Color("b4b3ab")),
                "soil" => (new Color("8a6142"), new Color("6f4b33")),
                _ => (new Color("e3c98a"), new Color("c9aa66")),
            };
            if (Icon == "stones")
                for (int i = 0; i < 6; i++)
                {
                    var p = new Vector2(55 + (i % 3) * 35 + (i / 3) * 17, 112 - (i / 3) * 27);
                    Oval(p, new(21, 16), main);
                    Oval(p + new Vector2(-6, -6), new(7, 5), shade);
                }
            else
            {
                DrawColoredPolygon(new Vector2[] { new(22, 124), new(70, 58), new(104, 52), new(158, 124) }, main);
                Oval(new(92, 60), new(24, 12), main);
                for (int i = 0; i < 7; i++)
                    DrawCircle(new(52 + i * 15, 104 - (i % 3) * 12), 3.5f, shade);
            }
        }
        else if (Icon == "logs")
        {
            // Six log ends in a pyramid.
            for (int i = 0; i < 6; i++)
            {
                int row =
                    i < 3 ? 0
                    : i < 5 ? 1
                    : 2;
                int column =
                    i < 3 ? i
                    : i < 5 ? i - 3
                    : 0;
                var p = new Vector2(90 + (column - (2 - row) / 2f) * 46, 112 - row * 40);
                DrawCircle(p, 22, new Color("5e3b26"));
                DrawCircle(p, 19, new Color("7a4f33"));
                DrawCircle(p, 15, new Color("dcb887"));
                DrawArc(p, 9, 0, Mathf.Tau, 20, new Color("a77a59"), 2, true);
                DrawCircle(p, 3, new Color("a77a59"));
            }
        }
        else if (Icon == "planks")
        {
            // A bundle of boards seen from the side, tied with two straps.
            string[] tones = { "d9b27a", "c99d63", "e0bd88", "c99d63" };
            for (int i = 0; i < 4; i++)
            {
                DrawRect(new Rect2(18, 104 - i * 24, 144, 22), ink);
                DrawRect(new Rect2(21, 107 - i * 24, 138, 16), new Color(tones[i]));
                DrawLine(new(30 + i * 20, 112 - i * 24), new(70 + i * 20, 112 - i * 24), new Color("b8895f"), 2);
            }
            foreach (int x in new[] { 52, 128 })
                DrawRect(new Rect2(x, 28, 8, 98), new Color("6f5a46"));
        }
        else if (Icon == "bricks")
        {
            // A wall of bricks with every other row shifted by half a brick.
            DrawRect(new Rect2(14, 28, 152, 100), ink);
            for (int row = 0; row < 4; row++)
            for (int i = 0; i < 4; i++)
            {
                float x = 18 + i * 37 - (row % 2 == 0 ? 0 : 18);
                float x0 = Mathf.Max(x, 18);
                float x1 = Mathf.Min(x + 35, 162);
                if (x1 <= x0)
                    continue;
                DrawRect(new Rect2(x0, 32 + row * 24, x1 - x0, 21), new Color(row % 2 == 0 ? "c4623f" : "b5533b"));
                DrawRect(new Rect2(x0 + 2, 34 + row * 24, x1 - x0 - 4, 4), new Color("e0a384"));
            }
        }
        else if (Icon == "gravel")
        {
            DrawColoredPolygon(
                new Vector2[] { new(18, 126), new(66, 62), new(112, 56), new(162, 126) },
                new Color("b9b4a5")
            );
            Oval(new(90, 62), new(26, 11), new Color("b9b4a5"));
            for (int i = 0; i < 14; i++)
                DrawCircle(
                    new(40 + (i * 37) % 100, 108 - (i * 13) % 44),
                    3 + i % 3,
                    new Color(i % 2 == 0 ? "8f8a7d" : "d3cebf")
                );
        }
        else if (Icon == "glass")
        {
            // Three tumblers in a row, wider at the top, each with a little water in it.
            foreach (int x in new[] { 40, 90, 140 })
            {
                DrawColoredPolygon(
                    new Vector2[] { new(x - 24, 36), new(x + 24, 36), new(x + 18, 124), new(x - 18, 124) },
                    ink
                );
                DrawColoredPolygon(
                    new Vector2[] { new(x - 20, 42), new(x + 20, 42), new(x + 15, 118), new(x - 15, 118) },
                    new Color("cfeaf4")
                );
                DrawColoredPolygon(
                    new Vector2[] { new(x - 17, 80), new(x + 17, 80), new(x + 15, 118), new(x - 15, 118) },
                    new Color("8fd3ea")
                );
                DrawLine(new(x - 12, 50), new(x - 10, 72), new Color("ffffff"), 3);
            }
        }
        else if (Icon == "flour")
        {
            // A full sack of white flour tied at the neck, a golden ear of wheat painted on it, a little heap spilt beside.
            Oval(new(90, 94), new(46, 40), ink);
            Oval(new(90, 94), new(42, 36), new("f4efe4"));
            Oval(new(90, 50), new(16, 10), ink);
            Oval(new(90, 50), new(13, 8), new("e6dfcf"));
            DrawLine(new(74, 60), new(106, 60), new Color("b8895f"), 6, true);
            DrawLine(new(90, 116), new(90, 84), new Color("c9b45a"), 3, true);
            for (int i = 0; i < 3; i++)
            {
                Oval(new(84, 104 - i * 9), new(4, 6), new("e0b23a"));
                Oval(new(96, 104 - i * 9), new(4, 6), new("e0b23a"));
            }
            Oval(new(90, 78), new(4, 6), new("f2c64a"));
            Oval(new(146, 126), new(20, 7), new("fbf8f2"));
        }
        else if (Icon == "wheat")
        {
            // Three stalks of wheat with golden ears, leaning a little apart.
            foreach (var (x, lean) in new[] { (60f, -10f), (90f, 0f), (120f, 10f) })
            {
                DrawLine(new(x, 128), new(x + lean, 56), new Color("c9b45a"), 6, true);
                for (int i = 0; i < 5; i++)
                {
                    float y = 56 - i * 11;
                    Oval(new(x + lean - 7, y + 4), new(6, 9), new("f2c64a"));
                    Oval(new(x + lean + 7, y + 4), new(6, 9), new("e0b23a"));
                }
                Oval(new(x + lean, 6), new(6, 10), new("f7dc7a"));
            }
        }
        else if (Icon == "bread")
        {
            // A round loaf with three cuts on top and a long loaf behind it.
            DrawLine(new(40, 96), new(140, 56), ink, 30, true);
            DrawLine(new(40, 96), new(140, 56), new Color("c27a2c"), 24, true);
            Oval(new(90, 96), new(54, 38), ink);
            Oval(new(90, 94), new(50, 34), new("c27a2c"));
            Oval(new(86, 88), new(40, 24), new("e8b062"));
            foreach (int x in new[] { 66, 88, 110 })
                DrawLine(new(x - 6, 76), new(x + 6, 100), new Color("b8732a"), 5, true);
        }
        else if (Icon == "binoculars")
        {
            // Two lenses seen from the front, joined by a bridge: "look round the valley".
            foreach (int x in new[] { 58, 122 })
            {
                DrawCircle(new Vector2(x, 80), 38, ink);
                DrawCircle(new Vector2(x, 80), 33, new Color("5e6f78"));
                DrawCircle(new Vector2(x, 80), 22, new Color("9ed5df"));
                DrawCircle(new Vector2(x - 8, 72), 7, new Color("e8f8fb"));
            }
            DrawRect(new Rect2(76, 70, 28, 18), ink);
            DrawRect(new Rect2(79, 73, 22, 12), new Color("5e6f78"));
        }
        else if (Icon == "fabric")
        {
            // Two rolls of cloth, one with a loose end.
            foreach (var (x, y) in new[] { (60, 96), (116, 96), (88, 58) })
            {
                DrawCircle(new Vector2(x, y), 26, ink);
                DrawCircle(new Vector2(x, y), 23, new Color("f1e9d6"));
                DrawArc(new Vector2(x, y), 15, 0, Mathf.Tau, 24, new Color("c9b896"), 2.5f, true);
                DrawCircle(new Vector2(x, y), 5, new Color("c9b896"));
            }
            DrawLine(new(60, 120), new(150, 126), new Color("f1e9d6"), 8, true);
        }
        else if (Icon == "capstone")
        {
            DrawRect(new Rect2(46, 100, 88, 24), ink);
            DrawRect(new Rect2(49, 103, 82, 18), new Color("9d9c95"));
            DrawColoredPolygon(new Vector2[] { new(58, 100), new(90, 22), new(122, 100) }, ink);
            DrawColoredPolygon(new Vector2[] { new(63, 98), new(90, 30), new(117, 98) }, new Color("b4b3ab"));
            DrawColoredPolygon(new Vector2[] { new(90, 30), new(117, 98), new(96, 98) }, new Color("9d9c95"));
        }
        else if (Icon == "flag")
        {
            DrawLine(new(60, 132), new(60, 18), new Color("a77a59"), 8, true);
            DrawColoredPolygon(new Vector2[] { new(64, 22), new(150, 40), new(64, 76) }, ink);
            DrawColoredPolygon(new Vector2[] { new(66, 27), new(142, 41), new(66, 70) }, new Color("e2563b"));
            DrawColoredPolygon(
                new Vector2[] { new(66, 27), new(142, 41), new(130, 46), new(66, 38) },
                new Color("fff1b8")
            );
        }
        else if (Icon.StartsWith("postcard-"))
        {
            DrawRect(new Rect2(9, 12, 162, 125), new Color("ffedce"));
            DrawRect(new Rect2(18, 21, 144, 107), new Color("aadbec"));
            if (Icon == "postcard-bridge")
            {
                DrawRect(new Rect2(18, 88, 144, 40), new Color("59bad4"));
                DrawArc(new(90, 120), 54, Mathf.Pi, Mathf.Tau, 32, new Color("8d6146"), 12, true);
                DrawLine(new(36, 95), new(144, 95), new Color("b88752"), 10, true);
                Oval(new(133, 114), new(13, 6), green);
                DrawCircle(new(133, 111), 4, new Color("e782ae"));
            }
            else
            {
                DrawColoredPolygon(
                    new Vector2[] { new(18, 85), new(67, 43), new(117, 80), new(162, 57), new(162, 128), new(18, 128) },
                    green
                );
                if (Icon == "postcard-garage")
                {
                    DrawRect(new Rect2(43, 62, 94, 60), new Color("76b69e"));
                    DrawRect(new Rect2(38, 56, 104, 10), new Color("e6af63"));
                    DrawRect(new Rect2(55, 74, 70, 48), ink);
                    DrawRect(new Rect2(63, 91, 54, 19), new Color("e96f61"));
                    DrawRect(new Rect2(74, 83, 32, 10), new Color("9ed5df"));
                    DrawCircle(new(72, 111), 6, Colors.White);
                    DrawCircle(new(108, 111), 6, Colors.White);
                }
                else if (Icon == "postcard-garden")
                {
                    DrawRect(new Rect2(32, 85, 116, 35), new Color("885f47"));
                    foreach (int x in new[] { 53, 90, 127 })
                    {
                        DrawLine(new(x, 100), new(x, 66), new Color("427846"), 5, true);
                        DrawCircle(new(x, 65), 11, new Color("ee87ab"));
                        DrawCircle(new(x, 65), 4, new Color("ffd86f"));
                    }
                }
                else if (Icon == "postcard-meadow")
                {
                    DrawCircle(new(131, 44), 13, new Color("ffd86f"));
                    DrawColoredPolygon(
                        new Vector2[] { new(48, 128), new(82, 85), new(99, 85), new(88, 128) },
                        new Color("dfbc83")
                    );
                    foreach (var flower in new Vector2[] { new(39, 102), new(111, 111), new(141, 91) })
                    {
                        DrawCircle(flower, 5, Colors.White);
                        DrawCircle(flower, 2, new Color("edb24c"));
                    }
                }
                else if (Icon == "postcard-farm")
                {
                    House(90, 66, 72, "ca6252", "704b47");
                    DrawRect(new Rect2(77, 92, 26, 31), new Color("f3cf97"));
                }
                else
                {
                    House(60, 82, 41, "ffd794", "a25c53");
                    House(112, 73, 49, "fff0cd", "9b6863");
                    DrawRect(new Rect2(54, 94, 12, 14), new Color("70adc7"));
                    DrawRect(new Rect2(106, 88, 12, 14), new Color("70adc7"));
                }
            }

            void House(float x, float y, float width, string wall, string roof)
            {
                DrawRect(new Rect2(x - width / 2, y, width, 123 - y), new Color(wall));
                DrawColoredPolygon(
                    new Vector2[] { new(x - width / 2 - 6, y), new(x, y - 24), new(x + width / 2 + 6, y) },
                    new Color(roof)
                );
            }
        }
        else if (Icon == "paint")
        {
            Oval(new(90, 77), new(55, 43), new("fff0ce"));
            foreach (
                var dot in new[]
                {
                    (new Vector2(58, 65), "ed4762"),
                    (new Vector2(83, 48), "ffce38"),
                    (new Vector2(113, 54), "46c6a0"),
                    (new Vector2(124, 82), "4976ed"),
                }
            )
                DrawCircle(dot.Item1, 11, new Color(dot.Item2));
            DrawLine(new(75, 107), new(111, 71), ink, 9, true);
            DrawCircle(new(72, 110), 9, new Color("ed4762"));
        }
        else if (Icon == "vehicle")
        {
            DrawStyleBox(
                new StyleBoxFlat
                {
                    BgColor = new("fff0ce"),
                    CornerRadiusTopLeft = 12,
                    CornerRadiusTopRight = 12,
                    CornerRadiusBottomLeft = 8,
                    CornerRadiusBottomRight = 8,
                },
                new Rect2(33, 67, 114, 38)
            );
            DrawColoredPolygon(
                new Vector2[] { new(52, 67), new(67, 39), new(111, 39), new(129, 67) },
                new Color("fff0ce")
            );
            DrawColoredPolygon(
                new Vector2[] { new(64, 63), new(74, 47), new(104, 47), new(115, 63) },
                new Color("4976ed")
            );
            foreach (float x in new[] { 59f, 123f })
            {
                DrawCircle(new(x, 106), 16, ink);
                DrawCircle(new(x, 106), 8, new Color("fff0ce"));
            }
        }
        else if (Icon == "upgrade")
        {
            DrawLine(new(56, 112), new(111, 57), Colors.White, 18, true);
            DrawCircle(new(55, 113), 14, Colors.White);
            DrawCircle(new(55, 113), 6, ink);
            DrawArc(new(117, 51), 24, -.35f, Mathf.Pi * 1.6f, 32, Colors.White, 13, true);
            DrawLine(new(143, 103), new(143, 130), new Color("ffce38"), 7, true);
            DrawLine(new(130, 116), new(156, 116), new Color("ffce38"), 7, true);
        }
        else if (Icon is "rain" or "storm")
        {
            Oval(new(90, 60), new(48, 22), Colors.White);
            Oval(new(82, 48), new(24, 23), Colors.White);
            if (Icon == "storm")
                DrawColoredPolygon(
                    new Vector2[]
                    {
                        new(94, 69),
                        new(121, 69),
                        new(100, 98),
                        new(118, 98),
                        new(77, 134),
                        new(88, 102),
                        new(69, 102),
                    },
                    new Color("ffdb70")
                );
            else
                for (int i = 0; i < 3; i++)
                    DrawLine(new(66 + i * 24, 93), new(56 + i * 24, 122), new Color("b7e8ff"), 8, true);
        }
        else if (Icon == "wind")
        {
            for (int i = 0; i < 3; i++)
            {
                float x = 18 * (i % 2);
                DrawLine(new(34 + x, 54 + i * 24), new(102 + x, 54 + i * 24), Colors.White, 8, true);
                DrawArc(new(102 + x, 43 + i * 24), 11, -Mathf.Pi / 2, Mathf.Pi / 2, 16, Colors.White, 8, true);
            }
        }
        else if (Icon == "clear")
        {
            DrawCircle(new(90, 75), 27, new Color("ffdb70"), true, -1, true);
            for (int i = 0; i < 8; i++)
            {
                var direction = Vector2.Right.Rotated(i * Mathf.Tau / 8);
                DrawLine(
                    new Vector2(90, 75) + direction * 36,
                    new Vector2(90, 75) + direction * 48,
                    new Color("ffdb70"),
                    7,
                    true
                );
            }
        }
        else if (Icon == "evening")
        {
            // A low sun sitting on the hills, with a few rays above it.
            var centre = new Vector2(90, 100);
            DrawCircle(centre, 28, new Color("ff9e66"), true, -1, true);
            for (int i = 0; i < 5; i++)
            {
                var direction = Vector2.Right.Rotated(Mathf.Pi + (i + 1) * Mathf.Pi / 6);
                DrawLine(centre + direction * 37, centre + direction * 49, new Color("ffc27a"), 7, true);
            }
            DrawRect(new Rect2(20, 100, 140, 36), ink);
            DrawLine(new(24, 100), new(156, 100), Colors.White, 6, true);
        }
        else if (Icon == "night")
        {
            // A sleepy moon with rosy cheeks and a couple of stars.
            var moon = new Color("fff0b8");
            DrawCircle(new(84, 82), 40, moon, true, -1, true);
            DrawArc(new(70, 78), 8, .15f, Mathf.Pi - .15f, 12, ink, 4, true);
            DrawArc(new(98, 78), 8, .15f, Mathf.Pi - .15f, 12, ink, 4, true);
            DrawArc(new(84, 90), 9, .3f, Mathf.Pi - .3f, 12, ink, 4, true);
            DrawCircle(new(62, 93), 6, new Color("f4a29b"), true, -1, true);
            DrawCircle(new(106, 93), 6, new Color("f4a29b"), true, -1, true);
            foreach (var (x, y, r) in new[] { (140f, 40f, 11f), (150f, 96f, 8f), (30f, 34f, 7f) })
                DrawColoredPolygon(
                    new Vector2[]
                    {
                        new(x, y - r),
                        new(x + r * .3f, y - r * .3f),
                        new(x + r, y),
                        new(x + r * .3f, y + r * .3f),
                        new(x, y + r),
                        new(x - r * .3f, y + r * .3f),
                        new(x - r, y),
                        new(x - r * .3f, y - r * .3f),
                    },
                    new Color("ffe27a")
                );
        }
        else if (Icon == "net")
        {
            // The hunter's net: a round hoop of rope with a grid of knots inside and a short handle.
            var rope = new Color("d8c49a");
            var wood = new Color("8a6440");
            DrawLine(new(58, 104), new(26, 140), wood, 10, true);
            DrawCircle(new(98, 66), 50, new Color(rope, .3f), true, -1, true);
            for (int i = -2; i <= 2; i++)
            {
                float offset = i * 19;
                float half = Mathf.Sqrt(50 * 50 - offset * offset);
                DrawLine(new(98 + offset, 66 - half), new(98 + offset, 66 + half), rope, 4, true);
                DrawLine(new(98 - half, 66 + offset), new(98 + half, 66 + offset), rope, 4, true);
            }
            DrawArc(new(98, 66), 50, 0, Mathf.Tau, 48, wood, 8, true);
        }
        else if (Icon is "wild-rabbit" or "wild-fox" or "wild-deer")
        {
            // A face of an animal of the hunt: long ears for the hare, pointed ones with dark tips for the fox and wide ones
            // for the roe deer, each with its own fur, muzzle and nose.
            bool fox = Icon == "wild-fox",
                deer = Icon == "wild-deer";
            var fur = new Color(
                fox ? "d58c4b"
                : deer ? "b68b60"
                : "c4af94"
            );
            var light = new Color(fox ? "fff5e6" : "efdfc2");
            var inner = new Color("eab4ad");
            foreach (int sign in new[] { -1, 1 })
            {
                if (fox)
                {
                    DrawColoredPolygon(
                        new Vector2[] { new(90 + sign * 18, 52), new(90 + sign * 54, 50), new(90 + sign * 50, 8) },
                        fur
                    );
                    DrawColoredPolygon(
                        new Vector2[] { new(90 + sign * 46, 22), new(90 + sign * 50, 8), new(90 + sign * 40, 14) },
                        ink
                    );
                }
                else if (deer)
                {
                    Oval(new(90 + sign * 62, 46), new(28, 13), fur);
                    Oval(new(90 + sign * 62, 46), new(18, 7), inner);
                }
                else
                {
                    Oval(new(90 + sign * 22, 30), new(13, 32), fur);
                    Oval(new(90 + sign * 22, 30), new(6, 23), inner);
                }
            }
            if (fox)
                DrawColoredPolygon(
                    new Vector2[] { new(34, 62), new(146, 62), new(130, 104), new(90, 138), new(50, 104) },
                    fur
                );
            else
                Oval(new(90, deer ? 84 : 90), new(deer ? 42 : 48, deer ? 50 : 42), fur);
            if (fox)
                foreach (int sign in new[] { -1, 1 })
                    DrawColoredPolygon(new Vector2[] { new(90, 96), new(90 + sign * 50, 98), new(90, 138) }, light);
            else
                Oval(new(90, deer ? 116 : 112), new(deer ? 24 : 26, deer ? 19 : 17), light);
            foreach (int sign in new[] { -1, 1 })
            {
                Oval(new(90 + sign * 20, deer ? 76 : 82), new(10, 12), Colors.White);
                Oval(new(90 + sign * 20, deer ? 78 : 84), new(5, 7), ink);
            }
            Oval(
                new(
                    90,
                    fox ? 126
                        : deer ? 110
                        : 104
                ),
                new(deer ? 10 : 8, 6),
                fox || deer ? ink : new Color("d97f86")
            );
            if (!fox && !deer)
                DrawRect(new Rect2(86, 114, 8, 8), Colors.White);
        }
        else if (Icon == "lantern")
        {
            // A little lantern with a handle, a warm glass and a bright flame inside.
            var iron = new Color("4a5a60");
            DrawArc(new(90, 40), 22, Mathf.Pi, Mathf.Tau, 20, iron, 6, true);
            DrawRect(new Rect2(64, 46, 52, 12), iron);
            DrawColoredPolygon(
                new Vector2[] { new(68, 58), new(112, 58), new(118, 112), new(62, 112) },
                new Color("ffe9a0")
            );
            DrawCircle(new(90, 88), 20, new Color("fff8d6"), true, -1, true);
            DrawCircle(new(90, 88), 10, new Color("ffd24a"), true, -1, true);
            foreach (float x in new[] { 79f, 101f })
                DrawLine(new(x, 58), new(x + (x - 90) * .25f, 112), iron, 4, true);
            DrawRect(new Rect2(56, 112, 68, 12), iron);
            foreach (var (x, y, r) in new[] { (30f, 52f, 6f), (150f, 70f, 8f), (140f, 28f, 5f) })
                DrawCircle(new(x, y), r, new Color("e9f56a", .8f), true, -1, true);
        }
        else if (Icon.StartsWith("trail-"))
        {
            // A bit of road with what the rear wheels leave on it: tyre marks, or flowers, hearts or stars.
            string trail = Icon[6..];
            var road = Enumerable
                .Range(0, 20)
                .Select(i => new Vector2(14 + i * 8, 76 + 20 * Mathf.Sin(i / 19f * Mathf.Tau)))
                .ToArray();
            DrawPolyline(road, new Color("ecd49a"), 62, true);
            if (trail == "tyres")
                foreach (float side in new[] { -11f, 11f })
                    for (int i = 0; i + 1 < road.Length; i += 2)
                        DrawLine(
                            road[i] + Vector2.Down * side,
                            road[i + 1] + Vector2.Down * side,
                            new Color("8a7757"),
                            6,
                            true
                        );
            else
                for (int i = 1; i < road.Length - 1; i += 3)
                {
                    var centre = road[i] + Vector2.Down * (i % 2 == 0 ? 11 : -11);
                    foreach (var piece in TrailShapes.Of(trail))
                        DrawColoredPolygon(piece.Outline.Select(p => centre + p * 15).ToArray(), piece.Color);
                }
        }
        else if (Icon == "quiz-letters")
        {
            DrawPolyline(new Vector2[] { new(53, 114), new(90, 34), new(127, 114) }, Colors.White, 12, true);
            DrawLine(new(69, 84), new(111, 84), new Color("ffdc83"), 9, true);
        }
        else if (Icon == "quiz-syllables")
        {
            DrawRect(new(22, 47, 63, 57), new Color("fff2ce"));
            DrawRect(new(94, 47, 63, 57), new Color("ffd687"));
            DrawString(ThemeDB.FallbackFont, new(30, 84), "KO", fontSize: 24, modulate: ink);
            DrawString(ThemeDB.FallbackFont, new(100, 84), "ČKA", fontSize: 23, modulate: ink);
        }
        else if (Icon == "quiz-reading")
        {
            DrawColoredPolygon(
                new Vector2[]
                {
                    new(34, 41),
                    new(62, 35),
                    new(90, 48),
                    new(118, 35),
                    new(146, 41),
                    new(146, 104),
                    new(118, 98),
                    new(90, 112),
                    new(62, 98),
                    new(34, 104),
                },
                new Color("fff2ce")
            );
            DrawLine(new(90, 49), new(90, 109), ink, 4, true);
            for (int line = 0; line < 3; line++)
            {
                float y = 52 + line * 14;
                DrawLine(new(48, y), new(75, y + 4), ink, 3, true);
                DrawLine(new(105, y + 4), new(132, y), ink, 3, true);
            }
        }
        else if (Icon == "quiz-math")
        {
            DrawString(ThemeDB.FallbackFont, new(22, 94), "1 + 2", fontSize: 52, modulate: Colors.White);
        }
        else if (Icon == "quiz-compare")
        {
            DrawString(ThemeDB.FallbackFont, new(30, 94), "3 < 5", fontSize: 52, modulate: Colors.White);
        }
        else if (Icon == "sad")
        {
            // A sad face after a wrong answer: a yellow circle, two eyes, a frown and a tear.
            DrawCircle(new(90, 76), 70, Colors.White);
            DrawCircle(new(90, 79), 62, new Color("c98a16"));
            DrawCircle(new(90, 75), 62, new Color("f8cf4c"));
            foreach (int sign in new[] { -1, 1 })
            {
                Oval(new(90 + sign * 22, 60), new(7, 10), ink);
                DrawLine(new(90 + sign * 34, 46), new(90 + sign * 12, 38), ink, 5, true);
            }
            DrawArc(new(90, 118), 26, Mathf.Pi * 1.2f, Mathf.Pi * 1.8f, 20, ink, 6, true);
            DrawColoredPolygon(
                new Vector2[] { new(108, 70), new(102, 84), new(108, 90), new(114, 84) },
                new Color("6fb7e3")
            );
        }
        else if (Icon == "check")
            DrawPolyline(new Vector2[] { new(50, 76), new(77, 103), new(132, 45) }, Colors.White, 12, true);
        else if (Icon == "delivery")
        {
            // Rozvoz: a parcel and an arrow that leaves it.
            DrawRect(new(14, 54, 58, 58), new Color("bf9565"));
            DrawRect(new(37, 54, 12, 58), new Color("fff0cf"));
            DrawRect(new(14, 76, 58, 12), new Color("fff0cf"));
            DrawLine(new(86, 83), new(150, 83), Colors.White, 11, true);
            DrawPolyline(new Vector2[] { new(126, 55), new(154, 83), new(126, 111) }, Colors.White, 11, true);
        }
        else if (Icon == "collection")
        {
            // Dovoz: a basket of apples and an arrow that comes back to it.
            DrawColoredPolygon(
                new Vector2[] { new(14, 82), new(76, 82), new(68, 120), new(22, 120) },
                new Color("c0873f")
            );
            DrawRect(new(10, 76, 70, 10), new Color("e0b06a"));
            foreach (int x in new[] { 30, 54 })
            {
                DrawCircle(new(x, 66), 14, new Color("ed776b"));
                DrawLine(new(x, 53), new(x + 3, 44), ink, 3, true);
            }
            DrawLine(new(166, 83), new(104, 83), Colors.White, 11, true);
            DrawPolyline(new Vector2[] { new(128, 55), new(100, 83), new(128, 111) }, Colors.White, 11, true);
        }
        else if (Icon == "album")
        {
            // An open album with two photographs: a sky with a hill, and a sun.
            DrawRect(new(26, 32, 128, 88), new Color("fff2ce"));
            DrawLine(new(90, 32), new(90, 120), ink, 4, true);
            DrawRect(new(36, 44, 44, 58), new Color("9ed5df"));
            DrawColoredPolygon(
                new Vector2[] { new(36, 102), new(36, 84), new(58, 70), new(80, 88), new(80, 102) },
                green
            );
            DrawRect(new(100, 44, 44, 58), new Color("ffe0a3"));
            DrawCircle(new(122, 66), 11, new Color("f3a02c"));
            DrawRect(new(100, 88, 44, 14), green);
        }
        else if (Icon == "barn")
        {
            DrawRect(new(36, 66, 108, 58), new Color("c0573e"));
            DrawColoredPolygon(new Vector2[] { new(26, 70), new(90, 26), new(154, 70) }, new Color("7d4a35"));
            DrawRect(new(70, 88, 40, 36), new Color("fff0cf"));
            DrawLine(new(70, 88), new(110, 124), new Color("c0573e"), 4, true);
            DrawLine(new(110, 88), new(70, 124), new Color("c0573e"), 4, true);
        }
        else if (Icon == "flower")
        {
            DrawLine(new(90, 74), new(90, 126), green, 7, true);
            Oval(new(110, 104), new(17, 8), green);
            for (int petal = 0; petal < 6; petal++)
            {
                float angle = petal * Mathf.Tau / 6;
                DrawCircle(
                    new Vector2(90, 54) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 24,
                    15,
                    new Color("ed7fa6")
                );
            }
            DrawCircle(new(90, 54), 13, new Color("f3c23c"));
        }
        else if (Icon == "gem")
        {
            DrawColoredPolygon(
                new Vector2[] { new(58, 38), new(122, 38), new(150, 70), new(90, 126), new(30, 70) },
                new Color("6ec7e8")
            );
            DrawColoredPolygon(
                new Vector2[] { new(58, 38), new(122, 38), new(112, 70), new(68, 70) },
                new Color("b5e8f7")
            );
            DrawPolyline(new Vector2[] { new(30, 70), new(150, 70) }, Colors.White, 4, true);
            DrawLine(new(68, 70), new(90, 126), Colors.White, 4, true);
            DrawLine(new(112, 70), new(90, 126), Colors.White, 4, true);
        }
        else if (Icon == "toy")
        {
            DrawCircle(new(90, 78), 46, new Color("ed776b"));
            DrawArc(new(90, 78), 46, Mathf.Pi * .15f, Mathf.Pi * .85f, 24, Colors.White, 14, true);
            DrawArc(new(90, 78), 46, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 24, new Color("f3c23c"), 14, true);
            DrawCircle(new(72, 58), 8, new Color(1, 1, 1, .55f));
        }
        else if (Icon == "cross")
        {
            DrawLine(new(58, 45), new(122, 105), Colors.White, 12, true);
            DrawLine(new(122, 45), new(58, 105), Colors.White, 12, true);
        }
        else if (Icon == "arrow")
        {
            DrawSetTransform(Size / 2, Direction, Vector2.One * scale);
            DrawColoredPolygon(
                new Vector2[]
                {
                    new(0, -56),
                    new(35, -12),
                    new(14, -12),
                    new(14, 46),
                    new(-14, 46),
                    new(-14, -12),
                    new(-35, -12),
                },
                new Color("edb941")
            );
        }
        else if (Icon == "play")
            DrawColoredPolygon(new Vector2[] { new(65, 37), new(65, 113), new(126, 75) }, Colors.White);
        else if (Icon == "pause")
        {
            DrawRect(new(60, 40, 20, 70), Colors.White);
            DrawRect(new(100, 40, 20, 70), Colors.White);
        }
        else if (Icon == "coin")
        {
            DrawCircle(new(90, 79), 54, new Color("c98a16"));
            DrawCircle(new(90, 75), 54, new Color("f3c23c"));
            DrawArc(new(90, 75), 40, 0, Mathf.Tau, 48, new Color("d99a1e"), 7, true);
            DrawArc(new(90, 75), 46, Mathf.Pi * 1.1f, Mathf.Pi * 1.45f, 12, new Color("fff3bf"), 6, true);
            Oval(new(90, 75), new(11, 21), new Color("d99a1e"));
            Oval(new(87, 72), new(6, 14), new Color("ffe58a"));
        }
        else if (Icon == "search")
        {
            DrawArc(new(78, 63), 32, 0, Mathf.Tau, 40, ink, 9, true);
            DrawLine(new(101, 87), new(137, 124), ink, 12, true);
        }
        else if (Art.Picture(Icon) is { } picture)
            DrawTextureRect(picture, new Rect2(0, 0, 180, 150), false);
    }
}
