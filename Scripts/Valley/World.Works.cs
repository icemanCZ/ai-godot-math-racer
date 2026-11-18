using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;

namespace MathRacer.Valley;

/// <summary>The log yard and the three works, planned together so a valley gets all of them or none.</summary>
internal sealed record IndustryPlan(HaulSource LogYard, WorksSite[] Sites);

public partial class World
{
    // The sawmill, brickworks and gravel works workers follow the beaver (44).
    internal const int FirstWorksId = ActorIds.FirstWorks;
    internal const int LastWorksId = ActorIds.LastWorks;

    // The workers at the quarry, the clay pit and the log yard follow the builders (48–50, planned for later).
    internal const int FirstSourceId = ActorIds.FirstSource;

    // The workers at the second sites (quarry, clay pit, sand pit, log yard) follow them.
    internal const int FirstExtraSourceId = ActorIds.FirstExtraSource;
    internal const int LastSourceId = ActorIds.LastSource;
    internal const float WorksPathHalfWidth = 3;

    /// <summary>Works keep at least this far from the other places haul sites have taken.</summary>
    private const float WorksSpacing = 46;

    /// <summary>Why spots failed, in search order; printed when a valley has no room for the works.</summary>
    private readonly int[] industryRejections = new int[6];

    /// <summary>The sawmill, brickworks and gravel works indexed by <see cref="Product"/>; empty without a ford.</summary>
    internal Works[] Industry { get; private set; } = Array.Empty<Works>();

    /// <summary>The log yard beside a road, where trailer loads of logs for the sawmill are picked up.</summary>
    internal HaulSource? LogYard { get; private set; }

    /// <summary>Where a load is picked up: the log yard, the quarry, the clay pit or the sand pit.</summary>
    internal HaulSource SourceFor(HaulLoad load)
    {
        if (HaulLoads.IsGoods(load))
        {
            var works = Industry[(int)HaulLoads.ProductOf(load)];
            return new HaulSource(load, works.Site.Position, works.Site.Road);
        }
        if (HaulLoads.IsSpecial(load))
        {
            var giver = SpecialGiver(load);
            var road = Routes.Samples.OrderBy(sample => FlatDistance(sample, giver.Position)).First();
            return new HaulSource(load, giver.Position, road);
        }
        return load == HaulLoad.Logs ? LogYard!.Value : HaulSources[(int)load];
    }

    /// <summary>The stock of each works in <see cref="Product"/> order; zeros where the valley has none.</summary>
    internal int[] WorksStock()
    {
        var stock = new int[WorksCatalog.Count];
        foreach (var works in Industry)
            stock[works.Site.Index] = works.Stock;
        return stock;
    }

    /// <summary>
    /// Plans the log yard and the three works after the ford and its sources, with their own seeded search, so
    /// a valley keeps everything planned before. Returns null (and plans nothing) when any of them has no room.
    /// </summary>
    private IndustryPlan? PlanIndustry(FordSite ford)
    {
        var random = new Random(Settings.Seed + 2320);
        var friends = InteractionActors.ToArray();
        int approaches = haulApproaches.Count;
        int clearings = haulClearings.Count;
        void Rollback()
        {
            haulApproaches.RemoveRange(approaches, haulApproaches.Count - approaches);
            haulClearings.RemoveRange(clearings, haulClearings.Count - clearings);
        }
        if (!FindSpur(random, friends, ford, false, out var yardRoad, out var yardSpot, out _))
        {
            GD.Print($"INDUSTRY_MISSING seed={Settings.Seed} log-yard rejected={string.Join(",", industryRejections)}");
            return null;
        }
        haulApproaches.Add((yardRoad, yardSpot));
        haulClearings.Add(yardSpot);
        var sites = new List<WorksSite>();
        for (int index = 0; index < WorksCatalog.Count; index++)
        {
            // The works stand at the edge of the village where the valley has room, else anywhere on the bank.
            if (
                !FindSpur(random, friends, ford, true, out var road, out var ring, out var inward, WorksBand)
                && !FindSpur(random, friends, ford, true, out road, out ring, out inward)
            )
            {
                Rollback();
                GD.Print(
                    $"INDUSTRY_MISSING seed={Settings.Seed} works={index} rejected(bank,river,spacing,spot,footprint,path)={string.Join(",", industryRejections)}"
                );
                return null;
            }
            var right = Vector3.Up.Cross(inward);
            var worker = Ground.At(
                ring.X - right.X * 4.8f + inward.X * 1.5f,
                ring.Z - right.Z * 4.8f + inward.Z * 1.5f
            );
            sites.Add(new WorksSite(index, ring, road, inward, worker));
            haulApproaches.Add((road, ring));
            // The buildings, the stock yard and the raw heap keep trees away.
            haulClearings.Add(ring);
            haulClearings.Add(ring + inward * 10);
            haulClearings.Add(ring + inward * 6 + right * 9.5f);
            haulClearings.Add(ring + inward * 9 - right * 8.8f);
        }
        GD.Print(
            $"INDUSTRY_READY yard={FlatDistance(yardRoad, yardSpot):0} "
                + string.Join(" ", sites.Select(s => $"works{s.Index}={FlatDistance(s.Road, s.Position):0}"))
        );
        return new IndustryPlan(new HaulSource(HaulLoad.Logs, yardSpot, yardRoad), sites.ToArray());
    }

    /// <summary>
    /// How strict one pass of the search for a site is. Passes run from the strictest to the loosest, so a
    /// crowded or hilly valley still gets every works, only a little closer to its neighbours or on a steeper slope.
    /// </summary>
    private readonly record struct SpurRules(
        float MinOffset,
        float MaxOffset,
        float Spacing,
        float Slope,
        float RoadClearance,
        float RiverMargin,
        int Attempts
    );

    private static readonly SpurRules[] SpurPasses =
    {
        new(24, 54, WorksSpacing, 1.1f, 16, 14, 6000),
        new(20, 80, 38, 1.6f, 14, 11, 12000),
        new(16, 120, 30, 2.2f, 12, 9, 24000),
        new(14, 170, 24, 3.0f, 10, 8, 48000),
        new(12, 240, 16, 4.0f, 8, 8, 96000),
    };

    /// <summary>
    /// Passes for the sites that belong at the edge of the village. The village already keeps a wide quiet belt for the
    /// roadside friends, so these sites live inside it, a short way off a road: the search only looks near the houses.
    /// </summary>
    private static readonly SpurRules[] EdgePasses =
    {
        new(10, 45, 40, 1.1f, 12, 14, 5000),
        new(8, 70, 32, 1.6f, 10, 11, 10000),
        new(8, 90, 26, 2.2f, 9, 9, 20000),
    };

    /// <summary>
    /// How far from the nearest house (see <see cref="Village.PlotDistance"/>) a site at the village edge may stand:
    /// beyond the yards and the square, but still within sight of the houses.
    /// </summary>
    internal readonly record struct VillageBand(float Min, float Max);

    /// <summary>The sawmill, the brickworks and the gravel works stand right at the edge of the village.</summary>
    internal static readonly VillageBand WorksBand = new(30, 64);

    /// <summary>The windmill and the lookout tower stand a little further out, where there is room and wind.</summary>
    internal static readonly VillageBand LandmarkBand = new(34, 98);

    private bool InBand(Vector3 point, VillageBand? band) =>
        band is not { } wanted
        || Village.PlotDistance(point) >= wanted.Min && Village.PlotDistance(point) <= wanted.Max;

    /// <summary>The road samples of the village bank that a site search may start from.</summary>
    private int[] BankSamples(FordSite ford, VillageBand? band, float reach) =>
        Enumerable
            .Range(0, Routes.Samples.Count - 1)
            .Where(i =>
            {
                var forward = Routes.Samples[i + 1] - Routes.Samples[i];
                forward.Y = 0;
                return !ford.OnFarBank(Routes.Samples[i])
                    && forward.LengthSquared() is > .01f and <= 9
                    && !River.OnBridge(Routes.Samples[i].X, Routes.Samples[i].Z)
                    && (band is not { } near || Village.PlotDistance(Routes.Samples[i]) <= near.Max + reach);
            })
            .ToArray();

    /// <summary>
    /// A spot on the village bank beside a road, reached by a clear off-road approach. A works needs a larger,
    /// level, dry footprint behind the spot (<paramref name="inward"/> points away from the road). With a
    /// <paramref name="band"/> the spot has to stand at that distance from the houses.
    /// </summary>
    private bool FindSpur(
        Random random,
        Animal[] friends,
        FordSite ford,
        bool works,
        out Vector3 road,
        out Vector3 spot,
        out Vector3 inward,
        VillageBand? band = null,
        Func<Vector3, bool>? accept = null
    )
    {
        var bank = BankSamples(ford, band, band == null ? 0 : 90);
        foreach (var rules in band == null ? SpurPasses : EdgePasses)
        {
            // A log yard is small, so it may stand a little closer to the road and to other sites.
            float spacing = works ? rules.Spacing : Mathf.Min(rules.Spacing, 40);
            float minimum = works ? rules.MinOffset : rules.MinOffset - 4;
            for (int attempt = 0; attempt < rules.Attempts && bank.Length > 0; attempt++)
            {
                int index = bank[random.Next(bank.Length)];
                road = Routes.Samples[index];
                var forward = Routes.Samples[index + 1] - road;
                forward.Y = 0;
                var outward = forward.Normalized().Cross(Vector3.Up) * (random.Next(2) == 0 ? -1 : 1);
                float distance = minimum + (float)random.NextDouble() * (rules.MaxOffset - minimum);
                var point = Ground.At(road.X + outward.X * distance, road.Z + outward.Z * distance);
                spot = point;
                inward = outward;
                int reason =
                    ford.OnFarBank(point) || !InBand(point, band) ? 0
                    : River.Distance(point.X, point.Z) < River.HalfWidth(point.Z) + rules.RiverMargin ? 1
                    : haulClearings.Any(other => FlatDistance(other, point) < spacing) ? 2
                    : !FreeHaulSpot(point, friends, rules.RoadClearance, band) || accept?.Invoke(point) == false ? 3
                    : works && !WorksFootprintFits(point, outward, friends, rules, band) ? 4
                    : !ClearHaulPath(road, point, friends, toWater: false) ? 5
                    : -1;
                if (reason >= 0)
                {
                    industryRejections[reason]++;
                    continue;
                }
                return true;
            }
        }
        road = spot = inward = default;
        return false;
    }

    private static readonly float[] FootprintAcross = { -12f, -6f, 0, 6f, 12f };
    private static readonly float[] FootprintAlong = { -5f, 0, 6f, 12f, 18f };

    /// <summary>The buildings, stock yard and raw heap stand on gently sloping, dry, otherwise empty ground.</summary>
    private bool WorksFootprintFits(
        Vector3 ring,
        Vector3 inward,
        Animal[] friends,
        SpurRules rules,
        VillageBand? band = null
    )
    {
        var right = Vector3.Up.Cross(inward);
        var friendSpots = ActorSpots(friends);
        var tower = TowerSpot;
        foreach (float x in FootprintAcross)
        foreach (float z in FootprintAlong)
        {
            var q = ring + right * x + inward * z;
            q = Ground.At(q.X, q.Z);
            if (
                Mathf.Abs(q.Y - ring.Y) > rules.Slope
                || River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + rules.RiverMargin - 5
                || AnyCloserThan(friendSpots, q.X, q.Z, 9)
                || AnyCloserThan(SpecialPickupSpots, q.X, q.Z, 10)
                || CloserThan(tower, q.X, q.Z, 18)
                || Village.IsReserved(q)
                || (band == null ? Village.IsNearVillage(q) : Village.PlotDistance(q) < 16)
                || IsPickupApproachReserved(q)
                || Routes.Distance(q) < Mathf.Min(8, rules.RoadClearance - 4)
            )
                return false;
        }
        return true;
    }

    /// <summary>A second quarry, clay pit, sand pit and log yard, far from the first: any site fills the trailer.</summary>
    internal HaulSource[] ExtraSources { get; private set; } = Array.Empty<HaulSource>();

    /// <summary>Every place where a load can be picked up: the first site of a raw material, then its second.</summary>
    internal IEnumerable<HaulSource> SourceSites(HaulLoad load)
    {
        yield return SourceFor(load);
        foreach (var extra in ExtraSources)
            if (extra.Load == load)
                yield return extra;
    }

    /// <summary>The site of a load nearest to <paramref name="from"/>: where a new trip sends the player.</summary>
    internal HaulSource NearestSource(HaulLoad load, Vector3 from) =>
        SourceSites(load).MinBy(source => FlatDistance(source.Position, from));

    /// <summary>
    /// Plans one more place for each raw material, far from the first one and out in the countryside. Optional: a
    /// valley without room for one simply keeps a single site. Planned last, with its own seeded search.
    /// </summary>
    private HaulSource[] PlanExtraSources(FordSite ford, HaulSource[] sources, IndustryPlan? industry)
    {
        var random = new Random(Settings.Seed + 2340);
        var friends = InteractionActors.ToArray();
        var extras = new List<HaulSource>();
        foreach (var load in SourceCatalog.ExtraLoads)
        {
            HaulSource? first = load == HaulLoad.Logs ? industry?.LogYard : sources[(int)load];
            if (first is not { } origin)
                continue;
            Array.Clear(industryRejections);
            if (
                !FindSpur(random, friends, ford, false, out var road, out var spot, out _, accept: p =>
                    FlatDistance(p, origin.Position) >= ExtraSourceGap
                )
            )
            {
                GD.Print(
                    $"EXTRA_SOURCE_MISSING seed={Settings.Seed} load={load} rejected={string.Join(",", industryRejections)}"
                );
                continue;
            }
            extras.Add(new HaulSource(load, spot, road));
            haulApproaches.Add((road, spot));
            haulClearings.Add(spot);
        }
        return extras.ToArray();
    }

    /// <summary>A second site keeps at least this far from the first one of its material.</summary>
    private const float ExtraSourceGap = 110;

    /// <summary>The workers of the quarry, the clay pit and the log yard, in <see cref="SourceCatalog"/> order.</summary>
    internal Animal?[] SourceWorkers { get; } = new Animal?[SourceCatalog.Count];

    /// <summary>The workers of the second quarry, clay pit, sand pit and log yard, in <see cref="SourceCatalog.Extra"/> order.</summary>
    internal Animal?[] ExtraSourceWorkers { get; } = new Animal?[SourceCatalog.Extra.Length];

    /// <summary>The worker of the first site of a raw material, or null where nobody works (the sand pit).</summary>
    internal Animal? SourceWorker(HaulLoad load)
    {
        int index = SourceCatalog.IndexOf(load);
        return index < 0 ? null : SourceWorkers[index];
    }

    /// <summary>The worker of the second site of a raw material, or null where the valley has none.</summary>
    internal Animal? ExtraSourceWorker(HaulLoad load)
    {
        int index = SourceCatalog.ExtraIndexOf(load);
        return index < 0 ? null : ExtraSourceWorkers[index];
    }

    /// <summary>Every worker at a raw-material site, first sites before second ones.</summary>
    internal IEnumerable<Animal> AllSourceWorkers => SourceWorkers.Concat(ExtraSourceWorkers).OfType<Animal>();

    /// <summary>
    /// The character who asks the loading question for a load: the worker of the site it is loaded at (the first
    /// one unless <paramref name="source"/> is the second), if there is one.
    /// </summary>
    internal Animal? PickupActor(HaulLoad load, HaulSource? source = null) =>
        HaulLoads.IsGoods(load) ? Industry[(int)HaulLoads.ProductOf(load)].Worker
        : HaulLoads.IsSpecial(load) ? SpecialGiver(load)
        : source is { } site && ExtraSources.Contains(site) ? ExtraSourceWorker(load)
        : SourceWorker(load);

    /// <summary>The collector who hands over a special load: the farmer gives cloth, the gem collector a stone, the toymaker a flag.</summary>
    internal Animal SpecialGiver(HaulLoad load) =>
        load switch
        {
            HaulLoad.Fabric => Village.Farmer,
            HaulLoad.Capstone => Village.GemCollector,
            _ => Village.Toymaker,
        };

    /// <summary>
    /// The actor IDs of whoever may ask the loading question for a load of a project (see <see cref="PickupActor"/>):
    /// one, or for a raw material the workers of both its sites (and, at the first sand pit, the builder).
    /// </summary>
    internal static int[] AskerIds(HaulLoad load, int project)
    {
        if (load == HaulLoad.Fabric)
            return new[] { FarmerId };
        if (load == HaulLoad.Capstone)
            return new[] { GemCollectorId };
        if (load == HaulLoad.Flag)
            return new[] { ToymakerId };
        if (HaulLoads.IsGoods(load))
            return new[] { FirstWorksId + (int)HaulLoads.ProductOf(load) };
        int first = SourceCatalog.IndexOf(load) >= 0 ? FirstSourceId + SourceCatalog.IndexOf(load) : BuilderId(project);
        int extra = SourceCatalog.ExtraIndexOf(load);
        return extra < 0 ? new[] { first } : new[] { first, FirstExtraSourceId + extra };
    }

    /// <summary>Places a worker beside the loading spot of a quarry, clay pit, sand pit or log yard.</summary>
    private Animal PlaceSourceWorker(HaulSource source, SourceSpec spec, int id)
    {
        var inward = source.Position - source.Road;
        inward.Y = 0;
        inward = inward.Normalized();
        var across = inward.Cross(Vector3.Up);
        var spot = source.Position - across * 3.4f + inward * 1.2f;
        var worker = Animal.Create(spec.Name, spec.Greeting, spec.Kind, spec.Fur);
        worker.Role = new SourceRole(source.Load);
        worker.DressAsWorker(spec.Hat);
        worker.Position = Ground.At(spot.X, spot.Z);
        var facing = source.Position - worker.Position;
        worker.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
        AddChild(worker);
        Register(id, worker);
        return worker;
    }

    /// <summary>Places the worker of the first quarry, clay pit or log yard (the sand pit has none).</summary>
    private void BuildSourceWorker(HaulSource source)
    {
        int index = SourceCatalog.IndexOf(source.Load);
        if (index >= 0)
            SourceWorkers[index] = PlaceSourceWorker(source, SourceCatalog.All[index], FirstSourceId + index);
    }

    /// <summary>Places the worker of a second site; every raw material has one, the sand pit too.</summary>
    private void BuildExtraSourceWorker(HaulSource source)
    {
        int index = SourceCatalog.ExtraIndexOf(source.Load);
        if (index >= 0)
            ExtraSourceWorkers[index] = PlaceSourceWorker(
                source,
                SourceCatalog.Extra[index],
                FirstExtraSourceId + index
            );
    }

    /// <summary>Raises the log yard's stacks and the three works with their workers.</summary>
    private void BuildIndustry(IndustryPlan? plan)
    {
        if (plan == null)
            return;
        LogYard = plan.LogYard;
        BuildHaulSource(plan.LogYard);
        BuildSourceWorker(plan.LogYard);
        var built = new List<Works>();
        foreach (var site in plan.Sites)
        {
            var works = new Works(site, Ground);
            AddChild(works);
            var spec = works.Spec;
            var worker = Animal.Create(spec.Worker, spec.Greeting, spec.Kind, spec.Fur);
            worker.Role = new WorksRole(site.Index);
            worker.DressAsWorker(spec.Hat);
            worker.Position = site.WorkerSpot;
            var facing = site.Position - site.WorkerSpot;
            worker.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
            AddChild(worker);
            works.Worker = worker;
            Register(FirstWorksId + site.Index, worker);
            built.Add(works);
        }
        Industry = built.ToArray();
    }
}
