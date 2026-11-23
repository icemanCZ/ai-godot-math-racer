using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The village zoo: that every valley has it, that it lies clear of water, roads, the playground and every tree, the
/// keeper, six deliveries each of which builds an enclosure and brings its animals, the animals that move (the
/// monkeys' swing, the elephant's spray, the pacing penguins), the visitors on the path, the finished zoo with its
/// coins and its baby panda, the way the car still drives to the ring and along the path, and the saves.
/// </summary>
public partial class ZooChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public ZooChecks(Game game, World world, Vehicle car)
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

    /// <summary>How many animals live in each enclosure: penguins, monkeys, elephant, giraffe, flamingos, pandas.</summary>
    private static readonly int[] AnimalsPerPen = { 4, 3, 1, 1, 3, 2 };

    /// <summary>How many visitors stroll along the path after this many deliveries.</summary>
    private static int VisitorsAfter(int deliveries) =>
        (deliveries >= 2 ? 1 : 0) + (deliveries >= 6 ? 1 : 0) + (deliveries >= 10 ? 1 : 0);

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("zoo-checks/progress.json");
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
                    $"zoo progress restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"zoo-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- The zoo and its keeper ----
            var zoo = world.Zoo;
            Check(zoo != null, "every valley has a zoo");
            var keeper = zoo!.Helper;
            Check(
                keeper.Role is ZooRole role
                    && role.Zoo == zoo
                    && world.ActorId(keeper) == World.ZooKeeperId
                    && world.ActorAt(World.ZooKeeperId) == keeper
                    && World.IsZooKeeper(World.ZooKeeperId)
                    && ActorIds.ZooKeeper == 71,
                "the keeper is registered under the next free actor ID"
            );
            Check(keeper.DisplayName == CharacterCatalog.ZooKeeper.Name, "the keeper has his name");
            Check(zoo.Stage == 0 && !zoo.Complete && zoo.AnimalsHome == 0, "the zoo starts as an empty site");
            Check(zoo.SiteStands && !zoo.GateStands, "only a construction site stands at first, with no gate");
            Check(zoo.VisitorsWalking == 0 && !zoo.Spraying, "no visitors and no spray yet");
            var waiting = world.WorldPets.SingleOrDefault(p => p.Kind == CollectorPetKind.PandaCub);
            Check(waiting != null && !waiting.IsOwned, "the baby panda is not a pet before the zoo is done");
            Check(ZooPlans.Trips == 12 && Zoo.Trips == ZooPlans.Trips, "twelve deliveries finish the zoo");
            for (int i = 0; i < ZooPlans.Trips; i++)
            {
                Check(HaulLoads.IsGoods(ZooPlans.CargoOf(i)), $"delivery {i + 1} is goods from a works");
                Check(!zoo.GetNode<Node3D>("ZooStage" + i).Visible, $"no part of stage {i + 1} stands yet");
            }
            for (int pen = 0; pen < ZooPlans.Pens; pen++)
            {
                Check(
                    ZooPlans.CargoOf(2 * pen) != ZooPlans.CargoOf(2 * pen + 1),
                    $"enclosure {pen + 1} needs two different materials"
                );
                Check(
                    zoo.AnimalsIn(2 * pen + 1) == AnimalsPerPen[pen] && zoo.AnimalsIn(2 * pen) == 0,
                    $"enclosure {pen + 1} has {AnimalsPerPen[pen]} animals ready for its second delivery"
                );
            }
            Check(
                ZooPlans.Parts.Length == ZooPlans.Trips && ZooPlans.Signs.Length == ZooPlans.Pens,
                "every delivery has a name and every enclosure a sign"
            );
            Check(Zoo.Pens.Length == ZooPlans.Pens, "one enclosure for every pair of deliveries");

            // ---- The ground: dry, off the roads, clear of the playground and of every tree ----
            int wet = 0,
                onRoad = 0,
                trees = 0,
                onYard = 0,
                total = 0;
            for (float along = -3; along <= 30.5f; along += 2.5f)
            for (float across = -13; across <= 13; across += 2.6f)
            {
                var global = zoo.ToGlobal(new Vector3(across, 0, along));
                total++;
                if (River.Distance(global.X, global.Z) < River.HalfWidth(global.Z) + 1)
                    wet++;
                if (along > 1.5f && world.Routes.Distance(global) < 3)
                    onRoad++;
                if (world.Plants.NearTrunk(global, 1.0f))
                    trees++;
                if (world.InPlaygroundZone(global))
                    onYard++;
            }
            Check(total > 150 && wet == 0, "no water inside the zoo");
            Check(onRoad == 0, "no road crosses the zoo");
            Check(trees == 0, "no tree stands in the zoo");
            Check(onYard == 0, "the zoo does not touch the playground");
            foreach (var (x, z) in Zoo.Pens)
                Check(
                    world.InZooZone(zoo.ToGlobal(new Vector3(x, 0, z))),
                    $"the enclosure at {x:0.0}, {z:0.0} lies in the cleared ground"
                );
            Check(
                world.Hidden.Friends.All(friend => !world.InZooZone(friend.GlobalPosition)),
                "no small friend hides in the zoo"
            );
            Check(
                world.InteractionActors.All(actor => actor == keeper || !world.InZooZone(actor.GlobalPosition)),
                "no other character stands in the zoo"
            );
            var playground = world.Playground;
            if (playground != null)
                Check(
                    Flat(zoo.Drop, playground.Drop) > 40,
                    $"the zoo stands {Flat(zoo.Drop, playground.Drop):0} m from the playground"
                );

            // ---- Deliveries ----
            void Near(Character who) => car.SetPose(world.Ground.At(who.Position.X + 2, who.Position.Z, .15f), 0);
            void Deliver(bool save = true)
            {
                int step = zoo.Stage;
                var cargo = ZooPlans.CargoOf(step);
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                Near(keeper);
                game.OpenZoo(keeper);
                Check(
                    game.State == GameState.ZooStatus && Has("StartZooTrip"),
                    $"the keeper offers delivery {step + 1}"
                );
                game.BeginZooTrip();
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Zoo == zoo && trip.Step == step && trailer.Attached,
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
                Check(zoo.Stage == step, "loading alone builds nothing");
                car.SetPose(world.Ground.At(zoo.Drop.X, zoo.Drop.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping && !car.Active, "the bed tips while the car waits");
                Frames(40);
                Check(zoo.Stage == step + 1 && trailer.Load == null, $"delivery {step + 1} builds the next enclosure");
                Check(
                    zoo.AnimalsHome == AnimalsPerPen.Take((step + 1) / 2).Sum(),
                    $"{AnimalsPerPen.Take((step + 1) / 2).Sum()} animals live in the zoo after delivery {step + 1}"
                );
                Check(
                    zoo.VisitorsWalking == VisitorsAfter(step + 1),
                    $"{VisitorsAfter(step + 1)} visitors stroll along the path after delivery {step + 1}"
                );
                for (int stage = 0; stage < ZooPlans.Trips; stage++)
                    Check(
                        zoo.GetNode<Node3D>("ZooStage" + stage).Visible == (stage <= step),
                        $"exactly the enclosures of {step + 1} deliveries stand"
                    );
                Check(
                    zoo.SiteStands == (step + 1 < ZooPlans.Trips),
                    "the construction site stays until the last delivery"
                );
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
                if (save)
                    RoundTrip($"after delivery {step + 1}");
            }

            // Without the goods the delivery cannot start; the keeper sends the player for the raw material.
            foreach (var works in world.Industry)
                works.SetStock(0);
            Near(keeper);
            game.OpenZoo(keeper);
            Check(
                !Has("StartZooTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                "the keeper shows what is missing when there is no gravel"
            );
            game.BeginZooTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts without its goods");
            var needed = world.Industry[(int)HaulLoads.ProductOf(ZooPlans.CargoOf(0))];
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
            game.OpenZoo(keeper);
            Check(Has("StartZooTrip") && !Has("MissingGoods"), "back at the keeper the delivery can start");
            game.FinishInteraction();

            // A trip under way is shown, starts no second one and a save without it ends it.
            Near(keeper);
            game.OpenZoo(keeper);
            game.BeginZooTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Zoo: not null }, "a trip starts");
            game.OpenZoo(keeper);
            Check(!Has("StartZooTrip"), "talking to the keeper during the trip starts no second one");
            game.FinishInteraction();
            var withoutTrip = game.CaptureProgress();
            withoutTrip.Task.Trip = null;
            game.RestoreProgress(withoutTrip);
            Check(game.Haul == null && trailer.Load == null, "a save without the trip ends it");

            int coinsBefore = game.Coins;
            Deliver();
            Check(game.Coins == coinsBefore, "no coins before the zoo is finished");
            Check(
                zoo.AnimalsHome == 0,
                "after the first delivery of an enclosure only its fence stands, with no animal"
            );
            Deliver();

            // The penguins pace about, the visitor walks along the path and the animals hop when the car comes.
            var first = zoo.FirstAnimalPosition(1);
            var visitor = zoo.FirstVisitorPosition;
            for (int i = 0; i < 12; i++)
                zoo._Process(.1);
            Check(Flat(first, zoo.FirstAnimalPosition(1)) > .05f, "the first penguin paces about");
            Check(Flat(visitor, zoo.FirstVisitorPosition) > .5f, "the visitor walks along the path");
            car.SetPose(world.Ground.At(zoo.Drop.X + 120, zoo.Drop.Z + 120, .15f), 0);
            Frames(2);
            for (int i = 0; i < 40 && zoo.Cheering; i++)
                zoo._Process(.1);
            Check(!zoo.Cheering, "the cheering dies away");
            car.SetPose(world.Ground.At(zoo.Drop.X, zoo.Drop.Z, .15f), 0);
            Frames(1);
            Check(zoo.Cheering, "the car's arrival makes the animals cheer");

            while (zoo.Stage < 4)
                Deliver();
            // The monkeys swing on their rope.
            float swing = 0;
            for (int i = 0; i < 40; i++)
            {
                zoo._Process(.1);
                swing = Mathf.Max(swing, Mathf.Abs(zoo.SwingAngle));
            }
            Check(swing > .3f, $"the monkey swings on the rope ({swing:0.00} rad)");

            while (zoo.Stage < 6)
                Deliver();
            // The elephant raises her trunk and sprays water now and then.
            bool sprayed = false,
                rested = false;
            for (int i = 0; i < 160; i++)
            {
                zoo._Process(.1);
                sprayed |= zoo.Spraying;
                rested |= !zoo.Spraying;
            }
            Check(sprayed && rested, "the elephant sprays water now and then");

            while (!zoo.Complete)
                Deliver(save: zoo.Stage < ZooPlans.Trips - 1);
            Frames(5);
            Check(zoo.Stage == ZooPlans.Trips && zoo.Complete, "twelve deliveries finish the zoo");
            Check(
                !zoo.SiteStands && zoo.GateStands,
                "the finished zoo has cleared its construction site and opened its gate"
            );
            Check(game.Coins == coinsBefore + ZooPlans.Coins, "the finished zoo paid its coins");
            var pet = world.WorldPets.Single(p => p.Kind == CollectorPetKind.PandaCub);
            Check(pet.IsOwned && pet.IsActive, "the finished zoo brought the baby panda as a pet");
            Check(
                pet.DisplayName == WorldPetCatalog.PandaCub.Name && pet.Collector == keeper,
                "the pet belongs to the keeper"
            );
            RoundTrip("a finished zoo");

            // Every enclosure blocks the car; the path between them is open.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            var space = world.GetWorld3D().DirectSpaceState;
            float HitAbove(Vector3 spot)
            {
                float soil = world.Ground.Height(spot.X, spot.Z);
                var hit = space.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(
                        new Vector3(spot.X, soil + 12, spot.Z),
                        new Vector3(spot.X, soil - 2, spot.Z)
                    )
                );
                return hit.Count > 0 ? hit["position"].AsVector3().Y - soil : -99;
            }
            for (int i = 0; i < Zoo.Pens.Length; i++)
                Check(
                    HitAbove(zoo.ToGlobal(new Vector3(Zoo.Pens[i].X, 0, Zoo.Pens[i].Z))) > .9f,
                    $"enclosure {i + 1} blocks the car"
                );
            foreach (float z in new[] { 5f, 10f, 15f, 20f, 25f })
                Check(HitAbove(zoo.ToGlobal(new Vector3(0, 0, z))) < .5f, $"the path is open to the car at {z:0} m");
            Check(await DriveWay(zoo), "the car still drives from the road to the ring with the zoo built");

            Near(keeper);
            game.OpenZoo(keeper);
            Check(
                game.State == GameState.ZooStatus && !Has("StartZooTrip"),
                "a finished zoo asks for no more material"
            );
            game.BeginZooTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts for a finished zoo");
            game.FinishInteraction();

            // ---- Saves ----
            var oldSave = LegacySaves.FromCurrent(game.CaptureProgress(), 3);
            var old = LegacySaves.Read(oldSave);
            Check(
                old != null && old.Zoo.Stage == 0 && old.Task.Trip == null,
                "a save from before the zoo loads with the construction site only"
            );
            game.RestoreProgress(old!);
            Check(zoo.Stage == 0 && zoo.AnimalsHome == 0, "the old save restores the empty site");
            Rejected(data => data.Zoo.Stage = ZooPlans.Trips + 1, "a zoo with seven deliveries");
            Rejected(data => data.Zoo.Stage = -1, "a zoo with a negative stage");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Zoo, Index = 1 },
                "a trip for a second zoo"
            );
            Rejected(
                data =>
                {
                    data.Zoo.Stage = ZooPlans.Trips;
                    data.Task.Trip = new SavedTrip { Kind = TripKinds.Zoo, Index = 0 };
                },
                "a trip to a zoo that has no delivery left"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.ZooStatus;
                    data.Nearby = World.FarmerId;
                },
                "a zoo panel at the farmer"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.RailwayStatus;
                    data.Nearby = World.ZooKeeperId;
                },
                "a railway panel at the zoo keeper"
            );
            Rejected(data => data.Nearby = World.HunterId + 1, "a character beyond the zoo keeper");

            // A panel left open at the keeper and a trip under way come back as they were.
            foreach (var works in world.Industry)
                works.SetStock(Works.Capacity);
            Near(keeper);
            game.OpenZoo(keeper);
            RoundTrip("the panel of the keeper");
            Check(game.State == GameState.ZooStatus && Has("StartZooTrip"), "the panel comes back after loading");
            game.BeginZooTrip();
            ChallengeChecks.Solve(game);
            RoundTrip("an empty trailer on its way");
            Check(game.Haul is { Loaded: false, Zoo: { } restored } && restored == zoo, "the trip comes back");
            var cargoNow = ZooPlans.CargoOf(0);
            var workSource = world.SourceFor(cargoNow);
            car.SetPose(world.Ground.At(workSource.Position.X, workSource.Position.Z, .15f), 0);
            Frames(1);
            RoundTrip("the question at the works");
            ChallengeChecks.Solve(game);
            RoundTrip("a full trailer on its way to the zoo");
            Check(game.Haul is { Zoo: not null, Loaded: true }, "the loaded trip comes back");

            GD.Print("ZOO_CHECKS_PASSED");
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
    private async Task<bool> DriveWay(Zoo zoo)
    {
        int count = Mathf.CeilToInt(Flat(zoo.Road, zoo.Drop) / 2) + 1;
        var points = Enumerable
            .Range(0, count)
            .Select(i => zoo.Road.Lerp(zoo.Drop, i / (float)(count - 1)))
            .Select(p => world.Ground.At(p.X, p.Z))
            .ToList();
        var start = points[1];
        var first = points[Mathf.Min(3, count - 1)] - start;
        car.SetPose(world.Ground.At(start.X, start.Z, .15f), Mathf.Atan2(-first.X, -first.Z));
        float best = float.PositiveInfinity;
        int progress = 0,
            interruptions = 0;
        for (int frame = 0; frame < 6000 && best > 3; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            // A roadside friend who stands near the road asks his question when the car comes by: answer it and go on.
            if (game.State != GameState.Exploring && interruptions++ < 6)
            {
                ChallengeChecks.Solve(game);
                game.FinishInteraction();
                continue;
            }
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
            best = Mathf.Min(best, Flat(car.Position, zoo.Drop));
        }
        Input.ActionRelease("accelerate");
        Input.ActionRelease("left");
        Input.ActionRelease("right");
        if (best > 4.5f)
            GD.Print($"DRIVE_STALL best={best:0.0} at={car.Position} progress={progress}/{count} state={game.State}");
        return best <= 4.5f;
    }
}
