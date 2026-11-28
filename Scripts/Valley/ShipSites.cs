using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// Where the shipyard on the coast and the turtle island are (<c>docs/SHIP_PLAN.md</c>). The frame of the yard has +Z
/// (<see cref="Seaward"/>) out to sea, square to the waterline, and +X (<see cref="Along"/>) along the coast. Everything
/// the car or the captain stands on is inside the valley, on the edge above the beach; the slipway, the pier and the ship
/// lie on the beach and in the water below it, and the island far out at sea.
/// </summary>
/// <param name="Waterline">Where the slipway meets the water at high tide, on the sea's level.</param>
/// <param name="Seaward">Out to sea, on the flat.</param>
/// <param name="Edge">The middle of the yard's ground on the edge of the valley above the slipway.</param>
/// <param name="Road">The point of a road the way to the yard starts from.</param>
/// <param name="Island">The middle of the turtle island, on its top.</param>
/// <param name="Buoys">The buoys that show the way from the anchorage to the island, in order.</param>
internal sealed record ShipSite(
    Vector3 Waterline,
    Vector3 Seaward,
    Vector3 Edge,
    Vector3 Road,
    Vector3 Island,
    Vector3[] Buoys
)
{
    /// <summary>The ground of the yard on the edge: this far round <see cref="Edge"/>.</summary>
    public const float YardRadius = 13;

    /// <summary>The ship at anchor lies this far out from the waterline.</summary>
    public const float AnchorOut = 58;

    /// <summary>The turtle island: half its length along the coast, half its width, and how high its top stands over the sea.</summary>
    public const float IslandHalfLength = 62,
        IslandHalfWidth = 40,
        IslandRise = 1.8f;

    public Vector3 Along => Vector3.Up.Cross(Seaward);

    public float Yaw => Mathf.Atan2(Seaward.X, Seaward.Z);

    /// <summary>The heading of a ship that lies along the coast, its bow towards +<see cref="Along"/>.</summary>
    public float AlongYaw => Mathf.Atan2(Along.X, Along.Z);

    /// <summary>A point of the yard's frame (along the coast, out to sea) from the waterline, on the sea's level.</summary>
    public Vector3 Local(float along, float seaward) => Waterline + Along * along + Seaward * seaward;

    /// <summary>A point of the frame round the yard's ground on the edge, at the edge's height.</summary>
    public Vector3 OnEdge(float along, float seaward) => Edge + Along * along + Seaward * seaward;

    /// <summary>Where the loads are tipped out: on the edge, at the head of the slipway.</summary>
    public Vector3 Drop => OnEdge(0, 3);

    /// <summary>Where the captain waits beside the yard, looking at the road.</summary>
    public Vector3 Talk => OnEdge(7, 1);

    /// <summary>Where the car is put down after the voyage home, a little off the captain.</summary>
    public Vector3 Return => OnEdge(-8, -3);

    /// <summary>The ship at anchor: its middle on the water, lying along the coast.</summary>
    public Vector3 Anchorage => Local(0, AnchorOut);

    /// <summary>Where the pier from the beach to the anchorage begins.</summary>
    public Vector3 PierFoot => Local(-9, -4);

    /// <summary>The end of the pier beside the ship at anchor.</summary>
    public Vector3 PierHead => Local(-9, AnchorOut - 5);

    /// <summary>Where the ship lies at the island's pier, on the side of the coast.</summary>
    public Vector3 IslandBerth => Island - Seaward * (IslandHalfWidth + 9);

    /// <summary>Where the car drives off onto the island, and where the captain waits there.</summary>
    public Vector3 IslandLanding => Island - Seaward * (IslandHalfWidth - 8);

    public Vector3 IslandTalk => IslandLanding + Along * 5 + Seaward * 2;

    /// <summary>How far a point lies out in the island's oval: below 1 on it.</summary>
    public float IslandFraction(Vector3 p)
    {
        var d = p - Island;
        float along = d.Dot(Along) / IslandHalfLength,
            out_ = d.Dot(Seaward) / IslandHalfWidth;
        return Mathf.Sqrt(along * along + out_ * out_);
    }

    /// <summary>The three nests of the turtles on the island's beach, round from the landing through the open sea.</summary>
    public Vector3 Nest(int nest)
    {
        float angle = Mathf.DegToRad(
            nest switch
            {
                0 => 62,
                1 => 180,
                _ => 298,
            }
        );
        // Angle 0 is the landing's side, towards the coast.
        return Island
            + Along * (Mathf.Sin(angle) * IslandHalfLength * .62f)
            - Seaward * (Mathf.Cos(angle) * IslandHalfWidth * .6f);
    }
}

/// <summary>
/// The way of a voyage: the ship sails from the anchorage along the buoys to the island's pier. The buoys are only a way
/// to show; the ship goes where it is steered.
/// </summary>
internal static class ShipRoute
{
    /// <summary>Points about <paramref name="step"/> apart along a polyline through <paramref name="corners"/>, ends left out.</summary>
    internal static Vector3[] Along(IReadOnlyList<Vector3> corners, float step)
    {
        var points = new List<Vector3>();
        float length = 0;
        for (int i = 1; i < corners.Count; i++)
            length += (corners[i] - corners[i - 1]).Length();
        int count = Mathf.Max(2, Mathf.RoundToInt(length / step));
        for (int k = 1; k < count; k++)
        {
            float target = length * k / count;
            for (int i = 1; i < corners.Count; i++)
            {
                float piece = (corners[i] - corners[i - 1]).Length();
                if (target <= piece)
                {
                    points.Add(corners[i - 1].Lerp(corners[i], target / piece));
                    break;
                }
                target -= piece;
            }
        }
        return points.ToArray();
    }
}
