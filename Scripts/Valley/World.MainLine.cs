using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The great railway across the valley (<c>docs/MAINLINE_PLAN.md</c>): the hub station at the edge of the old valley,
/// beside the road that joins it to the ring, a track out to the end of the road to each of three borders (the
/// mountains, the sea and the border river), the terminal station beside each of those roads and the forge by the
/// mountain slope. Everything is planned once the castle is, from the roads, the ground and what stands there alone and
/// without random numbers, so nothing planned before moves; the track runs beside the roads where the ground allows,
/// rides on an embankment or on pillars and changes no ground. The plants of the land beyond the old valley that stand
/// in its way are planned as before and only left out when their block is built (<see cref="InBorderfolkZone"/>), the
/// flowers and rocks of the old valley that stand there are hidden. The places are held by
/// <c>Tests/Worlds/seed-N.railway2.txt</c>.
/// </summary>
public partial class World
{
    internal const int EngineerId = ActorIds.Engineer;

    internal static bool IsEngineer(int id) => id == EngineerId;

    /// <summary>The track keeps this far from the middle of the old valley, which is full of the things made there.</summary>
    private const float LineFromCenter = 262;

    /// <summary>The cells the path of a track is searched on are this wide.</summary>
    private const float LineCellSize = 10;

    /// <summary>The hub is tried this far along the join that leaves the old roads, and this far to either side of it.</summary>
    private static readonly float[] LineHubAlong = { 60, 80, 100, 120, 140, 160, 180, 200, 220, 40, 250, 280 };

    private static readonly float[] LineHubAcross = { 24, 30, 36 };

    /// <summary>The hub's platform is this long (half of it), a terminal's this long.</summary>
    private const float LineHubHalf = 30,
        LineTerminalHalf = 16;

    /// <summary>The terminal is tried this far back along the road from its end, and this far from its middle.</summary>
    private static readonly float[] LineTerminalBack = { 70, 95, 55, 120, 40, 150, 180, 210, 240 };

    private static readonly float[] LineTerminalAcross = { 26, 32, 38, 46 };

    /// <summary>Where the hub, the three arms with their terminals and the forge are, or null in a valley without room for them.</summary>
    internal LineSite? MainLinePlace { get; private set; }

    /// <summary>The line, built with seventeen deliveries.</summary>
    internal MainLine? MainLine { get; private set; }

    /// <summary>The forge by the mountain slope.</summary>
    internal LineForge? LineForge { get; private set; }

    /// <summary>The engine driver, Jezevec strojvůdce Kuba.</summary>
    internal Character? Engineer { get; private set; }

    /// <summary>Whether the player builds the line; without the works, the log yard or the glassworks it stands from the start.</summary>
    internal bool MainLineBuildable { get; private set; }

    private List<(Vector2 Low, Vector2 High)> lineOccupied = new();

    private Vector2[] lineSegmentA = Array.Empty<Vector2>(),
        lineSegmentB = Array.Empty<Vector2>();

    private readonly Dictionary<long, List<int>> lineSegments = new();

    /// <summary>How the places were searched, for the log and the checks: the arms found and the nodes the searches visited.</summary>
    internal string LinePlanNote { get; private set; } = "";

    /// <summary>
    /// Chooses the forge, the hub, the terminals and the track of each arm: the first arm runs from the hub, the others
    /// branch off the track already planned, each by the cheapest path across the ground that keeps clear of everything.
    /// </summary>
    private void PlanMainLine()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (SlopePlace is not { } slope || OutlandJoinCount == 0)
        {
            GD.Print("MAINLINE line=none reason=places");
            return;
        }
        lineOccupied = OccupiedSpots();
        if (PlanLineForge(slope) is not { } forge)
        {
            GD.Print($"MAINLINE line=none reason=forge why={LineWhyNote()}");
            return;
        }
        if (PlanLineHub() is not { } hub)
        {
            GD.Print($"MAINLINE line=none reason=hub why={LineWhyNote()}");
            return;
        }
        var routes = new List<LineRoute>();
        var terminals = new List<LineStationSite>();
        int visited = 0;
        for (int arm = 0; arm < MainLinePlans.Arms; arm++)
        {
            var border = MainLinePlans.ArmBorder(arm);
            var road = BorderRoads.FirstOrDefault(r => r.Border == border).Points;
            if (road == null || PlanLineTerminal(road, hub, terminals) is not { } terminal)
            {
                GD.Print($"MAINLINE line=none reason=terminal{arm} why={LineWhyNote()}");
                return;
            }
            var goal = terminal.Start with { Y = 0 };
            // The first arm leaves the hub straight on; the others leave any point of the track already planned.
            var sources = new List<(Vector3 Point, int Route, int Index)>();
            if (arm == 0)
                sources.Add((hub.Local(0, hub.Half + 24), -1, 0));
            else
                for (int r = 0; r < routes.Count; r++)
                for (int i = 0; i < routes[r].Count; i += 4)
                    sources.Add((routes[r].Ground(i), r, i));
            var path = LineSearch(sources, goal, out var origin, ref visited);
            if (path == null)
            {
                GD.Print($"MAINLINE line=none reason=path{arm} visited={visited}");
                return;
            }
            // The path is made of points 2.5 m apart already (and moved clear of the trees); only the straight pieces at its
            // ends, in the hub and in the terminal, are sampled here.
            var points = new List<Vector3>();
            if (arm == 0)
            {
                points.AddRange(LineResample(new List<Vector3> { hub.End, hub.Local(0, hub.Half + 24) }));
                points.RemoveAt(points.Count - 1);
            }
            points.AddRange(path);
            var tail = LineResample(new List<Vector3> { path[^1], terminal.Local(0, terminal.Half) });
            points.AddRange(tail.Skip(1));
            if (points.Count < 8)
            {
                GD.Print($"MAINLINE line=none reason=short{arm}");
                return;
            }
            var (parent, junction) = arm == 0 ? (-1, 0f) : (origin.Route, origin.Index * LineRoute.Step);
            routes.Add(LineRoute.Make(points, parent, parent >= 0 ? routes[parent] : null, junction));
            terminals.Add(terminal);
        }
        MainLinePlace = new LineSite(hub, terminals.ToArray(), routes.ToArray(), forge);
        LinePlanNote = $"visited={visited}";
        IndexLine();
        // The flowers and rocks of the old valley that the track passes are hidden, like those on a join.
        Plants.ClearDrawn(p =>
            Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) <= WorldTuning.CoreRadius && InMainLineZone(p)
        );
        GD.Print(
            $"MAINLINE line=true arms={string.Join('/', routes.Select(r => (int)r.Length))} visited={visited} ms={clock.ElapsedMilliseconds}"
        );
    }

    /// <summary>
    /// The forge beside the mountain slope: the first of a few places to either side of the slope that is open, dry and
    /// nearly level, with the place of the loads between it and the road.
    /// </summary>
    private LineForgeSite? PlanLineForge(MountainSlopeSite slope)
    {
        var inward = (slope.Position - slope.Road) with { Y = 0 };
        if (inward.LengthSquared() < 1)
            return null;
        inward = inward.Normalized();
        var right = Vector3.Up.Cross(inward);
        foreach (float along in new[] { 0f, 8f, -8f, 16f, -16f, 24f })
        foreach (float across in new[] { 20f, -20f, 25f, -25f, 30f, -30f, 36f, -36f, 42f, -42f })
        {
            var foot = slope.Position + right * across + inward * along;
            var toward = (slope.Road - foot) with { Y = 0 };
            if (toward.LengthSquared() < 1)
                continue;
            var drop = foot + toward.Normalized() * 8.5f;
            if (!LineForgeFits(foot, drop))
                continue;
            return new LineForgeSite(
                Ground.At(foot.X, foot.Z),
                Ground.At(drop.X, drop.Z),
                slope.Road,
                Mathf.Atan2(toward.X, toward.Z)
            );
        }
        return null;
    }

    private bool LineForgeFits(Vector3 foot, Vector3 drop)
    {
        float low = float.MaxValue,
            high = float.MinValue;
        for (float dx = -LineForgeSite.Radius; dx <= LineForgeSite.Radius; dx += 1.75f)
        for (float dz = -LineForgeSite.Radius; dz <= LineForgeSite.Radius; dz += 1.75f)
        {
            var p = foot + new Vector3(dx, 0, dz);
            if (LineWhy(p) is { } why)
            {
                LineCount("forge-" + why);
                return false;
            }
            if (OutlandRoutes.Distance(p) < 5)
            {
                LineCount("forge-road");
                return false;
            }
            float h = Ground.Height(p.X, p.Z);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        if (high - low > 2.4f)
        {
            LineCount("forge-relief");
            return false;
        }
        return LineCell(drop) == 0 && !InDragonZone(drop);
    }

    /// <summary>
    /// The hub: beside the road of the first join that leaves an old road, far enough along it to be clear of the old
    /// valley, on whichever side has a level, free piece of ground for the platform, the apron before it and the
    /// station building across the track.
    /// </summary>
    private LineStationSite? PlanLineHub()
    {
        var joins = OutlandRoutePoints
            .Skip(1)
            .Take(OutlandJoinCount)
            .OrderBy(join => FlatDistance(join[0], Village.Center))
            .ToList();
        foreach (float relief in new[] { 3f, 4.5f, 6.5f })
        foreach (var join in joins)
        {
            using var curve = WorldPlan.CreateCurve(join);
            float length = curve.GetBakedLength();
            foreach (float across in LineHubAcross)
            foreach (float along in LineHubAlong)
            foreach (int side in new[] { 1, -1 })
            {
                if (along + 6 > length)
                    continue;
                var middle = curve.SampleBaked(along);
                var way = (curve.SampleBaked(along + 3) - curve.SampleBaked(along - 3)) with { Y = 0 };
                if (way.LengthSquared() < .01f)
                    continue;
                way = way.Normalized();
                var at = middle + Vector3.Up.Cross(way) * side * across;
                var site = new LineStationSite(
                    Ground.At(at.X, at.Z),
                    way,
                    Mathf.Atan2(way.X, way.Z),
                    -side,
                    LineHubHalf
                );
                if (LineStationFits(site, relief, LineHubHalf + 26))
                    return site;
            }
        }
        return null;
    }

    /// <summary>
    /// The terminal of an arm: beside the road to its border a little way back from the homestead at its end, on the
    /// side that has room, its platform turned towards the road.
    /// </summary>
    private LineStationSite? PlanLineTerminal(Vector3[] road, LineStationSite hub, List<LineStationSite> others)
    {
        using var curve = WorldPlan.CreateCurve(road);
        float length = curve.GetBakedLength();
        foreach (float relief in new[] { 2.5f, 4f, 6f, 9f })
        foreach (float back in LineTerminalBack)
        foreach (float across in LineTerminalAcross)
        foreach (int side in new[] { 1, -1 })
        {
            float along = length - back;
            if (along < 20)
                continue;
            var middle = curve.SampleBaked(along);
            var way = (curve.SampleBaked(along + 3) - curve.SampleBaked(along - 3)) with { Y = 0 };
            if (way.LengthSquared() < .01f)
                continue;
            way = way.Normalized();
            var at = middle + Vector3.Up.Cross(way) * side * across;
            var site = new LineStationSite(
                Ground.At(at.X, at.Z),
                way,
                Mathf.Atan2(way.X, way.Z),
                -side,
                LineTerminalHalf
            );
            // The arm comes in over 34 m of straight track before the platform.
            if (
                others.Any(o => FlatDistance(o.Position, site.Position) < 80)
                || FlatDistance(hub.Position, site.Position) < 200
                || !LineStationFits(site, relief, LineTerminalHalf + 36)
            )
                continue;
            return site;
        }
        return null;
    }

    /// <summary>
    /// Whether a station's ground is free and nearly level: the platform's track and the lead-in before it, the building
    /// across the track and the apron on the road's side, off every road.
    /// </summary>
    private bool LineStationFits(LineStationSite site, float relief, float reach)
    {
        float low = float.MaxValue,
            high = float.MinValue;
        for (float along = -reach; along <= site.Half + 6; along += 4)
        for (float across = -18; across <= 18; across += 3.6f)
        {
            // Beyond the platform's own length only the track's lead-in has to be free.
            if (along < -site.Half - 4 && Mathf.Abs(across) > 4)
                continue;
            var p = site.Local(across, along);
            if (LineWhy(p) is { } why)
            {
                LineCount("station-" + why);
                return false;
            }
            if (Routes.Distance(p) < 6 || OutlandRoutes.Distance(p) < 6)
            {
                LineCount("station-road");
                return false;
            }
            if (along >= -site.Half - 4)
            {
                float h = Ground.Height(p.X, p.Z);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
        }
        if (high - low > relief)
        {
            LineCount("station-relief");
            return false;
        }
        return true;
    }

    /// <summary>
    /// What a track may not cross: the sea, the border river, the old valley's bridges, the ground of anything planned or
    /// made, a tree, and the old valley's middle. Water that the valley's own river fills is not hard (a viaduct crosses it).
    /// </summary>
    private bool LineHard(Vector3 p) => LineWhy(p) != null;

    /// <summary>The reason a track may not cross a point, as a short name for the log, or null where it may.</summary>
    private string? LineWhy(Vector3 p, float treeClearance = 4.5f)
    {
        if (!ValleyBounds.Contains(p.X, p.Z, 30))
            return "edge";
        if (FlatMath.Length(p.X, p.Z) < LineFromCenter)
            return "center";
        var borders = WorldLayout.Plan.Borders;
        if (borders.Sand(p.X, p.Z) > .05f || Ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + 2.5f)
            return "sea";
        if (borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 8)
            return "border-river";
        if (
            WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 14) != null
            || River.OnBridge(p.X, p.Z)
            || (Mathf.Abs(p.Z - WorldLayout.Plan.BridgeZ) < 55 && River.Distance(p.X, p.Z) < 75)
        )
            return "bridge";
        if (InBorderfolkZone(p) || NearCastleSights(p))
            return "sights";
        if (
            Village.IsNearVillage(p)
            || Village.IsReserved(p)
            || Tracks?.Contains(p) == true
            || InGlassworksZone(p)
            || InFarmZone(p)
            || InBakeryZone(p)
            || InPlaygroundZone(p)
            || InZooZone(p)
            || InHunterZone(p)
        )
            return "village";
        // The trees of the land beyond the old valley are left out where the line's ground is (InMainLineZone); those of
        // the old valley stand, and the track goes round them.
        if (FlatMath.Length(p.X, p.Z) <= WorldTuning.CorePlayableRadius + 10 && Plants.NearTrunk(p, treeClearance))
            return "tree";
        var flat = new Vector2(p.X, p.Z);
        foreach (var box in lineOccupied)
            if (flat.X > box.Low.X - 6 && flat.X < box.High.X + 6 && flat.Y > box.Low.Y - 6 && flat.Y < box.High.Y + 6)
                return "made";
        return null;
    }

    private readonly Dictionary<string, int> lineWhy = new();

    private void LineCount(string why) => lineWhy[why] = lineWhy.GetValueOrDefault(why) + 1;

    private string LineWhyNote() =>
        string.Join(',', lineWhy.OrderByDescending(pair => pair.Value).Select(pair => $"{pair.Key}:{pair.Value}"));

    /// <summary>0 for ground a track may be laid on, 1 for the valley's river, 2 for ground it may not cross.</summary>
    private int LineCell(Vector3 p)
    {
        if (LineHard(p))
            return 2;
        return River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 4 ? 1 : 0;
    }

    /// <summary>
    /// The cheapest way over the ground from any of the <paramref name="sources"/> to the goal, as the middles of the
    /// cells it passes, smoothed where the smoothing keeps clear; null where there is none.
    /// </summary>
    private List<Vector3>? LineSearch(
        List<(Vector3 Point, int Route, int Index)> sources,
        Vector3 goal,
        out (int Route, int Index) origin,
        ref int visited
    )
    {
        origin = (-1, 0);
        float x0 = Mathf.Min(sources.Min(s => s.Point.X), goal.X) - 330,
            z0 = Mathf.Min(sources.Min(s => s.Point.Z), goal.Z) - 330;
        float x1 = Mathf.Max(sources.Max(s => s.Point.X), goal.X) + 330,
            z1 = Mathf.Max(sources.Max(s => s.Point.Z), goal.Z) + 330;
        x0 = Mathf.Max(x0, -ValleyBounds.HalfSize);
        z0 = Mathf.Max(z0, -ValleyBounds.HalfSize);
        x1 = Mathf.Min(x1, ValleyBounds.HalfSize);
        z1 = Mathf.Min(z1, ValleyBounds.HalfSize);
        int width = Mathf.CeilToInt((x1 - x0) / LineCellSize),
            height = Mathf.CeilToInt((z1 - z0) / LineCellSize);
        int count = width * height;
        var state = new sbyte[count];
        var heights = new float[count];
        var roadCost = new float[count];
        var cost = new float[count];
        var from = new int[count];
        var start = new int[count];
        var closed = new bool[count];
        Array.Fill(cost, float.PositiveInfinity);
        Array.Fill(from, -1);
        Array.Fill(start, -1);
        Vector3 Center(int cell) =>
            new(x0 + (cell % width + .5f) * LineCellSize, 0, z0 + (cell / width + .5f) * LineCellSize);
        int CellOf(Vector3 p)
        {
            int cx = Mathf.Clamp((int)((p.X - x0) / LineCellSize), 0, width - 1);
            int cz = Mathf.Clamp((int)((p.Z - z0) / LineCellSize), 0, height - 1);
            return cx + cz * width;
        }
        int goalCell = CellOf(goal);
        int State(int cell)
        {
            if (state[cell] == 0)
            {
                var c = Center(cell);
                state[cell] = (sbyte)((cell == goalCell ? 0 : LineCell(c)) + 1);
                heights[cell] = Ground.Height(c.X, c.Z);
                // Staying on a road is dear, crossing it a little dear, and the verge of a road is cheap.
                roadCost[cell] =
                    OutlandRoutes.Distance(c) < 4.2f || Routes.Distance(c) < 4.8f ? 40
                    : OutlandRoutes.Distance(c) < 18 ? -.2f
                    : 0;
            }
            return state[cell] - 1;
        }
        var open = new PriorityQueue<int, float>();
        float Estimate(int cell) => FlatDistance(Center(cell), goal) * .75f;
        for (int s = 0; s < sources.Count; s++)
        {
            int cell = CellOf(sources[s].Point);
            if (cost[cell] == 0)
                continue;
            // A start that lies on a blocked cell is still a start: the track is there already.
            state[cell] = 1;
            heights[cell] = Ground.Height(Center(cell).X, Center(cell).Z);
            cost[cell] = 0;
            start[cell] = s;
            open.Enqueue(cell, Estimate(cell));
        }
        int found = -1;
        int[] dx =  { 1, -1, 0, 0, 1, 1, -1, -1 },
            dz =  { 0, 0, 1, -1, 1, -1, 1, -1 };
        while (open.TryDequeue(out int cell, out _))
        {
            if (closed[cell])
                continue;
            closed[cell] = true;
            visited++;
            if (cell == goalCell)
            {
                found = cell;
                break;
            }
            int cx = cell % width,
                cz = cell / width;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + dx[k],
                    nz = cz + dz[k];
                if (nx < 0 || nz < 0 || nx >= width || nz >= height)
                    continue;
                int next = nx + nz * width;
                if (closed[next])
                    continue;
                int kind = State(next);
                if (kind == 2)
                    continue;
                float distance = k < 4 ? LineCellSize : LineCellSize * 1.4142f;
                float slope = Mathf.Abs(heights[next] - heights[cell]) / distance;
                if (slope > .42f)
                    continue;
                float step = distance * (1 + 14 * Mathf.Max(0, slope - .1f));
                if (kind == 1)
                    step += 90;
                step = roadCost[next] > 0 ? step + roadCost[next] : step * (1 + roadCost[next]);
                float candidate = cost[cell] + step;
                if (candidate >= cost[next])
                    continue;
                cost[next] = candidate;
                from[next] = cell;
                start[next] = start[cell];
                open.Enqueue(next, candidate + Estimate(next));
            }
        }
        if (found < 0)
            return null;
        var cells = new List<int>();
        for (int c = found; c >= 0; c = from[c])
            cells.Add(c);
        cells.Reverse();
        var source = sources[start[found]];
        origin = (source.Route, source.Index);
        var raw = new List<Vector3> { source.Point };
        for (int i = 1; i < cells.Count - 1; i++)
            raw.Add(Center(cells[i]));
        raw.Add(goal);
        // Pull the path straight where the ground allows, so that the staircase of the cells goes, then round its corners
        // as far as the ground allows: the smoothest of the paths that stay clear.
        var straight = LineShortcut(raw);
        List<Vector3> points = raw;
        for (int rounds = 3; rounds >= 0; rounds--)
        {
            var smooth = rounds == 0 ? straight : LineChaikin(straight, rounds);
            // Every point the track will really have, not only the corners, keeps clear of everything but a tree's far edge.
            points = LineResample(smooth);
            LineNudge(points);
            if (points.Skip(2).Take(Math.Max(0, points.Count - 4)).All(p => LineWhy(p, 2.8f) == null))
                break;
        }
        return points;
    }

    /// <summary>
    /// Moves the points of a track that lie close to a tree of the old valley sideways, to the nearest free place: the
    /// cells the path was found on are wider than the room between two trees, and a tree cannot be taken away.
    /// </summary>
    private void LineNudge(List<Vector3> points)
    {
        for (int pass = 0; pass < 4; pass++)
        for (int i = 2; i < points.Count - 2; i++)
        {
            var p = points[i];
            if (FlatMath.Length(p.X, p.Z) > WorldTuning.CorePlayableRadius + 10 || !Plants.NearTrunk(p, 2.8f))
                continue;
            var way = (points[i + 1] - points[i - 1]) with { Y = 0 };
            if (way.LengthSquared() < .01f)
                continue;
            var side = Vector3.Up.Cross(way.Normalized());
            for (float d = .8f; d <= 3.5f; d += .5f)
            {
                var moved = false;
                foreach (int s in new[] { 1, -1 })
                {
                    var q = p + side * (s * d);
                    if (LineWhy(q, 2.8f) != null)
                        continue;
                    points[i] = q;
                    moved = true;
                    break;
                }
                if (moved)
                    break;
            }
        }
    }

    /// <summary>
    /// Drops the points of a path whose neighbours can be joined by a straight line that is free and not steep all the
    /// way (and keeps the points on the river, where the path crosses it as it was found).
    /// </summary>
    private List<Vector3> LineShortcut(List<Vector3> raw)
    {
        var result = new List<Vector3> { raw[0] };
        int at = 0;
        while (at < raw.Count - 1)
        {
            int best = at + 1;
            for (int j = Math.Min(raw.Count - 1, at + 12); j > at + 1; j--)
                if (LineClear(raw[at], raw[j]))
                {
                    best = j;
                    break;
                }
            result.Add(raw[best]);
            at = best;
        }
        return result;
    }

    /// <summary>Whether a straight line from one point to another stays on free ground that rises or falls gently.</summary>
    private bool LineClear(Vector3 a, Vector3 b)
    {
        float length = FlatDistance(a, b);
        int steps = Math.Max(1, (int)(length / 5));
        float last = Ground.Height(a.X, a.Z);
        for (int i = 1; i <= steps; i++)
        {
            var p = a.Lerp(b, i / (float)steps);
            if (LineCell(p) != 0)
                return false;
            float h = Ground.Height(p.X, p.Z);
            if (Mathf.Abs(h - last) > .32f * length / steps)
                return false;
            last = h;
        }
        return true;
    }

    private static List<Vector3> LineChaikin(List<Vector3> raw, int rounds)
    {
        var points = raw;
        for (int round = 0; round < rounds; round++)
        {
            var next = new List<Vector3> { points[0] };
            for (int i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                next.Add(a.Lerp(b, .25f));
                next.Add(a.Lerp(b, .75f));
            }
            next.Add(points[^1]);
            points = next;
        }
        return points;
    }

    /// <summary>The points of a path, <see cref="LineRoute.Step"/> metres apart along it, on the ground.</summary>
    private List<Vector3> LineResample(List<Vector3> raw)
    {
        var result = new List<Vector3>();
        float carry = 0;
        var last = raw[0];
        result.Add(last);
        for (int i = 1; i < raw.Count; i++)
        {
            var b = raw[i];
            float segment = FlatDistance(last, b);
            if (segment < .01f)
                continue;
            var direction = (b - last) with { Y = 0 };
            direction = direction.Normalized();
            float at = LineRoute.Step - carry;
            while (at <= segment)
            {
                result.Add(last + direction * at);
                at += LineRoute.Step;
            }
            carry = segment - (at - LineRoute.Step);
            last = b;
        }
        if (FlatDistance(result[^1], last) > .5f)
            result.Add(last);
        for (int i = 0; i < result.Count; i++)
            result[i] = Ground.At(result[i].X, result[i].Z);
        return result;
    }

    /// <summary>Makes the zone of the line: the track's segments in a grid, for the plants and the wild animals to ask.</summary>
    private void IndexLine()
    {
        var a = new List<Vector2>();
        var b = new List<Vector2>();
        void AddSegment(Vector3 from, Vector3 to)
        {
            a.Add(new Vector2(from.X, from.Z));
            b.Add(new Vector2(to.X, to.Z));
        }
        var site = MainLinePlace!;
        AddSegment(site.Hub.Start, site.Hub.End);
        foreach (var terminal in site.Terminals)
            AddSegment(terminal.Start, terminal.End);
        foreach (var arm in site.Arms)
            for (int i = 0; i + 1 < arm.Count; i++)
                AddSegment(arm.Ground(i), arm.Ground(i + 1));
        lineSegmentA = a.ToArray();
        lineSegmentB = b.ToArray();
        lineSegments.Clear();
        for (int i = 0; i < lineSegmentA.Length; i++)
        {
            var low = lineSegmentA[i].Min(lineSegmentB[i]) - Vector2.One * LineRoute.ZoneHalfWidth;
            var high = lineSegmentA[i].Max(lineSegmentB[i]) + Vector2.One * LineRoute.ZoneHalfWidth;
            for (int cx = (int)MathF.Floor(low.X / 8); cx <= (int)MathF.Floor(high.X / 8); cx++)
            for (int cz = (int)MathF.Floor(low.Y / 8); cz <= (int)MathF.Floor(high.Y / 8); cz++)
            {
                long key = ((long)cx << 32) ^ (uint)cz;
                if (!lineSegments.TryGetValue(key, out var list))
                    lineSegments[key] = list = new List<int>();
                list.Add(i);
            }
        }
    }

    /// <summary>
    /// The ground of the great line: the track's bed with a little to each side, the stations with their platforms, aprons
    /// and buildings, and the forge with the place of its loads. The plants, the grass, the wayside things and the wild
    /// animals of the land beyond the old valley keep off.
    /// </summary>
    internal bool InMainLineZone(Vector3 p)
    {
        if (MainLinePlace is not { } site)
            return false;
        int cx = (int)MathF.Floor(p.X / 8),
            cz = (int)MathF.Floor(p.Z / 8);
        if (lineSegments.TryGetValue(((long)cx << 32) ^ (uint)cz, out var near))
            foreach (int i in near)
                if (
                    FlatMath.SegmentDistance(
                        p.X,
                        p.Z,
                        lineSegmentA[i].X,
                        lineSegmentA[i].Y,
                        lineSegmentB[i].X,
                        lineSegmentB[i].Y
                    ) < LineRoute.ZoneHalfWidth
                )
                    return true;
        bool InStation(LineStationSite s) =>
            InSiteRectangle(p, s.Position, s.Along, -s.Half - 5, s.Half + 5, LineStationSite.ZoneBuilding + 1);
        if (InStation(site.Hub))
            return true;
        foreach (var terminal in site.Terminals)
            if (InStation(terminal))
                return true;
        var forge = site.Forge;
        return FlatDistance(p, forge.Foot) < LineForgeSite.Radius + 1 || SegmentDistance(p, forge.Foot, forge.Drop) < 3;
    }

    /// <summary>
    /// Raises the line (not begun, or finished where the works, the log yard or the glassworks are missing), the forge,
    /// the engine driver and the mole that waits beside him until the whole line is laid.
    /// </summary>
    private void BuildMainLine()
    {
        if (MainLinePlace is not { } site)
            return;
        MainLineBuildable =
            LogYard != null
            && WorksFor(Product.Planks) != null
            && WorksFor(Product.Bricks) != null
            && WorksFor(Product.Gravel) != null
            && WorksFor(Product.Glass) != null;
        var forge = new LineForge(site.Forge, Ground);
        AddChild(forge);
        Art.Outline(forge, .025f, .3f);
        LineForge = forge;
        var line = new MainLine(site, Ground) { Forge = forge };
        line.SetStage(MainLineBuildable ? 0 : MainLinePlans.Trips);
        AddChild(line);
        Art.Outline(line, .025f, .3f);
        MainLine = line;
        var engineer = Character.Create(CharacterCatalog.Engineer);
        engineer.Role = new EngineerRole();
        engineer.DressAsEngineer();
        line.Engineer = engineer;
        var talk = site.Hub.Talk;
        engineer.Position = new Vector3(talk.X, line.StationTop(site.Hub), talk.Z);
        engineer.Rotation = new Vector3(0, site.Hub.Yaw + (site.Hub.Side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2), 0);
        AddChild(engineer);
        Engineer = engineer;
        var mole = new CollectorPet(
            WorldPetCatalog.Mole,
            engineer,
            () => line.Stage,
            MainLinePlans.Trips,
            worldPets.Count
        );
        var seat = site.Hub.Local(site.Hub.Side * 6.6f, -5.2f);
        mole.SetHome(new Vector3(seat.X, line.StationTop(site.Hub), seat.Z), site.Hub.Yaw);
        AddChild(mole);
        AddWorldPet(mole);
    }

    /// <summary>The engine driver is registered after the princess, who came before him.</summary>
    private void RegisterEngineer()
    {
        if (Engineer != null)
            Register(EngineerId, Engineer);
    }
}
