using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>What a delivery to the great line builds: the bed of the hub, its station, a stretch of an arm, a terminal or lamps.</summary>
internal enum LinePart
{
    HubBed,
    HubStation,
    Section,
    Terminal,
    Lamps,
}

/// <summary>One delivery to the great line: what is carried, to which arm (-1 for the hub) and what it builds.</summary>
internal readonly record struct LineStep(HaulLoad Cargo, int Arm, LinePart Part, int Section);

/// <summary>
/// The great railway across the valley (<c>docs/MAINLINE_PLAN.md</c>): a hub station at the edge of the old valley and
/// three arms, to the mountains, to the sea and to the border river, each with a terminal station. It is raised with
/// seventeen deliveries: two for the hub, and five for each arm, whose three stretches of track are laid one after
/// another, each one where the track before it ends, with gravel, planks and rails that the forge by the mountain slope
/// makes of stone, then bricks for the terminal station and glass for the lamps. A finished arm carries the car.
/// </summary>
internal static class MainLinePlans
{
    /// <summary>The arms, in the order they are built: to the mountains, to the sea and to the border river.</summary>
    public const int Arms = 3;

    /// <summary>Stretches of track an arm is laid in, each one delivery.</summary>
    public const int Sections = 3;

    /// <summary>Deliveries of the hub, and of each arm.</summary>
    public const int HubTrips = 2,
        ArmTrips = 5;

    /// <summary>Deliveries that finish the line.</summary>
    public const int Trips = HubTrips + Arms * ArmTrips;

    /// <summary>Coins paid for the hub, for each finished arm and for the whole line.</summary>
    public const int HubCoins = 10,
        ArmCoins = 20,
        Coins = 40;

    /// <summary>Rails the forge makes of one load of stone, and the most it holds.</summary>
    public const int RailsPerStone = 2,
        RailCapacity = 4;

    /// <summary>The questions of a ride cost nothing; the train is this fast, and no ride lasts longer than this.</summary>
    public const float TrainSpeed = 24,
        LongestRide = 42;

    /// <summary>The ride may be cut short by the pedals after this long.</summary>
    public const float RideGrace = 4;

    public static Border ArmBorder(int arm) =>
        arm switch
        {
            0 => Border.Mountains,
            1 => Border.Sea,
            _ => Border.River,
        };

    public static int ArmOfBorder(Border border) =>
        border switch
        {
            Border.Mountains => 0,
            Border.Sea => 1,
            Border.River => 2,
            _ => -1,
        };

    /// <summary>The delivery an arm starts with.</summary>
    public static int FirstStep(int arm) => HubTrips + arm * ArmTrips;

    /// <summary>The delivery of an arm that brings its rails, which the forge makes.</summary>
    public static int RailStep(int arm) => FirstStep(arm) + 2;

    private static readonly LineStep[] steps = Build();

    private static LineStep[] Build()
    {
        var list = new System.Collections.Generic.List<LineStep>
        {
            new(HaulLoad.Gravel, -1, LinePart.HubBed, 0),
            new(HaulLoad.Bricks, -1, LinePart.HubStation, 0),
        };
        for (int arm = 0; arm < Arms; arm++)
        {
            list.Add(new(HaulLoad.Gravel, arm, LinePart.Section, 0));
            list.Add(new(HaulLoad.Planks, arm, LinePart.Section, 1));
            list.Add(new(HaulLoad.Rails, arm, LinePart.Section, 2));
            list.Add(new(HaulLoad.Bricks, arm, LinePart.Terminal, 0));
            list.Add(new(HaulLoad.Glass, arm, LinePart.Lamps, 0));
        }
        return list.ToArray();
    }

    public static LineStep Step(int index) => steps[index];

    public static HaulLoad CargoOf(int step) => steps[step].Cargo;

    /// <summary>Whether the hub with its station stands after this many deliveries.</summary>
    public static bool HubStands(int stage) => stage >= HubTrips;

    /// <summary>Stretches of an arm that stand after this many deliveries, 0 to <see cref="Sections"/>.</summary>
    public static int SectionsBuilt(int stage, int arm) => System.Math.Clamp(stage - FirstStep(arm), 0, Sections);

    /// <summary>Whether the terminal station of an arm stands after this many deliveries.</summary>
    public static bool TerminalStands(int stage, int arm) => stage >= FirstStep(arm) + 4;

    /// <summary>Whether an arm is finished, its lamps lit, so that the train runs on it.</summary>
    public static bool ArmDone(int stage, int arm) => stage >= FirstStep(arm) + ArmTrips;

    /// <summary>The parts of the line that each delivery brings, for the speech bubble after it.</summary>
    public static readonly string[] Parts =
    {
        "násep a nástupiště velkého nádraží",
        "budovu velkého nádraží",
        "první úsek trati k horám",
        "druhý úsek trati k horám",
        "koleje na třetí úsek k horám",
        "stanici u hor",
        "lampy a semafory u hor",
        "první úsek trati k moři",
        "druhý úsek trati k moři",
        "koleje na třetí úsek k moři",
        "stanici u moře",
        "lampy a semafory u moře",
        "první úsek trati k řece",
        "druhý úsek trati k řece",
        "koleje na třetí úsek k řece",
        "stanici u řeky",
        "lampy a semafory u řeky",
    };

    /// <summary>The name of an arm in the sentences and on its signs.</summary>
    public static string ArmName(int arm) =>
        arm switch
        {
            0 => "k horám",
            1 => "k moři",
            _ => "k řece",
        };

    public static string ArmSign(int arm) =>
        arm switch
        {
            0 => "HORY",
            1 => "MOŘE",
            _ => "ŘEKA",
        };
}
