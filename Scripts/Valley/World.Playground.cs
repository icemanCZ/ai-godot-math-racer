using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;

namespace MathRacer.Valley;

public partial class World
{
    internal const int PlaygroundId = ActorIds.Playground;

    internal static bool IsPlaygroundHelper(int id) => id == PlaygroundId;

    /// <summary>The playground at the edge of the village, or null in a valley that has no ford and so no works.</summary>
    internal Playground? Playground { get; private set; }

    private PlaygroundSite? playgroundSite;

    /// <summary>The playground stands within sight of the houses, a little further out than the works.</summary>
    internal static readonly VillageBand PlaygroundBand = new(26, 130);

    /// <summary>
    /// The playground keeps this far from the works and the buildings (a works' yard reaches 23 m in front of its ring
    /// and 15 m to the sides, the playground's 21 m and 14 m), from the glassworks, the farm and the bakery, whose
    /// sites do not reserve any ground, and, when the valley is crowded, a little less.
    /// </summary>
    private const float PlaygroundGap = 46;

    /// <summary>
    /// The last passes of the search for the playground, used only where the strict rules of the works leave no room:
    /// the same rules as for any site, with steeper ground allowed (every toy stands on a level pad of its own), a short
    /// way off a road and little spacing, but never on a road, in the river, on the village or on another site.
    /// </summary>
    private static readonly SpurRules[] PlaygroundPasses =
    {
        new(8, 120, 24, 3.2f, 8, 8, 60000),
        new(6, 260, 12, 5f, 6, 6, 160000),
    };

    /// <summary>
    /// Plans the playground at the edge of the village after everything else, with a seeded search of its own, so the
    /// older sites stay where they were. Like the glassworks, the farm and the bakery it reserves no ground: the plants
    /// are generated as before and the trees, bushes and rocks on the yard and its path are taken out afterwards
    /// (<see cref="InPlaygroundZone"/>), and a small friend who hides there is moved. A valley always gets one: the
    /// search loosens its rules step by step, and only a valley with no works has none.
    /// </summary>
    private PlaygroundSite? PlanPlayground(
        FordSite ford,
        IndustryPlan industry,
        BuildPlan? builds,
        WorksSite? glass,
        FarmPlan? farm
    )
    {
        var random = new Random(Settings.Seed + 2390);
        var friends = InteractionActors.ToArray();
        var others = new List<(Vector3 Position, float Gap)>();
        foreach (var works in industry.Sites)
            others.Add((works.Position, PlaygroundGap * .8f));
        if (glass != null)
            others.Add((glass.Position, PlaygroundGap));
        if (farm != null)
        {
            others.Add((farm.Farm.Position, PlaygroundGap + 6));
            others.Add((farm.Bakery.Position, PlaygroundGap));
        }
        if (builds != null)
            foreach (var build in builds.Sites)
                others.Add((build.Position, PlaygroundGap * .7f));
        bool Apart(Vector3 p, float factor) =>
            others.All(other => FlatDistance(p, other.Position) >= other.Gap * factor);

        Array.Clear(industryRejections);
        Vector3 road = default,
            ring = default,
            inward = default;
        bool Try(VillageBand? band, float factor, SpurRules[]? passes = null) =>
            FindSpur(
                random,
                friends,
                ford,
                true,
                out road,
                out ring,
                out inward,
                band,
                p => Apart(p, factor),
                passes,
                (from, to) => YardIsFree(from, to, industry, builds)
            );
        // First the edge of the village, in sight of the houses; then wider and looser, and in the end anywhere on the bank.
        if (
            !Try(WorksBand, 1)
            && !Try(PlaygroundBand, 1)
            && !Try(PlaygroundBand, .7f, PlaygroundPasses)
            && !Try(null, 1)
            && !Try(null, .7f, PlaygroundPasses)
            && !Try(null, .5f, PlaygroundPasses)
        )
        {
            GD.Print(
                $"PLAYGROUND_MISSING seed={Settings.Seed} rejected(bank,river,spacing,spot,footprint,path)={string.Join(",", industryRejections)}"
            );
            return null;
        }
        var right = Vector3.Up.Cross(inward);
        var keeper = Ground.At(ring.X - right.X * 4.8f + inward.X * 1.5f, ring.Z - right.Z * 4.8f + inward.Z * 1.5f);
        playgroundSite = new PlaygroundSite(ring, road, inward, keeper);
        GD.Print($"PLAYGROUND_READY way={FlatDistance(road, ring):0} village={Village.PlotDistance(ring):0}");
        return playgroundSite;
    }

    /// <summary>
    /// Whether the yard behind <paramref name="ring"/> and the field path from <paramref name="road"/> keep clear of every
    /// other site: the yards of the works and of the glassworks, the farm and the bakery, the buildings and the places
    /// where raw materials are picked up. The older sites keep their distance from one another by the spacing of their
    /// rings, but the way to the playground can be long, so the whole way is looked at.
    /// </summary>
    private bool YardIsFree(Vector3 road, Vector3 ring, IndustryPlan industry, BuildPlan? builds) =>
        AreaIsFree(
            road,
            ring,
            industry,
            builds,
            PlaygroundSite.AlongMin,
            PlaygroundSite.AlongMax,
            PlaygroundSite.AcrossMax
        );

    /// <summary>
    /// <see cref="YardIsFree"/> for a site with another size: the area from <paramref name="alongMin"/> to
    /// <paramref name="alongMax"/> behind the ring and <paramref name="acrossMax"/> to each side, and the way to it.
    /// A later site (the zoo) also keeps clear of the playground's area (<paramref name="avoidPlayground"/>).
    /// </summary>
    private bool AreaIsFree(
        Vector3 road,
        Vector3 ring,
        IndustryPlan industry,
        BuildPlan? builds,
        float alongMin,
        float alongMax,
        float acrossMax,
        bool avoidPlayground = false
    )
    {
        var inward = ring - road;
        inward.Y = 0;
        inward = inward.Normalized();
        var right = Vector3.Up.Cross(inward);
        bool Taken(Vector3 q, float margin)
        {
            if (InGlassworksZone(q) || InFarmZone(q) || InBakeryZone(q) || (avoidPlayground && InPlaygroundZone(q)))
                return true;
            foreach (var works in industry.Sites)
                if (InYard(q, works.Position, works.Inward, -8 - margin, 23 + margin, 15 + margin))
                    return true;
            if (builds != null)
                foreach (var build in builds.Sites)
                    if (InYard(q, build.Position, build.Inward, -5 - margin, 19 + margin, 12 + margin))
                        return true;
            foreach (var clearing in haulClearings)
                if (FlatDistance(clearing, q) < 9 + margin)
                    return true;
            return false;
        }
        // The yard itself, with a little room all round, and then the way from the road to the ring.
        for (float along = alongMin; along <= alongMax; along += 4.5f)
        for (float across = -acrossMax; across <= acrossMax; across += 7)
            if (Taken(ring + inward * along + right * across, 2))
                return false;
        int steps = Mathf.CeilToInt(FlatDistance(road, ring) / 2);
        for (int step = 0; step <= steps; step++)
            foreach (float lateral in new[] { -4f, 0, 4 })
                if (Taken(road.Lerp(ring, step / (float)steps) + right * lateral, 0))
                    return false;
        return true;
    }

    private static bool InYard(Vector3 q, Vector3 ring, Vector3 inward, float from, float to, float across)
    {
        var offset = new Vector3(q.X - ring.X, 0, q.Z - ring.Z);
        float along = offset.Dot(inward);
        return along >= from && along <= to && Mathf.Abs(offset.Dot(Vector3.Up.Cross(inward))) <= across;
    }

    /// <summary>Raises the playground and its keeper, who has the next free actor ID.</summary>
    private void BuildPlayground(PlaygroundSite? site)
    {
        if (site == null)
            return;
        var playground = new Playground(site, Ground);
        AddChild(playground);
        var spec = CharacterCatalog.PlaygroundKeeper;
        var keeper = Character.Create(spec);
        keeper.Role = new PlaygroundRole(playground);
        keeper.DressAsWorker("f2b84b");
        keeper.Position = site.HelperSpot;
        var facing = site.Position - site.HelperSpot;
        keeper.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
        AddChild(keeper);
        playground.Helper = keeper;
        Register(PlaygroundId, keeper);
        Playground = playground;
    }

    /// <summary>
    /// Whether a point lies on the playground's yard, its toys and bunting, or on the field path from the road. Plants
    /// that stand there are removed and the small friends move out, so this is generous.
    /// </summary>
    internal bool InPlaygroundZone(Vector3 p)
    {
        if (playgroundSite is not { } site)
            return false;
        if (
            InSiteRectangle(
                p,
                site.Position,
                site.Inward,
                PlaygroundSite.AlongMin,
                PlaygroundSite.AlongMax,
                PlaygroundSite.AcrossMax
            )
        )
            return true;
        return SegmentDistance(p, site.Road, site.Position) < 5;
    }
}
