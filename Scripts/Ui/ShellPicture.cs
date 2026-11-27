using System.Linq;
using Godot;
using MathRacer.Buildings;

namespace MathRacer.Ui;

/// <summary>
/// A picture of a kind of shell (<c>docs/SHELLS_PLAN.md</c>) for children who cannot read: the scallop, the cockle, the
/// mussel, the whelk, the cowrie, the starfish, the sea urchin and the pearl oyster. <see cref="Faint"/> draws it as a
/// pale shadow, for a kind not yet found.
/// </summary>
public partial class ShellPicture : Control
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
        float s = Mathf.Min(Size.X, Size.Y) / 100;
        var center = Size / 2;
        float alpha = Faint ? .3f : 1;
        Vector2 P(float x, float y) => center + new Vector2(x - 50, y - 50) * s;
        Color C(string hex) => new Color(hex) with { A = alpha };
        void Oval(float x, float y, float rx, float ry, string color) =>
            DrawColoredPolygon(
                Enumerable
                    .Range(0, 24)
                    .Select(i => P(x + Mathf.Cos(i * Mathf.Tau / 24) * rx, y + Mathf.Sin(i * Mathf.Tau / 24) * ry))
                    .ToArray(),
                C(color)
            );
        void Line(float x0, float y0, float x1, float y1, float width, string color) =>
            DrawLine(P(x0, y0), P(x1, y1), C(color), width * s, true);
        switch (Kind)
        {
            case ShellPlans.Scallop:
                DrawColoredPolygon(
                    Enumerable
                        .Range(0, 13)
                        .Select(i => P(50 - Mathf.Cos(i * Mathf.Pi / 12) * 40, 62 - Mathf.Sin(i * Mathf.Pi / 12) * 40))
                        .Append(P(50, 80))
                        .ToArray(),
                    C("f08a5d")
                );
                for (int rib = -3; rib <= 3; rib++)
                    Line(50, 78, 50 + rib * 11, 28 + Mathf.Abs(rib) * 3, 3, "e06b45");
                DrawRect(new Rect2(P(40, 76), new Vector2(20, 10) * s), C("f4b48f"));
                break;
            case ShellPlans.Cockle:
                Oval(50, 54, 36, 30, "efe2c4");
                for (int rib = -2; rib <= 2; rib++)
                    Line(50 + rib * 12, 28, 50 + rib * 9, 82, 3, "d9c49b");
                break;
            case ShellPlans.Mussel:
                Oval(50, 50, 22, 40, "2d3e66");
                Oval(52, 48, 14, 30, "5c6f9a");
                break;
            case ShellPlans.Whelk:
                DrawColoredPolygon(new[] { P(30, 72), P(78, 20), P(70, 74) }, C("d8b27a"));
                Oval(44, 68, 18, 16, "c99a5e");
                Oval(40, 70, 9, 10, "f6d9c0");
                Line(40, 52, 70, 38, 3, "b8864a");
                Line(48, 42, 74, 28, 3, "b8864a");
                break;
            case ShellPlans.Cowrie:
                Oval(50, 50, 28, 38, "b7774a");
                foreach (var (x, y) in new[] { (40f, 34f), (58f, 40f), (46f, 56f), (60f, 64f), (38f, 70f), (52f, 28f) })
                    Oval(x, y, 4, 4, "f3e3c9");
                break;
            case ShellPlans.Starfish:
                DrawColoredPolygon(
                    Enumerable
                        .Range(0, 10)
                        .Select(i =>
                        {
                            float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
                            float r = i % 2 == 0 ? 42 : 17;
                            return P(50 + Mathf.Cos(a) * r, 54 + Mathf.Sin(a) * r);
                        })
                        .ToArray(),
                    C("f07a2b")
                );
                Oval(50, 54, 7, 7, "f39a4b");
                break;
            case ShellPlans.Urchin:
                for (int spike = 0; spike < 16; spike++)
                {
                    float a = spike * Mathf.Tau / 16;
                    Line(50, 54, 50 + Mathf.Cos(a) * 42, 54 + Mathf.Sin(a) * 36, 3, "8a5a9a");
                }
                Oval(50, 54, 26, 22, "6b3f7a");
                break;
            default:
                Oval(50, 64, 38, 18, "c9bfc9");
                Oval(50, 40, 36, 16, "b4a8b8");
                Oval(50, 60, 9, 9, "fffdf8");
                Oval(47, 57, 3, 3, "ffffff");
                break;
        }
    }
}
