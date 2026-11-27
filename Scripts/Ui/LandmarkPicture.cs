using System.Linq;
using Godot;
using MathRacer.Buildings;

namespace MathRacer.Ui;

/// <summary>
/// A picture of a wonder of the land beyond the old valley (<c>docs/EXPLORER_PLAN.md</c>) for children who cannot read,
/// the same on the owl's map, in her panel and in the corner panel: the oak with its door, the rock arch, the ruined
/// tower, the toadstool, the standing stones, the dinosaur's bones, the well and the scarecrow. <see cref="Faint"/>
/// draws it as a pale shadow.
/// </summary>
public partial class LandmarkPicture : Control
{
    public int Kind { get; init; }

    public bool Faint { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        float size = Mathf.Min(Size.X, Size.Y);
        Paint(this, Kind, Size / 2, size, Faint ? .35f : 1);
    }

    private static void Oval(CanvasItem canvas, Vector2 p, Vector2 radius, Color color) =>
        canvas.DrawColoredPolygon(
            Enumerable
                .Range(0, 28)
                .Select(i =>
                    p + new Vector2(Mathf.Cos(i * Mathf.Tau / 28) * radius.X, Mathf.Sin(i * Mathf.Tau / 28) * radius.Y)
                )
                .ToArray(),
            color
        );

    /// <summary>
    /// Draws the wonder of <paramref name="kind"/> on <paramref name="canvas"/> in a square of <paramref name="size"/>
    /// round <paramref name="center"/>, with <paramref name="alpha"/>.
    /// </summary>
    internal static void Paint(CanvasItem canvas, int kind, Vector2 center, float size, float alpha = 1)
    {
        // Drawn in a box of 100 by 100 round its middle.
        float s = size / 100;
        Vector2 P(float x, float y) => center + new Vector2(x - 50, y - 50) * s;
        Color C(string hex) => new Color(hex) with { A = alpha };
        void Box(float x, float y, float w, float h, string color) =>
            canvas.DrawRect(new Rect2(P(x, y), new Vector2(w, h) * s), C(color));
        void Ball(float x, float y, float rx, float ry, string color) =>
            Oval(canvas, P(x, y), new Vector2(rx, ry) * s, C(color));
        void Line(float x0, float y0, float x1, float y1, float width, string color) =>
            canvas.DrawLine(P(x0, y0), P(x1, y1), C(color), width * s, true);
        switch (kind)
        {
            case ExplorerPlans.Oak:
                Box(42, 52, 16, 40, "6b4a2e");
                Ball(50, 32, 30, 22, "4f8a3a");
                Ball(30, 42, 16, 13, "5a9a42");
                Ball(70, 40, 17, 14, "467f33");
                Ball(52, 20, 18, 12, "5fa548");
                Box(46, 76, 8, 16, "c0504d");
                Ball(50, 78, 3, 3, "ffe9a6");
                break;
            case ExplorerPlans.Arch:
                Ball(24, 70, 13, 22, "8d8a83");
                Ball(76, 70, 13, 22, "8d8a83");
                canvas.DrawArc(P(50, 52), 28 * s, Mathf.Pi, Mathf.Tau, 24, C("a29e95"), 16 * s, true);
                Ball(46, 22, 16, 5, "6f9a4a");
                Box(10, 90, 80, 4, "8fbf6a");
                break;
            case ExplorerPlans.Ruin:
                Box(30, 26, 40, 66, "a7a196");
                for (int i = 0; i < 4; i++)
                    Box(30 + i * 11, 18, 7, 9, "9c968b");
                Box(45, 40, 10, 16, "2d2a26");
                Box(43, 72, 14, 20, "4a3424");
                Line(62, 18, 62, 4, 2.5f, "5b4636");
                canvas.DrawColoredPolygon(new[] { P(62, 4), P(76, 8), P(62, 12) }, C("c0392b"));
                Box(8, 74, 20, 18, "a39d92");
                break;
            case ExplorerPlans.Mushroom:
                Box(42, 50, 16, 42, "f4eee0");
                canvas.DrawColoredPolygon(
                    Enumerable
                        .Range(0, 15)
                        .Select(i => P(50 - Mathf.Cos(i * Mathf.Pi / 14) * 42, 54 - Mathf.Sin(i * Mathf.Pi / 14) * 40))
                        .ToArray(),
                    C("d23b2f")
                );
                foreach (
                    var (x, y, r) in new[]
                    {
                        (36f, 34f, 5f),
                        (56f, 24f, 6f),
                        (70f, 42f, 4.5f),
                        (24f, 48f, 3.5f),
                        (48f, 44f, 4f),
                    }
                )
                    Ball(x, y, r, r * .8f, "fff8ec");
                break;
            case ExplorerPlans.Stones:
                Box(10, 42, 16, 50, "8f8c86");
                Box(42, 36, 16, 56, "9e9b94");
                Box(74, 44, 16, 48, "8f8c86");
                Box(36, 28, 28, 10, "99968f");
                Box(6, 90, 88, 4, "8fbf6a");
                break;
            case ExplorerPlans.Dinosaur:
                canvas.DrawPolyline(
                    new[] { P(6, 62), P(24, 54), P(44, 50), P(60, 46), P(70, 34), P(74, 20), P(82, 14) },
                    C("f2ead6"),
                    6 * s,
                    true
                );
                Ball(88, 14, 9, 5, "f2ead6");
                Ball(88, 13, 1.6f, 1.6f, "3b3433");
                for (int i = 0; i < 5; i++)
                    Line(34 + i * 6, 51, 36 + i * 6, 66 - Mathf.Abs(i - 2) * 2, 2.5f, "ddd2b8");
                foreach (float x in new[] { 30f, 38f, 54f, 62f })
                    Line(x, 54, x + 1, 90, 4, "f2ead6");
                break;
            case ExplorerPlans.Well:
                Box(30, 62, 40, 28, "9a958c");
                Box(28, 60, 44, 5, "b2ada3");
                Box(30, 30, 4, 32, "7a5236");
                Box(66, 30, 4, 32, "7a5236");
                canvas.DrawColoredPolygon(new[] { P(20, 34), P(50, 12), P(80, 34) }, C("b5543c"));
                Line(50, 34, 50, 50, 1.5f, "d9c79f");
                Box(45, 50, 10, 8, "8a6a45");
                break;
            default:
                Line(50, 30, 50, 92, 4, "7a5236");
                Line(18, 46, 82, 46, 4, "7a5236");
                Box(36, 40, 28, 30, "4f7fb8");
                Box(40, 46, 8, 8, "e8b938");
                Ball(50, 30, 10, 10, "f0dfb0");
                canvas.DrawColoredPolygon(new[] { P(32, 22), P(50, 6), P(68, 22) }, C("e0b85a"));
                Box(28, 20, 44, 4, "e0b85a");
                Ball(22, 86, 10, 7, "f39a33");
                Ball(78, 86, 10, 7, "e8862a");
                Ball(18, 42, 5, 4, "2b2b30");
                break;
        }
    }
}
