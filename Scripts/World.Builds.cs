using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer;

/// <summary>The sites of the mill, the lookout tower and the jetty, with the cuts that make their ways drivable.</summary>
internal sealed record BuildPlan(BuildSite[] Sites, List<Terrain.GradedPiece> Grading);

/// <summary>The builder of one of the mill, the lookout tower or the jetty.</summary>
internal sealed record BuilderSpec(string Name, string Greeting, int Kind, string Fur, string Hat);

internal static class BuilderCatalog
{
    /// <summary>Indexed by project; the bridge's builder is the beaver, who is created with the bridge.</summary>
    public static readonly BuilderSpec?[] All =
    {
        null,
        new("Mlynář Bruno", "Postavíme větrný mlýn! Přivez mi štěrk, cihly a prkna.", 2, "a0785a", "e8e0cf"),
        new("Hvězdář Kryštof", "Postavíme rozhlednu! Přivez mi štěrk, cihly a prkna.", 0, "9fb4e0", "5b4b9e"),
        new("Rybář Vilda", "Postavíme molo! Přivez mi štěrk, klády a prkna.", 1, "d98a4a", "3f7fb0"),
    };
}

public partial class World
{
    /// <summary>The steepest grade, rise over run, that a way to a building may keep without being cut into the ground.</summary>
    internal const float PathMaxGrade = .30f;

    /// <summary>Half the width of the way that is cut into a slope; the shoulder eases it back to the meadow.</summary>
    private const float CutHalfWidth = 4.5f;

    /// <summary>Ground this close to a road is never cut: the road was laid on it as it was (matches the terrain's margin).</summary>
    private const float RoadMargin = 11;

    private readonly int[] buildRejections = new int[8];

    /// <summary>Cut allowed in each pass of the search, in metres of ground moved at the worst point.</summary>
    private static readonly float[] CutPasses = { 0, 1.5f, 3, 4.5f, 6.5f };

    /// <summary>
    /// Plans the mill, the lookout tower and the jetty: each gets a spot beside a road with a drivable way to it. A
    /// way that is too steep is cut into the ground, within the cut that the pass of the search allows.
    /// Returns null (and plans nothing) when any of them has no room.
    /// </summary>
    private BuildPlan? PlanBuilds(FordSite ford)
    {
        var random = new Random(Settings.Seed + 2330);
        var friends = InteractionActors.ToArray();
        int approaches = haulApproaches.Count;
        int clearings = haulClearings.Count;
        var sites = new List<BuildSite>();
        var grading = new List<Terrain.GradedPiece>();
        bool failed = false;
        foreach (int project in new[] { BuildPlans.Tower, BuildPlans.Mill, BuildPlans.Jetty })
        {
            Array.Clear(buildRejections);
            var found =
                project == BuildPlans.Jetty
                    ? FindJettySite(random, friends, ford)
                    : FindBuildSite(project, random, friends, ford);
            if (found == null)
            {
                GD.Print(
                    $"BUILDS_MISSING seed={Settings.Seed} project={project} "
                        + $"rejected(bank,river,spacing,spot,footprint,path,cut,other)={string.Join(",", buildRejections)}"
                );
                failed = true;
                break;
            }
            var (site, pieces) = found.Value;
            sites.Add(site);
            grading.AddRange(pieces);
            haulApproaches.Add((site.Road, site.Position));
            var right = Vector3.Up.Cross(site.Inward);
            haulClearings.Add(site.Position);
            if (project == BuildPlans.Jetty)
            {
                haulClearings.Add(site.Position + site.Inward * 8);
                haulClearings.Add(site.Position + site.Inward * 16);
            }
            else
            {
                haulClearings.Add(site.Position + site.Inward * 11);
                haulClearings.Add(site.Position + site.Inward * 6 + right * 7);
                haulClearings.Add(site.Position + site.Inward * 6 - right * 7);
            }
        }
        if (failed)
        {
            haulApproaches.RemoveRange(approaches, haulApproaches.Count - approaches);
            haulClearings.RemoveRange(clearings, haulClearings.Count - clearings);
            return null;
        }
        GD.Print(
            "BUILDS_READY "
                + string.Join(" ", sites.Select(s => $"project{s.Project}=path{FlatDistance(s.Road, s.Position):0}"))
                + $" cuts={grading.Count}"
        );
        return new BuildPlan(sites.OrderBy(s => s.Project).ToArray(), grading);
    }

    /// <summary>
    /// Plans a way from <paramref name="road"/> to <paramref name="ring"/>. Where the ground keeps to the grade
    /// limit it is left alone; where it is steeper, the way is cut into the slope (or filled) just enough to
    /// keep the limit, with the way's ends pinned to the road and to the ring. Returns false when that would move
    /// more than <paramref name="maxCut"/> metres of ground. The last piece is a level platform at the ring.
    /// </summary>
    private bool PlanWay(Vector3 road, Vector3 ring, float maxCut, out List<Terrain.GradedPiece> pieces)
    {
        pieces = new List<Terrain.GradedPiece>();
        var a = new Vector2(road.X, road.Z);
        var b = new Vector2(ring.X, ring.Z);
        float length = a.DistanceTo(b);
        int steps = Mathf.Max(2, Mathf.CeilToInt(length / 2));
        float stepLength = length / steps;
        float limit = PathMaxGrade * stepLength;
        var natural = new float[steps + 1];
        var points = new Vector2[steps + 1];
        for (int i = 0; i <= steps; i++)
        {
            points[i] = a.Lerp(b, i / (float)steps);
            natural[i] = Ground.Height(points[i].X, points[i].Y);
        }
        // The ground beside roads and rivers is left as it is (see Terrain.Grade and the river's own shaping), so
        // the design follows it there and only the stretches in between are cut.
        var pinned = new bool[steps + 1];
        for (int i = 0; i <= steps; i++)
            pinned[i] =
                i == 0
                || Routes.Distance(new Vector3(points[i].X, 0, points[i].Y)) < RoadMargin
                || River.Distance(points[i].X, points[i].Y) < River.HalfWidth(points[i].Y) + 25;
        // The stretches that stay as they are must be gentle enough to drive: otherwise this way is rejected.
        for (int i = 0; i < steps; i++)
            if (pinned[i] && pinned[i + 1] && Mathf.Abs(natural[i + 1] - natural[i]) / stepLength > .55f)
                return false;
        var design = (float[])natural.Clone();
        float reachEnd = limit * steps;
        if (!pinned[steps])
            design[steps] = Mathf.Clamp(natural[steps], natural[0] - reachEnd, natural[0] + reachEnd);
        for (int pass = 0; pass < 10; pass++)
        {
            for (int i = 1; i <= steps; i++)
                if (!pinned[i])
                    design[i] = Mathf.Clamp(design[i], design[i - 1] - limit, design[i - 1] + limit);
            if (!pinned[steps])
                design[steps] = Mathf.Clamp(natural[steps], natural[0] - reachEnd, natural[0] + reachEnd);
            for (int i = steps - 1; i >= 0; i--)
                if (!pinned[i])
                    design[i] = Mathf.Clamp(design[i], design[i + 1] - limit, design[i + 1] + limit);
        }
        for (int i = 1; i <= steps; i++)
            if (!pinned[i])
                design[i] = Mathf.Clamp(design[i], design[i - 1] - limit, design[i - 1] + limit);
        float cut = 0;
        for (int i = 0; i <= steps; i++)
            cut = Mathf.Max(cut, Mathf.Abs(design[i] - natural[i]));
        if (cut > maxCut)
            return false;
        // Once any part has to be cut, every stretch that is not pinned is laid at its designed heights.
        if (cut > .05f)
            for (int i = 0; i < steps; i++)
                if (!pinned[i] || !pinned[i + 1])
                    pieces.Add(
                        new Terrain.GradedPiece(points[i], points[i + 1], design[i], design[i + 1], CutHalfWidth, 7)
                    );
        // The level platform for the building (or the bank) at the ring, at the height the way arrives at.
        pieces.Add(new Terrain.GradedPiece(b, b, design[steps], design[steps], 7, 6));
        return true;
    }

    /// <summary>The best spot for the mill or the lookout tower: the highest one that has a drivable way.</summary>
    private (BuildSite Site, List<Terrain.GradedPiece> Grading)? FindBuildSite(
        int project,
        Random random,
        Animal[] friends,
        FordSite ford
    )
    {
        var bank = Enumerable
            .Range(0, Routes.Samples.Count - 1)
            .Where(i =>
            {
                var forward = Routes.Samples[i + 1] - Routes.Samples[i];
                forward.Y = 0;
                return !ford.OnFarBank(Routes.Samples[i])
                    && forward.LengthSquared() is > .01f and <= 9
                    && !River.OnBridge(Routes.Samples[i].X, Routes.Samples[i].Z);
            })
            .ToArray();
        for (int pass = 0; pass < SpurPasses.Length; pass++)
        {
            var rules = SpurPasses[pass];
            float cut = CutPasses[pass];
            (BuildSite Site, List<Terrain.GradedPiece> Grading)? best = null;
            float bestScore = float.NegativeInfinity;
            int found = 0;
            for (int attempt = 0; attempt < rules.Attempts && found < 10 && bank.Length > 0; attempt++)
            {
                int index = bank[random.Next(bank.Length)];
                var road = Routes.Samples[index];
                var forward = Routes.Samples[index + 1] - road;
                forward.Y = 0;
                var outward = forward.Normalized().Cross(Vector3.Up) * (random.Next(2) == 0 ? -1 : 1);
                float distance = rules.MinOffset + (float)random.NextDouble() * (rules.MaxOffset - rules.MinOffset);
                var point = Ground.At(road.X + outward.X * distance, road.Z + outward.Z * distance);
                int reason =
                    ford.OnFarBank(point) ? 0
                    : River.Distance(point.X, point.Z) < River.HalfWidth(point.Z) + rules.RiverMargin ? 1
                    : haulClearings.Any(other => FlatDistance(other, point) < rules.Spacing) ? 2
                    : !FreeHaulSpot(point, friends, rules.RoadClearance) ? 3
                    : !WorksFootprintFits(point, outward, friends, rules with { Slope = rules.Slope + cut }) ? 4
                    : !ClearHaulPath(road, point, friends, toWater: false, checkSlope: false) ? 5
                    : -1;
                if (reason >= 0)
                {
                    buildRejections[reason]++;
                    continue;
                }
                if (!PlanWay(road, point, cut, out var pieces))
                {
                    buildRejections[6]++;
                    continue;
                }
                found++;
                // The tower wants the highest ground, the mill likes a hill too but cares more about a short way.
                float height = pieces.Count > 0 ? pieces[^1].HeightB : point.Y;
                float score = height * (project == BuildPlans.Tower ? 1f : .5f) - FlatDistance(road, point) * .05f;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                var right = Vector3.Up.Cross(outward);
                var spot = Ground.At(
                    point.X - right.X * 4.8f + outward.X * 1.5f,
                    point.Z - right.Z * 4.8f + outward.Z * 1.5f
                );
                var ring = new Vector3(point.X, height, point.Z);
                // A level platform for the building behind the ring, at the height of the ring.
                var platform = point + outward * 7;
                pieces.Add(
                    new Terrain.GradedPiece(
                        new(platform.X, platform.Z),
                        new(platform.X, platform.Z),
                        height,
                        height,
                        8.5f,
                        7
                    )
                );
                best = (new BuildSite(project, ring, road, outward, new Vector3(spot.X, height, spot.Z)), pieces);
            }
            if (best != null)
                return best;
        }
        return null;
    }

    /// <summary>
    /// The jetty: on the village bank, reached from a road, with its ring on dry ground a few metres from the
    /// water and the deck running towards the river. A steep bank is cut into the ground like any other way.
    /// </summary>
    private (BuildSite Site, List<Terrain.GradedPiece> Grading)? FindJettySite(
        Random random,
        Animal[] friends,
        FordSite ford
    )
    {
        float side = ford.Side;
        for (int pass = 0; pass < SpurPasses.Length; pass++)
        {
            var rules = SpurPasses[pass];
            float cut = CutPasses[pass];
            (BuildSite Site, List<Terrain.GradedPiece> Grading)? best = null;
            float bestScore = float.NegativeInfinity;
            for (float z = -Settings.PlayableRadius + 40; z <= Settings.PlayableRadius - 40; z += 4)
            {
                if (Mathf.Abs(z - River.BridgeZ) < 40 + pass * -6 || Mathf.Abs(z - ford.Z) < 44 + pass * -6)
                    continue;
                float centerX = River.CenterX(z);
                var inward = new Vector3(side, 0, 0);
                var ring = Ground.At(centerX - side * (River.HalfWidth(z) + 7), z);
                float level = River.Level(z);
                if (ring.Y < level + .3f || ring.Y > level + 4 + cut)
                {
                    buildRejections[7]++;
                    continue;
                }
                if (
                    haulClearings.Any(other => FlatDistance(other, ring) < rules.Spacing)
                    || !FreeHaulSpot(ring, friends, 6)
                )
                {
                    buildRejections[2]++;
                    continue;
                }
                // The bank beside the jetty has to be free for the builder and the pier's surroundings.
                bool free = true;
                foreach (float lateral in new[] { -5f, 0, 5 })
                foreach (float along in new[] { -3f, 3, 8 })
                {
                    var q = ring + new Vector3(inward.X * along, 0, lateral);
                    free &=
                        !AnyCloserThan(ActorSpots(friends), q.X, q.Z, 9, inclusive: true)
                        && new Vector2(TowerSpot.X - q.X, TowerSpot.Y - q.Z).Length() > 18
                        && !Village.IsReserved(q)
                        && !Village.IsNearVillage(q)
                        && !IsPickupApproachReserved(q);
                }
                if (!free)
                {
                    buildRejections[4]++;
                    continue;
                }
                var roadPoint = BankRoad(
                    ring,
                    farBank: false,
                    side,
                    friends,
                    160 + pass * 120,
                    40 + pass * 30,
                    checkSlope: false
                );
                if (roadPoint is not Vector3 road)
                {
                    buildRejections[5]++;
                    continue;
                }
                if (!PlanWay(road, ring, cut, out var pieces))
                {
                    buildRejections[6]++;
                    continue;
                }
                float height = pieces.Count > 0 ? pieces[^1].HeightB : ring.Y;
                if (height < level + .6f)
                {
                    buildRejections[7]++;
                    continue;
                }
                float score = -FlatDistance(road, ring) - Mathf.Abs(ring.Y - (level + 1.5f)) * 8;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                var spot = Ground.At(ring.X, ring.Z + 5);
                // A level bank for the footing and the ring, even if the way itself needs no cut.
                if (pieces.Count == 0)
                    pieces.Add(new Terrain.GradedPiece(new(ring.X, ring.Z), new(ring.X, ring.Z), height, height, 6, 5));
                best = (
                    new BuildSite(
                        BuildPlans.Jetty,
                        new Vector3(ring.X, height, ring.Z),
                        road,
                        inward,
                        new Vector3(spot.X, height, spot.Z)
                    ),
                    pieces
                );
            }
            if (best != null)
                return best;
        }
        return null;
    }

    /// <summary>The jetty's duck waits beside the fisherman until the jetty is finished.</summary>
    private void CreateDuck(Jetty jetty, Animal fisherman)
    {
        var duck = new CollectorPet(CollectorPetKind.Duck, Village, () => jetty.Stage, fisherman);
        var right = Vector3.Up.Cross(jetty.Site.Inward);
        var spot = jetty.Site.BuilderSpot + right * 2.4f;
        duck.SetHome(Ground.At(spot.X, spot.Z), Mathf.Atan2(jetty.Site.Inward.X, jetty.Site.Inward.Z));
        AddChild(duck);
        Duck = duck;
    }

    /// <summary>Places the mill, the tower and the jetty with their builders, after the terrain has been cut for them.</summary>
    private void BuildProjects(BuildPlan? plan)
    {
        if (plan == null || Ford == null)
            return;
        var projects = new BuildProject[BuildPlans.Count];
        projects[BuildPlans.Bridge] = Ford;
        foreach (var site in plan.Sites)
        {
            SitedProject project = site.Project switch
            {
                BuildPlans.Mill => new Mill(site, Ground),
                BuildPlans.Tower => new Lookout(site, Ground),
                _ => new Jetty(site, Ground),
            };
            AddChild(project);
            var spec = BuilderCatalog.All[site.Project]!;
            var builder = Animal.Create(spec.Name, spec.Greeting, spec.Kind, spec.Fur);
            builder.Role = new BuilderRole(site.Project);
            builder.DressAsWorker(spec.Hat);
            builder.Position = site.BuilderSpot;
            var facing = site.Position - site.BuilderSpot;
            builder.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
            AddChild(builder);
            project.Builder = builder;
            actors.Add(BuilderId(site.Project), builder);
            projects[site.Project] = project;
            if (project is Jetty jetty)
                CreateDuck(jetty, builder);
        }
        Projects = projects;
    }
}
