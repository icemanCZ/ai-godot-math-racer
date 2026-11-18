namespace MathRacer.Valley;

/// <summary>
/// The distances with which <see cref="World"/> places the benches, the signs and the characters, and keeps trees away
/// from them (metres). They decide where things stand in every valley, so changing one moves things in the valleys of
/// saved games too: a character stands where the seed puts it, not where a save says. The world fingerprints in
/// <c>Tests/Worlds</c> then change, and that is a decision to be made on purpose, not a side effect.
/// </summary>
internal static class WorldTuning
{
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
