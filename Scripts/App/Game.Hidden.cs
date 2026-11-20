using Godot;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The small friends who hide beside the roads. Driving close to one shows a button under the screen, and pressing E,
/// the space bar or A finds it: it says "pšt!", the car's friends say how many of that kind are found (for example 3/8)
/// and cheer. Finding the eighth of a kind earns that animal as a pet.
/// </summary>
public partial class Game
{
    private void TickHidden(float delta)
    {
        bool running = State is not (GameState.Paused or GameState.Title);
        world.Hidden.Animate(delta, running, vehicle.Position);
    }

    /// <summary>The friend still hiding close enough to the car to be found by pressing E or the space bar.</summary>
    internal HiddenFriend? HiddenNearby => State == GameState.Exploring ? world.Hidden.Nearby(vehicle.Position) : null;

    /// <summary>Finds the friend beside the car, if there is one; returns whether one was found.</summary>
    internal bool CollectHidden()
    {
        if (HiddenNearby is not { } friend)
            return false;
        friend.MarkFound(true);
        FoundHidden(friend);
        SaveProgress();
        return true;
    }

    private void FoundHidden(HiddenFriend friend)
    {
        var hidden = world.Hidden;
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
