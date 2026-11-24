using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The balloon station (<c>docs/BALLOON_PLAN.md</c>): a mown launch field with a landing target beside a road of the land
/// beyond the old valley, on the highest ground the roads pass, far from the village. In the station's frame +Z
/// (<see cref="Inward"/>) points from the road onto the field and +X (<see cref="Right"/>) to its right; every point is on
/// the ground.
/// </summary>
/// <param name="Road">The middle of the road beside the station, where the field way leaves it.</param>
/// <param name="Inward">From the road towards the field, on the flat.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Inward"/>.</param>
internal sealed record BalloonSite(Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>How far from the middle of the road the middle of the field lies, where the basket stands.</summary>
    public const float PadAlong = 21;

    /// <summary>The radius of the landing target round the basket.</summary>
    public const float FieldRadius = 9;

    /// <summary>The ground the field, the hut, the windsock and the way from the road take, in the station's frame.</summary>
    public const float ZoneAlongMin = 2,
        ZoneAlongMax = 37,
        ZoneAcross = 14;

    /// <summary>Local +X of the station's frame, as <see cref="BorderHomeSite.Right"/> is of a homestead's.</summary>
    public Vector3 Right => Vector3.Up.Cross(Inward);

    /// <summary>A point of the station's frame (across to the right, along away from the road) on the flat.</summary>
    public Vector3 Local(float across, float along) => Road + Right * across + Inward * along;

    /// <summary>The middle of the field, where the basket stands and the balloon takes off and lands.</summary>
    public Vector3 Pad => Local(0, PadAlong);

    /// <summary>Where the balloonist stands, between the road and the field, so that the car meets her from the road.</summary>
    public Vector3 Talk => Local(-4.5f, 8.5f);
}
