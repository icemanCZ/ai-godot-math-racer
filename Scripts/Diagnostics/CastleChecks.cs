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
/// The castle on the hill (<c>docs/CASTLE_PLAN.md</c>): it stands beside a road beyond the old valley on dry, nearly
/// level ground far from the village, clear of the plants; sixteen trailer trips raise it, the stone loaded by the sea and
/// in the mountains on the princess's question; then a load of logs puts up a beacon by each homestead of the borders, and
/// the trailer fetches the guests of the feast from their homes, each asking a question before getting on its bench; a
/// full table brings the feast, its coins, the fireworks and the ghost. Every step survives a save, a save from before the
/// castle loads, and the damaged saves are turned down.
/// </summary>
public partial class CastleChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public CastleChecks(Game game, World world, Vehicle car)
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
            string path = DiagnosticArtifacts.PathFor("castle-checks/progress.json");
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
                Check(before == after, $"the castle restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"castle-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Near(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            // ---- The place of the castle ----
            var site = world.CastlePlace;
            var castle = world.Castle;
            var princess = world.Castellan;
            Check(site != null && castle != null && princess != null, "the valley has the castle and its princess");
            Check(princess!.Role is CastleRole && castle!.Princess == princess, "the princess keeps the castle");
            Check(world.CastleBuildable && world.BeaconsBuildable, "the works and the log yard are there to build it");
            Check(
                Flat(site!.Center, world.Village.Center) >= 480,
                $"the castle stands {Flat(site.Center, world.Village.Center):0} m from the village"
            );
            Check(
                World.InsideValley(site.Center) && World.InsideValley(site.Gate) && World.InsideValley(site.Drop),
                "the castle, its gate and the place of the loads are inside the valley"
            );
            float low = float.MaxValue,
                high = float.MinValue;
            for (float along = CastleSite.FrontAlong; along <= CastleSite.BackAlong; along += 4)
            for (float across = -CastleSite.HalfWidth; across <= CastleSite.HalfWidth; across += 4)
            {
                var p = site.Local(across, along);
                float h = world.Ground.Height(p.X, p.Z);
                (low, high) = (Mathf.Min(low, h), Mathf.Max(high, h));
                Check(
                    world.Plants.OutlandCleared?.Invoke(p) == true && !world.Grass.CanGrow(p),
                    $"no plant or grass grows inside the walls ({across:0}, {along:0})"
                );
            }
            Check(high - low <= 8.5f, $"the ground under the walls rises {high - low:0.0} m");
            Check(
                site.Center.Y > world.Ground.Height(world.Village.Center.X, world.Village.Center.Z),
                "the castle stands higher than the village"
            );
            Check(
                world.BeaconCount >= 3 && world.CastleGuestCount >= 5,
                $"{world.BeaconCount} beacons and {world.CastleGuestCount} guests"
            );
            foreach (var beacon in world.Beacons.Where(b => b != null))
                Check(
                    World.InsideValley(beacon!.Site.Foot)
                        && World.InsideValley(beacon.Site.Drop)
                        && world.Plants.OutlandCleared?.Invoke(beacon.Site.Foot) == true,
                    $"the beacon of the {beacon.Site.Border} border stands on cleared ground inside the valley"
                );
            var ghost = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.CastleGhost);
            Check(ghost != null && !ghost.IsOwned && ghost.Collector == princess, "the ghost waits by the princess");

            // ---- Sixteen loads raise the castle ----
            Check(castle.Stage == 0 && !castle.Complete, "at first only the stakes of the plan and the crane stand");
            Frames(1);
            Check(princess.Guided, "the princess shows her mark");
            Rejected(data => data.Castle = null!, "no section of the castle");
            Rejected(data => data.Castle.Stage = -1, "a castle with a negative stage");
            Rejected(data => data.Castle.Stage = CastlePlans.Trips + 1, "a castle with seventeen loads");
            Rejected(data => data.Castle.Beacons = new[] { 0 }, "a beacon before the castle stands");
            Rejected(data => data.Castle.Guests = new[] { 0 }, "a guest before the castle stands");
            Rejected(
                data =>
                {
                    data.State = GameState.CastleStatus;
                    data.Nearby = World.HunterId;
                },
                "the princess's panel at the hunter"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Castle, Index = 1 },
                "a trip for a second castle"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.CastleGuest, Index = 0 },
                "a guest's trip before the castle stands"
            );
            int coinsBefore = game.Coins;
            for (int step = 0; step < CastlePlans.Trips; step++)
            {
                bool save = step is 0 or 1 or 9;
                var cargo = CastlePlans.CargoOf(step);
                foreach (var works in world.AllWorks)
                    works.SetStock(Works.Capacity);
                Near(site.Talk);
                game.OpenCastellan(princess);
                Check(
                    game.State == GameState.CastleStatus && Has("StartCastleTrip") && Has("CastleLoads"),
                    $"the princess offers load {step + 1} of the castle"
                );
                if (save)
                    RoundTrip("the princess's panel");
                game.BeginCastleStart();
                Check(game.State == GameState.Challenge, "the trip asks its quiz first");
                if (save)
                    RoundTrip("the quiz before a castle trip");
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Castle == castle && trip.Step == step && trailer.Attached,
                    $"load {step + 1} sends the empty trailer"
                );
                Check(trip!.Load == cargo, $"the trailer goes for {cargo}");
                if (cargo == HaulLoad.Stones)
                {
                    var stone = castle.StoneSource(step)!.Value;
                    Check(
                        Flat(trip.Source.Position, stone.Position) < 1,
                        $"the stone of load {step + 1} is broken {(CastlePlans.StoneFromSea(step) ? "by the sea" : "in the mountains")}"
                    );
                    Check(trip.Asker == princess, "the princess asks the stone's loading question from afar");
                }
                Frames(1);
                Check(!princess.Guided, "the princess's mark is gone while the trailer is away");
                if (save)
                    RoundTrip($"a trip for the castle ({step + 1})");
                Near(game.Haul!.Source.Position);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the loading question");
                if (save)
                    RoundTrip($"the loading question of the castle ({step + 1})");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (save)
                    RoundTrip($"a full trailer for the castle ({step + 1})");
                Near(site.Drop);
                Frames(1);
                Check(trailer.Dumping, "the bed tips before the gate");
                Frames(40);
                Check(castle.Stage == step + 1, $"load {step + 1} builds the {CastlePlans.Parts[step]}");
                Check(game.Haul == null && !game.TaskActive, "the next load waits for the player");
                if (step == 5)
                {
                    // Without bricks the princess shows what the brickworks needs and sends the player for the clay.
                    foreach (var works in world.AllWorks)
                        works.SetStock(0);
                    Near(site.Talk);
                    game.OpenCastellan(princess);
                    Check(
                        !Has("StartCastleTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                        "the princess shows what is missing when there are no bricks"
                    );
                    game.BeginMissingSupply();
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Loaded: false, Works: not null },
                        "the princess sends the trailer for the raw material"
                    );
                    var noTrip = game.CaptureProgress();
                    noTrip.Task.Trip = null;
                    game.RestoreProgress(noTrip);
                }
                if (step < CastlePlans.Trips - 1)
                    Check(game.Coins == coinsBefore, "no coins before the castle is finished");
            }
            Check(castle.Complete, "the sixteenth load finishes the castle");
            Check(game.Coins == coinsBefore + CastlePlans.Coins, "the finished castle pays its coins");
            RoundTrip("a finished castle");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Castle, Index = 0 },
                "a trip to a castle that has no load left"
            );

            // ---- The drawbridge and the lights ----
            var far = site.Local(0, -60);
            car.SetPose(world.Ground.At(far.X, far.Z, .15f), site.Yaw);
            Frames(80);
            Check(castle.BridgeDown < .05f, "the drawbridge is up while the car is far away");
            Near(site.Drop);
            Frames(40);
            Check(castle.BridgeDown > .95f, "the drawbridge comes down when the car drives up to the gate");

            // ---- The beacons ----
            Frames(1);
            Check(princess.Guided, "the princess shows her mark for the beacons");
            int beacons = 0;
            while (world.Beacons.FirstOrDefault(b => b is { Built: false }) is { } beacon)
            {
                Near(site.Talk);
                game.OpenCastellan(princess);
                Check(
                    game.State == GameState.CastleStatus && Has("StartCastleTrip") && Has("CastleBeacons"),
                    $"the princess sends logs for the beacon of the {beacon.Site.Border} border"
                );
                game.BeginCastleStart();
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: false, Beacon: not null } && game.Haul.Beacon == beacon,
                    "the trailer goes for the beacon's logs"
                );
                if (beacons == 0)
                    RoundTrip("a trip for a beacon");
                Near(game.Haul!.Source.Position);
                Frames(1);
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == HaulLoad.Logs, "the logs are loaded");
                Near(beacon.Site.Drop);
                Frames(1);
                Check(trailer.Dumping, "the bed tips beside the beacon");
                Frames(40);
                Check(beacon.Built && game.Haul == null, $"the beacon of the {beacon.Site.Border} border stands");
                beacons++;
                if (beacons == 1)
                {
                    RoundTrip("one beacon lit");
                    Rejected(
                        data =>
                            data.Task.Trip = new SavedTrip
                            {
                                Kind = TripKinds.CastleBeacon,
                                Index = (int)beacon.Site.Border,
                            },
                        "a trip for a beacon that burns already"
                    );
                    Rejected(data => data.Castle.Beacons = new[] { 0, 0 }, "a beacon lit twice");
                    Rejected(data => data.Castle.Beacons = new[] { 4 }, "a beacon of a fifth border");
                }
            }
            Check(beacons == world.BeaconCount, $"{beacons} beacons burn");
            Check(
                game.Coins == coinsBefore + CastlePlans.Coins + beacons * CastlePlans.BeaconCoins,
                "each beacon pays"
            );
            Frames(1);
            Check(castle.FireBurning, "with every beacon lit the great fire burns on the keep");

            // ---- The guests of the feast ----
            int seated = 0;
            for (int chair = 0; chair < CastlePlans.Guests; chair++)
            {
                if (world.CastleGuests[chair] is not { } guest)
                    continue;
                Near(site.Talk);
                game.OpenCastellan(princess);
                Check(
                    game.State == GameState.CastleStatus && Has("StartCastleTrip") && Has("CastleGuests"),
                    $"the princess sends the trailer for {guest.DisplayName}"
                );
                game.BeginCastleStart();
                ChallengeChecks.Solve(game);
                Check(
                    game.Haul is { Loaded: false, Guest: not null } && game.Haul.Guest.Guest == chair,
                    $"the empty trailer goes to the home of {guest.DisplayName}"
                );
                Check(game.Haul!.Asker == guest, "the guest asks the question at home");
                Near(game.Haul.Source.Position);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the guest's question");
                RoundTrip($"the question of the guest {guest.DisplayName}");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == HaulLoad.Guest, "the guest gets on the bench");
                Frames(1);
                Check(trailer.Passenger != null, "the guest's figure rides on the trailer");
                if (seated == 0)
                    RoundTrip("a guest on the trailer");
                Frames(1);
                Check(trailer.Passenger != null, "the guest rides on after a save");
                Check(guest.Visible, $"{guest.DisplayName} stays at home as well");
                Near(site.Drop);
                Frames(1);
                Check(trailer.Dumping, "the trailer stops before the gate");
                Frames(40);
                Check(castle.Seated(chair) && trailer.Passenger == null, $"{guest.DisplayName} sits at the table");
                seated++;
                if (seated == 1)
                {
                    RoundTrip("one guest at the table");
                    Rejected(
                        data => data.Task.Trip = new SavedTrip { Kind = TripKinds.CastleGuest, Index = chair },
                        "a trip for a guest who sits at the table already"
                    );
                    Rejected(data => data.Castle.Guests = new[] { chair, chair }, "a guest seated twice");
                    Rejected(data => data.Castle.Guests = new[] { CastlePlans.Guests }, "an eighth chair");
                }
                if (seated < world.CastleGuestCount)
                    Check(!ghost.IsOwned && !castle.Fireworks, "no feast while a chair is free");
            }
            Check(seated == world.CastleGuestCount, $"{seated} guests sit at the table");
            Check(castle.Fireworks && ghost.IsOwned, "the full table holds the feast and brings the ghost");
            Check(
                game.Coins
                    == coinsBefore
                        + CastlePlans.Coins
                        + beacons * CastlePlans.BeaconCoins
                        + seated * CastlePlans.GuestCoins
                        + CastlePlans.FeastCoins,
                "the guests and the feast pay their coins"
            );
            Frames(1);
            Check(!princess.Guided, "after the feast the princess shows no mark");
            RoundTrip("the feast held");
            Check(
                castle.Fireworks && ghost.IsOwned && castle.Seated(0) == (world.CastleGuests[0] != null),
                "the feast in a save"
            );
            Rejected(
                data =>
                {
                    data.Castle.Feast = true;
                    data.Castle.Guests = Array.Empty<int>();
                },
                "a feast without guests"
            );
            Rejected(data => data.Castle.Guests = null!, "no list of the guests");
            Rejected(data => data.Castle.Beacons = null!, "no list of the beacons");

            // A save from before the castle: the section is not in the file, and nothing is built.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Castle");
                string file = DiagnosticArtifacts.PathFor("castle-checks/before-the-castle.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read is { Castle: { Stage: 0, Beacons.Length: 0, Guests.Length: 0, Feast: false } },
                    "a save from before the castle loads"
                );
                game.RestoreProgress(read!);
                Check(
                    castle.Stage == 0 && !castle.Fireworks && world.Beacons.All(b => b is not { Built: true }),
                    "and the castle waits to be built"
                );
                Check(Enumerable.Range(0, CastlePlans.Guests).All(c => !castle.Seated(c)), "with nobody at the table");
            }
            GD.Print("CASTLE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
