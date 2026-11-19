using Godot;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>
/// One picture of the child's collection of stars, drawn on a night-blue card: the stars that shine are gold and joined by a
/// line where both ends shine, the others are faint rings that show where the next ones will be.
/// </summary>
public partial class StarPicture : Control
{
    /// <summary>Index into <see cref="Constellation.All"/>.</summary>
    public int Picture { get; init; }

    /// <summary>How many of its stars shine.</summary>
    public int Lit { get; init; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        var shape = Constellation.All[Picture];
        DrawStyleBox(
            new StyleBoxFlat
            {
                BgColor = new Color("1d2859"),
                CornerRadiusTopLeft = 18,
                CornerRadiusTopRight = 18,
                CornerRadiusBottomLeft = 18,
                CornerRadiusBottomRight = 18,
            },
            new Rect2(Vector2.Zero, Size)
        );
        // The pictures span about 28 degrees across and 20 up, with their middle a little above the offset origin.
        float scale = Mathf.Min(Size.X / 32, (Size.Y - 22) / 26);
        var middle = new Vector2(Size.X / 2, (Size.Y - 22) / 2 + 3);
        Vector2 At(int star) => middle + new Vector2(shape.Stars[star].X, -shape.Stars[star].Y + 3) * scale;
        for (int i = 0; i + 1 < shape.Lines.Length; i += 2)
            if (shape.Lines[i] < Lit && shape.Lines[i + 1] < Lit)
                DrawLine(At(shape.Lines[i]), At(shape.Lines[i + 1]), new Color("ffe9a0"), 3, true);
        for (int star = 0; star < shape.Stars.Length; star++)
        {
            if (star < Lit)
            {
                DrawCircle(At(star), 9, new Color("ffe9a0", .35f), true, -1, true);
                DrawCircle(At(star), 6, new Color("ffe9a0"), true, -1, true);
                DrawCircle(At(star), 3, Colors.White, true, -1, true);
            }
            else
                DrawArc(At(star), 5, 0, Mathf.Tau, 14, new Color("8fa2e6", .55f), 2, true);
        }
        var font = ThemeDB.FallbackFont;
        DrawString(
            font,
            new Vector2(0, Size.Y - 8),
            $"{shape.Name} {Lit}/{shape.Stars.Length}",
            HorizontalAlignment.Center,
            Size.X,
            16,
            Lit == shape.Stars.Length ? new Color("ffe9a0") : new Color("c7d2ff")
        );
    }
}
