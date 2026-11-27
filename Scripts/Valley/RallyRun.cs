using System;
using Godot;
using MathRacer.Buildings;

namespace MathRacer.Valley;

/// <summary>What happened on the rally in one step of the car.</summary>
internal enum RallyEventKind
{
    None,

    /// <summary>The car drove through the start arch: the clock runs.</summary>
    Started,

    /// <summary>The car drove through a gate, in <see cref="RallyEvent.Lane"/> (null when it went round it).</summary>
    Gate,

    /// <summary>The car drove through the arch at the end of the stage.</summary>
    Finished,

    /// <summary>The car left the stage for good: off the road too long, or back past its start.</summary>
    Abandoned,
}

/// <summary>One thing that happened on the rally, with the gate and the lane for a gate (0 left, 1 middle, 2 right).</summary>
internal readonly record struct RallyEvent(RallyEventKind Kind, int Gate = -1, int? Lane = null);

/// <summary>
/// One drive of the ferrywoman's rally without scene nodes: the stage that waits at its start arch or is being driven,
/// the clock, the gates passed and how they were answered, and how far along the ring the car is. A stage that is
/// finished goes straight on into the next one, whose start arch is the same, until the car has been round the whole
/// ring once. Nothing of it is saved: a load ends the drive, and only the best time of each stage is kept.
/// </summary>
internal sealed class RallyRun
{
    /// <summary>How far from the start arch the car is watched for driving through it.</summary>
    private const float StartWatch = 40;

    /// <summary>The car may be this far from the middle of the ring, and for this long, before the stage is given up.</summary>
    private const float OffCourse = 45,
        OffCourseSeconds = 4;

    private readonly RallyCourse course;
    private float along;
    private bool watching;
    private float offTime;

    public RallyRun(RallyCourse course, int stage)
    {
        this.course = course;
        Stage = stage;
        Answers = new bool?[RallyPlans.GatesPerStage];
    }

    /// <summary>The stage that waits or is driven.</summary>
    public int Stage { get; private set; }

    public RallyStage Current => course.Stages[Stage];

    /// <summary>Whether the clock runs; before that the stage waits for the car at its start arch.</summary>
    public bool Racing { get; private set; }

    /// <summary>The seconds driven on the stage so far.</summary>
    public float Driven { get; private set; }

    /// <summary>The gates of the stage driven through so far: true on the right answer, false on a wrong one or missed.</summary>
    public bool?[] Answers { get; private set; }

    /// <summary>The next gate of the stage, or <see cref="RallyPlans.GatesPerStage"/> when only the finish is left.</summary>
    public int NextGate { get; private set; }

    /// <summary>The stages finished one after another in this drive.</summary>
    public int StagesInRow { get; private set; }

    /// <summary>The gates driven through on a wrong answer or missed on this stage.</summary>
    public int Wrong
    {
        get
        {
            int wrong = 0;
            foreach (var answer in Answers)
                if (answer == false)
                    wrong++;
            return wrong;
        }
    }

    /// <summary>The time of the stage: the seconds driven and a penalty for every wrong gate.</summary>
    public float Time => Driven + Wrong * RallyPlans.PenaltySeconds;

    /// <summary>How far along the ring the car is, while the stage is driven.</summary>
    public float Along => along;

    /// <summary>Where the next thing to drive through is: the start arch, the next gate or the finish.</summary>
    public float GoalDistance =>
        !Racing ? Current.Start
        : NextGate < Current.Gates.Length ? Current.Gates[NextGate]
        : Current.Finish;

    /// <summary>How far the next thing to drive through still is along the ring, while the stage is driven.</summary>
    public float AheadOf => GoalDistance - along;

    /// <summary>The car moved on: whether it drove through the start, a gate or the finish, or left the stage.</summary>
    public RallyEvent Tick(Vector3 car, float dt)
    {
        if (!Racing)
            return WatchStart(car);
        Driven += dt;
        var (now, across, off) = course.Project(car, along, 60);
        float before = along;
        along = now;
        offTime = off > OffCourse ? offTime + dt : 0;
        if (offTime > OffCourseSeconds || along < Current.Start - 30)
            return new RallyEvent(RallyEventKind.Abandoned);
        if (NextGate < Current.Gates.Length)
        {
            float gate = Current.Gates[NextGate];
            if (before < gate && along >= gate)
            {
                int index = NextGate;
                int? lane = RallyCourse.LaneAt(across) is int side ? side + 1 : null;
                NextGate++;
                return new RallyEvent(RallyEventKind.Gate, index, lane);
            }
            return default;
        }
        if (before < Current.Finish && along >= Current.Finish)
            return new RallyEvent(RallyEventKind.Finished);
        return default;
    }

    /// <summary>Before the clock runs: the car is watched near the start arch until it drives through it the right way.</summary>
    private RallyEvent WatchStart(Vector3 car)
    {
        var start = course.At(Current.Start);
        if (new Vector2(car.X - start.X, car.Z - start.Z).Length() > StartWatch)
        {
            watching = false;
            return default;
        }
        var (now, across, _) = course.Project(car, Current.Start, StartWatch + 10);
        bool crossed = watching && along < Current.Start && now >= Current.Start;
        watching = true;
        along = now;
        if (!crossed || MathF.Abs(across) > RallyCourse.GateHalfWidth + 3)
            return default;
        Racing = true;
        Driven = 0;
        offTime = 0;
        return new RallyEvent(RallyEventKind.Started);
    }

    /// <summary>Records the answer of a gate the car drove through.</summary>
    public void Answer(int gate, bool right) => Answers[gate] = right;

    /// <summary>
    /// The stage is over and the next one begins at once, from the same arch, unless the car has been round the whole
    /// ring; returns whether it goes on.
    /// </summary>
    public bool GoOn()
    {
        StagesInRow++;
        if (StagesInRow >= course.Stages.Count)
            return false;
        float finish = Current.Finish;
        Stage = (Stage + 1) % course.Stages.Count;
        // The same arch, maybe a whole round further on: the car stays where it is along the ring.
        along -= finish - Current.Start;
        Answers = new bool?[RallyPlans.GatesPerStage];
        NextGate = 0;
        Driven = 0;
        offTime = 0;
        return true;
    }
}
