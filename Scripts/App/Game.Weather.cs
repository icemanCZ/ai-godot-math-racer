using System;
using MathRacer.Valley;

namespace MathRacer.App;

public partial class Game
{
    internal Weather Weather { get; } = new();

    internal void OfferWeatherChoice()
    {
        ClearChallenge();
        State = GameState.WeatherChoice;
        ShowWeatherChoice();
        Speak(Texts.TedProsimVyberPocasi, nearby);
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
