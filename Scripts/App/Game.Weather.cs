using System;
using MathRacer.Valley;

namespace MathRacer.App;

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
