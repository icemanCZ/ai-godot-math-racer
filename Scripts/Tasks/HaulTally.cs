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

    /// <summary>
    /// The wheat tipped out at the bakery and the loads the bakery took in. They are counted apart from the arrays above,
    /// whose length a saved game has always had, and a save from before the bakery has zeros.
    /// </summary>
    public int Wheat { get; private set; }

    public int Baked { get; private set; }

    public static bool IsRaw(HaulLoad load) => (int)load is >= 0 and < RawKinds;

    public int RawCount(HaulLoad load) =>
        IsRaw(load) ? raw[(int)load]
        : load == HaulLoad.Wheat ? Wheat
        : 0;

    public int WorksCount(int index) =>
        index is >= 0 and < WorksKinds ? works[index]
        : index == WorksCatalog.BreadIndex ? Baked
        : 0;

    public void AddRaw(HaulLoad load)
    {
        if (IsRaw(load))
            raw[(int)load] = Math.Min(Limit, raw[(int)load] + 1);
        else if (load == HaulLoad.Wheat)
            Wheat = Math.Min(Limit, Wheat + 1);
    }

    public void AddWorks(int index)
    {
        if (index is >= 0 and < WorksKinds)
            works[index] = Math.Min(Limit, works[index] + 1);
        else if (index == WorksCatalog.BreadIndex)
            Baked = Math.Min(Limit, Baked + 1);
    }

    /// <summary>
    /// Raises the loads a works has taken in to at least <paramref name="loads"/>. A stock that stands on the yard was
    /// made from that many loads, so a count that is behind its stock (a save from before the counts) catches up when
    /// the stock next changes; a restored save is left as it was saved.
    /// </summary>
    public void RaiseWorks(int index, int loads)
    {
        loads = Math.Clamp(loads, 0, Limit);
        if (index is >= 0 and < WorksKinds)
            works[index] = Math.Max(works[index], loads);
        else if (index == WorksCatalog.BreadIndex)
            Baked = Math.Max(Baked, loads);
    }

    /// <summary>
    /// Raises the loads of a raw material that were hauled to at least <paramref name="loads"/>: the goods in a works were
    /// made from loads of that material, so the stock is a lower bound for it just as for <see cref="RaiseWorks"/>.
    /// </summary>
    public void RaiseRaw(HaulLoad load, int loads)
    {
        loads = Math.Clamp(loads, 0, Limit);
        if (IsRaw(load))
            raw[(int)load] = Math.Max(raw[(int)load], loads);
        else if (load == HaulLoad.Wheat)
            Wheat = Math.Max(Wheat, loads);
    }

    public int[] RawCounts() => (int[])raw.Clone();

    public int[] WorksCounts() => (int[])works.Clone();

    /// <summary>Takes the counts of a save; a missing or short list leaves zeros.</summary>
    public void Restore(int[]? savedRaw, int[]? savedWorks, int savedWheat = 0, int savedBaked = 0)
    {
        Array.Clear(raw);
        Array.Clear(works);
        Copy(savedRaw, raw);
        Copy(savedWorks, works);
        Wheat = Math.Clamp(savedWheat, 0, Limit);
        Baked = Math.Clamp(savedBaked, 0, Limit);
    }

    private static void Copy(int[]? from, int[] to)
    {
        if (from == null)
            return;
        for (int i = 0; i < Math.Min(from.Length, to.Length); i++)
            to[i] = Math.Clamp(from[i], 0, Limit);
    }
}
