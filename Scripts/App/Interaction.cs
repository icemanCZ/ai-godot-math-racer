using MathRacer.Characters;

namespace MathRacer.App;

/// <summary>
/// Where the game is in the player's eyes: driving, in a conversation or a panel, or paused. It remembers the state
/// a pause came from, so that Resume returns to it, and the character the player is talking to. It holds no scene
/// nodes: <see cref="Game"/> stops the car and shows the panel.
/// </summary>
internal sealed class Interaction
{
    public GameState State { get; set; } = GameState.Title;

    /// <summary>The state that a pause interrupted.</summary>
    public GameState BeforePause { get; set; }

    /// <summary>The character the player is talking to or is next to.</summary>
    public Animal? Nearby { get; set; }

    /// <summary>The state a save records: the one before a pause, since a pause is never saved.</summary>
    public GameState Resting => State == GameState.Paused ? BeforePause : State;

    /// <summary>Title, driving and pause have no panel; every other state is an interaction that Back closes.</summary>
    public static bool IsInteraction(GameState state) =>
        state is not (GameState.Title or GameState.Exploring or GameState.Paused);

    /// <summary>Switches to an interaction state, with the character it is about when there is one.</summary>
    public void Enter(GameState state, Animal? actor = null)
    {
        if (actor != null)
            Nearby = actor;
        State = state;
    }

    public void Pause()
    {
        BeforePause = State;
        State = GameState.Paused;
    }

    /// <summary>Goes back to the state that the pause interrupted.</summary>
    public void Unpause() => State = BeforePause;
}
