using Godot;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The small friends who hide beside the roads. Driving close to one finds it: it says "pšt!", the notebook in the
/// corner counts it and the car's friends cheer. Finding the eighth of a kind earns that animal as a pet.
/// </summary>
public partial class Game
{
    private void TickHidden(float delta)
    {
        bool running = State is not (GameState.Paused or GameState.Title);
        world.Hidden.Animate(delta, running, vehicle.Position);
        if (State != GameState.Exploring)
            return;
        var found = world.Hidden.Find(vehicle.Position);
        foreach (int index in found)
            FoundHidden(world.Hidden.Friends[index]);
        if (found.Count > 0)
            SaveProgress();
    }

    private void FoundHidden(HiddenFriend friend)
    {
        var hidden = world.Hidden;
        ui.HiddenCount(hidden);
        int count = hidden.FoundOf(friend.Kind);
        string? pets = count == HiddenFriends.PerKind ? EarnPets() : null;
        if (pets != null)
        {
            CelebrateAt(
                friend.GlobalPosition + Vector3.Up * 1.5f,
                WithPets(Texts.HiddenAllOfKind(friend.Kind, HiddenFriends.PerKind), pets)
            );
            Speak(Texts.HiddenAllOfKind(friend.Kind, HiddenFriends.PerKind));
        }
        else
            Speak(Texts.HiddenFoundSpeech(friend.FriendName, count, HiddenFriends.PerKind));
    }
}
