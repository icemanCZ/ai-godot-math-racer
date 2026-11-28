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
/// The shepherd's lost sheep (<c>docs/SHEEP_PLAN.md</c>): eight meadows round the whole valley between the ring road and
/// the borders, open, dry and well apart, each with a way the car can drive from a road; the shepherd shows his fold
/// and sends the player out from the start, with no letter; a sheep the car comes near follows it, keeps up with a
/// gentle drive, falls behind a fast one, waits with its beacon and joins again when the car comes back; a save keeps the
/// found sheep behind the car; at the shepherd a question sends them over the rail into the fold, which pays; the eighth
/// brings the sheepdog puppy; a save from before the sheep loads with none of it; and the damaged saves are turned down.
/// </summary>
public partial class SheepChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public SheepChecks(Game game, World world, Vehicle car)
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
            string path = DiagnosticArtifacts.PathFor("sheep-checks/progress.json");
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
                Check(before == after, $"the sheep restore: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"sheep-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Load(Action<SaveData> change)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"sheep-checks/changed-{files++}.json");
                Check(SaveStore.Write(data, file), "changed save written");
                var read = SaveStore.Read(file);
                Check(read != null, "changed save read back");
                game.RestoreProgress(read!);
            }
            void Pose(Vector3 p, Vector3 forward) =>
                car.SetPose(world.Ground.At(p.X, p.Z, .15f), Mathf.Atan2(-forward.X, -forward.Z));
            // Drives the car on from where it is, a step a frame of a tenth of a second.
            void Drive(Vector3 forward, float speed, int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    Pose(car.Position + forward * speed * .1f, forward);
                    Frames(1);
                }
            }

            // ---- Where the sheep graze ----
            var spots = world.SheepSpots;
            var flock = world.SheepFlock;
            var shepherd = world.Shepherd;
            Check(
                spots.Count == SheepPlans.Sheep && flock != null && shepherd != null,
                $"the valley has {spots.Count} meadows of sheep, the flock and the shepherd"
            );
            Check(world.ActorId(shepherd!) == World.ShepherdId, $"the shepherd has the ID {World.ShepherdId}");
            var borders = WorldLayout.Plan.Borders;
            var home = world.BorderHomes[(int)Border.Mountains]!;
            var eighths = spots
                .Select(p => Mathf.PosMod(Mathf.FloorToInt(Mathf.Atan2(p.Z, p.X) / (Mathf.Tau / 8)), 8))
                .Distinct()
                .Count();
            Check(eighths >= 6, $"the sheep graze in {eighths} of the eight directions round the valley");
            float widest = spots.SelectMany(a => spots.Select(b => Flat(a, b))).Max();
            Check(widest > 1300, $"the two sheep furthest apart graze {widest:0} m from each other");
            for (int i = 0; i < spots.Count; i++)
            {
                var p = spots[i];
                string name = $"the meadow of {SheepPlans.Name(i)}";
                Check(
                    ValleyBounds.Contains(p.X, p.Z, 35)
                        && Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > WorldTuning.CoreRadius + 60,
                    $"{name} {p} lies beyond the old valley, inside the valley"
                );
                Check(
                    world.OutlandRoutes.Distance(p) > 12 && world.Routes.Distance(p) > 12,
                    $"{name} lies off the roads"
                );
                Check(
                    River.Distance(p.X, p.Z) > River.HalfWidth(p.Z) + 10
                        && borders.BorderRiverDistance(p.X, p.Z) > ValleyBorders.BorderRiverHalfWidth + 10
                        && p.Y > ValleyBorders.SeaLevel + 2,
                    $"{name} is dry"
                );
                Check(!world.Plants.NearTrunk(p, 3.5f), $"no trunk stands on {name}");
                Check(Flat(p, home.Road) > 140, $"{name} is {Flat(p, home.Road):0} m from the shepherd's homestead");
                for (int other = 0; other < i; other++)
                    Check(Flat(p, spots[other]) > 150, $"{name} keeps apart from the others");
                // The way from a road: it begins on a road and every metre of it is open.
                var way = world.SheepWays[i];
                Check(
                    world.OutlandRoutes.Distance(way[0]) < 3 && Flat(way[^1], p) < .5f,
                    $"a way leads from a road to {name}"
                );
                int blocked = 0,
                    wet = 0;
                for (int s = 1; s < way.Length; s++)
                {
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Flat(way[s - 1], way[s])));
                    for (int k = 1; k <= steps; k++)
                    {
                        var q = way[s - 1].Lerp(way[s], k / (float)steps);
                        float h = world.Ground.Height(q.X, q.Z);
                        if (world.Plants.NearTrunk(q with { Y = h }, 2.4f))
                            blocked++;
                        if (
                            River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 2
                            || borders.BorderRiverDistance(q.X, q.Z) < ValleyBorders.BorderRiverHalfWidth
                            || h < ValleyBorders.SeaLevel + 1
                        )
                            wet++;
                    }
                }
                Check(blocked == 0 && wet == 0, $"the way to {name} passes no trunk and no water ({blocked}, {wet})");
            }
            var puppy = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Sheepdog);
            Check(
                puppy != null && !puppy.IsOwned && puppy.Collector == shepherd,
                "the sheepdog puppy waits by the shepherd"
            );
            Check(Enumerable.Range(0, spots.Count).All(i => !flock!.Shows(i)), "no sheep shows before the hunt");

            // ---- Saves that are turned down ----
            Rejected(data => data.Sheep = null!, "no section of the sheep");
            Rejected(data => data.Sheep.Home = null!, "no list of the sheep at home");
            Rejected(
                data =>
                {
                    data.Sheep.Started = true;
                    data.Sheep.Home = new[] { SheepPlans.Sheep };
                },
                "a sheep the flock does not have"
            );
            Rejected(
                data =>
                {
                    data.Sheep.Started = true;
                    data.Sheep.Home = new[] { 2, 2 };
                },
                "a sheep at home twice"
            );
            Rejected(
                data =>
                {
                    data.Sheep.Started = true;
                    data.Sheep.Home = new[] { 3 };
                    data.Sheep.Following = new[] { 3 };
                },
                "a sheep both at home and behind the car"
            );
            Rejected(data => data.Sheep.Following = new[] { 1 }, "a sheep found before the shepherd sent the player");
            Rejected(
                data =>
                {
                    data.State = GameState.ShepherdStatus;
                    data.Nearby = World.HunterId;
                },
                "the fold's panel at the hunter"
            );
            Rejected(
                data =>
                {
                    data.Sheep.Started = true;
                    data.State = GameState.Challenge;
                    data.Nearby = World.ShepherdId;
                    data.Quiz.Departure = (int)Game.Departure.Sheep;
                    data.Quiz.DepartureState = GameState.ShepherdStatus;
                    data.Quiz.Challenge = new Quizzes.ChallengeGenerator(42).Next();
                },
                "the fold's question with no sheep behind the car"
            );
            // A save from before the sheep: the section is not in the file, and none is found.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Sheep");
                string file = DiagnosticArtifacts.PathFor("sheep-checks/before-the-sheep.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read is { Sheep: { Started: false, Home.Length: 0, Following.Length: 0 } },
                    "a save from before the sheep loads"
                );
                game.RestoreProgress(read!);
                Check(!game.Sheep.Started && game.Sheep.HomeCount == 0, "and no sheep is looked for");
            }

            // ---- The shepherd sends the player out, with no letter from the post ----
            var gate = shepherd!.Position + home.Inward * -3 + home.Right * 2;
            Pose(gate, home.Inward);
            Frames(2);
            Check(
                game.Post.Delivered[(int)Border.Mountains] == 0 && shepherd.Guided,
                "with no letter yet the shepherd shows his mark until he has sent the player out"
            );
            game.OpenBorderKeeper(shepherd);
            Check(
                game.State == GameState.ShepherdStatus && Has("StartSheep") && Has("SheepFold"),
                "his panel shows the fold and sends the player out"
            );
            RoundTrip("the fold's panel");
            game.BeginSheepHunt();
            var herd = game.Sheep;
            Check(game.State == GameState.Exploring && herd.Started && herd.LostCount == 8, "the hunt begins");
            Check(!shepherd.Guided, "and his mark goes out");
            Frames(1);
            Check(Enumerable.Range(0, 8).All(flock!.Shows), "every sheep grazes on its meadow");
            Check(
                game.SheepHudShown && game.SheepHudNote == OutlandTexts.SheepListen,
                "the corner panel points to a sheep"
            );

            // ---- Finding a sheep ----
            int first = herd.Nearest(SheepStatus.Lost, car.Position);
            var meadow = spots[first];
            var inward = (-meadow with { Y = 0 }).Normalized();
            Pose(meadow - inward * 20, inward);
            Frames(1);
            Check(herd.StatusOf(first) == SheepStatus.Lost, "20 m away the sheep still grazes");
            Pose(meadow - inward * 5, inward);
            Frames(1);
            Check(
                herd.StatusOf(first) == SheepStatus.Following && herd.FollowingCount == 1,
                $"{SheepPlans.Name(first)} follows the car once it comes near"
            );
            Check(shepherd.Guided, "and the shepherd shows his mark again");
            RoundTrip("a sheep behind the car");
            Check(game.Sheep.StatusOf(first) == SheepStatus.Following, "after a load the sheep still follows");
            herd = game.Sheep;

            // ---- A gentle drive: the sheep keeps up ----
            Drive(inward, 9, 40);
            Check(
                herd.StatusOf(first) == SheepStatus.Following && herd.Lag < 1,
                $"at 9 m/s the sheep keeps up (lag {herd.Lag:0.0} m)"
            );
            Frames(10);
            Check(
                Flat(flock.PositionOf(first), car.Position) < 14,
                $"it trots right behind the car, {Flat(flock.PositionOf(first), car.Position):0.0} m"
            );

            // ---- A fast drive: the sheep falls behind and waits ----
            bool warned = false;
            for (int i = 0; i < 90 && herd.StatusOf(first) == SheepStatus.Following; i++)
            {
                Drive(inward, 25, 1);
                warned |= game.SheepHudNote == OutlandTexts.SheepSlowDown;
            }
            Check(warned, "a fast drive makes the corner panel ask for a slower one");
            Check(herd.StatusOf(first) == SheepStatus.Waiting, "the sheep left far behind stops and waits");
            Frames(2);
            Check(flock.Beckons(first), "it shows its beacon");
            Check(
                game.SheepHudNote == OutlandTexts.SheepGoBack(SheepPlans.Name(first)),
                "the corner panel sends the car back"
            );
            RoundTrip("a sheep left behind");
            herd = game.Sheep;
            Check(
                herd.StatusOf(first) == SheepStatus.Following,
                "after a load the sheep left behind walks behind the car again"
            );
            // Left behind once more, and the car goes back for it.
            Drive(inward, 30, 60);
            Check(herd.StatusOf(first) == SheepStatus.Waiting, "a sheep is left behind again");
            var waiting = herd.WhereIs(first);
            Pose(waiting + inward * 4, -inward);
            Frames(1);
            Check(herd.StatusOf(first) == SheepStatus.Following, "and joins the car when it comes back");

            // ---- Into the fold ----
            int coins = game.Coins;
            Pose(gate, home.Inward);
            Frames(3);
            game.OpenBorderKeeper(shepherd);
            Check(game.State == GameState.ShepherdStatus && Has("PenSheep"), "the shepherd offers to pen the sheep");
            game.BeginSheepPen();
            Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "a question comes first");
            RoundTrip("the question at the fold");
            ChallengeChecks.Solve(game);
            Check(
                game.State == GameState.Reward && game.Coins == coins + SheepPlans.CoinsPerSheep,
                "the right answer pays for the sheep"
            );
            Check(game.Sheep.StatusOf(first) == SheepStatus.Home && game.Sheep.HomeCount == 1, "it is home");
            Check(flock.Penning, "and runs into the fold");
            Frames(80);
            Check(flock.InFold(first), "it has jumped over the rail");
            Check(
                Flat(flock.PositionOf(first), world.FoldMiddle) < 5,
                $"and stands in the fold, {Flat(flock.PositionOf(first), world.FoldMiddle):0.0} m from its middle"
            );
            RoundTrip("a sheep at home");
            game.FinishInteraction();

            // ---- The last sheep and the puppy ----
            int last = Enumerable.Range(0, 8).First(i => i != first);
            Load(data =>
            {
                data.Sheep.Home = Enumerable.Range(0, 8).Where(i => i != last).ToArray();
                data.Sheep.Following = Array.Empty<int>();
            });
            herd = game.Sheep;
            Check(herd.HomeCount == 7 && herd.LostCount == 1 && !puppy!.IsOwned, "seven at home, one lost, no puppy");
            var lastSpot = spots[last];
            Pose(lastSpot + new Vector3(3, 0, 3), Vector3.Forward);
            Frames(1);
            Check(herd.StatusOf(last) == SheepStatus.Following, "the last sheep follows the car");
            Frames(2);
            Check(game.SheepHudShown && game.SheepHudNote == OutlandTexts.SheepTakeHome, "and the arrow points home");
            Pose(gate, home.Inward);
            Frames(3);
            coins = game.Coins;
            game.OpenBorderKeeper(shepherd);
            game.BeginSheepPen();
            ChallengeChecks.Solve(game);
            Check(
                game.Coins == coins + SheepPlans.CoinsPerSheep + SheepPlans.FlockCoins,
                "the whole flock pays a bonus"
            );
            Check(game.Sheep.Complete && puppy!.IsOwned, "the eighth sheep brings the sheepdog puppy");
            Frames(80);
            Check(Enumerable.Range(0, 8).All(flock.InFold), "every sheep is in the fold");
            RoundTrip("the whole flock in a save");
            game.FinishInteraction();
            Frames(2);
            Check(!game.SheepHudShown, "and the corner panel is gone");
            game.OpenBorderKeeper(shepherd);
            Check(
                game.State == GameState.ShepherdStatus && !Has("StartSheep") && !Has("PenSheep"),
                "the shepherd shows the full fold"
            );
            RoundTrip("the full fold's panel");
            game.FinishInteraction();
            GD.Print("SHEEP_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
