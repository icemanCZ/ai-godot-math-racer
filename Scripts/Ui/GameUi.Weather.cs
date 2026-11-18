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
        // Two rows of three: the weather of the day, then the sun, the evening and the night.
        var row = new Godot.GridContainer { Columns = 3 };
        row.AddThemeConstantOverride("h_separation", 12);
        row.AddThemeConstantOverride("v_separation", 12);
        content.AddChild(row);
        foreach (
            var (kind, icon, name) in new[]
            {
                (WeatherKind.Rain, "rain", Texts.Dest),
                (WeatherKind.Wind, "wind", Texts.Vitr),
                (WeatherKind.Storm, "storm", Texts.Bourka),
                (WeatherKind.Clear, "clear", "Jasno"),
                (WeatherKind.Evening, "evening", Texts.Vecer),
                (WeatherKind.Night, "night", Texts.Noc),
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
