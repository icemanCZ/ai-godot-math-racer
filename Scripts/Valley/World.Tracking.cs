using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The tracks of the forest keeper (<c>docs/TRACKING_PLAN.md</c>): six trails of footprints that lead, one after another,
/// from beside the keeper's homestead in the forest out over the land beyond the old valley, each to the den of an
/// animal, where the next one begins. The dens and the ways are chosen once every plant of the land beyond the old valley
/// is planned, with random numbers of their own (<see cref="TrackingSeed"/>), so that the ways keep off the trees; nothing
/// that was there moves, and the plants that would stand on a den are planned as before and only left out when their
/// block is built (<see cref="InBorderfolkZone"/>). The trails are held by <c>Tests/Worlds/seed-N.tracking.txt</c>.
/// </summary>
public partial class World
{
    internal const int TrackingAnimalId = ActorIds.TrackingAnimal;

    internal static bool IsTrackingAnimal(int id) => id == TrackingAnimalId;

    /// <summary>The random numbers of the trails are their own, so nothing planned before them changes.</summary>
    private const int TrackingSeed = 8123;

    /// <summary>The trails in the order they are followed; empty in a valley without the forest keeper or room for them.</summary>
    internal IReadOnlyList<TrackingTrail> TrackingTrails { get; private set; } = System.Array.Empty<TrackingTrail>();

    /// <summary>The dens with the animals in them.</summary>
    internal TrackingDens? TrackingDens { get; private set; }

    /// <summary>The footprints of the trail being followed.</summary>
    internal TrackingPrints? TrackingPrints { get; private set; }

    /// <summary>The animal to coax out of its den: a character without a body of its own, in front of the den.</summary>
    internal Character? TrackingAnimal { get; private set; }

    /// <summary>The forest keeper, who keeps the book of tracks.</summary>
    internal Character? Forester => KeeperOf(Border.Forest);

    /// <summary>How far apart in a straight line a den lies from the previous one, for the first trail and the last.</summary>
    private const float TrailReachFirst = 270,
        TrailReachLast = 440;

    /// <summary>A trail's way may be at most this many times longer than the straight line between its ends.</summary>
    private const float TrailDetour = 1.6f;

    /// <summary>How many times a den is chosen again because none fits after it, before the valley gets no trails.</summary>
    private const int TrailBacktracks = 12;

    /// <summary>How many dens one trail tries before the valley is left without trails.</summary>
    private const int TrailAttempts = 300;

    /// <summary>The dens keep this far from the homesteads of the borders, the lighthouse, the dragon and the balloon.</summary>
    private const float DenFromSights = 90;

    /// <summary>The dens keep this far from each other, so that every animal has a corner of the land of its own.</summary>
    private const float DenFromDen = 200;

    /// <summary>The trails keep this much room from a trunk on either side, enough for the car to pass.</summary>
    private const float TrailTreeClearance = 3.2f;

    /// <summary>
    /// Chooses the dens and the ways between them: from beside the forest keeper's homestead, each den some way further
    /// round the valley, in the land between the ring road and the borders, and the way to it found over the open ground
    /// (<see cref="TrackingPath"/>); where they cannot all be found from one point beside the homestead, from another. A
    /// valley where they cannot be found from any gets none of them.
    /// </summary>
    private void PlanTracking()
    {
        if (BorderHomes[(int)Border.Forest] is not { } home)
            return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var rng = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + TrackingSeed) };
        var finder = new TrackingPath(TrailOpen, TrailCost, Ground.Height);
        // Round the valley the way that leads from the forest towards the mountains first.
        float turn = home.Road.X < 0 ? 1 : -1;
        int starts = 0;
        foreach (var start in TrailStarts(home))
        {
            if (!StartOpen(start))
                continue;
            starts++;
            if (PlanLegs(start, turn, finder, rng, out int backs) is not { } trails)
                continue;
            TrackingTrails = trails;
            GD.Print(
                $"TRACKING trails={trails.Count} length={trails.Sum(t => t.Length):0} starts={starts} backtracks={backs}"
                    + $" ms={clock.ElapsedMilliseconds}"
            );
            return;
        }
        GD.Print($"TRACKING trails=0 starts={starts} ms={clock.ElapsedMilliseconds}");
    }

    /// <summary>
    /// Where the first trail may begin, nearest first: beside the end of the road and the homestead, further out where
    /// the trees stand close, and at last on the road a little back from its end.
    /// </summary>
    private static IEnumerable<Vector3> TrailStarts(BorderHomeSite home)
    {
        foreach (float across in new[] { 5f, 10f, 16f, 24f })
        foreach (float along in new[] { 0f, 8f, -6f, 16f, -12f, 24f })
        foreach (int side in new[] { -1, 1 })
            yield return home.Local(side * (BorderHomeSite.AcrossMax + across), along) with
            {
                Y = 0,
            };
        foreach (float back in new[] { 10f, 18f, 28f })
            yield return home.Local(0, -back) with
            {
                Y = 0,
            };
    }

    /// <summary>A trail can begin at a point where the ground is open all around it, a car's length each way.</summary>
    private bool StartOpen(Vector3 p)
    {
        if (!TrailOpen(p.X, p.Z))
            return false;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8;
            if (!TrailOpen(p.X + 4.5f * Mathf.Cos(angle), p.Z + 4.5f * Mathf.Sin(angle)))
                return false;
        }
        return true;
    }

    /// <summary>
    /// The six trails from <paramref name="start"/>, or null where they cannot all be found: a den after which none fits
    /// is chosen again somewhere else, up to <see cref="TrailBacktracks"/> times.
    /// </summary>
    private List<TrackingTrail>? PlanLegs(
        Vector3 start,
        float turn,
        TrackingPath finder,
        RandomNumberGenerator rng,
        out int backs
    )
    {
        var trails = new List<TrackingTrail>();
        var dens = new List<Vector3>();
        int animals = System.Enum.GetValues<TrackingAnimal>().Length;
        backs = 0;
        for (int leg = 0; leg < animals; )
        {
            float reach = Mathf.Lerp(TrailReachFirst, TrailReachLast, leg / (float)(animals - 1));
            var from = leg == 0 ? start : dens[^1] with { Y = 0 };
            var trail = PlanTrail((TrackingAnimal)leg, from, reach, turn, dens, finder, rng);
            if (trail != null)
            {
                trails.Add(trail);
                dens.Add(trail.Den);
                leg++;
                continue;
            }
            if (leg == 0 || ++backs > TrailBacktracks)
                return null;
            trails.RemoveAt(trails.Count - 1);
            dens.RemoveAt(dens.Count - 1);
            leg--;
        }
        return trails;
    }

    /// <summary>
    /// One trail: dens are tried at about <paramref name="reach"/> from <paramref name="from"/>, round the valley the
    /// way of <paramref name="turn"/> and drawn back to the middle of the land between the ring road and the borders,
    /// until one fits and the way to it is found.
    /// </summary>
    private TrackingTrail? PlanTrail(
        TrackingAnimal animal,
        Vector3 from,
        float reach,
        float turn,
        List<Vector3> dens,
        TrackingPath finder,
        RandomNumberGenerator rng
    )
    {
        float inner = WorldTuning.CoreRadius + 70,
            outer = ValleyBounds.HalfSize - 45;
        float middle = (inner + outer) / 2;
        float angle = Mathf.Atan2(from.Z, from.X);
        var round = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle)) * turn;
        var outward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        float band = Mathf.Max(Mathf.Abs(from.X), Mathf.Abs(from.Z));
        var aim = (round + outward * Mathf.Clamp((middle - band) / 300, -.8f, .8f)).Normalized();
        for (int attempt = 0; attempt < TrailAttempts; attempt++)
        {
            // First round the valley at the full reach; then wider and nearer; at last anywhere around.
            var (spread, scale, onward) =
                attempt < 40 ? (.9f, 1f, true)
                : attempt < 70 ? (1.6f, .8f, true)
                : attempt < 100 ? (2.2f, .62f, true)
                : (Mathf.Pi, rng.RandfRange(.5f, 1f), false);
            var direction = aim.Rotated(Vector3.Up, rng.RandfRange(-spread, spread));
            float distance = reach * scale * rng.RandfRange(.9f, 1.12f);
            var den = from + direction * distance;
            float progress = Mathf.Wrap(Mathf.Atan2(den.Z, den.X) - angle, -Mathf.Pi, Mathf.Pi) * turn;
            if (onward && progress < .05f || !DenFits(den, dens, inner, outer))
                continue;
            var way = finder.Find(from, den, distance * TrailDetour);
            if (way == null || TrackingPath.LengthOf(way) > distance * TrailDetour)
                continue;
            // A trail from a den begins at its edge, where the animal of that den sits.
            return LayTrail(animal, way, den, dens.Count == 0 ? 2 : TrackingTrail.DenRadius);
        }
        return null;
    }

    /// <summary>
    /// A den lies in the land between the ring road and the borders, on level dry ground off the roads, in a clearing,
    /// away from the places of the borders and the other dens.
    /// </summary>
    private bool DenFits(Vector3 p, List<Vector3> dens, float inner, float outer)
    {
        float band = Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z));
        if (band < inner || band > outer || !ValleyBounds.Contains(p.X, p.Z, 45))
            return false;
        var borders = WorldLayout.Plan.Borders;
        if (
            River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 18
            || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 18
            || OutlandRoutes.Distance(p) < 20
            || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 30) != null
        )
            return false;
        foreach (var den in dens)
            if (FlatDistance(p, den) < DenFromDen)
                return false;
        foreach (var home in BorderHomes)
            if (home != null && FlatDistance(p, home.Road) < DenFromSights)
                return false;
        if (
            LighthousePlace is { } light && FlatDistance(p, light.Foot) < DenFromSights
            || QuarryPlace is { } quarry && FlatDistance(p, quarry.Position) < 60
            || DragonPlace is { } dragon && FlatDistance(p, dragon.Lair) < DenFromSights
            || SlopePlace is { } slope && FlatDistance(p, slope.Position) < 60
            || BalloonPlace is { } balloon && FlatDistance(p, balloon.Pad) < DenFromSights
        )
            return false;
        float low = float.MaxValue,
            high = float.MinValue;
        for (float x = -TrackingTrail.DenRadius; x <= TrackingTrail.DenRadius; x += 2.5f)
        for (float z = -TrackingTrail.DenRadius; z <= TrackingTrail.DenRadius; z += 2.5f)
        {
            var q = new Vector3(p.X + x, 0, p.Z + z);
            if (InBorderfolkZone(q) || borders.Sand(q.X, q.Z) > .02f)
                return false;
            float h = Ground.Height(q.X, q.Z);
            // No planned tree anywhere on the den: the samples lie 2.5 m apart, so each looks 2.5 m around.
            if (Plants.NearTrunk(q with { Y = h }, 2.5f))
                return false;
            if (h < ValleyBorders.SeaLevel + 3)
                return false;
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        return high - low < 1.8f;
    }

    /// <summary>
    /// Whether the car can drive at a point of a trail: beyond the old valley and inside the valley, out of the rivers,
    /// the sea and the beach, off the bridges and the places of the borders, and with room to pass every trunk.
    /// </summary>
    private bool TrailOpen(float x, float z)
    {
        if (
            Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) < WorldTuning.CoreRadius + 12
            || !ValleyBounds.Contains(x, z, 12)
            || River.Distance(x, z) < River.HalfWidth(z) + 4
        )
            return false;
        var borders = WorldLayout.Plan.Borders;
        if (
            borders.BorderRiverDistance(x, z) < ValleyBorders.BorderRiverHalfWidth + 6
            || borders.Sand(x, z) > .05f
            || WorldLayout.Plan.Outland.BridgeAt(x, z, 10) != null
        )
            return false;
        var p = new Vector3(x, Ground.Height(x, z), z);
        return p.Y > ValleyBorders.SeaLevel + 1.5f && !Plants.NearTrunk(p, TrailTreeClearance) && !InBorderfolkZone(p);
    }

    /// <summary>Near a road a trail costs more, so that it crosses the road rather than runs along it; so does a wood.</summary>
    private float TrailCost(float x, float z)
    {
        var p = new Vector3(x, 0, z);
        float cost = 0;
        if (OutlandRoutes.Distance(p) < 10)
            cost += 2.5f;
        if (Plants.NearTrunk(p with { Y = Ground.Height(x, z) }, 5))
            cost += .4f;
        return cost;
    }

    /// <summary>The height a footprint lies at: on the ground, or on the road where it crosses one.</summary>
    private float PrintHeight(float x, float z)
    {
        float ground = Ground.Height(x, z);
        return Mathf.Max(ground, OutlandRoutes.SurfaceHeight(new Vector3(x, ground, z), ground));
    }

    /// <summary>
    /// The trail along a way found to the den: the way ends <see cref="TrackingTrail.PrintsEndShort"/> short of the den,
    /// and footprints are laid along it at the animal's stride, left and right in turn, from <paramref name="printsFrom"/>
    /// metres on.
    /// </summary>
    private TrackingTrail LayTrail(TrackingAnimal animal, List<Vector3> way, Vector3 den, float printsFrom)
    {
        // Cut the way short of the den.
        float total = TrackingPath.LengthOf(way);
        float keep = total - TrackingTrail.PrintsEndShort;
        var path = new List<Vector3> { way[0] };
        float walked = 0;
        for (int i = 1; i < way.Count; i++)
        {
            float piece = FlatDistance(way[i], way[i - 1]);
            if (walked + piece >= keep)
            {
                path.Add(way[i - 1].Lerp(way[i], (keep - walked) / Mathf.Max(piece, .001f)));
                break;
            }
            walked += piece;
            path.Add(way[i]);
        }
        var points = path.Select(p => new Vector3(p.X, Ground.Height(p.X, p.Z), p.Z)).ToArray();
        float length = TrackingPath.LengthOf(points);
        var approach = (points[^1] - points[^2]) with { Y = 0 };
        approach = approach.LengthSquared() > .0001f ? approach.Normalized() : Vector3.Forward;
        var middle = Ground.At(den.X, den.Z);
        var talk = middle - approach * TrackingTrail.TalkAhead;
        var prints = new List<TrackingPrint>();
        float stride = TrackingPrints.Stride(animal);
        float across = TrackingPrints.Straddle(animal);
        int number = 0;
        for (float along = printsFrom; along <= length; along += stride, number++)
        {
            var (at, forward) = Along(points, along);
            var right = Vector3.Up.Cross(forward);
            int side =
                across == 0 ? 0
                : number % 2 == 0 ? -1
                : 1;
            var p = at + right * side * across;
            float h = PrintHeight(p.X, p.Z);
            float dx = PrintHeight(p.X + .4f, p.Z) - PrintHeight(p.X - .4f, p.Z),
                dz = PrintHeight(p.X, p.Z + .4f) - PrintHeight(p.X, p.Z - .4f);
            var normal = new Vector3(-dx / .8f, 1, -dz / .8f).Normalized();
            prints.Add(new TrackingPrint(new Vector3(p.X, h + .05f, p.Z), forward, normal, side, along));
        }
        return new TrackingTrail(
            animal,
            points,
            prints.ToArray(),
            middle,
            Ground.At(talk.X, talk.Z),
            Mathf.Atan2(-approach.X, -approach.Z),
            length
        );
    }

    /// <summary>The point at <paramref name="along"/> metres along a way, on the flat, and its direction there.</summary>
    private static (Vector3 At, Vector3 Forward) Along(Vector3[] points, float along)
    {
        float walked = 0;
        for (int i = 1; i < points.Length; i++)
        {
            var piece = (points[i] - points[i - 1]) with { Y = 0 };
            float length = piece.Length();
            if (walked + length >= along || i == points.Length - 1)
            {
                float t = length < .001f ? 0 : Mathf.Clamp((along - walked) / length, 0, 1);
                var at = points[i - 1].Lerp(points[i], t);
                return (at with { Y = 0 }, length < .001f ? Vector3.Forward : piece / length);
            }
            walked += length;
        }
        return (points[^1] with { Y = 0 }, Vector3.Forward);
    }

    /// <summary>Whether a point lies on a den, where no plant and no grass grows.</summary>
    internal bool InTrackingZone(Vector3 p)
    {
        foreach (var trail in TrackingTrails)
            if (FlatMath.Length(p.X - trail.Den.X, p.Z - trail.Den.Z) < TrackingTrail.DenRadius)
                return true;
        return false;
    }

    /// <summary>
    /// Raises the dens with their animals hidden in them, the footprints of the trail being followed, the animal to coax
    /// out and the forest keeper's squirrel, who waits beside her until every animal is found.
    /// </summary>
    private void BuildTracking()
    {
        if (TrackingTrails.Count == 0 || Forester is not { } forester)
            return;
        var dens = new TrackingDens(TrackingTrails) { Name = "TrackingDens" };
        AddChild(dens);
        Art.Outline(dens, .02f, .3f);
        TrackingDens = dens;
        var prints = new TrackingPrints { Name = "TrackingPrints" };
        AddChild(prints);
        TrackingPrints = prints;
        var animal = Character.CreateTrackingVoice(CharacterCatalog.TrackingAnimal);
        animal.Role = new TrackingAnimalRole();
        var first = TrackingTrails[0];
        animal.Position = first.Talk;
        animal.Away = true;
        AddChild(animal);
        TrackingAnimal = animal;
        var squirrel = new CollectorPet(
            WorldPetCatalog.Squirrel,
            forester,
            () => dens.Found,
            TrackingTrails.Count,
            worldPets.Count
        );
        var seat = forester.Transform * new Vector3(1.5f, 0, .5f);
        squirrel.SetHome(Ground.At(seat.X, seat.Z), forester.Rotation.Y);
        AddChild(squirrel);
        AddWorldPet(squirrel);
    }

    /// <summary>The animal to coax out is registered after the balloonist, who came before it.</summary>
    private void RegisterTrackingAnimal()
    {
        if (TrackingAnimal != null)
            Register(TrackingAnimalId, TrackingAnimal);
    }
}
