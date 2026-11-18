using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Diagnostics;

/// <summary>
/// A text description of everything a seed places in the world: characters, roads, terrain, buildings, pickups and
/// trees. A refactoring that moves code around must leave it byte for byte the same, or the valleys of saved games
/// would change under the players.
/// </summary>
internal static class WorldFingerprint
{
    private static string F(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string P(Vector3 p) => $"{F(p.X)} {F(p.Y)} {F(p.Z)}";

    private static string Hash(IEnumerable<string> parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", parts)));
        return Convert.ToHexString(bytes)[..16];
    }

    internal static string Folder => Path.Combine(ProjectSettings.GlobalizePath("res://Tests"), "Worlds");

    internal static string FileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.txt");

    /// <summary>
    /// What was added to the world after those files were made: the glassworks, which is placed after the plants so that
    /// nothing in the first files moves. It has a file of its own, kept the same way.
    /// </summary>
    internal static string GlassworksFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.glassworks.txt");

    internal static List<string> GlassworksLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        if (world.Glassworks is { } works)
        {
            lines.Add($"glassworks {P(works.Site.Position)} road {P(works.Site.Road)} inward {P(works.Site.Inward)}");
            lines.Add($"actor {world.ActorId(works.Worker)} {works.Worker.DisplayName} {P(works.Worker.Position)}");
        }
        else
            lines.Add("glassworks none");
        return lines;
    }

    internal static List<string> Lines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        // The glassworks' worker belongs to the file of the glassworks, which was made later.
        foreach (var actor in world.InteractionActors.Where(a => world.ActorId(a) < ActorIds.Glassworks))
            lines.Add(
                $"actor {world.ActorId(actor)} {actor.Role.GetType().Name} {actor.DisplayName} {P(actor.Position)}"
            );
        var village = world.Village;
        lines.Add(
            $"village center {P(village.Center)} garage {P(village.GarageEntrance)} greeting {P(village.GreetingSpot)}"
        );
        foreach (var plot in village.BuildPlots)
            lines.Add($"plot {plot.Index} {P(plot.Position)} drop {P(plot.Drop)} road {P(plot.Road)}");
        foreach (var project in world.Projects)
            lines.Add($"project {project.Index} focus {P(project.Focus)} first drop {P(project.DropFor(0))}");
        foreach (var works in world.Industry)
            lines.Add($"works {works.Site.Index} {P(works.Site.Position)} road {P(works.Site.Road)}");
        foreach (var load in new[] { HaulLoad.Stones, HaulLoad.Soil, HaulLoad.Sand, HaulLoad.Logs })
        foreach (var site in world.SourceSites(load))
            lines.Add($"source {load} {P(site.Position)} road {P(site.Road)}");
        lines.Add($"pickup spots {world.SpecialPickupSpots.Length} hash {Hash(world.SpecialPickupSpots.Select(P))}");
        var samples = world.Routes.Samples;
        lines.Add($"road samples {samples.Count} hash {Hash(samples.Select(P))}");
        var heights = new List<string>();
        for (int x = -500; x <= 500; x += 25)
        for (int z = -500; z <= 500; z += 25)
            heights.Add(F(world.Ground.Height(x, z)));
        lines.Add($"terrain heights {heights.Count} hash {Hash(heights)}");
        lines.Add(
            $"vegetation trees {world.Plants.TreeCount} bushes {world.Plants.BushCount} hash {Hash(world.Plants.TreePositions.Select(P))}"
        );
        return lines;
    }
}

/// <summary>
/// Writes the fingerprint of the current seed (<c>--write-world-fingerprint</c>). An existing file is kept: it
/// was made by the code before the refactoring and is what the checks hold the new code to.
/// </summary>
public partial class WorldFingerprintWriter : Node
{
    private readonly World world;

    public WorldFingerprintWriter(World world) => this.world = world;

    public override void _Ready()
    {
        try
        {
            string path = WorldFingerprint.FileFor(Settings.Seed);
            if (File.Exists(path))
            {
                GD.Print($"KEPT: {path} already exists and stays as it is");
            }
            else
            {
                Directory.CreateDirectory(WorldFingerprint.Folder);
                File.WriteAllLines(path, WorldFingerprint.Lines(world));
                GD.Print($"WROTE: {path}");
            }
            string glassworks = WorldFingerprint.GlassworksFileFor(Settings.Seed);
            if (File.Exists(glassworks))
                GD.Print($"KEPT: {glassworks} already exists and stays as it is");
            else
            {
                File.WriteAllLines(glassworks, WorldFingerprint.GlassworksLines(world));
                GD.Print($"WROTE: {glassworks}");
            }
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}

/// <summary>
/// <c>--world-fingerprint-checks</c>: the world generated for the seed is the one recorded in
/// <c>Tests/Worlds</c>, and a second generation of the same seed gives the same world.
/// </summary>
public partial class WorldFingerprintChecks : Node
{
    private readonly World world;

    public WorldFingerprintChecks(World world) => this.world = world;

    public override void _Ready()
    {
        try
        {
            string path = WorldFingerprint.FileFor(Settings.Seed);
            if (!File.Exists(path))
                throw new InvalidOperationException($"No fingerprint for seed {Settings.Seed}: {path}");
            string glassworksPath = WorldFingerprint.GlassworksFileFor(Settings.Seed);
            if (!File.Exists(glassworksPath))
                throw new InvalidOperationException(
                    $"No glassworks fingerprint for seed {Settings.Seed}: {glassworksPath}"
                );
            var expected = File.ReadAllLines(path).Concat(File.ReadAllLines(glassworksPath)).ToArray();
            var actual = WorldFingerprint.Lines(world).Concat(WorldFingerprint.GlassworksLines(world)).ToList();
            var different = new List<string>();
            for (int i = 0; i < Math.Max(expected.Length, actual.Count); i++)
            {
                string want = i < expected.Length ? expected[i] : "(missing)";
                string have = i < actual.Count ? actual[i] : "(missing)";
                if (want != have)
                    different.Add($"line {i + 1}\n  recorded: {want}\n  now:      {have}");
            }
            if (different.Count > 0)
                throw new InvalidOperationException(
                    $"The world of seed {Settings.Seed} changed in {different.Count} lines:\n"
                        + string.Join("\n", different.Take(12))
                );
            GD.Print(
                $"PASS: the world of seed {Settings.Seed} matches the recorded fingerprint ({expected.Length} lines)"
            );
            GD.Print("WORLD_FINGERPRINT_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}

/// <summary>
/// <c>--contract-checks</c>: the numbers that saved games depend on do not change by accident. Changing one of them
/// on purpose means writing a migration and changing the expectation here in the same commit.
/// </summary>
public partial class ContractChecks : Node
{
    private readonly Game game;
    private readonly World world;

    public ContractChecks(Game game, World world)
    {
        this.game = game;
        this.world = world;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    /// <summary>Saves store these numbers, so a name never moves to another number and no number is reused.</summary>
    private static readonly (string Name, int Value)[] GameStates =
    {
        ("Title", 0),
        ("Exploring", 1),
        ("Challenge", 2),
        ("Reward", 3),
        ("Paused", 4),
        ("ExpeditionChoice", 5),
        ("Garage", 6),
        ("HouseReward", 7),
        ("HouseCollection", 8),
        ("HouseFruitOffer", 9),
        ("FarmStatus", 10),
        ("GardenStatus", 11),
        ("CollectionStatus", 12),
        ("TradeStatus", 13),
        ("PostcardAlbum", 14),
        ("WeatherChoice", 15),
        ("SorcererStatus", 16),
        ("BuildStatus", 17),
        ("WorksStatus", 18),
        ("SourceStatus", 19),
        ("HouseMaterialOffer", 20),
        ("PlotStatus", 21),
    };

    public override void _Ready()
    {
        try
        {
            CheckGameStates();
            CheckActorIds();
            CheckPanels();
            CheckDiagnosticSuites();
            CheckNamespaces();
            GD.Print("CONTRACT_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static void CheckGameStates()
    {
        var actual = Enum.GetValues<GameState>().Select(state => (state.ToString(), (int)state)).ToArray();
        Check(
            actual.SequenceEqual(GameStates),
            "the numbers of GameState are the ones saved games use"
                + (actual.SequenceEqual(GameStates) ? "" : ": " + string.Join(", ", actual.Except(GameStates)))
        );
    }

    private void CheckActorIds()
    {
        Check(
            ActorIds.FirstResident == 30
                && ActorIds.Farmer == 34
                && ActorIds.Gardener == 35
                && ActorIds.GemCollector == 36
                && ActorIds.Toymaker == 37
                && ActorIds.FirstTrader == 38
                && ActorIds.Witch == 41
                && ActorIds.Sorcerer == 42
                && ActorIds.Bandit == 43
                && ActorIds.Beaver == 44
                && ActorIds.FirstWorks == 45
                && ActorIds.LastWorks == 47
                && ActorIds.FirstBuilder == 48
                && ActorIds.LastBuilder == 50
                && ActorIds.FirstSource == 51
                && ActorIds.FirstExtraSource == 54
                && ActorIds.LastSource == 57
                && ActorIds.FirstPlotResident == 58
                && ActorIds.LastPlotResident == 63
                && ActorIds.Glassworks == 64
                && ActorIds.Bakery == 65
                && ActorIds.FieldFarmer == 66,
            "the actor ID ranges are the ones saved games use"
        );

        var seen = new Dictionary<int, Character>();
        foreach (var actor in world.InteractionActors)
        {
            int id = world.ActorId(actor);
            Check(id >= 0 && seen.TryAdd(id, actor), $"actor {actor.DisplayName} has its own ID {id}");
            Check(world.ActorAt(id) == actor, $"ID {id} leads back to {actor.DisplayName}");
        }
        foreach (var (id, actor) in seen)
        {
            string expected =
                id < World.QuestAnimalCount ? nameof(RoadsideFriendRole)
                : id >= World.FirstResidentId && id < World.FarmerId ? nameof(HouseResidentRole)
                : id == World.FarmerId ? nameof(FarmerRole)
                : id == World.GardenerId ? nameof(GardenerRole)
                : id is World.GemCollectorId or World.ToymakerId ? nameof(ExhibitCollectorRole)
                : id >= World.FirstTraderId && id < World.WitchId ? nameof(TraderRole)
                : id == World.WitchId ? nameof(WitchRole)
                : id == World.SorcererId ? nameof(SorcererRole)
                : World.IsBuilder(id) ? nameof(BuilderRole)
                : id >= World.FirstWorksId && id <= World.LastWorksId || id is World.GlassworksId or World.BakeryId
                    ? nameof(WorksRole)
                : id >= World.FirstSourceId && id <= World.LastSourceId || id == World.FieldFarmerId
                    ? nameof(SourceRole)
                : World.IsPlotResident(id) ? nameof(PlotResidentRole)
                : "(no actor may have this ID)";
            Check(actor.Role.GetType().Name == expected, $"ID {id} ({actor.DisplayName}) is a {expected}");
            if (actor.Role is HouseResidentRole house)
                Check(house.House == id - World.FirstResidentId, $"resident ID {id} belongs to house {house.House}");
            if (actor.Role is TraderRole trader)
                Check(trader.Index == id - World.FirstTraderId, $"trader ID {id} sells shop {trader.Index}");
            if (actor.Role is BuilderRole builder)
                Check(builder.Project == World.BuilderProject(id), $"builder ID {id} builds project {builder.Project}");
            if (actor.Role is WorksRole works)
                Check(
                    works.Index
                        == (
                            id == World.GlassworksId ? WorksCatalog.GlassIndex
                            : id == World.BakeryId ? WorksCatalog.BreadIndex
                            : id - World.FirstWorksId
                        ),
                    $"works worker ID {id} serves works {works.Index}"
                );
            if (actor.Role is PlotResidentRole plot)
                Check(
                    plot.Plot.Index == id - World.FirstPlotResidentId,
                    $"newcomer ID {id} lives on plot {plot.Plot.Index}"
                );
        }
        // The ones every valley has.
        foreach (
            int id in Enumerable.Range(0, World.QuestAnimalCount).Concat(Enumerable.Range(World.FirstResidentId, 13))
        )
            Check(seen.ContainsKey(id), $"every valley has the actor with ID {id}");
        Check(seen.ContainsKey(World.BeaverId), "every valley has the beaver");
        for (int id = World.FirstPlotResidentId; id <= World.LastPlotResidentId; id++)
            Check(seen.ContainsKey(id), $"every valley has the newcomer with ID {id}");
        // The quarry, the clay pit and the log yard keep their own IDs in this order.
        for (int index = 0; index < SourceCatalog.Count; index++)
            if (seen.TryGetValue(World.FirstSourceId + index, out var worker))
                Check(
                    worker.Role is SourceRole { Load: var load } && load == SourceCatalog.All[index].Load,
                    $"ID {World.FirstSourceId + index} is the worker of the {SourceCatalog.All[index].Load} site"
                );
        // The second sites: stones, soil, sand and logs.
        var extra = new[] { HaulLoad.Stones, HaulLoad.Soil, HaulLoad.Sand, HaulLoad.Logs };
        for (int index = 0; index < extra.Length; index++)
            if (seen.TryGetValue(World.FirstExtraSourceId + index, out var worker))
                Check(
                    worker.Role is SourceRole { Load: var load } && load == extra[index],
                    $"ID {World.FirstExtraSourceId + index} is the worker of the second {extra[index]} site"
                );
    }

    /// <summary>
    /// Every state that opens a panel has one registered in <c>Game.RegisterPanels</c>, so that it is shown again
    /// after a pause or a load.
    /// </summary>
    private void CheckPanels()
    {
        foreach (var state in Enum.GetValues<GameState>())
        {
            if (state is GameState.Title or GameState.Exploring or GameState.Paused)
                continue;
            Check(game.HasPanel(state), $"an interaction state {state} has a panel");
        }
    }

    /// <summary>
    /// A source file is in the namespace of its folder (<c>Scripts/Saves/SaveStore.cs</c> is in
    /// <c>MathRacer.Saves</c>), a file does not lie loose in <c>Scripts</c>, and no namespace has the name of a type,
    /// which the compiler would not tell apart from it. The one exception is the file of the diagnostic hooks of
    /// <c>Game</c>, which is a part of that class and so has its namespace.
    /// </summary>
    private static void CheckNamespaces()
    {
        var exceptions = new System.Collections.Generic.Dictionary<string, string>
        {
            ["Diagnostics/Game.Diagnostics.cs"] = "MathRacer.App",
        };
        string scripts = ProjectSettings.GlobalizePath("res://Scripts");
        var wrong = new System.Collections.Generic.List<string>();
        int checkedFiles = 0;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories).Order())
        {
            string relative = Path.GetRelativePath(scripts, file).Replace('\\', '/');
            string[] parts = relative.Split('/');
            string expected = exceptions.TryGetValue(relative, out string? exception)
                ? exception
                : "MathRacer." + parts[0];
            var declared = System.Text.RegularExpressions.Regex.Match(
                File.ReadAllText(file),
                @"^namespace (\S+);",
                System.Text.RegularExpressions.RegexOptions.Multiline
            );
            checkedFiles++;
            if (parts.Length < 2 || !declared.Success || declared.Groups[1].Value != expected)
                wrong.Add($"{relative} should be in {expected}");
        }
        Check(
            wrong.Count == 0,
            $"{checkedFiles} source files are in the namespace of their folder"
                + (wrong.Count == 0 ? "" : ": " + string.Join("; ", wrong))
        );
        var assembly = typeof(Game).Assembly;
        var clashes = assembly
            .GetTypes()
            .Select(type => type.Namespace)
            .Where(name => name != null && name.Contains('.'))
            .Distinct()
            .Where(name => assembly.GetType(name!) != null)
            .ToList();
        Check(
            clashes.Count == 0,
            "no namespace has the name of a type" + (clashes.Count == 0 ? "" : ": " + string.Join(", ", clashes))
        );
    }

    /// <summary>
    /// Every suite in <c>Scripts/Diagnostics</c> is listed in <see cref="DiagnosticSuites"/>, so that the game can start
    /// it and the test run includes it. Helpers that are not suites on their own are named here.
    /// </summary>
    private static void CheckDiagnosticSuites()
    {
        // A static class is a helper that other suites call. RoadDrivingChecks is started by the road driving run
        // itself, and WorldContractChecks holds several suites under other names.
        string[] helpers = { "RoadDrivingChecks", "WorldContractChecks" };
        string source = File.ReadAllText(
            ProjectSettings.GlobalizePath("res://Scripts/Diagnostics/DiagnosticSuites.cs")
        );
        string folder = ProjectSettings.GlobalizePath("res://Scripts/Diagnostics");
        foreach (string file in Directory.GetFiles(folder, "*.cs").Order())
        {
            string name = Path.GetFileNameWithoutExtension(file);
            if (
                !(name.EndsWith("Checks") || name == "SmokeTest")
                || helpers.Contains(name)
                || File.ReadAllText(file).Contains($"static class {name}")
            )
                continue;
            Check(source.Contains($"new {name}("), $"the suite {name} is listed in DiagnosticSuites");
        }
        var modes = DiagnosticSuites.All.Where(suite => suite.Create != null).Select(suite => suite.Mode).ToArray();
        Check(modes.Distinct().Count() == modes.Length, "every suite has a flag of its own");
        foreach (var suite in DiagnosticSuites.All.Where(suite => suite.InSuite))
            Check(
                suite.Mode.EndsWith("-checks") || suite.Mode is "smoke-test" or "road-audit",
                $"{suite.Mode} is a flag that leaves the player's save alone"
            );
    }
}
