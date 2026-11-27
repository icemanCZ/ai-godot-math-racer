using System.Collections.Generic;
using System.Linq;
using MathRacer.Buildings;

namespace MathRacer.Valley;

/// <summary>
/// The atlas of the birds of the island (<c>docs/FERRY_PLAN.md</c>), without scene nodes: the number of the crossing whose
/// birds hide round the island now, which of them the telescope has seen, and how many birds of each kind the player has
/// seen. The birds of a crossing follow from the valley and its number, so they are not kept here. Saved as
/// <c>SaveData.Ferry</c>.
/// </summary>
internal sealed class FerryLog
{
    private readonly SortedSet<int> spotted = new();
    private readonly int[] kinds = new int[FerryPlans.Kinds];

    /// <summary>How many crossings to the island were made; the birds round it now are those of this number.</summary>
    internal int Visit { get; private set; }

    /// <summary>The birds of the crossing that were seen, by their number among its four.</summary>
    internal int[] Spotted => spotted.ToArray();

    /// <summary>How many birds of each kind the player has seen.</summary>
    internal int[] Kinds => (int[])kinds.Clone();

    /// <summary>How many kinds the atlas has.</summary>
    internal int KindsFound => kinds.Count(count => count > 0);

    internal bool Complete => KindsFound == FerryPlans.Kinds;

    internal bool IsSpotted(int number) => spotted.Contains(number);

    /// <summary>A crossing to the island: the birds of the next number hide round it, none of them seen yet.</summary>
    internal void NextVisit()
    {
        Visit++;
        spotted.Clear();
    }

    /// <summary>The telescope sees bird <paramref name="number"/> of the crossing; whether it is the first of its kind.</summary>
    internal bool Spot(int number, int kind)
    {
        if (!spotted.Add(number))
            return false;
        kinds[kind]++;
        return kinds[kind] == 1;
    }

    internal void Restore(int visit, int[] seen, int[] counts)
    {
        Visit = visit;
        spotted.Clear();
        foreach (int number in seen)
            spotted.Add(number);
        for (int kind = 0; kind < FerryPlans.Kinds; kind++)
            kinds[kind] = kind < counts.Length ? counts[kind] : 0;
    }
}
