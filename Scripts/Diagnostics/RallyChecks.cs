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
/// The ferrywoman's rally (<c>docs/RALLY_PLAN.md</c>): four stages that go round the whole ring road from one road to a
/// border to the next, each with an arch at its start and three gates on open, level, dry ground with no trunk in their
/// lanes; the ferrywoman shows her card only after her first letter, then shows her mark until a stage is driven; the
/// clock starts at the arch, the question of a gate shows before it with its answers over the lanes, the right lane
/// pays and a wrong one costs five seconds; the finish pays for the medal and goes straight on into the next stage; a
/// car that leaves the stage ends the drive; a load keeps the best times and ends the drive; gold on every stage brings
/// the otter; a save from before the rally loads with none of it; and the damaged saves are turned down.
/// </summary>
public partial class RallyChecks : Node
{
    /// <summary>The checks drive this fast, and step the game this often.</summary>
    private const float Speed = 14,
        Step = .25f;

    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public RallyChecks(Game game, World world, Vehicle car)
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

    private void Frames(int count, float delta = .1f)
    {
        for (int i = 0; i < count; i++)
            game._Process(delta);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("rally-checks/progress.json");
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
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"the rally restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"rally-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Load(Action<SaveData> change)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"rally-checks/changed-{files++}.json");
                Check(SaveStore.Write(data, file), "changed save written");
                var read = SaveStore.Read(file);
                Check(read != null, "changed save read back");
                game.RestoreProgress(read!);
            }
            var course = world.Rally;
            var gates = world.RallyGates;
            var ferrywoman = world.Ferrywoman;
            void Pose(float along, float across)
            {
                var forward = course!.Forward(along);
                var p = course.At(along) + course.Right(along) * across;
                car.SetPose(world.Ground.At(p.X, p.Z, .15f), Mathf.Atan2(-forward.X, -forward.Z));
            }

            // ---- The course ----
            Check(
                course != null && gates != null && ferrywoman != null,
                "the valley has the rally, its gates and the ferrywoman"
            );
            Check(world.ActorId(ferrywoman!) == World.FerrywomanId, $"the ferrywoman has the ID {World.FerrywomanId}");
            Check(course!.Stages.Count == RallyPlans.Stages, $"the rally has {course.Stages.Count} stages");
            Check(gates!.ArchCount == RallyPlans.Stages, "an arch stands at the start of every stage");
            float total = course.Stages.Sum(stage => stage.Length);
            Check(
                Mathf.Abs(total - course.Length) < 1,
                $"the stages go once round the ring ({total:0} m of {course.Length:0} m)"
            );
            Check(
                course.Stages.Select(stage => stage.From).Distinct().Count() == 4,
                "every stage begins at the road to another border"
            );
            for (int s = 0; s < course.Stages.Count; s++)
            {
                var stage = course.Stages[s];
                var next = course.Stages[(s + 1) % course.Stages.Count];
                Check(
                    Mathf.Abs(Mathf.PosMod(stage.Finish, course.Length) - next.Start) < .01f && stage.To == next.From,
                    $"stage {s + 1}, {stage.Name}, ends where the next begins"
                );
                Check(stage.Length > 600, $"stage {s + 1} is {stage.Length:0} m long");
                var arch = course.At(stage.Start);
                Check(
                    WorldLayout.Plan.Outland.BridgeAt(arch.X, arch.Z, 20) == null
                        && world.OutlandRoutes.Distance(arch) < 1.5f,
                    $"the arch of stage {s + 1} stands on the ring, off the bridges"
                );
                Check(stage.Gates.Length == RallyPlans.GatesPerStage, $"stage {s + 1} has three gates");
                Check(stage.Gates[0] - stage.Start >= 180, "its first gate is far enough from the start to be read");
                for (int g = 0; g < stage.Gates.Length; g++)
                {
                    float along = stage.Gates[g];
                    var p = course.At(along);
                    if (g > 0)
                        Check(along - stage.Gates[g - 1] >= 140, $"gate {g + 1} keeps apart from the one before");
                    Check(along < stage.Finish - 80, $"gate {g + 1} is well before the finish");
                    Check(
                        Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > WorldTuning.CoreRadius
                            && ValleyBounds.Contains(p.X, p.Z, 20),
                        $"gate {g + 1} of stage {s + 1} lies beyond the old valley, inside the valley"
                    );
                    Check(Flat(gates.GatePosition(s, g), p) < .01f, "the gate's model stands where it was planned");
                    int blocked = 0,
                        wet = 0;
                    var right = course.Right(along);
                    for (float across = -RallyCourse.GateHalfWidth; across <= RallyCourse.GateHalfWidth; across += 1)
                    {
                        var q = p + right * across;
                        float h = world.Ground.Height(q.X, q.Z);
                        if (world.Plants.NearTrunk(q with { Y = h }, 1.8f))
                            blocked++;
                        if (River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 1 || h < ValleyBorders.SeaLevel + 1)
                            wet++;
                    }
                    Check(blocked == 0 && wet == 0, $"the lanes of gate {g + 1} of stage {s + 1} are open and dry");
                }
            }
            var otter = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Otter);
            Check(
                otter != null && !otter.IsOwned && otter.Collector == ferrywoman,
                "the otter waits by the ferrywoman"
            );

            // ---- A drive goes on stage after stage, once round ----
            {
                var lap = new RallyRun(course, 2);
                int goneOn = 0;
                while (lap.GoOn())
                    goneOn++;
                Check(
                    goneOn == 3 && lap.StagesInRow == 4,
                    "a drive goes on into three more stages, once round the ring"
                );
            }

            // ---- Saves that are turned down ----
            Rejected(data => data.Rally = null!, "no section of the rally");
            Rejected(data => data.Rally.Best = null!, "no best times");
            Rejected(data => data.Rally.Best = new int[3], "best times of three stages");
            Rejected(data => data.Rally.Best = new[] { 0, -5, 0, 0 }, "a negative time");
            Rejected(data => data.Rally.Best = new[] { 0, RallyPlans.LongestTenths + 1, 0, 0 }, "a time over an hour");
            Rejected(data => data.Rally.Best = new[] { 900, 0, 0, 0 }, "a stage driven before the ferrywoman's letter");
            Rejected(
                data =>
                {
                    data.State = GameState.RallyStatus;
                    data.Nearby = World.ShepherdId;
                },
                "the rally's card at the shepherd"
            );
            // A save from before the rally: the section is not in the file, and no stage is driven.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Rally");
                string file = DiagnosticArtifacts.PathFor("rally-checks/before-the-rally.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read is { Rally.Best.Length: RallyPlans.Stages } && read.Rally.Best.All(time => time == 0),
                    "a save from before the rally loads"
                );
                game.RestoreProgress(read!);
                Check(game.RallyBest.All(time => time == 0) && game.RallyDrive == null, "and no stage is driven");
            }

            // ---- Before her letter she has only her own panel ----
            var home = world.BorderHomes[(int)Border.River]!;
            var gate = ferrywoman!.Position + home.Inward * -3 + home.Right * 2;
            void AtFerrywoman()
            {
                car.SetPose(world.Ground.At(gate.X, gate.Z, .15f), Mathf.Atan2(-home.Inward.X, -home.Inward.Z));
                Frames(2);
            }
            AtFerrywoman();
            Check(!ferrywoman.Guided, "before her letter the ferrywoman shows no mark");
            game.OpenBorderKeeper(ferrywoman);
            Check(
                game.State == GameState.KeeperStatus && !Has("RallyCard"),
                "and talking to her shows the keeper's panel"
            );
            game.FinishInteraction();

            // ---- Her letter came: the rally's card ----
            Load(data => data.Post.Delivered[(int)Border.River] = 1);
            AtFerrywoman();
            Check(ferrywoman.Guided, "once her letter came the ferrywoman shows her mark");
            game.OpenBorderKeeper(ferrywoman);
            Check(
                game.State == GameState.RallyStatus
                    && Has("RallyCard")
                    && Enumerable.Range(0, 4).All(i => Has($"RallyStart{i}")),
                "her card shows the four stages, each with a green arrow"
            );
            Check(!game.RallyHudShown, "no corner panel shows over her card");
            RoundTrip("the rally's card");
            game.BeginRallyStage(0);
            var drive = game.RallyDrive;
            Check(
                game.State == GameState.Exploring && drive is { Stage: 0, Racing: false },
                "a green arrow sends the player to the start of the stage"
            );
            Check(!ferrywoman.Guided, "and her mark goes out");
            Frames(1);
            Check(
                game.RallyHudShown && game.RallyHudNote == Texts.RallyDriveToStart,
                "the corner panel points to the start"
            );

            // ---- The start ----
            var first = course.Stages[0];
            float d = first.Start - 25;
            Pose(d, 1);
            Frames(1, Step);
            Check(!drive!.Racing, "before the arch the clock does not run");
            int coins = game.Coins;
            void DriveTo(float until, Func<float, float> across)
            {
                while (d < until)
                {
                    d += Speed * Step;
                    Pose(d, across(d));
                    Frames(1, Step);
                }
            }
            DriveTo(first.Start + 6, _ => 1);
            Check(drive.Racing && drive.NextGate == 0, "driving through the arch starts the clock");
            var question = game.RallyQuestion;
            Check(question != null && gates.Asks(0, 0), "the first gate hangs the answers of its question");

            // ---- The first gate: the right lane ----
            DriveTo(first.Gates[0] - Game.RallyQuestionAhead + 20, _ => 1);
            Check(game.RallyHudAsks && game.RallyHudNote == Texts.RallyPickLane, "near the gate its question shows");
            int rightLane = Enumerable.Range(0, 3).First(i => question!.Accepts(question.Choices[i]));
            float LaneAcross(int lane) => (lane - 1) * RallyCourse.LaneHalfWidth * 2;
            DriveTo(first.Gates[0] + 6, _ => LaneAcross(rightLane));
            Check(
                drive.Answers[0] == true && game.Coins == coins + RallyPlans.CoinsPerGate,
                "the right lane is right and pays a coin"
            );
            Check(
                gates.BoardColor(0, 0, rightLane).G > .7f && gates.BoardColor(0, 0, rightLane).R < .6f,
                "and the right board turns green"
            );
            Check(drive.NextGate == 1 && gates.Asks(0, 1), "the next gate asks the next question");
            var second = game.RallyQuestion!;
            Check(!ReferenceEquals(second, question), "a new question for the next gate");

            // ---- The second gate: a wrong lane ----
            int rightSecond = Enumerable.Range(0, 3).First(i => second.Accepts(second.Choices[i]));
            int wrongLane = (rightSecond + 1) % 3;
            DriveTo(first.Gates[1] + 6, _ => LaneAcross(wrongLane));
            Check(drive.Answers[1] == false && drive.Wrong == 1, "a wrong lane counts as wrong");
            Check(
                Mathf.Abs(drive.Time - drive.Driven - RallyPlans.PenaltySeconds) < .01f,
                "and adds five seconds to the time"
            );
            Check(game.RallyHudNote.StartsWith("Škoda"), "the corner panel tells the right answer");

            // ---- The third gate and the finish: gold, and straight on into the next stage ----
            var third = game.RallyQuestion!;
            int rightThird = Enumerable.Range(0, 3).First(i => third.Accepts(third.Choices[i]));
            DriveTo(first.Gates[2] + 6, _ => LaneAcross(rightThird));
            Check(drive.Answers[2] == true, "the third gate is right");
            DriveTo(first.Finish - 10, _ => 0);
            Check(
                game.RallyHudNote == Texts.RallyToFinish,
                "after the last gate the corner panel points to the finish"
            );
            int beforeFinish = game.Coins;
            float time = drive.Time;
            DriveTo(first.Finish + 4, _ => 0);
            float best = game.RallyBest[0];
            var medal = RallyPlans.MedalFor(best, first.Length);
            Check(
                medal == RallyMedal.Gold,
                $"{first.Length:0} m at {Speed} m/s with one wrong gate is gold ({best:0.0} s)"
            );
            Check(
                game.Coins == beforeFinish + RallyPlans.StageCoins(RallyMedal.Gold) + RallyPlans.NewMedalCoins,
                "the finish pays for the gold and for the new medal"
            );
            Check(
                best > time && best < time + 1.5f && game.RallyBest.Skip(1).All(other => other == 0),
                "the best time of the stage is kept"
            );
            Check(drive is { Stage: 1, Racing: true, NextGate: 0 }, "the next stage begins at once from the same arch");
            Check(gates.Asks(1, 0), "and its first gate asks");

            // ---- A load ends the drive and keeps the best time ----
            RoundTrip("a best time");
            Check(game.RallyDrive == null && game.RallyBest[0] > 0, "after a load no drive is under way");
            Frames(1);
            Check(!game.RallyHudShown, "and the corner panel is gone");
            AtFerrywoman();
            Check(!ferrywoman.Guided, "the ferrywoman shows no mark once a stage was driven");

            // ---- Leaving the stage ends the drive ----
            game.OpenBorderKeeper(ferrywoman);
            Check(game.State == GameState.RallyStatus, "her card opens again");
            game.BeginRallyStage(1);
            drive = game.RallyDrive;
            var stage2 = course.Stages[1];
            d = stage2.Start - 25;
            Pose(d, 0);
            Frames(1, Step);
            DriveTo(stage2.Start + 30, _ => 0);
            Check(drive!.Racing, "the second stage is under way");
            var off = course.At(d) + course.Right(d) * 90;
            car.SetPose(world.Ground.At(off.X, off.Z, .15f), 0);
            Frames(25, Step);
            Check(game.RallyDrive == null, "a car far off the road for long ends the drive");
            Check(game.RallyHudNote == Texts.RallyAbandoned, "and the corner panel says so");
            Check(game.RallyBest[1] == 0, "the stage left keeps no time");

            // ---- Gold on the last stage brings the otter ----
            Load(data => data.Rally.Best = new[] { 10, 10, 10, 0 });
            Check(!otter!.IsOwned, "three golds do not bring the otter");
            AtFerrywoman();
            game.OpenBorderKeeper(ferrywoman);
            game.BeginRallyStage(3);
            drive = game.RallyDrive;
            var last = course.Stages[3];
            d = last.Start - 25;
            Pose(d, 0);
            Frames(1, Step);
            DriveTo(last.Start + 6, _ => 0);
            for (int g = 0; g < RallyPlans.GatesPerStage; g++)
            {
                var asked = game.RallyQuestion!;
                int lane = Enumerable.Range(0, 3).First(i => asked.Accepts(asked.Choices[i]));
                DriveTo(last.Gates[g] + 6, _ => LaneAcross(lane));
            }
            DriveTo(last.Finish + 4, _ => 0);
            Check(drive!.Answers.All(answer => answer == null) && drive.Stage == 0, "the drive went on round the ring");
            Check(otter.IsOwned, "gold on every stage brings the otter");
            RoundTrip("every stage driven for gold");
            Check(otter.IsOwned, "and she stays after a load");
            AtFerrywoman();
            game.OpenBorderKeeper(ferrywoman);
            Check(game.State == GameState.RallyStatus && Has("RallyCard"), "the ferrywoman shows the card of golds");
            game.FinishInteraction();
            GD.Print("RALLY_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
