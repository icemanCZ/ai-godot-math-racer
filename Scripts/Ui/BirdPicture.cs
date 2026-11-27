using System.Linq;
using Godot;
using MathRacer.Buildings;

namespace MathRacer.Ui;

/// <summary>
/// A picture of a bird of the island's atlas (<c>docs/FERRY_PLAN.md</c>) for children who cannot read: the swan, the
/// stork, the cormorant, the tern, the plover, the grebe, the pelican and the flamingo; or, with <see cref="Punt"/> as
/// the kind, the heron's punt. <see cref="Faint"/> draws it as a pale shadow, for a bird not yet seen.
/// </summary>
public partial class BirdPicture : Control
{
    /// <summary>The kind that draws the punt instead of a bird.</summary>
    public const int Punt = -1;

    public int Kind { get; init; }

    public bool Faint { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw() => Paint(this, new Rect2(Vector2.Zero, Size), Kind, Faint);

    /// <summary>Draws the picture of <paramref name="kind"/> into <paramref name="area"/> of any canvas item.</summary>
    internal static void Paint(CanvasItem canvas, Rect2 area, int kind, bool faint)
    {
        float s = Mathf.Min(area.Size.X, area.Size.Y) / 100;
        var center = area.Position + area.Size / 2;
        float alpha = faint ? .3f : 1;
        Vector2 P(float x, float y) => center + new Vector2(x - 50, y - 50) * s;
        Color C(string hex) => new Color(hex) with { A = alpha };
        void Oval(float x, float y, float rx, float ry, string color, float turn = 0) =>
            canvas.DrawColoredPolygon(
                Enumerable
                    .Range(0, 24)
                    .Select(i =>
                    {
                        float a = i * Mathf.Tau / 24;
                        var v = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry).Rotated(turn);
                        return P(x + v.X, y + v.Y);
                    })
                    .ToArray(),
                C(color)
            );
        void Line(float x0, float y0, float x1, float y1, float width, string color) =>
            canvas.DrawLine(P(x0, y0), P(x1, y1), C(color), width * s, true);
        void Tri(float x0, float y0, float x1, float y1, float x2, float y2, string color) =>
            canvas.DrawColoredPolygon(new[] { P(x0, y0), P(x1, y1), P(x2, y2) }, C(color));
        void Eye(float x, float y) => Oval(x, y, 2.2f, 2.2f, "1d1d1d");
        void Water() => Oval(50, 84, 44, 6, "8cc6e6");
        switch (kind)
        {
            case Punt:
                Oval(50, 80, 46, 7, "8cc6e6");
                canvas.DrawColoredPolygon(new[] { P(8, 62), P(92, 62), P(82, 78), P(18, 78) }, C("a87b4c"));
                Line(12, 64, 88, 64, 3, "c0453b");
                Line(70, 22, 58, 80, 2.5f, "d9b886");
                break;
            case FerryPlans.Swan:
                Water();
                Oval(52, 70, 30, 13, "fbfbf8");
                Line(30, 66, 26, 40, 7, "fbfbf8");
                Line(26, 40, 32, 28, 7, "fbfbf8");
                Oval(34, 28, 7, 6, "fbfbf8");
                Tri(39, 27, 48, 31, 39, 31, "f08a2c");
                Eye(33, 26);
                break;
            case FerryPlans.Stork:
                Line(46, 64, 42, 90, 2.5f, "d83b2c");
                Line(56, 64, 60, 90, 2.5f, "d83b2c");
                Oval(52, 56, 24, 12, "fbfbf8");
                Oval(58, 54, 16, 8, "222222", .2f);
                Line(34, 52, 28, 28, 5, "fbfbf8");
                Oval(28, 26, 6, 6, "fbfbf8");
                Tri(32, 24, 50, 30, 32, 28, "d83b2c");
                Eye(27, 24);
                break;
            case FerryPlans.Cormorant:
                Line(46, 74, 44, 88, 2.5f, "222222");
                Line(54, 74, 56, 88, 2.5f, "222222");
                // Its wings held out to dry.
                Tri(44, 50, 10, 34, 18, 62, "1c2022");
                Tri(56, 50, 90, 34, 82, 62, "1c2022");
                Oval(50, 58, 12, 20, "262a2c");
                Oval(50, 32, 8, 8, "262a2c");
                Oval(52, 35, 4, 3, "f3d36b");
                Tri(56, 31, 68, 34, 56, 35, "4b4b4b");
                Eye(48, 30);
                break;
            case FerryPlans.Tern:
                Line(48, 70, 46, 84, 2, "e0502e");
                Line(56, 70, 58, 84, 2, "e0502e");
                Oval(52, 62, 24, 10, "fbfbf8");
                Tri(30, 58, 54, 52, 74, 60, "b9c4cc");
                Tri(76, 60, 94, 52, 90, 66, "fbfbf8");
                Oval(32, 52, 9, 8, "fbfbf8");
                Oval(32, 47, 9, 4, "1d1d1d");
                Tri(22, 52, 10, 55, 22, 56, "e0502e");
                Eye(30, 51);
                break;
            case FerryPlans.Plover:
                Line(46, 72, 44, 86, 2, "f0a03a");
                Line(56, 72, 58, 86, 2, "f0a03a");
                Oval(52, 62, 22, 14, "fbfbf8");
                Oval(54, 56, 22, 10, "9c8461");
                Oval(34, 48, 10, 9, "9c8461");
                Line(28, 56, 42, 56, 4, "1d1d1d");
                Tri(25, 48, 16, 50, 25, 51, "f0a03a");
                Eye(32, 46);
                break;
            case FerryPlans.Grebe:
                Water();
                Oval(54, 74, 28, 10, "6b4f3a");
                Line(36, 72, 34, 46, 7, "fbfbf8");
                Oval(34, 40, 8, 7, "3a2c22");
                Oval(28, 44, 6, 7, "d86a2a");
                Oval(40, 44, 6, 7, "d86a2a");
                Tri(30, 34, 34, 26, 38, 34, "1d1d1d");
                Tri(26, 40, 16, 42, 26, 43, "c94f4f");
                Eye(32, 39);
                break;
            case FerryPlans.Pelican:
                Line(46, 72, 44, 88, 3, "e0a03a");
                Line(58, 72, 60, 88, 3, "e0a03a");
                Oval(56, 60, 26, 16, "fbfbf8");
                Oval(60, 58, 16, 9, "e9e6df", .2f);
                Line(36, 52, 32, 34, 7, "fbfbf8");
                Oval(32, 30, 8, 7, "fbfbf8");
                canvas.DrawColoredPolygon(new[] { P(30, 30), P(6, 34), P(10, 44), P(28, 38) }, C("f08a2c"));
                Line(30, 30, 6, 33, 2.5f, "f0c040");
                Eye(32, 28);
                break;
            default:
                // The flamingo on one long leg, the other folded.
                Line(52, 60, 52, 92, 2.5f, "f48aa4");
                Line(52, 70, 60, 76, 2.5f, "f48aa4");
                Line(60, 76, 52, 80, 2.5f, "f48aa4");
                Oval(56, 54, 20, 11, "f6a6b8");
                Oval(60, 52, 12, 7, "ee7f98", .2f);
                Line(40, 50, 34, 30, 4, "f6a6b8");
                Line(34, 30, 40, 18, 4, "f6a6b8");
                Oval(42, 16, 6, 5, "f6a6b8");
                Tri(46, 14, 52, 22, 46, 20, "1d1d1d");
                Eye(41, 14);
                break;
        }
    }
}
