using Godot;

namespace MathRacer.Ui;

/// <summary>
/// What the player sees through the telescope of the hide (<c>docs/FERRY_PLAN.md</c>): the dark round edge of the lens,
/// a ring in the middle that fills while a bird stays in it, the arrows at the side towards the nearest bird still hidden
/// and, under the lens, the birds of the crossing, faint until seen and with a green check mark once they are.
/// </summary>
public partial class TelescopeOverlay : Control
{
    /// <summary>Which way to turn for the nearest bird still hidden: -1 to the left, 1 to the right, 0 in view.</summary>
    public int Hint { get; set; }

    /// <summary>How far the ring in the middle has filled, 0 to 1.</summary>
    public float Progress { get; set; }

    /// <summary>The birds of the crossing: their kind and whether the telescope has seen them.</summary>
    public (int Kind, bool Seen)[] Birds { get; set; } = System.Array.Empty<(int, bool)>();

    private float time;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
            return;
        time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2;
        float radius = Mathf.Min(Size.X, Size.Y) * .46f;
        float outer = Size.Length();
        // The dark edge round the round lens.
        const int segments = 72;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * Mathf.Tau / segments,
                a1 = (i + 1) * Mathf.Tau / segments;
            var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            DrawColoredPolygon(
                new[] { center + d0 * radius, center + d1 * radius, center + d1 * outer, center + d0 * outer },
                new Color(.03f, .04f, .05f, .96f)
            );
        }
        DrawArc(center, radius, 0, Mathf.Tau, segments, new Color("5b4a35"), 10, true);
        // The cross hairs and the ring in the middle, which fills in green.
        var hair = new Color(1, 1, 1, .35f);
        DrawLine(center + new Vector2(-radius, 0), center + new Vector2(-46, 0), hair, 2, true);
        DrawLine(center + new Vector2(46, 0), center + new Vector2(radius, 0), hair, 2, true);
        DrawLine(center + new Vector2(0, -radius), center + new Vector2(0, -46), hair, 2, true);
        DrawLine(center + new Vector2(0, 46), center + new Vector2(0, radius), hair, 2, true);
        DrawArc(center, 38, 0, Mathf.Tau, 48, new Color(1, 1, 1, .7f), 4, true);
        if (Progress > 0)
            DrawArc(
                center,
                38,
                -Mathf.Pi / 2,
                -Mathf.Pi / 2 + Mathf.Tau * Mathf.Clamp(Progress, 0, 1),
                48,
                new Color("62d17a"),
                9,
                true
            );
        // The arrow towards the nearest bird still hidden, nodding to draw the eye.
        if (Hint != 0)
        {
            float x = center.X + Hint * (radius - 70 + Mathf.Sin(time * 5) * 10);
            float y = center.Y;
            var tip = new Vector2(x + Hint * 40, y);
            DrawColoredPolygon(
                new[] { tip, new Vector2(x - Hint * 10, y - 42), new Vector2(x - Hint * 10, y + 42) },
                new Color("ffd34d")
            );
        }
        // The birds of the crossing under the lens.
        float cell = Mathf.Min(96, Size.Y * .11f);
        float width = Birds.Length * (cell + 14) - 14;
        var start = new Vector2(center.X - width / 2, Size.Y - cell - 18);
        for (int i = 0; i < Birds.Length; i++)
        {
            var area = new Rect2(start + new Vector2(i * (cell + 14), 0), new Vector2(cell, cell));
            DrawRect(area, new Color(1, 1, 1, Birds[i].Seen ? .85f : .25f));
            BirdPicture.Paint(this, area.Grow(-6), Birds[i].Kind, !Birds[i].Seen);
            if (Birds[i].Seen)
            {
                var corner = area.End - new Vector2(18, 18);
                DrawCircle(corner, 15, new Color("4f9d5e"));
                DrawPolyline(
                    new[] { corner + new Vector2(-8, 0), corner + new Vector2(-2, 7), corner + new Vector2(9, -7) },
                    Colors.White,
                    4,
                    true
                );
            }
            else
            {
                var font = GetThemeDefaultFont();
                DrawString(
                    font,
                    area.GetCenter() + new Vector2(-10, 14),
                    "?",
                    HorizontalAlignment.Left,
                    -1,
                    40,
                    new Color(1, 1, 1, .9f)
                );
            }
        }
    }
}
