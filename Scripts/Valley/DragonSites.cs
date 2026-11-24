using Godot;

namespace MathRacer.Valley;

/// <summary>
/// Where the dragon lies (<c>docs/DRAGON_PLAN.md</c>): on a rock ledge beside the homestead under the mountains, facing
/// the valley, with his nest in front of his head and the ring where the loads are tipped out in front of the nest. In the
/// lair's frame +Z (<see cref="Facing"/>) points from the dragon towards the valley and +X (<see cref="Right"/>) to its
/// right; every point is on the ground.
/// </summary>
/// <param name="Lair">The middle of the dragon's body, on the ground.</param>
/// <param name="Facing">Where the dragon looks, on the flat: back down the road to the mountains.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Facing"/>.</param>
/// <param name="Road">The end of the road to the mountains, where the arrows of a trip end.</param>
/// <param name="FireSide">The side of the nest (+1 the dragon's right, −1 his left) where the fire is laid, away from the homestead.</param>
internal sealed record DragonSite(Vector3 Lair, Vector3 Facing, float Yaw, Vector3 Road, int FireSide)
{
    /// <summary>How far in front of the middle of the body the nest, the talking place and the drop ring lie.</summary>
    public const float NestAhead = 11,
        TalkAhead = 14.2f,
        DropAhead = 15.8f;

    /// <summary>The ground the ledge, the dragon, his cave, the nest and the drop ring take, in the lair's frame.</summary>
    public const float ZoneBack = -15,
        ZoneFront = DropAhead + 4,
        ZoneAcross = 8.5f;

    /// <summary>Local +X of the lair's frame, as <see cref="BorderHomeSite.Right"/> is of a homestead's.</summary>
    public Vector3 Right => Vector3.Up.Cross(Facing);

    /// <summary>A point of the lair's frame (across to the right, ahead towards the valley) on the flat.</summary>
    public Vector3 Local(float across, float ahead) => Lair + Right * across + Facing * ahead;

    /// <summary>The middle of the nest, in front of the dragon's head.</summary>
    public Vector3 Nest => Local(0, NestAhead);

    /// <summary>Where the trailer tips its load out, in front of the nest.</summary>
    public Vector3 Drop => Local(0, DropAhead);

    /// <summary>Where the car stops to talk to the dragon: beside the drop ring, under his chin.</summary>
    public Vector3 Talk => Local(-FireSide * 2.6f, TalkAhead);
}

/// <summary>
/// The mountain slope beside the road to the mountains, near where it leaves the ring: fallen trees, scree under a rock and
/// a sandy bank of a stream, where the trailer is loaded with everything the dragon's nest needs. It has no worker; the
/// dragon asks the loading question from afar, as the keeper of the sea does at the coast quarry.
/// </summary>
internal sealed record MountainSlopeSite(Vector3 Position, Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>The ground the logs, the scree and the sand take round <see cref="Position"/>.</summary>
    public const float Radius = 8;
}
