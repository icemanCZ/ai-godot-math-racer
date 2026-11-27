using Godot;

namespace MathRacer.Valley;

/// <summary>
/// Where the owl, the valley's mapmaker, has her stand (<c>docs/EXPLORER_PLAN.md</c>): beside the road where it first
/// leaves the old valley, the gate to the land beyond it. In the stand's frame +Z (<see cref="Inward"/>) points from the
/// road onto the stand and +X (<see cref="Right"/>) to its right; every point is on the flat.
/// </summary>
/// <param name="Road">The middle of the road beside the stand.</param>
/// <param name="Inward">From the road towards the stand, on the flat.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Inward"/>.</param>
internal sealed record CartographerSite(Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>The ground the stand takes, in its frame.</summary>
    public const float ZoneAlongMin = 5,
        ZoneAlongMax = 16,
        ZoneAcross = 8;

    public Vector3 Right => Vector3.Up.Cross(Inward);

    /// <summary>A point of the stand's frame (across to the right, along away from the road) on the flat.</summary>
    public Vector3 Local(float across, float along) => Road + Right * across + Inward * along;

    /// <summary>Where the owl stands, near the road, so that the car meets her from it.</summary>
    public Vector3 Talk => Local(-2.5f, 6.8f);

    /// <summary>Where the big map stands on its easel, beside her.</summary>
    public Vector3 Easel => Local(2.6f, 9.5f);
}

/// <summary>
/// One of the eight wonders of the land beyond the old valley (<c>docs/EXPLORER_PLAN.md</c>), beside a road so that the
/// car passes it: its kind (which is also its number), the middle of its ground, the way it faces (towards the road) and
/// the point of the road it is seen from.
/// </summary>
internal sealed record Landmark(int Kind, Vector3 Center, float Yaw, Vector3 Road)
{
    /// <summary>The direction it faces, from its middle towards the road, on the flat.</summary>
    public Vector3 Facing => new(Mathf.Sin(Yaw), 0, Mathf.Cos(Yaw));
}
