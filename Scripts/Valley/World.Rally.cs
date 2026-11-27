using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The ferrywoman's rally round the land beyond the old valley (<c>docs/RALLY_PLAN.md</c>): the ring road cut into four
/// stages at the roads to the borders, an arch a little way on from each of them, where one stage ends and the next
/// begins, and three gates with three lanes each on every stage. It is planned once the shepherd's sheep are, from the
/// roads alone and without random numbers. The arches and the gates take no ground: they stand where the road and the
/// ground beside it are open and level and no planned trunk is near, have no collision, and leave every plant and the
/// grass as they were, so nothing that was there moves. The course is held by <c>Tests/Worlds/seed-N.rally.txt</c>.
/// </summary>
public partial class World
{
    /// <summary>The ferrywoman at the border river, the keeper of that border, who holds the rally.</summary>
    internal const int FerrywomanId = ActorIds.FirstBorderKeeper + (int)Border.River;

    /// <summary>An arch stands this far along the ring from the road to its border, and moves on in steps where it cannot.</summary>
    private const float ArchAfterJunction = 30,
        ArchStep = 5;

    /// <summary>The first gate of a stage is this far from its start, so that its question is read in time.</summary>
    private const float FirstGateAfterStart = 190;

    /// <summary>The gates of a stage keep this far apart along the ring, and the last this far from the finish.</summary>
    private const float GateFromGate = 150,
        GateBeforeFinish = 90;

    /// <summary>The rally round the ring; null in a valley where its stages or gates do not fit.</summary>
    internal RallyCourse? Rally { get; private set; }

    /// <summary>The arches and the gates of the rally.</summary>
    internal RallyGates? RallyGates { get; private set; }

    /// <summary>The ferrywoman, who holds the rally.</summary>
    internal Character? Ferrywoman => KeeperOf(Border.River);

    /// <summary>
    /// Lays out the rally: the middle of the ring every 2 m, the four roads to the borders where they leave it, an arch
    /// beyond each and three gates on every stage between two arches. A valley without all four roads, or where an arch
    /// or a gate does not fit, has no rally.
    /// </summary>
    private void PlanRally()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        float length = OutlandRoutes.MainLength;
        int count = Mathf.FloorToInt(length / RallyCourse.Step);
        if (count < 100 || BorderRoads.Length < RallyPlans.Stages)
        {
            GD.Print("RALLY stages=0 reason=roads");
            return;
        }
        var points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var p = OutlandRoutes.SampleMain(i * RallyCourse.Step).Center;
            points[i] = new Vector3(p.X, RallySurface(p.X, p.Z), p.Z);
        }
        var draft = new RallyCourse(points, System.Array.Empty<RallyStage>());
        // Every point where a road meets the ring: the joins from the old roads and the roads to the borders.
        var meetings = OutlandRoutePoints
            .Skip(1)
            .Take(OutlandJoinCount)
            .Select(join => join[^1])
            .Concat(BorderRoads.Select(road => road.Points[0]))
            .ToList();
        var starts = BorderRoads
            .Select(road => (road.Border, Along: NearestAlong(points, road.Points[0])))
            .OrderBy(road => road.Along)
            .ToList();
        var arches = new List<(Border Border, float Along)>();
        foreach (var (border, along) in starts)
        {
            float? arch = null;
            for (int step = 0; step <= 12 && arch == null; step++)
            {
                float d = along + ArchAfterJunction + step * ArchStep;
                if (ArchFits(draft, d, meetings))
                    arch = d;
            }
            if (arch is not float found)
            {
                GD.Print($"RALLY stages=0 reason=arch border={border}");
                return;
            }
            arches.Add((border, Mathf.PosMod(found, draft.Length)));
        }
        // The first stage begins beside the ferrywoman's own road, by the border river.
        int own = arches.FindIndex(arch => arch.Border == Border.River);
        if (own > 0)
            arches = arches.Skip(own).Concat(arches.Take(own)).ToList();
        var stages = new List<RallyStage>();
        for (int s = 0; s < arches.Count; s++)
        {
            var (from, start) = arches[s];
            var (to, end) = arches[(s + 1) % arches.Count];
            float finish = end > start ? end : end + draft.Length;
            var gates = PlanGates(draft, start, finish, meetings);
            if (gates == null)
            {
                GD.Print($"RALLY stages=0 reason=gates stage={s}");
                return;
            }
            stages.Add(new RallyStage(from, to, start, finish, gates));
        }
        Rally = new RallyCourse(points, stages);
        GD.Print(
            $"RALLY stages={stages.Count} length={draft.Length:0} "
                + $"stage-lengths={string.Join(",", stages.Select(s => s.Length.ToString("0")))} ms={clock.ElapsedMilliseconds}"
        );
    }

    /// <summary>The distance along the ring of the point of its middle nearest to <paramref name="p"/>.</summary>
    private static float NearestAlong(Vector3[] points, Vector3 p)
    {
        int best = 0;
        float nearest = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float d = FlatDistance(points[i], p);
            if (d < nearest)
            {
                nearest = d;
                best = i;
            }
        }
        return best * RallyCourse.Step;
    }

    /// <summary>The surface the arches and the gates stand on: the ground, a road, a bridge or the ford.</summary>
    private float RallySurface(float x, float z)
    {
        float ground = Ground.Height(x, z);
        var p = new Vector3(x, ground, z);
        float road = Mathf.Max(Routes.SurfaceHeight(p, ground), OutlandRoutes.SurfaceHeight(p, ground));
        return CrossingSurface(x, z, road);
    }

    /// <summary>An arch stands off the bridges, away from where other roads meet the ring and on dry ground at its posts.</summary>
    private bool ArchFits(RallyCourse course, float along, List<Vector3> meetings)
    {
        var p = course.At(along);
        if (WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 25) != null)
            return false;
        foreach (var meeting in meetings)
            if (FlatDistance(p, meeting) < 22)
                return false;
        var right = course.Right(along);
        foreach (int side in new[] { -1, 1 })
        {
            var post = p + right * side * (RallyCourse.GateHalfWidth + .6f);
            if (!DryAndOpen(post, 1.2f))
                return false;
        }
        return true;
    }

    /// <summary>
    /// The three gates of a stage, each as near a quarter of the way as it fits, then anywhere on the stage, keeping
    /// apart from one another and from the arches; null when one does not fit.
    /// </summary>
    private float[]? PlanGates(RallyCourse course, float start, float finish, List<Vector3> meetings)
    {
        float length = finish - start;
        var gates = new float[RallyPlans.GatesPerStage];
        float earliest = start + FirstGateAfterStart;
        for (int g = 0; g < gates.Length; g++)
        {
            float target = start + length * (g + 1) / (gates.Length + 1);
            float latest = finish - GateBeforeFinish - (gates.Length - 1 - g) * GateFromGate;
            float? found = null;
            foreach (float reach in new[] { length / 8, length / 3 })
            {
                for (float shift = 0; shift <= reach && found == null; shift += 4)
                    foreach (int sign in new[] { 1, -1 })
                    {
                        float d = target + sign * shift;
                        if (found == null && d >= earliest && d <= latest && GateFits(course, d, meetings))
                            found = d;
                    }
                if (found != null)
                    break;
            }
            if (found is not float at)
                return null;
            gates[g] = at;
            earliest = at + GateFromGate;
        }
        return gates;
    }

    /// <summary>
    /// A gate stands on a fairly straight stretch of the ring, off the bridges and where no other road meets it, with
    /// its three lanes over the road and its verges on open, dry ground about as high as the road, and keeps away from the
    /// places of the land beyond the old valley.
    /// </summary>
    private bool GateFits(RallyCourse course, float along, List<Vector3> meetings)
    {
        var p = course.At(along);
        if (WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 40) != null)
            return false;
        if (course.Forward(along - 25).Dot(course.Forward(along + 25)) < .9f)
            return false;
        foreach (var meeting in meetings)
            if (FlatDistance(p, meeting) < 40)
                return false;
        foreach (var spot in SheepSpots)
            if (FlatDistance(p, spot) < 25)
                return false;
        foreach (var trail in TrackingTrails)
            if (FlatDistance(p, trail.Den) < TrackingTrail.DenRadius + 12)
                return false;
        if (
            BalloonPlace is { } balloon && FlatDistance(p, balloon.Pad) < 45
            || QuarryPlace is { } quarry && FlatDistance(p, quarry.Position) < 30
            || LighthousePlace is { } light && FlatDistance(p, light.Foot) < 40
        )
            return false;
        foreach (float on in new[] { -5f, 0, 5 })
        {
            var middle = course.At(along + on);
            var right = course.Right(along + on);
            for (
                float across = -RallyCourse.GateHalfWidth - .5f;
                across <= RallyCourse.GateHalfWidth + .6f;
                across += 1.3f
            )
            {
                var q = middle + right * across;
                if (!DryAndOpen(q, 2.2f))
                    return false;
                if (Mathf.Abs(across) > 3.2f && Mathf.Abs(Ground.Height(q.X, q.Z) - middle.Y) > .75f)
                    return false;
            }
        }
        return true;
    }

    /// <summary>A point out of the water and the sand, off the places of the borders and with no trunk this near.</summary>
    private bool DryAndOpen(Vector3 p, float trunkClearance)
    {
        var borders = WorldLayout.Plan.Borders;
        float h = Ground.Height(p.X, p.Z);
        return River.Distance(p.X, p.Z) > River.HalfWidth(p.Z) + 3
            && borders.BorderRiverDistance(p.X, p.Z) > ValleyBorders.BorderRiverHalfWidth + 3
            && borders.Sand(p.X, p.Z) < .05f
            && h > ValleyBorders.SeaLevel + 2
            && !InBorderfolkZone(p)
            && !Plants.NearTrunk(new Vector3(p.X, h, p.Z), trunkClearance);
    }

    /// <summary>Raises the arches and the gates, and the ferrywoman's otter, who waits beside her for four golds.</summary>
    private void BuildRally()
    {
        if (Rally is not { } course || Ferrywoman is not { } ferrywoman)
            return;
        var gates = new RallyGates(course, RallySurface) { Name = "RallyGates" };
        AddChild(gates);
        Art.Outline(gates, .025f, .3f);
        RallyGates = gates;
        var otter = new CollectorPet(
            WorldPetCatalog.Otter,
            ferrywoman,
            () => gates.Golds,
            RallyPlans.Stages,
            worldPets.Count
        );
        var seat = ferrywoman.Transform * new Vector3(1.5f, 0, .6f);
        otter.SetHome(Ground.At(seat.X, seat.Z), ferrywoman.Rotation.Y);
        AddChild(otter);
        AddWorldPet(otter);
    }
}
