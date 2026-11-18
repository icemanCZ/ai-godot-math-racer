using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace MathRacer;

/// <summary>
/// The mill, the lookout tower and the jetty: sites, ways to them (cut into the slope where needed, then driven
/// with the car's physics), their builders, ten deliveries each with goods from the works and one special load
/// from a collector, buildings under way at the same time, and their saves.
/// </summary>
public partial class BuildChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public BuildChecks(Game game, World world, Vehicle car)
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

    private int StockOf(HaulLoad load) =>
        HaulLoads.IsGoods(load) ? world.Industry[(int)HaulLoads.ProductOf(load)].Stock : 0;

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("build-checks/progress.json");
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
                    $"build progress restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"build-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- Sites, builders and ways ----
            Check(
                world.Projects.Length == BuildPlans.Count
                    && world.Projects.Select((p, i) => p.Index == i).All(same => same),
                "valley has the bridge, the mill, the tower and the jetty"
            );
            var ford = world.Ford!;
            var tracks = world.Tracks!;
            var sites = new List<BuildSite>();
            foreach (var project in world.Projects.Skip(1))
            {
                var site = ((SitedProject)project).Site;
                sites.Add(site);
                string name = project.GetType().Name;
                Check(
                    project.Builder.Role is BuilderRole { Project: var role }
                        && role == project.Index
                        && world.ActorId(project.Builder) == World.BuilderId(project.Index)
                        && world.ActorAt(World.BuilderId(project.Index)) == project.Builder,
                    $"the {name} has a registered builder"
                );
                Check(
                    Flat(project.Builder.Position, site.Position) < 8,
                    $"the builder of the {name} stands by its ring"
                );
                Check(!ford.Site.OnFarBank(site.Position), $"the {name} lies on the village bank");
                if (project.Index is BuildPlans.Mill or BuildPlans.Tower)
                    Check(
                        world.Village.PlotDistance(site.Position) <= World.LandmarkBand.Max + 40,
                        $"the {name} stands close to the village"
                    );
                Check(tracks.Contains(site.Position) && tracks.Contains(site.Road), $"a way leads to the {name}");
                Check(project.Stage == 0 && !project.Complete, $"the {name} starts unbuilt");
                for (int step = 0; step <= 20; step++)
                    Check(
                        !world.Plants.NearTrunk(site.Road.Lerp(site.Position, step / 20f), 1.4f),
                        $"the way to the {name} is clear of trees"
                    );
                // The centre line keeps the grade limit, cut into the slope where the ground was steeper.
                float worst = 0;
                var previous = world.Ground.Height(site.Road.X, site.Road.Z);
                float length = Flat(site.Road, site.Position);
                int samples = Mathf.CeilToInt(length / 2);
                for (int i = 1; i <= samples; i++)
                {
                    var p = site.Road.Lerp(site.Position, i / (float)samples);
                    float h = world.Ground.Height(p.X, p.Z);
                    float grade = Mathf.Abs(h - previous) / (length / samples);
                    // Beside the river the shaped floodplain decides the ground: it gets a looser limit.
                    if (River.Distance(p.X, p.Z) - River.HalfWidth(p.Z) < 14)
                        grade *= .5f / .6f;
                    worst = Mathf.Max(worst, grade);
                    previous = h;
                }
                Check(worst <= .5f, $"the way to the {name} keeps the grade limit (worst {worst:0.00})");
            }
            for (int i = 0; i < sites.Count; i++)
            for (int j = i + 1; j < sites.Count; j++)
                Check(Flat(sites[i].Position, sites[j].Position) > 25, "the buildings keep apart");
            foreach (var site in sites)
            {
                bool reached = await DriveWay(site);
                Check(reached, $"the car drives the way to project {site.Project} and reaches its ring");
            }

            // ---- Deliveries, several buildings under way at the same time ----
            var trailer = game.Trailer;
            void Deliver(BuildProject project)
            {
                int step = project.Stage;
                var cargo = project.Steps[step].Cargo;
                string name = project.GetType().Name;
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                car.SetPose(world.Ground.At(project.Builder.Position.X + 2, project.Builder.Position.Z, .15f), 0);
                game.OpenBuilder(project.Builder);
                Check(
                    game.State == GameState.BuildStatus && Has("StartBuildTrip"),
                    $"the {name} builder offers delivery {step + 1}"
                );
                game.BeginBuildTrip();
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Project == project && trip.Step == step && trailer.Attached,
                    $"delivery {step + 1} of the {name} sends the empty trailer"
                );
                Check(
                    trip!.Load == cargo && trip.Asker == (world.PickupActor(cargo, trip.Source) ?? project.Builder),
                    "the right character loads it"
                );
                var source = world.SourceFor(cargo);
                car.SetPose(world.Ground.At(source.Position.X, source.Position.Z, .15f), 0);
                Frames(1);
                Check(
                    game.State == GameState.Challenge && game.CurrentChallenge != null,
                    "arriving at the source asks a question"
                );
                if (HaulLoads.IsSpecial(cargo))
                {
                    Check(
                        game.CurrentChallenge is not LoadChallenge,
                        "a collector asks a quiz, not a counting question"
                    );
                    RoundTrip("quiz with a collector");
                    Check(trip.Asker == world.SpecialGiver(cargo), "the collector who holds the special load asks");
                }
                else if (game.CurrentChallenge is LoadChallenge load)
                {
                    Check(load.Cargo == cargo && load.IsValid(), "the loading question matches the load");
                    RoundTrip("loading question");
                }
                int before = StockOf(cargo);
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: true } && trailer.Load == cargo && game.State == GameState.Exploring,
                    "solving the question fills the trailer"
                );
                Check(
                    StockOf(cargo) == before - (HaulLoads.IsGoods(cargo) ? Works.PerTrip : 0),
                    "goods leave the works' stock when they are loaded"
                );
                RoundTrip($"full trailer for the {name}");
                var drop = project.DropFor(step);
                car.SetPose(world.Ground.At(drop.X, drop.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(project.Stage == step + 1 && trailer.Load == null, $"delivery {step + 1} builds the {name}");
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
                RoundTrip($"after delivery {step + 1} of the {name}");
            }

            var mill = world.Projects[BuildPlans.Mill];
            var tower = world.Projects[BuildPlans.Tower];
            var jetty = world.Projects[BuildPlans.Jetty];
            var baker = world.Village.Traders[0];
            Check(
                game.TradeReward(baker) == Game.TradeCoins,
                "before the mill is built the baker pays the usual price"
            );
            var jettyDuck = world.Duck!;
            Check(!jettyDuck.IsOwned && !jettyDuck.IsActive, "the duck waits until the jetty is finished");
            int coinsBefore = game.Coins;
            var ownedBefore = game.CaptureProgress().Owned;
            Check(
                ownedBefore.All(item =>
                    item != Game.BuildItem(BuildPlans.Mill) && item != Game.BuildItem(BuildPlans.Jetty)
                ),
                "none of the building rewards is owned yet"
            );
            // Without the goods the next delivery cannot start.
            foreach (var works in world.Industry)
                works.SetStock(0);
            car.SetPose(world.Ground.At(mill.Builder.Position.X + 2, mill.Builder.Position.Z, .15f), 0);
            game.OpenBuilder(mill.Builder);
            Check(
                !Has("StartBuildTrip") && Has("MissingGoods"),
                "the miller shows what is missing when there are no goods"
            );
            game.BeginBuildTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts without its goods");

            // Continuity: the builder sends the player for the raw material, the works makes the goods and the
            // same builder then offers the delivery.
            var needed = world.Industry[(int)HaulLoads.ProductOf(mill.Steps[mill.Stage].Cargo)];
            Check(Has("StartMissingSupply"), "the miller offers to send the player for the raw material");
            game.BeginMissingSupply();
            ChallengeChecks.Solve(game);
            var fetch = game.Haul;
            Check(
                fetch is { Loaded: false } && fetch.Works == needed && trailer.Attached,
                "the miller sends the empty trailer for the material of the missing goods"
            );
            var rawSource = world.SourceFor(needed.Spec.Input);
            car.SetPose(world.Ground.At(rawSource.Position.X, rawSource.Position.Z, .15f), 0);
            Frames(1);
            Check(
                game.State == GameState.Challenge && fetch!.Asker == world.PickupActor(needed.Spec.Input, fetch.Source),
                "the worker at the source asks the loading question"
            );
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Loaded: true } && trailer.Load == needed.Spec.Input, "the raw material is loaded");
            RoundTrip("raw material for missing goods");
            car.SetPose(world.Ground.At(needed.Site.Position.X, needed.Site.Position.Z, .15f), 0);
            Frames(1);
            Frames(40);
            Check(
                needed.Stock == Works.PerLoad && needed.ShownUnits == Works.PerLoad && game.Haul == null,
                "the works makes the goods from the delivered raw material"
            );
            car.SetPose(world.Ground.At(mill.Builder.Position.X + 2, mill.Builder.Position.Z, .15f), 0);
            game.OpenBuilder(mill.Builder);
            Check(Has("StartBuildTrip") && !Has("MissingGoods"), "back at the miller the delivery can start");
            game.FinishInteraction();

            Deliver(mill);
            Deliver(tower);
            Deliver(mill);
            Deliver(jetty);
            Deliver(tower);
            Check(mill.Stage == 2 && tower.Stage == 2 && jetty.Stage == 1, "three buildings can be under way at once");
            RoundTrip("three buildings under way");
            foreach (var project in new[] { mill, tower, jetty })
                while (!project.Complete)
                    Deliver(project);
            Check(world.Projects.Skip(1).All(p => p.Complete), "all three buildings are finished");
            Frames(5);
            Check(!game.TrailerOwned, "only the bridge grants the trailer");
            Check(game.Coins == coinsBefore + 3 * Game.BuildCoins, "each finished building paid 30 coins");
            var ownedAfter = game.CaptureProgress().Owned;
            foreach (int project in new[] { BuildPlans.Mill, BuildPlans.Tower, BuildPlans.Jetty })
                Check(ownedAfter.Contains(Game.BuildItem(project)!), $"the {project} building brought its garage item");
            Check(
                new CarReward(CarAccessory.FlourSack.Id, true).Price == null
                    && new CarReward(CarAccessory.Binoculars.Id, true).Price == null
                    && new CarReward(CarLook.Water.Id, false).Price == null,
                "the building rewards cannot be bought"
            );
            Check(
                game.TradeReward(baker) == Game.TradeCoins + Game.BakerBonus,
                "a finished mill makes the baker pay more"
            );
            Frames(3);
            Check(((Mill)mill).SailSpeed > 0, "the finished mill turns its sails");
            Check(
                jettyDuck.IsOwned && jettyDuck.IsActive && jettyDuck.Visible,
                "the finished jetty brings the duck along"
            );
            RoundTrip("rewards of the finished buildings");
            Check(jettyDuck.IsActive, "the duck is still along after the progress is restored");
            var ownedSave = game.CaptureProgress();
            var duckOff = game.CaptureProgress();
            duckOff.DisabledCollectorPets = new[] { nameof(CollectorPetKind.Duck) };
            Check(
                SaveStore.Write(duckOff, path) && SaveStore.Read(path) != null,
                "a save with the duck sent home is accepted"
            );
            game.RestoreProgress(SaveStore.Read(path)!);
            Check(jettyDuck.IsOwned && !jettyDuck.IsActive, "the duck stays at home when the player sent it there");
            game.RestoreProgress(ownedSave!);

            // The boat sails the river past the finished jetty.
            var boat = ((Jetty)jetty).Boat;
            Check(boat is { Visible: true }, "the finished jetty has a boat");
            var boatStart = boat!.Position;
            boat._Process(6);
            Check(
                boat.Position.DistanceTo(boatStart) > 3
                    && boat.Position.Y > River.Level(boat.Position.Z) - .1f
                    && River.Distance(boat.Position.X, boat.Position.Z) < River.HalfWidth(boat.Position.Z),
                "the boat sails on the water"
            );

            // The finished tower lets the player look round the valley, then the stargazer rests.
            car.SetPose(world.Ground.At(tower.Builder.Position.X + 2, tower.Builder.Position.Z, .15f), 0);
            game.OpenBuilder(tower.Builder);
            Check(
                Has("StartLookout") && !((Button)game.FindChild("StartLookout", true, false)).Disabled,
                "the stargazer offers a look"
            );
            game.BeginLookout();
            Check(
                game.State == GameState.Exploring && game.LookingAround && !car.Active && game.WorldCoins.Flashing,
                "the look begins: the car waits and the coins are lit"
            );
            Check(
                world.Animals.Where(a => a.RewardAvailable).All(a => a.Pinged),
                "friends who wait for a quiz are lit too"
            );
            game.OpenBuilder(tower.Builder);
            Check(((Button)game.FindChild("StartLookout", true, false)).Disabled, "the stargazer rests after a look");
            game.FinishInteraction();
            Frames(160);
            Check(
                !game.LookingAround && car.Active && !game.WorldCoins.Flashing,
                "the look ends and the car can drive again"
            );

            // ---- Damaged saves ----
            Rejected(
                data => data.BuildStages = new[] { 0, BuildPlans.Trips + 1, 0, 0 },
                "a building with eleven deliveries"
            );
            Rejected(data => data.BuildStages = new[] { 0, 1 }, "progress for only two buildings");
            Rejected(
                data => data.Trip = new SavedTrip { Kind = TripKinds.Build, Index = BuildPlans.Count },
                "a trip for a fifth building"
            );
            Rejected(
                data => data.Trip = new SavedTrip { Kind = TripKinds.Build, Index = BuildPlans.Mill },
                "a trip to a finished building"
            );
            Rejected(
                data =>
                {
                    data.BuildStages = new[] { 0, 3, 0, 0 };
                    data.BuildTrip = BuildPlans.Mill;
                    data.SupplyTrip = 0;
                },
                "a building trip together with a supply trip"
            );
            Rejected(data => data.TrailerOwned = true, "a trailer without a finished bridge");

            GD.Print("BUILD_CHECKS_PASSED");
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

    /// <summary>Drives from the road along a way to its ring with the car's physics, steering towards the way.</summary>
    private async Task<bool> DriveWay(BuildSite site)
    {
        int count = Mathf.CeilToInt(Flat(site.Road, site.Position) / 2) + 1;
        var points = Enumerable
            .Range(0, count)
            .Select(i => site.Road.Lerp(site.Position, i / (float)(count - 1)))
            .Select(p => world.Ground.At(p.X, p.Z))
            .ToList();
        var start = points[1];
        var first = points[Mathf.Min(3, count - 1)] - start;
        car.SetPose(world.Ground.At(start.X, start.Z, .15f), Mathf.Atan2(-first.X, -first.Z));
        float best = float.PositiveInfinity;
        int progress = 0;
        for (int frame = 0; frame < 9000 && best > 4; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            while (
                progress + 1 < count && Flat(car.Position, points[progress + 1]) < Flat(car.Position, points[progress])
            )
                progress++;
            var target = points[Mathf.Min(count - 1, progress + 5)] - car.Position;
            float error = Mathf.AngleDifference(car.Heading, Mathf.Atan2(-target.X, -target.Z));
            float steer = Mathf.Clamp(-error * 2.5f, -1, 1);
            Input.ActionRelease("left");
            Input.ActionRelease("right");
            Input.ActionPress(steer < 0 ? "left" : "right", Mathf.Abs(steer));
            Input.ActionPress("accelerate", Mathf.Abs(error) > .45f ? .25f : .6f);
            best = Mathf.Min(best, Flat(car.Position, site.Position));
        }
        Input.ActionRelease("accelerate");
        Input.ActionRelease("left");
        Input.ActionRelease("right");
        return best <= 6;
    }
}
