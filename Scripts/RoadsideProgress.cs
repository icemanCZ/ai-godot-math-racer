using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

/// <summary>
/// Roadside friends' expeditions: a solved opening quiz unlocks a friend's delivery or collection,
/// which can be started later and is finished at most once.
/// </summary>
internal sealed class RoadsideProgress
{
    private readonly HashSet<Animal> unlocked = new();
    private readonly HashSet<Animal> completed = new();

    public int CompletedCount => completed.Count;

    public void Unlock(Animal friend) => unlocked.Add(friend);

    public void Complete(Animal friend) => completed.Add(friend);

    /// <summary>Whether the friend's expedition is unlocked, unfinished and not already under way.</summary>
    public bool CanStart(Animal friend, Expedition? active) =>
        unlocked.Contains(friend) && !completed.Contains(friend) && active?.Sender != friend;

    /// <summary>Saved as indices into the roadside friend list.</summary>
    public int[] UnlockedIndices(Animal[] friends) => unlocked.Select(a => Array.IndexOf(friends, a)).ToArray();

    public int[] CompletedIndices(Animal[] friends) => completed.Select(a => Array.IndexOf(friends, a)).ToArray();

    public void Restore(Animal[] friends, IEnumerable<int> unlockedIndices, IEnumerable<int> completedIndices)
    {
        unlocked.Clear();
        completed.Clear();
        foreach (int i in unlockedIndices)
            unlocked.Add(friends[i]);
        foreach (int i in completedIndices)
            completed.Add(friends[i]);
    }
}
