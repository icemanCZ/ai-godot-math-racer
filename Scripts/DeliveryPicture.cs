using Godot;
using System.Linq;

namespace MathRacer;

/// <summary>Font-independent instructions and portraits for children who cannot read.</summary>
public partial class DeliveryPicture : Control
{
    public string Icon { get; init; } = "";
    public Animal? Animal { get; init; }
    public int Count { get; init; } = 1;
    public int FilledCount { get; set; } = -1;
    public float Direction { get; set; }
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    private void Oval(Vector2 p, Vector2 radius, Color color)
        => DrawColoredPolygon(Enumerable.Range(0, 40).Select(i => p + new Vector2(Mathf.Cos(i * Mathf.Tau / 40) * radius.X, Mathf.Sin(i * Mathf.Tau / 40) * radius.Y)).ToArray(), color);
    public override void _Draw()
    {
        float scale = Mathf.Min(Size.X / 180, Size.Y / 150);
        DrawSetTransform((Size - new Vector2(180, 150) * scale) / 2, 0, Vector2.One * scale);
        Color ink = new("3c5960"), green = new("70b765");
        if (Animal != null)
        {
            var fur = new Color(Animal.FurColor);
            foreach (int sign in new[] { -1, 1 })
            {
                Oval(new(90 + sign * 31, Animal.Kind == 0 ? 35 : 48), new(17, Animal.Kind == 0 ? 33 : 20), fur);
                Oval(new(90 + sign * 31, Animal.Kind == 0 ? 35 : 48), new(8, Animal.Kind == 0 ? 23 : 10), new("eab4ad"));
            }
            Oval(new(90, 88), new(58, 49), fur); Oval(new(90, 111), new(30, 20), new("fff0d9"));
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
                    DrawColoredPolygon(new Vector2[] { new(x - 17, 53), new(x + 19, 60), new(x - 10, 119) }, new Color("f59242"));
                    for (int j = -1; j <= 1; j++) DrawLine(new(x, 56), new(x + j * 13, 30), green, 7, true);
                }
                else if (Icon == "apple")
                {
                    Oval(new(x - 9, 86), new(17, 26), new("ed776b")); Oval(new(x + 9, 86), new(17, 26), new("ed776b"));
                    DrawLine(new(x, 68), new(x + 3, 48), ink, 4, true); Oval(new(x + 14, 54), new(12, 5), green);
                }
                else if (Icon == "honey")
                {
                    DrawStyleBox(new StyleBoxFlat { BgColor = new("f1bb43"), CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10, CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12 }, new(x - 22, 60, 44, 52));
                    DrawRect(new(x - 24, 51, 48, 13), new Color("a27452")); Oval(new(x, 87), new(12, 15), new("fff5df"));
                }
                else if (Icon == "bread")
                {
                    Oval(new(x, 85), new(24, 17), new("d99b52"));
                    for (int cut = -1; cut <= 1; cut++) DrawLine(new(x + cut * 12 - 3, 74), new(x + cut * 12 + 3, 85), new Color("ffe1a4"), 4, true);
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
                else { DrawCircle(new(x, 75), 18, ink); DrawCircle(new(x, 75), 14, new Color("fff5df")); }
                if (FilledCount >= 0)
                {
                    DrawCircle(new(x, 136), 8, ink);
                    DrawCircle(new(x, 136), 5, i < FilledCount ? green : new Color("fff5df"));
                }
            }
        }
        else if (Icon == "paint")
        {
            Oval(new(90, 77), new(55, 43), new("fff0ce"));
            foreach (var dot in new[] { (new Vector2(58, 65), "ed4762"), (new Vector2(83, 48), "ffce38"),
                (new Vector2(113, 54), "46c6a0"), (new Vector2(124, 82), "4976ed") })
                DrawCircle(dot.Item1, 11, new Color(dot.Item2));
            DrawLine(new(75, 107), new(111, 71), ink, 9, true);
            DrawCircle(new(72, 110), 9, new Color("ed4762"));
        }
        else if (Icon == "vehicle")
        {
            DrawStyleBox(new StyleBoxFlat { BgColor = new("fff0ce"), CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
                CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8 }, new Rect2(33, 67, 114, 38));
            DrawColoredPolygon(new Vector2[] { new(52, 67), new(67, 39), new(111, 39), new(129, 67) }, new Color("fff0ce"));
            DrawColoredPolygon(new Vector2[] { new(64, 63), new(74, 47), new(104, 47), new(115, 63) }, new Color("4976ed"));
            foreach (float x in new[] { 59f, 123f }) { DrawCircle(new(x, 106), 16, ink); DrawCircle(new(x, 106), 8, new Color("fff0ce")); }
        }
        else if (Icon == "upgrade")
        {
            DrawLine(new(56, 112), new(111, 57), Colors.White, 18, true);
            DrawCircle(new(55, 113), 14, Colors.White); DrawCircle(new(55, 113), 6, ink);
            DrawArc(new(117, 51), 24, -.35f, Mathf.Pi * 1.6f, 32, Colors.White, 13, true);
            DrawLine(new(143, 103), new(143, 130), new Color("ffce38"), 7, true);
            DrawLine(new(130, 116), new(156, 116), new Color("ffce38"), 7, true);
        }
        else if (Icon == "check") DrawPolyline(new Vector2[] { new(50, 76), new(77, 103), new(132, 45) }, Colors.White, 12, true);
        else if (Icon == "back")
        {
            DrawLine(new(132, 75), new(48, 75), Colors.White, 10, true);
            DrawPolyline(new Vector2[] { new(77, 45), new(47, 75), new(77, 105) }, Colors.White, 10, true);
        }
        else if (Icon == "arrow")
        {
            DrawSetTransform(Size / 2, Direction, Vector2.One * scale);
            DrawColoredPolygon(new Vector2[] { new(0, -56), new(35, -12), new(14, -12), new(14, 46), new(-14, 46), new(-14, -12), new(-35, -12) }, new Color("edb941"));
        }
        else if (Icon == "play") DrawColoredPolygon(new Vector2[] { new(65, 37), new(65, 113), new(126, 75) }, Colors.White);
        else if (Icon == "pause") { DrawRect(new(60, 40, 20, 70), Colors.White); DrawRect(new(100, 40, 20, 70), Colors.White); }
        else if (Icon == "search")
        {
            DrawArc(new(78, 63), 32, 0, Mathf.Tau, 40, ink, 9, true);
            DrawLine(new(101, 87), new(137, 124), ink, 12, true);
        }
    }
}
