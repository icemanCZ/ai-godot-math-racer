using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The fifth chain: the wheat field with its farmer behind the village, the wheat hauled to the finished mill, the
/// flour that the miller loads for the bakery, the bread that the bakery bakes, the baker's orders for his customers that
/// take that bread and the order for bread that follows the housewarming of a house.
/// </summary>
public partial class BakeryChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public BakeryChecks(Game game, World world, Vehicle car)
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

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("bakery-checks/progress.json");
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), $"save written: {what}");
                var loaded = SaveStore.Read(path) ?? throw new InvalidOperationException($"save refused: {what}");
                game.RestoreProgress(loaded);
                var before = System.Text.Json.JsonSerializer.SerializeToNode(saved)!.AsObject();
                var after = System.Text.Json.JsonSerializer.SerializeToNode(game.CaptureProgress())!.AsObject();
                foreach (var (key, value) in before)
                    if (!System.Text.Json.Nodes.JsonNode.DeepEquals(value, after[key]))
                        GD.Print($"DIFFERENT {what}: {key} was {value} now {after[key]}");
                Check(
                    System.Text.Json.JsonSerializer.Serialize(saved)
                        == System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress()),
                    $"restoring and capturing again gives the same save: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"bakery-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- Placement ----
            Check(
                world.Farm != null && world.WheatField != null && world.FieldFarmer != null && world.Bakery != null,
                "valley has a wheat farm, its farmer and a bakery"
            );
            var field = world.WheatField!.Value;
            var farmer = world.FieldFarmer!;
            var bakery = world.Bakery!;
            Check(
                farmer.Role is SourceRole { Load: HaulLoad.Wheat, Index: -1 }
                    && world.ActorId(farmer) == World.FieldFarmerId
                    && world.ActorAt(World.FieldFarmerId) == farmer
                    && ActorIds.FieldFarmer == World.FieldFarmerId,
                "the farmer at the field has an ID of his own and loads wheat"
            );
            Check(
                bakery.Worker.Role is WorksRole { Index: WorksCatalog.BreadIndex }
                    && world.ActorId(bakery.Worker) == World.BakeryId
                    && world.ActorAt(World.BakeryId) == bakery.Worker
                    && bakery.Spec.Product == Product.Bread
                    && bakery.Spec.Input == HaulLoad.Flour
                    && bakery.Input == HaulLoad.Flour,
                "the baker has an ID of his own and bakes bread from flour"
            );
            var mill = (Mill)world.Projects[BuildPlans.Mill];
            var flour = world.FlourMill!;
            var miller = mill.Builder;
            Check(
                flour != null && flour.Mill == mill && flour.Miller == miller && !flour.Grinding && flour.Stock == 0,
                "the mill grinds for the bakery, but not before it is finished"
            );
            Check(
                world.SourceFor(HaulLoad.Flour).Position == mill.Site.Position
                    && world.PickupActor(HaulLoad.Flour) == miller
                    && World.AskerIds(HaulLoad.Flour, 0).SequenceEqual(new[] { World.BuilderId(BuildPlans.Mill) }),
                "flour is loaded at the mill's ring by the miller"
            );
            Check(
                world.WorksFor(Product.Bread) == bakery
                    && world.WorksByIndex(WorksCatalog.BreadIndex) == bakery
                    && world.AllWorks.Contains(bakery)
                    && world.Industry.Length == WorksCatalog.Count,
                "the bakery is the fifth works, apart from the original three"
            );
            Check(
                world.SourceFor(HaulLoad.Wheat).Position == field.Position
                    && world.PickupActor(HaulLoad.Wheat) == farmer
                    && World.AskerIds(HaulLoad.Wheat, 0).SequenceEqual(new[] { World.FieldFarmerId })
                    && world.SourceSites(HaulLoad.Wheat).Count() == 1
                    && world.SourceFor(HaulLoad.Bread).Position == bakery.Site.Position,
                "wheat is loaded at the field by the farmer, bread at the bakery"
            );
            Check(Flat(farmer.Position, field.Position) is > 1.5f and < 6, "the farmer stands by the loading ring");
            Check(
                world.Tracks!.Contains(field.Position)
                    && world.Tracks.Contains(field.Road)
                    && world.Tracks.Contains(bakery.Site.Position),
                "field paths lead to the farm and the bakery"
            );
            float village = world.Village.PlotDistance(field.Position);
            Check(village is > 40 and < 260, $"the farm lies outside the village but not far ({village:0} m)");
            Check(
                world.Farm!.GetChildren().OfType<MultiMeshInstance3D>().Count() == 4,
                "four fields of wheat lie around the farm"
            );
            Check(
                world.Glassworks == null
                    || Flat(world.Glassworks.Site.Position, field.Position) > 40
                        && Flat(world.Glassworks.Site.Position, bakery.Site.Position) > 30,
                "the farm and the bakery keep clear of the glassworks"
            );
            // The trees that stood on the farm are gone, so the fields can be driven to.
            var trunks = GetTree()
                .GetNodesInGroup("obstacle_trunks")
                .Cast<Node3D>()
                .Where(trunk => trunk.IsInsideTree() && !trunk.IsQueuedForDeletion())
                .Where(trunk => world.InFarmZone(trunk.GlobalPosition) && trunk.GetParent() == world.Plants)
                .ToList();
            Check(trunks.Count == 0, "no tree stands on the farm");

            // ---- Before the mill is finished nobody grinds the wheat ----
            var trailer = game.Trailer;
            Check(bakery.Stock == 0 && bakery.ShownUnits == 0, "the bakery starts with an empty yard");
            car.SetPose(world.Ground.At(farmer.Position.X + 2, farmer.Position.Z, .15f), 0);
            game.OpenSource(farmer);
            Check(
                game.State == GameState.SourceStatus && !Has("StartSupplyTrip") && Has("MillChain"),
                "while the mill is being built the farmer shows the chain but sends no wheat"
            );
            game.BeginSupplyTrip();
            Check(game.Haul == null, "no wheat trip starts before the mill is finished");
            game.FinishInteraction();
            var unfinished = game.CaptureProgress();
            unfinished.Buildings.Stages[BuildPlans.Mill] = BuildPlans.Trips;
            game.RestoreProgress(unfinished);
            Check(flour.Grinding && mill.Complete, "the finished mill grinds");
            RoundTrip("the finished mill");

            // ---- Hauling wheat to the mill ----
            void Wheat(int expected)
            {
                car.SetPose(world.Ground.At(farmer.Position.X + 2, farmer.Position.Z, .15f), 0);
                game.OpenSource(farmer);
                Check(
                    game.State == GameState.SourceStatus && Has("StartSupplyTrip") && Has("FlourStock"),
                    "the farmer offers the next load for the mill"
                );
                game.BeginSupplyTrip();
                var trip = game.Haul;
                Check(trip is { Loaded: false } && trip.Load == HaulLoad.Wheat, "the trailer is sent for wheat");
                Check(trip!.Target == flour && trip.Contractor == miller, "the wheat is meant for the mill");
                Check(trip.Asker == farmer, "the farmer asks the loading question");
                Check(
                    game.State == GameState.Challenge && game.CurrentChallenge != null,
                    $"the question is open ({game.State}, {game.CurrentChallenge?.GetType().Name})"
                );
                if (game.CurrentChallenge is LoadChallenge load)
                    Check(load.Cargo == HaulLoad.Wheat, "the loading question shows wheat");
                RoundTrip("loading wheat");
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: true } && trailer.Load == HaulLoad.Wheat,
                    "the trailer is loaded with wheat"
                );
                RoundTrip("full trailer of wheat");
                var ring = mill.Site.Position;
                car.SetPose(world.Ground.At(ring.X, ring.Z, .15f), 0);
                Frames(1);
                Frames(40);
                Check(
                    flour.Stock == expected && mill.ShownSacks == expected && game.Haul == null,
                    $"the mill shows {expected} sacks of flour"
                );
                Check(bakery.Stock == 0, "the wheat does not go to the bakery");
                RoundTrip($"after wheat delivery {expected}");
            }
            for (int load = 1; load <= FlourMill.Capacity / FlourMill.PerLoad; load++)
                Wheat(load * FlourMill.PerLoad);
            Check(flour.Full && world.Tally.Wheat == 6, "six loads of wheat fill the mill and are counted");
            car.SetPose(world.Ground.At(farmer.Position.X + 2, farmer.Position.Z, .15f), 0);
            game.OpenSource(farmer);
            Check(!Has("StartSupplyTrip"), "a full mill asks for no more wheat");
            game.FinishInteraction();

            // ---- Hauling flour to the bakery ----
            void Flour(int expected)
            {
                int sacks = flour.Stock;
                car.SetPose(world.Ground.At(miller.Position.X + 2, miller.Position.Z, .15f), 0);
                game.OpenBuilder(miller);
                Check(
                    game.State == GameState.BuildStatus && Has("StartSupplyTrip") && Has("FlourStock"),
                    "the miller offers flour for the bakery"
                );
                game.BeginSupplyTrip();
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Load == HaulLoad.Flour && trip.Works == bakery,
                    "the trailer is sent with flour for the bakery"
                );
                Check(
                    trip!.Asker == miller && game.State == GameState.Challenge,
                    "the miller asks the loading question"
                );
                if (game.CurrentChallenge is LoadChallenge load)
                    Check(load.Cargo == HaulLoad.Flour, "the loading question shows flour");
                RoundTrip("loading flour");
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: true } && trailer.Load == HaulLoad.Flour && flour.Stock == sacks - 1,
                    "one sack of flour is taken from the mill and loaded"
                );
                RoundTrip("full trailer of flour");
                var ring = bakery.Site.Position;
                car.SetPose(world.Ground.At(ring.X, ring.Z, .15f), 0);
                Frames(1);
                Frames(40);
                Check(
                    bakery.Stock == expected && bakery.ShownUnits == expected && game.Haul == null,
                    $"the bakery shows {expected} loaves"
                );
                RoundTrip($"after flour delivery {expected}");
            }
            for (int load = 1; load <= Works.Capacity / Works.PerLoad; load++)
                Flour(load * Works.PerLoad);
            Check(
                bakery.Full && world.Tally.Baked == 6 && flour.Stock == FlourMill.Capacity - 6,
                "six sacks of flour fill the bakery and are counted"
            );
            car.SetPose(world.Ground.At(miller.Position.X + 2, miller.Position.Z, .15f), 0);
            game.OpenBuilder(miller);
            Check(game.State == GameState.BuildStatus && !Has("StartSupplyTrip"), "a full bakery takes no more flour");
            game.FinishInteraction();

            // ---- The pets ----
            var wheatPet = world.WorldPets.First(p => p.Kind == CollectorPetKind.Wheat);
            var bakeryPet = world.WorldPets.First(p => p.Kind == CollectorPetKind.Bakery);
            Check(!wheatPet.IsOwned && !bakeryPet.IsOwned, "the pets of the wheat and the bakery are not earned yet");
            Check(wheatPet.Collector == farmer && bakeryPet.Collector == bakery.Worker, "each waits by its worker");
            for (int i = world.Tally.Wheat; i < WorldPetCatalog.SourceLoads; i++)
                world.Tally.AddRaw(HaulLoad.Wheat);
            for (int i = world.Tally.Baked; i < WorldPetCatalog.WorksLoads; i++)
                world.Tally.AddWorks(WorksCatalog.BreadIndex);
            Check(wheatPet.IsOwned && bakeryPet.IsOwned, "ten loads of wheat earn Zrníčko and Housánek");
            RoundTrip("the pets of the bakery");
            Check(
                world.Tally.Wheat == WorldPetCatalog.SourceLoads && world.Tally.Baked == WorldPetCatalog.WorksLoads,
                "the counts of wheat and baked loads are saved"
            );

            // ---- Bread for the houses ----
            BreadOrder(RoundTrip, Rejected);

            // ---- The baker's customers ----
            BakerOrders(RoundTrip);

            // ---- A save from before the mill: the farmer's question about wheat for the bakery ----
            LegacyWheatTrip(path);

            // ---- Damaged saves ----
            Rejected(data => data.Buildings.BreadStock = Works.Capacity + 1, "more bread than the yard holds");
            Rejected(data => data.Buildings.BreadStock = -1, "negative bread");
            Rejected(data => data.Buildings.FlourStock = FlourMill.Capacity + 1, "more flour than the mill holds");
            Rejected(data => data.Buildings.FlourStock = -1, "negative flour");
            Rejected(
                data =>
                {
                    data.Buildings.Stages[BuildPlans.Mill] = 9;
                    data.Buildings.FlourStock = 2;
                },
                "flour at a mill that is not finished"
            );
            Rejected(
                data =>
                {
                    data.Buildings.Stages[BuildPlans.Mill] = 9;
                    data.Buildings.FlourStock = 0;
                    data.Task = new TaskSave { Trip = new SavedTrip { Kind = TripKinds.Mill } };
                },
                "wheat for a mill that is not finished"
            );
            Rejected(
                data =>
                {
                    data.Buildings.FlourStock = FlourMill.Capacity;
                    data.Task = new TaskSave { Trip = new SavedTrip { Kind = TripKinds.Mill } };
                },
                "wheat for a mill full of flour"
            );
            Rejected(
                data =>
                {
                    data.Village.HouseLevels = new[] { 4, 0, 0, 0 };
                    data.Village.Baked = new[] { true, false, false, false };
                },
                "bread for a house that is not finished"
            );
            Rejected(
                data =>
                {
                    data.Village.HouseLevels = new[] { 10, 0, 0, 0 };
                    data.Village.Glazed = new bool[4];
                    data.Village.Baked = new[] { true, false, false, false };
                },
                "bread for a house that has no glasses"
            );
            Rejected(data => data.Village.Baked = new bool[3], "three houses given bread");
            Rejected(data => data.Hauls.Wheat = -1, "a negative count of wheat");
            Rejected(data => data.Nearby = World.PostmanId + 1, "a character beyond the last actor");

            GD.Print("BAKERY_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    /// <summary>
    /// The baker of the village shop sells the bakery's bread once the mill grinds flour for it: an order takes its loaves
    /// from the bakery, and without enough bread he sends the player for flour, or for wheat when the mill has none.
    /// </summary>
    private void BakerOrders(Action<string> roundTrip)
    {
        var bakery = world.Bakery!;
        var flour = world.FlourMill!;
        var baker = world.Village.Traders[0];
        var data = game.CaptureProgress();
        data.Task = new TaskSave();
        data.Quiz = new QuizSave { Types = data.Quiz.Types, ThreeTermMath = data.Quiz.ThreeTermMath };
        data.State = GameState.Exploring;
        data.Nearby = -1;
        data.Buildings.BreadStock = 0;
        data.Buildings.FlourStock = 3;
        game.RestoreProgress(data);

        // No bread: the baker sends the player to the mill for flour.
        car.SetPose(baker.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(baker);
        Check(
            game.State == GameState.TradeStatus && game.CurrentChallenge == null && Has("StartMissingSupply"),
            "without bread the baker asks for none of his quiz but shows what is missing"
        );
        roundTrip("the baker without bread");
        game.BeginMissingSupply();
        Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "a quiz comes before the trip");
        roundTrip("the baker's quiz before the trip for flour");
        ChallengeChecks.Solve(game);
        Check(
            game.Haul is { Loaded: false, Load: HaulLoad.Flour } trip && trip.Works == bakery && game.Delivery == null,
            "the baker sends the trailer to the mill for flour"
        );
        roundTrip("the baker's trip for flour");
        game.FinishInteraction();

        // No bread and no flour: the baker sends the player to the field for wheat.
        data = game.CaptureProgress();
        data.Task = new TaskSave();
        data.State = GameState.Exploring;
        data.Nearby = -1;
        data.Buildings.FlourStock = 0;
        game.RestoreProgress(data);
        game.OpenChallenge(baker);
        Check(game.State == GameState.TradeStatus && Has("StartMissingSupply"), "the baker shows the missing flour");
        game.BeginMissingSupply();
        ChallengeChecks.Solve(game);
        Check(
            game.Haul is { Loaded: false, Load: HaulLoad.Wheat } wheat && wheat.Target == flour,
            "without flour the baker sends the trailer to the field for wheat for the mill"
        );
        game.FinishInteraction();

        // With bread the order takes the loaves from the bakery and brings the customer the bread.
        data = game.CaptureProgress();
        data.Task = new TaskSave();
        data.State = GameState.Exploring;
        data.Nearby = -1;
        data.Buildings.BreadStock = Works.Capacity;
        game.RestoreProgress(data);
        int amount = Expedition.OrderAmount(world, baker);
        game.OpenChallenge(baker);
        Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "with bread the baker asks his quiz");
        ChallengeChecks.Solve(game);
        Check(
            game.Delivery is { Collection: false } order
                && order.Sender == baker
                && order.Amount == amount
                && order.Cargo == "bread"
                && bakery.Stock == Works.Capacity - amount
                && bakery.ShownUnits == bakery.Stock,
            $"the order takes its {amount} loaves from the bakery"
        );
        roundTrip("the baker's order with the bakery's bread");
        Check(bakery.Stock == Works.Capacity - amount, "a restored order does not take the loaves again");
        var customer = game.Delivery!.Recipient;
        int coins = game.Coins;
        car.SetPose(customer.Position + Vector3.Right * 2, 0);
        game.BeginDeliveryQuestion();
        Check(
            game.State == GameState.Reward && game.Coins == coins + Game.TradeCoins + Game.BakerBonus,
            "the customer gets the bread and the baker pays more for the mill's flour"
        );
        game.FinishInteraction();
        Check(game.Delivery == null && bakery.Stock == Works.Capacity - amount, "the order is done");
    }

    /// <summary>
    /// A save from before the mill ground flour: the trailer was sent to the field for wheat for the bakery and the farmer
    /// asks the loading question. It still loads, the trailer is loaded and the bakery bakes from the load.
    /// </summary>
    private void LegacyWheatTrip(string path)
    {
        var bakery = world.Bakery!;
        var farmer = world.FieldFarmer!;
        var data = game.CaptureProgress();
        data.Buildings.BreadStock = 0;
        data.Buildings.FlourStock = 0;
        data.Task = new TaskSave
        {
            Trip = new SavedTrip { Kind = TripKinds.Supply, Index = WorksCatalog.BreadIndex },
        };
        data.State = GameState.Challenge;
        data.Nearby = World.FieldFarmerId;
        data.Quiz = new QuizSave
        {
            Types = data.Quiz.Types,
            ThreeTermMath = data.Quiz.ThreeTermMath,
            Challenge = LoadChallenge.Create(new Random(5), HaulLoad.Wheat),
        };
        Check(SaveStore.Write(data, path), "save written: the farmer's question from before the mill");
        var loaded = SaveStore.Read(path);
        Check(loaded != null, "a save with the farmer's question about wheat for the bakery still loads");
        game.RestoreProgress(loaded!);
        Check(
            game.State == GameState.Challenge && game.Haul is { Loaded: false } trip && trip.Works == bakery,
            "the question about wheat for the bakery is open again"
        );
        ChallengeChecks.Solve(game);
        Check(game.Haul is { Loaded: true }, "the farmer loads the trailer as he did before the mill");
        var ring = bakery.Site.Position;
        car.SetPose(world.Ground.At(ring.X, ring.Z, .15f), 0);
        Frames(1);
        Frames(40);
        Check(game.Haul == null && bakery.Stock == Works.PerLoad, "the bakery bakes from the old load");
        Check(!farmer.IsQueuedForDeletion(), "the farmer is still at the field");
    }

    /// <summary>A finished house with glasses asks for bread once: the order is saved so that it is not asked again.</summary>
    private void BreadOrder(Action<string> roundTrip, Action<Action<SaveData>, string> rejected)
    {
        var village = world.Village;
        var works = world.Bakery!;
        var owner = village.Residents[0];
        var data = game.CaptureProgress();
        data.Village.HouseLevels = new[] { 10, 0, 0, 0 };
        data.Village.Glazed = new[] { true, false, false, false };
        data.Village.Baked = new bool[4];
        data.Task = new TaskSave();
        data.Quiz = new QuizSave { Types = data.Quiz.Types, ThreeTermMath = data.Quiz.ThreeTermMath };
        data.State = GameState.Exploring;
        data.Nearby = -1;
        data.Buildings.BreadStock = Works.Capacity;
        game.RestoreProgress(data);
        Check(works.Stock == Works.Capacity && works.ShownUnits == Works.Capacity, "the bakery shows its loaves");

        car.SetPose(owner.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(owner);
        Check(game.State == GameState.HouseMaterialOffer, "a house that has its glasses asks for bread");
        Check(Has("StartHouseTrip"), "with bread in stock the play button sends the trailer");
        roundTrip("bread asked for");
        game.BeginHouseTrip();
        ChallengeChecks.Solve(game);
        var trip = game.Haul;
        Check(
            trip is { Loaded: false } && trip.House == 0 && trip.Load == HaulLoad.Bread && game.Trailer.Attached,
            "the empty trailer is sent to the bakery"
        );
        Check(trip!.Asker == works.Worker && trip.Contractor == owner, "the baker loads it for the resident");
        roundTrip("trip for bread");
        car.SetPose(world.Ground.At(works.Site.Position.X, works.Site.Position.Z, .15f), 0);
        Frames(1);
        Check(game.State == GameState.Challenge, "the loading question is asked at the bakery");
        ChallengeChecks.Solve(game);
        Check(
            game.Haul is { Loaded: true }
                && game.Trailer.Load == HaulLoad.Bread
                && works.Stock == Works.Capacity - Works.PerTrip,
            "one loaf is taken from the stock and loaded"
        );
        roundTrip("bread loaded");
        int coins = game.Coins;
        var drop = village.HouseDrop(0);
        car.SetPose(world.Ground.At(drop.X, drop.Z, .15f), 0);
        Frames(1);
        Frames(40);
        Check(
            game.Haul == null && village.IsBaked(0) && game.Coins == coins + Game.HouseTaskCoins,
            "the bread is delivered: the resident pays and the house is marked"
        );
        Check(!village.IsBaked(1), "no other house is marked");
        roundTrip("bread delivered");
        Check(village.IsBaked(0), "the delivery survives a save");
        game.FinishInteraction();
        car.SetPose(owner.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(owner);
        Check(game.State == GameState.HouseReward, "a house that has its bread only offers thanks");
        game.FinishInteraction();

        // A house that wants bread from an empty bakery sends the player to the mill for flour.
        data = game.CaptureProgress();
        data.Village.Baked = new bool[4];
        data.Task = new TaskSave();
        data.State = GameState.Exploring;
        data.Nearby = -1;
        data.Buildings.BreadStock = 0;
        data.Buildings.FlourStock = 2;
        game.RestoreProgress(data);
        car.SetPose(owner.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(owner);
        Check(
            game.State == GameState.HouseMaterialOffer && Has("StartMissingSupply") && !Has("StartHouseTrip"),
            "a house that wants bread from an empty bakery shows what is missing"
        );
        game.BeginMissingSupply();
        ChallengeChecks.Solve(game);
        Check(
            game.Haul is { Load: HaulLoad.Flour } fetch && fetch.Works == works,
            "the resident sends the trailer to the mill for flour"
        );
        roundTrip("the resident's trip for flour");
        game.FinishInteraction();
        data = game.CaptureProgress();
        data.Task = new TaskSave();
        data.State = GameState.Exploring;
        data.Nearby = -1;
        data.Buildings.BreadStock = Works.Capacity;
        game.RestoreProgress(data);
    }
}
