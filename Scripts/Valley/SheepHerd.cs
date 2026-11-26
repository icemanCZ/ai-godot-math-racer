using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Characters;

namespace MathRacer.Valley;

/// <summary>What became of one of the shepherd's sheep.</summary>
internal enum SheepStatus
{
    /// <summary>Out on its meadow, grazing, waiting to be found.</summary>
    Lost,

    /// <summary>Walking behind the car along the way it drove.</summary>
    Following,

    /// <summary>Found, but left behind by a car that drove too fast: it stands and bleats until the car comes back.</summary>
    Waiting,

    /// <summary>In the fold of the shepherd's homestead.</summary>
    Home,
}

/// <summary>
/// The shepherd's flock (<c>docs/SHEEP_PLAN.md</c>) without nodes of the scene: whether the shepherd has sent the player
/// out, which sheep are still lost, which walk behind the car and which are home, and how far behind the car along the
/// way it drove each following sheep walks. A sheep runs at most <see cref="Run"/>, slower than the car's top speed, so
/// a car that drives fast leaves its sheep behind; one that falls <see cref="StrayDistance"/> behind stops and waits.
/// Only whether the player was sent out, the sheep at home and the sheep found are saved; where a found sheep is, is not,
/// and after a load every found sheep walks right behind the car.
/// </summary>
internal sealed class SheepHerd
{
    /// <summary>A lost or waiting sheep joins the car when the car comes this near.</summary>
    public const float FindRadius = 9;

    /// <summary>How fast a sheep runs after the car, in metres a second; the car drives up to 15.</summary>
    public const float Run = 12;

    /// <summary>A sheep this far behind its place stops and waits.</summary>
    public const float StrayDistance = 60;

    /// <summary>Sheep this far behind their places make the corner panel ask for a slower drive.</summary>
    public const float LagWarning = 14;

    /// <summary>Where the first sheep walks behind the car, and how much further back each next one walks.</summary>
    private const float SlotStart = 7.5f,
        SlotSpacing = 1.7f;

    /// <summary>How far to either side of the car's way the sheep walk, in turn, so that they go two by two.</summary>
    private const float SlotSide = .85f;

    /// <summary>A step of the car longer than this is a jump (a reset or a load), which does not leave the sheep behind.</summary>
    private const float Jump = 8;

    private readonly Vector3[] spots;
    private readonly SheepStatus[] status;
    private readonly float[] behind;
    private readonly Vector3[] waitAt;

    /// <summary>The found sheep, following or waiting, in the order they joined.</summary>
    private readonly List<int> order = new();

    /// <summary>The way the car drove, long enough for a sheep that is about to stray.</summary>
    private readonly CompanionPath path = new(480);

    private Vector3 lastCar;
    private bool hasLast;

    public SheepHerd(IReadOnlyList<Vector3> spots)
    {
        this.spots = spots.ToArray();
        status = new SheepStatus[this.spots.Length];
        behind = new float[this.spots.Length];
        waitAt = new Vector3[this.spots.Length];
    }

    /// <summary>Whether this herd grazes on the spots of that valley.</summary>
    public bool Grazes(IReadOnlyList<Vector3> other) =>
        other.Count == spots.Length && Enumerable.Range(0, spots.Length).All(i => spots[i] == other[i]);

    public int Count => spots.Length;

    /// <summary>Whether the shepherd has sent the player out after his sheep.</summary>
    public bool Started { get; private set; }

    public SheepStatus StatusOf(int sheep) => status[sheep];

    /// <summary>Where a sheep grazes while it is lost.</summary>
    public Vector3 Spot(int sheep) => spots[sheep];

    public int HomeCount => status.Count(s => s == SheepStatus.Home);

    public int FollowingCount => status.Count(s => s == SheepStatus.Following);

    public int WaitingCount => status.Count(s => s == SheepStatus.Waiting);

    public int LostCount => Started ? status.Count(s => s == SheepStatus.Lost) : 0;

    /// <summary>Every sheep is home.</summary>
    public bool Complete => spots.Length > 0 && HomeCount == spots.Length;

    /// <summary>The sheep are being looked for: the shepherd sent the player out and not all are home.</summary>
    public bool Active => Started && !Complete;

    /// <summary>The found sheep, following or waiting, in the order they joined; a save holds them.</summary>
    public int[] Found => order.ToArray();

    /// <summary>The sheep at home, by number; a save holds them.</summary>
    public int[] Home => Enumerable.Range(0, spots.Length).Where(i => status[i] == SheepStatus.Home).ToArray();

    /// <summary>The shepherd sends the player out: the sheep appear on their meadows.</summary>
    public void Start()
    {
        if (spots.Length > 0)
            Started = true;
    }

    /// <summary>How far behind the car along its way a following sheep is.</summary>
    public float Behind(int sheep) => behind[sheep];

    /// <summary>How far the sheep furthest behind its place is from it; 0 with no sheep following.</summary>
    public float Lag
    {
        get
        {
            float lag = 0;
            int rank = 0;
            foreach (int sheep in order)
                if (status[sheep] == SheepStatus.Following)
                    lag = Mathf.Max(lag, behind[sheep] - Slot(rank++));
            return lag;
        }
    }

    /// <summary>How far behind the car the sheep that is <paramref name="rank"/>th among those following walks.</summary>
    public static float Slot(int rank) => SlotStart + rank * SlotSpacing;

    /// <summary>
    /// Where a sheep is meant to be now and the way it faces: a following sheep on the car's way, to one side of it, a
    /// waiting one where it stopped and a lost one on its meadow. A sheep at home has no place here (its fold has).
    /// </summary>
    public Vector3 Target(int sheep, Vector3 car, Vector3 back, out Vector3 forward)
    {
        forward = Vector3.Forward;
        switch (status[sheep])
        {
            case SheepStatus.Following:
                int rank = order.Where(s => status[s] == SheepStatus.Following).ToList().IndexOf(sheep);
                var at = path.Sample(car, back, behind[sheep], out forward);
                var right = forward.Cross(Vector3.Up);
                return at + right * (rank % 2 == 0 ? -SlotSide : SlotSide);
            case SheepStatus.Waiting:
                forward = (car - waitAt[sheep]) with { Y = 0 };
                forward = forward.LengthSquared() > .01f ? forward.Normalized() : Vector3.Forward;
                return waitAt[sheep];
            default:
                return spots[sheep];
        }
    }

    /// <summary>
    /// The car moves on: the following sheep run after it as fast as they can, a sheep left too far behind stops, and a
    /// lost or waiting sheep the car comes near joins it. Returns the sheep that joined now.
    /// </summary>
    public List<int> Tick(Vector3 car, Vector3 back, float delta)
    {
        var joined = new List<int>();
        if (!Started)
            return joined;
        float advance = 0;
        if (hasLast)
        {
            var step = (car - lastCar) with { Y = 0 };
            float length = step.Length();
            if (length < Jump)
                advance = step.Dot(back) > 0 ? -length : length;
        }
        lastCar = car;
        hasLast = true;
        path.Record(car, back);
        int rank = 0;
        foreach (int sheep in order.ToArray())
        {
            if (status[sheep] != SheepStatus.Following)
                continue;
            float slot = Slot(rank);
            behind[sheep] = Mathf.Max(slot, behind[sheep] + advance - Run * delta);
            if (behind[sheep] - slot > StrayDistance)
            {
                status[sheep] = SheepStatus.Waiting;
                waitAt[sheep] = path.Sample(car, back, behind[sheep], out _);
                continue;
            }
            rank++;
        }
        for (int sheep = 0; sheep < spots.Length; sheep++)
        {
            if (status[sheep] is not (SheepStatus.Lost or SheepStatus.Waiting))
                continue;
            var at = status[sheep] == SheepStatus.Lost ? spots[sheep] : waitAt[sheep];
            float distance = FlatMath.Length(at.X - car.X, at.Z - car.Z);
            if (distance >= FindRadius)
                continue;
            if (status[sheep] == SheepStatus.Lost)
                order.Add(sheep);
            status[sheep] = SheepStatus.Following;
            behind[sheep] = Mathf.Max(Slot(FollowingCount - 1), distance);
            joined.Add(sheep);
        }
        return joined;
    }

    /// <summary>The sheep that walk behind the car go into the fold. Returns them, in the order they joined.</summary>
    public List<int> Pen()
    {
        var penned = order.Where(sheep => status[sheep] == SheepStatus.Following).ToList();
        foreach (int sheep in penned)
        {
            status[sheep] = SheepStatus.Home;
            order.Remove(sheep);
        }
        return penned;
    }

    /// <summary>
    /// Puts the flock back from a save: the sheep at home in the fold, and every found sheep right behind the car, in
    /// its place. Numbers that this valley does not have are left out.
    /// </summary>
    public void Restore(bool started, int[] home, int[] found)
    {
        Started = started && spots.Length > 0;
        order.Clear();
        System.Array.Fill(status, SheepStatus.Lost);
        foreach (int sheep in home)
            if (sheep >= 0 && sheep < spots.Length)
                status[sheep] = SheepStatus.Home;
        foreach (int sheep in found)
            if (sheep >= 0 && sheep < spots.Length && status[sheep] == SheepStatus.Lost && Started)
            {
                status[sheep] = SheepStatus.Following;
                behind[sheep] = Slot(order.Count);
                order.Add(sheep);
            }
        path.Clear();
        hasLast = false;
    }

    /// <summary>The nearest of the sheep with this status to <paramref name="car"/>, or -1.</summary>
    public int Nearest(SheepStatus wanted, Vector3 car)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int sheep = 0; sheep < spots.Length; sheep++)
        {
            if (status[sheep] != wanted)
                continue;
            var at = wanted == SheepStatus.Waiting ? waitAt[sheep] : spots[sheep];
            float distance = FlatMath.Length(at.X - car.X, at.Z - car.Z);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = sheep;
            }
        }
        return best;
    }

    /// <summary>Where a waiting sheep stopped, or where a lost one grazes.</summary>
    public Vector3 WhereIs(int sheep) => status[sheep] == SheepStatus.Waiting ? waitAt[sheep] : spots[sheep];
}
