using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The ferry to the bird island (<c>docs/FERRY_PLAN.md</c>): the landing of Volavka pozorovatelka Klárka beside the
/// homestead of the keeper of the river border, its pier down the bank to the border river, the way of the punt down the
/// river to its mouth, the island in the sea before it and the twelve clumps of reeds round the island that the birds hide
/// in. Everything but the birds is planned once the shells are, from the borders and the homestead alone, without random
/// numbers; the birds of a crossing follow from the valley and the crossing's number with random numbers of their own
/// (<see cref="FerrySeed"/>), so they are not saved, and nothing planned before them moves. The island is a model of its
/// own and changes no ground. All of it is held by <c>Tests/Worlds/seed-N.ferry.txt</c>.
/// </summary>
public partial class World
{
    internal const int BirdwatcherId = ActorIds.Birdwatcher;

    internal static bool IsBirdwatcher(int id) => id == BirdwatcherId;

    /// <summary>The random numbers of the birds are their own, so nothing planned before them changes.</summary>
    private const int FerrySeed = 9421;

    /// <summary>The island is driven on this far into its oval; beyond, its shore falls into the water.</summary>
    internal const float IslandDrivable = .85f;

    /// <summary>How far round the landing and the place of the loads the ground may rise by at most <see cref="LevelRise"/>.</summary>
    private const float LevelRadius = 4,
        LevelRise = 2.2f;

    /// <summary>The reeds keep this far from the island's middle at least, and this far from the punt's way.</summary>
    private const float ReedNearest = 45,
        ReedFromWay = 12;

    /// <summary>Where the landing, the pier, the island and its hide are, or null in a valley without room for them.</summary>
    internal FerrySite? FerryPlace { get; private set; }

    /// <summary>The way of the punt from the landing to the island.</summary>
    internal FerryRoute? FerryRoute { get; private set; }

    /// <summary>The clumps of reeds round the island, by their number.</summary>
    internal IReadOnlyList<ReedSpot> ReedSpots { get; private set; } = System.Array.Empty<ReedSpot>();

    /// <summary>The heron, Volavka pozorovatelka Klárka.</summary>
    internal Character? Birdwatcher { get; private set; }

    /// <summary>The landing, its pier and the punt, built with five deliveries.</summary>
    internal FerryLanding? FerryLanding { get; private set; }

    /// <summary>The island with its hide and the birds of the crossing in its reeds.</summary>
    internal BirdIsland? BirdIsland { get; private set; }

    /// <summary>Whether the player builds the punt; without the log yard or the sawmill it is there from the start.</summary>
    internal bool FerryBuildable { get; private set; }

    /// <summary>The car is on the island: it may drive there, beyond the edge of the valley, and nowhere else beyond it.</summary>
    internal bool IslandOpen { get; set; }

    /// <summary>
    /// Chooses the landing beside the homestead of the river border, the pier from the edge of the valley to the water,
    /// the island before the mouth of the border river, the way of the punt between them and the reeds round the island.
    /// </summary>
    private void PlanFerry()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (BorderHomes[(int)Border.River] is not { } home)
        {
            GD.Print("FERRY landing=none reason=home");
            return;
        }
        var borders = WorldLayout.Plan.Borders;
        FerrySite? chosen = null;
        foreach (float along in new[] { 14f, 10f, 18f, 6f })
        foreach (float across in new[] { 17f, -17f, 20f, -20f, 23f, -23f })
        {
            var landing = home.Local(across, along);
            var site = new FerrySite(
                Ground.At(landing.X, landing.Z),
                home.Inward,
                home.Yaw,
                home.Road,
                Vector3.Zero,
                Vector3.Zero,
                Vector3.Zero,
                Vector3.Zero,
                Vector3.Zero
            );
            if (!LandingFits(site))
                continue;
            // The pier leaves the valley straight on towards the border and runs down to the water of the border river.
            var start = landing;
            while (ValleyBounds.Contains(start.X, start.Z, 1))
                start += home.Inward;
            var berth = start;
            bool water = false;
            for (int step = 0; step < 300; step++, berth += home.Inward)
                if (
                    borders.BorderRiverDistance(berth.X, berth.Z) < ValleyBorders.BorderRiverHalfWidth - 7
                    && Ground.Height(berth.X, berth.Z) < ValleyBorders.BorderRiverLevel(berth.Z) - .6f
                )
                {
                    water = true;
                    break;
                }
            if (!water)
                continue;
            chosen = site with
            {
                PierStart = Ground.At(start.X, start.Z),
                Berth = berth with { Y = ValleyBorders.BorderRiverLevel(berth.Z) },
            };
            break;
        }
        if (chosen == null)
        {
            GD.Print("FERRY landing=none reason=room");
            return;
        }
        // The mouth: where the middle of the border river reaches the waterline of the sea.
        float mouth = borders.CoastMinimum - 80;
        while (mouth < borders.CoastMaximum + 80 && mouth < borders.Coast(borders.BorderRiverX(mouth)))
            mouth += 2;
        float riverX = borders.BorderRiverX(mouth);
        float top = ValleyBorders.SeaLevel + FerrySite.IslandRise;
        var island = new Vector3(riverX, top, mouth + FerrySite.IslandHalfLength + 30);
        var islandBerth = island + new Vector3(0, 0, -FerrySite.IslandHalfLength - 7);
        var hide = island + new Vector3(0, 0, 4);
        chosen = chosen with { Island = island, IslandBerth = islandBerth with { Y = 0 }, Hide = hide };
        if (ValleyBounds.Contains(island.X, island.Z, -FerrySite.IslandHalfWidth - 20))
        {
            GD.Print("FERRY landing=none reason=island");
            return;
        }
        // The punt goes out to the middle of the river, down it and over the sea to the island's pier.
        var corners = new List<Vector3> { chosen.Berth with { Y = 0 } };
        float z = chosen.Berth.Z + 30;
        corners.Add(new Vector3(borders.BorderRiverX(z), 0, z));
        for (z += 40; z < mouth; z += 40)
            corners.Add(new Vector3(borders.BorderRiverX(z), 0, z));
        corners.Add(new Vector3(riverX, 0, mouth));
        corners.Add(islandBerth with { Y = 0 });
        if (corners.Count < 4 || chosen.Berth.Z > mouth - 100)
        {
            GD.Print("FERRY landing=none reason=way");
            return;
        }
        var route = new FerryRoute(corners);
        // The reeds round the island: twelve directions, each tried a little nearer, further and to either side.
        var spots = new List<ReedSpot>();
        for (int i = 0; i < FerryPlans.Spots; i++)
        {
            float radius = ReedNearest + (i % 3) * 18;
            foreach (var (grow, turn) in new[] { (0f, 0f), (12f, 0f), (0f, 9f), (0f, -9f), (24f, 0f), (12f, 9f) })
            {
                float angle = Mathf.DegToRad(i * 360f / FerryPlans.Spots + 15 + turn);
                var p = island + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (radius + grow);
                if (!ReedFits(p, route, spots))
                    continue;
                float h = Ground.Height(p.X, p.Z);
                spots.Add(new ReedSpot(i, new Vector3(p.X, Mathf.Max(ValleyBorders.SeaLevel + .25f, h + .1f), p.Z)));
                break;
            }
        }
        if (spots.Count < FerryPlans.PerVisit * 2)
        {
            GD.Print($"FERRY landing=none reason=reeds spots={spots.Count}");
            return;
        }
        FerryPlace = chosen;
        FerryRoute = route;
        ReedSpots = spots;
        GD.Print(
            $"FERRY landing={chosen.Landing.X:0.0},{chosen.Landing.Z:0.0} way={route.Length:0} reeds={spots.Count} ms={clock.ElapsedMilliseconds}"
        );
    }

    /// <summary>
    /// The landing stands on dry ground inside the valley beside the homestead, off its yard and the roads, away from what
    /// else was planned beyond the old valley; so do the place where its loads are tipped out and the one where the car
    /// is put down after the crossing home. The bank may fall towards the river, as the deck stands on piles, but not so
    /// steeply that the trailer could not stand there.
    /// </summary>
    private bool LandingFits(FerrySite site)
    {
        foreach (var p in new[] { site.Landing, site.Drop })
        {
            float low = float.MaxValue,
                high = float.MinValue;
            for (float dx = -FerrySite.LandingRadius; dx <= FerrySite.LandingRadius; dx += 2)
            for (float dz = -FerrySite.LandingRadius; dz <= FerrySite.LandingRadius; dz += 2)
            {
                float far = dx * dx + dz * dz;
                if (far > FerrySite.LandingRadius * FerrySite.LandingRadius)
                    continue;
                var q = new Vector3(p.X + dx, 0, p.Z + dz);
                if (!ExplorerGroundOpen(q, 6) || NearFerrySights(q))
                    return false;
                if (far > LevelRadius * LevelRadius)
                    continue;
                float h = Ground.Height(q.X, q.Z);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
            if (high - low > LevelRise)
                return false;
        }
        // The car is put down a little further on after the crossing home, clear of the heron and of the camera on her.
        for (float dx = -3; dx <= 3; dx += 1.5f)
        for (float dz = -3; dz <= 3; dz += 1.5f)
        {
            var q = site.Return + new Vector3(dx, 0, dz);
            if (!ExplorerGroundOpen(q, 6) || NearFerrySights(q))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Whether a point is near what was planned beyond the old valley before the ferry and does not keep its own ground
    /// off the plants: the dens and ways of the forest keeper's trails, the shepherd's meadows and ways, the rally's
    /// arches and gates and the owl's wonders.
    /// </summary>
    private bool NearFerrySights(Vector3 p)
    {
        foreach (var trail in TrackingTrails)
        {
            if (FlatDistance(p, trail.Den) < 20)
                return true;
            foreach (var step in trail.Path)
                if (FlatDistance(p, step) < 4)
                    return true;
        }
        foreach (var spot in SheepSpots)
            if (FlatDistance(p, spot) < 20)
                return true;
        foreach (var way in SheepWays)
        foreach (var step in way)
            if (FlatDistance(p, step) < 4)
                return true;
        if (Rally is { } rally)
            foreach (var stage in rally.Stages)
            {
                if (FlatDistance(p, rally.At(stage.Start)) < 30)
                    return true;
                foreach (float gate in stage.Gates)
                    if (FlatDistance(p, rally.At(gate)) < 30)
                        return true;
            }
        foreach (var landmark in Landmarks)
            if (FlatDistance(p, landmark.Center) < ExplorerPlans.Radius(landmark.Kind) + 12)
                return true;
        return false;
    }

    /// <summary>A clump of reeds stands beyond the edge of the valley, off the island, the punt's way and the others.</summary>
    private bool ReedFits(Vector3 p, FerryRoute route, List<ReedSpot> spots)
    {
        if (ValleyBounds.Contains(p.X, p.Z, -6))
            return false;
        foreach (var other in spots)
            if (FlatDistance(p, other.Position) < 14)
                return false;
        foreach (var step in route.Samples)
            if (FlatDistance(p, step) < ReedFromWay)
                return false;
        // Not far up on the beach, where no water bird would wait.
        return Ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + 1.2f;
    }

    /// <summary>
    /// The birds of crossing number <paramref name="visit"/>: four of different kinds, drawn by how often each comes, each
    /// in a clump of reeds of its own.
    /// </summary>
    internal IslandBird[] BirdsOfVisit(int visit)
    {
        if (ReedSpots.Count < FerryPlans.PerVisit)
            return System.Array.Empty<IslandBird>();
        var rng = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + FerrySeed) * 7919UL + (ulong)visit };
        var left = Enumerable.Range(0, FerryPlans.Kinds).ToList();
        var spots = Enumerable.Range(0, ReedSpots.Count).ToList();
        var birds = new IslandBird[FerryPlans.PerVisit];
        for (int number = 0; number < FerryPlans.PerVisit; number++)
        {
            int sum = left.Sum(kind => FerryPlans.Weights[kind]);
            int roll = (int)rng.RandiRange(0, sum - 1),
                pick = 0;
            while (roll >= FerryPlans.Weights[left[pick]])
                roll -= FerryPlans.Weights[left[pick++]];
            int kind = left[pick];
            left.RemoveAt(pick);
            int spot = (int)rng.RandiRange(0, spots.Count - 1);
            birds[number] = new IslandBird(number, kind, spots[spot], rng.RandfRange(0, Mathf.Tau));
            spots.RemoveAt(spot);
        }
        return birds;
    }

    /// <summary>Whether a point lies inside the valley where it is played, off the island and everything beyond.</summary>
    internal static bool InsideValley(Vector3 p) => ValleyBounds.Contains(p.X, p.Z);

    /// <summary>Whether a point is on the part of the island the car may drive on.</summary>
    internal bool OnIsland(Vector3 p) => FerryPlace is { } site && site.IslandFraction(p.X, p.Z) < IslandDrivable;

    /// <summary>The landing, the place its loads are tipped out and the pier, where no plant grows.</summary>
    internal bool InFerryZone(Vector3 p)
    {
        if (FerryPlace is not { } site)
            return false;
        return FlatDistance(p, site.Landing) < FerrySite.LandingRadius + 1
            || FlatDistance(p, site.Drop) < 5
            || SegmentDistance(p, site.Drop, site.Return) < 4
            || SegmentDistance(p, site.Landing, site.PierStart) < 3
            || SegmentDistance(p, site.PierStart, site.Berth) < 3.5f;
    }

    /// <summary>
    /// Raises the landing, its pier and the punt (not begun, or finished where the log yard or the sawmill is missing),
    /// the island with its hide and reeds, the heron and the kingfisher, who waits at the landing for the full atlas.
    /// </summary>
    private void BuildFerry()
    {
        if (FerryPlace is not { } site || FerryRoute is not { } route)
            return;
        var landing = new FerryLanding(site, route, Ground) { Name = "FerryLanding" };
        bool buildable = LogYard != null && WorksFor(HaulLoads.ProductOf(HaulLoad.Planks)) != null;
        FerryBuildable = buildable;
        landing.SetStage(buildable ? 0 : FerryPlans.Trips);
        AddChild(landing);
        Art.Outline(landing, .025f, .3f);
        FerryLanding = landing;
        var island = new BirdIsland(site, ReedSpots) { Name = "BirdIsland" };
        AddChild(island);
        Art.Outline(island, .025f, .3f);
        BirdIsland = island;
        var heron = Character.Create(CharacterCatalog.Birdwatcher);
        heron.Role = new BirdwatcherRole();
        heron.DressAsHeron();
        heron.Position = Ground.At(site.Talk.X, site.Talk.Z);
        heron.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
        AddChild(heron);
        Birdwatcher = heron;
        landing.Heron = heron;
        var kingfisher = new CollectorPet(
            WorldPetCatalog.Kingfisher,
            heron,
            () => island.KindsFound,
            FerryPlans.Kinds,
            worldPets.Count
        );
        var seat = site.Local(2.2f, -3.6f);
        kingfisher.SetHome(Ground.At(seat.X, seat.Z), site.Yaw + Mathf.Pi);
        AddChild(kingfisher);
        AddWorldPet(kingfisher);
    }

    /// <summary>The heron is registered after the owl, who came before her.</summary>
    private void RegisterBirdwatcher()
    {
        if (Birdwatcher != null)
            Register(BirdwatcherId, Birdwatcher);
    }
}
