using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The pets that hauling and building earn: one for each raw material (ten loads hauled), each works (ten loads taken
/// in, twenty pieces made) and each finished building, and the glassworks with the glasses for the residents. They wait
/// beside their workers, are earned by real trips, show in the garage and survive a save, including one that was made
/// before the counts existed.
/// </summary>
internal sealed partial class WorldPetChecks(Game game, World world, Vehicle car) : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private Button PetButton(CollectorPet pet) => (Button)game.FindChild($"GaragePet_{pet.Kind}", true, false);

    private CollectorPet Pet(CollectorPetKind kind) => world.WorldPets.First(p => p.Kind == kind);

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("world-pet-checks/progress.json");
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), $"save written: {what}");
                var loaded = SaveStore.Read(path) ?? throw new InvalidOperationException($"save refused: {what}");
                game.RestoreProgress(loaded);
                var before = JsonSerializer.SerializeToNode(saved)!.AsObject();
                var after = JsonSerializer.SerializeToNode(game.CaptureProgress())!.AsObject();
                foreach (var (key, value) in before)
                    if (!JsonNode.DeepEquals(value, after[key]))
                        GD.Print($"DIFFERENT {what}: {key} was {value} now {after[key]}");
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"restoring and capturing again gives the same save: {what}"
                );
            }

            // ---- The pets wait beside their workers ----
            var pets = world.WorldPets;
            var kinds = pets.Select(p => p.Kind).ToList();
            Check(kinds.Distinct().Count() == kinds.Count, "every pet of the world exists once");
            foreach (
                var kind in new[]
                {
                    CollectorPetKind.Duck,
                    CollectorPetKind.Logs,
                    CollectorPetKind.Stones,
                    CollectorPetKind.Soil,
                    CollectorPetKind.Sawmill,
                    CollectorPetKind.Brickworks,
                    CollectorPetKind.GravelWorks,
                    CollectorPetKind.Bridge,
                    CollectorPetKind.Mill,
                    CollectorPetKind.Tower,
                }
            )
                Check(kinds.Contains(kind), $"the valley has the {kind} pet");
            // Every valley with a ford has the glassworks: in the town beside the other works, with a path to it, and no
            // plant is left standing on it.
            var glassworks = world.Glassworks ?? throw new InvalidOperationException("The valley has no glassworks.");
            Check(
                world.Village.PlotDistance(glassworks.Site.Position) < 110,
                "the glassworks stands in the town, near the other works"
            );
            Check(
                world.Tracks!.Contains(glassworks.Site.Road.Lerp(glassworks.Site.Position, .5f))
                    && world.Tracks.Contains(glassworks.Site.Position),
                "a path leads to the glassworks like to the other works"
            );
            var site = glassworks.Site;
            bool treeFree = true;
            for (float along = -6; along <= 22 && treeFree; along += 2)
            for (float across = -14; across <= 14 && treeFree; across += 2)
                treeFree = !world.Plants.NearTrunk(
                    site.Position + site.Inward * along + Vector3.Up.Cross(site.Inward) * across,
                    1.5f
                );
            for (float t = 0; t <= 1 && treeFree; t += .05f)
                treeFree = !world.Plants.NearTrunk(site.Road.Lerp(site.Position, t), 2f);
            Check(treeFree, "no tree stands on the glassworks or on the way to it");
            bool glass = true;
            bool sand = world.ExtraSourceWorker(HaulLoad.Sand) != null;
            Check(kinds.Contains(CollectorPetKind.Glassworks) == glass, "the glassworks has a pet where it stands");
            Check(kinds.Contains(CollectorPetKind.Sand) == sand, "the sand pit has a pet where it has a worker");
            GD.Print($"GLASSWORKS present={glass} sand-pet={sand}");
            Check(
                pets.All(p =>
                    p.Visible && !p.IsOwned && !p.IsActive && p.Position.DistanceTo(p.Collector.Position) < 4
                ),
                "every pet waits beside its worker or builder, unowned"
            );
            Check(
                pets.Select(p => p.Position).Distinct().Count() == pets.Count
                    && world.AllPets.Count == world.Village.CollectorPets.Count + pets.Count,
                "the pets stand apart and the garage lists them all"
            );
            Check(
                pets.Where(p => p.Kind != CollectorPetKind.Duck).Select(p => p.TrailOffset).Distinct().Count()
                    == pets.Count - 1,
                "every pet has a place of its own in the trail"
            );

            // ---- Pets for raw materials and works, earned by ten real loads each ----
            void Supply(Works works)
            {
                var spec = works.Spec;
                works.SetStock(0);
                var asker = world.SourceWorker(spec.Input) ?? world.ExtraSourceWorker(spec.Input)!;
                car.SetPose(world.Ground.At(asker.Position.X + 2, asker.Position.Z, .15f), 0);
                game.OpenSource(asker);
                game.BeginSupplyTrip();
                Check(
                    game.Haul is { Loaded: false } trip && trip.Load == spec.Input,
                    $"{spec.Name}: a trip for {spec.Input}"
                );
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true }, $"{spec.Name}: loaded");
                var ring = works.Site.Position;
                car.SetPose(world.Ground.At(ring.X, ring.Z, .15f), 0);
                Frames(1);
                Frames(40);
                Check(game.Haul == null && works.Stock == Works.PerLoad, $"{spec.Name}: unloaded and made goods");
            }

            var raw = new (CollectorPetKind? Raw, int Works, HaulLoad Load)[]
            {
                (CollectorPetKind.Logs, (int)Product.Planks, HaulLoad.Logs),
                (CollectorPetKind.Stones, (int)Product.Gravel, HaulLoad.Stones),
                (CollectorPetKind.Soil, (int)Product.Bricks, HaulLoad.Soil),
                (sand ? CollectorPetKind.Sand : null, WorksCatalog.GlassIndex, HaulLoad.Sand),
            };
            foreach (var (rawKind, worksIndex, load) in raw)
            {
                var works = world.WorksByIndex(worksIndex);
                if (works == null)
                    continue;
                var worksPet = Pet(CollectorPetFor(worksIndex));
                var rawPet = rawKind is { } k ? Pet(k) : null;
                for (int i = 1; i <= WorldPetCatalog.SourceLoads; i++)
                {
                    Supply(works);
                    Check(
                        world.Tally.RawCount(load) == i && world.Tally.WorksCount(worksIndex) == i,
                        $"{load}: {i} loads counted for the source and for {works.Spec.Name}"
                    );
                    bool done = i == WorldPetCatalog.SourceLoads;
                    Check(
                        worksPet.IsOwned == done && (rawPet == null || rawPet.IsOwned == done),
                        $"{load}: the pets of the source and of the works are earned at load {WorldPetCatalog.SourceLoads}, not before"
                    );
                }
                Check(
                    worksPet.IsActive && worksPet.Visible && (rawPet == null || rawPet.IsActive),
                    $"{load}: the new pets are along"
                );
                RoundTrip($"{load} pets earned");
                Check(
                    worksPet.IsActive && world.Tally.RawCount(load) == WorldPetCatalog.SourceLoads,
                    $"{load}: the count and the pets survive a save"
                );
            }

            // ---- A save from before the counts has none, and its pets are not owned ----
            var current = game.CaptureProgress();
            var old = JsonSerializer.SerializeToNode(current)!.AsObject();
            old.Remove("Hauls");
            ((JsonObject)old["Buildings"]!).Remove("GlassStock");
            ((JsonObject)old["Village"]!).Remove("Glazed");
            string oldPath = DiagnosticArtifacts.PathFor("world-pet-checks/before-counts.json");
            File.WriteAllText(oldPath, old.ToJsonString());
            var before = SaveStore.Read(oldPath) ?? throw new InvalidOperationException("The older save was refused.");
            game.RestoreProgress(before);
            Check(
                world.Tally.RawCount(HaulLoad.Logs) == 0
                    && world.Tally.WorksCount(0) == 0
                    && !Pet(CollectorPetKind.Logs).IsOwned
                    && !Pet(CollectorPetKind.Sawmill).IsOwned,
                "a save from before the counts loads with no loads counted and no pet earned"
            );

            // ---- A save with a full stock that was never counted: the stock was made, so it counts, for every works ----
            foreach (var stocked in world.AllWorks.ToList())
            {
                int index = stocked.Site.Index;
                var input = stocked.Spec.Input;
                var madePet = Pet(WorldPetCatalog.ForWorks(index).Kind);
                var sourcePet = world.WorldPets.FirstOrDefault(p => p.Kind == WorldPetCatalog.ForSource(input).Kind);
                game.RestoreProgress(before);
                stocked.SetStock(Works.Capacity);
                int loads = Works.Capacity / Works.PerLoad;
                Check(
                    world.Tally.WorksCount(index) == 0
                        && world.Tally.RawCount(input) == 0
                        && madePet.Count == Works.Capacity
                        && !madePet.IsOwned
                        && (sourcePet == null || (sourcePet.Count == loads && !sourcePet.IsOwned)),
                    $"{stocked.Spec.Name}: a full stock counts as twelve made and six loads hauled, and the restore changes no saved count"
                );
                // A trip takes one piece and its load comes back: the stock is counted first and the new load comes on top.
                game.CountStockMade(stocked);
                stocked.Take(Works.PerTrip);
                game.CountStockMade(stocked);
                stocked.Deliver();
                world.Tally.AddRaw(input);
                world.Tally.AddWorks(index);
                Check(
                    world.Tally.WorksCount(index) == loads + 1 && world.Tally.RawCount(input) == loads + 1,
                    $"{stocked.Spec.Name}: the stock is counted once, before it changes, and the new load adds to it"
                );
                // Everything made counts, also what left the stock: the stock stays full while the counts grow.
                while (madePet.Count < WorldPetCatalog.WorksPieces)
                    world.Tally.AddWorks(index);
                while (sourcePet != null && sourcePet.Count < WorldPetCatalog.SourceLoads)
                    world.Tally.AddRaw(input);
                Check(
                    stocked.Stock == Works.Capacity && madePet.IsOwned && (sourcePet == null || sourcePet.IsOwned),
                    $"{stocked.Spec.Name}: the pets are earned by what was made and hauled in all, not by the stock"
                );
                RoundTrip($"{stocked.Spec.Name} counted from the stock");
                Check(
                    madePet.IsOwned && (sourcePet == null || sourcePet.IsOwned),
                    $"{stocked.Spec.Name}: the counts and the pets survive a save"
                );
            }

            // ---- Pets for the finished bridge, mill and tower ----
            foreach (
                var (kind, project) in new[]
                {
                    (CollectorPetKind.Bridge, BuildPlans.Bridge),
                    (CollectorPetKind.Mill, BuildPlans.Mill),
                    (CollectorPetKind.Tower, BuildPlans.Tower),
                }
            )
            {
                var pet = Pet(kind);
                world.Projects[project].SetStage(BuildPlans.Trips - 1);
                Check(!pet.IsOwned, $"{kind}: not owned before the last delivery");
                world.Projects[project].SetStage(BuildPlans.Trips);
                Check(pet.IsOwned && pet.Goal == BuildPlans.Trips, $"{kind}: owned once the building is finished");
            }
            RoundTrip("finished buildings");
            Check(
                Pet(CollectorPetKind.Bridge).IsActive && Pet(CollectorPetKind.Mill).IsActive,
                "the building pets come along after a reload"
            );

            // ---- The garage shows and switches every pet ----
            car.SetPose(world.Village.GarageEntrance, 0);
            game.OpenGarage();
            foreach (var pet in pets)
            {
                Check(PetButton(pet) != null, $"{pet.Kind}: has a garage button");
                Check(
                    PetButton(pet).Disabled == !pet.IsOwned && PetButton(pet).TooltipText.Contains(pet.DisplayName),
                    $"{pet.Kind}: the button is open exactly when the pet is owned"
                );
            }
            var mill = Pet(CollectorPetKind.Mill);
            PetButton(mill).EmitSignal(Button.SignalName.Pressed);
            Check(mill.IsOwned && !mill.IsActive && !mill.Visible, "the garage button sends the mill pet home");
            game.FinishInteraction();
            RoundTrip("a pet switched off");
            Check(!mill.IsActive && Pet(CollectorPetKind.Bridge).IsActive, "the choice of pets survives a reload");

            // ---- The pets trail the car in places of their own ----
            car.SetPose(world.Ground.At(0, 8, .15f), 0);
            for (int i = 0; i < 160; i++)
                game._Process(.05);
            var active = pets.Where(p => p.IsActive).ToList();
            Check(
                active.Count >= 2
                    && active.All(p => p.Position.DistanceTo(car.Position) < 40)
                    && active.Select(p => p.Position).Distinct().Count() == active.Count,
                "the earned pets follow the car and keep apart"
            );
            // Every pet strolls around the parked car, also those at the back of a long trail, far behind the car.
            var last = active.Select(p => p.Position).ToArray();
            var moved = new bool[active.Count];
            var rested = new bool[active.Count];
            for (int frame = 0; frame < 240; frame++)
            {
                game._Process(.05);
                for (int i = 0; i < active.Count; i++)
                {
                    float step = active[i].Position.DistanceTo(last[i]);
                    moved[i] |= step > .05f;
                    rested[i] |= step < .001f;
                    last[i] = active[i].Position;
                }
            }
            var idle = active.Where((_, i) => !moved[i] || !rested[i]).Select(p => p.Kind).ToList();
            Check(
                idle.Count == 0 && active.All(p => p.Position.DistanceTo(car.Position) < 20),
                $"every earned pet strolls and rests around the parked car (not: {string.Join(", ", idle)})"
            );

            if (glass)
                GlassOrder(RoundTrip);

            GD.Print("WORLD_PET_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private static CollectorPetKind CollectorPetFor(int worksIndex) => WorldPetCatalog.ForWorks(worksIndex).Kind;

    /// <summary>
    /// A finished house asks for glasses once: the trailer fetches one from the glassworks' stock, the resident pays
    /// and the order is saved, so that it is not asked again.
    /// </summary>
    private void GlassOrder(Action<string> roundTrip)
    {
        var village = world.Village;
        var works = world.Glassworks!;
        var owner = village.Residents[0];
        var data = game.CaptureProgress();
        data.Village.HouseLevels = new[] { 10, 0, 0, 0 };
        data.Village.Glazed = new bool[4];
        data.Task = new TaskSave();
        data.Quiz = new QuizSave { Types = data.Quiz.Types, ThreeTermMath = data.Quiz.ThreeTermMath };
        data.State = GameState.Exploring;
        data.Nearby = -1;
        data.Buildings.GlassStock = Works.Capacity;
        game.RestoreProgress(data);
        works.SetStock(Works.Capacity);
        Check(works.Stock == Works.Capacity && works.ShownUnits == Works.Capacity, "the glassworks shows its glasses");

        car.SetPose(owner.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(owner);
        Check(game.State == GameState.HouseMaterialOffer, "a finished house asks for glasses");
        Check(Has("StartHouseTrip"), "with glasses in stock the play button sends the trailer");
        roundTrip("glasses asked for");
        game.BeginHouseTrip();
        ChallengeChecks.Solve(game);
        var trip = game.Haul;
        Check(
            trip is { Loaded: false } && trip.House == 0 && trip.Load == HaulLoad.Glass && game.Trailer.Attached,
            "the empty trailer is sent to the glassworks"
        );
        Check(trip!.Asker == works.Worker && trip.Contractor == owner, "the glassworker loads them for the resident");
        roundTrip("trip for glasses");
        car.SetPose(world.Ground.At(works.Site.Position.X, works.Site.Position.Z, .15f), 0);
        Frames(1);
        Check(game.State == GameState.Challenge, "the loading question is asked at the glassworks");
        ChallengeChecks.Solve(game);
        Check(
            game.Haul is { Loaded: true }
                && game.Trailer.Load == HaulLoad.Glass
                && works.Stock == Works.Capacity - Works.PerTrip,
            "one glass is taken from the stock and loaded"
        );
        roundTrip("glasses loaded");
        int coins = game.Coins;
        var drop = village.HouseDrop(0);
        car.SetPose(world.Ground.At(drop.X, drop.Z, .15f), 0);
        Frames(1);
        Frames(40);
        Check(
            game.Haul == null && village.IsGlazed(0) && game.Coins == coins + Game.HouseTaskCoins,
            "the glasses are delivered: the resident pays and the house is marked"
        );
        Check(!village.IsGlazed(1), "no other house is marked");
        roundTrip("glasses delivered");
        Check(village.IsGlazed(0), "the delivery survives a save");
        game.FinishInteraction();
        car.SetPose(owner.Position + Vector3.Right * 2, 0);
        game.OpenChallenge(owner);
        // A valley with a bakery asks for bread next; the glasses are never asked for again.
        Check(
            game.State == (world.Bakery != null ? GameState.HouseMaterialOffer : GameState.HouseReward),
            "a house that has its glasses does not ask for glasses again"
        );
        game.FinishInteraction();
    }
}
