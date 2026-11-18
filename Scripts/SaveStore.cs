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
    /// Version 4 keeps the trailer trip in <see cref="Trip"/> instead of a pair of fields for each kind of trip.
    /// Version 3 pays coins instead of offering car rewards. Version 2 stores the open question in
    /// <see cref="Challenge"/>; version 1 used one field per kind. Older versions are migrated when they are read.
    /// </summary>
    public const int CurrentVersion = 4;

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

    /// <summary>
    /// The trailer trip under way, or null: what it is for (<see cref="TripKinds"/>), which one of its kind, and
    /// whether the load is aboard. Never set together with an expedition.
    /// </summary>
    public SavedTrip? Trip { get; set; }

    // Before version 4 each kind of trip had a pair of fields. They are read only to migrate older saves into Trip
    // (MigrateTrips), which empties them, so a save of the current version never writes them.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BuildTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BuildTripLoaded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SupplyTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SupplyTripLoaded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? HouseTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HouseTripLoaded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PlotTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PlotTripLoaded { get; set; }

    /// <summary>Goods stored at the sawmill, brickworks and gravel works (<see cref="Product"/> order), 0–12 each.</summary>
    public int[] WorksStock { get; set; } = new int[3];

    /// <summary>Deliveries made to each of the six building plots, 0–6; zeros in saves from before the plots.</summary>
    public int[] PlotStages { get; set; } = new int[PlotPlans.Count];

    public bool TrailerOwned { get; set; }
    public bool TrailerAttached { get; set; }
    public string TrailerPaint { get; set; } = "red";
    public string[] DisabledCollectorPets { get; set; } = Array.Empty<string>();
    public int Version { get; set; } = CurrentVersion;
    public QuizType QuizTypes { get; set; } = QuizType.All;
    public bool ThreeTermMath { get; set; }
    public int NextQuiz { get; set; }
    public bool HandoverQuiz { get; set; }

    /// <summary>
    /// The trip that starts when the open quiz is solved (<see cref="Game.Departure"/>), or 0. Saves from before
    /// this field have none, so a quiz asked before a trip lost its trip when the game was closed.
    /// </summary>
    public int Departure { get; set; }

    /// <summary>The panel that offered the trip, for the kinds of trip that return to one; otherwise Title.</summary>
    public GameState DepartureState { get; set; }
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

/// <summary>A trailer trip under way: the delivery it fetches is the next one of its target.</summary>
public sealed class SavedTrip
{
    /// <summary>One of <see cref="TripKinds"/>.</summary>
    public string Kind { get; set; } = "";

    /// <summary>The project, works, house or plot, by its index among those of its kind.</summary>
    public int Index { get; set; }

    /// <summary>Whether the load is already on the trailer.</summary>
    public bool Loaded { get; set; }
}

public static class SaveStore
{
    private const int LegacyAnimalCount = 30;

    public static bool Enabled => LaunchOptions.UsesPlayerSave;
    public static string SavePath => ProjectSettings.GlobalizePath("user://progress.json");
    private static readonly Lazy<SaveData?> initial = new(() => Enabled ? ReadInitial(SavePath) : null);
    public static SaveData? Initial => initial.Value;

    /// <summary>Reads the player's save at startup and sets aside whatever the game could overwrite for good.</summary>
    private static SaveData? ReadInitial(string path)
    {
        var data = Read(path);
        Preserve(path);
        return data;
    }

    internal static SaveData? Read(string path)
    {
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate))
                    continue;
                return Load(candidate);
            }
            catch (Exception error) when (IsLoadFailure(error))
            {
                GD.PushWarning($"Cannot load {candidate}: {error.Message}");
            }
        }
        return null;
    }

    private static bool IsLoadFailure(Exception error) =>
        error is IOException or UnauthorizedAccessException or JsonException or NotSupportedException;

    /// <summary>Reads, migrates and validates one file; throws when it cannot be used.</summary>
    private static SaveData Load(string candidate)
    {
        var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(candidate))
            ?? throw new JsonException("Empty save data.");
        MigrateLegacyQuestion(data);
        MigrateRewardOffer(data);
        MigrateBuilds(data);
        MigrateTrips(data);
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

    /// <summary>
    /// The first autosave moves the file into <c>.bak</c> and the next one replaces that, so an older save would be
    /// gone within seconds. Before that can happen, a save of an older version is copied to
    /// <c>progress.vN.bak</c> and a file that cannot be loaded at all to <c>progress.nenacteno-HASH.json</c>.
    /// Both copies are made once and never overwritten.
    /// </summary>
    internal static void Preserve(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;
            byte[] bytes = File.ReadAllBytes(path);
            string directory = Path.GetDirectoryName(path)!;
            string name = Path.GetFileNameWithoutExtension(path);
            try
            {
                Load(path);
                if (RawVersion(bytes) is int version && version < SaveData.CurrentVersion)
                    CopyOnce(path, Path.Combine(directory, $"{name}.v{version}.bak"));
            }
            catch (Exception error) when (IsLoadFailure(error))
            {
                string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))[..8];
                CopyOnce(path, Path.Combine(directory, $"{name}.nenacteno-{hash}.json"));
            }
        }
        catch (Exception error) when (IsLoadFailure(error))
        {
            GD.PushWarning($"Cannot keep a copy of {path}: {error.Message}");
        }
    }

    private static int? RawVersion(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.TryGetProperty("Version", out var version) && version.TryGetInt32(out int value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void CopyOnce(string source, string target)
    {
        if (!File.Exists(target))
            File.Copy(source, target);
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
            || HasInvalidTrip(data)
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
        || (data.State == GameState.Challenge && data.Challenge == null)
        || HasInvalidDeparture(data);

    /// <summary>
    /// A trip waits for the open quiz: an ordinary question asked by the character who offers the trip, never a
    /// loading question or a handover. Saves from before this field have none.
    /// </summary>
    private static bool HasInvalidDeparture(SaveData data)
    {
        if (data.Departure == 0)
            return data.DepartureState != GameState.Title;
        if (
            !Enum.IsDefined((Game.Departure)data.Departure)
            || data.State != GameState.Challenge
            || data.Challenge is null or LoadChallenge or DeliveryChallenge
            || data.HandoverQuiz
            || data.Nearby == Game.BanditActorId
        )
            return true;
        bool resident = data.Nearby is >= World.FirstResidentId and < World.FarmerId;
        return (Game.Departure)data.Departure switch
        {
            Game.Departure.Golem => data.Nearby != World.SorcererId || data.DepartureState != GameState.Title,
            Game.Departure.Fruit => !resident || data.DepartureState != GameState.Title,
            Game.Departure.Build => !World.IsBuilder(data.Nearby) || data.DepartureState != GameState.BuildStatus,
            Game.Departure.Plot => !World.IsPlotResident(data.Nearby) || data.DepartureState != GameState.PlotStatus,
            Game.Departure.House => !resident || data.DepartureState != GameState.HouseMaterialOffer,
            // The works' missing goods are asked for from a builder, a newcomer or a resident.
            Game.Departure.Missing => data.DepartureState switch
            {
                GameState.BuildStatus => !World.IsBuilder(data.Nearby),
                GameState.PlotStatus => !World.IsPlotResident(data.Nearby),
                GameState.HouseMaterialOffer => !resident,
                _ => true,
            },
            _ => true,
        };
    }

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
        || (data.TrailerOwned && data.BuildStages[BuildPlans.Bridge] < BuildPlans.Trips)
        || (data.TrailerAttached && !data.TrailerOwned)
        || data.TrailerPaint == null
        || !Array.Exists(TrailerModel.Paints, paint => paint.Id == data.TrailerPaint)
        || HasInvalidAsker(data);

    /// <summary>
    /// The progress of the six building plots, each 0–6 deliveries.
    /// </summary>
    private static bool HasInvalidPlotState(SaveData data) =>
        data.PlotStages == null
        || data.PlotStages.Length != PlotPlans.Count
        || data.PlotStages.Any(stage => stage < 0 || stage > PlotPlans.Trips);

    /// <summary>The cargo and the characters who may ask about it for the active trailer trip, or none without one.</summary>
    private static (HaulLoad Cargo, int[] Askers, bool Loaded) ActiveTripAsker(SaveData data)
    {
        if (data.Trip is not { } trip)
            return (default, Array.Empty<int>(), false);
        switch (trip.Kind)
        {
            case TripKinds.Build
                when trip.Index is >= 0 and < BuildPlans.Count
                    && data.BuildStages is { Length: BuildPlans.Count } buildStages
                    && buildStages[trip.Index] is >= 0 and < BuildPlans.Trips:
            {
                var cargo = BuildPlans.For(trip.Index)[buildStages[trip.Index]].Cargo;
                return (cargo, World.AskerIds(cargo, trip.Index), trip.Loaded);
            }
            case TripKinds.Supply when trip.Index is >= 0 and < WorksCatalog.Count:
            {
                var cargo = WorksCatalog.All[trip.Index].Input;
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Plot
                when trip.Index is >= 0 and < PlotPlans.Count
                    && data.PlotStages is { Length: PlotPlans.Count } plotStages
                    && plotStages[trip.Index] is >= 0 and < PlotPlans.Trips:
            {
                var cargo = PlotPlans.CargoOf(plotStages[trip.Index]);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.House when SavedTask(data, trip.Index) is { } kind && Village.MaterialOf(kind) is { } goods:
                return (goods, World.AskerIds(goods, 0), trip.Loaded);
        }
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
    /// A trailer trip fetches the next delivery of its target, so that target must exist and be unfinished: a
    /// building or plot with deliveries left, a works that is not full, a house that asks for planks or bricks. It
    /// never runs together with an expedition.
    /// </summary>
    private static bool HasInvalidTrip(SaveData data)
    {
        if (data.Trip is not { } trip)
            return false;
        if (data.Sender >= 0)
            return true;
        return trip.Kind switch
        {
            TripKinds.Build => trip.Index is < 0 or >= BuildPlans.Count
                || data.BuildStages![trip.Index] >= BuildPlans.Trips,
            TripKinds.Supply => trip.Index is < 0 or >= WorksCatalog.Count
                || data.WorksStock[trip.Index] >= Works.Capacity,
            TripKinds.House => trip.Index is < 0 or >= 4
                || SavedTask(data, trip.Index) is not (VillageTaskKind.Planks or VillageTaskKind.Bricks),
            TripKinds.Plot => trip.Index is < 0 or >= PlotPlans.Count || data.PlotStages[trip.Index] >= PlotPlans.Trips,
            _ => true,
        };
    }

    /// <summary>
    /// Houses used to ask only quizzes and apples. A save that waits at a house whose next task is now different
    /// (an apple offer, or a quiz where the house now wants goods) goes back to driving, so the rest of it is kept.
    /// </summary>
    private static void MigrateHouseTasks(SaveData data)
    {
        if (data.Nearby is < World.FirstResidentId or >= World.FarmerId || data.Sender >= 0)
            return;
        var task = SavedTask(data, data.Nearby - World.FirstResidentId);
        // The quiz before a goods trip is meant: the house asks for goods and the trip follows the quiz.
        bool stale =
            data.State == GameState.HouseFruitOffer && task != VillageTaskKind.Fruit
            || data.State == GameState.Challenge
                && data.Departure == 0
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
        // A quiz before a trip is asked by the character who offers it, whatever trip is under way meanwhile.
        if (data.State == GameState.Challenge && data.Departure == 0)
        {
            if (!askers.Contains(data.Nearby) || loaded || data.HandoverQuiz || data.Challenge is DeliveryChallenge)
                return true;
        }
        return data.Challenge is LoadChallenge load && (!load.IsValid() || askers.Length == 0 || load.Cargo != cargo);
    }

    /// <summary>
    /// The stock of the three works, which never exceeds the yard.
    /// </summary>
    private static bool HasInvalidWorksState(SaveData data) =>
        data.WorksStock == null
        || data.WorksStock.Length != WorksCatalog.Count
        || data.WorksStock.Any(stock => stock < 0 || stock > Works.Capacity);

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
        data.Version = 3;
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
    /// Before version 4 each kind of trailer trip had a pair of fields. A save with one trip gets it as
    /// <see cref="SaveData.Trip"/>. A save with two trips, a loaded flag without a trip or a number that is not a
    /// trip stays damaged, so that validation refuses it as it always did.
    /// </summary>
    private static void MigrateTrips(SaveData data)
    {
        var trips = new System.Collections.Generic.List<SavedTrip>();
        bool damaged = false;
        void Take(string kind, int? index, bool? loaded)
        {
            if (index is null or -1)
                damaged |= loaded == true;
            else if (index < -1)
                damaged = true;
            else
                trips.Add(
                    new SavedTrip
                    {
                        Kind = kind,
                        Index = index.Value,
                        Loaded = loaded == true,
                    }
                );
        }
        Take(TripKinds.Build, data.BuildTrip, data.BuildTripLoaded);
        Take(TripKinds.Supply, data.SupplyTrip, data.SupplyTripLoaded);
        Take(TripKinds.House, data.HouseTrip, data.HouseTripLoaded);
        Take(TripKinds.Plot, data.PlotTrip, data.PlotTripLoaded);
        data.BuildTrip = data.SupplyTrip = data.HouseTrip = data.PlotTrip = null;
        data.BuildTripLoaded = data.SupplyTripLoaded = data.HouseTripLoaded = data.PlotTripLoaded = null;
        if (damaged || trips.Count > 1 || (trips.Count == 1 && data.Trip != null))
            data.Trip = new SavedTrip { Kind = "damaged", Index = -1 };
        else if (trips.Count == 1)
            data.Trip = trips[0];
        if (data.Version == 3)
            data.Version = 4;
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
