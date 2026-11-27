using System.Collections.Generic;
using System.Linq;
using MathRacer.Buildings;

namespace MathRacer.Valley;

/// <summary>
/// The shells of the beach (<c>docs/SHELLS_PLAN.md</c>) without scene nodes: the number of the tide whose shells lie on
/// the sand now, those of them already picked up, and how many shells of each kind the player has.
/// </summary>
internal sealed class ShellsLog
{
    private readonly HashSet<int> picked = new();
    private readonly int[] kinds = new int[ShellPlans.Kinds];

    internal int Tide { get; private set; }

    internal int[] Picked => picked.Order().ToArray();

    internal int[] Kinds => kinds.ToArray();

    internal bool IsPicked(int shell) => picked.Contains(shell);

    internal int CountOf(int kind) => kinds[kind];

    /// <summary>How many of the kinds the player has at least one shell of.</summary>
    internal int KindsFound => kinds.Count(count => count > 0);

    internal bool Complete => KindsFound == ShellPlans.Kinds;

    /// <summary>A high tide has come: the next tide's shells lie on the sand, none of them picked up.</summary>
    internal void NextTide()
    {
        Tide++;
        picked.Clear();
    }

    /// <summary>Picks up a shell of the tide; true when it was the first of its kind.</summary>
    internal bool Pick(int shell, int kind)
    {
        if (!picked.Add(shell))
            return false;
        kinds[kind] = System.Math.Min(kinds[kind] + 1, ShellPlans.MostOfAKind);
        return kinds[kind] == 1;
    }

    internal void Restore(int tide, int[] taken, int[] counts)
    {
        Tide = tide;
        picked.Clear();
        picked.UnionWith(taken);
        System.Array.Clear(kinds);
        for (int i = 0; i < kinds.Length && i < counts.Length; i++)
            kinds[i] = counts[i];
    }
}
