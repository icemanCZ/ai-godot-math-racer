using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace MathRacer;

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

            car.SetPose(world.Beaver!.Position + Vector3.Right * 2, 0);
            game.OpenBeaver();
            Check(game.State == GameState.FordStatus, "beaver offers the ford");
            RoundTrip("beaver panel");
            game.BeginFordTrip();
            Check(
                game.Haul is { Trip: 0, Loaded: false } && trailer.Attached && game.State == GameState.Exploring,
                "first trip hitches an empty trailer"
            );
            game.OpenChallenge(world.Village.Farmer);
            Check(
                game.State == GameState.FarmStatus && game.CurrentChallenge == null && game.Delivery == null,
                "a ford trip blocks other expeditions"
            );
            game.FinishInteraction();
            car.Reset();
            Check(
                Flat(trailer.Position, car.Position) is > 4.4f and < 5
                    && (trailer.Position - car.Position).Dot(car.Transform.Basis.Z) > 4.4f,
                "reset places the trailer behind the car"
            );
            RoundTrip("empty trailer");

            for (int trip = 0; trip < Ford.Trips; trip++)
            {
                var haul = game.Haul!;
                Check(haul.Trip == trip && !haul.Loaded, $"trip {trip + 1} heads for its source");
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
                        game.State == GameState.Challenge && game.CurrentChallenge == load,
                        "a wrong count keeps the question open"
                    );
                }
                if (trip == 1)
                    RoundTrip("loading question");
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: true } && trailer.Load == haul.Load && game.State == GameState.Exploring,
                    "solving the question fills the trailer"
                );
                Check(car.LoadFactor < 1, "a full trailer slows the car a little");
                RoundTrip("full trailer");
                var dump = game.Haul!.Destination;
                Check(site.OnFarBank(dump) == Ford.DumpsOnFarBank(trip), "load goes to the right bank");
                car.SetPose(world.Ground.At(dump.X, dump.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(
                    ford.Stage == trip + 1 && trailer.Load == null && car.LoadFactor == 1,
                    "dumped load builds the ford"
                );
                if (trip < Ford.Trips - 1)
                    Check(game.Haul is { Loaded: false } next && next.Trip == trip + 1, "the next trip starts");
                RoundTrip($"after load {trip + 1}");
            }
            Check(
                ford.Passable && game.TrailerOwned && game.Haul == null && trailer.Attached,
                "six loads finish the ford and grant the trailer"
            );
            Check(
                world.DriveSurface(site.CenterX, site.Z) > River.Level(site.Z) - .35f,
                "the finished deck lies above the water stop"
            );
            game.FinishInteraction();
            await DriveAcross(site, expectCrossing: true);

            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            Check(
                game.FindChild("GarageTrailer", true, false) is Button { Disabled: false },
                "trailer is available in the garage"
            );
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
            words.FordStage = 2;
            words.FordTrip = 2;
            words.FordTripLoaded = false;
            words.TrailerOwned = false;
            words.TrailerAttached = false;
            words.QuizTypes = QuizType.Letters;
            Check(SaveStore.Write(words, path) && SaveStore.Read(path) != null, "partial ford save accepted");
            game.RestoreProgress(SaveStore.Read(path)!);
            Check(
                ford.Stage == 2 && !ford.Passable && game.Haul is { Trip: 2 } && trailer.Attached,
                "a saved trip restores the ford and the trailer"
            );
            car.SetPose(world.Ground.At(game.Haul!.Source.Position.X, game.Haul.Source.Position.Z, .15f), 0);
            Frames(1);
            Check(game.CurrentChallenge is LetterChallenge, "without arithmetic the loading quiz uses words");

            // A fresh file has no backup to fall back to.
            string invalidPath = DiagnosticArtifacts.PathFor("ford-checks/invalid.json");
            var invalid = game.CaptureProgress();
            invalid.TrailerOwned = true;
            Check(
                SaveStore.Write(invalid, invalidPath) && SaveStore.Read(invalidPath) == null,
                "an unearned trailer is rejected"
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
                && Flat(trailer.Position, car.Position) is > 4 and < 5.2f
                && site.OnFarBank(trailer.Position),
            "the trailer follows across"
        );
    }
}
