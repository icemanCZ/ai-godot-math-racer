using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The small friends who hide beside the roads: <see cref="PerKind"/> ladybugs, hedgehogs and frogs. A friend is found
/// when the player presses E or the space bar with the car within <see cref="FindRadius"/>; finding all of one kind earns that animal as a pet. The friends
/// stand at the same places in every game of a valley and are numbered in the order they were placed, so a save only
/// needs the numbers of those found.
/// </summary>
internal sealed partial class HiddenFriends : Node3D
{
    internal const int PerKind = 8;
    internal static readonly int KindCount = System.Enum.GetValues<HiddenKind>().Length;
    internal static readonly int Total = PerKind * KindCount;
    internal const float FindRadius = 7;

    private readonly List<HiddenFriend> friends = new();

    internal IReadOnlyList<HiddenFriend> Friends => friends;

    internal int FoundCount => friends.Count(f => f.Found);

    internal int FoundOf(HiddenKind kind) => friends.Count(f => f.Kind == kind && f.Found);

    internal int[] FoundIndices() => Enumerable.Range(0, friends.Count).Where(i => friends[i].Found).ToArray();

    internal HiddenFriend Add(HiddenKind kind, Vector3 position, float yaw)
    {
        var friend = new HiddenFriend(kind, yaw) { Position = position };
        AddChild(friend);
        friends.Add(friend);
        return friend;
    }

    /// <summary>Takes the friends of a saved game: those whose numbers are listed are gone, the rest hide again.</summary>
    internal void Restore(IEnumerable<int> found)
    {
        foreach (var friend in friends)
            friend.Restore();
        foreach (int index in found)
            if (index >= 0 && index < friends.Count)
                friends[index].MarkFound(false);
    }

    /// <summary>The nearest friend still hiding within <see cref="FindRadius"/> of the car, or null.</summary>
    internal HiddenFriend? Nearby(Vector3 car)
    {
        HiddenFriend? nearest = null;
        float best = FindRadius * FindRadius;
        foreach (var friend in friends)
        {
            if (friend.Found)
                continue;
            float dx = friend.GlobalPosition.X - car.X,
                dz = friend.GlobalPosition.Z - car.Z;
            float distance = dx * dx + dz * dz;
            if (distance > best)
                continue;
            best = distance;
            nearest = friend;
        }
        return nearest;
    }

    internal void Ping(float seconds)
    {
        foreach (var friend in friends)
            friend.Ping(seconds);
    }

    internal void Animate(float delta, bool running, Vector3 car)
    {
        foreach (var friend in friends)
            friend.Animate(delta, running, car);
    }
}
