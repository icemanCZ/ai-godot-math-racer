using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace MathRacer;

// Plain data only: scene nodes are regenerated from the saved world seed.
public sealed class SaveData
{
    /// <summary>
    /// Version 3 pays coins instead of offering car rewards. Version 2 stores the open question in
    /// <see cref="Challenge"/>; version 1 used one field per kind.
    /// </summary>
    public const int CurrentVersion = 3;

    public PostcardProgress?[] Postcards { get; set; } = new PostcardProgress?[Game.PostcardCount];
    public int PostcardPage { get; set; } = -1;
    public int EventActive { get; set; } = -1;
    public int EventNext { get; set; }
    public int[] EventWins { get; set; } = new int[2];

    /// <summary>Gate times of the player's record per course; empty while the pace ghost is still unbeaten.</summary>
    public float[][] EventBest { get; set; } = new[] { Array.Empty<float>(), Array.Empty<float>() };

    /// <summary>Seconds on the clock of the active run, which has one gate time in <see cref="EventSplits"/> per gate passed.</summary>
    public float EventTime { get; set; }
    public float[] EventSplits { get; set; } = Array.Empty<float>();
    public bool GolemOwned { get; set; }
    public bool GolemActive { get; set; }

    // Before the buildings had ten deliveries each, only the bridge existed with six. These fields are read only
    // to migrate older saves into BuildStages.
    public int FordStage { get; set; }
    public int FordTrip { get; set; } = -1;
    public bool FordTripLoaded { get; set; }

    /// <summary>Deliveries made to the bridge, the mill, the tower and the jetty, 0–10 each; null in older saves.</summary>
    public int[]? BuildStages { get; set; }

    /// <summary>The building of the active trailer trip, whose step is its stage, or -1.</summary>
    public int BuildTrip { get; set; } = -1;
    public bool BuildTripLoaded { get; set; }

    /// <summary>Goods stored at the sawmill, brickworks and gravel works (<see cref="Product"/> order), 0–12 each.</summary>
    public int[] WorksStock { get; set; } = new int[3];

    /// <summary>The works the active supply trip brings raw material to, or -1. Never set together with a building trip.</summary>
    public int SupplyTrip { get; set; } = -1;
    public bool SupplyTripLoaded { get; set; }

    /// <summary>The house whose planks or bricks the active trailer trip fetches, or -1. Its task is the house's next one.</summary>
    public int HouseTrip { get; set; } = -1;
    public bool HouseTripLoaded { get; set; }

    /// <summary>Deliveries made to each of the six building plots, 0–6; zeros in saves from before the plots.</summary>
    public int[] PlotStages { get; set; } = new int[PlotPlans.Count];

    /// <summary>The plot whose next delivery the active trailer trip fetches, or -1. Never set together with another trip.</summary>
    public int PlotTrip { get; set; } = -1;
    public bool PlotTripLoaded { get; set; }
    public bool TrailerOwned { get; set; }
    public bool TrailerAttached { get; set; }
    public string TrailerPaint { get; set; } = "red";
    public string[] DisabledCollectorPets { get; set; } = Array.Empty<string>();
    public int Version { get; set; } = CurrentVersion;
    public QuizType QuizTypes { get; set; } = QuizType.All;
    public bool ThreeTermMath { get; set; }
    public int NextQuiz { get; set; }
    public bool HandoverQuiz { get; set; }
    public int BanditSite { get; set; } = -1;
    public int BanditSolved { get; set; }
    public float BanditCooldown { get; set; }
    public int Seed { get; set; }
    public float[] Position { get; set; } = new float[3];
    public float Heading { get; set; }
    public GameState State { get; set; }
    public int Nearby { get; set; } = -1;
    public bool[] Animals { get; set; } = Array.Empty<bool>();
    public int[] HouseLevels { get; set; } = new int[4];
    public int FarmCount { get; set; }
    public int GardenCount { get; set; }
    public int GemCount { get; set; }
    public int[] TradeCounts { get; set; } = new int[3];
    public int ToyCount { get; set; }
    public int[] Unlocked { get; set; } = Array.Empty<int>();
    public int[] Completed { get; set; } = Array.Empty<int>();
    public CarReward[] Owned { get; set; } = Array.Empty<CarReward>();
    public int Coins { get; set; }

    /// <summary>The coins shown on the open reward panel; they are already counted in <see cref="Coins"/>.</summary>
    public int RewardCoins { get; set; }
    public string Look { get; set; } = "";
    public string Model { get; set; } = "classic";
    public string[] Accessories { get; set; } = Array.Empty<string>();
    public int Sender { get; set; } = -1;
    public bool Collection { get; set; }
    public bool[] Picked { get; set; } = Array.Empty<bool>();
    public bool Arrived { get; set; }
    public bool DeliveryReward { get; set; }

    /// <summary>The open question, if any.</summary>
    public IChallenge? Challenge { get; set; }
    public SyllableSelection SyllableSelection { get; set; }
    public int[] Rejected { get; set; } = Array.Empty<int>();

    // Version 1 fields, read only to migrate older saves into Challenge.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MathChallenge? Math { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LetterChallenge? Letters { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SyllableChallenge? Syllables { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReadingChallenge? Reading { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeliveryChallenge? DeliveryQuestion { get; set; }
}

public static class SaveStore
{
    private const int LegacyAnimalCount = 30;

    public static bool Enabled => LaunchOptions.UsesPlayerSave;
    public static string SavePath => ProjectSettings.GlobalizePath("user://progress.json");
    private static readonly Lazy<SaveData?> initial = new(() => Enabled ? Read(SavePath) : null);
    public static SaveData? Initial => initial.Value;

    internal static SaveData? Read(string path)
    {
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate))
                    continue;
                var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(candidate))
                    ?? throw new JsonException("Empty save data.");
                MigrateLegacyQuestion(data);
                MigrateRewardOffer(data);
                MigrateBuilds(data);
                MigrateEventRun(data);
                MigrateHouseTasks(data);
                Validate(data);
                if (data.Postcards.Length == Game.OriginalPostcardCount)
                {
                    var expanded = new PostcardProgress?[Game.PostcardCount];
                    data.Postcards.CopyTo(expanded, 0);
                    data.Postcards = expanded;
                }
                MigrateAnimalCount(data);
                return data;
            }
            catch (Exception error)
                when (error is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                GD.PushWarning($"Cannot load {candidate}: {error.Message}");
            }
        }
        return null;
    }

    private static void Validate(SaveData data)
    {
        if (
            data.Version != SaveData.CurrentVersion
            || data.Seed <= 0
            || data.Seed >= int.MaxValue - 4096
            || HasInvalidWorldProgress(data)
            || HasInvalidReferences(data)
            || HasInvalidQuizState(data)
            || HasInvalidTradeProgress(data)
            || HasInvalidVillageProgress(data)
            || HasInvalidSpecialActorState(data)
            || HasInvalidPlotState(data)
            || HasInvalidBuildState(data)
            || HasInvalidWorksState(data)
            || HasInvalidHouseTrip(data)
            || HasInvalidPickupCount(data)
        )
            throw new JsonException("Invalid save data.");
    }

    private static bool HasInvalidWorldProgress(SaveData data) =>
        data.Postcards == null
        || (data.Postcards.Length != Game.PostcardCount && data.Postcards.Length != Game.OriginalPostcardCount)
        || data.Postcards.Where(
                (card, i) =>
                    card != null
                    && (
                        card.Jpeg == null
                        || card.Jpeg.Length > 300000
                        || !Game.ValidPostcardSelection(i, data.Seed, card)
                    )
            )
            .Any()
        || data.PostcardPage < -1
        || data.PostcardPage >= data.Postcards.Length
        || (data.PostcardPage >= 0 && data.Postcards[data.PostcardPage] == null)
        || data.EventActive < -1
        || data.EventActive > 1
        || data.EventNext < 0
        || data.EventNext >= DrivingEvents.GateCount
        || data.EventWins == null
        || data.EventWins.Length != 2
        || data.EventWins.Any(w => w < 0 || w > 9999)
        || data.EventBest == null
        || data.EventBest.Length != 2
        || data.EventBest.Any(best => best.Length != 0 && !DrivingEvents.ValidSplits(best))
        || data.EventSplits == null
        || data.EventSplits.Length != data.EventNext
        || (data.EventActive < 0 && (data.EventNext != 0 || data.EventTime != 0))
        || !float.IsFinite(data.EventTime)
        || data.EventTime < 0
        || data.EventTime > 3600
        || data.EventSplits.Any(t => !float.IsFinite(t) || t < 0 || t > data.EventTime)
        || data.Position == null
        || data.Position.Length != 3
        || data.Position.Any(p => !float.IsFinite(p))
        || !float.IsFinite(data.Heading)
        || !Enum.IsDefined(data.State)
        || data.State == GameState.Title
        || data.State == GameState.Paused;

    private static bool HasInvalidReferences(SaveData data) =>
        data.Animals == null
        || (data.Animals.Length != World.QuestAnimalCount && data.Animals.Length != LegacyAnimalCount)
        || data.Unlocked == null
        || data.Completed == null
        || data.Owned == null
        || data.Coins < 0
        || data.RewardCoins < 0
        || (data.RewardCoins > 0 && data.State != GameState.Reward)
        || data.Accessories == null
        || data.Picked == null
        || data.Rejected == null
        || data.Nearby < -1
        || data.Nearby > Math.Max(Game.BanditActorId, World.LastPlotResidentId)
        || data.Sender < -1
        || data.Sender > World.SorcererId
        || data.Sender == World.WitchId
        || data.Unlocked.Concat(data.Completed).Any(i => i < 0 || i >= data.Animals.Length)
        || (data.Nearby >= data.Animals.Length && data.Nearby < World.FirstResidentId)
        || (data.Sender >= data.Animals.Length && data.Sender < World.FirstResidentId);

    private static bool HasInvalidQuizState(SaveData data) =>
        data.QuizTypes == QuizType.None
        || (data.QuizTypes & ~QuizType.All) != 0
        || data.NextQuiz < 0
        || data.NextQuiz >= Game.QuizRotationLength
        || (data.Challenge is ReadingChallenge reading && !reading.IsValid())
        || data.BanditSite is < -1 or >= Game.MaxBanditSites
        || data.BanditSolved is < 0 or >= Game.BanditQuestionCount
        || !float.IsFinite(data.BanditCooldown)
        || data.BanditCooldown is < 0 or > Game.BanditCooldownTime
        || (
            data.Nearby == Game.BanditActorId
                ? data.State != GameState.Challenge
                    || data.BanditSite < 0
                    || data.BanditCooldown != 0
                    || data.HandoverQuiz
                    || data.Challenge is DeliveryChallenge
                : data.BanditSolved != 0
        )
        || (
            data.HandoverQuiz
            && (
                data.State != GameState.Challenge
                || data.Sender < 0
                || !data.Arrived
                || data.DeliveryReward
                // A handover question replaces cargo counting with a word quiz.
                || data.Challenge is not (LetterChallenge or SyllableChallenge or ReadingChallenge)
            )
        )
        || (
            data.Challenge is SyllableChallenge syllables
                ? !syllables.IsValid() || !data.SyllableSelection.IsValid(syllables)
                : data.SyllableSelection != default
        )
        || (data.State is GameState.Challenge or GameState.Reward or GameState.ExpeditionChoice && data.Nearby < 0)
        || (data.State == GameState.Challenge && data.Challenge == null);

    private static bool HasInvalidTradeProgress(SaveData data) =>
        data.TradeCounts == null
        || data.TradeCounts.Length != 3
        || data.TradeCounts.Any(c => c < 0 || c > 1000000)
        || (data.Sender >= World.FirstTraderId && data.Sender < World.WitchId && data.Collection)
        // A shop shows its status while another task is under way: an expedition or a ford trip.
        || (
            data.State == GameState.TradeStatus
            && (
                data.Nearby < World.FirstTraderId
                || (
                    data.Sender < 0
                    && data.BuildTrip < 0
                    && data.SupplyTrip < 0
                    && data.HouseTrip < 0
                    && data.PlotTrip < 0
                )
            )
        )
        || (
            data.Nearby >= World.FirstTraderId
            && data.Nearby < World.WitchId
            && data.State == GameState.Challenge
            // The opening quiz may run while another task is active; a shop never quizzes about its own order.
            && (data.Sender == data.Nearby || data.Challenge is DeliveryChallenge)
        )
        || (
            data.Sender >= World.FirstTraderId
            && data.Sender < World.WitchId
            && data.DeliveryReward
            && (data.State != GameState.Reward || !data.Arrived)
        );

    private static bool HasInvalidVillageProgress(SaveData data) =>
        data.GemCount < 0
        || data.GemCount > Village.GemNames.Length
        || data.ToyCount < 0
        || data.ToyCount > Village.ToyNames.Length
        || (
            data.Sender == World.GemCollectorId
            && (data.GemCount == Village.GemNames.Length || !data.Collection || data.DeliveryReward)
        )
        || (
            data.Sender == World.ToymakerId
            && (data.ToyCount == Village.ToyNames.Length || !data.Collection || data.DeliveryReward)
        )
        || (data.State == GameState.CollectionStatus && data.Nearby is not (World.GemCollectorId or World.ToymakerId))
        || data.GardenCount < 0
        || data.GardenCount > Village.FlowerNames.Length
        || (
            data.Sender == World.GardenerId
            && (data.GardenCount == Village.FlowerNames.Length || !data.Collection || data.DeliveryReward)
        )
        || (data.State == GameState.GardenStatus && data.Nearby != World.GardenerId)
        || data.FarmCount < 0
        || data.FarmCount > Village.FarmKinds.Length
        || (
            data.Sender == World.FarmerId
            && (data.FarmCount == Village.FarmKinds.Length || !data.Collection || data.DeliveryReward)
        )
        || (data.State == GameState.FarmStatus && data.Nearby != World.FarmerId)
        || data.HouseLevels == null
        || data.HouseLevels.Length != 4
        || data.HouseLevels.Any(level => level < 0 || level > Village.Upgrades.Length)
        || (
            data.State == GameState.HouseReward
            && (data.Nearby < World.FirstResidentId || data.Nearby >= World.FarmerId)
        )
        || (
            data.Sender >= World.FirstResidentId
            && data.Sender < World.FirstTraderId
            && (!data.Collection || data.DeliveryReward)
        )
        || (
            data.State == GameState.HouseCollection
            && (data.Sender < World.FirstResidentId || data.Sender >= World.FarmerId || data.Nearby != data.Sender)
        )
        || (
            data.State == GameState.HouseFruitOffer
            && (
                data.Nearby < World.FirstResidentId
                || data.Nearby >= World.FarmerId
                || data.HouseLevels[data.Nearby - World.FirstResidentId] >= Village.Upgrades.Length
            )
        )
        || (
            data.State == GameState.HouseMaterialOffer
            && (
                data.Nearby < World.FirstResidentId
                || data.Nearby >= World.FarmerId
                || SavedTask(data, data.Nearby - World.FirstResidentId)
                    is not (VillageTaskKind.Planks or VillageTaskKind.Bricks)
            )
        )
        || (
            data.Nearby >= World.FirstResidentId
            && data.Nearby < World.FirstTraderId
            && data.State is GameState.Reward or GameState.ExpeditionChoice
        )
        || (
            data.Nearby == World.FarmerId
            && data.State == GameState.Challenge
            && (
                data.Sender == World.FarmerId
                || data.FarmCount == Village.FarmKinds.Length
                || data.Challenge is DeliveryChallenge
            )
        )
        || (
            data.Nearby == World.GardenerId
            && data.State == GameState.Challenge
            && (
                data.Sender == World.GardenerId
                || data.GardenCount == Village.FlowerNames.Length
                || data.Challenge is DeliveryChallenge
            )
        )
        || (
            data.Nearby >= World.GemCollectorId
            && data.Nearby < World.FirstTraderId
            && data.State == GameState.Challenge
            && (
                data.Sender == data.Nearby
                || (data.Nearby == World.GemCollectorId ? data.GemCount : data.ToyCount)
                    == Village.CollectionNames(data.Nearby == World.ToymakerId).Length
                || data.Challenge is DeliveryChallenge
            )
        );

    private static bool HasInvalidSpecialActorState(SaveData data) =>
        (data.GolemActive && !data.GolemOwned)
        || (data.Sender == World.SorcererId && (!data.Collection || data.GolemOwned || data.DeliveryReward))
        || (data.State == GameState.SorcererStatus && data.Nearby != World.SorcererId)
        || (data.State == GameState.WeatherChoice && data.Nearby != World.WitchId)
        || (
            data.Nearby == World.WitchId
            && (
                data.State is not (GameState.Exploring or GameState.Challenge or GameState.WeatherChoice)
                || (data.State == GameState.Challenge && data.Challenge is DeliveryChallenge)
            )
        );

    /// <summary>
    /// The progress of the four buildings, the trailer trip for one of them and the character met at the moment.
    /// The trailer is the bridge's reward; a trip is never set together with an expedition or a supply trip.
    /// </summary>
    private static bool HasInvalidBuildState(SaveData data) =>
        data.BuildStages == null
        || data.BuildStages.Length != BuildPlans.Count
        || data.BuildStages.Any(stage => stage < 0 || stage > BuildPlans.Trips)
        || data.BuildTrip < -1
        || data.BuildTrip >= BuildPlans.Count
        || (
            data.BuildTrip >= 0
            && (
                data.BuildStages[data.BuildTrip] >= BuildPlans.Trips
                || data.Sender >= 0
                || data.SupplyTrip >= 0
                || data.HouseTrip >= 0
            )
        )
        || (data.BuildTripLoaded && data.BuildTrip < 0)
        || (data.TrailerOwned && data.BuildStages[BuildPlans.Bridge] < BuildPlans.Trips)
        || (data.TrailerAttached && !data.TrailerOwned)
        || data.TrailerPaint == null
        || !Array.Exists(TrailerModel.Paints, paint => paint.Id == data.TrailerPaint)
        || HasInvalidAsker(data);

    /// <summary>
    /// The progress of the six building plots and the trailer trip for one of them. A trip fetches the plot's next
    /// delivery, so that plot must be unfinished, and it never runs together with an expedition or another trip.
    /// </summary>
    private static bool HasInvalidPlotState(SaveData data) =>
        data.PlotStages == null
        || data.PlotStages.Length != PlotPlans.Count
        || data.PlotStages.Any(stage => stage < 0 || stage > PlotPlans.Trips)
        || data.PlotTrip < -1
        || data.PlotTrip >= PlotPlans.Count
        || (data.PlotTripLoaded && data.PlotTrip < 0)
        || (
            data.PlotTrip >= 0
            && (
                data.PlotStages[data.PlotTrip] >= PlotPlans.Trips
                || data.Sender >= 0
                || data.BuildTrip >= 0
                || data.SupplyTrip >= 0
                || data.HouseTrip >= 0
            )
        );

    /// <summary>The cargo and the character who asks about it for the active trailer trip, or (-1, -1) without one.</summary>
    private static (HaulLoad Cargo, int[] Askers, bool Loaded) ActiveTripAsker(SaveData data)
    {
        if (data.BuildTrip is >= 0 and < BuildPlans.Count && data.BuildStages![data.BuildTrip] < BuildPlans.Trips)
        {
            var cargo = BuildPlans.For(data.BuildTrip)[data.BuildStages![data.BuildTrip]].Cargo;
            return (cargo, World.AskerIds(cargo, data.BuildTrip), data.BuildTripLoaded);
        }
        if (data.SupplyTrip is >= 0 and < WorksCatalog.Count)
        {
            var cargo = WorksCatalog.All[data.SupplyTrip].Input;
            return (cargo, World.AskerIds(cargo, 0), data.SupplyTripLoaded);
        }
        if (
            data.PlotTrip is >= 0 and < PlotPlans.Count
            && data.PlotStages is { Length: PlotPlans.Count } plotStages
            && plotStages[data.PlotTrip] is >= 0 and < PlotPlans.Trips
        )
        {
            var cargo = PlotPlans.CargoOf(plotStages[data.PlotTrip]);
            return (cargo, World.AskerIds(cargo, 0), data.PlotTripLoaded);
        }
        if (data.HouseTrip >= 0 && SavedTask(data, data.HouseTrip) is { } kind && Village.MaterialOf(kind) is { } goods)
            return (goods, World.AskerIds(goods, 0), data.HouseTripLoaded);
        return (default, Array.Empty<int>(), false);
    }

    /// <summary>The task of a house's next improvement in a saved valley; null for a finished house or damaged levels.</summary>
    private static VillageTaskKind? SavedTask(SaveData data, int house) =>
        data.Seed > 0
        && data.HouseLevels is { Length: 4 } levels
        && house is >= 0 and < 4
        && levels[house] is >= 0 and < 10
            ? Village.CreateTasks(data.Seed, house)[levels[house]]
            : null;

    /// <summary>
    /// A house's trailer trip fetches the goods of its next task, so that house must still be unfinished and ask for
    /// planks or bricks. It never runs together with an expedition or another trailer trip.
    /// </summary>
    private static bool HasInvalidHouseTrip(SaveData data) =>
        data.HouseTrip < -1
        || data.HouseTrip >= 4
        || (data.HouseTripLoaded && data.HouseTrip < 0)
        || (
            data.HouseTrip >= 0
            && (
                data.Sender >= 0
                || data.BuildTrip >= 0
                || data.SupplyTrip >= 0
                || SavedTask(data, data.HouseTrip) is not (VillageTaskKind.Planks or VillageTaskKind.Bricks)
            )
        );

    /// <summary>
    /// Houses used to ask only quizzes and apples. A save that waits at a house whose next task is now different
    /// (an apple offer, or a quiz where the house now wants goods) goes back to driving, so the rest of it is kept.
    /// </summary>
    private static void MigrateHouseTasks(SaveData data)
    {
        if (data.Nearby is < World.FirstResidentId or >= World.FarmerId || data.Sender >= 0)
            return;
        var task = SavedTask(data, data.Nearby - World.FirstResidentId);
        bool stale =
            data.State == GameState.HouseFruitOffer && task != VillageTaskKind.Fruit
            || data.State == GameState.Challenge
                && data.Challenge is not null and not LoadChallenge
                && Village.MaterialOf(task ?? VillageTaskKind.Math) != null;
        if (!stale)
            return;
        data.State = GameState.Exploring;
        data.Nearby = -1;
        ClearQuestion(data);
    }

    /// <summary>
    /// Builders, works workers and source workers show a panel of their own and, during a trailer trip, ask the
    /// loading question. A question can only be open for the trip's own asker, and only before the load is aboard.
    /// </summary>
    private static bool HasInvalidAsker(SaveData data)
    {
        GameState? panel =
            World.IsBuilder(data.Nearby) ? GameState.BuildStatus
            : data.Nearby is >= World.FirstWorksId and <= World.LastWorksId ? GameState.WorksStatus
            : data.Nearby is >= World.FirstSourceId and <= World.LastSourceId ? GameState.SourceStatus
            : World.IsPlotResident(data.Nearby) ? GameState.PlotStatus
            : null;
        if (panel == null)
        {
            // The panels belong to their characters, and a loading question needs one of them to ask it.
            return data.State
                    is GameState.BuildStatus
                        or GameState.WorksStatus
                        or GameState.SourceStatus
                        or GameState.PlotStatus
                || data.Challenge is LoadChallenge;
        }
        if (data.State is not (GameState.Exploring or GameState.Challenge) && data.State != panel)
            return true;
        var (cargo, askers, loaded) = ActiveTripAsker(data);
        if (data.State == GameState.Challenge)
        {
            if (!askers.Contains(data.Nearby) || loaded || data.HandoverQuiz || data.Challenge is DeliveryChallenge)
                return true;
        }
        return data.Challenge is LoadChallenge load && (!load.IsValid() || askers.Length == 0 || load.Cargo != cargo);
    }

    /// <summary>
    /// The stock of the three works, the trailer trip one of them sent and the worker met at the moment.
    /// The stock never exceeds the yard. A supply trip is started by, and its question asked by, the worker at the source.
    /// </summary>
    private static bool HasInvalidWorksState(SaveData data) =>
        data.WorksStock == null
        || data.WorksStock.Length != WorksCatalog.Count
        || data.WorksStock.Any(stock => stock < 0 || stock > Works.Capacity)
        || data.SupplyTrip < -1
        || data.SupplyTrip >= WorksCatalog.Count
        || (data.SupplyTrip >= 0 && (data.BuildTrip >= 0 || data.Sender >= 0 || data.HouseTrip >= 0))
        || (data.SupplyTrip >= 0 && data.WorksStock[data.SupplyTrip] >= Works.Capacity)
        || (data.SupplyTripLoaded && data.SupplyTrip < 0);

    private static bool HasInvalidPickupCount(SaveData data) =>
        (
            data.Sender >= 0
            && data.Picked.Length
                != (
                    data.Sender == World.SorcererId ? 6
                    : data.Sender >= World.FarmerId && data.Sender < World.FirstTraderId ? 1
                    : data.Collection ? 3
                    : 0
                )
        );

    private static void MigrateAnimalCount(SaveData data)
    {
        if (data.Animals.Length == World.QuestAnimalCount)
            return;
        data.Animals = data.Animals.Take(World.QuestAnimalCount).ToArray();
        data.Unlocked = data.Unlocked.Where(i => i < World.QuestAnimalCount).ToArray();
        data.Completed = data.Completed.Where(i => i < World.QuestAnimalCount).ToArray();
        bool Removed(int id) => id >= World.QuestAnimalCount && id < World.FirstResidentId;
        // Village actor IDs stay at 30–40 so existing house and trade progress keeps its meaning.
        if (Removed(data.Sender))
        {
            data.Sender = -1;
            data.Collection = false;
            data.Picked = Array.Empty<bool>();
            data.Arrived = false;
            data.DeliveryReward = false;
            data.State = GameState.Exploring;
            ClearQuestion(data);
        }
        if (Removed(data.Nearby))
        {
            data.Nearby = -1;
            data.State = GameState.Exploring;
            ClearQuestion(data);
            data.DeliveryReward = false;
            // A retained delivery may have been at a customer removed from this smaller world.
            data.Arrived = false;
        }
    }

    /// <summary>Moves a version 1 question from its per-kind field into <see cref="SaveData.Challenge"/>.</summary>
    private static void MigrateLegacyQuestion(SaveData data)
    {
        data.Challenge ??= (IChallenge?)data.Math
            ?? (IChallenge?)data.Letters
            ?? (IChallenge?)data.Syllables
            ?? (IChallenge?)data.Reading
            ?? data.DeliveryQuestion;
        data.Math = null;
        data.Letters = null;
        data.Syllables = null;
        data.Reading = null;
        data.DeliveryQuestion = null;
        if (data.Version == 1)
            data.Version = 2;
    }

    /// <summary>
    /// Version 2 offered a choice of car items on the reward panel. A save made there pays the coins of the
    /// new reward instead, so the finished task is not lost.
    /// </summary>
    private static void MigrateRewardOffer(SaveData data)
    {
        if (data.Version != 2)
            return;
        data.Version = SaveData.CurrentVersion;
        if (data.State != GameState.Reward || !data.DeliveryReward)
            return;
        bool trade = data.Sender >= World.FirstTraderId && data.Sender < World.WitchId;
        data.RewardCoins = trade ? Game.TradeCoins : Game.RoadsideDeliveryCoins;
        data.Coins += data.RewardCoins;
    }

    /// <summary>
    /// Older saves have only the bridge, with six deliveries. A finished bridge stays finished, an unfinished one
    /// keeps its share of the work and a trip under way is dropped, since the loads have changed.
    /// </summary>
    private static void MigrateBuilds(SaveData data)
    {
        if (data.BuildStages != null)
            return;
        int old = Math.Clamp(data.FordStage, 0, 6);
        int stage = old >= 6 ? BuildPlans.Trips : old * BuildPlans.Trips / 6;
        data.BuildStages = new int[BuildPlans.Count];
        data.BuildStages[BuildPlans.Bridge] = stage;
        if (data.FordTrip >= 0 && data.Challenge is LoadChallenge)
        {
            ClearQuestion(data);
            data.State = GameState.Exploring;
            data.Nearby = -1;
        }
        data.FordStage = 0;
        data.FordTrip = -1;
        data.FordTripLoaded = false;
    }

    /// <summary>
    /// Saves from before the ghost race have no clock or gate times. A run that was under way is dropped, and a
    /// damaged record falls back to the pace ghost, so neither costs the player the rest of the save.
    /// </summary>
    private static void MigrateEventRun(SaveData data)
    {
        if (data.EventBest is not { Length: 2 })
            data.EventBest = new[] { Array.Empty<float>(), Array.Empty<float>() };
        for (int i = 0; i < 2; i++)
            if (data.EventBest[i] == null || !DrivingEvents.ValidSplits(data.EventBest[i]))
                data.EventBest[i] = Array.Empty<float>();
        bool consistent =
            data.EventSplits != null
            && data.EventSplits.Length == data.EventNext
            && data.EventActive >= 0
            && float.IsFinite(data.EventTime)
            && data.EventTime >= 0;
        if (consistent)
            return;
        data.EventSplits = Array.Empty<float>();
        data.EventTime = 0;
        if (data.EventActive >= 0 || data.EventNext != 0)
        {
            data.EventActive = -1;
            data.EventNext = 0;
        }
    }

    private static void ClearQuestion(SaveData data)
    {
        data.Challenge = null;
        data.SyllableSelection = default;
        data.Rejected = Array.Empty<int>();
        data.HandoverQuiz = false;
    }

    internal static bool Write(SaveData data, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (
                var stream = new FileStream(path + ".tmp", FileMode.Create, System.IO.FileAccess.Write, FileShare.None)
            )
            {
                JsonSerializer.Serialize(stream, data);
                stream.Flush(true);
            }
            // Another program (a virus scanner, a backup) can hold the old file for a moment: try a few times.
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                        File.Replace(path + ".tmp", path, path + ".bak");
                    else
                        File.Move(path + ".tmp", path);
                    break;
                }
                catch (IOException) when (attempt < 5)
                {
                    System.Threading.Thread.Sleep(60 * attempt);
                }
            }
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Cannot save progress: {error.Message}");
            return false;
        }
    }
}
