using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// Finds the way of an animal's trail over open ground (<c>docs/TRACKING_PLAN.md</c>): the cheapest way through a grid
/// of <see cref="Cell"/> metres (A*), on cells where the car can drive and between cells no steeper than
/// <see cref="MaxSlope"/>, then pulled straight wherever the straight line is open too, and its corners rounded a
/// little; a way any metre of which is closed or too steep is turned down. Steep ground and the roads cost more, so a
/// trail crosses a road rather than follows it. Everything is worked out from the ground alone, without random numbers,
/// so the same valley always gets the same way.
/// </summary>
internal sealed class TrackingPath
{
    /// <summary>The size of a cell of the search, in metres.</summary>
    public const float Cell = 4;

    /// <summary>The steepest rise between two neighbouring cells, or two samples of a straight piece, per metre.</summary>
    public const float MaxSlope = .3f;

    /// <summary>The most cells a single search looks at before it gives up.</summary>
    private const int SearchLimit = 90000;

    private readonly Func<float, float, bool> open;
    private readonly Func<float, float, float> cost;
    private readonly Func<float, float, float> height;
    private readonly Dictionary<long, bool> openCells = new();
    private readonly Dictionary<long, float> cellHeights = new();
    private readonly Dictionary<long, float> cellCosts = new();

    /// <param name="open">Whether the car can drive at a point.</param>
    /// <param name="cost">How much more than plain ground a metre costs at a point (0 for plain ground).</param>
    /// <param name="height">The height of the ground at a point.</param>
    public TrackingPath(Func<float, float, bool> open, Func<float, float, float> cost, Func<float, float, float> height)
    {
        this.open = open;
        this.cost = cost;
        this.height = height;
    }

    private static long Key(int x, int z) => ((long)x << 32) | (uint)z;

    private static float Middle(int i) => (i + .5f) * Cell;

    private bool Open(int x, int z)
    {
        long key = Key(x, z);
        if (!openCells.TryGetValue(key, out bool value))
            openCells[key] = value = open(Middle(x), Middle(z));
        return value;
    }

    private float Height(int x, int z)
    {
        long key = Key(x, z);
        if (!cellHeights.TryGetValue(key, out float value))
            cellHeights[key] = value = height(Middle(x), Middle(z));
        return value;
    }

    private float Cost(int x, int z)
    {
        long key = Key(x, z);
        if (!cellCosts.TryGetValue(key, out float value))
            cellCosts[key] = value = cost(Middle(x), Middle(z));
        return value;
    }

    /// <summary>
    /// The way from <paramref name="origin"/> to <paramref name="target"/> on the flat (Y is 0), or null where none is
    /// found; both ends are kept as they are, and the cells they lie in need not be open. Only cells through which the
    /// way can be at most <paramref name="longest"/> metres long are searched, so a search with no way ends soon.
    /// </summary>
    public List<Vector3>? Find(Vector3 origin, Vector3 target, float longest)
    {
        int sx = Mathf.FloorToInt(origin.X / Cell),
            sz = Mathf.FloorToInt(origin.Z / Cell);
        int gx = Mathf.FloorToInt(target.X / Cell),
            gz = Mathf.FloorToInt(target.Z / Cell);
        long start = Key(sx, sz),
            goal = Key(gx, gz);
        var queue = new PriorityQueue<long, float>();
        var best = new Dictionary<long, float> { [start] = 0 };
        var came = new Dictionary<long, long>();
        var closed = new HashSet<long>();
        float Guess(int x, int z) => FlatMath.Length((gx - x) * Cell, (gz - z) * Cell);
        queue.Enqueue(start, Guess(sx, sz));
        int looked = 0;
        bool found = false;
        while (queue.TryDequeue(out long current, out _))
        {
            if (!closed.Add(current))
                continue;
            if (current == goal)
            {
                found = true;
                break;
            }
            if (++looked > SearchLimit)
                break;
            int cx = (int)(current >> 32),
                cz = (int)(uint)current;
            float here = Height(cx, cz);
            float so = best[current];
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0)
                    continue;
                int nx = cx + dx,
                    nz = cz + dz;
                long next = Key(nx, nz);
                if (closed.Contains(next))
                    continue;
                float mx = Middle(nx),
                    mz = Middle(nz);
                if (
                    FlatMath.Length(mx - origin.X, mz - origin.Z) + FlatMath.Length(target.X - mx, target.Z - mz)
                    > longest + Cell
                )
                    continue;
                if (next != goal && !Open(nx, nz))
                    continue;
                // A diagonal step must not cut the corner of a closed cell.
                if (dx != 0 && dz != 0 && (!Open(cx + dx, cz) || !Open(cx, cz + dz)) && next != goal)
                    continue;
                float run = Cell * (dx != 0 && dz != 0 ? Mathf.Sqrt2 : 1);
                float slope = Mathf.Abs(Height(nx, nz) - here) / run;
                if (slope > MaxSlope)
                    continue;
                float step = run * (1 + Cost(nx, nz) + 5 * slope);
                float total = so + step;
                if (best.TryGetValue(next, out float known) && known <= total)
                    continue;
                best[next] = total;
                came[next] = current;
                queue.Enqueue(next, total + Guess(nx, nz));
            }
        }
        if (!found)
            return null;
        var cells = new List<Vector3>();
        for (long at = goal; ; at = came[at])
        {
            cells.Add(new Vector3(Middle((int)(at >> 32)), 0, Middle((int)(uint)at)));
            if (at == start)
                break;
        }
        cells.Reverse();
        cells[0] = origin with { Y = 0 };
        cells[^1] = target with { Y = 0 };
        var way = Corners(Pulled(cells));
        // Between the middles of two cells the ground may still rise too steeply: every piece is walked metre by metre.
        for (int i = 1; i < way.Count; i++)
            if (!Clear(way[i - 1], way[i]))
                return null;
        return way;
    }

    /// <summary>Whether the straight piece between two points is open all along and nowhere too steep.</summary>
    public bool Clear(Vector3 a, Vector3 b)
    {
        float length = FlatMath.Length(b.X - a.X, b.Z - a.Z);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length));
        float run = length / steps;
        float last = height(a.X, a.Z);
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            float x = a.X + (b.X - a.X) * t,
                z = a.Z + (b.Z - a.Z) * t;
            if (i < steps && !open(x, z))
                return false;
            float h = height(x, z);
            if (Mathf.Abs(h - last) > MaxSlope * run * 1.15f)
                return false;
            last = h;
        }
        return true;
    }

    /// <summary>
    /// Leaves out the points that the straight line from an earlier one reaches past: from each point kept, the line
    /// runs on to the furthest of the following points it reaches without a break, at most <see cref="PullReach"/> on.
    /// </summary>
    private List<Vector3> Pulled(List<Vector3> cells)
    {
        var pulled = new List<Vector3> { cells[0] };
        int at = 0;
        while (at < cells.Count - 1)
        {
            int next = at + 1;
            for (int far = at + 2; far < cells.Count && far - at <= PullReach; far++)
            {
                if (!Clear(cells[at], cells[far]))
                    break;
                next = far;
            }
            pulled.Add(cells[next]);
            at = next;
        }
        return pulled;
    }

    /// <summary>How many cells on a straight piece may reach at most, so that a long trail still bends now and then.</summary>
    private const int PullReach = 18;

    /// <summary>Rounds every corner by cutting it a little, where the cut is open as well.</summary>
    private List<Vector3> Corners(List<Vector3> points)
    {
        if (points.Count < 3)
            return points;
        var rounded = new List<Vector3> { points[0] };
        for (int i = 1; i + 1 < points.Count; i++)
        {
            var corner = points[i];
            var before = points[i - 1] - corner;
            var after = points[i + 1] - corner;
            float cutBefore = Mathf.Min(3, before.Length() * .3f),
                cutAfter = Mathf.Min(3, after.Length() * .3f);
            var a = corner + before.Normalized() * cutBefore;
            var b = corner + after.Normalized() * cutAfter;
            if (Clear(rounded[^1], a) && Clear(a, b) && Clear(b, points[i + 1]))
            {
                rounded.Add(a);
                rounded.Add(b);
            }
            else
                rounded.Add(corner);
        }
        rounded.Add(points[^1]);
        return rounded;
    }

    /// <summary>The flat length of a way.</summary>
    public static float LengthOf(IReadOnlyList<Vector3> points)
    {
        float length = 0;
        for (int i = 1; i < points.Count; i++)
            length += FlatMath.Length(points[i].X - points[i - 1].X, points[i].Z - points[i - 1].Z);
        return length;
    }
}
