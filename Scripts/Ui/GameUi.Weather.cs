using System;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Ui;

public partial class GameUi
{
    internal void WitchHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? Texts.ACarodejnice : Texts.ECarodejnice;
    }

    internal void WeatherChoice(Character witch, Action<WeatherKind> select)
    {
        Clear();
        Text(witch.DisplayName, 30);
        var row = new Godot.HBoxContainer { Alignment = Godot.BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12);
        content.AddChild(row);
        foreach (
            var (kind, icon, name) in new[]
            {
                (WeatherKind.Rain, "rain", Texts.Dest),
                (WeatherKind.Wind, "wind", Texts.Vitr),
                (WeatherKind.Storm, "storm", Texts.Bourka),
                (WeatherKind.Clear, "clear", "Jasno"),
            }
        )
        {
            var button = IconButton(icon, () => select(kind), "Weather_" + kind, row);
            button.AccessibilityName = name;
            button.TooltipText = name;
        }
        BackButton();
    }
}
