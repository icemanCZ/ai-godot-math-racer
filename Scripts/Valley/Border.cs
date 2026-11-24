namespace MathRacer.Valley;

/// <summary>
/// The four natural borders round the valley (<c>docs/MAP_EXPANSION_PLAN.md</c>, phase 3), numbered in the order of the
/// keepers who live at the end of the road to each (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>); the numbers are saved.
/// </summary>
public enum Border
{
    /// <summary>The sea in the south, with its beach.</summary>
    Sea = 0,

    /// <summary>The mountains in the north, where both rivers rise.</summary>
    Mountains = 1,

    /// <summary>The border river, on the side of the valley's own river.</summary>
    River = 2,

    /// <summary>The forest, on the side away from the river.</summary>
    Forest = 3,
}
