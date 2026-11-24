using System.Collections.Generic;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The balloon station (<c>docs/BALLOON_PLAN.md</c>): a launch field with a landing target and a tall rainbow balloon beside
/// a road of the land beyond the old valley, on the highest ground those roads pass that is far from the village, so
/// that the balloon is seen from all over the valley. Its place is chosen with the homesteads, from the roads and the
/// ground alone and without random numbers, so nothing that was there moves: the plants of the land beyond the old
/// valley that stand there are planned as before and only left out when their block is built
/// (<see cref="InBorderfolkZone"/>). The place is held by <c>Tests/Worlds/seed-N.balloon.txt</c>.
/// </summary>
public partial class World
{
    internal const int BalloonistId = ActorIds.Balloonist;

    internal static bool IsBalloonist(int id) => id == BalloonistId;

    /// <summary>Where the balloon station stands, or null in a valley without room for it beside a road.</summary>
    internal BalloonSite? BalloonPlace { get; private set; }

    /// <summary>The field, the hut, the windsock and the ropes of the station.</summary>
    internal BalloonStation? BalloonStation { get; private set; }

    /// <summary>The balloon itself, which stands on the field and flies.</summary>
    internal HotAirBalloon? Balloon { get; private set; }

    /// <summary>The stars that hang along the balloon's way during a flight.</summary>
    internal BalloonStars? BalloonStars { get; private set; }

    /// <summary>The way the wind carries the balloon.</summary>
    internal BalloonFlight? BalloonFlight { get; private set; }

    /// <summary>The balloonist, who stands between the road and the field.</summary>
    internal Character? Balloonist { get; private set; }

    /// <summary>The station keeps this far from the middle of the village.</summary>
    private const float BalloonFromVillage = 480;

    /// <summary>The station keeps this far from the homesteads of the borders, the lighthouse and the dragon.</summary>
    private const float BalloonFromSights = 160;

    /// <summary>How far apart the places along the roads are that the station is tried at.</summary>
    private const float BalloonStep = 8;

    /// <summary>
    /// Chooses the station's place: of every place beside a road of the land beyond the old valley where the station
    /// fits (<see cref="BalloonFits"/>), the one whose field lies highest; if none fits, the same with looser rules for
    /// the slope of the field.
    /// </summary>
    private void PlanBalloon()
    {
        var places = new List<(float Height, int Order, BalloonSite Site)>();
        foreach (var route in OutlandRoutePoints)
        {
            if (route.Length < 2)
                continue;
            using var curve = WorldPlan.CreateCurve(route);
            float length = curve.GetBakedLength();
            for (float along = 10; along + 10 < length; along += BalloonStep)
            {
                var middle = curve.SampleBaked(along);
                var direction = (curve.SampleBaked(along + 2) - curve.SampleBaked(along - 2)) with { Y = 0 };
                if (direction.LengthSquared() < .01f)
                    continue;
                var right = Vector3.Up.Cross(direction.Normalized());
                foreach (int side in new[] { 1, -1 })
                {
                    var inward = right * side;
                    var site = new BalloonSite(Ground.At(middle.X, middle.Z), inward, Mathf.Atan2(inward.X, inward.Z));
                    places.Add((Ground.Height(site.Pad.X, site.Pad.Z), places.Count, site));
                }
            }
        }
        // Highest first; of two at the same height, the one found first.
        places.Sort((a, b) => a.Height != b.Height ? b.Height.CompareTo(a.Height) : a.Order.CompareTo(b.Order));
        foreach (float slope in new[] { 2.4f, 3.6f, 5f })
        {
            foreach (var (_, _, site) in places)
                if (BalloonFits(site, slope))
                {
                    BalloonPlace = site;
                    break;
                }
            if (BalloonPlace != null)
                break;
        }
        GD.Print($"BALLOON station={BalloonPlace != null}");
    }

    /// <summary>
    /// The field, the hut and the windsock are on dry ground inside the valley and beyond the old one, far from the
    /// village, off every road and both rivers, clear of the homesteads, the lighthouse, the coast quarry and the dragon;
    /// the field is no steeper than <paramref name="slope"/> metres from its lowest to its highest point, and the car can
    /// drive from the road onto it.
    /// </summary>
    private bool BalloonFits(BalloonSite site, float slope)
    {
        var pad = site.Pad;
        if (FlatDistance(pad, Village.Center) < BalloonFromVillage || !ValleyBounds.Contains(pad.X, pad.Z, 30))
            return false;
        var borders = WorldLayout.Plan.Borders;
        for (float along = BalloonSite.ZoneAlongMin; along <= BalloonSite.ZoneAlongMax; along += 3.5f)
        for (float across = -BalloonSite.ZoneAcross; across <= BalloonSite.ZoneAcross; across += 3.5f)
        {
            var p = site.Local(across, along);
            if (
                Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) < WorldTuning.CoreRadius + 8
                || !ValleyBounds.Contains(p.X, p.Z, 6)
                || along > 7 && OutlandRoutes.Distance(p) < 7
                || Routes.Distance(p) < 20
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 12
                || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 12
                || borders.Sand(p.X, p.Z) > .05f
                || Ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + 3
                || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 25) != null
                || InBorderfolkZone(p)
            )
                return false;
        }
        // Well away from the places of the borders, so that the valley's sights do not crowd together.
        foreach (var home in BorderHomes)
            if (home != null && FlatDistance(pad, home.Road) < BalloonFromSights)
                return false;
        if (
            LighthousePlace is { } light && FlatDistance(pad, light.Foot) < BalloonFromSights
            || QuarryPlace is { } quarry && FlatDistance(pad, quarry.Position) < 60
            || DragonPlace is { } dragon && FlatDistance(pad, dragon.Lair) < BalloonFromSights
            || SlopePlace is { } mountain && FlatDistance(pad, mountain.Position) < 60
        )
            return false;
        // The field is nearly level.
        float low = float.MaxValue,
            high = float.MinValue;
        for (float along = BalloonSite.PadAlong - 10; along <= BalloonSite.PadAlong + 10; along += 2.5f)
        for (float across = -10; across <= 10; across += 2.5f)
        {
            var p = site.Local(across, along);
            float h = Ground.Height(p.X, p.Z);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        if (high - low > slope)
            return false;
        // From the road onto the field the car climbs no steeper than 25 degrees.
        var from = site.Road;
        var to = site.Local(0, BalloonSite.PadAlong - BalloonSite.FieldRadius);
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
    /// Raises the station, the balloon on its field, the stars of its flights (hidden), the balloonist and her rooster,
    /// who waits beside her until twenty stars are caught.
    /// </summary>
    private void BuildBalloon()
    {
        if (BalloonPlace is not { } site)
            return;
        var station = new BalloonStation(site, Ground);
        AddChild(station);
        Art.Outline(station, .025f, .3f);
        BalloonStation = station;
        var balloon = new HotAirBalloon();
        AddChild(balloon);
        Art.Outline(balloon, .03f, .3f);
        Balloon = balloon;
        station.Balloon = balloon;
        station.Moor();
        var stars = new BalloonStars();
        AddChild(stars);
        BalloonStars = stars;
        BalloonFlight = new BalloonFlight(site, Ground, Village.Center);
        var balloonist = Character.Create(CharacterCatalog.Balloonist);
        balloonist.Role = new BalloonistRole();
        balloonist.DressAsBalloonist();
        balloonist.Position = Ground.At(site.Talk.X, site.Talk.Z);
        // She faces the road, turned a little towards the field.
        balloonist.Rotation = new Vector3(0, site.Yaw + Mathf.Pi - .5f, 0);
        float top = station.BalloonAnchor.Y + HotAirBalloon.MouthHeight + HotAirBalloon.EnvelopeHeight;
        balloonist.RaiseFarMarker(top + 2 - balloonist.Position.Y - 4.7f);
        AddChild(balloonist);
        Balloonist = balloonist;
        station.Balloonist = balloonist;
        var rooster = new CollectorPet(
            WorldPetCatalog.Rooster,
            balloonist,
            () => station.Stars,
            BalloonPlans.RoosterStars,
            worldPets.Count
        );
        rooster.SetHome(Ground.At(site.Local(-6.2f, 9.6f).X, site.Local(-6.2f, 9.6f).Z), site.Yaw + Mathf.Pi);
        AddChild(rooster);
        AddWorldPet(rooster);
    }

    /// <summary>The balloonist is registered after the dragon, who came before her.</summary>
    private void RegisterBalloonist()
    {
        if (Balloonist != null)
            Register(BalloonistId, Balloonist);
    }

    /// <summary>The field, the hut, the windsock and the way from the road.</summary>
    internal bool InBalloonZone(Vector3 p) =>
        BalloonPlace is { } site
        && InSiteRectangle(
            p,
            site.Road,
            site.Inward,
            BalloonSite.ZoneAlongMin,
            BalloonSite.ZoneAlongMax,
            BalloonSite.ZoneAcross
        );
}
