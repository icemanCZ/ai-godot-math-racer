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
/// The owl's map (<c>docs/EXPLORER_PLAN.md</c>): her stand beside a road beyond the old valley and the eight wonders, one
/// of each kind, beside roads all round the valley, off the water, the roads and one another; the car sees a wonder by
/// driving past it, before the owl has shown her map too; her panel shows the map, her arrows lead to the nearest wonder
/// not yet seen, a question writes the wonders seen into the map, which pays; the eighth brings the little owl; a save
/// keeps all of it, a save from before the map loads with none of it, and the damaged saves are turned down.
/// </summary>
public partial class ExplorerChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public ExplorerChecks(Game game, World world, Vehicle car)
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
            string path = DiagnosticArtifacts.PathFor("explorer-checks/progress.json");
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
                Check(before == after, $"the map restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"explorer-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Load(Action<SaveData> change)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"explorer-checks/changed-{files++}.json");
                Check(SaveStore.Write(data, file), "changed save written");
                var read = SaveStore.Read(file);
                Check(read != null, "changed save read back");
                game.RestoreProgress(read!);
            }
            void Pose(Vector3 p, Vector3 forward) =>
                car.SetPose(world.Ground.At(p.X, p.Z, .15f), Mathf.Atan2(-forward.X, -forward.Z));

            // ---- Where the owl and the wonders are ----
            var owl = world.Cartographer;
            var site = world.CartographerPlace;
            var models = world.LandmarkModels;
            var landmarks = world.Landmarks;
            Check(
                owl != null && site != null && models != null && landmarks.Count == ExplorerPlans.Landmarks,
                $"the valley has the owl's stand and {landmarks.Count} wonders"
            );
            Check(world.ActorId(owl!) == World.CartographerId, $"the owl has the ID {World.CartographerId}");
            Check(owl!.Role is CartographerRole, "she draws the map");
            Check(
                Mathf.Max(Mathf.Abs(site!.Road.X), Mathf.Abs(site.Road.Z)) > WorldTuning.CoreRadius
                    && world.OutlandRoutes.Distance(site.Road) < 3,
                $"her stand {site.Road} is beside a road beyond the old valley"
            );
            Check(
                Flat(site.Road, world.Village.Center) < 900,
                $"not far from the village ({Flat(site.Road, world.Village.Center):0} m)"
            );
            Check(
                landmarks.Select((landmark, i) => landmark.Kind == i).All(same => same),
                "there is a wonder of each kind"
            );
            int eighths = landmarks.Select(landmark => World.EighthOf(landmark.Center)).Distinct().Count();
            Check(eighths >= 6, $"the wonders stand in {eighths} of the eight directions round the valley");
            var borders = WorldLayout.Plan.Borders;
            foreach (var landmark in landmarks)
            {
                var p = landmark.Center;
                float radius = ExplorerPlans.Radius(landmark.Kind);
                string name = ExplorerPlans.Name(landmark.Kind);
                Check(
                    ValleyBounds.Contains(p.X, p.Z, radius)
                        && Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > WorldTuning.CoreRadius + radius,
                    $"{name} {p} stands beyond the old valley, inside the valley"
                );
                Check(
                    world.OutlandRoutes.Distance(p) > radius + 3 && world.Routes.Distance(p) > radius + 3,
                    $"{name} stands off the roads"
                );
                Check(
                    world.OutlandRoutes.Distance(landmark.Road) < 3 && Flat(landmark.Road, p) < radius + 12,
                    $"{name} is seen from the road beside it"
                );
                Check(
                    River.Distance(p.X, p.Z) > River.HalfWidth(p.Z) + radius
                        && borders.BorderRiverDistance(p.X, p.Z) > ValleyBorders.BorderRiverHalfWidth + radius
                        && !borders.IsSand(p.X, p.Z),
                    $"{name} stands on dry ground"
                );
                Check(Flat(p, site.Road) > 100, $"{name} is {Flat(p, site.Road):0} m from the owl");
                foreach (var other in landmarks.Where(other => other.Kind < landmark.Kind))
                    Check(Flat(p, other.Center) > 200, $"{name} keeps apart from {ExplorerPlans.Name(other.Kind)}");
                Check(!models!.FlagShown(landmark.Kind), $"{name} has no flag of the owl yet");
            }
            var owlet = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Owlet);
            Check(owlet != null && !owlet.IsOwned && owlet.Collector == owl, "the little owl waits by the owl");

            // ---- Saves that are turned down ----
            Rejected(data => data.Explorer = null!, "no section of the map");
            Rejected(data => data.Explorer.Spotted = null!, "no list of the wonders seen");
            Rejected(data => data.Explorer.Spotted = new[] { ExplorerPlans.Landmarks }, "a wonder the valley lacks");
            Rejected(data => data.Explorer.Spotted = new[] { 2, 2 }, "a wonder seen twice");
            Rejected(
                data =>
                {
                    data.Explorer.Started = true;
                    data.Explorer.Spotted = new[] { 1 };
                    data.Explorer.Recorded = new[] { 3 };
                },
                "a wonder written in that was never seen"
            );
            Rejected(
                data =>
                {
                    data.Explorer.Spotted = new[] { 1 };
                    data.Explorer.Recorded = new[] { 1 };
                },
                "a wonder written in before the owl showed her map"
            );
            Rejected(
                data =>
                {
                    data.Explorer.Spotted = new[] { 4 };
                    data.Explorer.Guided = 4;
                },
                "the arrows to a wonder already seen"
            );
            Rejected(data => data.Explorer.Guided = ExplorerPlans.Landmarks, "the arrows to a wonder the valley lacks");
            Rejected(
                data =>
                {
                    data.State = GameState.ExplorerStatus;
                    data.Nearby = World.HunterId;
                },
                "the owl's panel at the hunter"
            );
            Rejected(
                data =>
                {
                    data.Explorer.Started = true;
                    data.State = GameState.Challenge;
                    data.Nearby = World.CartographerId;
                    data.Quiz.Departure = (int)Game.Departure.Explorer;
                    data.Quiz.DepartureState = GameState.ExplorerStatus;
                    data.Quiz.Challenge = new Quizzes.ChallengeGenerator(42).Next();
                },
                "the owl's question with no wonder seen"
            );
            // A save from before the map: the section is not in the file, and nothing is seen.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Explorer");
                string file = DiagnosticArtifacts.PathFor("explorer-checks/before-the-map.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read is { Explorer: { Started: false, Spotted.Length: 0, Recorded.Length: 0, Guided: -1 } },
                    "a save from before the map loads"
                );
                game.RestoreProgress(read!);
                Check(!game.Explorer.Started && game.Explorer.Spotted.Length == 0, "and no wonder is seen");
            }

            // ---- The owl shows her map ----
            var talk = site.Talk + site.Right * 2;
            Pose(talk, -site.Inward);
            Frames(2);
            Check(owl.Guided, "before she has shown her map the owl shows her mark");
            game.OpenCartographer(owl);
            Check(
                game.State == GameState.ExplorerStatus
                    && Has("ValleyMap")
                    && Has("GuideLandmark")
                    && !Has("RecordLandmarks"),
                "her panel shows the map and offers the way to a wonder"
            );
            RoundTrip("the owl's panel");
            int target = game.NearestHiddenLandmark(car.Position);
            game.BeginExplorerGuide();
            Check(
                game.State == GameState.Exploring && game.Explorer.Guided == target && game.ExplorerGuiding,
                $"the road arrows lead to {ExplorerPlans.Name(target)}, the nearest wonder"
            );
            Check(!owl.Guided, "and her mark goes out");
            Frames(2);
            Check(game.ExplorerHudShown, "the corner panel points to the wonder");
            RoundTrip("the arrows to a wonder");
            Check(game.ExplorerGuiding && game.Explorer.Guided == target, "after a load the arrows lead there still");

            // ---- Seeing a wonder ----
            var goal = landmarks[target];
            float reach = ExplorerPlans.Radius(target) + ExplorerPlans.SpotReach;
            var away = (goal.Road - goal.Center) with { Y = 0 };
            away = away.Normalized();
            var along = away.Cross(Vector3.Up);
            Pose(goal.Center + away * (reach + 12), along);
            Frames(1);
            Check(!game.Explorer.IsSpotted(target), $"{reach + 12:0} m away {ExplorerPlans.Name(target)} is not seen");
            Pose(goal.Road, along);
            Frames(1);
            Check(game.Explorer.IsSpotted(target), $"from the road beside it {ExplorerPlans.Name(target)} is seen");
            Check(game.Explorer.Guided == -1 && !game.ExplorerGuiding, "and the arrows that led there go out");
            Check(owl.Guided, "the owl shows her mark for a wonder to write in");
            RoundTrip("a wonder seen");

            // ---- Writing it into the map ----
            int coins = game.Coins;
            Pose(talk, -site.Inward);
            Frames(2);
            game.OpenCartographer(owl);
            Check(game.State == GameState.ExplorerStatus && Has("RecordLandmarks"), "the owl offers to write it in");
            game.BeginExplorerRecord();
            Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "a question comes first");
            RoundTrip("the owl's question");
            ChallengeChecks.Solve(game);
            Check(
                game.State == GameState.Reward && game.Coins == coins + ExplorerPlans.CoinsPerLandmark,
                "the right answer pays for the wonder"
            );
            Check(game.Explorer.IsRecorded(target) && models!.FlagShown(target), "it is in the map, with her flag");
            Check(!owl.Guided, "and her mark goes out");
            RoundTrip("a wonder in the map");
            game.FinishInteraction();

            // ---- A wonder seen before the owl showed her map ----
            Load(data => data.Explorer = new ExplorerSave());
            int early = (target + 1) % ExplorerPlans.Landmarks;
            Pose(landmarks[early].Road, Vector3.Forward);
            Frames(1);
            Check(
                game.Explorer.IsSpotted(early) && !game.Explorer.Started,
                "a wonder is seen before the owl has shown her map"
            );
            Pose(talk, -site.Inward);
            Frames(2);
            game.OpenCartographer(owl);
            Check(Has("RecordLandmarks"), "and she writes it in when she first shows it");
            game.FinishInteraction();

            // ---- The last wonder and the little owl ----
            int last = (target + 2) % ExplorerPlans.Landmarks;
            var others = Enumerable.Range(0, ExplorerPlans.Landmarks).Where(i => i != last).ToArray();
            Load(data =>
            {
                data.Explorer.Started = true;
                data.Explorer.Spotted = others;
                data.Explorer.Recorded = others;
                data.Explorer.Guided = -1;
            });
            Check(
                game.Explorer.RecordedCount == 7 && !owlet!.IsOwned && models!.RecordedCount == 7,
                "seven wonders in the map, no little owl yet"
            );
            Pose(landmarks[last].Road, Vector3.Forward);
            Frames(1);
            Check(game.Explorer.IsSpotted(last), "the last wonder is seen");
            Pose(talk, -site.Inward);
            Frames(2);
            coins = game.Coins;
            game.OpenCartographer(owl);
            Check(!Has("GuideLandmark"), "with every wonder seen the owl leads nowhere");
            game.BeginExplorerRecord();
            ChallengeChecks.Solve(game);
            Check(
                game.Coins == coins + ExplorerPlans.CoinsPerLandmark + ExplorerPlans.MapCoins,
                "the full map pays a bonus"
            );
            Check(game.Explorer.Complete && owlet!.IsOwned, "the eighth wonder brings the little owl");
            Check(
                Enumerable.Range(0, ExplorerPlans.Landmarks).All(models!.FlagShown),
                "every wonder has the owl's flag"
            );
            RoundTrip("the full map in a save");
            game.FinishInteraction();
            Frames(2);
            Check(!owl.Guided, "the owl with the full map shows no mark");
            game.OpenCartographer(owl);
            Check(
                game.State == GameState.ExplorerStatus && !Has("RecordLandmarks") && !Has("GuideLandmark"),
                "the owl shows the full map"
            );
            RoundTrip("the full map's panel");
            game.FinishInteraction();
            GD.Print("EXPLORER_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
