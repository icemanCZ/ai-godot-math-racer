using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The owl's map of the valley (<c>docs/EXPLORER_PLAN.md</c>): the stand of Sova kartografka Mapka beside the road where
/// it first leaves the old valley, and the eight wonders of the land beyond it, one of each kind in every eighth of it,
/// each beside a road so that the car passes it. They are planned once the ferrywoman's rally is, with random numbers of
/// their own (<see cref="ExplorerSeed"/>), and keep clear of everything planned before them; the plants of the land
/// beyond the old valley that stand on them are planned as before and only left out when their block is built
/// (<see cref="InBorderfolkZone"/>), so nothing that was there moves. They are held by
/// <c>Tests/Worlds/seed-N.explorer.txt</c>.
/// </summary>
public partial class World
{
    internal const int CartographerId = ActorIds.Cartographer;

    internal static bool IsCartographer(int id) => id == CartographerId;

    /// <summary>The random numbers of the wonders are their own, so nothing planned before them changes.</summary>
    private const int ExplorerSeed = 9203;

    /// <summary>The wonders keep this far apart, so that each has a corner of the land of its own.</summary>
    private const float LandmarkSpacing = 220;

    /// <summary>And this far from the owl's stand, so that none of them is found right beside her.</summary>
    private const float LandmarkFromOwl = 140;

    /// <summary>How far apart the places along the roads are that a wonder is tried at.</summary>
    private const float LandmarkStep = 9;

    /// <summary>Where the owl has her stand, or null in a valley without room for it.</summary>
    internal CartographerSite? CartographerPlace { get; private set; }

    /// <summary>The wonders by their kind; empty in a valley where they do not all fit.</summary>
    internal IReadOnlyList<Landmark> Landmarks { get; private set; } = System.Array.Empty<Landmark>();

    /// <summary>The owl, Sova kartografka Mapka.</summary>
    internal Character? Cartographer { get; private set; }

    /// <summary>The models of the wonders and the owl's flags on those written into her map.</summary>
    internal LandmarkModels? LandmarkModels { get; private set; }

    /// <summary>
    /// Chooses the owl's stand and then the wonders: for every eighth of the land beyond the old valley, in an order of
    /// the kinds of their own, the places beside its roads where a wonder fits, one taken at random; where none fits in
    /// its eighth, anywhere round the valley. A valley where the stand or a wonder does not fit has no map.
    /// </summary>
    private void PlanExplorer()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        PlanCartographer();
        if (CartographerPlace is not { } owl)
        {
            GD.Print("EXPLORER landmarks=0 reason=stand");
            return;
        }
        var rng = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + ExplorerSeed) };
        var places = new List<(Vector3 Road, Vector3 Inward)>();
        foreach (var route in OutlandRoutePoints)
        {
            if (route.Length < 2)
                continue;
            using var curve = WorldPlan.CreateCurve(route);
            float length = curve.GetBakedLength();
            for (float along = 12; along + 12 < length; along += LandmarkStep)
            {
                var middle = curve.SampleBaked(along);
                var direction = (curve.SampleBaked(along + 2) - curve.SampleBaked(along - 2)) with { Y = 0 };
                if (direction.LengthSquared() < .01f)
                    continue;
                var right = Vector3.Up.Cross(direction.Normalized());
                places.Add((Ground.At(middle.X, middle.Z), right));
                places.Add((Ground.At(middle.X, middle.Z), -right));
            }
        }
        // The kinds go round the eighths in an order of the valley's own.
        var order = Enumerable.Range(0, ExplorerPlans.Landmarks).ToArray();
        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = (int)rng.RandiRange(0, i);
            (order[i], order[j]) = (order[j], order[i]);
        }
        var chosen = new Landmark?[ExplorerPlans.Landmarks];
        int tries = 0;
        for (int pass = 0; pass < 2; pass++)
        for (int eighth = 0; eighth < ExplorerPlans.Landmarks; eighth++)
        {
            int kind = order[eighth];
            if (chosen[kind] != null)
                continue;
            // A shuffled list of the places, so that the one taken is a random one of those that fit.
            var candidates = places
                .Where(place => pass == 1 || EighthOf(LandmarkCenter(kind, place.Road, place.Inward)) == eighth)
                .ToList();
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = (int)rng.RandiRange(0, i);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
            foreach (var (road, inward) in candidates)
            {
                tries++;
                var center = LandmarkCenter(kind, road, inward);
                if (!LandmarkFits(kind, center, owl, chosen))
                    continue;
                var facing = -inward;
                chosen[kind] = new Landmark(kind, Ground.At(center.X, center.Z), Mathf.Atan2(facing.X, facing.Z), road);
                break;
            }
        }
        if (chosen.Any(landmark => landmark == null))
        {
            GD.Print(
                $"EXPLORER landmarks=0 placed={chosen.Count(l => l != null)} tries={tries} ms={clock.ElapsedMilliseconds}"
            );
            return;
        }
        Landmarks = chosen.Select(landmark => landmark!).ToArray();
        GD.Print($"EXPLORER landmarks={Landmarks.Count} tries={tries} ms={clock.ElapsedMilliseconds}");
    }

    /// <summary>The eighth of the land round the middle of the valley that a point lies in, 0 to 7.</summary>
    internal static int EighthOf(Vector3 p) =>
        Mathf.PosMod(Mathf.FloorToInt(Mathf.Atan2(p.Z, p.X) / (Mathf.Tau / ExplorerPlans.Landmarks)), 8);

    /// <summary>The middle of a wonder beside the road: off the road and its verge by the wonder's width.</summary>
    private static Vector3 LandmarkCenter(int kind, Vector3 road, Vector3 inward) =>
        road + inward * (ExplorerPlans.Radius(kind) + 7);

    /// <summary>
    /// A wonder stands beyond the old valley and inside the valley, on open, nearly level, dry ground off every road, the
    /// rivers, the beach and the bridges, away from the places of the borders, the dens and the ways of the forest
    /// keeper's trails, the shepherd's meadows, the gates of the rally, the owl's stand and the other wonders.
    /// </summary>
    private bool LandmarkFits(int kind, Vector3 center, CartographerSite owl, Landmark?[] chosen)
    {
        float radius = ExplorerPlans.Radius(kind);
        if (
            Mathf.Max(Mathf.Abs(center.X), Mathf.Abs(center.Z)) < WorldTuning.CoreRadius + radius + 25
            || !ValleyBounds.Contains(center.X, center.Z, radius + 8)
            || FlatDistance(center, owl.Road) < LandmarkFromOwl
        )
            return false;
        foreach (var other in chosen)
            if (other != null && FlatDistance(center, other.Center) < LandmarkSpacing)
                return false;
        if (NearExplorerSights(center, radius))
            return false;
        float low = float.MaxValue,
            high = float.MinValue;
        float reach = radius + 1.5f;
        for (float dx = -reach; dx <= reach; dx += 2.5f)
        for (float dz = -reach; dz <= reach; dz += 2.5f)
        {
            if (dx * dx + dz * dz > reach * reach)
                continue;
            var q = new Vector3(center.X + dx, 0, center.Z + dz);
            if (!ExplorerGroundOpen(q, 4.6f))
                return false;
            float h = Ground.Height(q.X, q.Z);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        return high - low < 2.4f;
    }

    /// <summary>
    /// Ground the owl's stand or a wonder may take: beyond the old valley, inside the valley, at least
    /// <paramref name="fromRoad"/> from the middle of a road beyond it, off the old roads, the rivers, the beach, the
    /// bridges and the ground of the borders' places.
    /// </summary>
    private bool ExplorerGroundOpen(Vector3 q, float fromRoad)
    {
        var borders = WorldLayout.Plan.Borders;
        return Mathf.Max(Mathf.Abs(q.X), Mathf.Abs(q.Z)) > WorldTuning.CoreRadius + 3
            && ValleyBounds.Contains(q.X, q.Z, 6)
            && OutlandRoutes.Distance(q) > fromRoad
            && Routes.Distance(q) > 8
            && River.Distance(q.X, q.Z) > River.HalfWidth(q.Z) + 8
            && borders.BorderRiverDistance(q.X, q.Z) > ValleyBorders.BorderRiverHalfWidth + 8
            && borders.Sand(q.X, q.Z) < .05f
            && Ground.Height(q.X, q.Z) > ValleyBorders.SeaLevel + 3
            && WorldLayout.Plan.Outland.BridgeAt(q.X, q.Z, 20) == null
            && !InBorderfolkZone(q);
    }

    /// <summary>
    /// Whether a point within <paramref name="radius"/> lies near what was planned in the land beyond the old valley
    /// before the map: the homesteads, the lighthouse, the coast quarry, the dragon, the mountain slope, the balloon
    /// station, the dens and ways of the forest keeper's trails, the shepherd's meadows and ways, and the rally's gates.
    /// </summary>
    private bool NearExplorerSights(Vector3 p, float radius)
    {
        foreach (var home in BorderHomes)
            if (home != null && FlatDistance(p, home.Road) < radius + 60)
                return true;
        if (
            LighthousePlace is { } light && FlatDistance(p, light.Foot) < radius + 60
            || QuarryPlace is { } quarry && FlatDistance(p, quarry.Position) < radius + 40
            || DragonPlace is { } dragon && FlatDistance(p, dragon.Lair) < radius + 60
            || SlopePlace is { } slope && FlatDistance(p, slope.Position) < radius + 40
            || BalloonPlace is { } balloon && FlatDistance(p, balloon.Pad) < radius + 60
        )
            return true;
        foreach (var trail in TrackingTrails)
        {
            if (FlatDistance(p, trail.Den) < radius + 20)
                return true;
            foreach (var step in trail.Path)
                if (FlatDistance(p, step) < radius + 4)
                    return true;
        }
        foreach (var spot in SheepSpots)
            if (FlatDistance(p, spot) < radius + 20)
                return true;
        foreach (var way in SheepWays)
        foreach (var step in way)
            if (FlatDistance(p, step) < radius + 4)
                return true;
        if (Rally is { } rally)
            foreach (var stage in rally.Stages)
            {
                if (FlatDistance(p, rally.At(stage.Start)) < radius + 30)
                    return true;
                foreach (float gate in stage.Gates)
                    if (FlatDistance(p, rally.At(gate)) < radius + 30)
                        return true;
            }
        return false;
    }

    /// <summary>
    /// Chooses the owl's stand: along the roads that join the old roads to the ring, the one leaving the old valley nearest
    /// the village first, the first place beyond the old valley where the stand fits beside it; without one, the place
    /// beside the ring nearest the village where it fits.
    /// </summary>
    private void PlanCartographer()
    {
        var joins = Enumerable
            .Range(1, OutlandJoinCount)
            .Select(i => OutlandRoutePoints[i])
            .Where(route => route.Length >= 2)
            .OrderBy(route => FlatDistance(route[0], Village.Center))
            .ToList();
        foreach (var join in joins)
        {
            using var curve = WorldPlan.CreateCurve(join);
            float length = curve.GetBakedLength();
            for (float along = 4; along + 4 < length; along += 3)
                if (TryCartographer(curve, along))
                    return;
        }
        if (OutlandRoutePoints.Length == 0)
            return;
        using var ring = WorldPlan.CreateCurve(OutlandRoutePoints[0]);
        float ringLength = ring.GetBakedLength();
        var alongs = new List<float>();
        for (float along = 4; along + 4 < ringLength; along += 6)
            alongs.Add(along);
        foreach (float along in alongs.OrderBy(a => FlatDistance(ring.SampleBaked(a), Village.Center)))
            if (TryCartographer(ring, along))
                return;
    }

    private bool TryCartographer(Curve3D curve, float along)
    {
        var middle = curve.SampleBaked(along);
        if (Mathf.Max(Mathf.Abs(middle.X), Mathf.Abs(middle.Z)) < WorldTuning.CoreRadius + 20)
            return false;
        var direction = (curve.SampleBaked(along + 2) - curve.SampleBaked(along - 2)) with { Y = 0 };
        if (direction.LengthSquared() < .01f)
            return false;
        var right = Vector3.Up.Cross(direction.Normalized());
        foreach (int side in new[] { 1, -1 })
        {
            var inward = right * side;
            var site = new CartographerSite(Ground.At(middle.X, middle.Z), inward, Mathf.Atan2(inward.X, inward.Z));
            if (!CartographerFits(site))
                continue;
            CartographerPlace = site;
            return true;
        }
        return false;
    }

    /// <summary>
    /// The stand is on open, nearly level, dry ground beyond the old valley, off the roads and the waters, away from the
    /// places of the borders and what else was planned beyond the old valley.
    /// </summary>
    private bool CartographerFits(CartographerSite site)
    {
        float low = float.MaxValue,
            high = float.MinValue;
        for (float along = CartographerSite.ZoneAlongMin; along <= CartographerSite.ZoneAlongMax; along += 2)
        for (float across = -CartographerSite.ZoneAcross; across <= CartographerSite.ZoneAcross; across += 2)
        {
            var p = site.Local(across, along);
            if (!ExplorerGroundOpen(p, 4.6f))
                return false;
            float h = Ground.Height(p.X, p.Z);
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        return high - low < 1.8f && !NearExplorerSights(site.Local(0, 10), 10);
    }

    /// <summary>The owl's stand and the ground of the wonders, where no plant and no grass grows.</summary>
    internal bool InExplorerZone(Vector3 p)
    {
        if (
            CartographerPlace is { } site
            && InSiteRectangle(
                p,
                site.Road,
                site.Inward,
                CartographerSite.ZoneAlongMin,
                CartographerSite.ZoneAlongMax,
                CartographerSite.ZoneAcross
            )
        )
            return true;
        foreach (var landmark in Landmarks)
        {
            float reach = ExplorerPlans.Radius(landmark.Kind) + 1.5f;
            if (FlatMath.Length(p.X - landmark.Center.X, p.Z - landmark.Center.Z) < reach)
                return true;
        }
        return false;
    }

    /// <summary>Raises the owl's stand, the owl, the wonders and the little owl, who waits beside her for the full map.</summary>
    private void BuildExplorer()
    {
        if (CartographerPlace is not { } site || Landmarks.Count != ExplorerPlans.Landmarks)
            return;
        var stand = new CartographerStand(site, Ground) { Name = "CartographerStand" };
        AddChild(stand);
        Art.Outline(stand, .025f, .3f);
        var models = new LandmarkModels(Landmarks, Ground) { Name = "Landmarks" };
        AddChild(models);
        Art.Outline(models, .03f, .3f);
        LandmarkModels = models;
        var owl = Character.Create(CharacterCatalog.Cartographer);
        owl.Role = new CartographerRole();
        owl.DressAsOwl();
        owl.Position = Ground.At(site.Talk.X, site.Talk.Z);
        // She faces the road, turned a little towards her map.
        owl.Rotation = new Vector3(0, site.Yaw + Mathf.Pi + .35f, 0);
        AddChild(owl);
        Cartographer = owl;
        var owlet = new CollectorPet(
            WorldPetCatalog.Owlet,
            owl,
            () => models.RecordedCount,
            ExplorerPlans.Landmarks,
            worldPets.Count
        );
        var seat = site.Local(-5, 9.5f);
        owlet.SetHome(Ground.At(seat.X, seat.Z), site.Yaw + Mathf.Pi);
        AddChild(owlet);
        AddWorldPet(owlet);
    }

    /// <summary>
    /// The waters on the owl's map, as x and z from the spring to the sea: the valley's river, the border river and the
    /// coast, a little beyond the edges of the valley.
    /// </summary>
    internal (Vector2[] River, Vector2[] BorderRiver, Vector2[] Coast) MapWaters()
    {
        var borders = WorldLayout.Plan.Borders;
        float reach = ValleyBounds.HalfSize + 150;
        var river = new List<Vector2>();
        for (float z = ValleyBorders.SpringZ + 60; z <= borders.Coast(River.CenterX(z)); z += 20)
            river.Add(new Vector2(River.CenterX(z), z));
        var borderRiver = new List<Vector2>();
        for (float z = -reach; z <= reach; z += 30)
            borderRiver.Add(new Vector2(borders.BorderRiverX(z), z));
        var coast = new List<Vector2>();
        for (float x = -reach; x <= reach; x += 30)
            coast.Add(new Vector2(x, borders.Coast(x)));
        return (river.ToArray(), borderRiver.ToArray(), coast.ToArray());
    }

    /// <summary>The owl is registered after the animal at the end of the forest keeper's trail, who came before her.</summary>
    private void RegisterCartographer()
    {
        if (Cartographer != null)
            Register(CartographerId, Cartographer);
    }
}
