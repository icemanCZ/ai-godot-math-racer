using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The shepherd's lost sheep (<c>docs/SHEEP_PLAN.md</c>): eight meadows, one in every eighth of the land round the valley,
/// between the ring road and the borders, where the sheep that ran off in the storm graze until they are found. They are
/// chosen once the forest keeper's trails are, with random numbers of their own (<see cref="SheepSeed"/>), and take no
/// ground: no plant and no grass is left out for them, so nothing that was there moves. The meadows are held by
/// <c>Tests/Worlds/seed-N.sheep.txt</c>.
/// </summary>
public partial class World
{
    /// <summary>The shepherd under the mountains, the keeper of that border, whose sheep ran off.</summary>
    internal const int ShepherdId = ActorIds.FirstBorderKeeper + (int)Border.Mountains;

    /// <summary>The random numbers of the meadows are their own, so nothing planned before them changes.</summary>
    private const int SheepSeed = 8311;

    /// <summary>How many places a sheep tries in its own eighth of the land, and then anywhere round the valley.</summary>
    private const int SheepAttempts = 160,
        SheepAnywhere = 400;

    /// <summary>The sheep keep this far from each other, so that each has a corner of the land of its own.</summary>
    private const float SheepFromSheep = 160;

    /// <summary>They graze this far from the shepherd's homestead, so that none of them is found right at home.</summary>
    private const float SheepFromHome = 150;

    /// <summary>And this far from the other homesteads, the lighthouse, the dragon and the balloon.</summary>
    private const float SheepFromSights = 80;

    /// <summary>The ground of a meadow, round the sheep, is open and nearly level this far out.</summary>
    private const float MeadowRadius = 4;

    /// <summary>The way the car drives from a road to a sheep may be at most this many times the straight line, and some.</summary>
    private const float SheepDetour = 1.8f,
        SheepDetourExtra = 30;

    /// <summary>Where the lost sheep graze, by number; empty in a valley without the shepherd or room for them.</summary>
    internal IReadOnlyList<Vector3> SheepSpots { get; private set; } = System.Array.Empty<Vector3>();

    /// <summary>The ways the car can drive from the nearest road to each sheep, for the checks.</summary>
    internal IReadOnlyList<Vector3[]> SheepWays { get; private set; } = System.Array.Empty<Vector3[]>();

    /// <summary>The sheep, lost, following, waiting and home.</summary>
    internal SheepFlock? SheepFlock { get; private set; }

    /// <summary>The shepherd, whose sheep ran off.</summary>
    internal Character? Shepherd => KeeperOf(Border.Mountains);

    /// <summary>
    /// Chooses the meadows: for every eighth of the land round the valley, places between the ring road and the borders,
    /// until one fits and the car can drive there from a road; where none fits in its eighth, anywhere round the valley.
    /// A valley where they cannot all be found gets no sheep.
    /// </summary>
    private void PlanSheep()
    {
        if (BorderHomes[(int)Border.Mountains] is not { } home)
            return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var rng = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + SheepSeed) };
        var finder = new TrackingPath(TrailOpen, TrailCost, Ground.Height);
        float inner = WorldTuning.CoreRadius + 70,
            outer = ValleyBounds.HalfSize - 40;
        var spots = new List<Vector3>();
        var ways = new List<Vector3[]>();
        int tries = 0;
        for (int sheep = 0; sheep < SheepPlans.Sheep; sheep++)
        {
            bool found = false;
            for (int attempt = 0; attempt < SheepAttempts + SheepAnywhere && !found; attempt++)
            {
                tries++;
                float eighth = Mathf.Tau / SheepPlans.Sheep;
                float angle =
                    attempt < SheepAttempts
                        ? (sheep + rng.RandfRange(.1f, .9f)) * eighth
                        : rng.RandfRange(0, Mathf.Tau);
                float band = rng.RandfRange(inner, outer);
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var p = direction * (band / Mathf.Max(Mathf.Abs(direction.X), Mathf.Abs(direction.Z)));
                if (!SheepSpotFits(p, spots, home, inner, outer))
                    continue;
                var way = SheepWay(p, finder);
                if (way == null)
                    continue;
                spots.Add(Ground.At(p.X, p.Z));
                ways.Add(way);
                found = true;
            }
            if (!found)
            {
                GD.Print($"SHEEP spots=0 failed={sheep} tries={tries} ms={clock.ElapsedMilliseconds}");
                return;
            }
        }
        SheepSpots = spots;
        SheepWays = ways;
        GD.Print($"SHEEP spots={spots.Count} tries={tries} ms={clock.ElapsedMilliseconds}");
    }

    /// <summary>
    /// A meadow lies in the land between the ring road and the borders, on open, nearly level, dry ground off the roads
    /// and the rivers, away from the places of the borders, the dens and the other sheep.
    /// </summary>
    private bool SheepSpotFits(Vector3 p, List<Vector3> spots, BorderHomeSite fold, float inner, float outer)
    {
        float band = Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z));
        if (band < inner || band > outer || !ValleyBounds.Contains(p.X, p.Z, 40))
            return false;
        var borders = WorldLayout.Plan.Borders;
        if (
            River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 14
            || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 14
            || OutlandRoutes.Distance(p) < 14
            || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 25) != null
            || FlatDistance(p, fold.Road) < SheepFromHome
        )
            return false;
        foreach (var other in spots)
            if (FlatDistance(p, other) < SheepFromSheep)
                return false;
        foreach (var site in BorderHomes)
            if (site != null && FlatDistance(p, site.Road) < SheepFromSights)
                return false;
        foreach (var trail in TrackingTrails)
            if (FlatDistance(p, trail.Den) < 40)
                return false;
        if (
            LighthousePlace is { } light && FlatDistance(p, light.Foot) < SheepFromSights
            || QuarryPlace is { } quarry && FlatDistance(p, quarry.Position) < 50
            || DragonPlace is { } dragon && FlatDistance(p, dragon.Lair) < SheepFromSights
            || SlopePlace is { } slope && FlatDistance(p, slope.Position) < 50
            || BalloonPlace is { } balloon && FlatDistance(p, balloon.Pad) < SheepFromSights
        )
            return false;
        float low = float.MaxValue,
            high = float.MinValue;
        for (int i = 0; i <= 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            float r = i == 8 ? 0 : MeadowRadius;
            float x = p.X + r * Mathf.Cos(a),
                z = p.Z + r * Mathf.Sin(a);
            if (InBorderfolkZone(new Vector3(x, 0, z)) || borders.Sand(x, z) > .02f)
                return false;
            float h = Ground.Height(x, z);
            if (h < ValleyBorders.SeaLevel + 3 || Plants.NearTrunk(new Vector3(x, h, z), MeadowRadius * .6f))
                return false;
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        return high - low < 1.4f && !Plants.NearTrunk(Ground.At(p.X, p.Z), MeadowRadius);
    }

    /// <summary>
    /// The way the car can drive to a sheep from the nearest point of a road beyond the old valley where the car can turn
    /// off, over open ground (<see cref="TrackingPath"/>), or null where there is none.
    /// </summary>
    private Vector3[]? SheepWay(Vector3 spot, TrackingPath finder)
    {
        Vector3? nearest = null;
        float best = float.MaxValue;
        foreach (var sample in OutlandRoutes.Samples)
        {
            float d = FlatMath.Length(sample.X - spot.X, sample.Z - spot.Z);
            if (d < best && TrailOpen(sample.X, sample.Z))
            {
                best = d;
                nearest = sample;
            }
        }
        if (nearest is not { } road || best > 420)
            return null;
        var from = road with { Y = 0 };
        float longest = best * SheepDetour + SheepDetourExtra;
        var way = finder.Find(from, spot with { Y = 0 }, longest);
        if (way == null || TrackingPath.LengthOf(way) > longest)
            return null;
        return way.Select(p => Ground.At(p.X, p.Z)).ToArray();
    }

    /// <summary>
    /// The height a sheep walks at: the ground, a road of the old valley or of the land beyond it, a bridge or the ford.
    /// </summary>
    internal float SheepSurface(float x, float z)
    {
        float ground = Ground.Height(x, z);
        var p = new Vector3(x, ground, z);
        float road = Mathf.Max(Routes.SurfaceHeight(p, ground), OutlandRoutes.SurfaceHeight(p, ground));
        return CrossingSurface(x, z, road);
    }

    /// <summary>A point of the shepherd's homestead in the frame of its model (across, on from the front of the yard).</summary>
    internal Vector3 FoldPoint(float across, float on)
    {
        var home = BorderHomes[(int)Border.Mountains]!;
        var p = home.Local(across, BorderHomeSite.YardStart + on);
        return Ground.At(p.X, p.Z);
    }

    /// <summary>The middle of the fold, where the car brings the sheep.</summary>
    internal Vector3 FoldMiddle => FoldPoint(SheepPlans.FoldMiddle.X, SheepPlans.FoldMiddle.Y);

    /// <summary>Raises the sheep, hidden until the shepherd sends the player out, and the sheepdog puppy beside him.</summary>
    private void BuildSheep()
    {
        if (SheepSpots.Count == 0 || Shepherd is not { } shepherd || BorderHomes[(int)Border.Mountains] is not { } home)
            return;
        var flock = new SheepFlock(this, SheepSpots, home.Yaw) { Name = "SheepFlock" };
        AddChild(flock);
        Art.Outline(flock, .02f, .3f);
        SheepFlock = flock;
        var puppy = new CollectorPet(
            WorldPetCatalog.Sheepdog,
            shepherd,
            () => flock.HomeCount,
            SheepPlans.Sheep,
            worldPets.Count
        );
        var seat = shepherd.Transform * new Vector3(-1.5f, 0, .6f);
        puppy.SetHome(Ground.At(seat.X, seat.Z), shepherd.Rotation.Y);
        AddChild(puppy);
        AddWorldPet(puppy);
    }
}
