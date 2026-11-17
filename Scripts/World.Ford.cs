using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class World
{
    // 43 is the bandit representative's save ID in Game.
    internal const int BeaverId = 44;

    /// <summary>The beaver's bridge, or null when the seeded valley has no suitable crossing.</summary>
    internal Ford? Ford { get; private set; }
    internal Animal? Beaver { get; private set; }

    /// <summary>Field paths to the sources and the roads to both bridge ends; null without a ford.</summary>
    internal Tracks? Tracks { get; private set; }

    internal const float BridgeRoadHalfWidth = Ford.HalfWidth;
    internal const float FieldPathHalfWidth = 2.2f;

    /// <summary>The beaver's bridge keeps at least this far from the main bridge along the river.</summary>
    internal const float MinBridgeGap = 150;

    /// <summary>The quarry, clay pit and sand pit, indexed by <see cref="HaulLoad"/>.</summary>
    internal HaulSource[] HaulSources { get; private set; } = Array.Empty<HaulSource>();

    private readonly List<(Vector3 Road, Vector3 Spot)> haulApproaches = new();
    private readonly List<Vector3> haulClearings = new();

    /// <summary>Why crossing candidates failed, in search order; printed when a valley has no ford.</summary>
    private readonly int[] fordRejections = new int[7];

    /// <summary>Height of the drivable surface: terrain, raised onto the bridge or the finished ford.</summary>
    internal float DriveSurface(float x, float z) => CrossingSurface(x, z, Ground.Height(x, z));

    /// <summary>Raises <paramref name="below"/> onto the bridge deck or the finished ford.</summary>
    internal float CrossingSurface(float x, float z, float below)
    {
        if (River.OnBridge(x, z))
            return Mathf.Max(below, River.BridgeSurfaceHeight(x));
        return Ford?.Surface(x, z, below) ?? below;
    }

    /// <summary>Whether a point lies on a crossing where the river does not stop the car.</summary>
    internal bool OnCrossing(float x, float z) =>
        River.OnBridge(x, z) || (Ford is { Passable: true } ford && ford.Contains(x, z));

    /// <summary>
    /// Plans the ford after every existing actor and pickup has its place, so earlier valleys keep their
    /// layout. The ford, its sources and the beaver use their own seeded searches.
    /// </summary>
    private void PlanFord(River river)
    {
        var site = FindFordSite();
        var sources = site == null ? null : FindHaulSources(site);
        if (site == null || sources == null)
        {
            haulApproaches.Clear();
            haulClearings.Clear();
            river.AddBankRocks(Ground, null);
            GD.Print(
                $"FORD_MISSING seed={Settings.Seed} site={site != null} "
                    + $"rejected(banks,height,spot,entry,near,far,beaver)={string.Join(",", fordRejections)}"
            );
            return;
        }
        GD.Print(
            $"FORD_READY z={site.Z:0} length={site.HalfLength * 2:0.0} "
                + $"near={FlatDistance(site.NearRoad, site.NearDump):0} far={FlatDistance(site.FarRoad, site.FarDump):0}"
        );
        river.AddBankRocks(Ground, site.Z);
        HaulSources = sources;
        Ford = new Ford(site, Ground);
        AddChild(Ford);
        foreach (var source in sources)
            BuildHaulSource(source);
        BuildTracks(site, sources);
        Beaver = Animal.Create("Bobr Bóďa", "Postavíme most přes řeku? Přivez mi kamení, hlínu a písek.", 2, "9a6a46");
        Beaver.Role = new BeaverRole();
        Beaver.DressAsBeaver();
        Beaver.Position = site.BeaverSpot;
        var facing = site.NearDump - site.BeaverSpot;
        Beaver.Rotation = new(0, Mathf.Atan2(facing.X, facing.Z), 0);
        AddChild(Beaver);
        actors.Add(BeaverId, Beaver);
    }

    private FordSite? FindFordSite()
    {
        var friends = InteractionActors.ToArray();
        FordSite? best = null;
        float bestScore = float.PositiveInfinity;
        for (float z = -Settings.PlayableRadius + 40; z <= Settings.PlayableRadius - 40; z += 4)
        {
            if (Mathf.Abs(z - River.BridgeZ) < MinBridgeGap)
                continue;
            float centerX = River.CenterX(z);
            // The village sits near the origin, so the far bank lies away from it.
            float side = centerX >= 0 ? 1 : -1;
            // The deck stands clear of the water like the main bridge, on piers between the banks.
            float deck = River.Level(z) + .65f;
            float width = Mathf.Max(River.HalfWidth(z), Mathf.Max(River.HalfWidth(z - 4), River.HalfWidth(z + 4)));
            // The deck ends where the bank rises above it, so the slab meets the ground without a step.
            float halfLength = 0;
            bool banksFit = true;
            foreach (float edge in new[] { z - Ford.HalfWidth, z, z + Ford.HalfWidth })
            foreach (float direction in new[] { -1f, 1f })
            {
                float d = 0;
                while (d < width + 9 && Ground.Height(centerX + direction * d, edge) < deck)
                    d += .25f;
                banksFit &= d < width + 9;
                halfLength = Mathf.Max(halfLength, d);
            }
            if (!banksFit)
            {
                fordRejections[0]++;
                continue;
            }
            halfLength += 1.5f;
            var near = Ground.At(centerX - side * (width + 10), z);
            var far = Ground.At(centerX + side * (width + 10), z);
            var nearEnd = new Vector3(centerX - side * halfLength, deck, z);
            var farEnd = new Vector3(centerX + side * halfLength, deck, z);
            if (near.Y - deck > 2.6f || far.Y - deck > 2.6f)
            {
                fordRejections[1]++;
                continue;
            }
            if (!FreeHaulSpot(near, friends, 20) || !FreeHaulSpot(far, friends, 20))
            {
                fordRejections[2]++;
                continue;
            }
            if (
                !ClearHaulPath(near, nearEnd, friends, toWater: true)
                || !ClearHaulPath(far, farEnd, friends, toWater: true)
            )
            {
                fordRejections[3]++;
                continue;
            }
            var nearRoad = BankRoad(near, farBank: false, side, friends, 220);
            if (nearRoad is not Vector3 nearStart)
            {
                fordRejections[4]++;
                continue;
            }
            var farRoad = BankRoad(far, farBank: true, side, friends, 120);
            if (farRoad is not Vector3 farStart)
            {
                fordRejections[5]++;
                continue;
            }
            Vector3? beaver = null;
            foreach (float offset in new[] { 6.5f, -6.5f })
            {
                var spot = Ground.At(near.X, z + offset);
                if (FreeHaulSpot(spot, friends, 16) && SegmentDistance(spot, nearStart, near) > 4.5f)
                {
                    beaver = spot;
                    break;
                }
            }
            if (beaver is not Vector3 beaverSpot)
            {
                fordRejections[6]++;
                continue;
            }
            float score = FlatDistance(nearStart, near) + FlatDistance(farStart, far);
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = new FordSite(z, centerX, side, halfLength, deck, near, far, nearStart, farStart, beaverSpot);
        }
        if (best == null)
            return null;
        haulApproaches.Add((best.NearRoad, best.NearDump));
        haulApproaches.Add((best.FarRoad, best.FarDump));
        haulApproaches.Add((best.NearDump, best.NearEnd));
        haulApproaches.Add((best.FarDump, best.FarEnd));
        haulClearings.Add(best.NearDump);
        haulClearings.Add(best.FarDump);
        haulClearings.Add(best.BeaverSpot);
        return best;
    }

    /// <summary>The closest road point on the given bank with a clear approach to <paramref name="spot"/>.</summary>
    private Vector3? BankRoad(Vector3 spot, bool farBank, float side, Animal[] friends, float maxDistance)
    {
        var candidates = Routes
            .Samples.Where(road =>
                !River.OnBridge(road.X, road.Z)
                && ((road.X - River.CenterX(road.Z)) * side > 0) == farBank
                && FlatDistance(road, spot) <= maxDistance
            )
            .OrderBy(road => FlatDistance(road, spot))
            .ToArray();
        // Neighbouring samples give nearly the same approach, so try road points at least 8 m apart.
        var tried = new List<Vector3>();
        foreach (var road in candidates)
        {
            if (tried.Count >= 16)
                break;
            if (tried.Exists(other => FlatDistance(other, road) < 8))
                continue;
            tried.Add(road);
            if (ClearHaulPath(road, spot, friends, toWater: false))
                return road;
        }
        return null;
    }

    private bool FreeHaulSpot(Vector3 spot, Animal[] friends, float roadClearance) =>
        new Vector2(spot.X, spot.Z).Length() < Settings.PlayableRadius - 15
        && Routes.Distance(spot) >= roadClearance
        && !Village.IsNearVillage(spot)
        && !Village.IsReserved(spot)
        && !IsPickupApproachReserved(spot)
        && friends.All(a => FlatDistance(a.Position, spot) >= 24)
        && SpecialPickupSpots.All(p => FlatDistance(p, spot) >= 22)
        && FlatDistance(Tower.Position, spot) >= 24
        && !River.OnBridge(spot.X, spot.Z)
        && Mathf.Abs(spot.Z - River.BridgeZ) > 14;

    /// <summary>A car-wide path without steep ground, buildings, other approaches or (unless entering it) water.</summary>
    private bool ClearHaulPath(Vector3 from, Vector3 to, Animal[] friends, bool toWater)
    {
        var edge = to - from;
        edge.Y = 0;
        if (edge.LengthSquared() < .01f)
            return true;
        var across = edge.Normalized().Cross(Vector3.Up);
        int steps = Mathf.CeilToInt(edge.Length() / 2);
        for (int step = 0; step <= steps; step++)
        {
            var center = from.Lerp(to, step / (float)steps);
            foreach (float lateral in new[] { -3.5f, 0, 3.5f })
            {
                var q = center + across * lateral;
                bool water = River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 6;
                if (
                    new Vector2(q.X, q.Z).Length() > Settings.PlayableRadius - 10
                    || Village.IsReserved(q)
                    || River.OnBridge(q.X, q.Z)
                    || (water && !toWater)
                    || IsPickupApproachReserved(q)
                    || friends.Any(a => FlatDistance(a.Position, q) < 7)
                    || SpecialPickupSpots.Any(p => FlatDistance(p, q) < 12)
                    || FlatDistance(Tower.Position, q) < 14
                )
                    return false;
                if (water)
                    continue;
                if (
                    Mathf.Abs(Ground.Height(q.X + 1, q.Z) - Ground.Height(q.X - 1, q.Z)) > 1
                    || Mathf.Abs(Ground.Height(q.X, q.Z + 1) - Ground.Height(q.X, q.Z - 1)) > 1
                )
                    return false;
            }
        }
        return true;
    }

    private HaulSource[]? FindHaulSources(FordSite site)
    {
        var random = new Random(Settings.Seed + 2311);
        var friends = InteractionActors.ToArray();
        var sources = new HaulSource[3];
        for (int load = 0; load < sources.Length; load++)
        {
            bool found = false;
            for (int attempt = 0; attempt < 4096 && !found; attempt++)
            {
                int index = random.Next(Routes.Samples.Count - 1);
                var road = Routes.Samples[index];
                var forward = Routes.Samples[index + 1] - road;
                forward.Y = 0;
                if (forward.LengthSquared() < .01f || forward.LengthSquared() > 9 || site.OnFarBank(road))
                    continue;
                var outward = forward.Normalized().Cross(Vector3.Up) * (random.Next(2) == 0 ? -1 : 1);
                var p = Ground.At(
                    road.X + outward.X * (20 + (float)random.NextDouble() * 25),
                    road.Z + outward.Z * (20 + (float)random.NextDouble() * 25)
                );
                if (
                    site.OnFarBank(p)
                    || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 14
                    || !FreeHaulSpot(p, friends, 16)
                    || haulClearings.Any(other => FlatDistance(other, p) < 40)
                    || !ClearHaulPath(road, p, friends, toWater: false)
                )
                    continue;
                sources[load] = new HaulSource((HaulLoad)load, p, road);
                haulApproaches.Add((road, p));
                haulClearings.Add(p);
                found = true;
            }
            if (!found)
                return null;
        }
        return sources;
    }

    /// <summary>Trees and bushes keep clear of the ford, its dumps, the sources and their approaches.</summary>
    private bool IsHaulReserved(Vector3 point)
    {
        if (Ford is { } ford)
        {
            var site = ford.Site;
            if (
                Mathf.Abs(point.Z - site.Z) < Ford.ClearHalfWidth
                && Mathf.Abs(point.X - site.CenterX) < site.HalfLength + 12
            )
                return true;
        }
        foreach (var clearing in haulClearings)
            if (FlatDistance(clearing, point) < 11)
                return true;
        return false;
    }

    private bool IsHaulApproachReserved(Vector3 point)
    {
        foreach (var (road, spot) in haulApproaches)
            if (SegmentDistance(point, road, spot) < 4.5f)
                return true;
        return false;
    }

    private static float SegmentDistance(Vector3 point, Vector3 from, Vector3 to)
    {
        var p = new Vector2(point.X, point.Z);
        var a = new Vector2(from.X, from.Z);
        var edge = new Vector2(to.X, to.Z) - a;
        float t = edge.LengthSquared() < .0001f ? 0 : Mathf.Clamp((p - a).Dot(edge) / edge.LengthSquared(), 0, 1);
        return p.DistanceTo(a + edge * t);
    }

    /// <summary>
    /// Draws the already cleared approaches: a road from the network to each bridge end through its
    /// dump, and a field path ending in a loading yard at each source.
    /// </summary>
    private void BuildTracks(FordSite site, HaulSource[] sources)
    {
        Tracks = new Tracks { Name = "Tracks" };
        foreach (
            var (road, dump, end) in new[]
            {
                (site.NearRoad, site.NearDump, site.NearEnd),
                (site.FarRoad, site.FarDump, site.FarEnd),
            }
        )
        {
            // The road runs on down the bank to the water, so no meadow is left between it and the
            // bridge, during the building or under the finished deck.
            var water = new Vector3(
                site.CenterX + Mathf.Sign(end.X - site.CenterX) * River.HalfWidth(site.Z),
                0,
                site.Z
            );
            Tracks.Line(road, dump, BridgeRoadHalfWidth, Tracks.Kind.Road);
            Tracks.Line(dump, water, BridgeRoadHalfWidth, Tracks.Kind.Road, FlatDistance(road, dump));
        }
        foreach (var source in sources)
        {
            Tracks.Line(source.Road, source.Position, FieldPathHalfWidth, Tracks.Kind.Path);
            Tracks.Pad(source.Position, 4.5f, Tracks.Kind.Path);
        }
        AddChild(Tracks);
        Tracks.Build(Ground);
    }

    private void BuildHaulSource(HaulSource source)
    {
        var site = new Node3D { Name = source.Load + "Source", Position = source.Position };
        AddChild(site);
        var inward = source.Position - source.Road;
        inward.Y = 0;
        inward = inward.Normalized();
        var across = inward.Cross(Vector3.Up);
        Vector3 Floor(Vector3 local) =>
            Ground.At(source.Position.X + local.X, source.Position.Z + local.Z) - source.Position;
        // Piles stand behind and beside the loading spot, leaving the approach open.
        string[] colors = source.Load switch
        {
            HaulLoad.Stones => new[] { "8f8f8a", "a7a6a0", "77756f" },
            HaulLoad.Soil => new[] { "8a6142", "6f4b33", "9c7150" },
            _ => new[] { "e3c98a", "d6b873", "efd9a2" },
        };
        for (int i = 0; i < 5; i++)
        {
            float lateral = (i - 2) * 2.6f;
            float depth = 5.5f + Mathf.Abs(i - 2) * -.8f;
            var local = inward * depth + across * lateral;
            float height =
                source.Load == HaulLoad.Stones ? 2.4f - Mathf.Abs(i - 2) * .4f : 1.4f - Mathf.Abs(i - 2) * .25f;
            var center = Floor(local) + Vector3.Up * height * .35f;
            Art.Ball(site, center, new Vector3(1.6f, height, 1.5f), colors[i % colors.Length]);
            Obstacles.Box(site, center, new Vector3(2.2f, height * .9f, 2.1f));
        }
        if (source.Load == HaulLoad.Stones)
            for (int i = 0; i < 6; i++)
            {
                var local = inward * (3 + (i % 2)) + across * ((i - 2.5f) * 1.1f);
                Art.Ball(site, Floor(local) + Vector3.Up * .15f, new(.32f, .22f, .3f), colors[(i + 1) % 3]);
            }
        // A small wooden chute beside the loading spot marks where the trailer is filled.
        var foot = Floor(inward * 3.2f + across * 3.6f);
        foreach (float x in new[] { -.5f, .5f })
        {
            Art.Cylinder(site, foot + across * x + Vector3.Up * 1.4f, .1f, .1f, 2.8f, "a77a59");
            Obstacles.Trunk(site, foot + across * x, .1f, 2.8f);
        }
        Art.Beam(site, foot + Vector3.Up * 2.8f, foot + Vector3.Up * 2.1f - across * 2.2f, .5f, "c39770");
        string title = source.Load switch
        {
            HaulLoad.Stones => "LOM",
            HaulLoad.Soil => "HLÍNA",
            _ => "PÍSEK",
        };
        Art.Sign(site, Floor(across * -4.5f + inward * 2) + Vector3.Up * 3.2f, title, 36);
    }
}
