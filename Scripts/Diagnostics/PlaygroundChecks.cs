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
/// The playground at the edge of the village: that every valley has it, that it stands clear of everything else (the
/// plants on its yard are gone and no small friend hides there), the keeper, six deliveries each of which brings a toy
/// and a child who plays, the finished playground with its coins and balloons, the way the car drives to it and the saves.
/// </summary>
public partial class PlaygroundChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public PlaygroundChecks(Game game, World world, Vehicle car)
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

    /// <summary>How many children play after this many deliveries: one on each of the first two toys, two on the seesaw...</summary>
    private static readonly int[] ChildrenAfter = { 0, 1, 2, 4, 5, 6, 8 };

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("playground-checks/progress.json");
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
                    $"playground progress restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"playground-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- The playground and its keeper ----
            var yard = world.Playground;
            Check(yard != null, "every valley has a playground");
            Check(world.Industry.Length == 3, "the playground belongs to a valley with works");
            var keeper = yard!.Helper;
            Check(
                keeper.Role is PlaygroundRole role
                    && role.Playground == yard
                    && world.ActorId(keeper) == World.PlaygroundId
                    && world.ActorAt(World.PlaygroundId) == keeper
                    && World.IsPlaygroundHelper(World.PlaygroundId)
                    && ActorIds.Playground == 67,
                "the keeper is registered under the next free actor ID"
            );
            Check(keeper.DisplayName == CharacterCatalog.PlaygroundKeeper.Name, "the keeper has her name");
            Check(yard.Stage == 0 && !yard.Complete, "the playground starts as a staked-out yard");
            Check(
                PlaygroundPlans.Trips == 6 && Playground.Trips == PlaygroundPlans.Trips,
                "six deliveries finish the playground"
            );
            for (int i = 0; i < PlaygroundPlans.Trips; i++)
            {
                Check(HaulLoads.IsGoods(PlaygroundPlans.CargoOf(i)), $"delivery {i + 1} is goods from a works");
                Check(!yard.GetNode<Node3D>("PlaygroundStage" + i).Visible, $"no toy of stage {i + 1} stands yet");
            }
            var ground = world.GetWorld3D().DirectSpaceState;
            var hit = ground.IntersectRay(
                PhysicsRayQueryParameters3D.Create(
                    keeper.Position + Vector3.Up * .25f,
                    keeper.Position + Vector3.Down * .25f
                )
            );
            Check(
                hit.Count > 0 && Mathf.Abs(hit["position"].AsVector3().Y - keeper.Position.Y) < .1f,
                "the keeper stands on the ground"
            );
            Check(!world.Routes.Contains(yard.Position), "the playground is off the road");
            Check(Flat(yard.Road, yard.Drop) > 8, "a field path leads from the road to the ring");

            // ---- It stands clear of everything else, and what stood there is gone ----
            var others = world
                .AllWorks.Select(works => (works.Site.Position, "a works"))
                .Concat(world.Projects.Select(project => (project.Focus, "a building")))
                .Concat(
                    world.Farm == null ? Array.Empty<(Vector3, string)>() : new[] { (world.Farm.Position, "the farm") }
                )
                .Concat(world.Village.BuildPlots.Select(plot => (plot.Position, "a building plot")));
            foreach (var (position, what) in others)
                Check(Flat(yard.Focus, position) > 14, $"the playground keeps clear of {what}");
            Check(
                world.InPlaygroundZone(yard.Focus) && !world.InPlaygroundZone(yard.Focus + Vector3.Right * 80),
                "zone"
            );
            int trees = 0,
                samples = 0;
            // Trees just outside the zone may reach into its edge, so the middle of the yard is looked at.
            for (float along = PlaygroundSite.AlongMin + 2; along <= PlaygroundSite.AlongMax - 2; along += 1.5f)
            for (float across = -PlaygroundSite.AcrossMax + 2; across <= PlaygroundSite.AcrossMax - 2; across += 1.5f)
            {
                var p = yard.Drop + yard.Site.Inward * along + Vector3.Up.Cross(yard.Site.Inward) * across;
                samples++;
                if (world.Plants.NearTrunk(p, 1.5f))
                    trees++;
            }
            Check(samples > 200 && trees == 0, "no tree stands on the yard or its toys");
            Check(
                world.Hidden.Friends.All(friend => !world.InPlaygroundZone(friend.GlobalPosition)),
                "no small friend hides on the yard"
            );
            Check(world.Hidden.Friends.Count == HiddenFriends.Total, "every small friend still has a hiding place");
            Check(await DriveWay(yard), "the car drives from the road to the ring of the playground");

            // ---- Deliveries ----
            void Near(Character who) => car.SetPose(world.Ground.At(who.Position.X + 2, who.Position.Z, .15f), 0);
            void Deliver(bool save = true)
            {
                int step = yard.Stage;
                var cargo = PlaygroundPlans.CargoOf(step);
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                Near(keeper);
                game.OpenPlayground(keeper);
                Check(
                    game.State == GameState.PlaygroundStatus && Has("StartPlaygroundTrip"),
                    $"the keeper offers delivery {step + 1}"
                );
                game.BeginPlaygroundTrip();
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Playground == yard && trip.Step == step && trailer.Attached,
                    $"delivery {step + 1} sends the empty trailer"
                );
                Check(
                    trip!.Load == cargo
                        && trip.Contractor == keeper
                        && trip.Asker == world.PickupActor(cargo, trip.Source),
                    "the works' worker loads it for the keeper"
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
                Check(yard.Stage == step, "loading alone builds nothing");
                car.SetPose(world.Ground.At(yard.Drop.X, yard.Drop.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(yard.Stage == step + 1 && trailer.Load == null, $"delivery {step + 1} builds the next toy");
                Check(
                    yard.ChildrenPlaying == ChildrenAfter[step + 1],
                    $"{ChildrenAfter[step + 1]} children play after delivery {step + 1}"
                );
                for (int stage = 0; stage < PlaygroundPlans.Trips; stage++)
                    Check(
                        yard.GetNode<Node3D>("PlaygroundStage" + stage).Visible == (stage <= step),
                        $"exactly the toys of {step + 1} deliveries stand"
                    );
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
                if (save)
                    RoundTrip($"after delivery {step + 1}");
            }

            // Without the goods the delivery cannot start; the keeper sends the player for the raw material.
            foreach (var works in world.Industry)
                works.SetStock(0);
            Near(keeper);
            game.OpenPlayground(keeper);
            Check(
                !Has("StartPlaygroundTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                "the keeper shows what is missing when there is no gravel"
            );
            game.BeginPlaygroundTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts without its goods");
            var needed = world.Industry[(int)HaulLoads.ProductOf(PlaygroundPlans.CargoOf(0))];
            game.BeginMissingSupply();
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Loaded: false } fetch && fetch.Works == needed,
                "the keeper sends the empty trailer for the raw material"
            );
            var raw = world.SourceFor(needed.Spec.Input);
            car.SetPose(world.Ground.At(raw.Position.X, raw.Position.Z, .15f), 0);
            Frames(1);
            ChallengeChecks.Solve(game);
            car.SetPose(world.Ground.At(needed.Site.Position.X, needed.Site.Position.Z, .15f), 0);
            Frames(1);
            Frames(40);
            Check(needed.Stock == Works.PerLoad && game.Haul == null, "the works makes the goods");
            Near(keeper);
            game.OpenPlayground(keeper);
            Check(Has("StartPlaygroundTrip") && !Has("MissingGoods"), "back at the keeper the delivery can start");
            game.FinishInteraction();

            // A trip under way is shown, starts no second one and a save without it ends it.
            Near(keeper);
            game.OpenPlayground(keeper);
            game.BeginPlaygroundTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Playground: not null }, "a trip starts");
            game.OpenPlayground(keeper);
            Check(!Has("StartPlaygroundTrip"), "talking to the keeper during the trip starts no second one");
            game.FinishInteraction();
            var withoutTrip = game.CaptureProgress();
            withoutTrip.Task.Trip = null;
            game.RestoreProgress(withoutTrip);
            Check(game.Haul == null && trailer.Load == null, "a save without the trip ends it");

            int coinsBefore = game.Coins;
            Deliver();
            Check(game.Coins == coinsBefore, "no coins before the playground is finished");
            while (!yard.Complete)
                Deliver(save: yard.Stage < PlaygroundPlans.Trips - 1);
            Frames(5);
            Check(yard.Stage == PlaygroundPlans.Trips && yard.Complete, "six deliveries finish the playground");
            Check(game.Coins == coinsBefore + PlaygroundPlans.Coins, "the finished playground paid its coins");
            var finished = game.CaptureProgress();
            Check(
                finished.Economy.Owned.Contains(new CarReward(CarAccessory.Balloons.Id, true)),
                "the finished playground brought the balloons"
            );
            Check(yard.ChildrenPlaying == 8, "eight children play on the finished playground");
            Check(
                CarAccessory.Catalogue.Contains(CarAccessory.Balloons)
                    && new CarReward(CarAccessory.Balloons.Id, true).Price == null,
                "the balloons cannot be bought, only won"
            );
            RoundTrip("a finished playground");

            // The toys move and the finished playground blocks the car.
            float swingStart = yard.SwingAngle,
                spinStart = yard.RoundaboutAngle;
            for (int i = 0; i < 12; i++)
                yard._Process(.1);
            Check(
                !Mathf.IsEqualApprox(yard.SwingAngle, swingStart)
                    && !Mathf.IsEqualApprox(yard.RoundaboutAngle, spinStart),
                "the swing swings and the roundabout turns"
            );
            Check(yard.Visible && Has("PlaygroundStage5"), "the finished playground is part of the village");
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            // The playhouse stands higher than the soil beside it, wherever on the slope the yard lies.
            var house = yard.ToGlobal(new Vector3(6.8f, 0, 14.8f));
            float soil = world.Ground.Height(house.X, house.Z);
            var wall = ground.IntersectRay(
                PhysicsRayQueryParameters3D.Create(
                    new Vector3(house.X, soil + 10, house.Z),
                    new Vector3(house.X, soil - 2, house.Z)
                )
            );
            Check(
                wall.Count > 0 && wall["position"].AsVector3().Y > soil + 1.5f,
                $"the playhouse blocks the car (soil {soil:0.00}, ring {yard.Position.Y:0.00})"
            );
            Near(keeper);
            game.OpenPlayground(keeper);
            Check(
                game.State == GameState.PlaygroundStatus && !Has("StartPlaygroundTrip"),
                "a finished playground asks for no more material"
            );
            game.BeginPlaygroundTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts for a finished playground");
            game.FinishInteraction();

            // ---- Saves ----
            var oldSave = LegacySaves.FromCurrent(game.CaptureProgress(), 3);
            var old = LegacySaves.Read(oldSave);
            Check(
                old != null && old.Playground.Stage == 0 && old.Task.Trip == null,
                "a save from before the playground loads with an empty yard"
            );
            game.RestoreProgress(old!);
            Check(yard.Stage == 0, "the old save restores the empty yard");
            Rejected(data => data.Playground.Stage = PlaygroundPlans.Trips + 1, "a playground with seven deliveries");
            Rejected(data => data.Playground.Stage = -1, "a playground with a negative stage");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Playground, Index = 1 },
                "a trip for a second playground"
            );
            Rejected(
                data =>
                {
                    data.Playground.Stage = PlaygroundPlans.Trips;
                    data.Task.Trip = new SavedTrip { Kind = TripKinds.Playground, Index = 0 };
                },
                "a trip to a playground that has no delivery left"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.PlaygroundStatus;
                    data.Nearby = World.FarmerId;
                },
                "a playground panel at the farmer"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.PlotStatus;
                    data.Nearby = World.PlaygroundId;
                },
                "a plot panel at the playground's keeper"
            );

            // A panel left open at the keeper and a trip under way come back as they were.
            foreach (var works in world.Industry)
                works.SetStock(Works.Capacity);
            Near(keeper);
            game.OpenPlayground(keeper);
            RoundTrip("the panel of the keeper");
            Check(
                game.State == GameState.PlaygroundStatus && Has("StartPlaygroundTrip"),
                "the panel comes back after loading"
            );
            game.BeginPlaygroundTrip();
            ChallengeChecks.Solve(game);
            RoundTrip("an empty trailer on its way");
            Check(game.Haul is { Loaded: false, Playground: { } restored } && restored == yard, "the trip comes back");
            var cargoNow = PlaygroundPlans.CargoOf(0);
            var workSource = world.SourceFor(cargoNow);
            car.SetPose(world.Ground.At(workSource.Position.X, workSource.Position.Z, .15f), 0);
            Frames(1);
            RoundTrip("the question at the works");
            ChallengeChecks.Solve(game);
            RoundTrip("a full trailer on its way to the playground");
            Check(game.Haul is { Playground: not null, Loaded: true }, "the loaded trip comes back");

            GD.Print("PLAYGROUND_CHECKS_PASSED");
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
        {
            // Name what stopped the car, for the log.
            var forward = new Vector3(-Mathf.Sin(car.Heading), 0, -Mathf.Cos(car.Heading));
            var from = car.Position + Vector3.Up * 1.0f;
            var blocker = world
                .GetWorld3D()
                .DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, from + forward * 6));
            GD.Print(
                $"DRIVE_STALL best={best:0.0} at={car.Position} progress={progress}/{count} "
                    + $"blocker={(blocker.Count > 0 ? ((Node)blocker["collider"].AsGodotObject()).GetPath() : "none")} "
                    + $"groups={(blocker.Count > 0 ? string.Join(",", ((Node)blocker["collider"].AsGodotObject()).GetGroups()) : "")}"
            );
        }
        return best <= 4.5f;
    }
}
