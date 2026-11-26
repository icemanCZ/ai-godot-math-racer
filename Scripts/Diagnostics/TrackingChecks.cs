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
/// The forest keeper's tracks (<c>docs/TRACKING_PLAN.md</c>): six trails lead one after another from beside her homestead
/// over the land beyond the old valley, each to a den in a clearing well apart from the others and the places of the
/// borders, along ways the car can drive (no trunk, no water, no steep slope) with prints on the ground all along; her
/// book sends the player out; only a stretch of prints shows in front of the car and moves on as it drives; the arrow
/// shows when the car leaves the trail; a save keeps the place on the trail; at the den a question coaxes the animal
/// out, pays and begins the next trail there; the sixth fills the book and brings the squirrel; a save from before the
/// trails loads with none of it found; and the damaged saves are turned down.
/// </summary>
public partial class TrackingChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public TrackingChecks(Game game, World world, Vehicle car)
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
            string path = DiagnosticArtifacts.PathFor("tracking-checks/progress.json");
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
                Check(before == after, $"the book of tracks restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"tracking-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Load(Action<SaveData> change)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"tracking-checks/changed-{files++}.json");
                Check(SaveStore.Write(data, file), "changed save written");
                var read = SaveStore.Read(file);
                Check(read != null, "changed save read back");
                game.RestoreProgress(read!);
            }
            void Drive(Vector3 p, Vector3 forward) =>
                car.SetPose(world.Ground.At(p.X, p.Z, .15f), Mathf.Atan2(forward.X, forward.Z));

            // ---- Where the trails lead ----
            var trails = world.TrackingTrails;
            var dens = world.TrackingDens;
            var prints = world.TrackingPrints;
            var animal = world.TrackingAnimal;
            var forester = world.Forester;
            Check(
                trails.Count == TrackingPlans.Animals
                    && dens != null
                    && prints != null
                    && animal != null
                    && forester != null,
                $"the valley has {trails.Count} trails, their dens and prints, and the forest keeper"
            );
            Check(world.ActorId(animal!) == World.TrackingAnimalId, $"the animal has the ID {World.TrackingAnimalId}");
            Check(animal!.Role is TrackingAnimalRole && animal.Away, "the animal is away until a trail is followed");
            Check(
                Flat(trails[0].Path[0], forester!.Position) < 50,
                $"the first trail begins beside the forest keeper's homestead, {Flat(trails[0].Path[0], forester.Position):0} m from her"
            );
            var borders = WorldLayout.Plan.Borders;
            float total = 0;
            for (int i = 0; i < trails.Count; i++)
            {
                var trail = trails[i];
                string name = $"the trail of the {trail.Animal}";
                total += trail.Length;
                Check(trail.Animal == (TrackingAnimal)i, $"{name} is number {i + 1}");
                if (i > 0)
                    Check(Flat(trail.Path[0], trails[i - 1].Den) < .5f, $"{name} begins at the den before it");
                var den = trail.Den;
                Check(
                    ValleyBounds.Contains(den.X, den.Z, 40)
                        && Mathf.Max(Mathf.Abs(den.X), Mathf.Abs(den.Z)) > WorldTuning.CoreRadius + 60,
                    $"its den {den} lies beyond the old valley, inside the valley"
                );
                Check(world.OutlandRoutes.Distance(den) > 18, $"the den of the {trail.Animal} lies off the roads");
                for (int other = 0; other < i; other++)
                    Check(
                        Flat(den, trails[other].Den) > 190,
                        $"the den of the {trail.Animal} keeps apart from the others"
                    );
                Check(
                    trail.Length is > 120 and < 800,
                    $"{name} is {trail.Length:0} m long, {Flat(trail.Path[0], den):0} m in a straight line"
                );
                Check(
                    Flat(trail.Path[^1], den) is > 4 and < 7 && Flat(trail.Talk, den) < 3,
                    "it ends in front of the den, where the animal is talked to"
                );
                // The way: open and gentle all along, every metre.
                int blocked = 0,
                    wet = 0,
                    steep = 0;
                string where = "";
                for (int p = 1; p < trail.Path.Length; p++)
                {
                    var a = trail.Path[p - 1];
                    var b = trail.Path[p];
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Flat(a, b)));
                    float previous = world.Ground.Height(a.X, a.Z);
                    for (int s = 1; s <= steps; s++)
                    {
                        var q = a.Lerp(b, s / (float)steps);
                        float h = world.Ground.Height(q.X, q.Z);
                        if (world.Plants.NearTrunk(q with { Y = h }, 2.4f))
                            blocked++;
                        if (
                            River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 2
                            || borders.BorderRiverDistance(q.X, q.Z) < ValleyBorders.BorderRiverHalfWidth
                            || h < ValleyBorders.SeaLevel + 1
                            || !ValleyBounds.Contains(q.X, q.Z, 8)
                        )
                            wet++;
                        if (Mathf.Abs(h - previous) > .4f * Flat(a, b) / steps)
                            steep++;
                        if (where == "" && blocked + wet + steep > 0)
                            where = $", first at {q} on piece {p} of {trail.Path.Length - 1}";
                        previous = h;
                    }
                }
                Check(
                    blocked == 0 && wet == 0 && steep == 0,
                    $"{name} passes no trunk, no water and no steep slope ({blocked} m by a trunk, {wet} m wet, {steep} m steep{where})"
                );
                Check(trail.Prints.Length > trail.Length / 3.2f, $"{name} has {trail.Prints.Length} prints all along");
                Check(
                    trail.Prints.All(print =>
                        print.Position.Y - world.Ground.Height(print.Position.X, print.Position.Z) is > -.05f and < .6f
                    ),
                    "every print lies on the ground"
                );
                var middle = world.Ground.At(den.X, den.Z);
                Check(
                    world.InBorderfolkZone(middle) && !world.Grass.CanGrow(middle),
                    $"no plant and no grass grows on the den of the {trail.Animal}"
                );
            }
            Check(total > 1500, $"the trails lead {total:0} m over the land beyond the old valley");
            Check(world.Plants.OutlandTreePositions.Count(world.InTrackingZone) == 0, "no tree is left out for a den");
            var squirrel = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Squirrel);
            Check(
                squirrel != null && !squirrel.IsOwned && squirrel.Collector == forester,
                "the squirrel waits by the forest keeper"
            );

            // ---- Saves that are turned down ----
            Rejected(data => data.Tracking = null!, "no section of the book of tracks");
            Rejected(data => data.Tracking.Found = -1, "a negative count of animals found");
            Rejected(
                data =>
                {
                    data.Tracking.Started = true;
                    data.Tracking.Found = TrackingPlans.Animals + 1;
                },
                "more animals than the book has"
            );
            Rejected(data => data.Tracking.Found = 2, "animals found before the keeper sent the player out");
            Rejected(
                data =>
                {
                    data.State = GameState.TrackingStatus;
                    data.Nearby = World.HunterId;
                },
                "the book's panel at the hunter"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.TradeStatus;
                    data.Nearby = World.TrackingAnimalId;
                },
                "a shop's panel at the animal of a trail"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.Challenge;
                    data.Nearby = World.TrackingAnimalId;
                },
                "a question at a den before any trail is followed"
            );
            // A save from before the trails: the section is not in the file, and nothing is found.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Tracking");
                string file = DiagnosticArtifacts.PathFor("tracking-checks/before-the-tracks.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(read is { Tracking: { Started: false, Found: 0 } }, "a save from before the trails loads");
                game.RestoreProgress(read!);
                Check(!game.Tracking.Started && game.Tracking.Found == 0, "and no trail is followed");
            }

            // ---- Before her first letter she only talks about the forest ----
            car.SetPose(world.Ground.At(forester.Position.X + 3, forester.Position.Z + 3, .15f), 0);
            Frames(2);
            Check(!forester.Guided, "before her first letter the forest keeper shows no mark");
            game.OpenBorderKeeper(forester);
            Check(game.State == GameState.KeeperStatus, "and her panel is the keeper's panel");
            game.FinishInteraction();
            Rejected(data => data.Tracking.Started = true, "a trail followed before her first letter");
            var letter = game.CaptureProgress();
            letter.Post.Delivered[(int)Border.Forest] = 1;
            game.RestoreProgress(letter);

            // ---- The forest keeper sends the player out ----
            Check(forester.Guided, "after her letter she shows her mark until she has sent the player out");
            game.OpenBorderKeeper(forester);
            Check(
                game.State == GameState.TrackingStatus && Has("StartTracking") && Has("TrackingBook"),
                "her panel shows the book of tracks and sends the player out"
            );
            RoundTrip("the book's panel");
            game.BeginTracking();
            var hunt = game.Tracking;
            Check(
                game.State == GameState.Exploring && hunt.Started && hunt.Current == trails[0],
                "the first trail begins"
            );
            Check(!forester.Guided, "and her mark goes out");
            Check(
                !animal.Away && Flat(animal.Position, trails[0].Talk) < .01f,
                "the animal waits in front of the first den"
            );
            Check(dens!.Shows(0) && !dens.SitsOut(0) && !dens.Shows(1), "only the hare peeps out of its den");
            Check(prints!.Trail == trails[0], "the prints of the first trail are shown");

            // ---- Following the prints ----
            var first = trails[0];
            int setOff = Mathf.Max(0, hunt.Progress);
            Check(
                setOff <= TrackingPrints.Ahead
                    && prints.Shows(setOff)
                    && !prints.Shows(setOff + TrackingPrints.Ahead + 4),
                $"only a stretch of prints shows ahead, from print {setOff} near the homestead"
            );
            int middleIndex = first.Prints.Length / 2;
            for (int i = 0; i <= middleIndex; i += 4)
            {
                Drive(first.Prints[i].Position, first.Prints[i].Forward);
                Frames(1);
            }
            Check(hunt.Progress >= middleIndex - 2, $"driving along the prints reaches print {hunt.Progress}");
            Check(
                prints.Shows(hunt.Progress + 10) && !prints.Shows(hunt.Progress - TrackingPrints.Behind - 3),
                "the stretch shown moves on with the car"
            );
            Check(
                !prints.Shows(Mathf.Min(first.Prints.Length - 1, hunt.Progress + TrackingPrints.Ahead + 3))
                    || hunt.Progress + TrackingPrints.Ahead + 3 >= first.Prints.Length,
                "and the far prints stay hidden"
            );
            Frames(1);
            Check(game.TrackingHudShown && !game.TrackingArrowShown, "the corner panel shows the trail, no arrow");
            var here = first.Prints[hunt.Progress];
            var side = Vector3.Up.Cross(here.Forward);
            Drive(here.Position + side * 60, here.Forward);
            Frames(1);
            Check(game.TrackingArrowShown, "off the trail the arrow points back to it");
            int reached = hunt.Progress;
            Drive(here.Position, here.Forward);
            Frames(1);
            Check(!game.TrackingArrowShown, "back on it, the arrow goes");
            RoundTrip("a save on the trail");
            Check(
                hunt == game.Tracking && game.Tracking.Progress >= reached - 3,
                "the place on the trail is found again"
            );

            // ---- At the den ----
            int coins = game.Coins;
            Drive(first.Prints[^1].Position, first.Prints[^1].Forward);
            Frames(2);
            Check(game.NearbyActor == animal, "at the end of the prints the car is by the animal");
            Check(animal.DisplayName == TrackingPlans.Name(TrackingAnimal.Hare), "it is the hare");
            game.Interact();
            Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "a question coaxes it out");
            RoundTrip("the question at a den");
            ChallengeChecks.Solve(game);
            Check(
                game.State == GameState.Reward && game.Coins == coins + TrackingPlans.CoinsPerAnimal,
                "the right answer pays"
            );
            Check(game.Tracking.Found == 1 && dens.SitsOut(0), "the hare is in the book and sits out by its den");
            Check(game.Tracking.Current == trails[1] && prints.Trail == trails[1], "the fox's trail begins at the den");
            Check(
                Flat(animal.Position, trails[1].Talk) < .01f
                    && animal.DisplayName == TrackingPlans.Name(TrackingAnimal.Fox),
                "the animal to coax out is now the fox, at her den"
            );
            Check(dens.Shows(1) && !dens.Shows(2), "the fox peeps out of hers");
            RoundTrip("the reward of a den");
            game.FinishInteraction();
            Frames(2);
            Check(game.NearbyActor != animal, "the hare's den asks nothing more");

            // ---- The last animal and the squirrel ----
            Load(data =>
            {
                data.Tracking.Started = true;
                data.Tracking.Found = TrackingPlans.Animals - 1;
            });
            Check(
                game.Tracking.Current == trails[^1] && !squirrel!.IsOwned && dens.SitsOut(4) && dens.Shows(5),
                "five animals found, the lynx's trail followed, no squirrel yet"
            );
            var last = trails[^1];
            Drive(last.Prints[^1].Position, last.Prints[^1].Forward);
            Frames(2);
            coins = game.Coins;
            game.Interact();
            Check(game.State == GameState.Challenge, "the lynx is asked out");
            ChallengeChecks.Solve(game);
            Check(
                game.Coins == coins + TrackingPlans.CoinsPerAnimal + TrackingPlans.BookCoins,
                "the full book pays a bonus"
            );
            Check(
                game.Tracking.Complete && squirrel!.IsOwned,
                "the sixth animal fills the book and brings the squirrel"
            );
            Check(animal.Away && prints.Trail == null, "no trail is followed any more");
            Check(Enumerable.Range(0, trails.Count).All(dens.SitsOut), "every animal sits by its den");
            RoundTrip("the full book in a save");
            game.FinishInteraction();
            Frames(2);
            Check(!game.TrackingHudShown, "and the corner panel is gone");
            car.SetPose(world.Ground.At(forester.Position.X + 3, forester.Position.Z + 3, .15f), 0);
            Frames(2);
            game.OpenBorderKeeper(forester);
            Check(
                game.State == GameState.TrackingStatus && !Has("StartTracking"),
                "the forest keeper shows the full book"
            );
            RoundTrip("the full book's panel");
            game.FinishInteraction();
            GD.Print("TRACKING_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
