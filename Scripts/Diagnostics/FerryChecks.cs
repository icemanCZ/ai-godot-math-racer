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
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The ferry to the bird island (<c>docs/FERRY_PLAN.md</c>): the heron's landing beside the homestead of the river
/// border, its pier to the water and the island before the mouth of the border river with the reeds round it; five
/// trailer trips build the landing and the punt; a question sends the car down the river on the punt, it drives about the
/// island and nowhere else beyond the valley; through the telescope a bird kept in the middle is written into the atlas
/// with its coins, the full atlas brings the kingfisher; the punt takes the car home; a save made on the punt or the
/// island puts the car back at the landing, a save from before the ferry loads, and the damaged saves are turned down.
/// </summary>
public partial class FerryChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public FerryChecks(Game game, World world, Vehicle car)
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

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("ferry-checks/progress.json");
            var trailer = game.Trailer;
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                foreach (var rule in SaveRules.All.Where(rule => rule.Invalid(saved)))
                    GD.Print($"BROKEN RULE: {rule.Name}");
                Check(SaveRules.All.All(rule => !rule.Invalid(saved)), $"save accepted: {what}");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save read back: {what}");
                game.RestoreProgress(loaded!);
                string before = JsonSerializer.Serialize(saved),
                    after = JsonSerializer.Serialize(game.CaptureProgress());
                if (before != after)
                {
                    int at = 0;
                    while (at < Math.Min(before.Length, after.Length) && before[at] == after[at])
                        at++;
                    int from = Math.Max(0, at - 120);
                    GD.Print($"SAVED:    {before.Substring(from, Math.Min(240, before.Length - from))}");
                    GD.Print($"RESTORED: {after.Substring(from, Math.Min(240, after.Length - from))}");
                }
                Check(before == after, $"the ferry restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"ferry-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Load(Action<SaveData> change)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"ferry-checks/changed-{files++}.json");
                Check(SaveStore.Write(data, file), "changed save written");
                var read = SaveStore.Read(file);
                Check(read != null, "changed save read back");
                game.RestoreProgress(read!);
            }
            void Near(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            // ---- The landing, the island and the reeds ----
            var site = world.FerryPlace;
            var route = world.FerryRoute;
            var landing = world.FerryLanding;
            var island = world.BirdIsland;
            var heron = world.Birdwatcher;
            Check(
                site != null && route != null && landing != null && island != null && heron != null,
                "the valley has the heron's landing, the way of the punt and the bird island"
            );
            Check(heron!.Role is BirdwatcherRole && landing!.Heron == heron, "the heron keeps the landing");
            Check(world.FerryBuildable, "the log yard and the sawmill are there to build it");
            Check(
                World.InsideValley(site!.Landing) && World.InsideValley(site.Talk) && World.InsideValley(site.Drop),
                "the landing, the heron and the place of the loads are inside the valley"
            );
            Check(!World.InsideValley(site.Berth), "the punt is moored on the border river, beyond the edge");
            Check(
                site.Berth.Y >= world.Ground.Height(site.Berth.X, site.Berth.Z) + .5f,
                "the water at the berth is deep enough for the punt"
            );
            Check(
                !World.InsideValley(site.Island) && site.Island.Z > ValleyBounds.HalfSize,
                $"the island {site.Island} lies beyond the edge, before the sea coast"
            );
            Check(
                Flat(route!.Samples[0], site.Berth) < 3 && Flat(route.Samples[^1], site.IslandBerth) < 3,
                $"the way of the punt ({route.Length:0} m) runs from the landing's pier to the island's pier"
            );
            Check(route.Duration >= 20 && route.Duration <= 40, $"the crossing takes {route.Duration:0} seconds");
            Check(
                route.Samples.All(p => !World.InsideValley(p)),
                "the punt sails beyond the edge of the valley all the way"
            );
            Check(world.ReedSpots.Count >= FerryPlans.PerVisit * 2, $"{world.ReedSpots.Count} clumps of reeds");
            foreach (var spot in world.ReedSpots)
                Check(
                    !World.InsideValley(spot.Position) && !world.OnIsland(spot.Position),
                    $"the reeds {spot.Number} grow in the water round the island"
                );
            var first = world.BirdsOfVisit(1);
            Check(
                first.Length == FerryPlans.PerVisit
                    && first.Select(b => b.Kind).Distinct().Count() == first.Length
                    && first.Select(b => b.Spot).Distinct().Count() == first.Length
                    && first
                        .Select(b => (b.Kind, b.Spot))
                        .SequenceEqual(world.BirdsOfVisit(1).Select(b => (b.Kind, b.Spot))),
                "four different birds hide in four different reeds, the same in every game"
            );
            Check(
                Enumerable
                    .Range(1, 6)
                    .Select(v => world.BirdsOfVisit(v))
                    .Any(birds =>
                        !birds.Select(b => (b.Kind, b.Spot)).SequenceEqual(first.Select(b => (b.Kind, b.Spot)))
                    ),
                "another crossing brings other birds"
            );
            var kingfisher = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Kingfisher);
            Check(
                kingfisher != null && !kingfisher.IsOwned && kingfisher.Collector == heron,
                "the kingfisher waits by the heron"
            );
            Check(
                world.SeaDepth(site.Island) == float.PositiveInfinity,
                "before the punt lands the car may not drive on the island"
            );

            // ---- Five loads build the landing and the punt ----
            Check(landing.Stage == 0 && !landing.Complete, "at first only the sign and the wreck stand at the landing");
            Frames(1);
            Check(heron.Guided, "the heron shows her mark");
            Rejected(data => data.Ferry = null!, "no section of the ferry");
            Rejected(data => data.Ferry.Stage = -1, "a landing with a negative stage");
            Rejected(data => data.Ferry.Stage = FerryPlans.Trips + 1, "a landing with six loads");
            Rejected(data => data.Ferry.Visit = 1, "a crossing before the punt is built");
            Rejected(
                data =>
                {
                    data.State = GameState.FerryStatus;
                    data.Nearby = World.HunterId;
                },
                "the heron's panel at the hunter"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Ferry, Index = 1 },
                "a trip for a second landing"
            );
            int coinsBefore = game.Coins;
            for (int step = 0; step < FerryPlans.Trips; step++)
            {
                bool save = step is 0 or 3;
                var cargo = FerryPlans.CargoOf(step);
                foreach (var works in world.AllWorks)
                    works.SetStock(Works.Capacity);
                Near(site.Talk);
                game.OpenBirdwatcher(heron);
                Check(
                    game.State == GameState.FerryStatus && Has("StartFerryTrip") && Has("FerryLoads"),
                    $"the heron offers load {step + 1} of the landing"
                );
                if (save)
                    RoundTrip("the heron's panel");
                game.BeginFerryStart();
                Check(game.State == GameState.Challenge, "the trip asks its quiz first");
                if (save)
                    RoundTrip("the quiz before a ferry trip");
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Ferry == landing && trip.Step == step && trailer.Attached,
                    $"load {step + 1} sends the empty trailer"
                );
                Check(trip!.Load == cargo, $"the trailer goes for {cargo}");
                Frames(1);
                Check(!heron.Guided, "the heron's mark is gone while the trailer is away");
                if (save)
                    RoundTrip($"a trip for the landing ({step + 1})");
                var source = game.Haul!.Source;
                Near(source.Position);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the loading question");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (save)
                    RoundTrip($"a full trailer for the landing ({step + 1})");
                Near(site.Drop);
                Frames(1);
                Check(trailer.Dumping, "the bed tips beside the landing");
                Frames(40);
                Check(landing.Stage == step + 1, $"load {step + 1} builds the {FerryPlans.Parts[step]}");
                Check(game.Haul == null && !game.TaskActive, "the next load waits for the player");
                if (step == 2)
                {
                    // Without planks the heron shows what the sawmill needs and sends the player for the logs.
                    foreach (var works in world.AllWorks)
                        works.SetStock(0);
                    Near(site.Talk);
                    game.OpenBirdwatcher(heron);
                    Check(
                        !Has("StartFerryTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                        "the heron shows what is missing when there are no planks"
                    );
                    game.BeginMissingSupply();
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Loaded: false, Works: not null },
                        "the heron sends the trailer for the raw material"
                    );
                    var noTrip = game.CaptureProgress();
                    noTrip.Task.Trip = null;
                    game.RestoreProgress(noTrip);
                }
                if (step < FerryPlans.Trips - 1)
                    Check(game.Coins == coinsBefore, "no coins before the punt is finished");
            }
            Check(landing.Complete, "the fifth load finishes the punt");
            Check(game.Coins == coinsBefore + FerryPlans.Coins, "the finished punt pays its coins");
            Frames(1);
            Check(heron.Guided, "the heron shows her mark again for the crossing");
            RoundTrip("a finished landing");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Ferry, Index = 0 },
                "a trip to a landing that has no load left"
            );

            // ---- The crossing ----
            void Sail(string what)
            {
                Near(site.Talk);
                game.OpenBirdwatcher(heron);
                Check(
                    game.State == GameState.FerryStatus && Has("FerrySail") && Has("Atlas"),
                    $"{what}: the atlas and the crossing"
                );
                game.BeginFerryStart();
                Check(game.State == GameState.Challenge, $"{what}: the crossing asks its question first");
                ChallengeChecks.Solve(game);
                Check(game.FerrySailing && !car.Active, $"{what}: the punt casts off with the car aboard");
            }
            void Arrive(string what)
            {
                for (int i = 0; i < 500 && game.FerrySailing; i++)
                    Frames(1);
                Check(!game.FerrySailing, $"{what}: the punt lands");
            }
            Sail("the first crossing");
            Check(game.Ferry.Visit == 1 && game.Ferry.Spotted.Length == 0, "the first crossing");
            var start = car.Position;
            Frames(50);
            Check(
                Flat(car.Position, start) > 20 && Flat(car.Position, landing.Punt.Position) < 3,
                "the car rides along on the punt"
            );
            Check(
                landing.Punt.Position.Y > world.Ground.Height(landing.Punt.Position.X, landing.Punt.Position.Z),
                "the punt floats above the river bed"
            );
            Check(Flat(heron.Position, landing.Punt.Position) < 4, "the heron poles at the stern");
            Frames(1);
            Check(!heron.Guided, "no mark over the heron while she poles");
            Arrive("to the island");
            Check(game.OnBirdIsland && car.Active, "on the island the car drives off the punt");
            Check(world.OnIsland(car.Position), $"the car stands on the island ({car.Position})");
            Check(world.SeaDepth(car.Position) < 0, "the car may drive about the island");
            Check(
                world.SeaDepth(site.Island + new Vector3(FerrySite.IslandHalfWidth + 12, 0, 0)) > 0
                    && world.SeaDepth(site.Island + new Vector3(0, 0, FerrySite.IslandHalfLength + 12)) > 0,
                "and not off it into the sea"
            );
            Check(Flat(heron.Position, site.IslandTalk) < 1, "the heron waits by the hide");

            // A save on the island keeps the atlas and puts the car at the landing.
            {
                var saved = game.CaptureProgress();
                var at = new Vector3(saved.Vehicle.Position[0], saved.Vehicle.Position[1], saved.Vehicle.Position[2]);
                Check(
                    Flat(at, site.Return) < 1 && saved.Ferry.Visit == 1,
                    "a save on the island keeps the crossing and puts the car at the landing"
                );
            }

            // ---- The telescope ----
            car.SetPose(site.IslandLanding + Vector3.Up * .3f, Mathf.Pi);
            game.OpenBirdwatcher(heron);
            Check(
                game.State == GameState.FerryStatus && Has("Telescope") && Has("FerryHome") && Has("IslandBirds"),
                "on the island the heron offers the telescope and the punt home"
            );
            game.BeginTelescope();
            Check(game.TelescopeOpen && !car.Active, "the telescope opens");
            Frames(1);
            Check(game.TelescopeHudShown, "the round view of the telescope shows");
            var birds = world.BirdsOfVisit(1);
            Check(island!.Birds.Count == birds.Length, "the birds of the crossing sit in their reeds");
            int seen = 0;
            foreach (var bird in birds)
            {
                string name = FerryPlans.Name(bird.Kind);
                var head = island.HeadOf(bird.Number);
                Check(!World.InsideValley(head), $"the {name} sits beyond the edge, by the island");
                game.AimTelescope(head + Vector3.Up * 30);
                Frames(10);
                Check(!game.Ferry.IsSpotted(bird.Number), $"the {name} is not seen while the telescope looks past it");
                int coins = game.Coins;
                game.AimTelescope(head);
                Frames(3);
                Check(!game.Ferry.IsSpotted(bird.Number), $"the {name} is not seen at a glance");
                Frames(5);
                seen++;
                Check(game.Ferry.IsSpotted(bird.Number), $"the {name} kept in the middle is seen");
                Check(
                    game.Coins == coins + FerryPlans.CoinsPerBird + FerryPlans.NewKindCoins
                        && game.Ferry.Kinds[bird.Kind] == 1
                        && game.Ferry.KindsFound == seen,
                    $"the {name} goes into the atlas, and the first of a kind pays more"
                );
                Frames(3);
                Check(game.Ferry.Spotted.Length == seen, "a bird is written into the atlas only once");
            }
            Check(!kingfisher!.IsOwned, "four kinds do not bring the kingfisher");
            Check(game.TelescopeOpen, "the telescope stays open a moment after the last bird");
            Frames(35);
            Check(!game.TelescopeOpen && !game.TelescopeHudShown && car.Active, "then the car is back");

            // ---- Home again ----
            game.OpenBirdwatcher(heron);
            game.BeginFerryHome();
            Check(game.FerrySailing && !game.OnBirdIsland, "the punt takes the car home without a question");
            Arrive("home");
            Check(
                !game.OnBirdIsland && World.InsideValley(car.Position) && Flat(car.Position, site.Return) < 1,
                "at home the car stands by the landing again"
            );
            Check(Flat(heron.Position, site.Talk) < 1, "and the heron by her landing");
            Check(world.SeaDepth(site.Island) == float.PositiveInfinity, "the island is out of reach again");
            RoundTrip("home after the first crossing");

            // ---- A save made on the punt ----
            Sail("the second crossing");
            Check(
                game.Ferry.Visit == 2 && game.Ferry.Spotted.Length == 0 && game.Ferry.KindsFound == FerryPlans.PerVisit,
                "the second crossing has new birds, the atlas keeps the old"
            );
            Frames(30);
            RoundTrip("a save in the middle of the crossing");
            Check(
                !game.FerrySailing && !game.OnBirdIsland && Flat(car.Position, site.Return) < 1,
                "a save made on the punt loads with the car at the landing"
            );
            Check(Flat(landing.Punt.Position, site.Berth) < 3, "and the punt moored at home");

            // ---- Saves that are turned down ----
            Rejected(data => data.Ferry.Spotted = null!, "no list of the birds seen");
            Rejected(data => data.Ferry.Kinds = null!, "no list of the kinds");
            Rejected(data => data.Ferry.Visit = -1, "a crossing before the first");
            Rejected(data => data.Ferry.Spotted = new[] { FerryPlans.PerVisit }, "a bird the crossing did not bring");
            Rejected(data => data.Ferry.Spotted = new[] { 1, 1 }, "a bird seen twice");
            Rejected(data => data.Ferry.Kinds = new int[3], "the wrong number of kinds");
            Rejected(data => data.Ferry.Kinds = new[] { 1, -1, 0, 0, 0, 0, 0, 0 }, "fewer than no birds of a kind");
            Rejected(
                data =>
                {
                    data.Ferry.Visit = 0;
                    data.Ferry.Spotted = new[] { 0 };
                },
                "a bird seen before the first crossing"
            );
            // A save from before the ferry: the section is not in the file, and nothing is built or seen.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Ferry");
                string file = DiagnosticArtifacts.PathFor("ferry-checks/before-the-ferry.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read is { Ferry: { Stage: 0, Visit: 0, Spotted.Length: 0, Kinds.Length: FerryPlans.Kinds } },
                    "a save from before the ferry loads"
                );
                game.RestoreProgress(read!);
                Check(landing.Stage == 0 && game.Ferry.KindsFound == 0, "and the landing waits to be built");
            }

            // ---- The last kind and the kingfisher ----
            int missing = FerryPlans.Flamingo;
            int visit = Enumerable.Range(1, 400).First(v => world.BirdsOfVisit(v).Any(b => b.Kind == missing));
            Load(data =>
            {
                data.Ferry.Stage = FerryPlans.Trips;
                data.Ferry.Visit = visit - 1;
                data.Ferry.Spotted = Array.Empty<int>();
                data.Ferry.Kinds = Enumerable.Range(0, FerryPlans.Kinds).Select(k => k == missing ? 0 : 2).ToArray();
            });
            Check(
                landing.Complete && game.Ferry.KindsFound == 7 && !kingfisher.IsOwned,
                "seven kinds, no kingfisher yet"
            );
            Sail("the crossing of the flamingo");
            Arrive("to the island again");
            car.SetPose(site.IslandLanding + Vector3.Up * .3f, Mathf.Pi);
            game.OpenBirdwatcher(heron);
            game.BeginTelescope();
            Frames(1);
            var flamingo = world.BirdsOfVisit(visit).First(b => b.Kind == missing);
            int before = game.Coins;
            game.AimTelescope(island.HeadOf(flamingo.Number));
            Frames(8);
            Check(
                game.Coins == before + FerryPlans.CoinsPerBird + FerryPlans.NewKindCoins + FerryPlans.AtlasCoins,
                "the last kind pays a bonus"
            );
            Check(game.Ferry.Complete && kingfisher.IsOwned, "the full atlas brings the kingfisher");
            Frames(1);
            Check(!heron.Guided, "with the full atlas the heron shows no mark");
            RoundTrip("the full atlas");
            Check(game.Ferry.Complete && !game.TelescopeOpen && !game.OnBirdIsland, "the full atlas in a save");
            GD.Print("FERRY_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
