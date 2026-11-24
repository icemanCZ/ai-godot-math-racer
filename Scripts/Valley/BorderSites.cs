using Godot;

namespace MathRacer.Valley;

/// <summary>
/// Where the keeper of a border lives (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 1): at the end of the road to the
/// border, with the homestead's yard straight on beyond it. In the site's frame +Z (<see cref="Inward"/>) points on
/// along the road, away from the valley, and +X (<see cref="Right"/>) to the right of that.
/// </summary>
/// <param name="Border">The border the homestead stands at.</param>
/// <param name="Road">The last point of the road, where the car stops.</param>
/// <param name="Inward">The road's direction at its end, on the flat.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Inward"/>.</param>
internal sealed record BorderHomeSite(Border Border, Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>Where the yard of the homestead begins, measured on from the end of the road.</summary>
    public const float YardStart = 6;

    /// <summary>The yard: from <see cref="YardStart"/> on to this far beyond the end of the road, and this far to each side.</summary>
    public const float YardEnd = 24,
        HalfWidth = 9;

    /// <summary>The ground taken by the homestead, its keeper and the turning space at the end of the road.</summary>
    public const float AlongMin = -4,
        AlongMax = YardEnd + 2,
        AcrossMax = HalfWidth + 2;

    public Vector3 Right => Vector3.Up.Cross(Inward);

    /// <summary>A point of the site's frame (across, along) on the flat; its height is the road end's.</summary>
    public Vector3 Local(float across, float along) => Road + Right * across + Inward * along;

    /// <summary>Where the keeper waits: at the gate of the yard, a little to the right, looking down the road.</summary>
    public Vector3 KeeperSpot => Local(2.6f, 3.2f);

    /// <summary>The front middle of the yard, where the homestead's model has its origin.</summary>
    public Vector3 Yard => Local(0, YardStart);
}

/// <summary>
/// The lighthouse on the shore beyond the end of the road to the sea and the ring where its loads are tipped out, beside
/// the keeper's gate. <see cref="Foot"/> is on dry ground of the beach, outside the playable valley, so the tower is
/// seen from far but never driven into.
/// </summary>
internal sealed record LighthouseSite(Vector3 Foot, Vector3 Drop, Vector3 Road, float Yaw)
{
    /// <summary>The ground the lighthouse and its lodge take round <see cref="Foot"/>.</summary>
    public const float Radius = 9;
}

/// <summary>
/// The stone quarry on the coast: a rocky outcrop beside the road to the sea, near where it leaves the ring, where the
/// trailer is loaded with stone for the lighthouse. It has no worker; the lighthouse keeper sends the trailer there.
/// </summary>
internal sealed record CoastQuarrySite(Vector3 Position, Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>The ground the outcrop and its stone heaps take round <see cref="Position"/>.</summary>
    public const float Radius = 8;
}
