using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// A station of the great line (<c>docs/MAINLINE_PLAN.md</c>): a straight piece of track with a platform on the side
/// that faces the road, where the loads are tipped out and the engine driver waits, and the station building across the
/// track. In the station's frame +Z (<see cref="Along"/>) is the way the track runs and +X (<see cref="Right"/>) to its
/// right; the platform is on the side <see cref="Side"/> (+1 right, −1 left) of the track, towards the road.
/// </summary>
/// <param name="Position">The middle of the platform's track, on the ground.</param>
/// <param name="Along">The way the track runs, on the flat.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Along"/>.</param>
/// <param name="Side">The side of the track the platform and the road are on.</param>
/// <param name="Half">Half the length of the platform's track.</param>
internal sealed record LineStationSite(Vector3 Position, Vector3 Along, float Yaw, int Side, float Half)
{
    /// <summary>The ground a station takes, in its frame: the building's side, the road's side, along each way.</summary>
    public const float ZoneBuilding = 15,
        ZoneApron = 14;

    public Vector3 Right => Vector3.Up.Cross(Along);

    /// <summary>A point of the station's frame (across to the right, along the track) on the flat.</summary>
    public Vector3 Local(float across, float along) => Position + Right * across + Along * along;

    /// <summary>Where the loads of the station and its track are tipped out: the apron beyond the platform.</summary>
    public Vector3 Drop => Local(Side * 13, 0);

    /// <summary>Where the engine driver waits, on the platform.</summary>
    public Vector3 Talk => Local(Side * 5.4f, -3);

    /// <summary>Where the car is put down again after a ride, on the apron.</summary>
    public Vector3 Return => Local(Side * 14, 8);

    /// <summary>Where the train stands, on the track.</summary>
    public Vector3 Berth => Local(0, -Half * .35f);

    /// <summary>The track's two ends.</summary>
    public Vector3 Start => Local(0, -Half);

    public Vector3 End => Local(0, Half);
}

/// <summary>
/// The forge beside the mountain slope (<c>docs/MAINLINE_PLAN.md</c>): a stone hut where the stone the trailer brings is
/// forged into rails. The loads are tipped out and loaded at <see cref="Drop"/>, between the forge and the road.
/// </summary>
/// <param name="Foot">The middle of the forge's foot, on the ground.</param>
/// <param name="Drop">Where stone is tipped out and rails are loaded.</param>
/// <param name="Road">The point of the road before the forge, where the way to the drop starts.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z from the forge towards the drop.</param>
internal sealed record LineForgeSite(Vector3 Foot, Vector3 Drop, Vector3 Road, float Yaw)
{
    /// <summary>The ground the forge, its heaps and the place of the loads take round <see cref="Foot"/>.</summary>
    public const float Radius = 7.5f;
}

/// <summary>
/// The line's whole plan beyond what each arm holds: the hub, the three arms with their terminals and the forge.
/// </summary>
/// <param name="Hub">The hub station at the edge of the old valley.</param>
/// <param name="Terminals">The terminal stations of the arms, in the order of the arms.</param>
/// <param name="Arms">The track of each arm: the first from the hub, the others branching off the ones before.</param>
/// <param name="Forge">The forge by the mountain slope.</param>
internal sealed record LineSite(
    LineStationSite Hub,
    LineStationSite[] Terminals,
    LineRoute[] Arms,
    LineForgeSite Forge
);

/// <summary>
/// The track of one arm, from where it leaves the hub or the arm it branches off to its terminal station, as points
/// <see cref="Step"/> metres apart on the ground, with the height of the top of the bed at each: the bed follows the
/// ground, rides on an embankment over small dips and on pillars over deeper ones and over the river. It is a plan only;
/// <c>MainLine</c> draws it, a section at a time, and nothing of the ground changes for it.
/// </summary>
internal sealed class LineRoute
{
    public const float Step = 2.5f;

    /// <summary>The bed lies this far above the ground at least, in a metre, and the track on it a little more.</summary>
    public const float BedLift = .28f;

    /// <summary>The track stays this wide (the bed) and the plants keep this far from its middle.</summary>
    public const float BedHalfWidth = 1.6f,
        ZoneHalfWidth = 3.4f;

    private readonly Vector3[] points;
    private readonly float[] bed;
    private readonly bool[] over;

    /// <summary>The distance along the arm from the junction at which the arm leaves the arm it branches off, 0 for the first.</summary>
    internal float JunctionAlong { get; }

    /// <summary>The arm it branches off, or -1 when it starts at the hub.</summary>
    internal int Parent { get; }

    /// <summary>The stretches of the arm ahead of this one that the train has to run first, to reach the junction.</summary>
    internal LineRoute? Trunk { get; }

    internal int Count => points.Length;

    internal float Length => (points.Length - 1) * Step;

    internal IReadOnlyList<Vector3> Points => points;

    /// <summary>Whether the bed at a point rides over water.</summary>
    internal bool OverWater(int i) => over[i];

    internal LineRoute(Vector3[] points, float[] bed, bool[] over, int parent, LineRoute? trunk, float junctionAlong)
    {
        this.points = points;
        this.bed = bed;
        this.over = over;
        Parent = parent;
        Trunk = trunk;
        JunctionAlong = junctionAlong;
    }

    /// <summary>
    /// The bed along a path: the ground and a little more, on an embankment through small dips, and over the valley's
    /// river a deck of its own above the water, the whole smoothed along its way so that the track rises and falls
    /// gently. A branch starts at the height of the track it leaves.
    /// </summary>
    internal static LineRoute Make(IList<Vector3> points, int parent, LineRoute? trunk, float junction)
    {
        int n = points.Count;
        var floor = new float[n];
        var over = new bool[n];
        for (int i = 0; i < n; i++)
        {
            var p = points[i];
            over[i] = River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 2.5f;
            floor[i] = p.Y + BedLift;
            if (over[i])
                floor[i] = Mathf.Max(floor[i], River.Level(p.Z) + 2.6f);
        }
        var bed = new float[n];
        const int window = 6;
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            int count = 0;
            for (int k = Math.Max(0, i - window); k <= Math.Min(n - 1, i + window); k++)
            {
                sum += floor[k];
                count++;
            }
            bed[i] = Math.Max(sum / count, floor[i]);
        }
        if (trunk != null)
        {
            float level = trunk.At(junction).Y;
            for (int i = 1; i < n && i * Step < 24; i++)
                bed[i] = Math.Max(Mathf.Lerp(level, bed[i], i * Step / 24), floor[i]);
            bed[0] = Math.Max(level, floor[0]);
        }
        return new LineRoute(points.ToArray(), bed, over, parent, trunk, junction);
    }

    /// <summary>The number of the point nearest to a place.</summary>
    internal int Nearest(Vector3 p)
    {
        int best = 0;
        float distance = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = (points[i].X - p.X) * (points[i].X - p.X) + (points[i].Z - p.Z) * (points[i].Z - p.Z);
            if (d < distance)
            {
                distance = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>The point of the ground at the track's middle, at a point number.</summary>
    internal Vector3 Ground(int i) => points[i];

    /// <summary>The height of the top of the bed at a point number.</summary>
    internal float Bed(int i) => bed[i];

    /// <summary>The track's middle at a distance along it, at the height of the bed.</summary>
    internal Vector3 At(float distance)
    {
        float d = Mathf.Clamp(distance, 0, Length) / Step;
        int i = Math.Min((int)MathF.Floor(d), points.Length - 2);
        float t = d - i;
        var p = points[i].Lerp(points[i + 1], t);
        p.Y = Mathf.Lerp(bed[i], bed[i + 1], t);
        return p;
    }

    /// <summary>Which way the track runs at a distance along it, level.</summary>
    internal Vector3 Forward(float distance)
    {
        var delta = (At(distance + Step) - At(distance - Step)) with { Y = 0 };
        return delta.LengthSquared() < .0001f ? Vector3.Forward : delta.Normalized();
    }

    /// <summary>Where the stretches of an arm end: the third, two thirds and all of its length.</summary>
    internal float SectionEnd(int section, int sections) => Length * (section + 1) / sections;

    /// <summary>The distance along the whole way from the hub to the end of this arm, over the trunk it branches off.</summary>
    internal LineRoute FromHub() => Trunk == null ? this : Join(Trunk.FromHub(), JunctionAlong, this);

    /// <summary>The way along the first <paramref name="length"/> metres of one route and then all of the next.</summary>
    internal static LineRoute Join(LineRoute first, float length, LineRoute second)
    {
        int cut = Math.Clamp((int)MathF.Round(length / Step), 1, first.points.Length - 1);
        var pts = new List<Vector3>();
        var heights = new List<float>();
        var water = new List<bool>();
        for (int i = 0; i < cut; i++)
        {
            pts.Add(first.points[i]);
            heights.Add(first.bed[i]);
            water.Add(first.over[i]);
        }
        for (int i = 0; i < second.points.Length; i++)
        {
            pts.Add(second.points[i]);
            heights.Add(second.bed[i]);
            water.Add(second.over[i]);
        }
        return new LineRoute(pts.ToArray(), heights.ToArray(), water.ToArray(), second.Parent, null, 0);
    }

    /// <summary>The same way the other way round.</summary>
    internal LineRoute Reversed()
    {
        int n = points.Length;
        var pts = new Vector3[n];
        var heights = new float[n];
        var water = new bool[n];
        for (int i = 0; i < n; i++)
        {
            pts[i] = points[n - 1 - i];
            heights[i] = bed[n - 1 - i];
            water[i] = over[n - 1 - i];
        }
        return new LineRoute(pts, heights, water, Parent, null, 0);
    }
}
