using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Vehicles;

namespace MathRacer.Valley;

/// <summary>
/// The shipyard on the coast and the turtle island (<c>docs/SHIP_PLAN.md</c>): Mrož kapitán Vojta waits on the edge of the
/// valley above a beach, the ship Albatros is raised on the slipway below him, and a voyage takes it along the buoys out
/// to the turtle island. Everything is planned after the great railway, from the coast and the places before it alone,
/// without random numbers, so nothing planned before it moves; the treasures of a voyage follow from the valley, the
/// voyage's number and the kinds already found with random numbers of their own (<see cref="ShipSeed"/>), so they are not
/// saved. Nothing changes the ground. All of it is held by <c>Tests/Worlds/seed-N.ship.txt</c>.
/// </summary>
public partial class World
{
    internal const int CaptainId = ActorIds.Captain;

    internal static bool IsCaptain(int id) => id == CaptainId;

    /// <summary>The random numbers of the treasures are their own, so nothing planned before them changes.</summary>
    private const int ShipSeed = 5281;

    /// <summary>The island is driven on this far into its oval; beyond, its shore falls into the water.</summary>
    internal const float TurtleIslandDrivable = .86f;

    /// <summary>Where the shipyard, the anchorage, the island and the buoys are, or null in a valley without room for them.</summary>
    internal ShipSite? ShipPlace { get; private set; }

    /// <summary>Why the shipyard is where it is, or why there is none, for the logs of the checks.</summary>
    internal string ShipPlanNote { get; private set; } = "";

    /// <summary>The walrus captain, Mrož kapitán Vojta.</summary>
    internal Character? Captain { get; private set; }

    internal Ship? Ship { get; private set; }

    internal Shipyard? Shipyard { get; private set; }

    internal TurtleIsland? TurtleIsland { get; private set; }

    /// <summary>The ship's anchor beside the forge of the great line, shown while it waits there to be loaded.</summary>
    internal Node3D? ForgedAnchor { get; private set; }

    /// <summary>Whether the player builds the ship; without the log yard, the sawmill or the glassworks it floats from the start.</summary>
    internal bool ShipBuildable { get; private set; }

    /// <summary>The car is on the turtle island: it may drive there, beyond the edge of the valley, and nowhere else beyond it.</summary>
    internal bool TurtleIslandOpen { get; set; }

    /// <summary>
    /// Chooses the shipyard on a wide beach along the coast from the homestead of the sea, away from the lighthouse, the
    /// cove of the shells, both rivers and everything planned before it, as near the station of the great line by the sea
    /// as it can be; then the island out at sea, its nests and the buoys of the way there.
    /// </summary>
    private void PlanShip()
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        if (BorderHomes[(int)Border.Sea] is not { } home)
        {
            ShipPlanNote = "reason=home";
            GD.Print("SHIP yard=none reason=home");
            return;
        }
        var borders = WorldLayout.Plan.Borders;
        Vector3? station = null;
        int seaArm = MainLinePlans.ArmOfBorder(Border.Sea);
        if (MainLinePlace is { } line && seaArm >= 0 && seaArm < line.Terminals.Length)
            station = line.Terminals[seaArm].Position;
        var goal = station ?? home.Road;
        ShipSite? best = null;
        float bestScore = float.MaxValue;
        string why = "room";
        var misses = new Dictionary<string, int>();
        for (float offset = 160; offset <= 470; offset += 10)
            foreach (int side in new[] { 1, -1 })
            {
                float x = home.Road.X + side * offset;
                if (Mathf.Abs(x) > ValleyBounds.HalfSize - 70)
                    continue;
                float coast = borders.Coast(x);
                if (coast - ValleyBounds.HalfSize < 40)
                    continue;
                float slope = (borders.Coast(x + 5) - borders.Coast(x - 5)) / 10;
                var seaward = new Vector3(-slope, 0, 1).Normalized();
                var waterline = new Vector3(x, ValleyBorders.SeaLevel, coast);
                var edge = waterline;
                while (!ValleyBounds.Contains(edge.X, edge.Z, ShipSite.YardRadius + 3))
                    edge -= seaward * 2;
                edge = Ground.At(edge.X, edge.Z);
                var site = new ShipSite(
                    waterline,
                    seaward,
                    edge,
                    home.Road,
                    Vector3.Zero,
                    System.Array.Empty<Vector3>()
                );
                if (!ShipyardFits(site, out why))
                {
                    misses[why] = misses.GetValueOrDefault(why) + 1;
                    continue;
                }
                float score = FlatDistance(edge, goal);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = site;
                }
            }
        if (best == null)
        {
            ShipPlanNote = $"reason={why} misses={string.Join(",", misses.Select(m => $"{m.Key}:{m.Value}"))}";
            GD.Print($"SHIP yard=none {ShipPlanNote}");
            return;
        }
        // The way there starts from the nearest point of a road.
        var road = OutlandRoutes
            .Samples.Concat(Routes.Samples)
            .Where(p => ValleyBounds.Contains(p.X, p.Z, 4))
            .OrderBy(p => FlatDistance(p, best.Edge))
            .FirstOrDefault(home.Road);
        best = best with { Road = Ground.At(road.X, road.Z) };
        if (PlanTurtleIsland(best) is not { } placed)
        {
            ShipPlanNote = "reason=island";
            GD.Print("SHIP yard=none reason=island");
            return;
        }
        ShipPlace = placed;
        ShipPlanNote =
            $"yard={placed.Edge.X:0},{placed.Edge.Z:0} station={station != null} buoys={placed.Buoys.Length}";
        GD.Print($"SHIP {ShipPlanNote} ms={clock.ElapsedMilliseconds}");
    }

    /// <summary>
    /// The yard fits where the beach under it is low and wide, far from the lighthouse, the cove of the shells, the river's
    /// mouth, the border river and the bird island, and where its ground on the edge, the captain's place and the place the
    /// car is put down are dry, open and nearly level.
    /// </summary>
    private bool ShipyardFits(ShipSite site, out string why)
    {
        var borders = WorldLayout.Plan.Borders;
        var w = site.Waterline;
        why = "far";
        if (LighthousePlace is { } light && FlatDistance(w, light.Foot) < 150)
            return false;
        if (ShellCove is { } cove && FlatDistance(w, cove) < 200)
            return false;
        if (River.Distance(w.X, w.Z) < River.HalfWidth(w.Z) + 90)
            return false;
        if (borders.BorderRiverDistance(w.X, w.Z) < ValleyBorders.BorderRiverHalfWidth + 150)
            return false;
        if (FerryPlace is { } ferry && FlatDistance(w, ferry.Island) < 220)
            return false;
        why = "beach";
        foreach (float seaward in new[] { -20f, -9f, 0f })
        {
            var p = site.Local(0, seaward);
            if (Ground.Height(p.X, p.Z) > ValleyBorders.SeaLevel + 3.5f)
                return false;
        }
        why = "edge";
        float low = float.MaxValue,
            high = float.MinValue;
        var points = new List<Vector3> { site.Talk, site.Return, site.Drop };
        for (float dx = -ShipSite.YardRadius; dx <= ShipSite.YardRadius; dx += 3)
        for (float dz = -ShipSite.YardRadius; dz <= ShipSite.YardRadius; dz += 3)
            if (dx * dx + dz * dz <= ShipSite.YardRadius * ShipSite.YardRadius)
                points.Add(site.OnEdge(dx, dz));
        foreach (var q in points)
        {
            why = "edge-bounds";
            if (!ValleyBounds.Contains(q.X, q.Z, 1.5f))
                return false;
            why = "edge-road";
            if (OutlandRoutes.Distance(q) < 5 || Routes.Distance(q) < 8)
                return false;
            why = "edge-river";
            if (
                River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 8
                || borders.BorderRiverDistance(q.X, q.Z) < ValleyBorders.BorderRiverHalfWidth + 8
                || WorldLayout.Plan.Outland.BridgeAt(q.X, q.Z, 20) != null
            )
                return false;
            why = "edge-taken";
            if (InBorderfolkZone(q) || NearFerrySights(q))
                return false;
            why = "edge-low";
            float h = Ground.Height(q.X, q.Z);
            if (h < ValleyBorders.SeaLevel + .6f)
                return false;
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        why = "level";
        if (high - low > 3.2f)
            return false;
        why = "road";
        return Mathf.Min(OutlandRoutes.Distance(site.Edge), Routes.Distance(site.Edge)) < 480;
    }

    /// <summary>
    /// The island far out at sea before the yard, a little along the coast, off the bird island; and the buoys from the
    /// anchorage along the coast and out to it, each in deep water.
    /// </summary>
    private ShipSite? PlanTurtleIsland(ShipSite site)
    {
        var borders = WorldLayout.Plan.Borders;
        float coast = site.Waterline.Z;
        float limit = WorldTuning.SceneryRadius - 50 - ShipSite.IslandHalfWidth;
        float out_ = Mathf.Clamp(limit - coast, 250, 400);
        // Along the coast away from the homestead of the sea, so that the way passes the shore and not the lighthouse.
        int away =
            BorderHomes[(int)Border.Sea] is { } home && (site.Waterline - home.Road).Dot(site.Along) < 0 ? -1 : 1;
        foreach (float shift in new[] { 160f, 100f, 220f, 40f, 280f })
        {
            var island = site.Local(away * shift, out_);
            island.Y = ValleyBorders.SeaLevel + ShipSite.IslandRise;
            if (Mathf.Abs(island.X) > WorldTuning.SceneryRadius - 140)
                continue;
            if (FerryPlace is { } ferry && FlatDistance(island, ferry.Island) < 400)
                continue;
            // The sea round the island is deep: no shore of the coast within its reach.
            if (Ground.Height(island.X, island.Z) > ValleyBorders.SeaLevel - 6)
                continue;
            var placed = site with { Island = island };
            var corners = new List<Vector3>
            {
                placed.Anchorage,
                placed.Local(away * shift * .45f, 130),
                placed.Local(away * shift * .9f, out_ * .62f),
                placed.IslandBerth,
            };
            // Five stretches between the anchorage and the berth, so four buoys on the way, however long it is.
            float length = 0;
            for (int i = 1; i < corners.Count; i++)
                length += FlatDistance(corners[i - 1], corners[i]);
            var buoys = ShipRoute.Along(corners, length / 5.01f);
            bool deep = true;
            for (int i = 0; i < buoys.Length; i++)
            {
                var b = buoys[i];
                for (
                    int push = 0;
                    push < 6 && Ground.Height(b.X, b.Z) > ValleyBorders.SeaLevel - Tide.Range - 3;
                    push++
                )
                    b += placed.Seaward * 25;
                deep &= Ground.Height(b.X, b.Z) <= ValleyBorders.SeaLevel - Tide.Range - 3;
                buoys[i] = b with { Y = ValleyBorders.SeaLevel };
            }
            if (!deep || buoys.Length < 4)
            {
                GD.Print($"SHIP island shift={shift} deep={deep} buoys={buoys.Length}");
                continue;
            }
            return placed with { Buoys = buoys };
        }
        return null;
    }

    /// <summary>
    /// How deep the sea is under a point for the ship: negative on land, on the islands and their shallows, and beyond the
    /// sea it may sail on, which reaches some way round the way from the anchorage to the island.
    /// </summary>
    internal float ShipDepth(Vector3 p)
    {
        if (ShipPlace is not { } site)
            return -1;
        if (site.IslandFraction(p) < 1.1f)
            return -1;
        if (FerryPlace is { } ferry && ferry.IslandFraction(p.X, p.Z) < 1.5f)
            return -1;
        if (FlatMath.SegmentDistance(p.X, p.Z, site.Anchorage.X, site.Anchorage.Z, site.Island.X, site.Island.Z) > 380)
            return -1;
        return SeaSurface - Ground.Height(p.X, p.Z);
    }

    /// <summary>Whether a point is on the part of the turtle island the car may drive on.</summary>
    internal bool OnTurtleIsland(Vector3 p) => ShipPlace is { } site && site.IslandFraction(p) < TurtleIslandDrivable;

    /// <summary>The yard's ground on the edge, the captain's place and where the car is put down, where no plant grows.</summary>
    internal bool InShipZone(Vector3 p)
    {
        if (ShipPlace is not { } site)
            return false;
        return FlatDistance(p, site.Edge) < ShipSite.YardRadius + 1
            || FlatDistance(p, site.Talk) < 3
            || FlatDistance(p, site.Return) < 4.5f;
    }

    /// <summary>
    /// The treasures of voyage number <paramref name="voyage"/>, by nest (-1 for an empty one): two turtles in two of the
    /// three nests; the first sits on a kind the cabin has not got yet, as long as one is missing, the second on any kind
    /// but the first, drawn by how often each comes.
    /// </summary>
    internal int[] TreasuresOfVoyage(int voyage, int[] kinds)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + ShipSeed) * 7919UL + (ulong)voyage };
        int Draw(List<int> from)
        {
            int sum = from.Sum(kind => ShipPlans.Weights[kind]);
            int roll = (int)rng.RandiRange(0, sum - 1),
                pick = 0;
            while (roll >= ShipPlans.Weights[from[pick]])
                roll -= ShipPlans.Weights[from[pick++]];
            return from[pick];
        }
        var all = Enumerable.Range(0, ShipPlans.Kinds).ToList();
        var missing = all.Where(kind => kind >= kinds.Length || kinds[kind] <= 0).ToList();
        int first = Draw(missing.Count > 0 ? missing : all);
        int second = Draw(all.Where(kind => kind != first).ToList());
        int empty = (int)rng.RandiRange(0, ShipPlans.Nests - 1);
        var nests = new int[ShipPlans.Nests];
        int next = 0;
        for (int nest = 0; nest < ShipPlans.Nests; nest++)
            nests[nest] =
                nest == empty ? -1
                : next++ == 0 ? first
                : second;
        return nests;
    }

    /// <summary>
    /// Raises the shipyard with its slipway, pier and buoys, the ship on the slipway (not begun, or launched where the log
    /// yard, the sawmill or the glassworks is missing), the turtle island and the captain on the edge.
    /// </summary>
    private void BuildShip()
    {
        if (ShipPlace is not { } site)
            return;
        var yard = new Shipyard(site, Ground);
        AddChild(yard);
        Art.Outline(yard, .025f, .3f);
        Shipyard = yard;
        var ship = new Ship
        {
            Site = site,
            Ground = Ground,
            Forge = LineForge,
        };
        AddChild(ship);
        Art.Outline(ship, .025f, .3f);
        Ship = ship;
        ShipBuildable =
            LogYard != null
            && WorksFor(HaulLoads.ProductOf(HaulLoad.Planks)) != null
            && WorksFor(HaulLoads.ProductOf(HaulLoad.Glass)) != null;
        var island = new TurtleIsland(site);
        AddChild(island);
        Art.Outline(island, .025f, .3f);
        TurtleIsland = island;
        var captain = Character.Create(CharacterCatalog.Captain);
        captain.Role = new CaptainRole();
        captain.DressAsCaptain();
        captain.Position = Ground.At(site.Talk.X, site.Talk.Z);
        captain.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
        AddChild(captain);
        Captain = captain;
        ship.Captain = captain;
        // The anchor waits beside the forge once it is forged, until the trailer takes it.
        if (LineForge is { } forge)
        {
            var anchor = TrailerModel.AnchorPile();
            anchor.Name = "ForgedAnchor";
            var beside = forge.Drop + (forge.Drop - forge.Position).Normalized() * 2.2f;
            anchor.Position = Ground.At(beside.X, beside.Z);
            anchor.Visible = false;
            AddChild(anchor);
            ForgedAnchor = anchor;
        }
        ship.SetStage(ShipBuildable ? 0 : ShipPlans.Trips);
        ship.Afloat = !ShipBuildable;
        RestShip();
    }

    /// <summary>The ship stands on the slipway while it is built, and lies at the anchorage once it was launched.</summary>
    internal void RestShip()
    {
        if (Ship is not { } ship || Shipyard is not { } yard || ShipPlace is not { } site)
            return;
        yard.SetLaunched(ship.Afloat, ship.Stage);
        if (ship.Afloat)
            ship.Float(site.Anchorage, site.AlongYaw, SeaSurface);
        else
        {
            var pose = yard.SlipPose;
            ship.Position = pose.Position;
            ship.Rotation = new Vector3(pose.Pitch, pose.Yaw, 0);
        }
    }

    /// <summary>Whether the anchor is forged for the ship: only where the great line has its forge.</summary>
    internal bool ShipHasForge => LineForge != null;

    /// <summary>The captain is registered after the engine driver, who came before him.</summary>
    private void RegisterCaptain()
    {
        if (Captain != null)
            Register(CaptainId, Captain);
    }
}
