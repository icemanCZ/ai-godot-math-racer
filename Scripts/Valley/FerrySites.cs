using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The ferry to the bird island (<c>docs/FERRY_PLAN.md</c>). The landing stands beside the homestead of the keeper of the
/// river border, inside the valley; its pier runs from the edge of the valley down the bank to the water of the border
/// river, where the punt waits (<see cref="Berth"/>). The island lies in the sea before the river's mouth, beyond the
/// edge, with a pier of its own on the side of the river (<see cref="IslandBerth"/>) and the hide in its middle. In the
/// landing's frame +Z (<see cref="Inward"/>) points from the homestead's road end towards the border and +X
/// (<see cref="Right"/>) to its right.
/// </summary>
/// <param name="Landing">The middle of the landing's platform, where the heron waits and the loads are tipped out.</param>
/// <param name="Inward">From the end of the road towards the border, on the flat.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Inward"/>.</param>
/// <param name="Road">The end of the road to the river border, which the trailer comes along.</param>
/// <param name="PierStart">Where the pier leaves the valley, at its edge.</param>
/// <param name="Berth">Where the punt waits in the border river at the end of the pier.</param>
/// <param name="Island">The middle of the island, on its top.</param>
/// <param name="IslandBerth">Where the punt lands at the island's pier.</param>
/// <param name="Hide">The foot of the hide in the middle of the island.</param>
internal sealed record FerrySite(
    Vector3 Landing,
    Vector3 Inward,
    float Yaw,
    Vector3 Road,
    Vector3 PierStart,
    Vector3 Berth,
    Vector3 Island,
    Vector3 IslandBerth,
    Vector3 Hide
)
{
    /// <summary>How far the island reaches from its middle across the river's flow (x) and along it (z).</summary>
    public const float IslandHalfWidth = 30,
        IslandHalfLength = 22;

    /// <summary>The top of the island lies this far above the sea at high tide.</summary>
    public const float IslandRise = 1.4f;

    /// <summary>The ground the landing takes round its middle, kept off the plants.</summary>
    public const float LandingRadius = 7;

    public Vector3 Right => Vector3.Up.Cross(Inward);

    /// <summary>A point of the landing's frame (across to the right, along towards the border) on the flat.</summary>
    public Vector3 Local(float across, float along) => Landing + Right * across + Inward * along;

    /// <summary>Where the heron waits at the landing, on the ground in front of its deck, towards the road.</summary>
    public Vector3 Talk => Local(0, -4.4f);

    /// <summary>Where the trailer tips out its loads, in front of the landing.</summary>
    public Vector3 Drop => Local(0, -9);

    /// <summary>Where the car is put down again after the crossing home, facing the road.</summary>
    public Vector3 Return => Local(0, -16);

    /// <summary>
    /// How far a point lies inside the island's oval, as a fraction of it: less than 1 on the island, 1 at its edge. The
    /// oval is turned with the river, so <see cref="IslandHalfLength"/> runs along the flow (+Z, to the sea).
    /// </summary>
    public float IslandFraction(float x, float z)
    {
        float dx = (x - Island.X) / IslandHalfWidth,
            dz = (z - Island.Z) / IslandHalfLength;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    /// <summary>Where the car drives off the punt onto the island, facing its middle.</summary>
    public Vector3 IslandLanding => Island + new Vector3(0, 0, -IslandHalfLength + 6);

    /// <summary>Where the heron waits on the island, beside the hide, so that the car meets her there.</summary>
    public Vector3 IslandTalk => Hide + new Vector3(-4.5f, 0, -3);

    /// <summary>The eye of the telescope in the window of the hide.</summary>
    public Vector3 Eye => Hide + Vector3.Up * 5.4f;
}

/// <summary>A clump of reeds round the island that a bird may hide in: its number and the foot of the reeds.</summary>
internal sealed record ReedSpot(int Number, Vector3 Position);

/// <summary>A bird of a crossing: which of the four of it, its kind, the clump of reeds it hides in and how it is turned.</summary>
internal sealed record IslandBird(int Number, int Kind, int Spot, float Yaw);
