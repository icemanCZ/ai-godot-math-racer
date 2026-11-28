using System;
using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Quizzes;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The ferrywoman's rally round the land beyond the old valley (<c>docs/RALLY_PLAN.md</c>). Once the post has brought
/// Liška převoznice Vendula her first letter, her panel (state <see cref="GameState.RallyStatus"/>) shows the four
/// stages of the ring road with their best times and medals, and a green arrow beside each sends the player to its
/// start arch. Driving through it starts the clock. On the way are three gates with three lanes each: a question shows
/// in the corner panel before each, and its three answers hang over the lanes; the right lane pays a coin, a wrong one
/// (or going round the gate) adds five seconds. The finish arch is the start of the next stage, so the drive goes on
/// until the whole ring is driven. The time earns a medal, a better medal than before pays a bonus, and gold on all
/// four stages brings the otter Šplouchalka. Only the best time of each stage is saved (<c>SaveData.Rally</c>); a drive
/// under way is not, and a load ends it.
/// </summary>
public partial class Game
{
    /// <summary>The question of the next gate is shown and read this far before it.</summary>
    internal const float RallyQuestionAhead = 170;

    /// <summary>The best time of each stage in tenths of a second, 0 for a stage not yet driven.</summary>
    private int[] rallyBest = new int[RallyPlans.Stages];

    /// <summary>The drive under way: a stage waiting at its start arch or being driven.</summary>
    private RallyRun? rallyRun;

    /// <summary>The question whose answers hang over the next gate.</summary>
    private IChallenge? rallyQuestion;

    /// <summary>Whether the question of the next gate was read out already.</summary>
    private bool rallyQuestionRead;

    /// <summary>A short note in the corner panel, such as the right answer after a wrong lane, and how long it stays.</summary>
    private string rallyFlash = "";

    private double rallyFlashTime;

    /// <summary>Shows a short note in the corner panel of the rally for a few seconds, and reads it out.</summary>
    private void FlashRally(string note)
    {
        rallyFlash = note;
        rallyFlashTime = 4;
        Speak(note);
    }

    /// <summary>The drive under way, for the checks.</summary>
    internal RallyRun? RallyDrive => rallyRun;

    /// <summary>The question of the next gate, for the checks.</summary>
    internal IChallenge? RallyQuestion => rallyQuestion;

    /// <summary>The best times of the stages in seconds (0 for none), for the checks.</summary>
    internal float[] RallyBest => rallyBest.Select(RallyPlans.Seconds).ToArray();

    /// <summary>The valley has the rally and the post has brought the ferrywoman her first letter.</summary>
    private bool RallyWanted =>
        world.Rally != null
        && world.RallyGates != null
        && (rallyBest.Any(time => time > 0) || Post.Delivered[(int)Border.River] > 0);

    /// <summary>The rally is wanted and no stage was ever driven: the ferrywoman shows her mark.</summary>
    private bool RallyAwaitsStart => RallyWanted && rallyRun == null && rallyBest.All(time => time == 0);

    /// <summary>The medal of each stage from its best time.</summary>
    private RallyMedal[] RallyMedals =>
        world.Rally is not { } course
            ? new RallyMedal[RallyPlans.Stages]
            : Enumerable
                .Range(0, RallyPlans.Stages)
                .Select(s => RallyPlans.MedalFor(RallyPlans.Seconds(rallyBest[s]), course.Stages[s].Length))
                .ToArray();

    /// <summary>The ferrywoman shows the card of her rally; false before her first letter or without a rally.</summary>
    private bool OpenRally(Character keeper)
    {
        if (!RallyWanted)
            return false;
        EnterInteraction(GameState.RallyStatus, keeper);
        ShowRally();
        Speak(
            rallyBest.All(time => time == 0) ? keeper.Greeting + "\n" + OutlandTexts.RallyWanted
                : RallyMedals.All(medal => medal == RallyMedal.Gold) ? OutlandTexts.RallyAllGold
                : OutlandTexts.RallyAgain,
            keeper
        );
        return true;
    }

    private void ShowRally()
    {
        if (nearby is not { Role: BorderKeeperRole { Border: Border.River } } keeper || world.Rally is not { } course)
            return;
        ui.RallyPanel(
            keeper,
            course.Stages.Select(stage => (stage.From, stage.To)).ToArray(),
            course.Stages.Select(stage => stage.Name).ToArray(),
            RallyBest,
            RallyMedals,
            rallyRun?.Stage ?? -1,
            world.AllPets.FirstOrDefault(pet => pet.Kind == CollectorPetKind.Otter),
            Post.Stamps((int)Border.River)
        );
    }

    /// <summary>A green arrow of her card: the stage waits for the car at its start arch.</summary>
    internal void BeginRallyStage(int stage)
    {
        if (
            State != GameState.RallyStatus
            || nearby is not { Role: BorderKeeperRole { Border: Border.River } } keeper
            || !RallyWanted
            || world.Rally is not { } course
            || stage < 0
            || stage >= course.Stages.Count
        )
            return;
        rallyRun = new RallyRun(course, stage);
        rallyQuestion = null;
        world.RallyGates!.Reset();
        keeper.Celebrate();
        CloseInteraction();
        UpdatePostGuides();
        Speak(OutlandTexts.RallyToStart(course.Stages[stage].Name), keeper);
    }

    /// <summary>The question of the next gate of the stage driven now hangs its answers over the gate's lanes.</summary>
    private void AskRallyGate()
    {
        if (rallyRun is not { Racing: true } run || world.RallyGates is not { } gates)
            return;
        rallyQuestionRead = false;
        if (run.NextGate >= run.Current.Gates.Length)
        {
            rallyQuestion = null;
            return;
        }
        rallyQuestion = quiz.NextGate();
        gates.Ask(run.Stage, run.NextGate, rallyQuestion);
    }

    /// <summary>The lane of the right answer of a gate's question, 0 left to 2 right.</summary>
    private static int RightLane(IChallenge question)
    {
        for (int i = 0; i < question.Choices.Count; i++)
            if (question.Accepts(question.Choices[i]))
                return i;
        return 0;
    }

    /// <summary>
    /// The drive moves on: the clock while the car is driven, the start arch, the gates and the finish, and the corner
    /// panel with the clock, the gates, the question of the next gate and the arrow to it.
    /// </summary>
    private void TickRally(double delta)
    {
        if (State is GameState.Title || world.RallyGates is not { } gates || world.Rally is not { } course)
            return;
        gates.Tick(State == GameState.Paused ? 0 : (float)delta);
        bool exploring = State == GameState.Exploring;
        if (exploring && rallyFlashTime > 0)
            rallyFlashTime -= delta;
        string flash = rallyFlashTime > 0 ? rallyFlash : "";
        if (rallyRun is not { } run)
        {
            RallyNoteOnly(exploring, flash);
            return;
        }
        if (exploring)
        {
            var happened = run.Tick(vehicle.Position, (float)delta);
            switch (happened.Kind)
            {
                case RallyEventKind.Started:
                    AskRallyGate();
                    Speak(OutlandTexts.RallyGo);
                    break;
                case RallyEventKind.Gate:
                    PassRallyGate(run, happened.Gate, happened.Lane);
                    break;
                case RallyEventKind.Finished:
                    FinishRallyStage(run);
                    break;
                case RallyEventKind.Abandoned:
                    EndRally(OutlandTexts.RallyAbandoned);
                    break;
            }
        }
        if (rallyRun is not { } drive)
        {
            flash = rallyFlashTime > 0 ? rallyFlash : "";
            RallyNoteOnly(exploring, flash);
            return;
        }
        var goal = course.At(drive.GoalDistance);
        float ahead = drive.Racing
            ? drive.AheadOf
            : new Vector2(goal.X - vehicle.Position.X, goal.Z - vehicle.Position.Z).Length();
        bool asking =
            drive.Racing
            && rallyQuestion != null
            && drive.NextGate < drive.Current.Gates.Length
            && ahead < RallyQuestionAhead;
        if (asking && !rallyQuestionRead && exploring)
        {
            rallyQuestionRead = true;
            Speak(OutlandTexts.RallyQuestion(GateInstruction(rallyQuestion!)));
        }
        flash = rallyFlashTime > 0 ? rallyFlash : "";
        string note =
            flash != "" ? flash
            : !drive.Racing ? OutlandTexts.RallyDriveToStart
            : asking ? OutlandTexts.RallyPickLane
            : drive.NextGate >= drive.Current.Gates.Length ? OutlandTexts.RallyToFinish
            : OutlandTexts.RallyToGate;
        var stage = drive.Current;
        ui.RallyHud(
            exploring,
            vehicle,
            goal,
            (stage.From, stage.To),
            drive.Racing ? RallyPlans.Clock(drive.Time) : "",
            stage.Gates.Select(gate => (gate - stage.Start) / stage.Length).ToArray(),
            drive.Answers,
            drive.Racing ? (drive.Along - stage.Start) / stage.Length : null,
            drive.Racing || ahead < 12 ? "" : $"{ahead:0} m",
            note,
            asking ? rallyQuestion : null
        );
    }

    /// <summary>The corner panel with a note alone, after a drive ended, for as long as the note lasts.</summary>
    private void RallyNoteOnly(bool exploring, string flash) =>
        ui.RallyHud(
            exploring && flash != "",
            vehicle,
            null,
            null,
            "",
            Array.Empty<float>(),
            Array.Empty<bool?>(),
            null,
            "",
            flash,
            null
        );

    /// <summary>What a question asks for at a gate, as the ferrywoman says it.</summary>
    private static string GateInstruction(IChallenge question) =>
        question switch
        {
            LetterChallenge letter => OutlandTexts.RallyLetter(letter.Letter),
            ReadingChallenge reading => OutlandTexts.RallyReading(reading.Word),
            CompareChallenge => OutlandTexts.RallyCompare,
            _ => question.Expression.Replace("= ?", "").Trim(),
        };

    /// <summary>A gate was driven through: a coin for the right lane, five seconds more for a wrong one or none.</summary>
    private void PassRallyGate(RallyRun run, int gate, int? lane)
    {
        if (rallyQuestion is not { } question || world.RallyGates is not { } gates)
            return;
        int right = RightLane(question);
        bool correct = lane == right;
        run.Answer(gate, correct);
        gates.Answered(run.Stage, gate, lane, right);
        var where = gates.GatePosition(run.Stage, gate) + Vector3.Up * 5;
        if (correct)
        {
            AddCoins(RallyPlans.CoinsPerGate);
            CelebrateAt(where, OutlandTexts.RallyRight);
            Speak(OutlandTexts.RallyRight);
        }
        else
        {
            string answer = RallyGates.ChoiceText(question, question.Choices[right]);
            FlashRally(OutlandTexts.RallyWrong(answer, (int)RallyPlans.PenaltySeconds));
        }
        AskRallyGate();
    }

    /// <summary>
    /// The finish arch: the time and its medal, coins for the medal and a bonus for a better medal than ever before,
    /// the otter for gold on every stage; then the next stage begins at once, unless the whole ring is driven.
    /// </summary>
    private void FinishRallyStage(RallyRun run)
    {
        if (world.Rally is not { } course || world.RallyGates is not { } gates)
            return;
        var stage = run.Current;
        float time = run.Time;
        int before = rallyBest[run.Stage];
        var was = RallyPlans.MedalFor(RallyPlans.Seconds(before), stage.Length);
        var medal = RallyPlans.MedalFor(time, stage.Length);
        bool best = before == 0 || RallyPlans.Tenths(time) < before;
        if (best)
            rallyBest[run.Stage] = RallyPlans.Tenths(time);
        bool better = medal > was;
        gates.Golds = RallyMedals.Count(m => m == RallyMedal.Gold);
        int right = run.Answers.Count(answer => answer == true);
        int coins = RallyPlans.StageCoins(medal) + (better ? RallyPlans.NewMedalCoins : 0);
        string message = OutlandTexts.RallyFinished(
            stage.Name,
            RallyPlans.Clock(time),
            medal,
            right,
            run.Answers.Length
        );
        if (better && was != RallyMedal.None)
            message += "\n" + OutlandTexts.RallyBetterMedal(RallyPlans.NewMedalCoins);
        else if (best && before > 0)
            message += "\n" + OutlandTexts.RallyBestTime;
        bool goesOn = run.GoOn();
        if (!goesOn)
        {
            coins += RallyPlans.LapCoins;
            message += "\n" + OutlandTexts.RallyLap(RallyPlans.LapCoins);
        }
        AddCoins(coins);
        var arch = course.At(stage.Finish) + Vector3.Up * 5;
        string? pets = EarnPets();
        CelebrateAt(arch, WithPets(message, pets));
        Speak(pets != null ? OutlandTexts.RallyOtter : OutlandTexts.RallyMedalSpoken(medal));
        if (goesOn)
        {
            gates.Reset();
            AskRallyGate();
        }
        else
            EndRally(null);
        SaveProgress();
    }

    /// <summary>The drive ends: after the whole ring, or with a note when the car left the stage.</summary>
    private void EndRally(string? note)
    {
        rallyRun = null;
        rallyQuestion = null;
        world.RallyGates?.Reset();
        UpdatePostGuides();
        if (note != null)
            FlashRally(note);
    }

    /// <summary>Puts the best times back from a save, before the pets are, so that the otter knows whether she is owned.</summary>
    private void RestoreRally(int[] best)
    {
        rallyBest = best.ToArray();
        rallyRun = null;
        rallyQuestion = null;
        rallyFlashTime = 0;
        if (world.RallyGates is { } gates)
        {
            gates.Reset();
            gates.Golds = RallyMedals.Count(medal => medal == RallyMedal.Gold);
        }
        UpdatePostGuides();
    }

    /// <summary>Whether the corner panel of the rally shows, and its note, for the checks.</summary>
    internal bool RallyHudShown => ui.RallyHudShown;

    internal string RallyHudNote => ui.RallyHudNote;

    internal bool RallyHudAsks => ui.RallyHudAsks;
}
