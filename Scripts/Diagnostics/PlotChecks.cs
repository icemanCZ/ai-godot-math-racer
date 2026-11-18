using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;

namespace MathRacer;

/// <summary>
/// The six building plots of the village: where they lie, their newcomers, six deliveries of goods each with the
/// loading questions, the house that rises with every delivery, the newcomer moving in, several plots under way at
/// the same time, the way the car drives to a plot, and the saves.
/// </summary>
public partial class PlotChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public PlotChecks(Game game, World world, Vehicle car)
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

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("plot-checks/progress.json");
            var village = world.Village;
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
                    $"plot progress restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"plot-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Near(Animal resident)
            {
                car.SetPose(world.Ground.At(resident.Position.X + 2, resident.Position.Z, .15f), 0);
            }

            // ---- Plots and newcomers ----
            var plots = village.BuildPlots;
            Check(plots.Count == PlotPlans.Count && PlotPlans.Count == 6, "the village has six building plots");
            Check(
                village.Residents.Count == 4 && CharacterCatalog.PlotResidents.Count == PlotPlans.Count,
                "the new residents are separate from the four villagers"
            );
            var names = plots.Select(p => p.Resident.DisplayName).ToHashSet();
            Check(names.Count == PlotPlans.Count, "every plot has a newcomer with a name of their own");
            var middle = village.Center;
            foreach (var plot in plots)
            {
                var resident = plot.Resident;
                Check(
                    resident.Role is PlotResidentRole role
                        && role.Plot == plot
                        && world.ActorId(resident) == World.FirstPlotResidentId + plot.Index
                        && world.ActorAt(World.FirstPlotResidentId + plot.Index) == resident
                        && World.IsPlotResident(World.FirstPlotResidentId + plot.Index),
                    $"the newcomer of plot {plot.Index} is registered"
                );
                Check(plot.Stage == 0 && !plot.Complete, $"plot {plot.Index} starts empty");
                Check(
                    Flat(resident.Position, plot.WaitSpot) < .01f && Flat(resident.Position, plot.Drop) < 8,
                    $"the newcomer of plot {plot.Index} waits beside the lot"
                );
                Check(Flat(plot.Position, middle) < 400, $"plot {plot.Index} lies in the village");
                Check(!world.Routes.Contains(plot.Position), $"plot {plot.Index} is off the road");
                Check(Flat(plot.Road, plot.Position) is > 10 and < 17, $"plot {plot.Index} lies beside its street");
                var ray = PhysicsRayQueryParameters3D.Create(
                    resident.Position + Vector3.Up * .25f,
                    resident.Position + Vector3.Down * .25f
                );
                var hit = world.GetWorld3D().DirectSpaceState.IntersectRay(ray);
                Check(
                    hit.Count > 0 && Mathf.Abs(hit["position"].AsVector3().Y - resident.Position.Y) < .08f,
                    $"the newcomer of plot {plot.Index} stands on the ground"
                );
                for (int stage = 0; stage < HousePlot.Trips; stage++)
                    Check(
                        !plot.GetNode<Node3D>("PlotStage" + stage).Visible,
                        $"plot {plot.Index}: no part of the house stands yet"
                    );
            }
            for (int i = 0; i < plots.Count; i++)
            for (int j = i + 1; j < plots.Count; j++)
                Check(Flat(plots[i].Position, plots[j].Position) > 11, "the plots keep apart");
            foreach (var plot in plots)
            {
                foreach (var (building, _) in village.BuildingFoundations)
                    Check(
                        Flat(plot.Position, building.Position) > 10,
                        $"plot {plot.Index} keeps clear of {building.Name}"
                    );
                Check(village.IsReserved(plot.Position), $"plot {plot.Index} excludes generated vegetation");
            }
            // The way to the ring is clear: the car drives from the road to it with its physics.
            foreach (var plot in plots)
                Check(await DriveWay(plot), $"the car drives from the road to the ring of plot {plot.Index}");

            // ---- Deliveries ----
            Check(PlotPlans.CargoOf(0) == HaulLoad.Gravel, "a house starts with gravel");
            for (int i = 0; i < PlotPlans.Trips; i++)
                Check(HaulLoads.IsGoods(PlotPlans.CargoOf(i)), $"delivery {i + 1} is goods from a works");
            void Deliver(HousePlot plot, bool save = true)
            {
                int step = plot.Stage;
                var cargo = PlotPlans.CargoOf(step);
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                Near(plot.Resident);
                game.OpenPlot(plot.Resident);
                Check(
                    game.State == GameState.PlotStatus && Has("StartPlotTrip"),
                    $"plot {plot.Index}: the newcomer offers delivery {step + 1}"
                );
                game.BeginPlotTrip();
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Plot == plot && trip.Step == step && trailer.Attached,
                    $"plot {plot.Index}: delivery {step + 1} sends the empty trailer"
                );
                Check(
                    trip!.Load == cargo
                        && trip.Contractor == plot.Resident
                        && trip.Asker == world.PickupActor(cargo, trip.Source),
                    "the works' worker loads it for the newcomer"
                );
                RoundTrip("trip to the works");
                var source = world.SourceFor(cargo);
                car.SetPose(world.Ground.At(source.Position.X, source.Position.Z, .15f), 0);
                Frames(1);
                Check(
                    game.State == GameState.Challenge && game.CurrentChallenge != null,
                    "arriving at the works asks a question"
                );
                if (game.CurrentChallenge is LoadChallenge load)
                {
                    Check(load.Cargo == cargo && load.IsValid(), "the loading question matches the load");
                    RoundTrip("loading question");
                }
                int before = world.Industry[(int)HaulLoads.ProductOf(cargo)].Stock;
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: true } && trailer.Load == cargo && game.State == GameState.Exploring,
                    "solving the question fills the trailer"
                );
                Check(
                    world.Industry[(int)HaulLoads.ProductOf(cargo)].Stock == before - Works.PerTrip,
                    "goods leave the works' stock when they are loaded"
                );
                RoundTrip($"full trailer for plot {plot.Index}");
                Check(plot.Stage == step, "loading alone builds nothing");
                car.SetPose(world.Ground.At(plot.Drop.X, plot.Drop.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(plot.Stage == step + 1 && trailer.Load == null, $"delivery {step + 1} raises the house");
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
                if (save)
                    RoundTrip($"after delivery {step + 1} to plot {plot.Index}");
            }

            // Without the goods the delivery cannot start; the newcomer sends the player for the raw material.
            var first = plots[0];
            foreach (var works in world.Industry)
                works.SetStock(0);
            Near(first.Resident);
            game.OpenPlot(first.Resident);
            Check(
                !Has("StartPlotTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                "the newcomer shows what is missing when there is no gravel"
            );
            game.BeginPlotTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts without its goods");
            var needed = world.Industry[(int)HaulLoads.ProductOf(PlotPlans.CargoOf(0))];
            game.BeginMissingSupply();
            ChallengeChecks.Solve(game);
            var fetch = game.Haul;
            Check(
                fetch is { Loaded: false } && fetch.Works == needed && trailer.Attached,
                "the newcomer sends the empty trailer for the raw material"
            );
            var raw = world.SourceFor(needed.Spec.Input);
            car.SetPose(world.Ground.At(raw.Position.X, raw.Position.Z, .15f), 0);
            Frames(1);
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Loaded: true } && trailer.Load == needed.Spec.Input, "the raw material is loaded");
            car.SetPose(world.Ground.At(needed.Site.Position.X, needed.Site.Position.Z, .15f), 0);
            Frames(1);
            Frames(40);
            Check(needed.Stock == Works.PerLoad && game.Haul == null, "the works makes the goods");
            Near(first.Resident);
            game.OpenPlot(first.Resident);
            Check(Has("StartPlotTrip") && !Has("MissingGoods"), "back at the newcomer the delivery can start");
            game.FinishInteraction();

            // Another task replaces a plot's trip, and a trip under way shows on the newcomer's panel.
            Near(first.Resident);
            game.OpenPlot(first.Resident);
            game.BeginPlotTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Plot: not null }, "a trip starts");
            game.OpenPlot(first.Resident);
            Check(!Has("StartPlotTrip"), "talking to the newcomer during the trip starts no second one");
            game.FinishInteraction();
            var withoutTrip = game.CaptureProgress();
            withoutTrip.Task.Trip = null;
            game.RestoreProgress(withoutTrip);
            Check(game.Haul == null && trailer.Load == null, "a save without the trip ends it");

            int coinsBefore = game.Coins;
            Deliver(first);
            Deliver(plots[1]);
            Deliver(first);
            Deliver(plots[2]);
            Deliver(plots[1]);
            Check(
                first.Stage == 2 && plots[1].Stage == 2 && plots[2].Stage == 1,
                "several plots can be under way at once"
            );
            Check(
                first.GetNode<Node3D>("PlotStage0").Visible
                    && first.GetNode<Node3D>("PlotStage1").Visible
                    && !first.GetNode<Node3D>("PlotStage2").Visible,
                "the house shows exactly the stages that were delivered"
            );
            RoundTrip("three plots under way");
            Check(game.Coins == coinsBefore, "no coins before a house is finished");
            Check(
                first.Resident.Position.DistanceTo(first.WaitSpot) < .01f,
                "the newcomer waits until the house is ready"
            );

            while (!first.Complete)
                Deliver(first, save: first.Stage < PlotPlans.Trips - 1);
            Frames(5);
            Check(first.Stage == PlotPlans.Trips && first.Complete, "six deliveries finish the house");
            Check(
                Flat(first.Resident.Position, first.WaitSpot) < .01f && !first.Resident.Running,
                "the newcomer has not moved yet when the last load is tipped out"
            );
            for (int i = 0; i < 4; i++)
                first._Process(.1);
            Check(
                first.Resident.Running && Flat(first.Resident.Position, first.WaitSpot) > .5f,
                "the newcomer walks towards the new house"
            );
            Check(game.Coins == coinsBefore + Game.PlotCoins, "a finished house paid its coins");
            for (int stage = 0; stage < HousePlot.Trips; stage++)
                Check(
                    first.GetNode<Node3D>("PlotStage" + stage).Visible,
                    $"stage {stage + 1} of the finished house stands"
                );
            for (int i = 0; i < 120 && first.Resident.Position.DistanceTo(first.HomeSpot) > .01f; i++)
                first._Process(.1);
            Check(
                Flat(first.Resident.Position, first.HomeSpot) < .01f && !first.Resident.Running,
                "the newcomer has moved in at the doorstep"
            );
            Check(
                world.Ground.Height(first.Position.X, first.Position.Z) < first.Position.Y,
                "the house stands above the ground"
            );
            // Shapes that were switched on take effect with the next physics step.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var wall = world
                .GetWorld3D()
                .DirectSpaceState.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(
                        first.Position + Vector3.Up * 6,
                        first.Position + Vector3.Down * 2
                    )
                );
            Check(
                wall.Count > 0 && wall["position"].AsVector3().Y > first.Position.Y,
                "the finished house blocks the car"
            );
            RoundTrip("a finished house");
            Check(
                first.Resident.Position.DistanceTo(first.HomeSpot) < .01f,
                "a restored game keeps the newcomer in the finished house"
            );
            Near(first.Resident);
            game.OpenPlot(first.Resident);
            Check(
                game.State == GameState.PlotStatus && !Has("StartPlotTrip"),
                "a finished house asks for no more material"
            );
            game.BeginPlotTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts for a finished house");
            game.FinishInteraction();
            Check(Has("PlotStage5"), "the finished house is part of the village");
            foreach (var other in plots.Skip(1))
                Check(
                    !other.Complete && other.Resident.Position.DistanceTo(other.WaitSpot) < .01f,
                    $"the newcomer of plot {other.Index} still waits"
                );

            // ---- Every house gets finished and every newcomer moves in ----
            foreach (var plot in plots)
                while (!plot.Complete)
                    Deliver(plot);
            Frames(5);
            foreach (var plot in plots)
            {
                for (int i = 0; i < 120 && plot.Resident.Position.DistanceTo(plot.HomeSpot) > .01f; i++)
                    plot._Process(.1);
                Check(
                    plot.Complete && Flat(plot.Resident.Position, plot.HomeSpot) < .01f,
                    $"plot {plot.Index}: the house is finished and the newcomer lives in it"
                );
            }
            Check(game.Coins == coinsBefore + PlotPlans.Count * Game.PlotCoins, "each finished house paid 10 coins");
            RoundTrip("all six houses finished");

            // ---- Saves ----
            var oldSave = LegacySaves.FromCurrent(game.CaptureProgress(), 3);
            oldSave.Remove("PlotStages");
            var old = LegacySaves.Read(oldSave);
            Check(
                old != null && old.Buildings.PlotStages.All(stage => stage == 0) && old.Task.Trip == null,
                "a save from before the plots loads with empty plots"
            );
            game.RestoreProgress(old!);
            Check(plots.All(p => p.Stage == 0), "the old save restores empty plots");
            Check(
                plots.All(p => p.Resident.Position.DistanceTo(p.WaitSpot) < .01f),
                "the newcomers wait again on empty plots"
            );
            Rejected(
                data => data.Buildings.PlotStages = new[] { 0, PlotPlans.Trips + 1, 0, 0, 0, 0 },
                "a house with seven deliveries"
            );
            Rejected(data => data.Buildings.PlotStages = new[] { 0, 1 }, "progress for only two plots");
            Rejected(data => data.Buildings.PlotStages = null!, "no plot progress at all");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Plot, Index = PlotPlans.Count },
                "a trip for a seventh plot"
            );
            Rejected(
                data =>
                {
                    data.Buildings.PlotStages = new[] { PlotPlans.Trips, 0, 0, 0, 0, 0 };
                    data.Task.Trip = new SavedTrip { Kind = TripKinds.Plot, Index = 0 };
                },
                "a trip to a plot that has no delivery left"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.PlotStatus;
                    data.Nearby = World.FarmerId;
                },
                "a plot panel at the farmer"
            );
            Rejected(data => data.Nearby = World.LastPlotResidentId + 1, "a character beyond the last newcomer");

            // A panel left open at a newcomer and a trip under way come back as they were.
            game.RestoreProgress(old!);
            foreach (var works in world.Industry)
                works.SetStock(Works.Capacity);
            var second = plots[3];
            Near(second.Resident);
            game.OpenPlot(second.Resident);
            RoundTrip("the panel of a newcomer");
            Check(game.State == GameState.PlotStatus && Has("StartPlotTrip"), "the panel comes back after loading");
            game.BeginPlotTrip();
            ChallengeChecks.Solve(game);
            RoundTrip("an empty trailer on its way");
            Check(game.Haul is { Loaded: false, Plot: { } restored } && restored == second, "the trip comes back");
            var cargoNow = PlotPlans.CargoOf(0);
            var workSource = world.SourceFor(cargoNow);
            car.SetPose(world.Ground.At(workSource.Position.X, workSource.Position.Z, .15f), 0);
            Frames(1);
            RoundTrip("the question at the works");
            ChallengeChecks.Solve(game);
            RoundTrip("a full trailer on its way to the plot");
            Check(game.Haul is { Plot: not null, Loaded: true }, "the loaded trip comes back");

            GD.Print("PLOT_CHECKS_PASSED");
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

    /// <summary>Drives from the road along the way to a plot's ring with the car's physics, steering towards the way.</summary>
    private async Task<bool> DriveWay(HousePlot plot)
    {
        int count = Mathf.CeilToInt(Flat(plot.Road, plot.Drop) / 2) + 1;
        var points = Enumerable
            .Range(0, count)
            .Select(i => plot.Road.Lerp(plot.Drop, i / (float)(count - 1)))
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
            best = Mathf.Min(best, Flat(car.Position, plot.Drop));
        }
        Input.ActionRelease("accelerate");
        Input.ActionRelease("left");
        Input.ActionRelease("right");
        return best <= 4.5f;
    }
}
