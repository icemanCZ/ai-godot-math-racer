using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>The medal of a stage of the ferrywoman's rally, by the time driven; saves keep the time, not the medal.</summary>
public enum RallyMedal
{
    None,
    Bronze,
    Silver,
    Gold,
}

/// <summary>
/// The numbers of the ferrywoman's rally round the land beyond the old valley (<c>docs/RALLY_PLAN.md</c>): four stages
/// of the ring road, from one road to a border to the next, three gates with answers on each, and the medals and coins
/// a stage earns.
/// </summary>
internal static class RallyPlans
{
    /// <summary>The stages, one between every two roads to the borders, round the whole ring.</summary>
    public const int Stages = 4;

    /// <summary>The gates with three answers on every stage.</summary>
    public const int GatesPerStage = 3;

    /// <summary>The seconds added to the time of a stage for a gate driven through on a wrong answer, or missed.</summary>
    public const float PenaltySeconds = 5;

    /// <summary>The coins of a gate driven through on the right answer.</summary>
    public const int CoinsPerGate = 1;

    /// <summary>The coins of a stage finished, by its medal: bronze, silver and gold.</summary>
    public static int StageCoins(RallyMedal medal) =>
        medal switch
        {
            RallyMedal.Gold => 8,
            RallyMedal.Silver => 5,
            _ => 3,
        };

    /// <summary>The coins of a medal won on a stage for the first time, better than any before it.</summary>
    public const int NewMedalCoins = 10;

    /// <summary>The coins of all four stages driven one after another, the whole way round the valley in one go.</summary>
    public const int LapCoins = 15;

    /// <summary>
    /// The average speeds, in metres a second, that earn gold and silver (with the penalties counted). The car runs at
    /// 15 at most, so a clean drive on the road earns gold and a gentle one silver.
    /// </summary>
    public const float GoldSpeed = 11.5f,
        SilverSpeed = 8.5f;

    /// <summary>The medal of a time on a stage of the given length; no time, no medal.</summary>
    public static RallyMedal MedalFor(float seconds, float length) =>
        seconds <= 0 ? RallyMedal.None
        : seconds <= length / GoldSpeed ? RallyMedal.Gold
        : seconds <= length / SilverSpeed ? RallyMedal.Silver
        : RallyMedal.Bronze;

    /// <summary>The longest time a save keeps for a stage, an hour in tenths of a second.</summary>
    public const int LongestTenths = 36000;

    /// <summary>The best time of a stage as a save keeps it, in tenths of a second, as seconds.</summary>
    public static float Seconds(int tenths) => tenths / 10f;

    /// <summary>Seconds as a save keeps them, in whole tenths, never 0 for a stage that was driven.</summary>
    public static int Tenths(float seconds) =>
        System.Math.Clamp((int)System.MathF.Round(seconds * 10), 1, LongestTenths);

    /// <summary>The time of a stage as a child reads it: minutes, seconds and a tenth, such as "1:42,3".</summary>
    public static string Clock(float seconds)
    {
        int tenths = (int)System.MathF.Floor(System.MathF.Max(0, seconds) * 10);
        int minutes = tenths / 600;
        int rest = tenths % 600;
        return $"{minutes}:{rest / 10:00},{rest % 10}";
    }

    /// <summary>The borders whose roads begin and end a stage, as the ferrywoman names them: "od hor k řece".</summary>
    public static string StageName(Border from, Border to) => $"{From(from)} {To(to)}";

    private static string From(Border border) =>
        border switch
        {
            Border.Sea => "Od moře",
            Border.Mountains => "Od hor",
            Border.River => "Od řeky",
            _ => "Od lesa",
        };

    private static string To(Border border) =>
        border switch
        {
            Border.Sea => "k moři",
            Border.Mountains => "k horám",
            Border.River => "k řece",
            _ => "k lesu",
        };
}
