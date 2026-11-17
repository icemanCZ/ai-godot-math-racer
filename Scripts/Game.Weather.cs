using System;

namespace MathRacer;

public partial class Game
{
    internal Weather Weather { get; } = new();

    internal void OpenWeatherChallenge()
    {
        nearby = world.Witch;
        vehicle.Active = false;
        BeginQuiz();
    }

    internal void OfferWeatherChoice()
    {
        ClearChallenge();
        State = GameState.WeatherChoice;
        ShowWeatherChoice();
        Speak("Teď prosím vyber počasí.", nearby);
    }

    private void ShowWeatherChoice() => ui.WeatherChoice(world.Witch, SelectWeather);

    internal void SelectWeather(WeatherKind kind)
    {
        if (State != GameState.WeatherChoice)
            return;
        Weather.Begin(kind);
        world.Witch.Celebrate();
        CloseInteraction();
    }
}

public partial class GameUi
{
    internal void WitchHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Čarodějnice" : "E · Čarodějnice";
    }

    internal void WeatherChoice(Animal witch, Action<WeatherKind> select)
    {
        Clear();
        Text(witch.DisplayName, 30);
        var row = new Godot.HBoxContainer { Alignment = Godot.BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 12);
        content.AddChild(row);
        foreach (
            var (kind, icon, name) in new[]
            {
                (WeatherKind.Rain, "rain", "Déšť"),
                (WeatherKind.Wind, "wind", "Vítr"),
                (WeatherKind.Storm, "storm", "Bouřka"),
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
