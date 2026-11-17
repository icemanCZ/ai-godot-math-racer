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
    public bool GolemOwned { get; set; }
    public bool GolemActive { get; set; }

    /// <summary>Loads already built into the beaver's ford, 0–6.</summary>
    public int FordStage { get; set; }

    /// <summary>The active ford trip, which equals <see cref="FordStage"/>, or -1.</summary>
    public int FordTrip { get; set; } = -1;
    public bool FordTripLoaded { get; set; }
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
            || HasInvalidFordState(data)
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
        || data.Nearby > Math.Max(Game.BanditActorId, World.BeaverId)
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
            && (data.Nearby < World.FirstTraderId || (data.Sender < 0 && data.FordTrip < 0))
        )
        || (
            data.Nearby >= World.FirstTraderId
            && data.Nearby < World.WitchId
            && data.State == GameState.Challenge
            && (data.Sender >= 0 || data.Challenge is DeliveryChallenge)
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
            data.Nearby >= World.FirstResidentId
            && data.Nearby < World.FirstTraderId
            && data.State is GameState.Reward or GameState.ExpeditionChoice
        )
        || (
            data.Nearby == World.FarmerId
            && data.State == GameState.Challenge
            && (data.Sender >= 0 || data.FarmCount == Village.FarmKinds.Length || data.Challenge is DeliveryChallenge)
        )
        || (
            data.Nearby == World.GardenerId
            && data.State == GameState.Challenge
            && (
                data.Sender >= 0
                || data.GardenCount == Village.FlowerNames.Length
                || data.Challenge is DeliveryChallenge
            )
        )
        || (
            data.Nearby >= World.GemCollectorId
            && data.Nearby < World.FirstTraderId
            && data.State == GameState.Challenge
            && (
                data.Sender >= 0
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

    private static bool HasInvalidFordState(SaveData data) =>
        data.FordStage is < 0 or > Ford.Trips
        || data.FordTrip < -1
        || data.FordTrip >= Ford.Trips
        || (data.FordTrip >= 0 && (data.FordTrip != data.FordStage || data.Sender >= 0))
        || (data.FordTripLoaded && data.FordTrip < 0)
        || (data.TrailerOwned && data.FordStage < Ford.Trips)
        || (data.TrailerAttached && !data.TrailerOwned)
        || data.TrailerPaint == null
        || !Array.Exists(TrailerModel.Paints, paint => paint.Id == data.TrailerPaint)
        || (data.State == GameState.FordStatus && data.Nearby != World.BeaverId)
        || (
            data.Nearby == World.BeaverId
            && (
                data.State is not (GameState.Exploring or GameState.FordStatus or GameState.Challenge)
                || (
                    data.State == GameState.Challenge
                    && (
                        data.FordTrip < 0
                        || data.FordTripLoaded
                        || data.HandoverQuiz
                        || data.Challenge is DeliveryChallenge
                    )
                )
            )
        )
        || (
            data.Challenge is LoadChallenge load
            && (
                !load.IsValid()
                || data.Nearby != World.BeaverId
                || data.FordTrip < 0
                || load.Cargo != Ford.TripLoads[data.FordTrip]
            )
        );

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
            if (File.Exists(path))
                File.Replace(path + ".tmp", path, path + ".bak");
            else
                File.Move(path + ".tmp", path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Cannot save progress: {error.Message}");
            return false;
        }
    }
}
