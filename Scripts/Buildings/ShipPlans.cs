namespace MathRacer.Buildings;

/// <summary>What a delivery to the ship builds.</summary>
internal enum ShipPart
{
    Keel,
    BowRibs,
    SternRibs,
    Hull,
    Deck,
    Cabin,
    Lanterns,
    Anchor,
    Masts,
    ForeSails,
    MainSails,
}

/// <summary>One delivery to the ship: what is carried and what it builds; <see cref="Row"/> is the strake of the hull.</summary>
internal readonly record struct ShipStep(HaulLoad Cargo, ShipPart Part, int Row);

/// <summary>
/// The ship Albatros of the shipyard on the coast (<c>docs/SHIP_PLAN.md</c>): fourteen deliveries raise it on the
/// slipway, logs for the keel, the ribs and the masts, planks for the hull, the deck and the cabin, glass for the
/// lanterns, the anchor that the forge of the great line makes of stone and the farmer's cloth for the sails; the last one
/// launches it. Then the player steers it along the buoys to the turtle island, where two turtles of each voyage sit on
/// treasures for the captain's cabin; the eight kinds of them fill the cabin.
/// </summary>
internal static class ShipPlans
{
    private static readonly ShipStep[] steps =
    {
        new(HaulLoad.Logs, ShipPart.Keel, 0),
        new(HaulLoad.Logs, ShipPart.BowRibs, 0),
        new(HaulLoad.Logs, ShipPart.SternRibs, 0),
        new(HaulLoad.Planks, ShipPart.Hull, 0),
        new(HaulLoad.Planks, ShipPart.Hull, 1),
        new(HaulLoad.Planks, ShipPart.Hull, 2),
        new(HaulLoad.Planks, ShipPart.Hull, 3),
        new(HaulLoad.Planks, ShipPart.Deck, 0),
        new(HaulLoad.Planks, ShipPart.Cabin, 0),
        new(HaulLoad.Glass, ShipPart.Lanterns, 0),
        new(HaulLoad.Anchor, ShipPart.Anchor, 0),
        new(HaulLoad.Logs, ShipPart.Masts, 0),
        new(HaulLoad.Fabric, ShipPart.ForeSails, 0),
        new(HaulLoad.Fabric, ShipPart.MainSails, 0),
    };

    /// <summary>Deliveries that finish the ship.</summary>
    public const int Trips = 14;

    /// <summary>The strakes of planks the hull is laid in, one delivery each.</summary>
    public const int Rows = 4;

    /// <summary>The delivery of the anchor and those of the sails.</summary>
    public const int AnchorStep = 10,
        FirstSailStep = 12;

    /// <summary>Coins for the launched ship, for each buoy passed, for a treasure and the first of a kind, and the full cabin.</summary>
    public const int Coins = 50,
        BuoyCoins = 1,
        SouvenirCoins = 2,
        NewKindCoins = 5,
        CabinCoins = 30;

    /// <summary>The most treasures of one kind a cabin may hold in a save.</summary>
    public const int MostOfAKind = 999;

    /// <summary>The kinds of treasure of the cabin, how many lie under the turtles of one voyage, and the nests.</summary>
    public const int Kinds = 8,
        PerVoyage = 2,
        Nests = 3;

    /// <summary>How near the ship must pass a buoy to ring it, and the car a turtle for its question.</summary>
    public const float BuoyReach = 15,
        TurtleReach = 4.5f;

    /// <summary>The ship is this fast with full sails, with half sails and furled; it turns this fast at full rudder.</summary>
    public const float FullSpeed = 11,
        HalfSpeed = 6.5f,
        FurledSpeed = 3.2f,
        TurnRate = .5f;

    /// <summary>The steering left alone this long hands the ship to the autopilot.</summary>
    public const float AutopilotAfter = 3;

    /// <summary>The ship keeps out of water shallower than this, looking this far ahead.</summary>
    public const float LeastDepth = 2.5f,
        LookAhead = 16;

    /// <summary>Near the island's pier the ship docks by itself; a voyage never lasts longer than this.</summary>
    public const float DockReach = 42,
        LongestVoyage = 240;

    /// <summary>The pedals cut the voyage home short after this long.</summary>
    public const float HomeGrace = 4;

    public static ShipStep Step(int index) => steps[index];

    public static HaulLoad CargoOf(int step) => steps[step].Cargo;

    /// <summary>The parts of the ship that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "kýl lodi",
        "žebra přídě",
        "žebra zádě",
        "první řadu prken trupu",
        "druhou řadu prken trupu",
        "třetí řadu prken trupu",
        "poslední řadu prken trupu",
        "palubu se zábradlím",
        "kajutu a kormidlo",
        "okna kajuty a lucerny",
        "kotvu",
        "stěžně a ráhna",
        "přední plachty",
        "hlavní plachty",
    };

    /// <summary>The treasures of the cabin.</summary>
    public static readonly string[] Names =
    {
        "lahev se vzkazem",
        "kompas",
        "lodní zvon",
        "dalekohled",
        "kokos",
        "ananas",
        "korál",
        "truhlička s dukáty",
    };

    /// <summary>How often each treasure lies under a turtle: coconuts and pineapples often, the chest seldom.</summary>
    public static readonly int[] Weights = { 4, 4, 3, 3, 6, 6, 3, 1 };

    public static string Name(int kind) => Names[kind];
}
