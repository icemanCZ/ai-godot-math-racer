namespace MathRacer.Valley;

/// <summary>
/// The distances with which <see cref="World"/> places the benches, the signs and the characters, and keeps trees away
/// from them (metres). They decide where things stand in every valley, so changing one moves things in the valleys of
/// saved games too: a character stands where the seed puts it, not where a save says. The world fingerprints in
/// <c>Tests/Worlds</c> then change, and that is a decision to be made on purpose, not a side effect.
/// </summary>
internal static class WorldTuning
{
    // The valley as it was before it grew: its terrain grid covers the square of ±CoreRadius, and what its generation
    // places stands inside the circle of CorePlayableRadius. Everything there comes from the same code and the same
    // random numbers as before, which the world fingerprints hold, so these two never change and the generation of the
    // core never asks ValleyBounds, which is where the valley ends while it is played.
    public const float CoreRadius = 555f;
    public const float CorePlayableRadius = 525f;

    // The land round the core: its terrain is a grid as fine as the core's up to ±OutlandRadius, and coarse scenery
    // beyond it up to ±SceneryRadius, which nobody drives on. OutlandRadius lies a whole number of terrain chunks
    // (32 cells of 2.5 m) beyond the core, and both edges on the coarse 10 m grid, so that the grids meet without gaps.
    public const float OutlandRadius = 1275f;
    public const float SceneryRadius = 1675f;

    // Benches along the main road, at these distances from its start, to the side.
    public static readonly float[] BenchDistances = { 115f, 180f };
    public const float BenchOffset = 9f;

    // The signs "ZA KAMARÁDY" along the main road.
    public static readonly float[] SignDistances = { 22f, 140f };
    public const float SignOffset = 6f;

    // Trees keep this far from the witch's cauldron, the sorcerer's tower and the sorcerer.
    public const float WitchTreeClearing = 7f;
    public const float TowerTreeClearing = 18f;
    public const float SorcererTreeClearing = 8f;

    // The roadside friends stand beside the road.
    public const float FriendMinRoadStep = .1f;
    public const float FriendMaxRoadStep = 3f;
    public const float FriendVergeOffset = 6.5f;
    public const float FriendBorderMargin = 10f;
    public const float FriendRoadClearance = 5.5f;
    public const float FriendWitchClearance = 20f;
    public const float FriendSorcererClearance = 25f;
    public const float FriendRiverMargin = 7f;
    public const float FriendSpacing = 22f;

    // The witch stands on a verge between these distances along the main road, the lowest score winning.
    public const float WitchFirstRoadDistance = 192f;
    public const float WitchLastRoadDistance = 384f;
    public const float WitchRoadStep = 8f;
    public const float WitchVergeOffset = 5.3f;
    public const float WitchVillageEdgeGrow = 6f;
    public const float WitchDistancePenalty = .07f;
    public const float WitchVillagePenalty = 20f;
    public const float WitchRiverMargin = 8f;
    public const float WitchRiverPenalty = 100f;
}
