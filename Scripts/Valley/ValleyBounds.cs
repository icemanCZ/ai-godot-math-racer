namespace MathRacer.Valley;

/// <summary>
/// Where the valley ends while it is played: how far the car may drive, where a lamp may stand, where a caught animal
/// may land and how far the grass and the wild animals go. It is a square just inside the natural borders
/// (<see cref="ValleyBorders"/>): the foot of the mountains, the beach before the sea, the bank of the border river and
/// the edge of the forest. The generation of the old valley does not ask this; it keeps to
/// <see cref="WorldTuning.CorePlayableRadius"/>, so that nothing already there moves (<c>docs/MAP_EXPANSION_PLAN.md</c>).
/// </summary>
internal static class ValleyBounds
{
    /// <summary>Half the side of the square that is played, some six times the old valley's circle.</summary>
    public const float HalfSize = 1130;

    /// <summary>Whether the point lies inside the valley at least <paramref name="margin"/> metres from its edge.</summary>
    public static bool Contains(float x, float z, float margin = 0) =>
        System.MathF.Abs(x) <= HalfSize - margin && System.MathF.Abs(z) <= HalfSize - margin;
}
