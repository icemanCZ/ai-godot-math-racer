using System;
using System.Collections.Generic;
using System.Linq;
using MathRacer.Tasks;

namespace MathRacer.Characters;

/// <summary>
/// Roadside friends' expeditions: a solved opening quiz unlocks a friend's delivery or collection,
/// which can be started later and is finished at most once.
/// </summary>
internal sealed class RoadsideProgress
{
    private readonly HashSet<Character> unlocked = new();
    private readonly HashSet<Character> completed = new();

    public int CompletedCount => completed.Count;

    public void Unlock(Character friend) => unlocked.Add(friend);

    public void Complete(Character friend) => completed.Add(friend);

    /// <summary>Whether the friend's expedition is unlocked, unfinished and not already under way.</summary>
    public bool CanStart(Character friend, Expedition? active) =>
        unlocked.Contains(friend) && !completed.Contains(friend) && active?.Sender != friend;

    /// <summary>Saved as indices into the roadside friend list.</summary>
    public int[] UnlockedIndices(Character[] friends) => unlocked.Select(a => Array.IndexOf(friends, a)).ToArray();

    public int[] CompletedIndices(Character[] friends) => completed.Select(a => Array.IndexOf(friends, a)).ToArray();

    public void Restore(Character[] friends, IEnumerable<int> unlockedIndices, IEnumerable<int> completedIndices)
    {
        unlocked.Clear();
        completed.Clear();
        foreach (int i in unlockedIndices)
            unlocked.Add(friends[i]);
        foreach (int i in completedIndices)
            completed.Add(friends[i]);
    }
}
