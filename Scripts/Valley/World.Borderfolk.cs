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
/// The four keepers of the borders (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 1): each lives at the end of the road
/// to a border, with a homestead straight on beyond it. The lighthouse's place on the shore and the coast quarry beside
/// the road to the sea are chosen here as well, so that the fingerprint of the coast never changes when they are built.
/// Everything is placed once the roads beyond the old valley are laid and the ground is final, from the roads alone and
/// without random numbers; nothing that was there moves: the plants of the land beyond the old valley that stand on the
/// homesteads are planned as before and only left out when their block is built (<see cref="Vegetation.OutlandCleared"/>).
/// </summary>
public partial class World
{
    internal const int FirstBorderKeeperId = ActorIds.FirstBorderKeeper;

    internal static bool IsBorderKeeper(int id) => id is >= ActorIds.FirstBorderKeeper and <= ActorIds.LastBorderKeeper;

    /// <summary>The homesteads at the ends of the roads to the borders, by <see cref="Border"/>; null where none stands.</summary>
    internal BorderHomeSite?[] BorderHomes { get; } = new BorderHomeSite?[4];

    /// <summary>The keepers of the borders, by <see cref="Border"/>; null where no homestead stands.</summary>
    internal Character?[] Keepers { get; } = new Character?[4];

    /// <summary>The models of the homesteads, by <see cref="Border"/>.</summary>
    internal BorderHome?[] Homesteads { get; } = new BorderHome?[4];

    /// <summary>Where the lighthouse stands on the shore beyond the end of the road to the sea, or null without it.</summary>
    internal LighthouseSite? LighthousePlace { get; private set; }

    /// <summary>Where the trailer is loaded with stone for the lighthouse, beside the road to the sea.</summary>
    internal CoastQuarrySite? QuarryPlace { get; private set; }

    /// <summary>The keeper of a border, or null.</summary>
    internal Character? KeeperOf(Border border) => Keepers[(int)border];

    /// <summary>The border whose keeper has this actor ID; only for <see cref="IsBorderKeeper"/>.</summary>
    internal static Border BorderOf(int id) => (Border)(id - ActorIds.FirstBorderKeeper);

    /// <summary>The lighthouse keeper's own border, by the sea.</summary>
    internal Character? LighthouseKeeper => KeeperOf(Border.Sea);

    /// <summary>
    /// Chooses the homesteads, the lighthouse's place and the coast quarry from the roads to the borders, and keeps the
    /// plants of the land beyond the old valley off them.
    /// </summary>
    private void PlanBorderfolk()
    {
        foreach (var (border, points) in BorderRoads)
        {
            var end = points[^1];
            var inward = (end - points[^2]) with { Y = 0 };
            if (inward.LengthSquared() < .01f)
                continue;
            inward = inward.Normalized();
            var road = Ground.At(end.X, end.Z);
            BorderHomes[(int)border] = new BorderHomeSite(border, road, inward, Mathf.Atan2(inward.X, inward.Z));
        }
        if (BorderHomes[(int)Border.Sea] is { } sea)
        {
            LighthousePlace = PlanLighthouse(sea);
            var seaRoad = BorderRoads.FirstOrDefault(road => road.Border == Border.Sea).Points;
            if (seaRoad != null)
                QuarryPlace = PlanCoastQuarry(seaRoad);
        }
        // The dragon lies beside the homestead under the mountains; his ledge is cleared like the homesteads.
        PlanDragon();
        // The balloon station stands on high ground beside a road far from the village, cleared in the same way.
        PlanBalloon();
        Plants.OutlandCleared = InBorderfolkZone;
        GD.Print(
            $"BORDERFOLK homes={BorderHomes.Count(h => h != null)} lighthouse={LighthousePlace != null} quarry={QuarryPlace != null}"
        );
    }

    /// <summary>
    /// The lighthouse stands on the shore straight on beyond the keeper's homestead: as far towards the sea as the
    /// ground stays dry, a few metres short of the water, but never in the yard.
    /// </summary>
    private LighthouseSite PlanLighthouse(BorderHomeSite home)
    {
        float least = BorderHomeSite.YardEnd + LighthouseSite.Radius + 3;
        float dry = least;
        for (float along = least; along <= 140; along += 2)
        {
            var p = home.Local(0, along);
            if (Ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + 1.6f)
                break;
            dry = along;
        }
        float at = Mathf.Max(least, dry - 4);
        var foot = home.Local(0, at);
        var drop = home.Local(-4.5f, 2.5f);
        return new LighthouseSite(Ground.At(foot.X, foot.Z), Ground.At(drop.X, drop.Z), home.Road, home.Yaw);
    }

    /// <summary>
    /// The coast quarry: an outcrop beside the road to the sea, a little way on from where the road leaves the ring, on
    /// dry ground off every road and away from both rivers.
    /// </summary>
    private CoastQuarrySite? PlanCoastQuarry(Vector3[] points)
    {
        var start = points[0] with { Y = 0 };
        var direction = (points[1] - points[0]) with { Y = 0 };
        if (direction.LengthSquared() < .01f)
            return null;
        direction = direction.Normalized();
        var right = Vector3.Up.Cross(direction);
        var borders = WorldLayout.Plan.Borders;
        foreach (float along in new[] { 45f, 60f, 75f, 35f, 90f, 110f })
        foreach (int side in new[] { 1, -1 })
        {
            var road = start + direction * along;
            var p = road + right * side * 13;
            if (
                !ValleyBounds.Contains(p.X, p.Z, CoastQuarrySite.Radius + 6)
                || OutlandRoutes.Distance(p) < CoastQuarrySite.Radius + 3
                || Routes.Distance(p) < 20
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 12
                || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 12
                || borders.Sand(p.X, p.Z) > .05f
                || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 20) != null
            )
                continue;
            float low = float.MaxValue,
                high = float.MinValue;
            foreach (var (dx, dz) in new[] { (0f, 0f), (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                float h = Ground.Height(p.X + dx * CoastQuarrySite.Radius, p.Z + dz * CoastQuarrySite.Radius);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
            if (high - low > 3.5f || low < ValleyBorders.SeaLevel + 2)
                continue;
            var inward = (p - road).Normalized();
            return new CoastQuarrySite(
                Ground.At(p.X, p.Z),
                Ground.At(road.X, road.Z),
                inward,
                Mathf.Atan2(inward.X, inward.Z)
            );
        }
        return null;
    }

    /// <summary>Raises the homesteads and their keepers, who have the actor IDs after the hunter's.</summary>
    private void BuildBorderfolk()
    {
        foreach (var site in BorderHomes)
        {
            if (site == null)
                continue;
            var home = new BorderHome(site, Ground) { Name = $"BorderHome{site.Border}" };
            AddChild(home);
            // Built after the scenery was outlined, so the homestead is outlined on its own, like the village.
            Art.Outline(home, .025f, .3f);
            Homesteads[(int)site.Border] = home;
            var keeper = Character.Create(CharacterCatalog.BorderKeepers[(int)site.Border]);
            keeper.Role = new BorderKeeperRole(site.Border);
            keeper.DressAsKeeper(site.Border);
            keeper.Position = Ground.At(site.KeeperSpot.X, site.KeeperSpot.Z);
            keeper.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
            AddChild(keeper);
            Register(ActorIds.FirstBorderKeeper + (int)site.Border, keeper);
            Keepers[(int)site.Border] = keeper;
        }
        BuildLighthouse();
        BuildDragon();
        BuildBalloon();
        BuildTracking();
        BuildSheep();
        BuildRally();
        BuildExplorer();
        BuildShells();
        BuildFerry();
        BuildCastle();
    }

    /// <summary>The lighthouse on the shore, raised by the player's deliveries.</summary>
    internal Lighthouse? Lighthouse { get; private set; }

    /// <summary>Whether the player raises the lighthouse; without the works for its goods it stands from the start.</summary>
    internal bool LighthouseBuildable { get; private set; }

    /// <summary>The boat with lit windows that sails past the lighthouse by night.</summary>
    internal NightBoat? NightBoat { get; private set; }

    /// <summary>
    /// Raises the coast quarry, the lighthouse (not begun, or standing where the works for its planks, bricks and glasses
    /// are missing), the boat on the sea and the gull, the lighthouse keeper's pet, which comes when the lantern is up.
    /// </summary>
    private void BuildLighthouse()
    {
        if (LighthousePlace is not { } site || LighthouseKeeper is not { } keeper)
            return;
        if (QuarryPlace is { } quarrySite)
        {
            var quarry = new CoastQuarry(quarrySite, Ground) { Name = "CoastQuarry" };
            AddChild(quarry);
            Art.Outline(quarry, .025f, .3f);
        }
        var lighthouse = new Lighthouse(site, Ground) { Keeper = keeper, Quarry = QuarryPlace };
        bool buildable =
            QuarryPlace != null
            && new[] { HaulLoad.Planks, HaulLoad.Bricks, HaulLoad.Glass }.All(load =>
                WorksFor(HaulLoads.ProductOf(load)) != null
            );
        LighthouseBuildable = buildable;
        lighthouse.SetStage(buildable ? 0 : Lighthouse.Stages);
        AddChild(lighthouse);
        Art.Outline(lighthouse, .025f, .3f);
        Lighthouse = lighthouse;
        var home = BorderHomes[(int)Border.Sea]!;
        var sea = site.Foot + home.Inward * 140;
        var boat = new NightBoat(
            new Vector3(sea.X, ValleyBorders.SeaLevel, sea.Z) - home.Right * 160,
            new Vector3(sea.X, ValleyBorders.SeaLevel, sea.Z) + home.Right * 160
        );
        AddChild(boat);
        NightBoat = boat;
        var gull = new CollectorPet(
            WorldPetCatalog.Gull,
            keeper,
            () => lighthouse.Stage,
            Lighthouse.Stages,
            worldPets.Count
        );
        var seat = keeper.Transform * new Vector3(-1.4f, 0, .6f);
        gull.SetHome(Ground.At(seat.X, seat.Z), keeper.Rotation.Y);
        AddChild(gull);
        AddWorldPet(gull);
    }

    /// <summary>
    /// Whether a point lies on a homestead and the turning space before it, on the lighthouse's ground, on the coast
    /// quarry, by the dragon, at the balloon station or on a den of the forest keeper's trails: the plants, the grass, the wayside things and the wild animals
    /// of the land beyond the old valley keep off.
    /// </summary>
    internal bool InBorderfolkZone(Vector3 p)
    {
        if (
            InDragonZone(p)
            || InBalloonZone(p)
            || InTrackingZone(p)
            || InExplorerZone(p)
            || InShellsZone(p)
            || InFerryZone(p)
            || InCastleZone(p)
        )
            return true;
        foreach (var site in BorderHomes)
            if (
                site != null
                && InSiteRectangle(
                    p,
                    site.Road,
                    site.Inward,
                    BorderHomeSite.AlongMin,
                    BorderHomeSite.AlongMax,
                    BorderHomeSite.AcrossMax
                )
            )
                return true;
        if (LighthousePlace is { } light && FlatDistance(p, light.Foot) < LighthouseSite.Radius + 2)
            return true;
        if (QuarryPlace is { } quarry)
        {
            if (FlatDistance(p, quarry.Position) < CoastQuarrySite.Radius + 2)
                return true;
            if (SegmentDistance(p, quarry.Road, quarry.Position) < 4)
                return true;
        }
        return false;
    }

    /// <summary>The homesteads and keepers in the order of the borders, for the fingerprint of the coast.</summary>
    internal IEnumerable<(BorderHomeSite Site, Character? Keeper)> BorderfolkPlaces() =>
        BorderHomes.Where(site => site != null).Select(site => (site!, Keepers[(int)site!.Border]));
}
