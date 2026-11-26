using Godot;

namespace MathRacer.Buildings;

/// <summary>
/// The shepherd's lost sheep (<c>docs/SHEEP_PLAN.md</c>): their names, where each stands once it is home in the fold of
/// the shepherd's homestead, and what bringing them home pays: coins for every sheep driven into the fold, a bonus for
/// the whole flock, and the sheepdog puppy Alík once all eight are home.
/// </summary>
internal static class SheepPlans
{
    /// <summary>How many sheep ran off in the storm, one for every eighth of the land round the valley.</summary>
    public const int Sheep = 8;

    /// <summary>The coins for every sheep driven into the fold.</summary>
    public const int CoinsPerSheep = 5;

    /// <summary>The bonus when the whole flock is home.</summary>
    public const int FlockCoins = 25;

    /// <summary>The names of the sheep, in their order; a save holds their numbers.</summary>
    public static readonly string[] Names =
    {
        "Bělka",
        "Kudrnka",
        "Vločka",
        "Chundelka",
        "Mráček",
        "Pampeliška",
        "Ťapka",
        "Kulička",
    };

    public static string Name(int sheep) => Names[Mathf.Clamp(sheep, 0, Names.Length - 1)];

    /// <summary>
    /// Where each sheep stands in the fold once it is home, in the frame of the homestead's model (across, on from the
    /// front of the yard): between the three sheep that never ran off, inside the rails from 2.4 to 8.6 across and 2.4 to
    /// 9.6 on.
    /// </summary>
    public static readonly Vector2[] FoldSpots =
    {
        new(3.3f, 3.4f),
        new(5.5f, 3.3f),
        new(3.2f, 6.6f),
        new(4.7f, 8.4f),
        new(7.8f, 8.8f),
        new(7.9f, 5.8f),
        new(5.4f, 6.1f),
        new(3.3f, 8.9f),
    };

    /// <summary>The front rail of the fold, which the sheep jump over, lies this far on from the front of the yard.</summary>
    public const float FoldFront = 2.4f;

    /// <summary>The fold's rails across, from one side to the other.</summary>
    public const float FoldLeft = 2.4f,
        FoldRight = 8.6f;

    /// <summary>The middle of the fold in the homestead's frame.</summary>
    public static readonly Vector2 FoldMiddle = new(5.5f, 6.0f);
}
