using System.Collections.Generic;
using System.Linq;
using MathRacer.Buildings;

namespace MathRacer.Valley;

/// <summary>
/// The owl's map of the valley (<c>docs/EXPLORER_PLAN.md</c>) without scene nodes: whether she has shown it to the player,
/// the wonders the car has seen and those she has written into her map, in the order it happened, and the wonder the road
/// arrows lead to now, or -1. A wonder is written in only once it is seen, and the arrows lead only to one not yet seen.
/// </summary>
internal sealed class ExplorerLog
{
    private readonly List<int> spotted = new();
    private readonly List<int> recorded = new();

    /// <summary>Whether the owl has shown the player her map.</summary>
    internal bool Started { get; private set; }

    /// <summary>The wonder the road arrows lead to now, or -1.</summary>
    internal int Guided { get; private set; } = -1;

    internal int[] Spotted => spotted.ToArray();

    internal int[] Recorded => recorded.ToArray();

    internal IReadOnlyCollection<int> RecordedSet => recorded;

    internal bool IsSpotted(int landmark) => spotted.Contains(landmark);

    internal bool IsRecorded(int landmark) => recorded.Contains(landmark);

    /// <summary>The wonders seen that are not yet in the map, which the owl's question writes in.</summary>
    internal int[] Unrecorded => spotted.Where(landmark => !recorded.Contains(landmark)).ToArray();

    internal int RecordedCount => recorded.Count;

    internal bool Complete => recorded.Count == ExplorerPlans.Landmarks;

    internal void Start() => Started = true;

    /// <summary>The car has seen a wonder; it was not seen before. The arrows that led to it go out.</summary>
    internal bool Spot(int landmark)
    {
        if (spotted.Contains(landmark))
            return false;
        spotted.Add(landmark);
        if (Guided == landmark)
            Guided = -1;
        return true;
    }

    /// <summary>Writes every wonder seen into the map and returns those that were new to it.</summary>
    internal int[] Record()
    {
        var added = Unrecorded;
        recorded.AddRange(added);
        return added;
    }

    /// <summary>The road arrows lead to a wonder not yet seen, or to none with -1.</summary>
    internal void Guide(int landmark) => Guided = landmark >= 0 && !spotted.Contains(landmark) ? landmark : -1;

    internal void Restore(bool started, int[] seen, int[] written, int guided)
    {
        Started = started;
        spotted.Clear();
        spotted.AddRange(seen);
        recorded.Clear();
        recorded.AddRange(written);
        Guided = -1;
        Guide(guided);
    }
}
