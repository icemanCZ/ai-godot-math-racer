using System;
using Godot;
using MathRacer.Valley;

namespace MathRacer.App;

public partial class Game
{
    internal Weather Weather { get; } = new();

    /// <summary>The butterflies and wind-blown leaves round the car (<see cref="Valley.Flutters"/>).</summary>
    private readonly Flutters flutters = new() { Name = "Flutters" };

    private bool fluttersReady;

    private void SetupFlutters()
    {
        world.AddChild(flutters);
        flutters.Setup(world.Ground);
        fluttersReady = true;
    }

    /// <summary>Butterflies fly on a fine day, not in the rain or after dusk; leaves fly with the wind.</summary>
    private void TickFlutters(float dt)
    {
        if (!fluttersReady)
            return;
        float calm = Weather.Current is WeatherKind.Rain or WeatherKind.Storm
            ? 0
            : 1 - Mathf.Clamp(Weather.Darkness / .35f, 0, 1);
        flutters.Tick(dt, vehicle.Position, calm, Weather.Wind);
    }

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
