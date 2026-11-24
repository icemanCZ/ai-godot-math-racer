using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The plants of the land between the old valley and the borders (<c>docs/MAP_EXPANSION_PLAN.md</c>, phase 4): groves,
/// trees in the meadows and along the roads, bushes and flowers with their rocks, as thick as in the old valley. Where
/// each one stands is worked out last, from random generators of their own, and kept apart from the old valley's plants:
/// <see cref="TreeCount"/>, <see cref="BushCount"/> and <see cref="TreePositions"/> still tell the old valley alone, as
/// every saved valley knows it. <see cref="NearTrunk"/> sees them all from the start.
/// <para>
/// The plants themselves, with their trunks, rocks and trampling, are built only a block of 64 m at a time once the car
/// comes near it (<see cref="StreamOutland"/>), and stay built. Their parts come from random numbers of the block, so
/// that a tree looks the same whenever it is built; the land needs no more than that to stay the same. From far away
/// every tree is one simple shape (<see cref="BuildDistantTrees"/>), which is made for all of them at once.
/// </para>
/// </summary>
public partial class Vegetation
{
    /// <summary>Whether the plants now placed or built stand beyond the old valley.</summary>
    private bool outlandPass;

    /// <summary>Whether the plants beyond the old valley are being planned (placed, not yet built).</summary>
    private bool outlandPlanning;

    private readonly Dictionary<Vector2I, List<Vector3>> outlandTreePositions = new();

    /// <summary>The kinds of tree in the groves beyond the old valley, as in it: regions round a centre each.</summary>
    private (Vector2 Center, int Type)[] outlandRegions = Array.Empty<(Vector2, int)>();

    internal IEnumerable<Vector3> OutlandTreePositions => outlandTreePositions.Values.SelectMany(trees => trees);

    /// <summary>The near batches of the plants beyond the old valley built so far.</summary>
    private readonly List<MultiMeshInstance3D> outlandNear = new();

    /// <summary>Everything drawn of the plants beyond the old valley, near and far (the trunks and rocks aside).</summary>
    internal Node3D OutlandPlants { get; } = new() { Name = "OutlandPlants" };

    /// <summary>
    /// The ground beyond the old valley that is built on after its plants were planned (the homesteads of the borders):
    /// a plant whose foot lies there stays in the plan, and so in <see cref="OutlandTreePositions"/> and the counts, but
    /// is never built, near or far.
    /// </summary>
    internal Func<Vector3, bool>? OutlandCleared { get; set; }

    /// <summary>
    /// Whether every block of plants beyond the old valley built so far is drawn in full only near the camera, and with
    /// its simpler shapes (or not at all) further away.
    /// </summary>
    internal bool OutlandDetailIsNear =>
        outlandNear.Count > 0
        && outlandNear.All(near => near.VisibilityRangeEnd > 0 && near.VisibilityRangeEnd <= FarDistance);
    public int OutlandTreeCount { get; private set; }
    public int OutlandBushCount { get; private set; }

    /// <summary>How many blocks of plants beyond the old valley are built, for the checks.</summary>
    internal int OutlandBlocksBuilt => outlandBuilt.Count;

    /// <summary>The old valley's plants are scattered over a square of this half size.</summary>
    private const float CoreScatter = 510;

    /// <summary>How many times more plants the land round the old valley gets than the old valley, for its larger area.</summary>
    private static float OutlandShare => Mathf.Pow(ValleyBounds.HalfSize / CoreScatter, 2) - 1;

    private enum OutlandKind : byte
    {
        Tree,
        Bush,
        Flower,
    }

    /// <param name="Number">For a tree, how many trees beyond the old valley were planted with it; for a flower, its
    /// number in its pass.</param>
    private readonly record struct OutlandPlant(Vector3 Foot, float Size, OutlandKind Kind, int Type, int Number);

    /// <summary>The plants beyond the old valley by the 64 m block they stand in, until it is built.</summary>
    private readonly Dictionary<Vector2I, List<OutlandPlant>> outlandPlan = new();
    private readonly HashSet<Vector2I> outlandBuilt = new();

    /// <summary>The batches of each built block get keys of their own, since a plant's parts reach over its block.</summary>
    private int outlandBuildGroup;

    private static Vector2I BlockOf(Vector3 p) => new(Mathf.FloorToInt(p.X / 64), Mathf.FloorToInt(p.Z / 64));

    private void PlanOutland(OutlandPlant plant)
    {
        var block = BlockOf(plant.Foot);
        if (!outlandPlan.TryGetValue(block, out var list))
            outlandPlan[block] = list = new();
        list.Add(plant);
    }

    /// <summary>
    /// Places the plants of the land beyond the old valley, on its final ground and beside its roads, where nothing else
    /// stands (<paramref name="blocked"/>): out of the water, off the beach and short of the border forest, which has
    /// trees of its own. The old valley's plants are drawn already and nothing of them changes.
    /// </summary>
    internal void GenerateOutland(Terrain terrain, Roads roads, Roads outlandRoads, Func<Vector3, bool> blocked)
    {
        outlandPass = true;
        outlandPlanning = true;
        rng = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + 7750) };
        var borders = WorldLayout.Plan.Borders;
        float half = ValleyBounds.HalfSize;
        float share = OutlandShare;
        int Scaled(int count) => Mathf.RoundToInt(count * share);
        outlandRegions = Enumerable
            .Range(0, Scaled(21))
            .Select(_ => (new Vector2(rng.RandfRange(-half, half), rng.RandfRange(-half, half)), rng.RandiRange(0, 6)))
            .ToArray();
        bool Free(Vector3 p, float roadClearance, float waterClearance) =>
            FlatMath.Length(p.X, p.Z) > WorldTuning.CorePlayableRadius
            && ValleyBounds.Contains(p.X, p.Z, 8)
            && p.Y > ValleyBorders.SeaLevel + 1.2f
            && River.Distance(p.X, p.Z) > River.HalfWidth(p.Z) + waterClearance
            && borders.BorderRiverDistance(p.X, p.Z) > ValleyBorders.BorderRiverHalfWidth + waterClearance
            && borders.Sand(p.X, p.Z) < .05f
            && outlandRoads.Distance(p) > roadClearance
            && roads.Distance(p) > roadClearance
            && !blocked(p);
        Vector3 Anywhere() => terrain.At(rng.RandfRange(-half, half), rng.RandfRange(-half, half));
        void PlanBush(Vector3 p, float size, int type)
        {
            OutlandBushCount++;
            PlanOutland(new OutlandPlant(p, size, OutlandKind.Bush, type, 0));
        }
        // Dense groves with open meadows between them.
        for (int grove = 0; grove < Scaled(14); grove++)
        {
            var center = new Vector2(rng.RandfRange(-half, half), rng.RandfRange(-half, half));
            for (int tree = 0; tree < 180; tree++)
            {
                float a = rng.RandfRange(0, Mathf.Tau),
                    r = Mathf.Sqrt(rng.Randf()) * 43;
                var p = terrain.At(center.X + Mathf.Cos(a) * r, center.Y + Mathf.Sin(a) * r);
                if (Free(p, 7, 10))
                    PlantTree(p, rng.RandfRange(1, 1.8f), TreeTypeAt(p));
            }
        }
        // Trees in the meadows, thick where the old valley's woods run on and thin elsewhere.
        for (int i = 0; i < Scaled(6500); i++)
        {
            var p = Anywhere();
            if (!Free(p, 7, 10))
                continue;
            bool forest =
                Mathf.Sin(p.X * .018f + WorldLayout.ForestPhase) * Mathf.Cos(p.Z * .015f - WorldLayout.ForestPhase)
                > -.05f;
            if (!forest && rng.Randf() < .85f)
                continue;
            PlantTree(p, rng.RandfRange(.8f, 1.45f), TreeTypeAt(p));
        }
        // Small groups along the roads, and undergrowth beside them.
        var samples = outlandRoads.Samples;
        for (int i = 0; i + 1 < samples.Count; i += 23)
        {
            var tangent = samples[i + 1] - samples[i];
            if (tangent.Length() < .1f || tangent.Length() > 3)
                continue;
            var side = tangent.Normalized().Cross(Vector3.Up) * (i % 2 == 0 ? 1 : -1);
            var verge = samples[i] + side * rng.RandfRange(10, 15);
            var p = terrain.At(verge.X, verge.Z);
            if (Free(p, 9, 8))
                PlantTree(p, rng.RandfRange(.95f, 1.3f), TreeTypeAt(p));
        }
        for (int i = 0; i < Scaled(2100); i++)
        {
            var p = Anywhere();
            if (!Free(p, 5.4f, 8) || NearTrunk(p, 1.2f))
                continue;
            PlanBush(p, rng.RandfRange(.65f, 1.35f), i % 3);
            if (i % 3 != 0)
                continue;
            var neighbor = terrain.At(p.X + rng.RandfRange(1.8f, 3), p.Z + rng.RandfRange(-2, 2));
            if (Free(neighbor, 5.4f, 8) && !NearTrunk(neighbor, 1.2f))
                PlanBush(neighbor, rng.RandfRange(.5f, .85f), i % 3);
        }
        for (int i = 0; i < samples.Count; i += 11)
        {
            var road = samples[i];
            var p = terrain.At(road.X + rng.RandfRange(-10, 10), road.Z + rng.RandfRange(-10, 10));
            if (Free(p, 5.4f, 8) && !NearTrunk(p, 1.2f))
                PlanBush(p, rng.RandfRange(.6f, 1.1f), i % 3);
        }
        for (int i = 0; i < Scaled(9000); i++)
        {
            var p = Anywhere();
            if (Free(p, 4.2f, 6) && !NearTrunk(p, 1.2f))
                PlanOutland(new OutlandPlant(p, 1, OutlandKind.Flower, 0, i));
        }
        outlandPlanning = false;
        outlandPass = false;
        AddChild(OutlandPlants);
        BuildDistantTrees();
        GD.Print(
            $"OUTLAND_VEGETATION_READY trees={OutlandTreeCount} bushes={OutlandBushCount} blocks={outlandPlan.Count}"
        );
    }

    /// <summary>
    /// The full plants are built in every block whose nearest point is this near the car: from there on its trees are
    /// drawn in full rather than as one simple shape each (<see cref="OutlandDetailRange"/>).
    /// </summary>
    private const float OutlandBuildNow = OutlandDetailRange + 8;

    /// <summary>Blocks this near are built ahead of time, a few milliseconds of every frame.</summary>
    private const float OutlandBuildAhead = OutlandDetailRange + 100;

    private const double OutlandFrameBudgetMs = 3;
    private Vector3 outlandFocus = new(float.MaxValue, 0, float.MaxValue);
    private readonly List<Vector2I> outlandPending = new();

    /// <summary>
    /// Builds the blocks of plants beyond the old valley that the car at <paramref name="focus"/> comes near: those it
    /// could see in full at once, the rest of those ahead within the time of a frame.
    /// </summary>
    internal void StreamOutland(Vector3 focus)
    {
        if (outlandPlan.Count == 0)
            return;
        if (FlatMath.Length(focus.X - outlandFocus.X, focus.Z - outlandFocus.Z) >= 8)
        {
            outlandFocus = focus;
            outlandPending.Clear();
            outlandPending.AddRange(
                outlandPlan
                    .Keys.Where(block => BlockDistance(block, focus) < OutlandBuildAhead)
                    .OrderBy(block => BlockDistance(block, focus))
            );
        }
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        while (outlandPending.Count > 0)
        {
            var block = outlandPending[0];
            double spent =
                (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (BlockDistance(block, focus) >= OutlandBuildNow && spent > OutlandFrameBudgetMs)
                break;
            outlandPending.RemoveAt(0);
            BuildOutlandBlock(block);
        }
    }

    /// <summary>Builds at once every block of plants beyond the old valley near <paramref name="focus"/>.</summary>
    internal void BuildOutlandAround(Vector3 focus)
    {
        foreach (var block in outlandPlan.Keys.Where(block => BlockDistance(block, focus) < OutlandBuildAhead).ToList())
            BuildOutlandBlock(block);
    }

    /// <summary>How far <paramref name="p"/> is from the nearest point of a block.</summary>
    private static float BlockDistance(Vector2I block, Vector3 p)
    {
        float dx = Mathf.Max(0, Mathf.Max(block.X * 64 - p.X, p.X - (block.X + 1) * 64));
        float dz = Mathf.Max(0, Mathf.Max(block.Y * 64 - p.Z, p.Z - (block.Y + 1) * 64));
        return FlatMath.Length(dx, dz);
    }

    private void BuildOutlandBlock(Vector2I block)
    {
        if (!outlandPlan.Remove(block, out var plants))
            return;
        outlandBuilt.Add(block);
        outlandPass = true;
        outlandBuildGroup++;
        ulong seed =
            (ulong)Settings.Seed * 2654435761UL ^ (ulong)(uint)block.X * 73856093UL ^ (ulong)(uint)block.Y * 19349663UL;
        rng = new RandomNumberGenerator { Seed = seed };
        detailRng = new RandomNumberGenerator { Seed = seed + 1 };
        foreach (var plant in plants)
        {
            if (OutlandCleared?.Invoke(plant.Foot) == true)
                continue;
            switch (plant.Kind)
            {
                case OutlandKind.Tree:
                    BuildTree(plant.Foot, plant.Size, plant.Type, plant.Number);
                    break;
                case OutlandKind.Bush:
                    Bush(plant.Foot, plant.Size, plant.Type);
                    break;
                default:
                    Flower(plant.Foot, plant.Number);
                    break;
            }
        }
        BuildBatches();
        outlandPass = false;
    }

    /// <summary>The trees beyond the old valley as they were planted, for their shapes far away.</summary>
    private readonly List<(Vector3 Foot, float Size, int Type)> outlandTrees = new();

    /// <summary>
    /// Beyond this distance a block of plants beyond the old valley shows its trees as one simple shape each and its
    /// bushes and rocks no longer: there are four times as many of them as in the old valley, and from the old valley
    /// nearly all of them are this far.
    /// </summary>
    private const float OutlandDetailRange = 300;

    /// <summary>
    /// Each tree beyond the old valley once more, as one cone or one crown of a few faces like the distant border forest,
    /// in the same 64 m blocks as its parts, so that a block shows its trees one way at a time.
    /// </summary>
    private void BuildDistantTrees()
    {
        var random = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + 7752) };
        var blocks = new Dictionary<(Vector2I Block, bool Conifer), List<(Transform3D Transform, Color Tint)>>();
        foreach (var (foot, size, type) in outlandTrees)
        {
            bool conifer = type == 2;
            var key = (BlockOf(foot), conifer);
            if (!blocks.TryGetValue(key, out var list))
                blocks[key] = list = new();
            var basis = new Basis(Vector3.Up, random.RandfRange(0, Mathf.Tau)).Scaled(
                Vector3.One * size * (conifer ? .6f : .78f)
            );
            float shade = random.RandfRange(.86f, 1.06f);
            var tint = type switch
            {
                3 => new Color(1.12f, 1.14f, .86f),
                4 => new Color(1.08f, .96f, .92f),
                5 => new Color(.95f, 1.08f, .84f),
                _ => new Color(1, 1, 1),
            };
            if (OutlandCleared?.Invoke(foot) != true)
                list.Add((new Transform3D(basis, foot - Vector3.Up * .3f), tint * shade));
        }
        var material = BorderForest.TreeMaterial();
        var meshes = new Dictionary<bool, Mesh>
        {
            [true] = BorderForest.DistantConifer(),
            [false] = BorderForest.DistantBroadleaf(),
        };
        foreach (var (key, trees) in blocks)
        {
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                UseColors = true,
                Mesh = meshes[key.Conifer],
                InstanceCount = trees.Count,
            };
            for (int i = 0; i < trees.Count; i++)
            {
                multimesh.SetInstanceTransform(i, trees[i].Transform);
                multimesh.SetInstanceColor(i, trees[i].Tint with { A = 1 });
            }
            OutlandPlants.AddChild(
                new MultiMeshInstance3D
                {
                    Name = "DistantOutlandTrees",
                    Multimesh = multimesh,
                    MaterialOverride = material,
                    VisibilityRangeBegin = OutlandDetailRange,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
        }
    }

    /// <summary>The kind of tree beyond the old valley: willows by the river, conifers under the mountains.</summary>
    private int OutlandTreeTypeAt(Vector3 p)
    {
        if (River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 28)
            return 5;
        if (p.Z < -870 + 40 * Mathf.Sin(p.X * .011f + WorldLayout.ForestPhase))
            return 2;
        return NearestType(outlandRegions, new Vector2(p.X, p.Z));
    }
}
