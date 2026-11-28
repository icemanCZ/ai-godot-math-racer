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
        ("PlaygroundStatus", 22),
        ("MayorShop", 23),
        ("GlassmakerShop", 24),
        ("RailwayStatus", 25),
        ("ZooStatus", 26),
        ("TreasureStatus", 27),
        ("FireflyStatus", 28),
        ("HunterStatus", 29),
        ("KeeperStatus", 30),
        ("PostStatus", 31),
        ("LighthouseStatus", 32),
        ("DragonStatus", 33),
        ("BalloonStatus", 34),
        ("TrackingStatus", 35),
        ("ShepherdStatus", 36),
        ("RallyStatus", 37),
        ("ExplorerStatus", 38),
        ("FerryStatus", 39),
        ("CastleStatus", 40),
        ("MainLineStatus", 41),
        ("ShipStatus", 42),
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
            CheckValleyBounds();
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
                && ActorIds.FieldFarmer == 66
                && ActorIds.Playground == 67
                && ActorIds.Mayor == 68
                && ActorIds.Glassmaker == 69
                && ActorIds.Station == 70
                && ActorIds.ZooKeeper == 71
                && ActorIds.Treasurer == 72
                && ActorIds.Hunter == 73
                && ActorIds.FirstBorderKeeper == 74
                && ActorIds.LastBorderKeeper == 77
                && ActorIds.Postman == 78
                && ActorIds.Dragon == 79
                && ActorIds.Balloonist == 80
                && ActorIds.TrackingAnimal == 81
                && ActorIds.Cartographer == 82
                && ActorIds.Birdwatcher == 83
                && ActorIds.Castellan == 84
                && ActorIds.Engineer == 85
                && ActorIds.Captain == 86,
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
                : id == World.PlaygroundId ? nameof(PlaygroundRole)
                : id == World.MayorId ? nameof(MayorRole)
                : id == World.GlassmakerId ? nameof(GlassmakerRole)
                : id == World.StationId ? nameof(StationRole)
                : id == World.ZooKeeperId ? nameof(ZooRole)
                : id == World.TreasurerId ? nameof(TreasureRole)
                : id == World.HunterId ? nameof(HunterRole)
                : World.IsBorderKeeper(id) ? nameof(BorderKeeperRole)
                : id == World.PostmanId ? nameof(PostmanRole)
                : World.IsDragon(id) ? nameof(DragonRole)
                : World.IsBalloonist(id) ? nameof(BalloonistRole)
                : World.IsTrackingAnimal(id) ? nameof(TrackingAnimalRole)
                : World.IsCartographer(id) ? nameof(CartographerRole)
                : World.IsBirdwatcher(id) ? nameof(BirdwatcherRole)
                : World.IsCastellan(id) ? nameof(CastleRole)
                : World.IsEngineer(id) ? nameof(EngineerRole)
                : World.IsCaptain(id) ? nameof(CaptainRole)
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
        Check(seen.ContainsKey(World.MayorId), "every valley has the mayor");
        Check(seen.ContainsKey(World.GlassmakerId), "every valley has the lamp maker");
        Check(seen.ContainsKey(World.StationId), "every valley has the stationmaster");
        Check(seen.ContainsKey(World.TreasurerId), "every valley has the pirate captain");
        Check(seen.ContainsKey(World.HunterId), "every valley has the hunter");
        for (int id = ActorIds.FirstBorderKeeper; id <= ActorIds.LastBorderKeeper; id++)
            Check(seen.ContainsKey(id), $"every valley has the keeper of the border with ID {id}");
        Check(seen.ContainsKey(World.PostmanId), "every valley has the postman");
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
    /// Where the valley ends while it is played (<see cref="ValleyBounds"/>) is asked only by the code that limits play:
    /// the car, the grass, the lamps, the fireflies, the wild animals and the hunt, the coast quarry beside the road to
    /// the sea, the dragon's ledge and his mountain slope, the balloon station, the forest keeper's trails, the
    /// shepherd's sheep, and the checks. The generation of the old valley keeps to <see cref="WorldTuning.CorePlayableRadius"/>, or a valley that
    /// grows would move what saved games already have. A new use elsewhere is a decision to be made on purpose, and to be added here.
    /// </summary>
    private static void CheckValleyBounds()
    {
        string[] allowed =
        {
            "Saves/SaveRules.cs",
            "Settlement/LampField.cs",
            "Valley/Grass.cs",
            "Valley/HuntField.cs",
            "Valley/ValleyBorders.cs",
            "Valley/Vegetation.Outland.cs",
            "Valley/Wayside.cs",
            "Valley/Wildlife.cs",
            "Valley/World.Borderfolk.cs",
            "Valley/World.Balloon.cs",
            "Valley/World.Tracking.cs",
            "Valley/World.Sheep.cs",
            "Valley/World.Dragon.cs",
            "Valley/World.Explorer.cs",
            "Valley/World.Shells.cs",
            "Valley/World.Ferry.cs",
            "Valley/World.Castle.cs",
            "Valley/World.MainLine.cs",
            "Valley/World.Ship.cs",
            "Valley/World.Fireflies.cs",
            "Valley/World.Hunter.cs",
            "Valley/World.Outland.cs",
            "Vehicles/Vehicle.cs",
        };
        string scripts = ProjectSettings.GlobalizePath("res://Scripts");
        var users = Directory
            .GetFiles(scripts, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(scripts, file).Replace('\\', '/'))
            .Where(relative => !relative.StartsWith("Diagnostics/"))
            .Where(relative => File.ReadAllText(Path.Combine(scripts, relative)).Contains("ValleyBounds."))
            .Order()
            .ToList();
        var unexpected = users.Except(allowed).ToList();
        Check(
            unexpected.Count == 0,
            "only the code that limits play asks where the valley ends"
                + (unexpected.Count == 0 ? "" : ": " + string.Join(", ", unexpected))
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
