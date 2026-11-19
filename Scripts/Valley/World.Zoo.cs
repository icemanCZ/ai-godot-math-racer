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
    internal const int ZooKeeperId = ActorIds.ZooKeeper;

    internal static bool IsZooKeeper(int id) => id == ZooKeeperId;

    /// <summary>The zoo at the edge of the village, or null in a valley that has no playground to be its neighbour.</summary>
    internal Zoo? Zoo { get; private set; }

    private ZooSite? zooSite;

    /// <summary>The zoo stands a little further out than the playground, still in sight of the houses.</summary>
    internal static readonly VillageBand ZooBand = new(30, 150);

    /// <summary>
    /// The zoo keeps this far from the works, the buildings, the glassworks, the farm and the bakery and, because it is
    /// much bigger, from the playground; when the valley is crowded, a little less.
    /// </summary>
    private const float ZooGap = 52;

    private static readonly SpurRules[] ZooPasses =
    {
        new(8, 120, 24, 3.2f, 8, 8, 50000),
        new(6, 260, 12, 5f, 6, 6, 140000),
    };

    private static readonly float[] ZooAcross = { -14f, -7f, 0, 7f, 14f };
    private static readonly float[] ZooAlong = { -2f, 4f, 10f, 16f, 22f, 28f, 31f };

    /// <summary>
    /// Plans the zoo after the playground, with a seeded search of its own, so every older site stays where it was.
    /// Like the playground it reserves no ground: the plants on its area are taken out afterwards
    /// (<see cref="InZooZone"/>) and a small friend who hides there is moved. It is much larger than a works, so besides
    /// the usual rules of <see cref="FindSpur"/> the whole area is tested for water, roads, houses, steep ground and
    /// other sites. A valley with room for it gets one; the search loosens its rules step by step.
    /// </summary>
    private ZooSite? PlanZoo(FordSite ford, IndustryPlan industry, BuildPlan? builds, WorksSite? glass, FarmPlan? farm)
    {
        if (playgroundSite is not { } yard)
            return null;
        var random = new Random(Settings.Seed + 2391);
        var friends = InteractionActors.ToArray();
        var friendSpots = ActorSpots(friends);
        var others = new List<(Vector3 Position, float Gap)> { (yard.Position, ZooGap + 14) };
        foreach (var works in industry.Sites)
            others.Add((works.Position, ZooGap * .8f));
        if (glass != null)
            others.Add((glass.Position, ZooGap));
        if (farm != null)
        {
            others.Add((farm.Farm.Position, ZooGap + 6));
            others.Add((farm.Bakery.Position, ZooGap));
        }
        if (builds != null)
            foreach (var build in builds.Sites)
                others.Add((build.Position, ZooGap * .7f));
        bool Apart(Vector3 p, float factor) =>
            others.All(other => FlatDistance(p, other.Position) >= other.Gap * factor);

        Array.Clear(industryRejections);
        Vector3 road = default,
            ring = default,
            inward = default;
        bool Try(VillageBand? band, float factor, SpurRules[] passes, float slope) =>
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
                (from, to) =>
                    AreaIsFree(
                        from,
                        to,
                        industry,
                        builds,
                        ZooSite.AlongMin,
                        ZooSite.AlongMax,
                        ZooSite.AcrossMax,
                        avoidPlayground: true
                    ) && ZooFootprintFits(to, (to - from) with { Y = 0 }, friendSpots, slope)
            );
        if (
            !Try(ZooBand, 1, ZooPasses[..1], 3.5f)
            && !Try(ZooBand, .8f, ZooPasses, 5f)
            && !Try(null, 1, ZooPasses, 5f)
            && !Try(null, .7f, ZooPasses, 6f)
            && !Try(null, .5f, ZooPasses, 7f)
        )
        {
            GD.Print(
                $"ZOO_MISSING seed={Settings.Seed} rejected(bank,river,spacing,spot,footprint,path)={string.Join(",", industryRejections)}"
            );
            return null;
        }
        var right = Vector3.Up.Cross(inward);
        var keeper = Ground.At(ring.X - right.X * 4.4f + inward.X * 1.2f, ring.Z - right.Z * 4.4f + inward.Z * 1.2f);
        zooSite = new ZooSite(ring, road, inward, keeper);
        GD.Print($"ZOO_READY way={FlatDistance(road, ring):0} village={Village.PlotDistance(ring):0}");
        return zooSite;
    }

    /// <summary>
    /// The whole zoo, which reaches 31 m behind the ring and 14 m to each side, stands clear of water, houses, roads,
    /// the tower, the roadside friends and the places where raw materials are picked up, on ground that does not climb
    /// more than <paramref name="slope"/> metres (every enclosure stands on a level pad of its own).
    /// </summary>
    private bool ZooFootprintFits(Vector3 ring, Vector3 inward, Vector2[] friendSpots, float slope)
    {
        inward = inward.Normalized();
        var right = Vector3.Up.Cross(inward);
        var tower = TowerSpot;
        foreach (float x in ZooAcross)
        foreach (float z in ZooAlong)
        {
            var q = ring + right * x + inward * z;
            q = Ground.At(q.X, q.Z);
            if (
                Mathf.Abs(q.Y - ring.Y) > slope
                || River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 6
                || AnyCloserThan(friendSpots, q.X, q.Z, 8)
                || AnyCloserThan(SpecialPickupSpots, q.X, q.Z, 9)
                || CloserThan(tower, q.X, q.Z, 16)
                || Village.IsReserved(q)
                || Village.PlotDistance(q) < 8
                || IsPickupApproachReserved(q)
                || (z > 1 && Routes.Distance(q) < 6)
            )
                return false;
        }
        return true;
    }

    /// <summary>Raises the zoo and its keeper, who has the next free actor ID, registered after the railway's stationmaster.</summary>
    private void BuildZoo()
    {
        if (zooSite is not { } site)
            return;
        var zoo = new Zoo(site, Ground);
        AddChild(zoo);
        var keeper = Character.Create(CharacterCatalog.ZooKeeper);
        keeper.Role = new ZooRole(zoo);
        keeper.DressAsZookeeper();
        keeper.Position = site.HelperSpot;
        var facing = site.Position - site.HelperSpot;
        keeper.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
        AddChild(keeper);
        zoo.Helper = keeper;
        Register(ZooKeeperId, keeper);
        Zoo = zoo;
        // The finished zoo's pet, a baby panda, waits beside the keeper.
        var pet = new CollectorPet(WorldPetCatalog.PandaCub, keeper, () => zoo.Stage, ZooPlans.Trips, worldPets.Count);
        var right = Vector3.Up.Cross(site.Inward);
        pet.SetHome(site.HelperSpot - right * 1.3f - site.Inward * .2f, keeper.Rotation.Y);
        AddChild(pet);
        AddWorldPet(pet);
    }

    /// <summary>
    /// Whether a point lies on the zoo's area or on the field path from the road. Plants that stand there are removed
    /// and the small friends move out, so this is generous.
    /// </summary>
    internal bool InZooZone(Vector3 p)
    {
        if (zooSite is not { } site)
            return false;
        var offset = new Vector3(p.X - site.Position.X, 0, p.Z - site.Position.Z);
        float along = offset.Dot(site.Inward);
        float across = offset.Dot(Vector3.Up.Cross(site.Inward));
        if (along >= ZooSite.AlongMin && along <= ZooSite.AlongMax && Mathf.Abs(across) <= ZooSite.AcrossMax)
            return true;
        return SegmentDistance(p, site.Road, site.Position) < 5;
    }
}
