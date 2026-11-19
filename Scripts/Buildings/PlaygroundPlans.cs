using Godot;

namespace MathRacer.Buildings;

/// <summary>
/// Where the playground stands: <see cref="Position"/> is the ring where the trailer tips out its load, <see cref="Road"/>
/// the road point the field path starts from, <see cref="Inward"/> the direction in which the yard lies behind the ring
/// and <see cref="HelperSpot"/> where the keeper stands.
/// </summary>
internal sealed record PlaygroundSite(Vector3 Position, Vector3 Road, Vector3 Inward, Vector3 HelperSpot)
{
    /// <summary>The yard lies in this area (along the way inward, across it); plants and rocks there are taken out.</summary>
    public const float AlongMin = -6,
        AlongMax = 21,
        AcrossMax = 14;
}

/// <summary>
/// What the playground needs: six deliveries of goods from the works, each of which brings one more toy and the child
/// who plays on it. Like the building plots, the playground only ever wants goods, never a raw load.
/// </summary>
internal static class PlaygroundPlans
{
    /// <summary>Deliveries that finish the playground.</summary>
    public const int Trips = 6;

    /// <summary>Coins paid when the playground is finished.</summary>
    public const int Coins = 20;

    private static readonly HaulLoad[] cargo =
    {
        // The path and the sandbox, the swings, the seesaw, the little brick house, the slide and the roundabout.
        HaulLoad.Gravel,
        HaulLoad.Planks,
        HaulLoad.Planks,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Bricks,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>The toy that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Toys =
    {
        "pískoviště",
        "houpačky",
        "houpací prkno",
        "domeček",
        "skluzavka",
        "kolotoč",
    };
}
