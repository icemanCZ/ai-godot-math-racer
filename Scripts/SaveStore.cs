using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MathRacer;

// Plain data only: scene nodes are regenerated from the saved world seed.
public sealed class SaveData
{
    public int EventActive { get; set; } = -1;
    public int EventNext { get; set; }
    public int[] EventWins { get; set; } = new int[2];
    public int Version { get; set; } = 1;
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
    public CarReward[] Rewards { get; set; } = Array.Empty<CarReward>();
    public CarReward[] PreviousRewards { get; set; } = Array.Empty<CarReward>();
    public string Look { get; set; } = "";
    public string Model { get; set; } = "classic";
    public string[] Accessories { get; set; } = Array.Empty<string>();
    public int Sender { get; set; } = -1;
    public bool Collection { get; set; }
    public bool[] Picked { get; set; } = Array.Empty<bool>();
    public bool Arrived { get; set; }
    public bool DeliveryReward { get; set; }
    public MathChallenge? Math { get; set; }
    public LetterChallenge? Letters { get; set; }
    public DeliveryChallenge? DeliveryQuestion { get; set; }
    public int[] Rejected { get; set; } = Array.Empty<int>();
    public string Feedback { get; set; } = "";
}

public static class SaveStore
{
    public static bool Enabled => !OS.GetCmdlineUserArgs().Any(a =>
        a.EndsWith("-checks") || a is "--smoke-test" or "--road-audit" or "--capture" or "--no-save" || a.StartsWith("--seed="));
    public static string SavePath => ProjectSettings.GlobalizePath("user://progress.json");
    private static readonly Lazy<SaveData?> initial = new(() => Enabled ? Read(SavePath) : null);
    public static SaveData? Initial => initial.Value;

    internal static SaveData? Read(string path)
    {
        foreach (string candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(candidate));
                if (data == null || data.Version != 1 || data.Seed <= 0 || data.Seed >= int.MaxValue - 4096 ||
                    data.EventActive < -1 || data.EventActive > 1 || data.EventNext < 0 || data.EventNext >= DrivingEvents.GateCount ||
                    data.EventWins == null || data.EventWins.Length != 2 || data.EventWins.Any(w => w < 0 || w > 9999) ||
                    data.Position == null || data.Position.Length != 3 || data.Position.Any(p => !float.IsFinite(p)) ||
                    !float.IsFinite(data.Heading) || !Enum.IsDefined(data.State) || data.State == GameState.Title || data.State == GameState.Paused ||
                    data.Animals == null || (data.Animals.Length != World.QuestAnimalCount && data.Animals.Length != 30) || data.Unlocked == null || data.Completed == null ||
                    data.Owned == null || data.Rewards == null || data.PreviousRewards == null || data.Accessories == null ||
                    data.Picked == null || data.Rejected == null || data.Nearby < -1 || data.Nearby >= 41 || data.Sender < -1 || data.Sender >= 41 ||
                    data.TradeCounts == null || data.TradeCounts.Length != 3 || data.TradeCounts.Any(c => c < 0 || c > 1000000) ||
                    (data.Sender >= 38 && data.Collection) ||
                    (data.State == GameState.TradeStatus && (data.Nearby < 38 || data.Sender < 0)) ||
                    (data.Nearby >= 38 && data.State == GameState.Challenge && (data.Sender >= 0 || data.Math == null)) ||
                    (data.Sender >= 38 && data.DeliveryReward && (data.State != GameState.Reward || !data.Arrived)) ||
                    data.GemCount < 0 || data.GemCount > Village.GemNames.Length || data.ToyCount < 0 || data.ToyCount > Village.ToyNames.Length ||
                    (data.Sender == 36 && (data.GemCount == Village.GemNames.Length || !data.Collection || data.DeliveryReward)) ||
                    (data.Sender == 37 && (data.ToyCount == Village.ToyNames.Length || !data.Collection || data.DeliveryReward)) ||
                    (data.State == GameState.CollectionStatus && data.Nearby is not (36 or 37)) ||
                    data.GardenCount < 0 || data.GardenCount > Village.FlowerNames.Length ||
                    (data.Sender == 35 && (data.GardenCount == Village.FlowerNames.Length || !data.Collection || data.DeliveryReward)) ||
                    (data.State == GameState.GardenStatus && data.Nearby != 35) ||
                    data.FarmCount < 0 || data.FarmCount > Village.FarmKinds.Length ||
                    (data.Sender == 34 && (data.FarmCount == Village.FarmKinds.Length || !data.Collection || data.DeliveryReward)) ||
                    (data.State == GameState.FarmStatus && data.Nearby != 34) ||
                    data.HouseLevels == null || data.HouseLevels.Length != 4 || data.HouseLevels.Any(level => level < 0 || level > Village.Upgrades.Length) ||
                    (data.State == GameState.HouseReward && (data.Nearby < 30 || data.Nearby >= 34)) ||
                    (data.Sender >= 30 && data.Sender < 38 && (!data.Collection || data.DeliveryReward)) ||
                    (data.State == GameState.HouseCollection && (data.Sender < 30 || data.Sender >= 34 || data.Nearby != data.Sender)) ||
                    (data.State == GameState.HouseFruitOffer && (data.Nearby < 30 || data.Nearby >= 34 || data.HouseLevels[data.Nearby - 30] >= Village.Upgrades.Length)) ||
                    (data.Nearby >= 30 && data.Nearby < 38 && data.State is GameState.Reward or GameState.ExpeditionChoice) ||
                    data.Unlocked.Concat(data.Completed).Any(i => i < 0 || i >= data.Animals.Length) ||
                    (data.Nearby >= data.Animals.Length && data.Nearby < 30) ||
                    (data.Sender >= data.Animals.Length && data.Sender < 30) ||
                    (data.State is GameState.Challenge or GameState.Reward or GameState.ExpeditionChoice && data.Nearby < 0) ||
                    (data.State == GameState.Challenge && data.Math == null && data.Letters == null && data.DeliveryQuestion == null) ||
                    (data.Nearby == 34 && data.State == GameState.Challenge && (data.Sender >= 0 || data.FarmCount == Village.FarmKinds.Length || data.DeliveryQuestion != null)) ||
                    (data.Nearby == 35 && data.State == GameState.Challenge && (data.Sender >= 0 || data.GardenCount == Village.FlowerNames.Length || data.DeliveryQuestion != null)) ||
                    (data.Nearby >= 36 && data.Nearby < 38 && data.State == GameState.Challenge && (data.Sender >= 0 || (data.Nearby == 36 ? data.GemCount : data.ToyCount) == Village.CollectionNames(data.Nearby == 37).Length || data.DeliveryQuestion != null)) ||
                    (data.Sender >= 0 && data.Picked.Length != (data.Sender >= 34 && data.Sender < 38 ? 1 : data.Collection ? 3 : 0)))
                    throw new JsonException("Invalid save data.");
                MigrateAnimalCount(data);
                return data;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            { GD.PushWarning($"Cannot load {candidate}: {error.Message}"); }
        }
        return null;
    }

    private static void MigrateAnimalCount(SaveData data)
    {
        if (data.Animals.Length == World.QuestAnimalCount) return;
        data.Animals = data.Animals.Take(World.QuestAnimalCount).ToArray();
        data.Unlocked = data.Unlocked.Where(i => i < World.QuestAnimalCount).ToArray();
        data.Completed = data.Completed.Where(i => i < World.QuestAnimalCount).ToArray();
        bool Removed(int id) => id >= World.QuestAnimalCount && id < 30;
        // Village actor IDs stay at 30–40 so existing house and trade progress keeps its meaning.
        if (Removed(data.Sender))
        {
            data.Sender = -1; data.Collection = false; data.Picked = Array.Empty<bool>();
            data.Arrived = false; data.DeliveryReward = false;
            data.State = GameState.Exploring;
            data.Math = null; data.Letters = null; data.DeliveryQuestion = null; data.Rejected = Array.Empty<int>();
        }
        if (Removed(data.Nearby))
        {
            data.Nearby = -1; data.State = GameState.Exploring;
            data.Math = null; data.Letters = null; data.DeliveryQuestion = null; data.Rejected = Array.Empty<int>();
            data.DeliveryReward = false;
            // A retained delivery may have been at a customer removed from this smaller world.
            data.Arrived = false;
        }
    }

    internal static bool Write(SaveData data, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(path + ".tmp", FileMode.Create, System.IO.FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { GD.PushWarning($"Cannot save progress: {error.Message}"); return false; }
    }
}
