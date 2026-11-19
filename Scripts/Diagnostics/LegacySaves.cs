using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using MathRacer.Saves;

namespace MathRacer.Diagnostics;

/// <summary>
/// Saves in the flat shape of versions 1 to 4, made from the current game for the checks of the migrations. The
/// current save has sections; a save of an older game has every field at the top, under the name it had then. A check
/// takes the flat JSON, damages or ages it the way an older game would have left it, and loads it as the newest game
/// does.
/// </summary>
internal static class LegacySaves
{
    private static int files;

    /// <summary>The field of the flat save, and the section and field of the current save that it moved to.</summary>
    internal static readonly (string Flat, string Section, string Field)[] Fields =
    {
        ("Position", "Vehicle", "Position"),
        ("Heading", "Vehicle", "Heading"),
        ("Look", "Vehicle", "Look"),
        ("Model", "Vehicle", "Model"),
        ("Accessories", "Vehicle", "Accessories"),
        ("Coins", "Economy", "Coins"),
        ("RewardCoins", "Economy", "RewardCoins"),
        ("Owned", "Economy", "Owned"),
        ("QuizTypes", "Quiz", "Types"),
        ("ThreeTermMath", "Quiz", "ThreeTermMath"),
        ("NextQuiz", "Quiz", "Next"),
        ("HandoverQuiz", "Quiz", "Handover"),
        ("Challenge", "Quiz", "Challenge"),
        ("SyllableSelection", "Quiz", "Selection"),
        ("Rejected", "Quiz", "Rejected"),
        ("Departure", "Quiz", "Departure"),
        ("DepartureState", "Quiz", "DepartureState"),
        ("Sender", "Task", "Sender"),
        ("Collection", "Task", "Collection"),
        ("Picked", "Task", "Picked"),
        ("Arrived", "Task", "Arrived"),
        ("DeliveryReward", "Task", "DeliveryReward"),
        ("Trip", "Task", "Trip"),
        ("Animals", "Roadside", "Animals"),
        ("Unlocked", "Roadside", "Unlocked"),
        ("Completed", "Roadside", "Completed"),
        ("HouseLevels", "Village", "HouseLevels"),
        ("FarmCount", "Village", "FarmCount"),
        ("GardenCount", "Village", "GardenCount"),
        ("GemCount", "Village", "GemCount"),
        ("ToyCount", "Village", "ToyCount"),
        ("TradeCounts", "Village", "TradeCounts"),
        ("BuildStages", "Buildings", "Stages"),
        ("PlotStages", "Buildings", "PlotStages"),
        ("WorksStock", "Buildings", "WorksStock"),
        ("TrailerOwned", "Buildings", "TrailerOwned"),
        ("TrailerAttached", "Buildings", "TrailerAttached"),
        ("TrailerPaint", "Buildings", "TrailerPaint"),
        ("GolemOwned", "Companions", "GolemOwned"),
        ("GolemActive", "Companions", "GolemActive"),
        ("DisabledCollectorPets", "Companions", "DisabledPets"),
        ("Postcards", "Postcards", "Cards"),
        ("PostcardPage", "Postcards", "Page"),
        ("EventActive", "Events", "Active"),
        ("EventNext", "Events", "Next"),
        ("EventWins", "Events", "Wins"),
        ("EventBest", "Events", "Best"),
        ("EventTime", "Events", "Time"),
        ("EventSplits", "Events", "Splits"),
        ("BanditSite", "Bandits", "Site"),
        ("BanditSolved", "Bandits", "Solved"),
        ("BanditCooldown", "Bandits", "Cooldown"),
    };

    /// <summary>
    /// Fields that were added to version 5 after its committed saves were made, and that the flat saves never had: the
    /// counts of hauled loads, the glassworks' stock and the houses that were given glasses, the bakery's loaves and the houses that were given bread. Each has a default, so a file
    /// without it loads as before and the game writes it from then on. A new optional field of the current version is
    /// listed here, with the section it belongs to, instead of changing a committed save.
    /// </summary>
    internal static readonly (string Section, string Field)[] AddedAfterVersion5 =
    {
        ("Hauls", "Raw"),
        ("Hauls", "Works"),
        ("Buildings", "GlassStock"),
        ("Village", "Glazed"),
        ("Hauls", "Wheat"),
        ("Hauls", "Baked"),
        ("Buildings", "BreadStock"),
        ("Village", "Baked"),
        ("Hidden", "Found"),
        ("Vehicle", "Trail"),
        ("Playground", "Stage"),
        ("Decorations", "Owned"),
        ("Lamps", "Carried"),
        ("Lamps", "Placed"),
    };

    /// <summary>
    /// The JSON the game writes for a save, as it would be in a file made before the fields of
    /// <see cref="AddedAfterVersion5"/> existed: those that the committed <paramref name="original"/> lacks are taken out.
    /// </summary>
    internal static JsonNode WithoutAddedFields(JsonNode written, JsonObject original)
    {
        var copy = written.DeepClone().AsObject();
        foreach (var (section, field) in AddedAfterVersion5)
        {
            bool inFile = original[section] is JsonObject saved && saved.ContainsKey(field);
            if (inFile || copy[section] is not JsonObject target)
                continue;
            target.Remove(field);
            if (target.Count == 0)
                copy.Remove(section);
        }
        return copy;
    }

    /// <summary>The save as an older game wrote it: the sections flattened to the fields they came from.</summary>
    internal static JsonObject FromCurrent(SaveData data, int version = SaveDataV4.LastVersion)
    {
        var current = JsonSerializer.SerializeToNode(data)!.AsObject();
        var flat = new JsonObject
        {
            ["Version"] = version,
            ["Seed"] = current["Seed"]!.DeepClone(),
            ["State"] = current["State"]!.DeepClone(),
            ["Nearby"] = current["Nearby"]!.DeepClone(),
        };
        foreach (var (name, section, field) in Fields)
            flat[name] = current[section]![field]?.DeepClone();
        return flat;
    }

    /// <summary>Loads a flat save the way the game does, from a file of its own so that no backup is mistaken for it.</summary>
    internal static SaveData? Read(JsonObject flat)
    {
        string path = DiagnosticArtifacts.PathFor($"legacy-saves/{files++}.json");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, flat.ToJsonString());
        return SaveStore.Read(path);
    }

    /// <summary>Whether the game refuses the flat save after it was damaged the way <paramref name="damage"/> says.</summary>
    internal static bool IsRejected(SaveData data, Action<JsonObject> damage, int version = SaveDataV4.LastVersion)
    {
        var flat = FromCurrent(data, version);
        damage(flat);
        return Read(flat) == null;
    }

    /// <summary>
    /// Every field of every section is one of those in <see cref="Fields"/>, and the other way round, so that moving a
    /// save forward cannot leave a field behind.
    /// </summary>
    internal static bool CoversEverySection()
    {
        var current = typeof(SaveData)
            .GetProperties()
            .Where(p => p.PropertyType.Namespace == typeof(SaveData).Namespace && p.PropertyType != typeof(SaveData))
            .SelectMany(section =>
                section.PropertyType.GetProperties().Select(field => (Section: section.Name, Field: field.Name))
            )
            .OrderBy(pair => pair.Section)
            .ThenBy(pair => pair.Field);
        var mapped = Fields
            .Select(f => (f.Section, f.Field))
            .Concat(AddedAfterVersion5)
            .OrderBy(pair => pair.Section)
            .ThenBy(pair => pair.Field);
        return current.SequenceEqual(mapped);
    }
}
