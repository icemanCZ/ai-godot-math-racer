using System;
using System.Linq;
using System.Text.Json;
using Godot;

namespace MathRacer;

/// <summary>
/// The raw-material economy: the log yard, the sawmill, the brickworks and the gravel works, their workers,
/// supply trips with the trailer, the visible stock and its saves.
/// </summary>
public partial class WorksChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public WorksChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    /// <summary>Panels are freed at the end of a frame, so a button that is queued for deletion does not count.</summary>
    private bool HasStartButton() =>
        game.FindChildren("StartSupplyTrip", "", true, false).Any(node => !node.IsQueuedForDeletion());

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("works-checks/progress.json");
            void Frames(int count)
            {
                for (int i = 0; i < count; i++)
                    game._Process(.1);
            }
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save accepted: {what}");
                game.RestoreProgress(loaded!);
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"works progress restores: {what}"
                );
                foreach (var works in world.Industry)
                    Check(works.ShownUnits == works.Stock, $"{works.Spec.Name} shows its stock after restoring");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"works-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- Placement ----
            Check(
                world.Ford != null && world.LogYard != null && world.Industry.Length == WorksCatalog.Count,
                "valley has a log yard and three works"
            );
            var site = world.Ford!.Site;
            var yard = world.LogYard!.Value;
            Check(
                yard.Load == HaulLoad.Logs && world.SourceFor(HaulLoad.Logs).Position == yard.Position,
                "logs come from the log yard"
            );
            Check(
                world.SourceFor(HaulLoad.Stones).Position == world.HaulSources[(int)HaulLoad.Stones].Position,
                "stones still come from the quarry"
            );
            Check(!site.OnFarBank(yard.Position), "the log yard lies on the village bank");
            var tracks = world.Tracks!;
            Check(tracks.Contains(yard.Position) && tracks.Contains(yard.Road), "a field path leads to the log yard");
            for (int i = 0; i < SourceCatalog.Count; i++)
            {
                var load = SourceCatalog.All[i].Load;
                var worker = world.SourceWorker(load);
                var source = world.SourceFor(load);
                Check(
                    worker?.Role is SourceRole { Index: var index }
                        && index == i
                        && world.ActorId(worker) == World.FirstSourceId + i
                        && world.ActorAt(World.FirstSourceId + i) == worker,
                    $"the {load} site has a registered worker"
                );
                Check(
                    Flat(worker!.Position, source.Position) is > 1.5f and < 6,
                    $"the {load} worker stands by the loading spot"
                );
            }
            Check(world.SourceWorker(HaulLoad.Sand) == null, "the sand pit has no worker");
            for (int i = 0; i < world.Industry.Length; i++)
            {
                var works = world.Industry[i];
                var spec = works.Spec;
                Check(works.Site.Index == i && (int)spec.Product == i, $"{spec.Name} is indexed by product");
                Check(!site.OnFarBank(works.Site.Position), $"{spec.Name} lies on the village bank");
                Check(
                    tracks.Contains(works.Site.Position) && tracks.Contains(works.Site.Road),
                    $"a field path leads to {spec.Name}"
                );
                Check(
                    works.Worker.Role is WorksRole { Index: var role }
                        && role == i
                        && world.ActorId(works.Worker) == World.FirstWorksId + i
                        && world.ActorAt(World.FirstWorksId + i) == works.Worker,
                    $"{spec.Name} has a registered worker"
                );
                Check(
                    Flat(works.Worker.Position, works.Site.Position) < 7,
                    $"the worker stands by the ring of {spec.Name}"
                );
                Check(works.Stock == 0 && works.ShownUnits == 0, $"{spec.Name} starts with an empty yard");
                Check(
                    world.HaulSources.All(s => Flat(s.Position, works.Site.Position) > 25)
                        && Flat(yard.Position, works.Site.Position) > 25,
                    $"{spec.Name} keeps clear of the sources"
                );
                for (int j = i + 1; j < world.Industry.Length; j++)
                    Check(Flat(works.Site.Position, world.Industry[j].Site.Position) > 40, "works keep apart");
                for (int step = 0; step <= 20; step++)
                    Check(
                        !world.Plants.NearTrunk(works.Site.Road.Lerp(works.Site.Position, step / 20f), 1.4f),
                        $"the approach to {spec.Name} is clear of trees"
                    );
                for (float offset = 0; offset <= 15; offset += 5)
                    Check(
                        !world.Plants.NearTrunk(works.Site.Position + works.Site.Inward * offset, 1.4f),
                        $"no tree stands in the way at {spec.Name}"
                    );
            }
            for (int step = 0; step <= 20; step++)
                Check(
                    !world.Plants.NearTrunk(yard.Road.Lerp(yard.Position, step / 20f), 1.4f),
                    "the approach to the log yard is clear of trees"
                );

            // ---- The works stand at the edge of the village ----
            foreach (var works in world.Industry)
                Check(
                    world.Village.PlotDistance(works.Site.Position) <= World.WorksBand.Max + 20,
                    $"{works.Spec.Name} stands at the edge of the village"
                );

            // ---- Every source, works and building has an icon of its own ----
            var icons = new System.Collections.Generic.List<string>();
            icons.AddRange(world.Industry.Select(w => w.Worker.Role.Icon));
            icons.AddRange(SourceCatalog.All.Select(s => SourceCatalog.IconOf(s.Load)));
            icons.Add(SourceCatalog.IconOf(HaulLoad.Sand));
            icons.AddRange(world.Projects.Select(p => p.Builder.Role.Icon));
            icons.AddRange(new[] { "gates", "slalom" });
            Check(
                icons.All(icon => icon != "" && icon != "trailer" && icon != "basket")
                    && icons.Distinct().Count() == icons.Count,
                $"every source, works, building and driving course has its own icon ({string.Join(", ", icons)})"
            );

            // ---- Every raw material has a second site, far from the first and out in the countryside ----
            foreach (var load in new[] { HaulLoad.Stones, HaulLoad.Soil, HaulLoad.Sand, HaulLoad.Logs })
            {
                var sites = world.SourceSites(load).ToArray();
                Check(sites.Length == 2, $"{load} has a second site");
                if (sites.Length != 2)
                    continue;
                Check(Flat(sites[0].Position, sites[1].Position) >= 100, $"the two {load} sites lie far apart");
                Check(!site.OnFarBank(sites[1].Position), $"the second {load} site lies on the village bank");
                Check(
                    tracks.Contains(sites[1].Position) && tracks.Contains(sites[1].Road),
                    $"a field path leads to the second {load} site"
                );
                Check(
                    world.NearestSource(load, sites[1].Position).Position == sites[1].Position
                        && world.NearestSource(load, sites[0].Position).Position == sites[0].Position,
                    $"a trip starts at the {load} site nearest to the car"
                );
                for (int step = 0; step <= 20; step++)
                    Check(
                        !world.Plants.NearTrunk(sites[1].Road.Lerp(sites[1].Position, step / 20f), 1.4f),
                        $"the approach to the second {load} site is clear of trees"
                    );
            }

            // ---- The works are open from the start: the trailer is lent for every trip ----
            Check(
                !game.TrailerOwned && world.Industry.All(w => w.Stock == 0),
                "a new valley has no trailer and empty stocks"
            );
            var sawmill = world.Industry[(int)Product.Planks];
            var trailer = game.Trailer;
            var logger = world.SourceWorker(HaulLoad.Logs)!;
            car.SetPose(world.Ground.At(logger.Position.X + 2, logger.Position.Z, .15f), 0);
            game.OpenSource(logger);
            Check(game.State == GameState.SourceStatus, "the log yard worker opens a panel");
            Check(HasStartButton(), "the first load can be fetched without owning the trailer");
            RoundTrip("source panel without trailer");
            game.FinishInteraction();

            // ---- Supply trips ----
            void Supply(Works works, int expected)
            {
                var spec = works.Spec;
                var asker = world.SourceWorker(spec.Input)!;
                car.SetPose(world.Ground.At(asker.Position.X + 2, asker.Position.Z, .15f), 0);
                // The works' worker only shows the stock.
                game.OpenWorks(works.Worker);
                Check(game.State == GameState.WorksStatus && !HasStartButton(), $"{spec.Name} offers no trip itself");
                game.FinishInteraction();
                game.OpenSource(asker);
                Check(game.State == GameState.SourceStatus, $"the {spec.Input} worker opens a panel");
                Check(HasStartButton(), $"the {spec.Input} worker offers the next load");
                game.BeginSupplyTrip();
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false }
                        && trip.Target.Index == works.Site.Index
                        && trailer.Attached
                        && game.State == GameState.Challenge
                        && game.CurrentChallenge != null,
                    $"the {spec.Input} worker sends the empty trailer and asks the loading question"
                );
                Check(trip!.Load == spec.Input && trip.Source.Load == spec.Input, $"{spec.Name} wants {spec.Input}");
                if (game.CurrentChallenge is LoadChallenge load)
                {
                    Check(load.Cargo == spec.Input && load.IsValid(), "the loading question shows the right material");
                    RoundTrip("loading question");
                    Rejected(
                        data => data.Quiz.Challenge = load with { Cargo = (HaulLoad)(((int)spec.Input + 1) % 4) },
                        "a loading question for another material"
                    );
                }
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: true } && trailer.Load == spec.Input && game.State == GameState.Exploring,
                    "the trailer is loaded"
                );
                Check(car.LoadFactor < 1, "a full trailer slows the car");
                RoundTrip($"full trailer for {spec.Name}");
                var ring = works.Site.Position;
                car.SetPose(world.Ground.At(ring.X, ring.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(
                    works.Stock == expected && works.ShownUnits == expected,
                    $"{spec.Name} shows {expected} units on its yard"
                );
                Check(
                    trailer.Load == null && car.LoadFactor == 1 && game.Haul == null && !game.TaskActive,
                    "the supply trip is over"
                );
                Check(works.Working, $"{spec.Name} works after a delivery");
                RoundTrip($"after delivery to {spec.Name}");
            }

            for (int load = 1; load <= Works.Capacity / Works.PerLoad; load++)
                Supply(sawmill, load * Works.PerLoad);
            Check(sawmill.Full && sawmill.Stock == Works.Capacity, "six loads of logs fill the sawmill yard");
            car.SetPose(world.Ground.At(logger.Position.X + 2, logger.Position.Z, .15f), 0);
            game.OpenSource(logger);
            Check(!HasStartButton(), "a full sawmill offers no more work");
            game.BeginSupplyTrip();
            Check(game.Haul == null, "nothing is hauled to a full sawmill");
            game.FinishInteraction();

            Supply(world.Industry[(int)Product.Bricks], Works.PerLoad);
            Supply(world.Industry[(int)Product.Gravel], Works.PerLoad);
            Supply(world.Industry[(int)Product.Bricks], Works.PerLoad * 2);

            // ---- Consuming stock for later buildings ----
            Check(
                sawmill.Take(5) && sawmill.Stock == Works.Capacity - 5 && sawmill.ShownUnits == sawmill.Stock,
                "a building can take planks from the stock"
            );
            Check(
                !sawmill.Take(Works.Capacity) && sawmill.Stock == Works.Capacity - 5,
                "more than the stock cannot be taken"
            );
            RoundTrip("stock partly used");

            // ---- The second sites have workers of their own ----
            foreach (var load in SourceCatalog.ExtraLoads)
            {
                var extraSite = world.SourceSites(load).Last();
                var extraWorker = world.ExtraSourceWorker(load);
                int extraId = World.FirstExtraSourceId + SourceCatalog.ExtraIndexOf(load);
                Check(
                    extraWorker?.Role is SourceRole { Load: var roleLoad }
                        && roleLoad == load
                        && world.ActorId(extraWorker) == extraId
                        && world.ActorAt(extraId) == extraWorker,
                    $"the second {load} site has a registered worker"
                );
                Check(
                    Flat(extraWorker!.Position, extraSite.Position) is > 1.5f and < 6,
                    $"the worker stands by the loading spot of the second {load} site"
                );
                Check(
                    world.PickupActor(load, extraSite) == extraWorker && world.PickupActor(load) != extraWorker,
                    $"the second {load} worker asks the loading question for his own site"
                );
                Check(
                    extraWorker.Role.Icon == SourceCatalog.IconOf(load),
                    $"the second {load} worker shows the site's icon"
                );
            }
            Check(
                world.AllSourceWorkers.Select(w => w.DisplayName).Distinct().Count() == 7,
                "the seven source workers are different characters"
            );
            var secondQuarry = world.SourceSites(HaulLoad.Stones).Last();
            var quarryWorker = world.ExtraSourceWorker(HaulLoad.Stones)!;
            car.SetPose(world.Ground.At(quarryWorker.Position.X + 2, quarryWorker.Position.Z, .15f), 0);
            game.OpenSource(quarryWorker);
            Check(
                game.State == GameState.SourceStatus && HasStartButton(),
                "the second quarry's worker offers the trip"
            );
            game.BeginSupplyTrip();
            Check(
                game.Haul is { Works: not null, Loaded: false } second
                    && second.Source == secondQuarry
                    && second.Asker == quarryWorker
                    && game.State == GameState.Challenge,
                "the second quarry's worker asks the loading question"
            );
            Check(
                SaveStore.Write(game.CaptureProgress(), path) && SaveStore.Read(path) != null,
                "a loading question of the second quarry's worker is saved"
            );
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Loaded: true } && trailer.Load == HaulLoad.Stones,
                "the load of the second quarry is aboard"
            );
            game.FinishInteraction();
            game.OpenChallenge(world.Village.Farmer);
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "another task replaces the trip from the second quarry");

            // ---- One task slot ----
            var bricks = world.Industry[(int)Product.Bricks];
            var clayWorker = world.SourceWorker(HaulLoad.Soil)!;
            car.SetPose(world.Ground.At(clayWorker.Position.X + 2, clayWorker.Position.Z, .15f), 0);
            game.OpenSource(clayWorker);
            game.BeginSupplyTrip();
            Check(game.Haul is { Works: not null }, "the clay pit worker sends the trailer");
            game.FinishInteraction();
            game.OpenChallenge(world.Village.Farmer);
            Check(
                game.State == GameState.Challenge && game.Haul != null,
                "another task's quiz keeps the supply trip until it is accepted"
            );
            ChallengeChecks.Solve(game);
            Check(game.Haul == null && game.Delivery?.Farm == true, "accepting another task cancels the supply trip");
            Frames(2);
            Check(!trailer.Attached && trailer.Load == null, "the trailer is unhitched when no task needs it");
            car.SetPose(world.Ground.At(clayWorker.Position.X + 2, clayWorker.Position.Z, .15f), 0);
            game.OpenSource(clayWorker);
            game.BeginSupplyTrip();
            Check(
                game.Haul is { Works: not null } && game.Delivery == null,
                "accepting a supply trip cancels the expedition"
            );
            game.FinishInteraction();
            RoundTrip("supply trip under way");

            // ---- Without arithmetic a word quiz replaces the loading count ----
            var words = game.CaptureProgress();
            words.Quiz.Types = QuizType.Letters;
            Check(SaveStore.Write(words, path) && SaveStore.Read(path) != null, "word-quiz save accepted");
            game.RestoreProgress(SaveStore.Read(path)!);
            var wanted = world.SourceFor(bricks.Spec.Input);
            car.SetPose(world.Ground.At(wanted.Position.X, wanted.Position.Z, .15f), 0);
            Frames(1);
            Check(game.CurrentChallenge is LetterChallenge, "without arithmetic the loading quiz uses words");

            // ---- Damaged saves ----
            Rejected(data => data.Buildings.WorksStock = new[] { Works.Capacity + 1, 0, 0 }, "stock above the yard");
            Rejected(data => data.Buildings.WorksStock = new[] { -1, 0, 0 }, "negative stock");
            Rejected(data => data.Buildings.WorksStock = new[] { 1, 2 }, "stock of only two works");
            Rejected(data => data.Buildings.WorksStock = null!, "missing stock");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Supply, Index = 3 },
                "a supply trip for a fourth works"
            );
            Rejected(data => data.Nearby = World.FirstWorksId + 1, "a question asked by a works worker");
            Rejected(data => data.Nearby = World.FirstSourceId, "a question asked by the wrong source worker");
            Rejected(
                data =>
                {
                    data.Task.Trip = new SavedTrip { Kind = TripKinds.Supply, Index = 0 };
                    data.Buildings.WorksStock = new[] { Works.Capacity, 0, 0 };
                },
                "a supply trip to a full works"
            );
            GD.Print("WORKS_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }
}
