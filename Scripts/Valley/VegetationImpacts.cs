using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;

namespace MathRacer.Valley;

public partial class Vegetation
{
    /// <summary>
    /// Instances of a tree in one batch: a tree is planted whole before the next plant, so its parts in a batch follow
    /// one another.
    /// </summary>
    private readonly record struct TreeRun(BatchKey Batch, int First, int Count);

    private sealed class TreeVisual(Vector3 foot, float size)
    {
        internal readonly Vector3 Foot = foot;
        internal readonly float Size = size;

        /// <summary>
        /// Where its parts are; their resting places are read back from the batches only while it shakes (<see
        /// cref="Rest"/>), since keeping them for some twenty thousand trees took a quarter of a gigabyte.
        /// </summary>
        internal readonly List<TreeRun> Runs = new();
        internal Transform3D[]? Rest;
        internal readonly List<(Vector3 Center, Vector3 Size, string Color)> Crowns = new();
        internal Vector3 Axis;
        internal float Age,
            Strength;
    }

    private sealed class FallingLeaf(MeshInstance3D mesh, Vector3 velocity, Vector3 spin, float phase)
    {
        internal readonly MeshInstance3D Mesh = mesh;
        internal Vector3 Velocity = velocity;
        internal readonly Vector3 Spin = spin;
        internal readonly float Phase = phase;
        internal float Age;
    }

    private TreeVisual? buildingTree;
    private readonly Dictionary<ulong, TreeVisual> treeVisuals = new();
    private readonly Dictionary<BatchKey, MultiMesh> renderedBatches = new();

    /// <summary>The simpler copies of the batches for the far blocks (Vegetation.FarDistance), moved with them.</summary>
    private readonly Dictionary<BatchKey, MultiMesh> farBatches = new();
    private readonly List<TreeVisual> shakingTrees = new();
    private readonly List<FallingLeaf> fallingLeaves = new();
    private readonly RandomNumberGenerator impactRng = new() { Seed = (ulong)Settings.Seed + 902 };
    internal int ShakingTreeCount => shakingTrees.Count;
    internal int FallingLeafCount => fallingLeaves.Count;

    internal bool TreeBatchesShareWindRoots()
    {
        if (treeVisuals.Count == 0)
            return false;
        bool rendered = DisplayServer.GetName() != "headless";
        foreach (var tree in System.Linq.Enumerable.Take(treeVisuals.Values, 24))
        foreach (var run in tree.Runs)
        {
            var batch = renderedBatches[run.Batch];
            if (!batch.UseCustomData)
                return false;
            if (!rendered)
                continue;
            for (int index = run.First; index < run.First + run.Count; index++)
            {
                var wind = batch.GetInstanceCustomData(index);
                if (
                    new Vector3(wind.R, wind.G, wind.B).DistanceTo(tree.Foot) > .5f
                    || Mathf.Abs(wind.A - tree.Size) > .01f
                )
                    return false;
            }
        }
        return true;
    }

    internal void HitTree(StaticBody3D body, Vector3 movement, Vector3 normal)
    {
        float speed = -movement.Dot(normal);
        if (speed < 1.2f || !treeVisuals.TryGetValue(body.GetInstanceId(), out var tree) || shakingTrees.Contains(tree))
            return;
        tree.Axis = Vector3.Up.Cross(-normal).Normalized();
        tree.Age = 0;
        tree.Strength = Mathf.Lerp(.025f, .085f, Mathf.Clamp(speed / 14, 0, 1));
        tree.Rest = RestOf(tree);
        shakingTrees.Add(tree);
        // Bound particles even if a player strikes several neighboring trunks in succession.
        int count = impactRng.RandiRange(5, 9);
        for (int i = 0; i < count && fallingLeaves.Count < 96 && tree.Crowns.Count > 0; i++)
        {
            var crown = tree.Crowns[impactRng.RandiRange(0, tree.Crowns.Count - 1)];
            var offset =
                new Vector3(impactRng.RandfRange(-.65f, .65f), -.35f, impactRng.RandfRange(-.65f, .65f)) * crown.Size;
            float size = impactRng.RandfRange(.14f, .23f) * tree.Size;
            var mesh = Art.Ball(this, crown.Center + offset, new Vector3(size, size * .13f, size * .48f), crown.Color);
            mesh.Rotation = new Vector3(impactRng.RandfRange(-1, 1), impactRng.RandfRange(0, Mathf.Tau), .4f);
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            fallingLeaves.Add(
                new FallingLeaf(
                    mesh,
                    -normal * speed * .07f
                        + new Vector3(impactRng.RandfRange(-.5f, .5f), .1f, impactRng.RandfRange(-.5f, .5f)),
                    new Vector3(impactRng.RandfRange(1, 3), impactRng.RandfRange(-2, 2), impactRng.RandfRange(1, 3)),
                    impactRng.RandfRange(0, Mathf.Tau)
                )
            );
        }
    }

    /// <summary>A headless run keeps no instance data, so there a tree shakes without its parts being moved.</summary>
    private static readonly bool PartsDrawn = DisplayServer.GetName() != "headless";

    /// <summary>The parts of a tree where they rest, read from its batches when it begins to shake.</summary>
    private Transform3D[]? RestOf(TreeVisual tree)
    {
        if (!PartsDrawn)
            return null;
        int count = 0;
        foreach (var run in tree.Runs)
            count += run.Count;
        var rest = new Transform3D[count];
        int at = 0;
        foreach (var run in tree.Runs)
        {
            var batch = renderedBatches[run.Batch];
            for (int index = run.First; index < run.First + run.Count; index++)
                rest[at++] = batch.GetInstanceTransform(index);
        }
        return rest;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (GetParent()?.GetParent() is Game game && game.State != GameState.Exploring)
            return;
        AdvanceImpacts((float)delta);
    }

    internal void AdvanceImpacts(float dt)
    {
        for (int i = shakingTrees.Count - 1; i >= 0; i--)
        {
            var tree = shakingTrees[i];
            tree.Age += dt;
            bool finished = tree.Age >= 1.6f;
            float angle = finished ? 0 : Mathf.Sin(tree.Age * 19) * Mathf.Exp(-tree.Age * 2.8f) * tree.Strength;
            var rotation = new Basis(tree.Axis, angle);
            if (tree.Rest is { } rest)
            {
                int at = 0;
                foreach (var run in tree.Runs)
                {
                    var near = renderedBatches[run.Batch];
                    var far = farBatches.GetValueOrDefault(run.Batch);
                    for (int index = run.First; index < run.First + run.Count; index++, at++)
                    {
                        var transform = finished
                            ? rest[at]
                            : new Transform3D(
                                rotation * rest[at].Basis,
                                tree.Foot + rotation * (rest[at].Origin - tree.Foot)
                            );
                        near.SetInstanceTransform(index, transform);
                        far?.SetInstanceTransform(index, transform);
                    }
                }
            }
            if (finished)
            {
                tree.Rest = null;
                shakingTrees.RemoveAt(i);
            }
        }
        for (int i = fallingLeaves.Count - 1; i >= 0; i--)
        {
            var leaf = fallingLeaves[i];
            leaf.Age += dt;
            var p = leaf.Mesh.Position;
            float floor = ground.Height(p.X, p.Z) + .04f;
            if (p.Y > floor)
            {
                leaf.Velocity.Y = Mathf.Max(-1.7f, leaf.Velocity.Y - 2.4f * dt);
                p +=
                    (
                        leaf.Velocity
                        + new Vector3(
                            Mathf.Sin(leaf.Age * 5 + leaf.Phase) * .65f,
                            0,
                            Mathf.Cos(leaf.Age * 4 + leaf.Phase) * .4f
                        )
                    ) * dt;
                p.Y = Mathf.Max(p.Y, ground.Height(p.X, p.Z) + .04f);
                leaf.Mesh.Position = p;
                leaf.Mesh.Rotation += leaf.Spin * dt;
            }
            if (leaf.Age > 5)
                leaf.Mesh.Scale *= Mathf.Exp(-4 * dt);
            if (leaf.Age < 6)
                continue;
            leaf.Mesh.QueueFree();
            fallingLeaves.RemoveAt(i);
        }
    }
}
