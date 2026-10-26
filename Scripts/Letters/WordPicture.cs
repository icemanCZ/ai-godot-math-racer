using Godot;
using System;
using System.Linq;

namespace MathRacer;

/// <summary>Original vector illustrations, drawn independently of fonts and external assets.</summary>
public partial class WordPicture : Control
{
    public string PictureId { get; init; } = "";
    private static readonly Color Ink = new("3c5960");
    public override void _Ready() => Resized += QueueRedraw;
    private void Shape(string color, params Vector2[] points)
    {
        DrawColoredPolygon(points, new Color(color));
        DrawPolyline(points.Append(points[0]).ToArray(), Ink, 3, true);
    }
    private void Oval(float x, float y, float rx, float ry, string color)
        => Shape(color, Enumerable.Range(0, 48).Select(i => new Vector2(x + Mathf.Cos(i * Mathf.Tau / 48) * rx, y + Mathf.Sin(i * Mathf.Tau / 48) * ry)).ToArray());
    private void Line(float x1, float y1, float x2, float y2, float width = 4)
        => DrawLine(new(x1, y1), new(x2, y2), Ink, width, true);
    public override void _Draw()
    {
        float scale = Math.Min(Size.X / 220, Size.Y / 180);
        DrawSetTransform((Size - new Vector2(220, 180) * scale) / 2, 0, Vector2.One * scale);
        switch (PictureId)
        {
            case "car":
                Shape("ee786d", new(32, 113), new(40, 87), new(69, 84), new(89, 56), new(139, 56), new(163, 87), new(187, 95), new(187, 126), new(32, 126));
                Shape("bde9f4", new(81, 83), new(96, 64), new(113, 64), new(113, 83));
                Shape("bde9f4", new(121, 64), new(136, 64), new(151, 83), new(121, 83));
                Oval(66, 127, 20, 20, "3c5960"); Oval(156, 127, 20, 20, "3c5960");
                Oval(66, 127, 9, 9, "fff5df"); Oval(156, 127, 9, 9, "fff5df");
                Oval(181, 104, 6, 7, "ffe47c"); break;
            case "banana":
                Shape("ffda50", new(48, 48), new(56, 83), new(74, 109), new(102, 120), new(132, 118), new(160, 99), new(180, 69), new(168, 113), new(145, 141), new(111, 151), new(78, 141), new(54, 116), new(41, 81), new(40, 49));
                Line(48, 49, 45, 37, 7); Line(178, 69, 182, 60, 6);
                DrawPolyline(new Vector2[] { new(55, 90), new(70, 120), new(101, 137), new(132, 133), new(158, 113) }, new Color("d4a832"), 3, true); break;
            case "lemon":
                Shape("ffe35d", new(40, 97), new(52, 86), new(60, 66), new(82, 51), new(111, 48), new(144, 57), new(165, 78), new(180, 84), new(177, 95), new(164, 104), new(151, 124), new(125, 134), new(94, 135), new(67, 123), new(53, 104));
                Oval(137, 43, 23, 10, "78b770");
                DrawArc(new(109, 93), 31, 2.8f, 4.2f, 20, new Color("fff4b0"), 5, true); break;
            case "house":
                Shape("ffe2a2", new(57, 79), new(163, 79), new(163, 149), new(57, 149));
                Shape("ed8270", new(42, 80), new(110, 30), new(179, 80));
                Shape("9c785f", new(99, 105), new(126, 105), new(126, 149), new(99, 149));
                Shape("b7e7ee", new(68, 98), new(88, 98), new(88, 120), new(68, 120));
                Shape("b7e7ee", new(137, 98), new(153, 98), new(153, 120), new(137, 120));
                Oval(119, 130, 2, 2, "ffe377"); break;
            case "pear":
                Shape("bfd96b", new(110, 45), new(97, 49), new(89, 65), new(83, 85), new(64, 107), new(60, 126), new(71, 145), new(95, 153), new(125, 152), new(149, 140), new(157, 120), new(149, 101), new(133, 81), new(126, 58), new(117, 47));
                Line(112, 48, 118, 27, 6); Oval(137, 34, 19, 9, "73b374");
                Oval(82, 124, 6, 11, "e4edac"); break;
            case "apple":
                Oval(83, 101, 39, 47, "ed776b"); Oval(132, 101, 39, 47, "ed776b");
                // Cover the shared outline to make a single apple silhouette.
                DrawRect(new Rect2(82, 58, 49, 84), new Color("ed776b"));
                Line(109, 62, 115, 34, 7); Oval(135, 40, 22, 10, "7db77a");
                DrawArc(new(86, 99), 25, 3.3f, 4.4f, 16, new Color("ffd0bb"), 6, true); break;
            case "key":
                Shape("f4cd56", new(92, 86), new(171, 86), new(171, 102), new(161, 102), new(161, 118), new(145, 118), new(145, 102), new(92, 102));
                Oval(69, 94, 33, 33, "f4cd56"); Oval(69, 94, 15, 15, "ffffff"); break;
            case "boat":
                Shape("bd8665", new(40, 119), new(182, 119), new(160, 147), new(66, 147));
                Line(109, 32, 109, 120, 5);
                Shape("fff0c7", new(103, 41), new(103, 110), new(49, 110));
                Shape("f29380", new(116, 49), new(166, 108), new(116, 108));
                DrawPolyline(new Vector2[] { new(39, 158), new(61, 162), new(87, 157), new(112, 162), new(138, 157), new(164, 162), new(183, 157) }, new Color("68b8d6"), 5, true); break;
            case "ball":
                Oval(110, 95, 59, 59, "ffda69");
                Shape("7bbbd2", new(110, 36), new(120, 65), new(98, 79), new(73, 63), new(79, 45));
                Shape("ed8278", new(168, 91), new(140, 83), new(127, 106), new(141, 130), new(160, 125));
                Shape("8fbb80", new(78, 144), new(98, 125), new(82, 101), new(53, 103), new(59, 126)); break;
            case "fish":
                Shape("eea56e", new(151, 92), new(187, 62), new(187, 126));
                Shape("eea56e", new(91, 68), new(114, 43), new(133, 71));
                Oval(105, 94, 57, 34, "f6bc78"); Oval(72, 85, 7, 7, "ffffff");
                DrawCircle(new(71, 85), 3, Ink, true, -1, true);
                Shape("e99069", new(112, 92), new(130, 104), new(110, 115));
                DrawArc(new(78, 94), 20, -.7f, .8f, 16, Ink, 3, true); break;
            case "butterfly":
                Oval(76, 73, 34, 39, "b9a0dc"); Oval(144, 73, 34, 39, "b9a0dc");
                Oval(80, 123, 28, 27, "f0a0be"); Oval(140, 123, 28, 27, "f0a0be");
                Oval(73, 70, 12, 15, "ffe299"); Oval(147, 70, 12, 15, "ffe299");
                Oval(110, 99, 9, 45, "796589");
                Line(107, 58, 95, 37, 3); Line(113, 58, 125, 37, 3); break;
            case "cat":
                Shape("edb67a", new(56, 86), new(52, 38), new(88, 58), new(132, 58), new(168, 38), new(164, 86));
                Oval(110, 103, 57, 45, "edb67a");
                Shape("e8a2a8", new(61, 48), new(79, 61), new(64, 70));
                Shape("e8a2a8", new(159, 48), new(141, 61), new(156, 70));
                Oval(87, 98, 6, 9, "3c5960"); Oval(133, 98, 6, 9, "3c5960");
                Shape("c78180", new(101, 116), new(119, 116), new(110, 124));
                Line(110, 124, 110, 131, 2); Line(110, 131, 99, 135, 2); Line(110, 131, 121, 135, 2);
                Line(75, 118, 40, 111, 2); Line(74, 126, 38, 129, 2);
                Line(145, 118, 180, 111, 2); Line(146, 126, 182, 129, 2); break;
            case "dog":
                Oval(110, 95, 49, 52, "dca879");
                Oval(61, 87, 21, 45, "92674f"); Oval(159, 87, 21, 45, "92674f");
                Oval(91, 88, 5, 7, "3c5960"); Oval(129, 88, 5, 7, "3c5960");
                Oval(110, 121, 30, 23, "fff0d4"); Oval(110, 110, 12, 8, "3c5960");
                Oval(110, 141, 9, 14, "ed9f9f"); Line(110, 132, 110, 145, 2); break;
            case "elephant":
                Oval(62, 88, 29, 45, "a9b8d0"); Oval(158, 88, 29, 45, "a9b8d0");
                Oval(110, 83, 42, 44, "bfcce0");
                Shape("bfcce0", new(96, 105), new(124, 105), new(124, 139), new(143, 139), new(151, 131), new(156, 139), new(145, 157), new(113, 157), new(98, 147));
                Oval(92, 85, 5, 6, "3c5960"); Oval(128, 85, 5, 6, "3c5960");
                Line(101, 123, 119, 123, 2); Line(102, 133, 119, 133, 2); break;
            case "giraffe":
                Shape("f3cf82", new(88, 151), new(93, 68), new(125, 68), new(135, 151));
                Oval(110, 63, 29, 32, "f3cf82");
                Oval(77, 51, 16, 9, "f3cf82"); Oval(143, 51, 16, 9, "f3cf82");
                Line(96, 36, 94, 22, 5); Line(124, 36, 126, 22, 5);
                Oval(94, 21, 5, 5, "ad8059"); Oval(126, 21, 5, 5, "ad8059");
                Oval(98, 59, 4, 5, "3c5960"); Oval(122, 59, 4, 5, "3c5960");
                Oval(110, 79, 23, 12, "e6b875");
                Oval(105, 108, 8, 10, "b78758"); Oval(122, 136, 8, 10, "b78758");
                Oval(94, 143, 5, 7, "b78758"); break;
            case "zebra":
                Oval(84, 44, 11, 24, "fffaf0"); Oval(136, 44, 11, 24, "fffaf0");
                Shape("525868", new(92, 58), new(99, 30), new(112, 26), new(124, 58));
                Oval(110, 94, 37, 51, "fffaf0");
                Shape("525868", new(79, 68), new(101, 80), new(80, 81));
                Shape("525868", new(141, 68), new(119, 80), new(140, 81));
                Shape("525868", new(75, 92), new(97, 101), new(77, 105));
                Shape("525868", new(145, 92), new(123, 101), new(143, 105));
                Line(110, 47, 110, 76, 7);
                Oval(96, 86, 4, 6, "3c5960"); Oval(124, 86, 4, 6, "3c5960");
                Oval(110, 133, 32, 20, "b6b7bd");
                Oval(97, 133, 3, 4, "3c5960"); Oval(123, 133, 3, 4, "3c5960"); break;
            case "turtle":
                Oval(69, 135, 13, 13, "a5ca85"); Oval(139, 135, 13, 13, "a5ca85");
                Shape("a5ca85", new(57, 113), new(32, 116), new(59, 127));
                Oval(175, 105, 22, 19, "a5ca85");
                Oval(108, 104, 55, 35, "7baa77");
                Shape("b8cf8a", new(91, 80), new(121, 80), new(137, 104), new(120, 126), new(91, 126), new(76, 104));
                Line(76, 104, 56, 104, 2); Line(137, 104, 160, 104, 2);
                Oval(182, 100, 3, 4, "3c5960"); break;
            case "duck":
                Shape("edaa57", new(120, 145), new(120, 155), new(101, 155), new(105, 147));
                Oval(103, 113, 55, 34, "f9d76f");
                Shape("f9d76f", new(58, 105), new(34, 88), new(40, 126), new(65, 136));
                Oval(145, 73, 25, 27, "f9d76f");
                Shape("edaa57", new(165, 73), new(193, 80), new(166, 89));
                Oval(151, 67, 4, 5, "3c5960");
                DrawArc(new(104, 109), 26, .2f, 2.9f, 24, new Color("d8a94f"), 3, true); break;
            case "umbrella":
                Line(111, 57, 111, 143, 5);
                DrawArc(new(99, 143), 12, 0, Mathf.Pi, 20, Ink, 5, true);
                Shape("79bfd1", new(39, 94), new(47, 71), new(62, 51), new(85, 38), new(110, 34), new(137, 39), new(158, 53), new(175, 75), new(181, 94), new(156, 84), new(134, 94), new(110, 84), new(86, 94), new(63, 84));
                DrawPolyline(new Vector2[] { new(110, 34), new(94, 55), new(86, 94) }, Ink, 2, true);
                DrawPolyline(new Vector2[] { new(110, 34), new(126, 55), new(134, 94) }, Ink, 2, true);
                Line(110, 34, 110, 25, 4); break;
            case "tree":
                Shape("aa805f", new(96, 82), new(125, 82), new(130, 154), new(90, 154));
                Oval(79, 82, 33, 31, "91bb78"); Oval(139, 78, 35, 33, "91bb78");
                Oval(110, 51, 37, 32, "a6ca85"); Oval(110, 91, 36, 26, "91bb78");
                Line(110, 124, 110, 148, 3); break;
            case "flower":
                Line(110, 82, 110, 154, 6);
                Oval(87, 126, 24, 10, "8ab879"); Oval(131, 139, 23, 10, "8ab879");
                for (int i = 0; i < 6; i++)
                {
                    var petal = new Vector2(110, 74) + Vector2.FromAngle(i * Mathf.Tau / 6) * 29;
                    Oval(petal.X, petal.Y, 20, 20, "eda1ba");
                }
                Oval(110, 74, 19, 19, "ffda6e"); break;
            case "strawberry":
                Shape("ed7d7b", new(67, 58), new(91, 52), new(110, 59), new(131, 52), new(156, 60), new(163, 81), new(153, 108), new(132, 135), new(110, 153), new(87, 136), new(68, 108), new(58, 81));
                Shape("8ab97b", new(110, 58), new(80, 38), new(87, 60), new(65, 63), new(96, 76), new(110, 64), new(126, 76), new(153, 63), new(131, 58), new(138, 38));
                foreach (var seed in new Vector2[] { new(83, 87), new(110, 86), new(139, 87), new(92, 108), new(124, 109), new(109, 131) })
                    DrawLine(seed, seed + new Vector2(0, 5), new Color("ffe3a2"), 4, true);
                break;
            case "carrot":
                Shape("eea057", new(69, 80), new(86, 60), new(122, 66), new(139, 86), new(66, 157), new(59, 155));
                Shape("8bb87a", new(101, 66), new(82, 32), new(100, 42), new(113, 24), new(116, 46), new(143, 35), new(122, 70));
                Line(78, 94, 96, 100, 3); Line(94, 120, 106, 124, 3); break;
            case "icecream":
                Shape("ddb17c", new(78, 83), new(143, 83), new(111, 159));
                Line(90, 108, 126, 126, 2); Line(98, 131, 130, 106, 2);
                Oval(88, 80, 25, 23, "edabc0"); Oval(133, 80, 25, 23, "edabc0");
                Oval(110, 55, 30, 29, "fff0d3"); Oval(115, 31, 8, 8, "dc7274"); break;
            case "clock":
                Oval(110, 95, 60, 60, "8dbccf"); Oval(110, 95, 50, 50, "fffaf0");
                for (int i = 0; i < 12; i++)
                {
                    var direction = Vector2.FromAngle(i * Mathf.Tau / 12);
                    DrawLine(new Vector2(110, 95) + direction * 39, new Vector2(110, 95) + direction * 44, Ink, 3, true);
                }
                Line(110, 95, 110, 62, 5); Line(110, 95, 138, 110, 5); Oval(110, 95, 4, 4, "ee9d7b"); break;
            case "book":
                Shape("8db9cc", new(37, 57), new(102, 63), new(110, 70), new(118, 63), new(183, 57), new(183, 139), new(118, 145), new(110, 151), new(102, 145), new(37, 139));
                Shape("fff5de", new(44, 48), new(99, 54), new(110, 64), new(110, 139), new(99, 131), new(44, 126));
                Shape("fff5de", new(110, 64), new(121, 54), new(176, 48), new(176, 126), new(121, 131), new(110, 139));
                Line(55, 74, 96, 79, 2); Line(55, 91, 96, 96, 2); Line(55, 108, 96, 113, 2);
                Line(124, 79, 165, 74, 2); Line(124, 96, 165, 91, 2); Line(124, 113, 165, 108, 2); break;
            case "pencil":
                Shape("ffda6e", new(57, 126), new(143, 40), new(168, 65), new(82, 151));
                Shape("e8a2b5", new(143, 40), new(156, 27), new(181, 52), new(168, 65));
                Shape("f2d1a1", new(57, 126), new(82, 151), new(42, 166));
                Shape("525868", new(46, 152), new(56, 162), new(42, 166));
                Line(68, 138, 155, 52, 3); break;
            case "chocolate":
                Shape("bf8d73", new(48, 42), new(172, 42), new(172, 146), new(48, 146));
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 4; col++)
                    {
                        int x = 54 + col * 29; int y = 48 + row * 31;
                        Shape("94674f", new(x, y), new(x + 24, y), new(x + 24, y + 26), new(x, y + 26));
                        DrawLine(new(x + 4, y + 5), new(x + 20, y + 5), new Color("c09478"), 3, true);
                    }
                Shape("e7a5b6", new(42, 109), new(103, 121), new(178, 108), new(178, 153), new(42, 153)); break;
            case "train":
                Shape("83b6cb", new(43, 81), new(87, 81), new(87, 124), new(43, 124));
                Shape("ed8e7d", new(96, 88), new(133, 88), new(133, 55), new(173, 55), new(173, 124), new(96, 124));
                Shape("fff0d3", new(141, 65), new(164, 65), new(164, 84), new(141, 84));
                Shape("fff0d3", new(50, 89), new(77, 89), new(77, 106), new(50, 106));
                Shape("525868", new(100, 87), new(100, 69), new(112, 69), new(112, 87));
                Line(133, 52, 178, 52, 6); Line(40, 146, 183, 146, 4);
                Oval(57, 129, 12, 12, "525868"); Oval(80, 129, 12, 12, "525868");
                Oval(112, 129, 12, 12, "525868"); Oval(155, 127, 16, 16, "525868");
                Oval(102, 53, 9, 7, "ccd6df"); Oval(89, 37, 13, 9, "ccd6df"); break;
            case "rocket":
                Shape("ed8e7d", new(87, 102), new(64, 127), new(64, 148), new(92, 136));
                Shape("ed8e7d", new(133, 102), new(156, 127), new(156, 148), new(128, 136));
                Shape("ffcf65", new(96, 130), new(110, 166), new(124, 130));
                Shape("e1e9eb", new(88, 133), new(85, 86), new(92, 54), new(110, 27), new(128, 54), new(135, 86), new(132, 133));
                Shape("ed8e7d", new(92, 54), new(110, 27), new(128, 54));
                Oval(110, 88, 16, 18, "8dbed2"); Oval(110, 88, 9, 11, "c6e9ee"); break;
            case "sun":
                for (int i = 0; i < 12; i++)
                {
                    var direction = Vector2.FromAngle(i * Mathf.Tau / 12);
                    DrawLine(new Vector2(110, 91) + direction * 54, new Vector2(110, 91) + direction * 70, new Color("efb545"), 7, true);
                }
                Oval(110, 91, 43, 43, "ffdb66");
                DrawCircle(new(96, 85), 3, Ink); DrawCircle(new(124, 85), 3, Ink);
                DrawArc(new(110, 95), 15, .25f, 2.9f, 20, Ink, 3, true); break;
            case "snail":
                Shape("a7c78b", new(41, 127), new(154, 116), new(169, 81), new(186, 87), new(186, 126), new(171, 145), new(69, 145));
                Oval(104, 98, 43, 40, "d4a1cf");
                var spiral = Enumerable.Range(0, 70).Select(i => {
                    float angle = i * .15f; float radius = 2 + i * .4f;
                    return new Vector2(104 + Mathf.Cos(angle) * radius, 98 + Mathf.Sin(angle) * radius);
                }).ToArray();
                DrawPolyline(spiral, Ink, 3, true);
                Line(171, 89, 163, 66, 3); Line(182, 89, 190, 67, 3);
                Oval(163, 65, 5, 6, "ffffff"); Oval(190, 65, 5, 6, "ffffff");
                DrawCircle(new(163, 65), 2, Ink); DrawCircle(new(190, 65), 2, Ink); break;
        }
    }
}

