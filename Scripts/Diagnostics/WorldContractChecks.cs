using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Diagnostics;

/// <summary>
/// A text description of everything a seed places in the world: characters, roads, terrain, buildings, pickups and
/// trees. A refactoring that moves code around must leave it byte for byte the same, or the valleys of saved games
/// would change under the players.
/// </summary>
internal static class WorldFingerprint
{
    private static string F(float value) => value.ToString("F2", CultureInfo.InvariantCulture);

    private static string P(Vector3 p) => $"{F(p.X)} {F(p.Y)} {F(p.Z)}";

    private static string Hash(IEnumerable<string> parts)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", parts)));
        return Convert.ToHexString(bytes)[..16];
    }

    internal static string Folder => Path.Combine(ProjectSettings.GlobalizePath("res://Tests"), "Worlds");

    internal static string FileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.txt");

    /// <summary>
    /// What was added to the world after those files were made: the glassworks, which is placed after the plants so that
    /// nothing in the first files moves. It has a file of its own, kept the same way.
    /// </summary>
    internal static string GlassworksFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.glassworks.txt");

    internal static List<string> GlassworksLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        if (world.Glassworks is { } works)
        {
            lines.Add($"glassworks {P(works.Site.Position)} road {P(works.Site.Road)} inward {P(works.Site.Inward)}");
            lines.Add($"actor {world.ActorId(works.Worker)} {works.Worker.DisplayName} {P(works.Worker.Position)}");
        }
        else
            lines.Add("glassworks none");
        return lines;
    }

    /// <summary>
    /// The land between the old valley and the borders (<c>docs/MAP_EXPANSION_PLAN.md</c>, phase 4): the ring and its
    /// bridges, the roads beyond the old valley, the ground there, its plants and its animals. It is generated after
    /// everything in the first two files and has a file of its own, kept the same way.
    /// </summary>
    internal static string OutlandFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.outland.txt");

    internal static List<string> OutlandLines(World world)
    {
        var layout = WorldLayout.Plan.Outland;
        var lines = new List<string> { $"seed {Settings.Seed}", $"ring radius {F(layout.RingRadius)}" };
        foreach (var bridge in layout.Bridges)
            lines.Add(
                $"bridge {F(bridge.CenterX)} {F(bridge.Z)} half length {F(bridge.HalfLength)} top {F(bridge.Top)}"
            );
        var points = world.OutlandRoutePoints;
        lines.Add($"outland roads {points.Length} joins {world.OutlandJoinCount}");
        foreach (var route in points)
            lines.Add($"outland route {route.Length} from {P(route[0])} to {P(route[^1])}");
        var samples = world.OutlandRoutes.Samples;
        lines.Add($"outland road samples {samples.Count} hash {Hash(samples.Select(P))}");
        // Every 25 m beyond the grid of the first file, out to the edge of the valley.
        var heights = new List<string>();
        for (int x = -1125; x <= 1125; x += 25)
        for (int z = -1125; z <= 1125; z += 25)
            if (Math.Max(Math.Abs(x), Math.Abs(z)) > 500)
                heights.Add(F(world.Ground.Height(x, z)));
        lines.Add($"outland heights {heights.Count} hash {Hash(heights)}");
        var plants = world.Plants;
        lines.Add(
            $"outland vegetation trees {plants.OutlandTreeCount} bushes {plants.OutlandBushCount} hash {Hash(plants.OutlandTreePositions.Select(P))}"
        );
        var animals = world.Wildlife.Animals.Skip(world.Wildlife.Animals.Count - world.Wildlife.OutlandCount);
        lines.Add(
            $"outland animals {world.Wildlife.OutlandCount} hash {Hash(animals.Select(a => $"{a.Kind} {P(a.Home)}"))}"
        );
        return lines;
    }

    /// <summary>
    /// What came to the land beyond the old valley after its own file was made (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>):
    /// the homesteads of the borders and their keepers, the lighthouse's place and the coast quarry.
    /// </summary>
    internal static string CoastFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.coast.txt");

    internal static List<string> CoastLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        foreach (var (site, keeper) in world.BorderfolkPlaces())
            lines.Add(
                $"home {site.Border} road {P(site.Road)} inward {P(site.Inward)} keeper {(keeper == null ? "none" : P(keeper.Position))}"
            );
        lines.Add(
            world.LighthousePlace is { } light
                ? $"lighthouse foot {P(light.Foot)} drop {P(light.Drop)}"
                : "lighthouse none"
        );
        lines.Add(
            world.QuarryPlace is { } quarry
                ? $"coast quarry {P(quarry.Position)} road {P(quarry.Road)}"
                : "coast quarry none"
        );
        return lines;
    }

    /// <summary>
    /// What came after the coast (<c>docs/DRAGON_PLAN.md</c>): the dragon's ledge beside the homestead under the mountains,
    /// the mountain slope where his nest's loads are taken, and the plants that were left out for them.
    /// </summary>
    internal static string DragonFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.dragon.txt");

    internal static List<string> DragonLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        lines.Add(
            world.DragonPlace is { } site
                ? $"dragon lair {P(site.Lair)} facing {P(site.Facing)} drop {P(site.Drop)} fire side {site.FireSide}"
                : "dragon none"
        );
        lines.Add(
            world.SlopePlace is { } slope
                ? $"mountain slope {P(slope.Position)} road {P(slope.Road)}"
                : "mountain slope none"
        );
        lines.Add($"trees left out {world.Plants.OutlandTreePositions.Count(world.InDragonZone)}");
        return lines;
    }

    /// <summary>
    /// What came after the dragon (<c>docs/BALLOON_PLAN.md</c>): the balloon station beside a road of the land beyond the
    /// old valley, the way of its flights and the plants that were left out for it.
    /// </summary>
    internal static string BalloonFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.balloon.txt");

    internal static List<string> BalloonLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        lines.Add(
            world.BalloonPlace is { } site
                ? $"balloon station road {P(site.Road)} inward {P(site.Inward)} pad {P(site.Pad)}"
                : "balloon station none"
        );
        lines.Add(
            world.BalloonFlight is { } flight
                ? $"balloon flight length {F(flight.Length)} star {P(flight.StarAt(0, 0))}"
                : "balloon flight none"
        );
        lines.Add($"trees left out {world.Plants.OutlandTreePositions.Count(world.InBalloonZone)}");
        return lines;
    }

    /// <summary>
    /// What came after the balloon station (<c>docs/TRACKING_PLAN.md</c>): the dens of the forest keeper's trails, the
    /// ways between them and their prints.
    /// </summary>
    internal static string TrackingFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.tracking.txt");

    internal static List<string> TrackingLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}", $"tracking trails {world.TrackingTrails.Count}" };
        foreach (var trail in world.TrackingTrails)
            lines.Add(
                $"trail {trail.Animal} den {P(trail.Den)} length {F(trail.Length)} prints {trail.Prints.Length}"
                    + $" hash {Hash(trail.Path.Select(P))}"
            );
        lines.Add($"trees left out {world.Plants.OutlandTreePositions.Count(world.InTrackingZone)}");
        return lines;
    }

    /// <summary>
    /// What came after the forest keeper's trails (<c>docs/SHEEP_PLAN.md</c>): the meadows of the shepherd's lost sheep
    /// and the ways the car drives there from a road.
    /// </summary>
    internal static string SheepFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.sheep.txt");

    internal static List<string> SheepLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}", $"sheep meadows {world.SheepSpots.Count}" };
        for (int i = 0; i < world.SheepSpots.Count; i++)
            lines.Add(
                $"sheep {i} meadow {P(world.SheepSpots[i])} way {world.SheepWays[i].Length}"
                    + $" hash {Hash(world.SheepWays[i].Select(P))}"
            );
        return lines;
    }

    /// <summary>
    /// What came after the shepherd's sheep (<c>docs/RALLY_PLAN.md</c>): the ring of the ferrywoman's rally, its four
    /// stages with their arches and the gates on each.
    /// </summary>
    internal static string RallyFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.rally.txt");

    internal static List<string> RallyLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        if (world.Rally is not { } course)
        {
            lines.Add("rally none");
            return lines;
        }
        lines.Add($"rally ring {F(course.Length)} points {course.Points.Count} hash {Hash(course.Points.Select(P))}");
        foreach (var stage in course.Stages)
            lines.Add(
                $"stage {stage.From} {stage.To} start {P(course.At(stage.Start))} length {F(stage.Length)}"
                    + $" gates {string.Join(" ", stage.Gates.Select(g => P(course.At(g))))}"
            );
        return lines;
    }

    /// <summary>
    /// What came after the ferrywoman's rally (<c>docs/EXPLORER_PLAN.md</c>): the owl's stand and the eight wonders of
    /// her map.
    /// </summary>
    internal static string ExplorerFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.explorer.txt");

    internal static List<string> ExplorerLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        lines.Add(
            world.CartographerPlace is { } owl
                ? $"owl road {P(owl.Road)} inward {P(owl.Inward)} talk {P(owl.Talk)}"
                : "owl none"
        );
        foreach (var landmark in world.Landmarks)
            lines.Add(
                $"landmark {landmark.Kind} {ExplorerPlans.Name(landmark.Kind)} {P(landmark.Center)}"
                    + $" road {P(landmark.Road)} yaw {F(landmark.Yaw)}"
            );
        return lines;
    }

    /// <summary>
    /// What came after the owl's map (<c>docs/SHELLS_PLAN.md</c>): the cove of the shells, its sign and the shells of its
    /// first two tides.
    /// </summary>
    internal static string ShellsFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.shells.txt");

    internal static List<string> ShellsLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        lines.Add(world.ShellCove is { } cove ? $"cove {P(cove)}" : "cove none");
        lines.Add(world.ShellSign is { } sign ? $"sign {P(sign)}" : "sign none");
        for (int tide = 0; tide < 2; tide++)
            foreach (var shell in world.ShellsOfTide(tide))
                lines.Add($"tide {tide} shell {shell.Number} kind {shell.Kind} {P(shell.Position)} yaw {F(shell.Yaw)}");
        return lines;
    }

    /// <summary>
    /// What came after the shells (<c>docs/FERRY_PLAN.md</c>): the heron's landing, its pier, the island, the way of the
    /// punt, the reeds round the island and the birds of the first two crossings.
    /// </summary>
    internal static string FerryFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.ferry.txt");

    internal static List<string> FerryLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        if (world.FerryPlace is not { } site || world.FerryRoute is not { } route)
        {
            lines.Add("ferry none");
            return lines;
        }
        lines.Add($"landing {P(site.Landing)} inward {P(site.Inward)} talk {P(site.Talk)} drop {P(site.Drop)}");
        lines.Add($"pier {P(site.PierStart)} berth {P(site.Berth)}");
        lines.Add($"island {P(site.Island)} berth {P(site.IslandBerth)} hide {P(site.Hide)}");
        lines.Add($"way {F(route.Length)} points {route.Samples.Count} seconds {F(route.Duration)}");
        foreach (var spot in world.ReedSpots)
            lines.Add($"reeds {spot.Number} {P(spot.Position)}");
        for (int visit = 1; visit <= 2; visit++)
            foreach (var bird in world.BirdsOfVisit(visit))
                lines.Add(
                    $"visit {visit} bird {bird.Number} {FerryPlans.Name(bird.Kind)} reeds {bird.Spot} yaw {F(bird.Yaw)}"
                );
        return lines;
    }

    /// <summary>
    /// What came after the ferry (<c>docs/CASTLE_PLAN.md</c>): the castle on the hill, the princess at its gate, its four
    /// beacons and the guests of its feast.
    /// </summary>
    internal static string CastleFileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.castle.txt");

    internal static List<string> CastleLines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        if (world.CastlePlace is not { } site)
        {
            lines.Add("castle none");
            return lines;
        }
        lines.Add($"castle road {P(site.Road)} inward {P(site.Inward)} yaw {F(site.Yaw)}");
        lines.Add(
            $"courtyard {P(world.Ground.At(site.Center.X, site.Center.Z))} gate {P(site.Gate)} drop {P(site.Drop)}"
        );
        if (world.Castellan is { } princess)
            lines.Add($"princess {world.ActorId(princess)} {princess.DisplayName} {P(princess.Position)}");
        foreach (var beacon in world.BeaconPlaces)
            if (beacon != null)
                lines.Add($"beacon {beacon.Border} foot {P(beacon.Foot)} drop {P(beacon.Drop)} yaw {F(beacon.Yaw)}");
        for (int chair = 0; chair < CastlePlans.Guests; chair++)
            if (world.CastleGuests[chair] is { } guest)
                lines.Add(
                    $"guest {chair} {world.ActorId(guest)} {guest.DisplayName} road {P(world.CastleGuestRoads[chair])}"
                );
        return lines;
    }

    /// <summary>
    /// What came after the castle (<c>docs/MAINLINE_PLAN.md</c>): the great railway across the valley, its hub, the three
    /// arms with their terminals, the forge and the engine driver.
    /// </summary>
    internal static string Railway2FileFor(int seed) => Path.Combine(Folder, $"seed-{seed}.railway2.txt");

    internal static List<string> Railway2Lines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        if (world.MainLinePlace is not { } site)
        {
            lines.Add("railway2 none");
            return lines;
        }
        lines.Add($"hub {P(site.Hub.Position)} along {P(site.Hub.Along)} side {site.Hub.Side} half {F(site.Hub.Half)}");
        lines.Add(
            $"forge {P(site.Forge.Foot)} drop {P(site.Forge.Drop)} road {P(site.Forge.Road)} yaw {F(site.Forge.Yaw)}"
        );
        for (int arm = 0; arm < site.Arms.Length; arm++)
        {
            var terminal = site.Terminals[arm];
            var route = site.Arms[arm];
            lines.Add(
                $"terminal {arm} {P(terminal.Position)} along {P(terminal.Along)} side {terminal.Side} half {F(terminal.Half)}"
            );
            lines.Add(
                $"arm {arm} parent {route.Parent} junction {F(route.JunctionAlong)} points {route.Count} length {F(route.Length)} hash {Hash(route.Points.Select(P))} bed {Hash(Enumerable.Range(0, route.Count).Select(i => F(route.Bed(i))))}"
            );
        }
        if (world.Engineer is { } engineer)
            lines.Add($"engineer {world.ActorId(engineer)} {engineer.DisplayName} {P(engineer.Position)}");
        return lines;
    }

    internal static List<string> Lines(World world)
    {
        var lines = new List<string> { $"seed {Settings.Seed}" };
        // The glassworks' worker belongs to the file of the glassworks, which was made later.
        foreach (var actor in world.InteractionActors.Where(a => world.ActorId(a) < ActorIds.Glassworks))
            lines.Add(
                $"actor {world.ActorId(actor)} {actor.Role.GetType().Name} {actor.DisplayName} {P(actor.Position)}"
            );
        var village = world.Village;
        lines.Add(
            $"village center {P(village.Center)} garage {P(village.GarageEntrance)} greeting {P(village.GreetingSpot)}"
        );
        foreach (var plot in village.BuildPlots)
            lines.Add($"plot {plot.Index} {P(plot.Position)} drop {P(plot.Drop)} road {P(plot.Road)}");
        foreach (var project in world.Projects)
            lines.Add($"project {project.Index} focus {P(project.Focus)} first drop {P(project.DropFor(0))}");
        foreach (var works in world.Industry)
            lines.Add($"works {works.Site.Index} {P(works.Site.Position)} road {P(works.Site.Road)}");
        foreach (var load in new[] { HaulLoad.Stones, HaulLoad.Soil, HaulLoad.Sand, HaulLoad.Logs })
        foreach (var site in world.SourceSites(load))
            lines.Add($"source {load} {P(site.Position)} road {P(site.Road)}");
        lines.Add($"pickup spots {world.SpecialPickupSpots.Length} hash {Hash(world.SpecialPickupSpots.Select(P))}");
        var samples = world.Routes.Samples;
        lines.Add($"road samples {samples.Count} hash {Hash(samples.Select(P))}");
        var heights = new List<string>();
        for (int x = -500; x <= 500; x += 25)
        for (int z = -500; z <= 500; z += 25)
            heights.Add(F(world.Ground.Height(x, z)));
        lines.Add($"terrain heights {heights.Count} hash {Hash(heights)}");
        lines.Add(
            $"vegetation trees {world.Plants.TreeCount} bushes {world.Plants.BushCount} hash {Hash(world.Plants.TreePositions.Select(P))}"
        );
        return lines;
    }
}

/// <summary>
/// Writes the fingerprint of the current seed (<c>--write-world-fingerprint</c>). An existing file is kept: it
/// was made by the code before the refactoring and is what the checks hold the new code to.
/// </summary>
public partial class WorldFingerprintWriter : Node
{
    private readonly World world;

    public WorldFingerprintWriter(World world) => this.world = world;

    public override void _Ready()
    {
        try
        {
            string path = WorldFingerprint.FileFor(Settings.Seed);
            if (File.Exists(path))
            {
                GD.Print($"KEPT: {path} already exists and stays as it is");
            }
            else
            {
                Directory.CreateDirectory(WorldFingerprint.Folder);
                File.WriteAllLines(path, WorldFingerprint.Lines(world));
                GD.Print($"WROTE: {path}");
            }
            string glassworks = WorldFingerprint.GlassworksFileFor(Settings.Seed);
            if (File.Exists(glassworks))
                GD.Print($"KEPT: {glassworks} already exists and stays as it is");
            else
            {
                File.WriteAllLines(glassworks, WorldFingerprint.GlassworksLines(world));
                GD.Print($"WROTE: {glassworks}");
            }
            string outland = WorldFingerprint.OutlandFileFor(Settings.Seed);
            if (File.Exists(outland))
                GD.Print($"KEPT: {outland} already exists and stays as it is");
            else
            {
                File.WriteAllLines(outland, WorldFingerprint.OutlandLines(world));
                GD.Print($"WROTE: {outland}");
            }
            string coast = WorldFingerprint.CoastFileFor(Settings.Seed);
            if (File.Exists(coast))
                GD.Print($"KEPT: {coast} already exists and stays as it is");
            else
            {
                File.WriteAllLines(coast, WorldFingerprint.CoastLines(world));
                GD.Print($"WROTE: {coast}");
            }
            string dragon = WorldFingerprint.DragonFileFor(Settings.Seed);
            if (File.Exists(dragon))
                GD.Print($"KEPT: {dragon} already exists and stays as it is");
            else
            {
                File.WriteAllLines(dragon, WorldFingerprint.DragonLines(world));
                GD.Print($"WROTE: {dragon}");
            }
            string balloon = WorldFingerprint.BalloonFileFor(Settings.Seed);
            if (File.Exists(balloon))
                GD.Print($"KEPT: {balloon} already exists and stays as it is");
            else
            {
                File.WriteAllLines(balloon, WorldFingerprint.BalloonLines(world));
                GD.Print($"WROTE: {balloon}");
            }
            string tracking = WorldFingerprint.TrackingFileFor(Settings.Seed);
            if (File.Exists(tracking))
                GD.Print($"KEPT: {tracking} already exists and stays as it is");
            else
            {
                File.WriteAllLines(tracking, WorldFingerprint.TrackingLines(world));
                GD.Print($"WROTE: {tracking}");
            }
            string sheep = WorldFingerprint.SheepFileFor(Settings.Seed);
            if (File.Exists(sheep))
                GD.Print($"KEPT: {sheep} already exists and stays as it is");
            else
            {
                File.WriteAllLines(sheep, WorldFingerprint.SheepLines(world));
                GD.Print($"WROTE: {sheep}");
            }
            string rally = WorldFingerprint.RallyFileFor(Settings.Seed);
            if (File.Exists(rally))
                GD.Print($"KEPT: {rally} already exists and stays as it is");
            else
            {
                File.WriteAllLines(rally, WorldFingerprint.RallyLines(world));
                GD.Print($"WROTE: {rally}");
            }
            foreach (
                var (file, lines) in new[]
                {
                    (WorldFingerprint.ExplorerFileFor(Settings.Seed), WorldFingerprint.ExplorerLines(world)),
                    (WorldFingerprint.ShellsFileFor(Settings.Seed), WorldFingerprint.ShellsLines(world)),
                    (WorldFingerprint.FerryFileFor(Settings.Seed), WorldFingerprint.FerryLines(world)),
                    (WorldFingerprint.CastleFileFor(Settings.Seed), WorldFingerprint.CastleLines(world)),
                    (WorldFingerprint.Railway2FileFor(Settings.Seed), WorldFingerprint.Railway2Lines(world)),
                }
            )
            {
                if (File.Exists(file))
                    GD.Print($"KEPT: {file} already exists and stays as it is");
                else
                {
                    File.WriteAllLines(file, lines);
                    GD.Print($"WROTE: {file}");
                }
            }
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}

/// <summary>
/// <c>--world-fingerprint-checks</c>: the world generated for the seed is the one recorded in
/// <c>Tests/Worlds</c>, and a second generation of the same seed gives the same world.
/// </summary>
public partial class WorldFingerprintChecks : Node
{
    private readonly World world;

    public WorldFingerprintChecks(World world) => this.world = world;

    public override void _Ready()
    {
        try
        {
            string path = WorldFingerprint.FileFor(Settings.Seed);
            if (!File.Exists(path))
                throw new InvalidOperationException($"No fingerprint for seed {Settings.Seed}: {path}");
            string glassworksPath = WorldFingerprint.GlassworksFileFor(Settings.Seed);
            if (!File.Exists(glassworksPath))
                throw new InvalidOperationException(
                    $"No glassworks fingerprint for seed {Settings.Seed}: {glassworksPath}"
                );
            string outlandPath = WorldFingerprint.OutlandFileFor(Settings.Seed);
            if (!File.Exists(outlandPath))
                throw new InvalidOperationException($"No outland fingerprint for seed {Settings.Seed}: {outlandPath}");
            string coastPath = WorldFingerprint.CoastFileFor(Settings.Seed);
            if (!File.Exists(coastPath))
                throw new InvalidOperationException($"No coast fingerprint for seed {Settings.Seed}: {coastPath}");
            string dragonPath = WorldFingerprint.DragonFileFor(Settings.Seed);
            if (!File.Exists(dragonPath))
                throw new InvalidOperationException($"No dragon fingerprint for seed {Settings.Seed}: {dragonPath}");
            string balloonPath = WorldFingerprint.BalloonFileFor(Settings.Seed);
            if (!File.Exists(balloonPath))
                throw new InvalidOperationException($"No balloon fingerprint for seed {Settings.Seed}: {balloonPath}");
            string trackingPath = WorldFingerprint.TrackingFileFor(Settings.Seed);
            if (!File.Exists(trackingPath))
                throw new InvalidOperationException(
                    $"No tracking fingerprint for seed {Settings.Seed}: {trackingPath}"
                );
            string sheepPath = WorldFingerprint.SheepFileFor(Settings.Seed);
            if (!File.Exists(sheepPath))
                throw new InvalidOperationException($"No sheep fingerprint for seed {Settings.Seed}: {sheepPath}");
            string rallyPath = WorldFingerprint.RallyFileFor(Settings.Seed);
            if (!File.Exists(rallyPath))
                throw new InvalidOperationException($"No rally fingerprint for seed {Settings.Seed}: {rallyPath}");
            string explorerPath = WorldFingerprint.ExplorerFileFor(Settings.Seed);
            if (!File.Exists(explorerPath))
                throw new InvalidOperationException(
                    $"No explorer fingerprint for seed {Settings.Seed}: {explorerPath}"
                );
            string shellsPath = WorldFingerprint.ShellsFileFor(Settings.Seed);
            if (!File.Exists(shellsPath))
                throw new InvalidOperationException($"No shells fingerprint for seed {Settings.Seed}: {shellsPath}");
            string ferryPath = WorldFingerprint.FerryFileFor(Settings.Seed);
            if (!File.Exists(ferryPath))
                throw new InvalidOperationException($"No ferry fingerprint for seed {Settings.Seed}: {ferryPath}");
            string castlePath = WorldFingerprint.CastleFileFor(Settings.Seed);
            if (!File.Exists(castlePath))
                throw new InvalidOperationException($"No castle fingerprint for seed {Settings.Seed}: {castlePath}");
            string railway2Path = WorldFingerprint.Railway2FileFor(Settings.Seed);
            if (!File.Exists(railway2Path))
                throw new InvalidOperationException(
                    $"No great railway fingerprint for seed {Settings.Seed}: {railway2Path}"
                );
            var expected = File.ReadAllLines(path)
                .Concat(File.ReadAllLines(glassworksPath))
                .Concat(File.ReadAllLines(outlandPath))
                .Concat(File.ReadAllLines(coastPath))
                .Concat(File.ReadAllLines(dragonPath))
                .Concat(File.ReadAllLines(balloonPath))
                .Concat(File.ReadAllLines(trackingPath))
                .Concat(File.ReadAllLines(sheepPath))
                .Concat(File.ReadAllLines(rallyPath))
                .Concat(File.ReadAllLines(explorerPath))
                .Concat(File.ReadAllLines(shellsPath))
                .Concat(File.ReadAllLines(ferryPath))
                .Concat(File.ReadAllLines(castlePath))
                .Concat(File.ReadAllLines(railway2Path))
                .ToArray();
            var actual = WorldFingerprint
                .Lines(world)
                .Concat(WorldFingerprint.GlassworksLines(world))
                .Concat(WorldFingerprint.OutlandLines(world))
                .Concat(WorldFingerprint.CoastLines(world))
                .Concat(WorldFingerprint.DragonLines(world))
                .Concat(WorldFingerprint.BalloonLines(world))
                .Concat(WorldFingerprint.TrackingLines(world))
                .Concat(WorldFingerprint.SheepLines(world))
                .Concat(WorldFingerprint.RallyLines(world))
                .Concat(WorldFingerprint.ExplorerLines(world))
                .Concat(WorldFingerprint.ShellsLines(world))
                .Concat(WorldFingerprint.FerryLines(world))
                .Concat(WorldFingerprint.CastleLines(world))
                .Concat(WorldFingerprint.Railway2Lines(world))
                .ToList();
            var different = new List<string>();
            for (int i = 0; i < Math.Max(expected.Length, actual.Count); i++)
            {
                string want = i < expected.Length ? expected[i] : "(missing)";
                string have = i < actual.Count ? actual[i] : "(missing)";
                if (want != have)
                    different.Add($"line {i + 1}\n  recorded: {want}\n  now:      {have}");
            }
            if (different.Count > 0)
                throw new InvalidOperationException(
                    $"The world of seed {Settings.Seed} changed in {different.Count} lines:\n"
                        + string.Join("\n", different.Take(12))
                );
            GD.Print(
                $"PASS: the world of seed {Settings.Seed} matches the recorded fingerprint ({expected.Length} lines)"
            );
            GD.Print("WORLD_FINGERPRINT_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
