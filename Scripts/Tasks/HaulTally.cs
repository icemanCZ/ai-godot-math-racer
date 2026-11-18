using System;
using MathRacer.Buildings;

namespace MathRacer.Tasks;

/// <summary>
/// How many loads the player has hauled so far. The pets of the raw-material sources and of the works are earned by
/// these counts: every raw load that is tipped out (at a works or at a building) counts once for its material, and every
/// load a works takes in counts once for that works. Holds no scene nodes.
/// </summary>
internal sealed class HaulTally
{
    /// <summary>Raw materials are the first four <see cref="HaulLoad"/> values: stones, soil, sand and logs.</summary>
    public const int RawKinds = 4;

    /// <summary>The sawmill, the brickworks, the gravel works and the glassworks.</summary>
    public const int WorksKinds = 4;

    /// <summary>A count stops here, which is also what a save may hold.</summary>
    public const int Limit = 9999;

    private readonly int[] raw = new int[RawKinds];
    private readonly int[] works = new int[WorksKinds];

    public static bool IsRaw(HaulLoad load) => (int)load is >= 0 and < RawKinds;

    public int RawCount(HaulLoad load) => IsRaw(load) ? raw[(int)load] : 0;

    public int WorksCount(int index) => index is >= 0 and < WorksKinds ? works[index] : 0;

    public void AddRaw(HaulLoad load)
    {
        if (IsRaw(load))
            raw[(int)load] = Math.Min(Limit, raw[(int)load] + 1);
    }

    public void AddWorks(int index)
    {
        if (index is >= 0 and < WorksKinds)
            works[index] = Math.Min(Limit, works[index] + 1);
    }

    public int[] RawCounts() => (int[])raw.Clone();

    public int[] WorksCounts() => (int[])works.Clone();

    /// <summary>Takes the counts of a save; a missing or short list leaves zeros.</summary>
    public void Restore(int[]? savedRaw, int[]? savedWorks)
    {
        Array.Clear(raw);
        Array.Clear(works);
        Copy(savedRaw, raw);
        Copy(savedWorks, works);
    }

    private static void Copy(int[]? from, int[] to)
    {
        if (from == null)
            return;
        for (int i = 0; i < Math.Min(from.Length, to.Length); i++)
            to[i] = Math.Clamp(from[i], 0, Limit);
    }
}
