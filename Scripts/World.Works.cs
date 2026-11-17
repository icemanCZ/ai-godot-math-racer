using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer;

/// <summary>The log yard and the three works, planned together so a valley gets all of them or none.</summary>
internal sealed record IndustryPlan(HaulSource LogYard, WorksSite[] Sites);

public partial class World
{
    // The sawmill, brickworks and gravel works workers follow the beaver (44).
    internal const int FirstWorksId = 45;
    internal const int LastWorksId = FirstWorksId + WorksCatalog.Count - 1;

    // The workers at the quarry, the clay pit and the log yard follow the builders (48–50, planned for later).
    internal const int FirstSourceId = 51;
    internal const int LastSourceId = FirstSourceId + SourceCatalog.Count - 1;
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
            if (!FindSpur(random, friends, ford, true, out var road, out var ring, out var inward))
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
    /// A spot on the village bank beside a road, reached by a clear off-road approach. A works needs a larger,
    /// level, dry footprint behind the spot (<paramref name="inward"/> points away from the road).
    /// </summary>
    private bool FindSpur(
        Random random,
        Animal[] friends,
        FordSite ford,
        bool works,
        out Vector3 road,
        out Vector3 spot,
        out Vector3 inward
    )
    {
        var bank = Enumerable
            .Range(0, Routes.Samples.Count - 1)
            .Where(i =>
            {
                var forward = Routes.Samples[i + 1] - Routes.Samples[i];
                forward.Y = 0;
                return !ford.OnFarBank(Routes.Samples[i])
                    && forward.LengthSquared() is > .01f and <= 9
                    && !River.OnBridge(Routes.Samples[i].X, Routes.Samples[i].Z);
            })
            .ToArray();
        foreach (var rules in SpurPasses)
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
                    ford.OnFarBank(point) ? 0
                    : River.Distance(point.X, point.Z) < River.HalfWidth(point.Z) + rules.RiverMargin ? 1
                    : haulClearings.Any(other => FlatDistance(other, point) < spacing) ? 2
                    : !FreeHaulSpot(point, friends, rules.RoadClearance) ? 3
                    : works && !WorksFootprintFits(point, outward, friends, rules) ? 4
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
    private bool WorksFootprintFits(Vector3 ring, Vector3 inward, Animal[] friends, SpurRules rules)
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
                || Village.IsNearVillage(q)
                || IsPickupApproachReserved(q)
                || Routes.Distance(q) < Mathf.Min(8, rules.RoadClearance - 4)
            )
                return false;
        }
        return true;
    }

    /// <summary>The workers of the quarry, the clay pit and the log yard, in <see cref="SourceCatalog"/> order.</summary>
    internal Animal?[] SourceWorkers { get; } = new Animal?[SourceCatalog.Count];

    /// <summary>The worker who loads a raw material, or null where nobody does (the sand pit).</summary>
    internal Animal? SourceWorker(HaulLoad load)
    {
        int index = SourceCatalog.IndexOf(load);
        return index < 0 ? null : SourceWorkers[index];
    }

    /// <summary>The character who asks the loading question for a load: its source worker, if there is one.</summary>
    internal Animal? PickupActor(HaulLoad load) =>
        HaulLoads.IsGoods(load) ? Industry[(int)HaulLoads.ProductOf(load)].Worker
        : HaulLoads.IsSpecial(load) ? SpecialGiver(load)
        : SourceWorker(load);

    /// <summary>The collector who hands over a special load: the farmer gives cloth, the gem collector a stone, the toymaker a flag.</summary>
    internal Animal SpecialGiver(HaulLoad load) =>
        load switch
        {
            HaulLoad.Fabric => Village.Farmer,
            HaulLoad.Capstone => Village.GemCollector,
            _ => Village.Toymaker,
        };

    /// <summary>The actor ID of whoever asks the loading question for a load of a project: see <see cref="PickupActor"/>.</summary>
    internal static int AskerId(HaulLoad load, int project) =>
        load == HaulLoad.Fabric ? FarmerId
        : load == HaulLoad.Capstone ? GemCollectorId
        : load == HaulLoad.Flag ? ToymakerId
        : HaulLoads.IsGoods(load) ? FirstWorksId + (int)HaulLoads.ProductOf(load)
        : SourceCatalog.IndexOf(load) >= 0 ? FirstSourceId + SourceCatalog.IndexOf(load)
        : BuilderId(project);

    /// <summary>Places the worker beside the loading spot of a quarry, clay pit or log yard.</summary>
    private void BuildSourceWorker(HaulSource source)
    {
        int index = SourceCatalog.IndexOf(source.Load);
        if (index < 0)
            return;
        var spec = SourceCatalog.All[index];
        var inward = source.Position - source.Road;
        inward.Y = 0;
        inward = inward.Normalized();
        var across = inward.Cross(Vector3.Up);
        var spot = source.Position - across * 3.4f + inward * 1.2f;
        var worker = Animal.Create(spec.Name, spec.Greeting, spec.Kind, spec.Fur);
        worker.Role = new SourceRole(index);
        worker.DressAsWorker(spec.Hat);
        worker.Position = Ground.At(spot.X, spot.Z);
        var facing = source.Position - worker.Position;
        worker.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
        AddChild(worker);
        SourceWorkers[index] = worker;
        actors.Add(FirstSourceId + index, worker);
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
            actors.Add(FirstWorksId + site.Index, worker);
            built.Add(works);
        }
        Industry = built.ToArray();
    }
}
