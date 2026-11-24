using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The dragon under the mountains (<c>docs/DRAGON_PLAN.md</c>). He lies on a rock ledge beside the homestead at the end
/// of the road to the mountains, facing the valley, with his nest and the drop ring in front of his head; the mountain
/// slope where the trailer is loaded lies beside the same road, near the ring. Both are chosen with the homesteads, from
/// the roads alone and without random numbers, so nothing that was there moves: the plants of the land beyond the old
/// valley that stand there are planned as before and only left out when their block is built
/// (<see cref="InBorderfolkZone"/>). The places are held by <c>Tests/Worlds/seed-N.dragon.txt</c>.
/// </summary>
public partial class World
{
    /// <summary>Where the dragon lies, or null in a valley without a road to the mountains or room beside its end.</summary>
    internal DragonSite? DragonPlace { get; private set; }

    /// <summary>Where the trailer is loaded for the dragon's nest, beside the road to the mountains.</summary>
    internal MountainSlopeSite? SlopePlace { get; private set; }

    /// <summary>The dragon to talk to: a character without a body of its own under his chin (the body is <see cref="DragonBody"/>).</summary>
    internal Character? Dragon { get; private set; }

    /// <summary>The dragon himself, who breathes, looks round, sneezes and flies.</summary>
    internal Dragon? DragonBody { get; private set; }

    /// <summary>The ledge, the cave and the nest, raised by the player's deliveries.</summary>
    internal DragonLair? DragonLair { get; private set; }

    /// <summary>The way the dragon flies round the valley with the player on his back.</summary>
    internal DragonFlight? DragonFlight { get; private set; }

    /// <summary>How far from the end of the road the dragon may lie, sideways, in the homestead's frame; the nearest that fits.</summary>
    private static readonly float[] DragonAcross = { 21, 26, 31, 36, 41 };

    /// <summary>How far on beyond the end of the road the drop ring lies, in the homestead's frame.</summary>
    private const float DragonDropAlong = 3;

    /// <summary>
    /// How far back down the road from its end the dragon may lie on the bank above it, when there is no room beside the
    /// homestead; the nearest that fits.
    /// </summary>
    private static readonly float[] DragonRoadBack = { 18, 26, 34, 44, 56, 70, 86 };

    /// <summary>How far from the middle of the road the drop ring lies when he lies above the road.</summary>
    private const float DragonDropOffRoad = 2;

    /// <summary>
    /// Chooses the dragon's ledge under the mountains and the mountain slope on its road. He lies beside the homestead at
    /// the end of the road, looking back down the valley, where the ground there is level with the road; where the road
    /// ends in a cut, he lies on its bank a little way back, looking down at the road, with his nest and the drop ring at
    /// its edge.
    /// </summary>
    private void PlanDragon()
    {
        if (BorderHomes[(int)Border.Mountains] is not { } home)
            return;
        var road = BorderRoads.FirstOrDefault(r => r.Border == Border.Mountains).Points;
        DragonPlace = BesideHomestead(home) ?? (road == null ? null : AboveRoad(home, road));
        if (DragonPlace != null && road != null)
            SlopePlace = PlanMountainSlope(road);
        // Everything for the nest is loaded at the slope, and the dragon asks its question: no slope, no dragon.
        if (SlopePlace == null)
            DragonPlace = null;
        GD.Print($"DRAGON lair={DragonPlace != null} slope={SlopePlace != null}");
    }

    /// <summary>A ledge with its drop ring and the dragon's nest at the given place, the fire away from the homestead.</summary>
    private DragonSite DragonAt(Vector3 drop, Vector3 facing, Vector3 road, BorderHomeSite home)
    {
        var lair = drop - facing * DragonSite.DropAhead;
        int fireSide = Vector3.Up.Cross(facing).Dot(home.Road - lair) > 0 ? -1 : 1;
        return new DragonSite(Ground.At(lair.X, lair.Z), facing, Mathf.Atan2(facing.X, facing.Z), road, fireSide);
    }

    private DragonSite? BesideHomestead(BorderHomeSite home)
    {
        foreach (float across in DragonAcross)
        foreach (int side in new[] { 1, -1 })
        {
            var site = DragonAt(home.Local(side * across, DragonDropAlong), -home.Inward, home.Road, home);
            if (DragonFits(site, home, aboveRoad: false))
                return site;
        }
        return null;
    }

    private DragonSite? AboveRoad(BorderHomeSite home, Vector3[] road)
    {
        using var curve = WorldPlan.CreateCurve(road);
        float length = curve.GetBakedLength();
        foreach (float back in DragonRoadBack)
        foreach (int side in new[] { 1, -1 })
        {
            if (back + 2 >= length)
                continue;
            var middle = curve.SampleBaked(length - back);
            var along = (curve.SampleBaked(length - back + 2) - curve.SampleBaked(length - back - 2)) with { Y = 0 };
            if (along.LengthSquared() < .01f)
                continue;
            var right = Vector3.Up.Cross(along.Normalized());
            var drop = middle + right * side * DragonDropOffRoad;
            var site = DragonAt(drop, -right * side, Ground.At(middle.X, middle.Z), home);
            if (DragonFits(site, home, aboveRoad: true))
                return site;
        }
        return null;
    }

    /// <summary>
    /// The ledge, the nest and the drop ring are on dry ground inside the valley, off the old roads and both rivers and
    /// clear of the homestead. Beside the homestead they keep off the road too, and the car can drive from the end of the
    /// road to the drop ring; above the road, the drop ring and the place under his chin are on it, and the dragon and his
    /// cave keep off it and every other road.
    /// </summary>
    private bool DragonFits(DragonSite site, BorderHomeSite home, bool aboveRoad)
    {
        var borders = WorldLayout.Plan.Borders;
        var points = new List<(Vector3 P, float Ahead)>();
        for (float ahead = DragonSite.ZoneBack; ahead <= DragonSite.ZoneFront; ahead += 3)
        for (float across = -DragonSite.ZoneAcross; across <= DragonSite.ZoneAcross; across += DragonSite.ZoneAcross)
            points.Add((site.Local(across, ahead), ahead));
        foreach (var (p, _) in points)
            if (
                !ValleyBounds.Contains(p.X, p.Z, 4)
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 10
                || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 10
                || Routes.Distance(p) < 20
                || InSiteRectangle(
                    p,
                    home.Road,
                    home.Inward,
                    BorderHomeSite.AlongMin,
                    BorderHomeSite.AlongMax,
                    BorderHomeSite.AcrossMax
                )
            )
                return false;
        foreach (var p in new[] { site.Drop, site.Nest, site.Talk, site.Local(0, 5) })
            if (!ValleyBounds.Contains(p.X, p.Z, 8))
                return false;
        if (aboveRoad)
        {
            if (OutlandRoutes.Distance(site.Drop) > 4 || OutlandRoutes.Distance(site.Talk) > 5)
                return false;
            foreach (var (p, ahead) in points)
                if (ahead < DragonSite.NestAhead - 3 && OutlandRoutes.Distance(p) < 9)
                    return false;
            if (OutlandRoutes.Distance(site.Local(site.FireSide * 4.4f, DragonSite.NestAhead - .8f)) < 6)
                return false;
        }
        else
            foreach (var p in new[] { site.Drop, site.Nest, site.Talk, site.Local(0, 5) })
                if (OutlandRoutes.Distance(p) < 6)
                    return false;
        // The ground under the dragon (and, beside the homestead, the nest) may rise towards the mountains, but not like a
        // wall; his cave behind him may lie in the hillside.
        float low = float.MaxValue,
            high = float.MinValue;
        float front = aboveRoad ? 6 : DragonSite.NestAhead + 3;
        foreach (var (p, ahead) in points)
            if (ahead > -10 && ahead < front)
            {
                float h = Ground.Height(p.X, p.Z);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
        if (high - low > 12)
            return false;
        if (aboveRoad)
            return true;
        // From the end of the road to the drop ring and on to the nest, the car climbs no steeper than 30 degrees.
        var from = home.Road;
        foreach (var to in new[] { site.Drop, site.Nest })
        {
            float length = FlatDistance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 2.5f));
            float last = Ground.Height(from.X, from.Z);
            for (int i = 1; i <= steps; i++)
            {
                var p = from.Lerp(to, i / (float)steps);
                float h = Ground.Height(p.X, p.Z);
                if (Mathf.Abs(h - last) > .6f * length / steps)
                    return false;
                last = h;
            }
            from = to;
        }
        return true;
    }

    /// <summary>
    /// The mountain slope: fallen trees, scree and a sandy bank beside the road to the mountains, a little way on from
    /// where it leaves the ring, on dry ground off every road and away from both rivers.
    /// </summary>
    private MountainSlopeSite? PlanMountainSlope(Vector3[] points)
    {
        var start = points[0] with { Y = 0 };
        var direction = (points[1] - points[0]) with { Y = 0 };
        if (direction.LengthSquared() < .01f)
            return null;
        direction = direction.Normalized();
        var right = Vector3.Up.Cross(direction);
        var borders = WorldLayout.Plan.Borders;
        foreach (float along in new[] { 45f, 60f, 75f, 35f, 90f, 110f, 130f })
        foreach (int side in new[] { 1, -1 })
        {
            var road = start + direction * along;
            var p = road + right * side * 13;
            if (
                !ValleyBounds.Contains(p.X, p.Z, MountainSlopeSite.Radius + 6)
                || OutlandRoutes.Distance(p) < MountainSlopeSite.Radius + 3
                || Routes.Distance(p) < 20
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 12
                || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 12
                || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 20) != null
                || InDragonZone(p)
            )
                continue;
            float low = float.MaxValue,
                high = float.MinValue;
            foreach (var (dx, dz) in new[] { (0f, 0f), (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                float h = Ground.Height(p.X + dx * MountainSlopeSite.Radius, p.Z + dz * MountainSlopeSite.Radius);
                low = Mathf.Min(low, h);
                high = Mathf.Max(high, h);
            }
            if (high - low > 3.5f)
                continue;
            var inward = (p - road).Normalized();
            return new MountainSlopeSite(
                Ground.At(p.X, p.Z),
                Ground.At(road.X, road.Z),
                inward,
                Mathf.Atan2(inward.X, inward.Z)
            );
        }
        return null;
    }

    /// <summary>
    /// Raises the ledge with its cave and nest, the dragon on it and the character under his chin, the mountain slope and
    /// the dragon's little one, which waits by the nest until the egg has hatched.
    /// </summary>
    private void BuildDragon()
    {
        if (DragonPlace is not { } site)
            return;
        if (SlopePlace is { } slopeSite)
        {
            var slope = new MountainSlope(slopeSite, Ground);
            AddChild(slope);
            Art.Outline(slope, .025f, .3f);
        }
        var lair = new DragonLair(site, Ground) { Slope = SlopePlace };
        AddChild(lair);
        Art.Outline(lair, .025f, .3f);
        DragonLair = lair;
        var body = new Dragon { Name = "Dragon" };
        AddChild(body);
        body.Rest(lair.DragonRest);
        Art.Outline(body, .03f);
        DragonBody = body;
        var dragon = Character.CreateDragon(CharacterCatalog.Dragon, lair.HeadHeight);
        dragon.Role = new DragonRole();
        dragon.Position = Ground.At(site.Talk.X, site.Talk.Z);
        dragon.Rotation = new Vector3(0, site.Yaw, 0);
        AddChild(dragon);
        Dragon = dragon;
        lair.Dragon = dragon;
        DragonFlight = new DragonFlight(lair.DragonRest, Ground);
        var little = new CollectorPet(
            WorldPetCatalog.Dragonling,
            dragon,
            () => lair.Hatched ? 1 : 0,
            1,
            worldPets.Count
        );
        little.SetHome(lair.LittleHome, site.Yaw + Mathf.Pi);
        AddChild(little);
        AddWorldPet(little);
    }

    /// <summary>The dragon is registered after the postman, who came before him.</summary>
    private void RegisterDragon()
    {
        if (Dragon != null)
            Register(DragonId, Dragon);
    }

    /// <summary>The ledge, the cave, the nest and the drop ring, and the mountain slope with its way from the road.</summary>
    internal bool InDragonZone(Vector3 p)
    {
        if (
            DragonPlace is { } site
            && InSiteRectangle(
                p,
                site.Lair,
                site.Facing,
                DragonSite.ZoneBack,
                DragonSite.ZoneFront,
                DragonSite.ZoneAcross
            )
        )
            return true;
        if (SlopePlace is { } slope)
        {
            if (FlatDistance(p, slope.Position) < MountainSlopeSite.Radius + 2)
                return true;
            if (SegmentDistance(p, slope.Road, slope.Position) < 4)
                return true;
        }
        return false;
    }
}
