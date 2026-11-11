using System;

namespace MathRacer;

public partial class Game
{
    internal const int WitchId = 41;
    internal Weather Weather { get; } = new();

    private void OpenWeatherChallenge()
    {
        nearby = world.Witch;
        vehicle.Active = false;
        CurrentChallenge = challenges.Next();
        rejectedAnswers.Clear();
        feedback = "Za správnou odpověď si vybereš počasí.";
        State = GameState.Challenge;
        ShowChallenge();
    }

    private void ShowWeatherChoice() => ui.WeatherChoice(world.Witch, SelectWeather);

    internal void SelectWeather(WeatherKind kind)
    {
        if (State != GameState.WeatherChoice) return;
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
        Text("Jaké počasí mám přičarovat?", 25);
        Button("Déšť", () => select(WeatherKind.Rain)).GrabFocus();
        Button("Vítr", () => select(WeatherKind.Wind));
        Button("Bouřka", () => select(WeatherKind.Storm));
        Button("Jasno", () => select(WeatherKind.Clear));
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke());
    }
}
