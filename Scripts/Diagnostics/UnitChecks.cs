using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Quizzes;
using MathRacer.Saves;

namespace MathRacer.Diagnostics;

/// <summary>
/// <c>--unit-checks</c>: the checks that need no valley, so that they run in a second or two before any world is
/// generated. The committed saves of every version load and round trip, damaged saves are refused, every kind of question
/// has exactly the answers it should, and the actor IDs do not overlap. What needs the game running stays in the other
/// suites.
/// </summary>
internal static class UnitChecks
{
    private static int passed;
    private static int failed;
    private static int files;

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            return;
        }
        failed++;
        GD.Print("FAIL: " + message);
    }

    /// <summary>Runs every check and returns the exit code.</summary>
    internal static int Run()
    {
        try
        {
            CommittedSaves();
            DamagedSaves();
            Questions();
            ActorRanges();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            return 1;
        }
        GD.Print($"UNIT_CHECKS checks={passed + failed} failed={failed}");
        if (failed == 0)
            GD.Print("UNIT_CHECKS_PASSED");
        return failed == 0 ? 0 : 1;
    }

    private static string Scratch() => DiagnosticArtifacts.PathFor($"unit-checks/{files++}.json");

    /// <summary>
    /// Every committed save loads, serialises to the same JSON after a round trip, and a save of the current version is
    /// exactly the file that was read (nothing is repaired in it). A save of version 4 loses no field on its way into
    /// the sections: flattened again it is the file.
    /// </summary>
    private static void CommittedSaves()
    {
        string root = ProjectSettings.GlobalizePath("res://Tests/Saves");
        Check(LegacySaves.CoversEverySection(), "every field of every section has the flat field it came from");
        int count = 0;
        foreach (string folder in Directory.GetDirectories(root, "v*").Order())
        foreach (string file in Directory.GetFiles(folder, "*.json").Order())
        {
            string name = Path.GetFileName(folder) + "/" + Path.GetFileNameWithoutExtension(file);
            var data = SaveStore.Read(file);
            Check(data != null, $"{name} loads");
            if (data == null)
                continue;
            count++;
            string json = JsonSerializer.Serialize(data);
            Check(
                JsonSerializer.Serialize(JsonSerializer.Deserialize<SaveData>(json)) == json,
                $"{name} serialises to the same JSON after a round trip"
            );
            var original = JsonNode.Parse(File.ReadAllText(file))!.AsObject();
            if (folder.EndsWith("v5"))
                Check(JsonNode.DeepEquals(JsonNode.Parse(json), original), $"{name} is what the game writes");
            if (folder.EndsWith("v4"))
            {
                var flat = LegacySaves.FromCurrent(data, SaveDataV4.LastVersion);
                var moved = new[] { "Seed", "State", "Nearby" }.Concat(LegacySaves.Fields.Select(f => f.Flat));
                var different = moved.Where(key => !JsonNode.DeepEquals(flat[key], original[key])).ToList();
                Check(different.Count == 0, $"{name} loses no field: {string.Join(", ", different)}");
            }
        }
        Check(count >= 117, $"{count} committed saves were read");
    }

    /// <summary>One thing is wrong with an otherwise good save, and the game refuses it.</summary>
    private static void DamagedSaves()
    {
        string exploring = Path.Combine(ProjectSettings.GlobalizePath("res://Tests/Saves"), "v5", "exploring.json");
        JsonObject Base() => JsonNode.Parse(File.ReadAllText(exploring))!.AsObject();
        bool Refused(Action<JsonObject> damage)
        {
            var save = Base();
            damage(save);
            string path = Scratch();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, save.ToJsonString());
            return SaveStore.Read(path) == null;
        }
        Check(!Refused(_ => { }), "the save before it is damaged is accepted");
        Check(Refused(s => s["Seed"] = 0), "a seed of zero");
        Check(Refused(s => s["Version"] = SaveData.CurrentVersion + 1), "a version from the future");
        Check(Refused(s => s["State"] = (int)GameState.Title), "the title screen as the saved state");
        Check(Refused(s => s["State"] = (int)GameState.Paused), "a pause as the saved state");
        Check(Refused(s => s["State"] = 99), "a state that does not exist");
        Check(Refused(s => s["Nearby"] = 999), "a character that does not exist");
        Check(Refused(s => s["Economy"]!["Coins"] = -1), "negative coins");
        Check(Refused(s => s["Economy"] = null), "a missing section");
        Check(Refused(s => s["Quiz"]!["Types"] = 0), "no kind of question switched on");
        Check(Refused(s => s["Vehicle"]!["Position"] = new JsonArray(1, 2)), "a position of two numbers");
        Check(Refused(s => s["Events"]!["Wins"] = new JsonArray(1)), "wins of one course");
        Check(Refused(s => s["Roadside"]!["Animals"] = new JsonArray(true, false)), "two roadside friends");
        Check(Refused(s => s["Village"]!["HouseLevels"] = new JsonArray(0, 0, 0)), "three houses");
        Check(
            Refused(s => s["Buildings"]!["Stages"] = new JsonArray(0, 0, 0, BuildPlans.Trips + 1)),
            "eleven deliveries"
        );
        Check(
            Refused(s => s["Task"]!["Trip"] = JsonNode.Parse("{\"Kind\":\"bogus\",\"Index\":0,\"Loaded\":false}")),
            "a trip of a kind that does not exist"
        );
        Check(
            Refused(s => s["Task"]!["Trip"] = JsonNode.Parse("{\"Kind\":\"build\",\"Index\":9,\"Loaded\":false}")),
            "a trip to a fifth building"
        );
    }

    /// <summary>
    /// A question has the answers it should: every kind has distinct choices and a right answer, the generators of
    /// one seed give the same questions every time, and a question that checks itself says it is sound.
    /// </summary>
    private static void Questions()
    {
        void Verify(IChallenge question, bool oneAnswer, string kind)
        {
            Check(question.Choices.Distinct().Count() == question.Choices.Count, $"{kind}: the choices are distinct");
            int right = question.Choices.Count(question.Accepts);
            Check(oneAnswer ? right == 1 : right >= 1, $"{kind}: {right} right answers in {question.Expression}");
            Check(
                question is not ReadingChallenge reading || reading.IsValid(),
                $"{kind}: a reading question is sound ({question.Expression})"
            );
            Check(
                question is not SyllableChallenge syllables || syllables.IsValid(),
                $"{kind}: a syllable question is sound ({question.Expression})"
            );
        }
        var math = new ChallengeGenerator(53);
        var letters = new LetterChallengeGenerator(53);
        var syllables = new SyllableChallengeGenerator(53);
        var reading = new ReadingChallengeGenerator(53);
        var random = new Random(7);
        var cargo = new[]
        {
            HaulLoad.Stones,
            HaulLoad.Soil,
            HaulLoad.Sand,
            HaulLoad.Logs,
            HaulLoad.Planks,
            HaulLoad.Bricks,
            HaulLoad.Gravel,
        };
        for (int i = 0; i < 300; i++)
        {
            Verify(math.Next(i % 2 == 0), true, "arithmetic");
            Verify(letters.Next(), true, "letters");
            Verify(syllables.Next(), false, "syllables");
            Verify(reading.Next(), true, "reading");
            var load = LoadChallenge.Create(random, cargo[i % cargo.Length]);
            Verify(load, true, "loading");
            Check(load.IsValid(), "a loading question is sound");
        }
        var first = new ChallengeGenerator(42);
        var second = new ChallengeGenerator(42);
        bool same = true;
        for (int i = 0; i < 100; i++)
        {
            var a = first.Next(i % 3 == 0);
            var b = second.Next(i % 3 == 0);
            same &= a.Expression == b.Expression && a.Choices.SequenceEqual(b.Choices);
        }
        Check(same, "one seed gives the same arithmetic questions every time");
    }

    /// <summary>The ranges of actor IDs follow one another and do not overlap, in the order the saves rely on.</summary>
    private static void ActorRanges()
    {
        int[] starts =
        {
            ActorIds.FirstResident,
            ActorIds.Farmer,
            ActorIds.Gardener,
            ActorIds.GemCollector,
            ActorIds.Toymaker,
            ActorIds.FirstTrader,
            ActorIds.Witch,
            ActorIds.Sorcerer,
            ActorIds.Bandit,
            ActorIds.Beaver,
            ActorIds.FirstWorks,
            ActorIds.FirstBuilder,
            ActorIds.FirstSource,
            ActorIds.FirstExtraSource,
            ActorIds.FirstPlotResident,
        };
        Check(starts.Zip(starts.Skip(1), (a, b) => a < b).All(ok => ok), "the actor ID ranges follow one another");
        Check(
            ActorIds.LastWorks < ActorIds.FirstBuilder
                && ActorIds.LastBuilder < ActorIds.FirstSource
                && ActorIds.LastSource < ActorIds.FirstPlotResident,
            "the ranges of workers, builders and sources do not overlap"
        );
        Check(
            Enum.GetValues<GameState>().Select(state => (int)state).Distinct().Count()
                == Enum.GetValues<GameState>().Length,
            "no two interaction states have the same number"
        );
    }
}
