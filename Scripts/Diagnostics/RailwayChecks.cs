using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The little railway round the playground: that every valley with a playground has it, that its loop and station lie
/// inside the ground the playground clears, the stationmaster, six deliveries each of which adds to the track or the
/// train, the train that runs round the loop and whistles, the finished railway with its coins and its pet, the way the
/// car still drives to the ring, and the saves.
/// </summary>
public partial class RailwayChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public RailwayChecks(Game game, World world, Vehicle car)
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

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    /// <summary>How many engines and wagons run after this many deliveries.</summary>
    private static readonly int[] CarsAfter = { 0, 0, 0, 1, 2, 2, 3 };

    /// <summary>How many animals ride or wait after this many deliveries: the driver, a traveller, four passengers, the flag keeper.</summary>
    private static readonly int[] PeopleAfter = { 0, 0, 0, 1, 4, 4, 7 };

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("railway-checks/progress.json");
            var trailer = game.Trailer;
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
                    $"railway progress restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"railway-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- The railway and its stationmaster ----
            var yard = world.Playground;
            var railway = world.Railway;
            Check(yard != null && railway != null, "every valley with a playground has a railway");
            var master = railway!.Helper;
            Check(
                master.Role is StationRole role
                    && role.Railway == railway
                    && world.ActorId(master) == World.StationId
                    && world.ActorAt(World.StationId) == master
                    && World.IsStationHelper(World.StationId)
                    && ActorIds.Station == 70,
                "the stationmaster is registered under the next free actor ID"
            );
            Check(master.DisplayName == CharacterCatalog.Stationmaster.Name, "the stationmaster has their name");
            Check(railway.Stage == 0 && !railway.Complete, "the railway starts as pegs and a post");
            Check(!railway.TrainRunning && railway.CarsRunning == 0, "no train runs yet");
            var waiting = world.WorldPets.SingleOrDefault(p => p.Kind == CollectorPetKind.Stationmaster);
            Check(
                waiting != null && !waiting.IsOwned,
                "the little stationmaster is not a pet before the railway is done"
            );
            Check(RailwayPlans.Trips == 6 && Railway.Trips == RailwayPlans.Trips, "six deliveries finish the railway");
            for (int i = 0; i < RailwayPlans.Trips; i++)
            {
                Check(HaulLoads.IsGoods(RailwayPlans.CargoOf(i)), $"delivery {i + 1} is goods from a works");
                Check(!railway.GetNode<Node3D>("RailwayStage" + i).Visible, $"no part of stage {i + 1} stands yet");
            }
            Check(RailwayPlans.Parts.Length == RailwayPlans.Trips, "every delivery has a name");
            Check(
                railway.Drop == yard!.Drop && railway.Road == yard.Road,
                "the railway is served at the playground's ring"
            );
            var ground = world.GetWorld3D().DirectSpaceState;
            Check(
                master.Position.Y > yard.Position.Y - 6 && master.Position.Y < yard.Position.Y + 6,
                "the stationmaster stands on the platform"
            );

            // ---- The loop lies inside the ground the playground clears, free of trees ----
            var track = railway.Track;
            Check(track.Count > 120, $"the track is a loop of {track.Count / 2} metres");
            Check(railway.TrackLength is > 70 and < 110, "the loop is neither tiny nor huge");
            float closing = new Vector2(track[0].X - track[^1].X, track[0].Z - track[^1].Z).Length();
            Check(closing < 1f, "the track closes on itself");
            int outside = 0,
                trees = 0;
            foreach (var point in track)
            {
                var global = railway.ToGlobal(point);
                if (!world.InPlaygroundZone(global))
                    outside++;
                if (world.Plants.NearTrunk(global, 1.2f))
                    trees++;
            }
            Check(outside == 0, "every metre of the track lies inside the cleared ground");
            Check(trees == 0, "no tree stands on the track");
            foreach (var corner in new[] { (3.6f, -2.4f), (8.8f, 2.6f), (-7.9f, -5.7f), (-3.5f, -5.7f), (12.9f, 19f) })
                Check(
                    world.InPlaygroundZone(railway.ToGlobal(new Vector3(corner.Item1, 0, corner.Item2))),
                    $"the station and the tunnel lie in the cleared ground at {corner}"
                );
            Check(
                world.Hidden.Friends.All(friend => !world.InPlaygroundZone(friend.GlobalPosition)),
                "no small friend hides in the railway's ground"
            );
            // The keeper of the playground is still the one nearest to the ring.
            Check(
                Flat(yard.Helper.Position, railway.Drop) < Flat(master.Position, railway.Drop),
                "at the ring the playground's keeper is nearer than the stationmaster"
            );

            // ---- Deliveries ----
            void Near(Character who) => car.SetPose(world.Ground.At(who.Position.X + 2, who.Position.Z, .15f), 0);
            void Deliver(bool save = true)
            {
                int step = railway.Stage;
                var cargo = RailwayPlans.CargoOf(step);
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                Near(master);
                game.OpenRailway(master);
                Check(
                    game.State == GameState.RailwayStatus && Has("StartRailwayTrip"),
                    $"the stationmaster offers delivery {step + 1}"
                );
                game.BeginRailwayTrip();
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Railway == railway && trip.Step == step && trailer.Attached,
                    $"delivery {step + 1} sends the empty trailer"
                );
                Check(
                    trip!.Load == cargo
                        && trip.Contractor == master
                        && trip.Asker == world.PickupActor(cargo, trip.Source),
                    "the works' worker loads it for the stationmaster"
                );
                if (save)
                    RoundTrip("trip to the works");
                var source = world.SourceFor(cargo);
                car.SetPose(world.Ground.At(source.Position.X, source.Position.Z, .15f), 0);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the works asks a question");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (save)
                    RoundTrip($"full trailer for delivery {step + 1}");
                Check(railway.Stage == step, "loading alone builds nothing");
                car.SetPose(world.Ground.At(railway.Drop.X, railway.Drop.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(railway.Stage == step + 1 && trailer.Load == null, $"delivery {step + 1} builds the next part");
                Check(
                    railway.CarsRunning == CarsAfter[step + 1] && railway.TrainRunning == (step + 1 >= 3),
                    $"{CarsAfter[step + 1]} cars run after delivery {step + 1}"
                );
                Check(
                    railway.PeopleAboard == PeopleAfter[step + 1],
                    $"{PeopleAfter[step + 1]} animals ride or wait after delivery {step + 1}"
                );
                for (int stage = 0; stage < RailwayPlans.Trips; stage++)
                    Check(
                        railway.GetNode<Node3D>("RailwayStage" + stage).Visible == (stage <= step),
                        $"exactly the parts of {step + 1} deliveries stand"
                    );
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
                if (save)
                    RoundTrip($"after delivery {step + 1}");
            }

            // Without the goods the delivery cannot start; the stationmaster sends the player for the raw material.
            foreach (var works in world.Industry)
                works.SetStock(0);
            Near(master);
            game.OpenRailway(master);
            Check(
                !Has("StartRailwayTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                "the stationmaster shows what is missing when there is no gravel"
            );
            game.BeginRailwayTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts without its goods");
            var needed = world.Industry[(int)HaulLoads.ProductOf(RailwayPlans.CargoOf(0))];
            game.BeginMissingSupply();
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Loaded: false } fetch && fetch.Works == needed,
                "the stationmaster sends the empty trailer for the raw material"
            );
            var raw = world.SourceFor(needed.Spec.Input);
            car.SetPose(world.Ground.At(raw.Position.X, raw.Position.Z, .15f), 0);
            Frames(1);
            ChallengeChecks.Solve(game);
            car.SetPose(world.Ground.At(needed.Site.Position.X, needed.Site.Position.Z, .15f), 0);
            Frames(1);
            Frames(40);
            Check(needed.Stock == Works.PerLoad && game.Haul == null, "the works makes the goods");
            Near(master);
            game.OpenRailway(master);
            Check(Has("StartRailwayTrip") && !Has("MissingGoods"), "back at the stationmaster the delivery can start");
            game.FinishInteraction();

            // A trip under way is shown, starts no second one and a save without it ends it.
            Near(master);
            game.OpenRailway(master);
            game.BeginRailwayTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Railway: not null }, "a trip starts");
            game.OpenRailway(master);
            Check(!Has("StartRailwayTrip"), "talking to the stationmaster during the trip starts no second one");
            game.FinishInteraction();
            var withoutTrip = game.CaptureProgress();
            withoutTrip.Task.Trip = null;
            game.RestoreProgress(withoutTrip);
            Check(game.Haul == null && trailer.Load == null, "a save without the trip ends it");

            int coinsBefore = game.Coins;
            Deliver();
            Check(game.Coins == coinsBefore, "no coins before the railway is finished");
            while (railway.Stage < 3)
                Deliver();

            // The train runs: it goes round the loop on the rails, one wagon behind another, and whistles.
            Check(railway.TrainRunning && railway.CarsRunning == 1, "after three deliveries the engine runs");
            float start = railway.EngineDistance;
            bool whistled = false,
                steamed = false;
            float worst = 0;
            for (int i = 0; i < 480; i++)
            {
                railway._Process(.1);
                whistled |= railway.WhistleShown;
                steamed |= railway.FindChildren("Steam*", "MeshInstance3D", false, false).Any(n => ((Node3D)n).Visible);
                var engine = railway.EnginePosition;
                float nearest = float.MaxValue;
                foreach (var point in track)
                    nearest = Mathf.Min(nearest, Flat(railway.ToGlobal(point), engine));
                worst = Mathf.Max(worst, nearest);
            }
            Check(!Mathf.IsEqualApprox(start, railway.EngineDistance), "the engine moves");
            Check(worst < .6f, $"the engine keeps to the rails all the way round (worst {worst:0.00} m)");
            Check(whistled, "the train whistles as it goes round");
            Check(steamed, "the engine puffs steam");
            // A lap takes between a quarter and three quarters of a minute.
            float lap = 0;
            float distance = railway.EngineDistance;
            while (railway.EngineDistance >= distance - .001f && lap < 90)
            {
                distance = railway.EngineDistance;
                railway._Process(.1);
                lap += .1f;
            }
            Check(lap is > 12 and < 75, $"a lap takes {lap:0} seconds");
            // Whoever comes near makes the train whistle again.
            car.SetPose(world.Ground.At(railway.Drop.X + 90, railway.Drop.Z + 90, .15f), 0);
            Frames(2);
            for (int i = 0; i < 60 && railway.WhistleShown; i++)
                railway._Process(.1);
            Check(!railway.WhistleShown, "the whistle dies away");
            car.SetPose(world.Ground.At(railway.Drop.X, railway.Drop.Z, .15f), 0);
            Frames(1);
            Check(railway.WhistleShown, "the car's arrival makes the train whistle");

            while (!railway.Complete)
                Deliver(save: railway.Stage < RailwayPlans.Trips - 1);
            Frames(5);
            Check(railway.Stage == RailwayPlans.Trips && railway.Complete, "six deliveries finish the railway");
            Check(railway.CarsRunning == 3, "the finished train has an engine and two wagons");
            Check(game.Coins == coinsBefore + RailwayPlans.Coins, "the finished railway paid its coins");
            var pet = world.WorldPets.Single(p => p.Kind == CollectorPetKind.Stationmaster);
            Check(pet.IsOwned && pet.IsActive, "the finished railway brought the little stationmaster as a pet");
            Check(
                pet.DisplayName == WorldPetCatalog.Stationmaster.Name && pet.Collector == master,
                "the pet belongs to the stationmaster"
            );
            RoundTrip("a finished railway");

            // The wagons follow the engine at a steady distance.
            for (int i = 0; i < 40; i++)
                railway._Process(.1);
            float first = Flat(railway.CarPosition(0), railway.CarPosition(1));
            float second = Flat(railway.CarPosition(1), railway.CarPosition(2));
            Check(
                first is > 1.5f and < 3.2f && second is > 1.5f and < 3.2f,
                $"the wagons follow ({first:0.0}, {second:0.0} m)"
            );

            // The finished station and tunnel block the car; the crossing does not.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            foreach (
                var (name, local) in new[]
                {
                    ("the station", new Vector3(6.5f, 0, 1f)),
                    ("the tunnel hill", new Vector3(-5.7f, 0, -3.5f)),
                }
            )
            {
                var spot = railway.ToGlobal(local);
                float soil = world.Ground.Height(spot.X, spot.Z);
                var wall = ground.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(
                        new Vector3(spot.X, soil + 10, spot.Z),
                        new Vector3(spot.X, soil - 2, spot.Z)
                    )
                );
                Check(
                    wall.Count > 0 && wall["position"].AsVector3().Y > soil + 1.2f,
                    $"{name} blocks the car (soil {soil:0.00})"
                );
            }
            var crossing = railway.ToGlobal(new Vector3(0, 0, -3.5f));
            float crossingSoil = world.Ground.Height(crossing.X, crossing.Z);
            var crossingHit = ground.IntersectRay(
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(crossing.X, crossingSoil + 10, crossing.Z),
                    new Vector3(crossing.X, crossingSoil - 2, crossing.Z)
                )
            );
            Check(
                crossingHit.Count > 0 && crossingHit["position"].AsVector3().Y < crossingSoil + .5f,
                "the car drives over the track at the crossing"
            );
            Check(await DriveWay(yard), "the car still drives from the road to the ring with the railway built");

            Near(master);
            game.OpenRailway(master);
            Check(
                game.State == GameState.RailwayStatus && !Has("StartRailwayTrip"),
                "a finished railway asks for no more material"
            );
            game.BeginRailwayTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts for a finished railway");
            game.FinishInteraction();

            // ---- Saves ----
            var oldSave = LegacySaves.FromCurrent(game.CaptureProgress(), 3);
            var old = LegacySaves.Read(oldSave);
            Check(
                old != null && old.Railway.Stage == 0 && old.Task.Trip == null,
                "a save from before the railway loads with the pegs and the post only"
            );
            game.RestoreProgress(old!);
            Check(railway.Stage == 0 && !railway.TrainRunning, "the old save restores the empty site");
            Rejected(data => data.Railway.Stage = RailwayPlans.Trips + 1, "a railway with seven deliveries");
            Rejected(data => data.Railway.Stage = -1, "a railway with a negative stage");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Railway, Index = 1 },
                "a trip for a second railway"
            );
            Rejected(
                data =>
                {
                    data.Railway.Stage = RailwayPlans.Trips;
                    data.Task.Trip = new SavedTrip { Kind = TripKinds.Railway, Index = 0 };
                },
                "a trip to a railway that has no delivery left"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.RailwayStatus;
                    data.Nearby = World.FarmerId;
                },
                "a railway panel at the farmer"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.PlaygroundStatus;
                    data.Nearby = World.StationId;
                },
                "a playground panel at the stationmaster"
            );
            Rejected(data => data.Nearby = World.TreasurerId + 1, "a character beyond the stationmaster");

            // A panel left open at the stationmaster and a trip under way come back as they were.
            foreach (var works in world.Industry)
                works.SetStock(Works.Capacity);
            Near(master);
            game.OpenRailway(master);
            RoundTrip("the panel of the stationmaster");
            Check(
                game.State == GameState.RailwayStatus && Has("StartRailwayTrip"),
                "the panel comes back after loading"
            );
            game.BeginRailwayTrip();
            ChallengeChecks.Solve(game);
            RoundTrip("an empty trailer on its way");
            Check(game.Haul is { Loaded: false, Railway: { } restored } && restored == railway, "the trip comes back");
            var cargoNow = RailwayPlans.CargoOf(0);
            var workSource = world.SourceFor(cargoNow);
            car.SetPose(world.Ground.At(workSource.Position.X, workSource.Position.Z, .15f), 0);
            Frames(1);
            RoundTrip("the question at the works");
            ChallengeChecks.Solve(game);
            RoundTrip("a full trailer on its way to the station");
            Check(game.Haul is { Railway: not null, Loaded: true }, "the loaded trip comes back");

            GD.Print("RAILWAY_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            Input.ActionRelease("accelerate");
            Input.ActionRelease("left");
            Input.ActionRelease("right");
            GetTree().Quit(1);
        }
    }

    /// <summary>Drives from the road along the way to the ring with the car's physics, steering towards the way.</summary>
    private async Task<bool> DriveWay(Playground yard)
    {
        int count = Mathf.CeilToInt(Flat(yard.Road, yard.Drop) / 2) + 1;
        var points = Enumerable
            .Range(0, count)
            .Select(i => yard.Road.Lerp(yard.Drop, i / (float)(count - 1)))
            .Select(p => world.Ground.At(p.X, p.Z))
            .ToList();
        var start = points[1];
        var first = points[Mathf.Min(3, count - 1)] - start;
        car.SetPose(world.Ground.At(start.X, start.Z, .15f), Mathf.Atan2(-first.X, -first.Z));
        float best = float.PositiveInfinity;
        int progress = 0;
        for (int frame = 0; frame < 6000 && best > 3; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            while (
                progress + 1 < count && Flat(car.Position, points[progress + 1]) < Flat(car.Position, points[progress])
            )
                progress++;
            var target = points[Mathf.Min(count - 1, progress + 4)] - car.Position;
            float error = Mathf.AngleDifference(car.Heading, Mathf.Atan2(-target.X, -target.Z));
            float steer = Mathf.Clamp(-error * 2.5f, -1, 1);
            Input.ActionRelease("left");
            Input.ActionRelease("right");
            Input.ActionPress(steer < 0 ? "left" : "right", Mathf.Abs(steer));
            Input.ActionPress("accelerate", Mathf.Abs(error) > .45f ? .25f : .6f);
            best = Mathf.Min(best, Flat(car.Position, yard.Drop));
        }
        Input.ActionRelease("accelerate");
        Input.ActionRelease("left");
        Input.ActionRelease("right");
        if (best > 4.5f)
            GD.Print($"DRIVE_STALL best={best:0.0} at={car.Position} progress={progress}/{count}");
        return best <= 4.5f;
    }
}
