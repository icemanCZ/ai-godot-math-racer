using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MathRacer.App;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The valley beyond the old one (<c>docs/MAP_EXPANSION_PLAN.md</c>): the old valley, the core, keeps its size, and
/// the edge where the valley ends while it is played (<see cref="ValleyBounds"/>) holds everything the valley places and
/// turns the car back. The core itself is held by the world fingerprints; this suite grows with the land around it.
/// </summary>
public partial class OutlandChecks : Node
{
    private readonly World world;
    private readonly Vehicle car;

    public OutlandChecks(World world, Vehicle car)
    {
        this.world = world;
        this.car = car;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    public override async void _Ready()
    {
        try
        {
            await Frames(2);
            CheckCore();
            CheckPlacedInside();
            CheckOutlandGround();
            CheckRiverRunsOn();
            CheckBorders();
            await CheckColliders();
            CheckOutlandRoads();
            CheckOutlandPlants();
            await CheckBridges();
            await CheckCarTurnsBack();
            GD.Print("OUTLAND_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    /// <summary>The old valley keeps the size that every saved valley was made with.</summary>
    private void CheckCore()
    {
        Check(
            WorldTuning.CoreRadius == 555 && WorldTuning.CorePlayableRadius == 525,
            "the old valley keeps its terrain of ±555 m and its playable circle of 525 m"
        );
        float edge = WorldTuning.CoreRadius;
        Check(
            new[] { (-edge, -edge), (edge, -edge), (-edge, edge), (edge, edge), (0f, 0f) }.All(p =>
                float.IsFinite(world.Ground.Height(p.Item1, p.Item2))
            ),
            "the terrain has a height at the corners of the old valley"
        );
        Check(
            ValleyBounds.Contains(0, 0, WorldTuning.CorePlayableRadius - 1)
                && ValleyBounds.Contains(WorldTuning.CorePlayableRadius - 1, 0),
            "the valley that is played reaches at least as far as the old one"
        );
    }

    /// <summary>Nothing the valley places stands beyond its edge.</summary>
    private void CheckPlacedInside()
    {
        var actors = world.InteractionActors.Where(a => !ValleyBounds.Contains(a.Position.X, a.Position.Z)).ToList();
        Check(
            actors.Count == 0,
            "every character stands inside the valley"
                + (actors.Count == 0 ? "" : ": " + string.Join(", ", actors.Select(a => a.DisplayName)))
        );
        Check(
            world.SpecialPickupSpots.All(p => ValleyBounds.Contains(p.X, p.Z)),
            "every pickup spot lies inside the valley"
        );
        Check(world.Routes.Samples.All(p => ValleyBounds.Contains(p.X, p.Z)), "every road runs inside the valley");
        Check(
            world.Plants.TreePositions.All(p => ValleyBounds.Contains(p.X, p.Z)),
            "every tree grows inside the valley"
        );
    }

    /// <summary>The land round the core has ground everywhere, and it meets the core without a step.</summary>
    private void CheckOutlandGround()
    {
        var ground = world.Ground;
        Check(ground.HasOutland, "the land round the old valley is worked out once the valley is built");
        bool finite = true;
        float lowest = float.MaxValue,
            highest = float.MinValue;
        for (float z = -WorldTuning.SceneryRadius; z <= WorldTuning.SceneryRadius; z += 40)
        for (float x = -WorldTuning.SceneryRadius; x <= WorldTuning.SceneryRadius; x += 40)
        {
            float height = ground.Height(x, z);
            finite &= float.IsFinite(height);
            lowest = Mathf.Min(lowest, height);
            highest = Mathf.Max(highest, height);
        }
        Check(
            finite && lowest > -30 && highest < 300,
            $"the ground has a sensible height out to the scenery ({lowest:0.0} to {highest:0.0} m)"
        );
        float step = 0;
        float edge = WorldTuning.CoreRadius;
        for (float along = -edge; along <= edge; along += 7.3f)
            foreach (
                var (x, z, dx, dz) in new[]
                {
                    (edge, along, 1f, 0f),
                    (-edge, along, -1f, 0f),
                    (along, edge, 0f, 1f),
                    (along, -edge, 0f, -1f),
                }
            )
                step = Mathf.Max(
                    step,
                    Mathf.Abs(ground.Height(x + dx * .01f, z + dz * .01f) - ground.Height(x - dx * .01f, z - dz * .01f))
                );
        Check(step < .05f, $"the outland meets the old valley without a step (at most {step:0.000} m)");
        float outer = WorldTuning.OutlandRadius;
        float scenery = 0;
        for (float along = -outer; along <= outer; along += 10)
            scenery = Mathf.Max(
                scenery,
                Mathf.Abs(ground.Height(outer - .01f, along) - ground.Height(outer + .01f, along))
            );
        Check(scenery < .05f, $"the scenery meets the outland without a step (at most {scenery:0.000} m)");
    }

    /// <summary>The river does not end with the old valley: it runs on, as smoothly, to the edge of the scenery.</summary>
    /// <summary>The four borders are where they should be, and each of them closes the valley.</summary>
    private void CheckBorders()
    {
        var ground = world.Ground;
        var borders = WorldLayout.Plan.Borders;
        float edge = ValleyBounds.HalfSize;
        int side = WorldLayout.Plan.Side;
        bool mountains = true,
            footDrivable = true,
            sea = true,
            beachDry = true,
            river = true,
            bankDry = true;
        string riverTrouble = "";
        float lowestPeak = float.MaxValue,
            highestFoot = float.MinValue,
            shallowestSea = float.MinValue,
            lowestShore = float.MaxValue;
        int sandy = 0,
            samples = 0;
        for (float along = -edge + 30; along <= edge - 30; along += 100)
        {
            samples++;
            float peak = float.MinValue;
            for (float z = -WorldTuning.SceneryRadius; z < -1300; z += 10)
                peak = Mathf.Max(peak, ground.Height(along, z));
            mountains &= peak > 110;
            footDrivable &= ground.Height(along, -edge) < 90;
            lowestPeak = Mathf.Min(lowestPeak, peak);
            highestFoot = Mathf.Max(highestFoot, ground.Height(along, -edge));
            float deepest = float.MaxValue;
            for (float z = edge; z <= WorldTuning.SceneryRadius; z += 5)
                deepest = Mathf.Min(deepest, ground.Height(along, z));
            sea &= deepest < ValleyBorders.SeaLevel - 1.5f;
            shallowestSea = Mathf.Max(shallowestSea, deepest);
            // The valley's own river flows into the sea here: its bed is lower than the shore.
            if (River.Distance(along, edge) > River.HalfWidth(edge) + 12)
            {
                beachDry &= ground.Height(along, edge) > ValleyBorders.SeaLevel + .3f;
                lowestShore = Mathf.Min(lowestShore, ground.Height(along, edge));
            }
            if (borders.IsSand(along, borders.Coast(along) - 15))
                sandy++;
            if (along > -1000 && along < borders.CoastMinimum - 60)
            {
                float bed = float.MaxValue;
                for (float d = edge; d < edge + 140; d += 2.5f)
                    bed = Mathf.Min(bed, ground.Height(side * d, along));
                float bank = ground.Height(side * edge, along),
                    level = ValleyBorders.BorderRiverLevel(along);
                if (bed >= level - .8f || bank <= level + .3f)
                    riverTrouble += $" z={along:0}: bed {bed:0.0}, bank {bank:0.0}, water {level:0.0};";
                river &= bed < level - .8f;
                bankDry &= bank > level + .3f;
            }
        }
        Check(
            mountains && footDrivable,
            $"high mountains close the north (lowest peak {lowestPeak:0}), with their foot at the edge of the valley"
                + $" (at most {highestFoot:0} m)"
        );
        Check(
            sea && beachDry,
            $"the sea closes the south (its shallowest deep {shallowestSea:0.0}), beyond a dry shore at the edge of the"
                + $" valley (at least {lowestShore:0.0})"
        );
        Check(sandy == samples, $"a beach lies all along the sea ({sandy} of {samples} places)");
        Check(
            river && bankDry,
            "the border river closes its side, beyond a dry bank at the edge of the valley" + riverTrouble
        );
        var trees = world.BorderTrees.Feet;
        Check(
            trees.Count > 8000 && trees.All(p => !ValleyBounds.Contains(p.X, p.Z) && p.Y > ValleyBorders.SeaLevel + 1),
            $"a forest of {trees.Count} trees stands beyond the edge of the valley, none in the water"
        );
        int forestSide = trees.Count(p => -side * p.X > edge);
        Check(forestSide * 2 > trees.Count, $"most of them form the forest on its side ({forestSide})");
        float spring = -1500;
        Check(
            ground.Height(River.CenterX(spring), spring) > River.Level(spring) + 5
                && ground.Height(borders.BorderRiverX(spring), spring) > ValleyBorders.BorderRiverLevel(spring) + 5,
            "both rivers rise in the mountains: high up their beds close"
        );
    }

    private static void CheckRiverRunsOn()
    {
        float jump = Mathf.Max(
            Mathf.Abs(River.CenterX(WorldTuning.CoreRadius + .01f) - River.CenterX(WorldTuning.CoreRadius - .01f)),
            Mathf.Abs(River.CenterX(-WorldTuning.CoreRadius - .01f) - River.CenterX(-WorldTuning.CoreRadius + .01f))
        );
        float steepest = 0;
        for (float z = -WorldTuning.SceneryRadius; z < WorldTuning.SceneryRadius; z += 8)
            steepest = Mathf.Max(steepest, Mathf.Abs(River.CenterX(z + 8) - River.CenterX(z)) / 8);
        Check(
            jump < .01f && steepest < .35f,
            $"the river runs on beyond the old valley without a jump and bends gently (slope at most {steepest:0.00})"
        );
    }

    /// <summary>
    /// The outland has collision only round the car: chunks get it when the car comes near and lose it once it is gone,
    /// and their triangles are the ground <see cref="Terrain.Height"/> gives.
    /// </summary>
    private async Task CheckColliders()
    {
        var ground = world.Ground;
        var spot = new Vector3(WorldTuning.CoreRadius + 150, 0, -300);
        ground.StreamColliders(spot);
        int near = ground.OutlandColliderCount;
        var faces = ground
            .OutlandColliders.SelectMany(body =>
                ((ConcavePolygonShape3D)body.GetChild<CollisionShape3D>(0).Shape).GetFaces()
            )
            .ToArray();
        float worst = 0;
        foreach (var face in faces.Where((_, i) => i % 97 == 0))
            worst = Mathf.Max(worst, Mathf.Abs(face.Y - ground.Height(face.X, face.Z)));
        Check(
            near > 0 && faces.Length > 0 && worst < .001f,
            $"the outland round a car has collision on its ground ({near} chunks, off by at most {worst:0.0000} m)"
        );
        ground.StreamColliders(Vector3.Zero);
        await Frames(2);
        Check(ground.OutlandColliderCount == 0, "the collision goes again once the car is back in the old valley");
    }

    /// <summary>
    /// On every side the car that drives at the edge with full throttle stops there and stays in the valley; a car put
    /// far beyond it goes back to the start.
    /// </summary>
    /// <summary>
    /// The roads beyond the old valley: a ring round it, joined to the old roads, and a road out to each border. They
    /// run on dry, gently graded ground inside the valley, cross the river only on the bridges and pass no tree of the
    /// old valley.
    /// </summary>
    private void CheckOutlandRoads()
    {
        var routes = world.OutlandRoutePoints;
        var roads = world.OutlandRoutes;
        int borderRoads = routes.Length - 1 - world.OutlandJoinCount;
        Check(
            routes.Length > 0 && routes[0][0] == routes[0][^1] && world.OutlandJoinCount >= 2 && borderRoads == 4,
            $"a ring road goes round the old valley, {world.OutlandJoinCount} roads join it to the old ones and"
                + $" {borderRoads} run out to the borders"
        );
        var layout = WorldLayout.Plan.Outland;
        var borders = WorldLayout.Plan.Borders;
        var ground = world.Ground;
        int wet = 0,
            outside = 0,
            nearTree = 0;
        float steepest = 0;
        var steepestAt = Vector3.Zero;
        var samples = roads.Samples;
        for (int i = 0; i < samples.Count; i++)
        {
            var p = samples[i];
            bool onBridge = layout.BridgeAt(p.X, p.Z, 4) != null;
            if (!ValleyBounds.Contains(p.X, p.Z, 20))
                outside++;
            if (
                !onBridge
                && (
                    River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 4
                    || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 6
                    || ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + .5f
                )
            )
                wet++;
            if (world.Plants.NearTrunk(p, 4.5f))
                nearTree++;
            bool inCore = Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) < WorldTuning.CoreRadius + 50;
            if (i + 1 < samples.Count && !inCore && !onBridge)
            {
                var q = samples[i + 1];
                float run = FlatMath.Length(q.X - p.X, q.Z - p.Z);
                float grade = Mathf.Abs(ground.Height(q.X, q.Z) - ground.Height(p.X, p.Z)) / run;
                if (run > .5f && run < 3 && grade > steepest)
                {
                    steepest = grade;
                    steepestAt = p;
                }
            }
        }
        Check(outside == 0, $"the roads beyond the old valley stay inside it ({samples.Count} points)");
        Check(wet == 0, $"they keep out of the water and cross the river only on its bridges ({wet} points wet)");
        Check(nearTree == 0, $"none of them passes through a tree ({nearTree} points)");
        Check(
            steepest < .2f,
            $"their ground is graded gently (steepest {steepest:0.00} at {steepestAt.X:0} {steepestAt.Z:0})"
        );
        var bare = samples
            .Where(p =>
                Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > WorldTuning.CoreRadius + 60
                && layout.BridgeAt(p.X, p.Z, 1) == null
                && !roads.Contains(p)
            )
            .ToList();
        Check(
            bare.Count == 0,
            "they have a surface all along beyond the old valley, off the bridges"
                + (bare.Count == 0 ? "" : $" ({bare.Count} points without, first at {bare[0].X:0} {bare[0].Z:0})")
        );
    }

    /// <summary>The land round the old valley is planted as thickly, on dry ground and off its roads.</summary>
    private void CheckOutlandPlants()
    {
        var plants = world.Plants;
        var borders = WorldLayout.Plan.Borders;
        Check(
            plants.OutlandTreeCount > plants.TreeCount * 3 && plants.OutlandBushCount > plants.BushCount * 3,
            $"the land round the old valley has {plants.OutlandTreeCount} trees and {plants.OutlandBushCount} bushes"
                + $" (the old valley {plants.TreeCount} and {plants.BushCount})"
        );
        var misplaced = plants
            .OutlandTreePositions.Where(p =>
                !ValleyBounds.Contains(p.X, p.Z, 8)
                || FlatMath.Length(p.X, p.Z) <= WorldTuning.CorePlayableRadius
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 8
                || borders.BorderRiverDistance(p.X, p.Z) < ValleyBorders.BorderRiverHalfWidth + 8
                || borders.IsSand(p.X, p.Z)
                || world.OutlandRoutes.Distance(p) < 7
                || WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 8) != null
            )
            .ToList();
        Check(
            misplaced.Count == 0,
            "its trees grow beyond the old valley, inside the edge, on dry ground and away from the roads"
                + (
                    misplaced.Count == 0
                        ? ""
                        : $" ({misplaced.Count} not, first at {misplaced[0].X:0} {misplaced[0].Z:0})"
                )
        );
        // The plants themselves are built only near the car; here round a point of the ring.
        plants.BuildOutlandAround(world.OutlandRoutePoints[0][0]);
        Check(
            plants.OutlandBlocksBuilt > 0 && plants.OutlandDetailIsNear,
            $"its plants are built near the car ({plants.OutlandBlocksBuilt} blocks) and drawn in full only near the camera"
        );
    }

    /// <summary>A car driving straight over each bridge of the ring comes across dry, on the far bank.</summary>
    private async Task CheckBridges()
    {
        foreach (var bridge in WorldLayout.Plan.Outland.Bridges)
        {
            foreach (int way in new[] { 1, -1 })
            {
                var start = new Vector3(bridge.CenterX - way * (bridge.HalfLength + 30), 0, bridge.Z);
                world.Ground.StreamColliders(start);
                car.SetPose(world.Ground.At(start.X, start.Z, .15f), -way * Mathf.Pi / 2);
                float lowest = float.MaxValue;
                Input.ActionPress("accelerate");
                for (int frame = 0; frame < 600; frame++)
                {
                    await Frames(1);
                    if (Mathf.Abs(car.Position.X - bridge.CenterX) < bridge.HalfLength)
                        lowest = Mathf.Min(lowest, car.Position.Y);
                    if (way * (car.Position.X - bridge.CenterX) > bridge.HalfLength + 12)
                        break;
                }
                Input.ActionRelease("accelerate");
                Check(
                    way * (car.Position.X - bridge.CenterX) > bridge.HalfLength + 5
                        && lowest > River.Level(bridge.Z) + .5f
                        && Mathf.Abs(car.Position.Z - bridge.Z) < OutlandLayout.Bridge.HalfWidth,
                    $"a car drives over the bridge at z = {bridge.Z:0} {(way > 0 ? "one way" : "and back")}"
                        + $" (at x = {car.Position.X - bridge.CenterX:0.0} from its middle and {car.Position.Z - bridge.Z:0.0} across, lowest {lowest - River.Level(bridge.Z):0.00} m over the water)"
                );
            }
        }
        car.Reset();
    }

    private async Task CheckCarTurnsBack()
    {
        int side = WorldLayout.Plan.Side;
        float near = ValleyBounds.HalfSize - 10;
        var sides = new (string Name, Vector3 Spot, float Heading)[]
        {
            ("the mountains", new Vector3(0, 0, -near), 0),
            ("the sea", new Vector3(0, 0, near), Mathf.Pi),
            ("the border river", new Vector3(side * near, 0, 0), -side * Mathf.Pi / 2),
            ("the forest", new Vector3(-side * near, 0, 0), side * Mathf.Pi / 2),
        };
        // Solid things the car could run into on its way: trees, rocks and the rest, with the plants built there.
        foreach (var (_, spot, _) in sides)
            world.Plants.BuildOutlandAround(spot);
        var solids = GetTree()
            .GetNodesInGroup("solid_obstacles")
            .OfType<Node3D>()
            .Where(node => node.IsInsideTree())
            .Select(node => node.GlobalPosition)
            .ToList();
        foreach (var (name, edgeSpot, heading) in sides)
        {
            // Along the edge to the first place where the way to it is clear.
            var forward = new Vector3(-Mathf.Sin(heading), 0, -Mathf.Cos(heading));
            var along = forward.Cross(Vector3.Up);
            var spot = edgeSpot;
            for (int step = 1; step < 60; step++)
            {
                bool clear = true;
                for (float ahead = -4; ahead <= 14 && clear; ahead += 2)
                {
                    var p = spot + forward * ahead;
                    clear =
                        !world.Plants.NearTrunk(p, 3.5f)
                        && !solids.Any(other => FlatMath.Length(other.X - p.X, other.Z - p.Z) < 3.5f)
                        && world.Ground.Height(p.X, p.Z) > ValleyBorders.SeaLevel + .5f;
                }
                if (clear)
                    break;
                spot = edgeSpot + along * (step % 2 == 0 ? step : -step) * 6;
            }
            world.Ground.StreamColliders(spot);
            car.SetPose(world.Ground.At(spot.X, spot.Z, .15f), heading);
            // By the sea the car drives on down the beach (docs/SHELLS_PLAN.md), which takes longer, and stops at the water.
            bool sea = name == "the sea";
            Input.ActionPress("accelerate");
            await Frames(sea ? 900 : 240);
            Input.ActionRelease("accelerate");
            float travelled = FlatMath.Length(car.Position.X - spot.X, car.Position.Z - spot.Z);
            if (sea)
            {
                float depth = world.SeaDepth(car.Position);
                Check(
                    !ValleyBounds.Contains(car.Position.X, car.Position.Z) && depth < .5f && travelled > 20,
                    $"a car driving towards the sea goes down the beach and stops at the water (it drove {travelled:0.0} m,"
                        + $" {depth:0.00} m deep)"
                );
                continue;
            }
            Check(
                ValleyBounds.Contains(car.Position.X, car.Position.Z) && travelled > 5,
                $"a car driving towards {name} stops at the edge of the valley (it drove {travelled:0.0} m)"
            );
        }
        car.SetPose(world.Ground.At(-ValleyBounds.HalfSize - 40, 0, .15f), 0);
        await Frames(10);
        Check(
            FlatMath.Length(car.Position.X, car.Position.Z - 8) < 3,
            "a car put far beyond the edge of the valley goes back to the start"
        );
        car.Reset();
    }
}
