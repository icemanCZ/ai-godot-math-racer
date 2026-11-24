using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>The trailer and the beaver's ford: placement, six trips, saves, the crossing and the garage.</summary>
public partial class FordChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;

    public FordChecks(Game game, World world, Vehicle car)
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

    /// <summary>Panels are freed at the end of a frame, so a node that is queued for deletion does not count.</summary>
    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private int StockOf(HaulLoad load) =>
        HaulLoads.IsGoods(load) ? world.Industry[(int)HaulLoads.ProductOf(load)].Stock : 0;

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("ford-checks/progress.json");
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save accepted: {what}");
                game.RestoreProgress(loaded!);
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"ford progress restores: {what}"
                );
            }
            void Frames(int count)
            {
                for (int i = 0; i < count; i++)
                    game._Process(.1);
            }

            Check(world.Ford != null && world.Beaver != null && world.HaulSources.Length == 3, "valley has a ford");
            var ford = world.Ford!;
            var site = ford.Site;
            var trailer = game.Trailer;
            Check(!site.OnFarBank(site.NearDump) && site.OnFarBank(site.FarDump), "dumps lie on both banks");
            Check(
                Mathf.Abs(site.Z - River.BridgeZ) >= World.MinBridgeGap,
                "the beaver's bridge is far from the main one"
            );
            var tracks = world.Tracks!;
            Check(
                new[] { site.NearRoad, site.NearDump, site.FarRoad, site.FarDump }.All(tracks.Contains)
                    && world.HaulSources.All(s => tracks.Contains(s.Position) && tracks.Contains(s.Road)),
                "roads lead to both bridge ends and field paths to every source"
            );
            foreach (var end in new[] { site.NearEnd, site.FarEnd })
            {
                var inward = new Vector3(site.CenterX - end.X, 0, 0).Normalized();
                Check(
                    tracks.Contains(end - inward * .5f) && tracks.Contains(end + inward * 2),
                    "each road runs under the end of the deck without a meadow gap"
                );
            }
            Check(world.HaulSources.All(s => !site.OnFarBank(s.Position)), "sources lie on the village bank");
            Check(
                world.HaulSources.Select((s, i) => s.Load == (HaulLoad)i).All(same => same),
                "sources are indexed by load"
            );
            foreach (
                var (from, to) in new[]
                {
                    (site.NearRoad, site.NearDump),
                    (site.FarRoad, site.FarDump),
                    (site.NearDump, site.NearEnd),
                    (site.FarDump, site.FarEnd),
                }.Concat(world.HaulSources.Select(s => (s.Road, s.Position)))
            )
                for (int step = 0; step <= 20; step++)
                    Check(
                        !world.Plants.NearTrunk(from.Lerp(to, step / 20f), 1.4f),
                        "haul approaches are clear of trees"
                    );
            Check(ford.Stage == 0 && !ford.Passable, "new valley starts with an unbuilt ford");
            Check(
                world.DriveSurface(site.CenterX, site.Z) < River.Level(site.Z),
                "the riverbed is below water before the ford is built"
            );
            await DriveAcross(site, expectCrossing: false);

            // The first delivery is gravel, which has to be made first: without it the beaver shows what is missing.
            car.SetPose(world.Beaver!.Position + Vector3.Right * 2, 0);
            game.OpenBuilder(world.Beaver!);
            Check(
                game.State == GameState.BuildStatus && !Has("StartBuildTrip") && Has("MissingGoods"),
                "the beaver cannot start a delivery of goods that are not made"
            );
            game.BeginBuildTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts without its goods");
            game.FinishInteraction();
            foreach (var works in world.Industry)
                works.SetStock(Works.Capacity);

            car.SetPose(world.Beaver!.Position + Vector3.Right * 2, 0);
            game.OpenBuilder(world.Beaver!);
            Check(game.State == GameState.BuildStatus && Has("StartBuildTrip"), "beaver offers the ford");
            RoundTrip("beaver panel");
            game.BeginBuildTrip();
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Project: not null, Step: 0, Loaded: false }
                    && trailer.Attached
                    && game.State == GameState.Exploring,
                "first trip hitches an empty trailer"
            );
            game.OpenChallenge(world.Village.Farmer);
            Check(
                game.State == GameState.Challenge && game.Haul != null,
                "another task's quiz keeps the ford trip until it is accepted"
            );
            ChallengeChecks.Solve(game);
            Check(game.Haul == null && game.Delivery?.Farm == true, "accepting another task cancels the ford trip");
            Frames(2);
            Check(!trailer.Attached && trailer.Load == null, "the trailer is unhitched when no task needs it");
            car.SetPose(world.Beaver!.Position + Vector3.Right * 2, 0);
            game.OpenBuilder(world.Beaver!);
            game.BeginBuildTrip();
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Project: not null, Step: 0, Loaded: false } && game.Delivery == null && trailer.Attached,
                "accepting the ford trip cancels the expedition and hitches the trailer again"
            );
            game.FinishInteraction();
            car.Reset();
            Check(
                Flat(trailer.Position, car.Position) is > 3.9f and < 4.7f
                    && (trailer.Position - car.Position).Dot(car.Transform.Basis.Z) > 3.9f,
                "reset places the trailer behind the car"
            );
            RoundTrip("empty trailer");

            for (int trip = 0; trip < Ford.Trips; trip++)
            {
                var haul = game.Haul!;
                Check(haul.Step == trip && !haul.Loaded, $"trip {trip + 1} heads for its source");
                car.SetPose(world.Ground.At(haul.Source.Position.X, haul.Source.Position.Z, .15f), 0);
                Frames(1);
                Check(
                    game.State == GameState.Challenge && game.CurrentChallenge != null,
                    "arriving at the source asks the loading question"
                );
                if (game.CurrentChallenge is LoadChallenge load)
                {
                    Check(load.Cargo == haul.Load && load.IsValid(), "loading question matches the load");
                    game.SubmitAnswer(load.Choices.First(choice => !load.Accepts(choice)));
                    Check(
                        game.State == GameState.Challenge
                            && game.CurrentChallenge is LoadChallenge next
                            && next.Cargo == load.Cargo
                            && next.Present != load.Present
                            && next.IsValid(),
                        "a wrong count asks a new loading question of the same load"
                    );
                }
                int stockBefore = StockOf(haul.Load);
                if (trip == 1)
                    RoundTrip("loading question");
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: true } && trailer.Load == haul.Load && game.State == GameState.Exploring,
                    "solving the question fills the trailer"
                );
                Check(
                    StockOf(haul.Load) == stockBefore - (HaulLoads.IsGoods(haul.Load) ? Works.PerTrip : 0),
                    "loading goods takes them from the works stock"
                );
                Check(car.LoadFactor < 1, "a full trailer slows the car a little");
                RoundTrip("full trailer");
                var dump = game.Haul!.Destination;
                Check(site.OnFarBank(dump) == ford.Steps[trip].FarBank, "load goes to the right bank");
                car.SetPose(world.Ground.At(dump.X, dump.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(
                    ford.Stage == trip + 1 && trailer.Load == null && car.LoadFactor == 1,
                    "dumped load builds the ford"
                );
                if (trip < Ford.Trips - 1)
                {
                    Check(game.Haul == null && !game.TaskActive, "the next trip waits for the player");
                    RoundTrip($"between loads {trip + 1}");
                    car.SetPose(world.Beaver!.Position + Vector3.Right * 2, 0);
                    game.OpenBuilder(world.Beaver!);
                    Check(game.State == GameState.BuildStatus, "the beaver offers the next load");
                    game.BeginBuildTrip();
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Project: not null, Loaded: false } next
                            && next.Step == trip + 1
                            && trailer.Attached,
                        "confirming starts the next trip"
                    );
                }
                RoundTrip($"after load {trip + 1}");
            }
            Frames(200);
            Check(
                ford.Passable && game.TrailerOwned && game.Haul == null && !trailer.Attached,
                "ten loads finish the ford, grant the trailer and unhitch it"
            );
            Check(
                world.DriveSurface(site.CenterX, site.Z) > River.Level(site.Z) - .35f,
                "the finished deck lies above the water stop"
            );
            game.FinishInteraction();
            // The trailer is only hitched for a parade now, so hitch it in the garage to tow it across.
            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            game.ToggleTrailer();
            game.FinishInteraction();
            await DriveAcross(site, expectCrossing: true);

            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            game.ToggleTrailer();
            Check(
                game.FindChild("GarageTrailer", true, false) is Button { Disabled: false },
                "trailer is available in the garage"
            );
            Check(!trailer.Attached, "the trailer stays unhitched between tasks");
            game.ToggleTrailer();
            Check(trailer.Attached, "the trailer can be hitched for a parade");
            game.ToggleTrailer();
            Check(!trailer.Attached, "the trailer can be unhitched");
            RoundTrip("unhitched");
            game.SelectTrailerPaint("blue");
            game.ToggleTrailer();
            Check(trailer.Attached && trailer.PaintId == "blue", "the trailer can be hitched in a new colour");
            RoundTrip("garage");
            game.FinishInteraction();

            // A word quiz replaces the loading count when arithmetic is off.
            var words = game.CaptureProgress();
            words.Buildings.Stages = new[] { 2, 0, 0, 0 };
            words.Task.Trip = new SavedTrip { Kind = TripKinds.Build, Index = BuildPlans.Bridge };
            words.Buildings.TrailerOwned = false;
            words.Buildings.TrailerAttached = false;
            words.Quiz.Types = QuizType.Letters;
            Check(SaveStore.Write(words, path) && SaveStore.Read(path) != null, "partial ford save accepted");
            game.RestoreProgress(SaveStore.Read(path)!);
            Check(
                ford.Stage == 2 && !ford.Passable && game.Haul is { Project: not null, Step: 2 } && trailer.Attached,
                "a saved trip restores the ford and the trailer"
            );
            car.SetPose(world.Ground.At(game.Haul!.Source.Position.X, game.Haul.Source.Position.Z, .15f), 0);
            Frames(1);
            Check(game.CurrentChallenge is LetterChallenge, "without arithmetic the loading quiz uses words");

            // A fresh file has no backup to fall back to.
            string invalidPath = DiagnosticArtifacts.PathFor("ford-checks/invalid.json");
            var invalid = game.CaptureProgress();
            invalid.Buildings.TrailerOwned = true;
            Check(
                SaveStore.Write(invalid, invalidPath) && SaveStore.Read(invalidPath) == null,
                "an unearned trailer is rejected"
            );
            // Saves from before the buildings had ten deliveries have only FordStage (0-6) and no BuildStages.
            System.Text.Json.Nodes.JsonObject Legacy(int stage, int trip = -1)
            {
                var capture = game.CaptureProgress();
                capture.Task.Trip = null;
                capture.Buildings.TrailerOwned = stage == 6;
                capture.Buildings.TrailerAttached = false;
                capture.State = GameState.Exploring;
                capture.Nearby = -1;
                capture.Quiz.Challenge = null;
                capture.Task.Sender = -1;
                capture.Task.Collection = false;
                capture.Task.Picked = Array.Empty<bool>();
                capture.Task.Arrived = false;
                var legacy = LegacySaves.FromCurrent(capture, 3);
                legacy.Remove("BuildStages");
                legacy["FordStage"] = stage;
                legacy["FordTrip"] = trip;
                return legacy;
            }
            var finished = LegacySaves.Read(Legacy(6));
            Check(
                finished is { Buildings: { Stages: [BuildPlans.Trips, 0, 0, 0], TrailerOwned: true } },
                "a finished six-load bridge becomes a finished ten-load bridge with its trailer"
            );
            var halfway = LegacySaves.Read(Legacy(3));
            Check(
                halfway is { Buildings: { Stages: [5, 0, 0, 0] } },
                "a half-built bridge keeps its share of the work"
            );
            var tripUnderWay = LegacySaves.Read(Legacy(2, trip: 2));
            Check(
                tripUnderWay is { Buildings: { Stages: [3, 0, 0, 0] }, Task: { Trip: null } },
                "a trip under way in an old save is dropped"
            );
            game.RestoreProgress(finished!);
            Check(
                ford.Passable && game.TrailerOwned && game.Haul == null,
                "a migrated finished bridge restores as built"
            );

            GD.Print("FORD_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    /// <summary>Drives from the village bank straight along the ford for six seconds.</summary>
    private async Task DriveAcross(FordSite site, bool expectCrossing)
    {
        var start = site.NearDump;
        car.SetPose(world.Ground.At(start.X, site.Z, .15f), site.Side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2);
        Check(car.Active, "car can drive toward the ford");
        float lowest = float.PositiveInfinity;
        Input.ActionPress("accelerate");
        for (int frame = 0; frame < 360; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            if (Mathf.Abs(car.Position.X - site.CenterX) < 3)
                lowest = Mathf.Min(lowest, car.Position.Y);
        }
        Input.ActionRelease("accelerate");
        bool crossed = site.OnFarBank(car.Position) && River.Distance(car.Position.X, car.Position.Z) > 8;
        if (!expectCrossing)
        {
            Check(!crossed && !site.OnFarBank(car.Position), "the river stops the car before the ford is built");
            return;
        }
        Check(crossed && Mathf.Abs(car.Position.Z - site.Z) < Ford.HalfWidth, "the car crosses on the ford");
        Check(lowest > River.Level(site.Z) - .35f, "the ford holds the car above the riverbed");
        var trailer = game.Trailer;
        Check(
            trailer.Attached
                && Flat(trailer.Position, car.Position) is > 3.5f and < 4.8f
                && site.OnFarBank(trailer.Position),
            "the trailer follows across"
        );
    }
}
