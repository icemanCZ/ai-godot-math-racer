using Godot;

namespace MathRacer.Buildings;

/// <summary>
/// Where the zoo stands: <see cref="Position"/> is the ring where the trailer tips out its load, <see cref="Road"/> the
/// road point the field path starts from, <see cref="Inward"/> the direction in which the zoo lies behind the ring and
/// <see cref="HelperSpot"/> where the keeper stands.
/// </summary>
internal sealed record ZooSite(Vector3 Position, Vector3 Road, Vector3 Inward, Vector3 HelperSpot)
{
    /// <summary>The zoo lies in this area (along the way inward, across it); plants and rocks there are taken out.</summary>
    public const float AlongMin = -5,
        AlongMax = 31,
        AcrossMax = 14;
}

/// <summary>
/// What the zoo needs: twelve deliveries of goods from the works, two for each of the six enclosures and always of two
/// different goods. The first delivery of a pair builds the fence and the ground of the enclosure, the second brings the
/// animal who lives in it and its fittings. Like the playground, the zoo only ever wants goods, never a raw load.
/// </summary>
internal static class ZooPlans
{
    /// <summary>Enclosures in the zoo.</summary>
    public const int Pens = 6;

    /// <summary>Deliveries that finish the zoo: two for every enclosure.</summary>
    public const int Trips = Pens * 2;

    /// <summary>Coins paid when the zoo is finished.</summary>
    public const int Coins = 60;

    private static readonly HaulLoad[] cargo =
    {
        // Penguins: gravel for the path and the rim of the pool, bricks for the wall. Monkeys: planks for the fence,
        // gravel for the ground. Elephant: bricks for the wall, planks for her house. Giraffe: planks for the fence,
        // bricks for the feeding tower. Flamingos: gravel for the pond, bricks for the bridge. Pandas: planks for the
        // fence, gravel for the bamboo grove.
        HaulLoad.Gravel,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Gravel,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Planks,
        HaulLoad.Bricks,
        HaulLoad.Gravel,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Gravel,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];

    /// <summary>The enclosure that a delivery works on.</summary>
    public static int PenOf(int step) => step / 2;

    /// <summary>What each delivery brings (accusative), for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "cestu a ohradu s bazénkem",
        "tučňáky a ledové skály",
        "ohradu pro opičky",
        "opičky a prolézačku",
        "ohradu pro slona",
        "slona a cihlový domek",
        "ohradu pro žirafu",
        "žirafu a krmicí věž",
        "rybníček pro plameňáky",
        "plameňáky a cihlový mostek",
        "ohradu pro pandy",
        "pandy a bambusový háj",
    };

    /// <summary>The name on the sign of each enclosure.</summary>
    public static readonly string[] Signs = { "Tučňáci", "Opičky", "Sloník", "Žirafa", "Plameňáci", "Pandy" };
}
