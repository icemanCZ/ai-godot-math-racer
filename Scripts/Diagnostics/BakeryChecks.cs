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
/// The fifth chain: the wheat field with its farmer behind the village, the wheat hauled to the bakery, the bread
/// that the bakery bakes and the order for bread that follows the housewarming of a house.
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
                    && bakery.Spec.Input == HaulLoad.Wheat,
                "the baker has an ID of his own and bakes bread from wheat"
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

            // ---- Hauling wheat to the bakery ----
            var trailer = game.Trailer;
            Check(bakery.Stock == 0 && bakery.ShownUnits == 0, "the bakery starts with an empty yard");
            void Supply(int expected)
            {
                car.SetPose(world.Ground.At(farmer.Position.X + 2, farmer.Position.Z, .15f), 0);
                game.OpenSource(farmer);
                Check(
                    game.State == GameState.SourceStatus && Has("StartSupplyTrip"),
                    "the farmer offers the next load"
                );
                game.BeginSupplyTrip();
                var trip = game.Haul;
                Check(trip is { Loaded: false } && trip.Load == HaulLoad.Wheat, "the trailer is sent for wheat");
                Check(trip!.Target.Index == WorksCatalog.BreadIndex, "the wheat is meant for the bakery");
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
                var ring = bakery.Site.Position;
                car.SetPose(world.Ground.At(ring.X, ring.Z, .15f), 0);
                Frames(1);
                Frames(40);
                Check(
                    bakery.Stock == expected && bakery.ShownUnits == expected && game.Haul == null,
                    $"the bakery shows {expected} loaves"
                );
                RoundTrip($"after wheat delivery {expected}");
            }
            for (int load = 1; load <= Works.Capacity / Works.PerLoad; load++)
                Supply(load * Works.PerLoad);
            Check(
                bakery.Full && world.Tally.Wheat == 6 && world.Tally.Baked == 6,
                "six loads of wheat fill the bakery and are counted"
            );
            car.SetPose(world.Ground.At(farmer.Position.X + 2, farmer.Position.Z, .15f), 0);
            game.OpenSource(farmer);
            Check(!Has("StartSupplyTrip"), "a full bakery asks for no more wheat");
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

            // ---- Damaged saves ----
            Rejected(data => data.Buildings.BreadStock = Works.Capacity + 1, "more bread than the yard holds");
            Rejected(data => data.Buildings.BreadStock = -1, "negative bread");
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
            Rejected(data => data.Nearby = World.MayorId + 1, "a character beyond the last actor");

            GD.Print("BAKERY_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
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
    }
}
