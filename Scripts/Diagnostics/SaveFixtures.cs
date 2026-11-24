using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>Where the committed save fixtures live and which ones must exist.</summary>
internal static class SaveFixtureFiles
{
    /// <summary>Saves written by the game itself; they are never edited or regenerated (see Tests/Saves/README.md).</summary>
    internal static string Root => ProjectSettings.GlobalizePath("res://Tests/Saves");

    /// <summary>The folder of the format the game writes now; the folders of older formats stay as they are.</summary>
    internal const string CurrentFolder = "v5";

    /// <summary>One folder for each version of the save format that fixtures were made with.</summary>
    internal static readonly string[] Folders = { "v3", "v4", "v5" };

    /// <summary>The seed every fixture was made with.</summary>
    internal const int Seed = 42;

    /// <summary>
    /// Deleting one of these fails the checks, so a fixture cannot disappear silently. Names are stable; the
    /// generator writes each one once and refuses to overwrite it.
    /// </summary>
    internal static readonly string[] Required =
    {
        "exploring",
        "quiz-math",
        "quiz-letters",
        "quiz-syllables",
        "quiz-reading",
        "quiz-math-wrong-answer",
        "quiz-syllables-partial",
        "expedition-choice",
        "collection-in-progress",
        "delivery-started",
        "delivery-question",
        "reward-panel",
        "garage-open",
        "builder-panel",
        // Saved while the quiz before a trip is open; made after the game learnt to keep the trip (see Departure).
        "quiz-before-build-trip",
        "quiz-before-plot-trip",
        "quiz-before-house-trip",
        "quiz-before-missing-supply",
        "quiz-before-golem-trip",
        "quiz-before-fruit-trip",
        "build-trip-empty",
        "build-loading-question",
        "build-trip-loaded",
        "paused-in-build-trip",
        "source-panel",
        "supply-loading-question",
        "supply-trip-empty",
        "supply-trip-loaded",
        "works-panel",
        "house-material-panel",
        "house-trip-empty",
        "house-trip-loaded",
        "plot-panel",
        "plot-trip-empty",
        "plot-trip-loaded",
        "plots-partial",
        "bandit-encounter",
        "event-run-active",
        "progress-rich",
    };

    internal static string Path(string folder, string name) => System.IO.Path.Combine(Root, folder, name + ".json");
}

/// <summary>Moves the car through a trailer trip with the real game logic, the way the checks do.</summary>
internal static class TripDriver
{
    internal static void Frames(Game game, int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    internal static void DriveTo(Game game, World world, Vehicle car, Vector3 point)
    {
        car.SetPose(world.Ground.At(point.X, point.Z, .15f), 0);
        Frames(game, 1);
    }

    /// <summary>Loads the trailer at the source, answers the loading question and tips the load out.</summary>
    internal static void Finish(Game game, World world, Vehicle car)
    {
        for (int guard = 0; guard < 4 && game.Haul != null; guard++)
        {
            var trip = game.Haul;
            if (!trip.Loaded)
            {
                DriveTo(game, world, car, trip.Source.Position);
                ChallengeChecks.Solve(game);
                Frames(game, 1);
            }
            else
            {
                DriveTo(game, world, car, trip.Destination);
                Frames(game, 40);
            }
        }
        if (game.Haul != null)
            throw new InvalidOperationException("the trailer trip did not finish");
    }

    /// <summary>Everything that a finished trip, delivery or building step changes.</summary>
    internal static string Progress(SaveData data) =>
        string.Join(
            "|",
            data.Economy.Coins,
            string.Join(",", data.Buildings.Stages ?? Array.Empty<int>()),
            string.Join(",", data.Buildings.PlotStages),
            string.Join(",", data.Village.HouseLevels),
            string.Join(",", data.Buildings.WorksStock)
        );
}

/// <summary>
/// Writes the save fixtures (<c>--save-fixtures</c>): one real save for each situation a player can quit in. A
/// fixture that already exists is kept as it is, because a fixture is only worth something if the code that wrote it
/// is older than the code it tests.
/// </summary>
public partial class SaveFixtures : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private SaveData baseline = null!;
    private int written;
    private int kept;
    private readonly string folder;

    public SaveFixtures(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
        folder = LaunchOptions.Value("--fixture-folder") ?? SaveFixtureFiles.CurrentFolder;
    }

    private void Frames(int count) => TripDriver.Frames(game, count);

    private void DriveTo(Vector3 point) => TripDriver.DriveTo(game, world, car, point);

    private void Near(Character actor) => car.SetPose(world.Ground.At(actor.Position.X + 2, actor.Position.Z, .15f), 0);

    private void Reset() => game.RestoreProgress(baseline);

    private void Fill()
    {
        foreach (var works in world.Industry)
            works.SetStock(Works.Capacity);
    }

    private void Save(string name, SaveData? data = null)
    {
        data ??= game.CaptureProgress();
        string path = SaveFixtureFiles.Path(folder, name);
        if (File.Exists(path))
        {
            kept++;
            GD.Print($"KEPT: {name} already exists and stays as it is");
            return;
        }
        if (data.Seed != SaveFixtureFiles.Seed)
            throw new InvalidOperationException($"{name}: fixtures are made with seed {SaveFixtureFiles.Seed}");
        if (!SaveStore.Write(data, path))
            throw new InvalidOperationException($"{name}: cannot write {path}");
        if (SaveStore.Read(path) == null)
        {
            File.Delete(path);
            throw new InvalidOperationException($"{name}: the game does not accept its own save");
        }
        written++;
        GD.Print($"WROTE: {name} (state {data.State})");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public override async void _Ready()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(SaveFixtureFiles.Root, folder));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(Settings.Seed == SaveFixtureFiles.Seed, $"run with --seed={SaveFixtureFiles.Seed}");
            baseline = game.CaptureProgress();
            Save("exploring");
            Quizzes();
            Expeditions();
            Garage();
            BuildTrip();
            QuizzesBeforeTrips();
            SupplyTrip();
            HouseTrip();
            PlotTrips();
            Events();
            await Bandits();
            Rich();
            var missing = SaveFixtureFiles
                .Required.Where(name => !File.Exists(SaveFixtureFiles.Path(folder, name)))
                .ToList();
            Require(
                missing.Count == 0 || folder != SaveFixtureFiles.CurrentFolder,
                "missing fixtures: " + string.Join(", ", missing)
            );
            GD.Print($"SAVE_FIXTURES_DONE written={written} kept={kept}");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static string QuizName(IChallenge question) =>
        question switch
        {
            MathChallenge => "quiz-math",
            LetterChallenge => "quiz-letters",
            SyllableChallenge => "quiz-syllables",
            ReadingChallenge => "quiz-reading",
            _ => "",
        };

    private void Quizzes()
    {
        Reset();
        var seen = new HashSet<string>();
        // The kinds rotate from friend to friend, so opening several friends in turn meets each of them.
        for (int i = 0; i < world.Animals.Length && seen.Count < 4; i++)
        {
            var animal = world.Animals[i];
            Near(animal);
            game.OpenChallenge(animal);
            var question = game.CurrentChallenge;
            Require(question != null, "a roadside friend asks a question");
            string name = QuizName(question!);
            if (name != "" && seen.Add(name))
            {
                Save(name);
                if (question is MathChallenge)
                {
                    game.SubmitAnswer(question.Choices.First(choice => !question.Accepts(choice)));
                    Save("quiz-math-wrong-answer");
                }
                if (question is SyllableChallenge syllables)
                {
                    game.SubmitAnswer(syllables.Choices.First(syllables.Accepts));
                    Save("quiz-syllables-partial");
                }
            }
            game.FinishInteraction();
        }
        Require(seen.Count == 4, "all four kinds of question were met: " + string.Join(", ", seen));
    }

    private void Expeditions()
    {
        Reset();
        var first = world.Animals[0];
        Near(first);
        game.OpenChallenge(first);
        ChallengeChecks.Solve(game);
        Require(game.State == GameState.ExpeditionChoice, "a solved opening quiz offers the expedition choice");
        Save("expedition-choice");
        game.SelectExpedition(true);
        Require(game.Delivery != null, "the collection starts");
        car.SetPose(game.Delivery!.PickupPositions[1], 1.2f);
        game.Delivery.Tick(.25);
        Require(game.Delivery.Collected >= 1, "an apple is picked");
        Save("collection-in-progress");
        foreach (var point in game.Delivery.PickupPositions)
        {
            car.SetPose(point, 0);
            game.Delivery.Tick(.25);
        }
        car.SetPose(game.Delivery.Recipient.Position + Vector3.Right * 2, .5f);
        game.BeginDeliveryQuestion();
        Require(game.State == GameState.Challenge, "the handover asks a question");
        Save("delivery-question");
        ChallengeChecks.Solve(game);
        Require(game.State == GameState.Reward, "the answer pays and opens the reward panel");
        Save("reward-panel");
        game.FinishInteraction();

        Reset();
        var second = world.Animals[1];
        Near(second);
        game.OpenChallenge(second);
        ChallengeChecks.Solve(game);
        game.SelectExpedition(false);
        Require(game.Delivery is { Collection: false }, "the delivery starts");
        Save("delivery-started");
    }

    private void Garage()
    {
        Reset();
        car.SetPose(world.Village.GarageEntrance, 0);
        game.AddCoins(60);
        game.OpenGarage();
        Require(game.State == GameState.Garage, "the garage opens");
        Save("garage-open");
        game.FinishInteraction();
    }

    private void BuildTrip()
    {
        Reset();
        Fill();
        var project = world.Projects[BuildPlans.Bridge];
        car.SetPose(world.Ground.At(project.Builder.Position.X + 2, project.Builder.Position.Z, .15f), 0);
        game.OpenBuilder(project.Builder);
        Require(game.State == GameState.BuildStatus, "the builder's panel opens");
        Save("builder-panel");
        game.BeginBuildTrip();
        Require(game.State == GameState.Challenge, "a quiz comes before the trip");
        Save("quiz-before-build-trip");
        ChallengeChecks.Solve(game);
        Require(game.Haul is { Project: not null, Loaded: false }, "the trip starts empty");
        Save("build-trip-empty");
        DriveTo(game.Haul!.Source.Position);
        Require(game.State == GameState.Challenge, "arriving at the source asks the loading question");
        Save("build-loading-question");
        ChallengeChecks.Solve(game);
        Require(game.Haul is { Loaded: true }, "the trailer is loaded");
        Save("build-trip-loaded");
        game.TogglePause();
        Save("paused-in-build-trip");
        game.TogglePause();
    }

    /// <summary>Puts one house at a level where its next task is the one asked for, and returns the house.</summary>
    private int HouseWithTask(Func<VillageTaskKind, bool> wanted)
    {
        var village = world.Village;
        for (int house = 0; house < village.Residents.Count; house++)
        for (int level = 0; level < Village.Upgrades.Length; level++)
        {
            var levels = new int[village.Residents.Count];
            levels[house] = level;
            village.RestoreLevels(levels);
            if (wanted(village.TaskAt(house)))
                return house;
        }
        throw new InvalidOperationException("no house has the task");
    }

    /// <summary>
    /// The quiz that comes before every trip, saved while it is open: the trip must still follow it after the game
    /// was closed and opened again.
    /// </summary>
    private void QuizzesBeforeTrips()
    {
        var village = world.Village;

        Reset();
        Fill();
        var plot = village.BuildPlots[0];
        Near(plot.Resident);
        game.OpenPlot(plot.Resident);
        game.BeginPlotTrip();
        Require(game.State == GameState.Challenge && game.Haul == null, "a quiz comes before the plot trip");
        Save("quiz-before-plot-trip");

        Reset();
        Fill();
        var owner = village.Residents[HouseWithTask(task => Village.MaterialOf(task) != null)];
        Near(owner);
        game.OpenChallenge(owner);
        game.BeginHouseTrip();
        Require(game.State == GameState.Challenge && game.Haul == null, "a quiz comes before the house trip");
        Save("quiz-before-house-trip");

        Reset();
        foreach (var works in world.Industry)
            works.SetStock(0);
        var builder = world.Projects[BuildPlans.Bridge].Builder;
        Near(builder);
        game.OpenBuilder(builder);
        game.BeginMissingSupply();
        Require(game.State == GameState.Challenge && game.Haul == null, "a quiz comes before the raw material trip");
        Save("quiz-before-missing-supply");

        Reset();
        Near(world.Sorcerer);
        game.OpenSorcerer();
        game.BeginGolemQuest();
        Require(game.State == GameState.Challenge && game.Delivery == null, "a quiz comes before the golem's trip");
        Save("quiz-before-golem-trip");

        Reset();
        var fruitOwner = village.Residents[HouseWithTask(task => task == VillageTaskKind.Fruit)];
        Near(fruitOwner);
        game.OpenChallenge(fruitOwner);
        game.BeginVillageCollection();
        Require(game.State == GameState.Challenge && game.Delivery == null, "a quiz comes before the apple trip");
        Save("quiz-before-fruit-trip");
        game.FinishInteraction();
    }

    private void SupplyTrip()
    {
        Reset();
        var logger = world.SourceWorker(HaulLoad.Logs)!;
        car.SetPose(world.Ground.At(logger.Position.X + 2, logger.Position.Z, .15f), 0);
        game.OpenSource(logger);
        Require(game.State == GameState.SourceStatus, "the log yard worker opens a panel");
        Save("source-panel");
        game.BeginSupplyTrip();
        Require(
            game.Haul is { Works: not null, Loaded: false } && game.State == GameState.Challenge,
            "the loading question comes at once"
        );
        Save("supply-loading-question");
        game.FinishInteraction();
        car.SetPose(world.Village.Center + Vector3.Right * 30, 0);
        Frames(2);
        Require(
            game.Haul is { Works: not null, Loaded: false } && game.State == GameState.Exploring,
            "the empty trip goes on"
        );
        Save("supply-trip-empty");
        DriveTo(game.Haul!.Source.Position);
        ChallengeChecks.Solve(game);
        Require(game.Haul is { Loaded: true }, "the raw material is loaded");
        Save("supply-trip-loaded");
        game.FinishInteraction();

        Reset();
        var works = world.Industry[0];
        car.SetPose(world.Ground.At(works.Worker.Position.X + 2, works.Worker.Position.Z, .15f), 0);
        game.OpenWorks(works.Worker);
        Require(game.State == GameState.WorksStatus, "the works' worker shows the stock");
        Save("works-panel");
        game.FinishInteraction();
    }

    private void HouseTrip()
    {
        Reset();
        var village = world.Village;
        int house = -1;
        for (int candidate = 0; candidate < village.Residents.Count && house < 0; candidate++)
        for (int level = 0; level < Village.Upgrades.Length && house < 0; level++)
        {
            var levels = new int[village.Residents.Count];
            levels[candidate] = level;
            village.RestoreLevels(levels);
            if (Village.MaterialOf(village.TaskAt(candidate)) != null)
                house = candidate;
        }
        Require(house >= 0, "a house wants goods at some level");
        Fill();
        var owner = village.Residents[house];
        Near(owner);
        game.OpenChallenge(owner);
        Require(game.State == GameState.HouseMaterialOffer, "the resident asks for goods");
        Save("house-material-panel");
        game.BeginHouseTrip();
        ChallengeChecks.Solve(game);
        Require(game.Haul is { House: >= 0, Loaded: false }, "the house trip starts empty");
        Save("house-trip-empty");
        DriveTo(game.Haul!.Source.Position);
        ChallengeChecks.Solve(game);
        Require(game.Haul is { Loaded: true }, "the goods are loaded");
        Save("house-trip-loaded");
        game.FinishInteraction();
    }

    private void Deliver(HousePlot plot, bool save)
    {
        Fill();
        Near(plot.Resident);
        game.OpenPlot(plot.Resident);
        if (save && plot.Index == 0 && plot.Stage == 0)
            Save("plot-panel");
        game.BeginPlotTrip();
        ChallengeChecks.Solve(game);
        Require(game.Haul is { Plot: not null, Loaded: false }, "the plot trip starts empty");
        if (save && plot.Index == 0 && plot.Stage == 0)
            Save("plot-trip-empty");
        DriveTo(game.Haul!.Source.Position);
        ChallengeChecks.Solve(game);
        Require(game.Haul is { Loaded: true }, "the goods for the plot are loaded");
        if (save && plot.Index == 0 && plot.Stage == 0)
            Save("plot-trip-loaded");
        TripDriver.Finish(game, world, car);
    }

    private void PlotTrips()
    {
        Reset();
        var plots = world.Village.BuildPlots;
        Deliver(plots[0], true);
        Deliver(plots[1], false);
        Deliver(plots[0], false);
        Deliver(plots[2], false);
        Require(plots[0].Stage == 2 && plots[1].Stage == 1 && plots[2].Stage == 1, "several plots are under way");
        Save("plots-partial");
    }

    private void Events()
    {
        Reset();
        var events = game.Events;
        events.Begin(0);
        for (int gate = 0; gate < 5; gate++)
        {
            car.SetPose(events.Points[0, gate], 0);
            game._Process(.05);
            game._Process(.05);
        }
        Require(events.Active == 0 && events.RunSplits.Count > 0, "a course run is under way");
        Save("event-run-active");
        events.Cancel();
    }

    private async Task Bandits()
    {
        Reset();
        for (int site = 0; site < game.BanditSiteCount; site++)
        {
            var (road, along) = game.BanditApproach(site);
            var start = road - along * 6;
            start.Y = world.Routes.SurfaceHeight(start, world.Ground.Height(start.X, start.Z)) + .15f;
            car.SetPose(start, Mathf.Atan2(-along.X, -along.Z));
            Input.ActionPress("accelerate");
            for (int i = 0; i < 24; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ActionRelease("accelerate");
            for (int i = 0; i < 100; i++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var data = game.CaptureProgress();
            if (game.State == GameState.Challenge && data.Nearby == Game.BanditActorId)
            {
                Save("bandit-encounter", data);
                game.FinishInteraction();
                return;
            }
        }
        throw new InvalidOperationException("no bandit encounter started");
    }

    /// <summary>A save with plenty of progress of every kind.</summary>
    private void Rich()
    {
        Reset();
        var village = world.Village;
        game.AddCoins(137);
        game.OwnedRewards.Add(new("wheels", true));
        game.OwnedRewards.Add(new("buggy", false, true));
        village.RestoreFarm(3);
        village.RestoreGarden(2);
        village.RestoreCollection(false, 4);
        village.RestoreCollection(true, 1);
        village.RestorePlots(new[] { 6, 3, 0, 1, 0, 0 });
        for (int i = 0; i < world.Projects.Length; i++)
            world.Projects[i].SetStage(new[] { 10, 4, 10, 2 }[i]);
        world.Industry[0].SetStock(5);
        world.Industry[1].SetStock(3);
        world.Industry[2].SetStock(Works.Capacity);
        var data = game.CaptureProgress();
        data.Buildings.TrailerOwned = true;
        data.Buildings.TrailerPaint = "blue";
        Save("progress-rich", data);
    }
}

/// <summary>
/// Loads every committed save fixture (<c>--save-fixture-checks</c>), restores it into the game, checks that the game
/// captures the same save back, lets the game run, and carries on from wherever the player quit: the quiz is
/// answered, the trailer trip is driven to its end, the delivery or the course run is finished. Nothing in
/// <c>Tests/Saves</c> is ever changed to make this pass.
/// </summary>
public partial class SaveFixtureChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private SaveData baseline = null!;
    private int savedFiles;

    public SaveFixtureChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private void Frames(int count) => TripDriver.Frames(game, count);

    private void Accepted(string what)
    {
        string path = DiagnosticArtifacts.PathFor("save-fixture-checks/after.json");
        Check(
            SaveStore.Write(game.CaptureProgress(), path) && SaveStore.Read(path) != null,
            "the game accepts its own save " + what
        );
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            baseline = game.CaptureProgress();
            var missing = SaveFixtureFiles
                .Folders.SelectMany(folder =>
                    SaveFixtureFiles
                        .Required.Where(name => !File.Exists(SaveFixtureFiles.Path(folder, name)))
                        .Select(name => folder + "/" + name)
                )
                .ToList();
            Check(
                missing.Count == 0,
                "no committed fixture is missing" + (missing.Count == 0 ? "" : ": " + string.Join(", ", missing))
            );
            Check(LegacySaves.CoversEverySection(), "every field of every section has the flat field it came from");
            int used = 0;
            foreach (string folder in Directory.GetDirectories(SaveFixtureFiles.Root, "v*").Order())
            foreach (string file in Directory.GetFiles(folder, "*.json").Order())
            {
                string name = Path.GetFileName(folder) + "/" + Path.GetFileNameWithoutExtension(file);
                var data = SaveStore.Read(file);
                Check(data != null, $"{name}: the game still accepts this save");
                if (data!.Seed != Settings.Seed)
                {
                    GD.Print($"SKIP: {name} was made with seed {data.Seed}");
                    continue;
                }
                used++;
                if (name.StartsWith("v4/"))
                    CheckMigrationKeepsEveryField(name, file, data);
                if (name == "v5/exploring")
                    CheckSections(data);
                await Continue(name, data);
            }
            Check(
                used >= SaveFixtureFiles.Required.Length * SaveFixtureFiles.Folders.Length,
                $"{used} fixtures of seed {Settings.Seed} were checked"
            );
            GD.Print("SAVE_FIXTURE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    /// <summary>
    /// A save of version 4 is read into the sections of version 5 without losing or changing a field: flattened again,
    /// it is the file that was read.
    /// </summary>
    private void CheckMigrationKeepsEveryField(string name, string file, SaveData data)
    {
        var original = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!.AsObject();
        var flat = LegacySaves.FromCurrent(data, SaveDataV4.LastVersion);
        var moved = new[] { "Seed", "State", "Nearby" }.Concat(LegacySaves.Fields.Select(field => field.Flat)).ToList();
        var different = moved
            .Where(key => !System.Text.Json.Nodes.JsonNode.DeepEquals(flat[key], original[key]))
            .ToList();
        Check(
            different.Count == 0,
            $"{name}: every field moved to its section unchanged"
                + (different.Count == 0 ? "" : ": " + string.Join(", ", different))
        );
    }

    /// <summary>A section a file leaves out has its defaults, and one that is null is not a save.</summary>
    private void CheckSections(SaveData data)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToNode(data)!.AsObject();
        string path = DiagnosticArtifacts.PathFor($"save-fixture-checks/sections-{savedFiles++}.json");
        var withoutVillage = json.DeepClone().AsObject();
        withoutVillage.Remove("Village");
        File.WriteAllText(path, withoutVillage.ToJsonString());
        Check(
            SaveStore.Read(path) is { Village: { HouseLevels: [0, 0, 0, 0], FarmCount: 0 } },
            "a save without the village section has an empty village"
        );
        foreach (string section in new[] { "Vehicle", "Economy", "Quiz", "Task", "Roadside", "Buildings", "Events" })
        {
            var nulled = json.DeepClone().AsObject();
            nulled[section] = null;
            path = DiagnosticArtifacts.PathFor($"save-fixture-checks/sections-{savedFiles++}.json");
            File.WriteAllText(path, nulled.ToJsonString());
            Check(SaveStore.Read(path) == null, $"a save whose {section} section is null is rejected");
        }
    }

    /// <summary>A pending trip that does not fit the character who asks the quiz is not a save the game accepts.</summary>
    private void RejectDamagedDepartures(SaveData data)
    {
        void Rejected(Action<SaveData> damage, string what)
        {
            var copy = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(data))!;
            damage(copy);
            string path = DiagnosticArtifacts.PathFor($"save-fixture-checks/damaged-departure-{savedFiles++}.json");
            Check(SaveStore.Write(copy, path) && SaveStore.Read(path) == null, $"a save is rejected: {what}");
        }
        Rejected(save => save.Quiz.Departure = 99, "a trip that does not exist waits for the quiz");
        Rejected(
            save => save.Quiz.Departure = (int)Game.Departure.Plot,
            "a plot trip waits for a quiz asked by a builder"
        );
        Rejected(save => save.Quiz.DepartureState = GameState.PlotStatus, "the quiz was asked from another panel");
        Rejected(
            save => save.Quiz.Departure = (int)Game.Departure.Golem,
            "the golem's trip waits for a quiz asked by a builder"
        );
        Rejected(
            save => save.Quiz.Departure = 0,
            "no trip waits, so the panel's character cannot be asking an ordinary quiz"
        );
        Rejected(save => save.Quiz.Handover = true, "a trip waits for a handover quiz");
    }

    /// <summary>
    /// The trip of a save is one object of a kind that exists, for a target that needs a delivery, and a save of
    /// the older format with a pair of fields for each kind of trip still loads into it.
    /// </summary>
    private void CheckTripSaves(SaveData data)
    {
        var trip = data.Task.Trip!;
        SaveData Copy() => JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(data))!;
        SaveData? Reloaded(SaveData copy)
        {
            // A file of its own each time: a damaged file would fall back to the backup of the one before.
            string path = DiagnosticArtifacts.PathFor($"save-fixture-checks/trip-save-{savedFiles++}.json");
            return SaveStore.Write(copy, path) ? SaveStore.Read(path) : null;
        }
        void Rejected(Action<SaveData> damage, string what)
        {
            var copy = Copy();
            damage(copy);
            Check(Reloaded(copy) == null, $"a save is rejected: {what}");
        }
        Rejected(
            save => save.Task.Trip = new SavedTrip { Kind = "bogus", Index = 0 },
            "a trip of a kind that does not exist"
        );
        Rejected(save => save.Task.Trip!.Index = BuildPlans.Count, "a trip to a fifth building");
        Rejected(save => save.Task.Trip!.Index = -1, "a trip to no building");
        Rejected(save => save.Task.Sender = 3, "a trip together with an expedition");

        // A save of version 3 has a pair of fields for each kind of trip.
        var old = LegacySaves.FromCurrent(data, 3);
        old.Remove("Trip");
        old["BuildTrip"] = trip.Index;
        old["BuildTripLoaded"] = trip.Loaded;
        var migrated = LegacySaves.Read(old);
        Check(
            migrated is { Version: SaveData.CurrentVersion, Task: { Trip: { } moved } }
                && moved.Kind == trip.Kind
                && moved.Index == trip.Index
                && moved.Loaded == trip.Loaded,
            "a version 3 save with a building trip migrates to Trip"
        );
        Check(
            migrated != null && !JsonSerializer.Serialize(migrated).Contains("\"BuildTrip\""),
            "a migrated save is written without the old trip fields"
        );
        Check(LegacySaves.IsRejected(data, flat =>
            {
                flat.Remove("Trip");
                flat["BuildTrip"] = trip.Index;
                flat["SupplyTrip"] = 0;
            }), "a version 3 save with two trips is rejected");
        Check(LegacySaves.IsRejected(data, flat =>
            {
                flat.Remove("Trip");
                flat["PlotTripLoaded"] = true;
            }), "a version 3 save with a loaded trailer and no trip is rejected");
        Check(LegacySaves.IsRejected(data, flat =>
            {
                flat.Remove("Trip");
                flat["HouseTrip"] = -2;
            }), "a version 3 save with a trip number below -1 is rejected");
    }

    private async Task Continue(string name, SaveData data)
    {
        if (name.EndsWith("quiz-before-build-trip"))
            RejectDamagedDepartures(data);
        if (name.EndsWith("/build-trip-loaded"))
            CheckTripSaves(data);
        game.RestoreProgress(baseline);
        game.RestoreProgress(data);
        // The game switches on the kinds of quiz the save did not know yet and writes that it knows them all.
        var expected = JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(data))!;
        expected.Quiz.Types = data.Quiz.EnabledTypes();
        expected.Quiz.Offered = QuizType.All;
        Check(
            JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(game.CaptureProgress()),
            $"{name}: restoring and capturing again gives the same save"
        );
        Check(
            data.Quiz.Offered.HasFlag(QuizType.Compare) || game.CaptureProgress().Quiz.Types.HasFlag(QuizType.Compare),
            $"{name}: comparing numbers, which the save did not know yet, is switched on"
        );
        Check(
            game.Coins == data.Economy.Coins && game.OwnedRewards.Count == data.Economy.Owned.Length,
            $"{name}: coins and garage items are kept"
        );
        // The game goes on running from the restored state.
        if (game.State == GameState.Exploring)
            Frames(30);
        Accepted($"{name}: after three seconds");
        string before = TripDriver.Progress(game.CaptureProgress());

        // An open question is answered. A bandit encounter has three of them.
        bool bandits = data.Nearby == Game.BanditActorId;
        for (int i = 0; i < (bandits ? 6 : 1) && game.State == GameState.Challenge; i++)
        {
            ChallengeChecks.Solve(game);
            Frames(12);
            for (int frame = 0; frame < 10; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Check(game.State != GameState.Challenge, $"{name}: the question can be answered");

        // A quiz that was open before a trip still starts the trip once it is solved.
        bool? started = name[(name.LastIndexOf('/') + 1)..] switch
        {
            "quiz-before-build-trip" => game.Haul is { Project: not null, Loaded: false },
            "quiz-before-plot-trip" => game.Haul is { Plot: not null, Loaded: false },
            "quiz-before-house-trip" => game.Haul is { House: >= 0, Loaded: false },
            "quiz-before-missing-supply" => game.Haul is { Works: not null, Loaded: false },
            "quiz-before-golem-trip" => game.Delivery is { Golem: true },
            "quiz-before-fruit-trip" => game.Delivery is { Collection: true, Golem: false },
            _ => null,
        };
        if (started != null)
            Check(started.Value, $"{name}: solving the quiz starts the trip");

        // A panel is closed again.
        if (game.State != GameState.Exploring)
            game.FinishInteraction();
        Check(game.State == GameState.Exploring && car.Active, $"{name}: the player is driving again");

        // A trailer trip is driven to its end.
        if (game.Haul != null)
        {
            TripDriver.Finish(game, world, car);
            Check(
                game.Haul == null && TripDriver.Progress(game.CaptureProgress()) != before,
                $"{name}: the trailer trip finishes and the progress moves on"
            );
        }

        // A delivery is finished.
        if (game.Delivery != null)
        {
            int coins = game.Coins;
            var delivery = game.Delivery;
            if (delivery.Collection)
                foreach (var point in delivery.PickupPositions)
                {
                    car.SetPose(point, 0);
                    delivery.Tick(.25);
                    Frames(1);
                }
            car.SetPose(delivery.Recipient.Position + Vector3.Right * 2, .5f);
            Frames(2);
            ChallengeChecks.Solve(game);
            if (game.State != GameState.Exploring)
                game.FinishInteraction();
            Check(game.Delivery == null && game.Coins > coins, $"{name}: the delivery finishes and pays");
        }

        // A course run is finished.
        if (game.Events.Active >= 0)
        {
            int course = game.Events.Active;
            var events = game.Events;
            for (int gate = Math.Max(1, data.Events.Next); gate < DrivingEvents.GateCount; gate++)
            {
                car.SetPose(events.Points[course, gate], 0);
                game._Process(.05);
                game._Process(.05);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(events.Active == -1, $"{name}: the course run finishes");
        }
        Frames(10);
        Accepted($"{name}: after carrying on");
        Check(game.Coins >= data.Economy.Coins, $"{name}: carrying on never loses coins");
    }
}
