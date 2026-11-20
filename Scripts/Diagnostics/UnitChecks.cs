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
using MathRacer.Valley;

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
            CollectionTitles();
            FlatMathMatchesGodot();
            FlatRandomMatchesGodot();
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

    /// <summary>
    /// The world searches, the grass and the road surface compute in plain floats (<see cref="FlatMath"/>) what they
    /// used to compute with Godot's vectors, and the valley must come out the same, bit for bit: on random input every
    /// form gives exactly what the Godot method gives.
    /// </summary>
    private static void FlatMathMatchesGodot()
    {
        var random = new Random(7);
        float Next() =>
            random.Next(8) switch
            {
                0 => (float)(random.NextDouble() * 2 - 1) * 1e-3f,
                1 => random.Next(-600, 600),
                2 => (float)random.NextDouble() * 1e5f - 5e4f,
                _ => (float)(random.NextDouble() * 1400 - 700),
            };
        static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        var wrong = new int[12];
        for (int i = 0; i < 200_000; i++)
        {
            float a = Next(),
                b = Next(),
                c = Next(),
                d = Next(),
                e = Next(),
                f = Next();
            float weight = (float)random.NextDouble() * 1.2f - .1f;
            float low = Math.Min(c, d),
                high = Math.Max(c, d);
            if (!Same(FlatMath.Length(a, b), new Vector2(a, b).Length()))
                wrong[0]++;
            if (!Same(FlatMath.Lerp(a, b, weight), Mathf.Lerp(a, b, weight)))
                wrong[1]++;
            if (!Same(FlatMath.Clamp(a, low, high), Mathf.Clamp(a, low, high)))
                wrong[2]++;
            var p = new Vector2(a, b);
            var from = new Vector2(c, d);
            var delta = new Vector2(e, f) - from;
            float t = Mathf.Clamp((p - from).Dot(delta) / delta.LengthSquared(), 0, 1);
            if (!Same(FlatMath.SegmentDistance(a, b, c, d, e, f), p.DistanceTo(from + delta * t)))
                wrong[3]++;
            // The forms written out where they are used: squared distance, 3D distance, cross product, transform.
            if (!Same((a - c) * (a - c) + (b - d) * (b - d), p.DistanceSquaredTo(from)))
                wrong[4]++;
            float dx = a - d,
                dy = b - e,
                dz = c - f;
            if (!Same(MathF.Sqrt(dx * dx + dy * dy + dz * dz), new Vector3(a, b, c).DistanceTo(new Vector3(d, e, f))))
                wrong[5]++;
            if (!Same(a * d - b * c, new Vector2(a, b).Cross(new Vector2(c, d))))
                wrong[6]++;
            var transform = new Transform3D(
                new Basis(Vector3.Up, a * .01f).Scaled(new Vector3(1.3f, 1, .7f)),
                new Vector3(b, c, d)
            );
            var point = new Vector3(e, f, a);
            var moved = transform * point;
            var rows = transform.Basis;
            if (
                !Same(rows.Row0.X * e + rows.Row0.Y * f + rows.Row0.Z * a + transform.Origin.X, moved.X)
                || !Same(rows.Row2.X * e + rows.Row2.Y * f + rows.Row2.Z * a + transform.Origin.Z, moved.Z)
            )
                wrong[7]++;
            if (!Same(MathF.Floor(a), Mathf.Floor(a)) || (int)MathF.Floor(a / 16) != Mathf.FloorToInt(a / 16))
                wrong[8]++;
            if (!Same(MathF.Abs(a), Mathf.Abs(a)) || !Same(MathF.Sin(a), Mathf.Sin(a)))
                wrong[9]++;
            if (!Same(Math.Max(a, b), Mathf.Max(a, b)) || !Same(Math.Min(a, b), Mathf.Min(a, b)))
                wrong[10]++;
            var inward = new Vector3(c, d * .01f, e).Normalized();
            var offset = new Vector3(a - f, 0, b - c);
            float along = offset.Dot(inward),
                across = offset.Dot(Vector3.Up.Cross(inward));
            float alongMin = -weight * 20,
                alongMax = weight * 30,
                acrossMax = weight * 15;
            bool inside = along >= alongMin && along <= alongMax && Mathf.Abs(across) <= acrossMax;
            if (
                !Same(FlatMath.Dot(a, b, c, d, e, f), new Vector3(a, b, c).Dot(new Vector3(d, e, f)))
                || FlatMath.InRectangle(a, b, f, c, inward.X, inward.Y, inward.Z, alongMin, alongMax, acrossMax)
                    != inside
            )
                wrong[11]++;
            // The same rectangle test with the point at the edge, where the rounding decides.
            float edgeX = f + inward.X * alongMax,
                edgeZ = c + inward.Z * alongMax;
            var edgeOffset = new Vector3(edgeX - f, 0, edgeZ - c);
            bool edgeInside =
                edgeOffset.Dot(inward) >= alongMin
                && edgeOffset.Dot(inward) <= alongMax
                && Mathf.Abs(edgeOffset.Dot(Vector3.Up.Cross(inward))) <= acrossMax;
            if (
                FlatMath.InRectangle(edgeX, edgeZ, f, c, inward.X, inward.Y, inward.Z, alongMin, alongMax, acrossMax)
                != edgeInside
            )
                wrong[11]++;
        }
        string[] names =
        {
            "Length",
            "Lerp",
            "Clamp",
            "SegmentDistance",
            "squared distance",
            "3D distance",
            "cross product",
            "transform",
            "floor",
            "abs and sin",
            "min and max",
            "site rectangle",
        };
        for (int i = 0; i < names.Length; i++)
            Check(wrong[i] == 0, $"FlatMath: {names[i]} gives what Godot gives ({wrong[i]} differ)");
        FlatBasisMatchesGodot(random);
    }

    /// <summary>The tree drawing's rotations and scalings (<see cref="FlatBasis"/>) give what Godot's give, bit for bit.</summary>
    /// <summary>The grass draws its blades from <see cref="FlatRandom"/>, which must give Godot's numbers.</summary>
    private static void FlatRandomMatchesGodot()
    {
        static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        var random = new Random(7);
        var wrong = new int[3];
        for (int run = 0; run < 2000; run++)
        {
            // Seeds as the grass makes them, and any 64-bit number.
            ulong seed =
                run % 2 == 0
                    ? unchecked((ulong)(random.Next(1, int.MaxValue) + random.Next(-40, 40) * 73856093L))
                    : ((ulong)random.NextInt64() << 1) | (uint)random.Next(2);
            var godot = new RandomNumberGenerator { Seed = seed };
            var flat = new FlatRandom(seed);
            for (int i = 0; i < 200; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        if (!Same(godot.Randf(), flat.Randf()))
                            wrong[0]++;
                        break;
                    case 1:
                        float from = (float)(random.NextDouble() * 20 - 10),
                            to = from + (float)(random.NextDouble() * 10);
                        if (!Same(godot.RandfRange(from, to), flat.RandfRange(from, to)))
                            wrong[1]++;
                        break;
                    default:
                        if (godot.Randi() != flat.Next())
                            wrong[2]++;
                        break;
                }
            }
        }
        string[] names = { "Randf", "RandfRange", "Randi" };
        for (int i = 0; i < names.Length; i++)
            Check(wrong[i] == 0, $"FlatRandom: {names[i]} gives what Godot gives ({wrong[i]} differ)");
    }

    private static void FlatBasisMatchesGodot(Random random)
    {
        float Angle() => (float)(random.NextDouble() * 14 - 7);
        float Size() => (float)(random.NextDouble() * 3);
        Vector3 Axis() =>
            random.Next(4) switch
            {
                0 => Vector3.Up,
                1 => Vector3.Forward,
                _ => new Vector3(Angle(), Angle(), Angle()).Normalized(),
            };
        static bool Same(float a, float b) => BitConverter.SingleToInt32Bits(a) == BitConverter.SingleToInt32Bits(b);
        static bool SameVector(Vector3 a, Vector3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);
        static bool SameBasis(Basis a, Basis b) =>
            SameVector(a.Row0, b.Row0) && SameVector(a.Row1, b.Row1) && SameVector(a.Row2, b.Row2);
        var wrong = new int[5];
        for (int i = 0; i < 100_000; i++)
        {
            var axis = Axis();
            float angle = Angle();
            if (!SameBasis(FlatBasis.AxisAngle(axis, angle), new Basis(axis, angle)))
                wrong[0]++;
            var left = new Basis(Axis(), Angle()).Scaled(new Vector3(Size(), Size(), Size()));
            var right = new Basis(Axis(), Angle());
            if (!SameBasis(FlatBasis.Multiply(left, right), left * right))
                wrong[1]++;
            var turn = Axis();
            float by = Angle();
            if (!SameBasis(FlatBasis.Rotated(left, turn, by), left.Rotated(turn, by)))
                wrong[2]++;
            var scale = new Vector3(Size(), Size(), Size());
            if (
                !SameBasis(FlatBasis.ScaledLocal(left, scale), left.ScaledLocal(scale))
                || !SameBasis(FlatBasis.ScaledLocal(Basis.Identity, scale), Basis.Identity.ScaledLocal(scale))
            )
                wrong[3]++;
            var v = new Vector3(Angle(), Angle(), Angle());
            if (!SameVector(FlatBasis.Times(left, v), left * v))
                wrong[4]++;
        }
        string[] names = { "axis and angle", "product", "Rotated", "ScaledLocal", "basis times vector" };
        for (int i = 0; i < names.Length; i++)
            Check(wrong[i] == 0, $"FlatBasis: {names[i]} gives what Godot gives ({wrong[i]} differ)");
    }

    /// <summary>
    /// Every collected item has a name for signs and panels beside the form used after "přivez": the same number, and a
    /// name, so it starts with a capital letter.
    /// </summary>
    private static void CollectionTitles()
    {
        var sets = new (string Set, string[] Names, string[] Titles)[]
        {
            ("animals", Settlement.Village.FarmNames, Settlement.Village.FarmTitles),
            ("flowers", Settlement.Village.FlowerNames, Settlement.Village.FlowerTitles),
            ("gems", Settlement.Village.GemNames, Settlement.Village.GemTitles),
            ("toys", Settlement.Village.ToyNames, Settlement.Village.ToyTitles),
        };
        foreach (var (set, names, titles) in sets)
        {
            Check(names.Length == titles.Length, $"{set}: every name has a title");
            Check(
                titles.All(title => title.Length > 0 && char.IsUpper(title[0])),
                $"{set}: titles are names and start with a capital letter"
            );
        }
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
                Check(
                    JsonNode.DeepEquals(LegacySaves.WithoutAddedFields(JsonNode.Parse(json)!, original), original),
                    $"{name} is what the game writes"
                );
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
            ActorIds.Glassworks,
        };
        Check(starts.Zip(starts.Skip(1), (a, b) => a < b).All(ok => ok), "the actor ID ranges follow one another");
        Check(
            ActorIds.LastWorks < ActorIds.FirstBuilder
                && ActorIds.LastBuilder < ActorIds.FirstSource
                && ActorIds.LastSource < ActorIds.FirstPlotResident
                && ActorIds.LastPlotResident < ActorIds.Glassworks,
            "the ranges of workers, builders and sources do not overlap"
        );
        Check(
            Enum.GetValues<GameState>().Select(state => (int)state).Distinct().Count()
                == Enum.GetValues<GameState>().Length,
            "no two interaction states have the same number"
        );
    }
}
