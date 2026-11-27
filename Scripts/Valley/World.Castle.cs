using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The castle on the hill (<c>docs/CASTLE_PLAN.md</c>): a castle beside a road of the land beyond the old valley, on the
/// highest ground those roads pass where its square of walls fits, far from the village and from the balloon, so that
/// it is seen from all over the valley; and its four beacons, one beside the homestead of each border. Everything is
/// planned once the ferry is, from the roads and the ground alone and without random numbers, so nothing planned before
/// moves: the plants of the land beyond the old valley that stand there are planned as before and only left out when
/// their block is built (<see cref="InBorderfolkZone"/>). The places are held by <c>Tests/Worlds/seed-N.castle.txt</c>.
/// </summary>
public partial class World
{
    internal const int CastellanId = ActorIds.Castellan;

    internal static bool IsCastellan(int id) => id == CastellanId;

    /// <summary>
    /// The ID of the guest of a chair of the feast: the four keepers of the borders in their order, the balloonist, the
    /// owl and the heron.
    /// </summary>
    internal static int CastleGuestId(int chair) =>
        chair switch
        {
            < 4 => FirstBorderKeeperId + chair,
            CastlePlans.BalloonistGuest => BalloonistId,
            CastlePlans.CartographerGuest => CartographerId,
            _ => BirdwatcherId,
        };

    /// <summary>The castle keeps this far from the middle of the village, and its middle this far from the balloon.</summary>
    private const float CastleFromVillage = 480,
        CastleFromBalloon = 300;

    /// <summary>The castle keeps this far from the homesteads of the borders, the lighthouse, the dragon and the landing.</summary>
    private const float CastleFromSights = 160;

    /// <summary>How far apart the places along the roads are that the castle is tried at.</summary>
    private const float CastleStep = 8;

    /// <summary>Where the castle stands, or null in a valley without room for it beside a road.</summary>
    internal CastleSite? CastlePlace { get; private set; }

    /// <summary>Where the beacons stand, by the border (null where a border has no homestead or no room beside it).</summary>
    internal BeaconSite?[] BeaconPlaces { get; } = new BeaconSite?[CastlePlans.Beacons];

    /// <summary>The castle, built with sixteen deliveries.</summary>
    internal Castle? Castle { get; private set; }

    /// <summary>The beacons, by the border.</summary>
    internal BeaconTower?[] Beacons { get; } = new BeaconTower?[CastlePlans.Beacons];

    /// <summary>The princess at the gate, Lvice kněžna Libuše.</summary>
    internal Character? Castellan { get; private set; }

    /// <summary>The guests of the feast by their chair, and the road point before each one's home; null without them.</summary>
    internal Character?[] CastleGuests { get; } = new Character?[CastlePlans.Guests];

    internal Vector3[] CastleGuestRoads { get; } = new Vector3[CastlePlans.Guests];

    /// <summary>The guests that live in this valley: the ghost comes once all of them sit at the table.</summary>
    internal int CastleGuestCount => CastleGuests.Count(guest => guest != null);

    /// <summary>Whether the player builds the castle; without the works and the log yard it stands from the start.</summary>
    internal bool CastleBuildable { get; private set; }

    /// <summary>Whether the player builds the beacons; without the log yard they burn from the start.</summary>
    internal bool BeaconsBuildable { get; private set; }

    /// <summary>The beacons that stand in this valley.</summary>
    internal int BeaconCount => Beacons.Count(beacon => beacon != null);

    /// <summary>
    /// Chooses the castle's place: of every place beside a road of the land beyond the old valley where the castle fits
    /// (<see cref="CastleFits"/>), the one whose middle lies highest; if none fits, the same with looser rules for the
    /// slope of the ground under its walls. Then a beacon beside the homestead of every border.
    /// </summary>
    private void PlanCastle()
    {
        var places = new List<(float Height, int Order, CastleSite Site)>();
        foreach (var route in OutlandRoutePoints)
        {
            if (route.Length < 2)
                continue;
            using var curve = WorldPlan.CreateCurve(route);
            float length = curve.GetBakedLength();
            for (float along = 10; along + 10 < length; along += CastleStep)
            {
                var middle = curve.SampleBaked(along);
                var direction = (curve.SampleBaked(along + 2) - curve.SampleBaked(along - 2)) with { Y = 0 };
                if (direction.LengthSquared() < .01f)
                    continue;
                var right = Vector3.Up.Cross(direction.Normalized());
                foreach (int side in new[] { 1, -1 })
                {
                    var inward = right * side;
                    var site = new CastleSite(Ground.At(middle.X, middle.Z), inward, Mathf.Atan2(inward.X, inward.Z));
                    places.Add((Ground.Height(site.Center.X, site.Center.Z), places.Count, site));
                }
            }
        }
        // Highest first; of two at the same height, the one found first.
        places.Sort((a, b) => a.Height != b.Height ? b.Height.CompareTo(a.Height) : a.Order.CompareTo(b.Order));
        foreach (float relief in new[] { 3f, 4.5f, 6f, 8f })
        {
            foreach (var (_, _, site) in places)
                if (CastleFits(site, relief))
                {
                    CastlePlace = site;
                    break;
                }
            if (CastlePlace != null)
                break;
        }
        if (CastlePlace != null)
            PlanBeacons();
        GD.Print($"CASTLE castle={CastlePlace != null} beacons={BeaconPlaces.Count(b => b != null)}");
    }

    /// <summary>
    /// The castle, its way from the road and the place of the loads are on dry ground inside the valley and beyond the
    /// old one, far from the village, off every road and both rivers, clear of everything planned before; the ground
    /// under the walls is no steeper than <paramref name="relief"/> metres from its lowest to its highest point, and the
    /// car can drive from the road up to the gate.
    /// </summary>
    private bool CastleFits(CastleSite site, float relief)
    {
        var center = site.Center;
        if (FlatDistance(center, Village.Center) < CastleFromVillage || !ValleyBounds.Contains(center.X, center.Z, 40))
            return false;
        if (BalloonPlace is { } balloon && FlatDistance(center, balloon.Pad) < CastleFromBalloon)
            return false;
        foreach (var home in BorderHomes)
            if (home != null && FlatDistance(center, home.Road) < CastleFromSights)
                return false;
        if (
            LighthousePlace is { } light && FlatDistance(center, light.Foot) < CastleFromSights
            || QuarryPlace is { } quarry && FlatDistance(center, quarry.Position) < 80
            || DragonPlace is { } dragon && FlatDistance(center, dragon.Lair) < CastleFromSights
            || SlopePlace is { } mountain && FlatDistance(center, mountain.Position) < 80
            || FerryPlace is { } ferry && FlatDistance(center, ferry.Landing) < CastleFromSights
            || CartographerPlace is { } owl && FlatDistance(center, owl.Road) < 120
            || ShellCove is { } cove && FlatDistance(center, cove) < 120
        )
            return false;
        var borders = WorldLayout.Plan.Borders;
        for (float along = CastleSite.ZoneAlongMin; along <= CastleSite.ZoneAlongMax; along += 3.5f)
        for (float across = -CastleSite.ZoneAcross; across <= CastleSite.ZoneAcross; across += 3.5f)
        {
            var p = site.Local(across, along);
            if (
                Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) < WorldTuning.CoreRadius + 8
                || !ValleyBounds.Contains(p.X, p.Z, 6)
                || along > 7 && OutlandRoutes.Distance(p) < 8
                || Routes.Distance(p) < 20
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 12
                || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 12
                || borders.Sand(p.X, p.Z) > .05f
                || Ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + 3
                || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 25) != null
                || InBorderfolkZone(p)
                || NearCastleSights(p)
            )
                return false;
        }
        // The ground under the walls is nearly level.
        float low = float.MaxValue,
            high = float.MinValue;
        float half = CastleSite.HalfWidth + 2;
        for (float along = CastleSite.FrontAlong - 2; along <= CastleSite.BackAlong + 2; along += 2.5f)
        for (float across = -half; across <= half; across += 2.5f)
        {
            var p = site.Local(across, along);
            float h = Ground.Height(p.X, p.Z);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        if (high - low > relief)
            return false;
        // From the road to the gate and on to the middle of the courtyard the car climbs no steeper than 25 degrees.
        var from = site.Road;
        var to = site.Center;
        float length = FlatDistance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / 2));
        float last = Ground.Height(from.X, from.Z);
        for (int i = 1; i <= steps; i++)
        {
            var p = from.Lerp(to, i / (float)steps);
            float h = Ground.Height(p.X, p.Z);
            if (Mathf.Abs(h - last) > .46f * length / steps)
                return false;
            last = h;
        }
        return true;
    }

    /// <summary>
    /// Whether a point is near what was planned beyond the old valley before the castle and does not keep its own ground
    /// off the plants: the dens and ways of the forest keeper's trails, the shepherd's meadows and ways and the owl's
    /// wonders.
    /// </summary>
    private bool NearCastleSights(Vector3 p)
    {
        foreach (var trail in TrackingTrails)
        {
            if (FlatDistance(p, trail.Den) < 25)
                return true;
            foreach (var step in trail.Path)
                if (FlatDistance(p, step) < 6)
                    return true;
        }
        foreach (var spot in SheepSpots)
            if (FlatDistance(p, spot) < 25)
                return true;
        foreach (var way in SheepWays)
        foreach (var step in way)
            if (FlatDistance(p, step) < 6)
                return true;
        foreach (var landmark in Landmarks)
            if (FlatDistance(p, landmark.Center) < 30)
                return true;
        return false;
    }

    /// <summary>
    /// A beacon beside the homestead of every border: the first of a few places to either side of the end of the road
    /// that is open, dry and nearly level, clear of the homestead and of everything planned before, with the place of
    /// its logs between it and the road's end.
    /// </summary>
    private void PlanBeacons()
    {
        foreach (var home in BorderHomes)
        {
            if (home == null)
                continue;
            foreach (float along in new[] { 0f, 6f, -4f, 12f })
            foreach (float across in new[] { 16f, -16f, 20f, -20f, 24f, -24f })
            {
                var foot = home.Local(across, along);
                var toward = (home.Road - foot) with { Y = 0 };
                if (toward.LengthSquared() < 1)
                    continue;
                var drop = foot + toward.Normalized() * 6;
                if (!BeaconFits(foot, drop))
                    continue;
                BeaconPlaces[(int)home.Border] = new BeaconSite(
                    home.Border,
                    Ground.At(foot.X, foot.Z),
                    Ground.At(drop.X, drop.Z),
                    home.Road,
                    Mathf.Atan2(toward.X, toward.Z)
                );
                goto next;
            }
            next:
            ;
        }
    }

    private bool BeaconFits(Vector3 foot, Vector3 drop)
    {
        var borders = WorldLayout.Plan.Borders;
        float low = float.MaxValue,
            high = float.MinValue;
        for (float dx = -BeaconSite.Radius; dx <= BeaconSite.Radius; dx += 1.75f)
        for (float dz = -BeaconSite.Radius; dz <= BeaconSite.Radius; dz += 1.75f)
        {
            var p = foot + new Vector3(dx, 0, dz);
            if (
                !ValleyBounds.Contains(p.X, p.Z, 6)
                || OutlandRoutes.Distance(p) < 6
                || Routes.Distance(p) < 8
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 6
                || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 6
                || borders.Sand(p.X, p.Z) > .05f
                || Ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + 1.5f
                || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 12) != null
                || InBorderfolkZone(p)
                || NearCastleSights(p)
            )
                return false;
            float h = Ground.Height(p.X, p.Z);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        if (high - low > 2.2f)
            return false;
        // The place of the logs is on dry ground the car can reach, off the water and inside the valley.
        return ValleyBounds.Contains(drop.X, drop.Z, 5)
            && River.Distance(drop.X, drop.Z) > River.HalfWidth(drop.Z) + 3
            && borders.BorderRiverDistance(drop.X, drop.Z) > ValleyBorders.BorderRiverHalfWidth + 3
            && Ground.Height(drop.X, drop.Z) > ValleyBorders.SeaLevel + 1
            && !InDragonZone(drop)
            && !InFerryZone(drop)
            && (LighthousePlace is not { } light || FlatDistance(drop, light.Foot) > LighthouseSite.Radius + 3);
    }

    /// <summary>The castle with its way and the place of its loads, and every beacon with the place of its logs.</summary>
    internal bool InCastleZone(Vector3 p)
    {
        if (CastlePlace is not { } site)
            return false;
        if (
            InSiteRectangle(
                p,
                site.Road,
                site.Inward,
                CastleSite.ZoneAlongMin,
                CastleSite.ZoneAlongMax,
                CastleSite.ZoneAcross
            )
        )
            return true;
        foreach (var beacon in BeaconPlaces)
            if (
                beacon != null
                && (
                    FlatDistance(p, beacon.Foot) < BeaconSite.Radius + 1
                    || SegmentDistance(p, beacon.Foot, beacon.Drop) < 2.5f
                )
            )
                return true;
        return false;
    }

    /// <summary>
    /// Raises the castle (not begun, or finished where the works or the log yard are missing), its beacons, the princess
    /// and the ghost, who waits beside her until every guest of the valley sits at the table; and finds the guests.
    /// </summary>
    private void BuildCastle()
    {
        if (CastlePlace is not { } site)
            return;
        var castle = new Castle(site, Ground) { Quarry = QuarryPlace, Slope = SlopePlace };
        CastleBuildable =
            LogYard != null
            && WorksFor(Product.Planks) != null
            && WorksFor(Product.Bricks) != null
            && WorksFor(Product.Gravel) != null
            && WorksFor(Product.Glass) != null;
        BeaconsBuildable = LogYard != null;
        castle.SetStage(CastleBuildable ? 0 : CastlePlans.Trips);
        AddChild(castle);
        Art.Outline(castle, .025f, .3f);
        Castle = castle;
        var princess = Character.Create(CharacterCatalog.Castellan);
        princess.Role = new CastleRole();
        princess.DressAsPrincess();
        princess.Position = Ground.At(site.Talk.X, site.Talk.Z);
        // She faces the road, turned a little towards the gate.
        princess.Rotation = new Vector3(0, site.Yaw + Mathf.Pi + .5f, 0);
        princess.RaiseFarMarker(castle.TopFocus.Y + 3 - princess.Position.Y - 4.7f);
        AddChild(princess);
        Castellan = princess;
        castle.Princess = princess;
        for (int border = 0; border < CastlePlans.Beacons; border++)
        {
            if (BeaconPlaces[border] is not { } place)
                continue;
            var beacon = new BeaconTower(place, Ground) { Princess = princess };
            AddChild(beacon);
            Art.Outline(beacon, .025f, .3f);
            // Without the log yard the beacons burn from the start; their plan shows once the castle stands.
            beacon.SetBuilt(!BeaconsBuildable, !CastleBuildable);
            Beacons[border] = beacon;
        }
        for (int border = 0; border < 4; border++)
            if (Keepers[border] is { } keeper && BorderHomes[border] is { } home)
                (CastleGuests[border], CastleGuestRoads[border]) = (keeper, home.Road);
        if (Balloonist != null && BalloonPlace is { } balloon)
            (CastleGuests[CastlePlans.BalloonistGuest], CastleGuestRoads[CastlePlans.BalloonistGuest]) = (
                Balloonist,
                balloon.Road
            );
        if (Cartographer != null && CartographerPlace is { } owl)
            (CastleGuests[CastlePlans.CartographerGuest], CastleGuestRoads[CastlePlans.CartographerGuest]) = (
                Cartographer,
                owl.Road
            );
        if (Birdwatcher != null && FerryPlace is { } ferry)
            (CastleGuests[CastlePlans.BirdwatcherGuest], CastleGuestRoads[CastlePlans.BirdwatcherGuest]) = (
                Birdwatcher,
                ferry.Road
            );
        var ghost = new CollectorPet(
            WorldPetCatalog.CastleGhost,
            princess,
            () => Enumerable.Range(0, CastlePlans.Guests).Count(castle.Seated),
            Mathf.Max(1, CastleGuestCount),
            worldPets.Count
        );
        var seat = site.Local(-8, 9);
        ghost.SetHome(Ground.At(seat.X, seat.Z), site.Yaw + Mathf.Pi);
        AddChild(ghost);
        AddWorldPet(ghost);
    }

    /// <summary>The princess is registered after the heron, who came before her.</summary>
    private void RegisterCastellan()
    {
        if (Castellan != null)
            Register(CastellanId, Castellan);
    }
}
