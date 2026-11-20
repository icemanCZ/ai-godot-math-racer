using Godot;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// Little things in the air around the car: pastel butterflies that wander and flap over the meadow on a fine day, and
/// leaves that tumble past on the wind. They are scenery only and follow the car: one that is left far behind comes
/// back somewhere ahead. Butterflies hide in the rain and at night, when the fireflies come out; leaves fly only when the
/// wind blows.
/// </summary>
internal sealed partial class Flutters : Node3D
{
    private const int ButterflyCount = 12;
    private const int LeafCount = 28;
    private const float ButterflyRange = 26;

    /// <summary>Butterflies are drawn larger than life, so a child sees them from the car.</summary>
    private const float ButterflySize = 1.8f;

    private const float LeafSize = 1.6f;
    private const float LeafRange = 18;

    private sealed class Butterfly
    {
        public Node3D Root = null!;
        public Node3D Left = null!,
            Right = null!;
        public Vector3 Target;
        public float Phase,
            Speed,
            Retarget;
    }

    private sealed class Leaf
    {
        public Node3D Root = null!;
        public Vector3 Spin;
        public float Drift;
    }

    private readonly Butterfly[] butterflies = new Butterfly[ButterflyCount];
    private readonly Leaf[] leaves = new Leaf[LeafCount];
    private readonly System.Random random = new();
    private Terrain ground = null!;
    private float butterflyShow,
        leafShow,
        time;

    private static readonly string[] WingColors = { "ffa6c9", "ffe27a", "c7a6ff", "9fd8ff", "ffb877", "b7f0a6" };
    private static readonly string[] LeafColors = { "7cc35e", "a8d05a", "f2c04e", "ee9b47", "d9734a" };

    internal void Setup(Terrain terrain)
    {
        ground = terrain;
        for (int i = 0; i < ButterflyCount; i++)
            butterflies[i] = NewButterfly(WingColors[i % WingColors.Length]);
        for (int i = 0; i < LeafCount; i++)
            leaves[i] = NewLeaf(LeafColors[i % LeafColors.Length]);
        Visible = false;
    }

    /// <summary>
    /// Moves everything one frame on around <paramref name="focus"/> (the car). <paramref name="calm"/> is 1 on a fine
    /// day and 0 in the rain or at night; <paramref name="wind"/> is the wind on the ground, in metres a second.
    /// </summary>
    internal void Tick(float dt, Vector3 focus, float calm, Vector2 wind)
    {
        time += dt;
        butterflyShow = Mathf.MoveToward(butterflyShow, calm, dt * .5f);
        leafShow = Mathf.MoveToward(leafShow, Mathf.Clamp(wind.Length() / 1.1f, 0, 1), dt * .4f);
        Visible = butterflyShow > 0 || leafShow > 0;
        if (!Visible)
            return;
        foreach (var butterfly in butterflies)
            MoveButterfly(butterfly, dt, focus);
        foreach (var leaf in leaves)
            MoveLeaf(leaf, dt, focus, wind);
    }

    private void MoveButterfly(Butterfly b, float dt, Vector3 focus)
    {
        b.Root.Visible = butterflyShow > 0;
        if (!b.Root.Visible)
            return;
        var flat = new Vector2(b.Root.Position.X - focus.X, b.Root.Position.Z - focus.Z);
        if (flat.Length() > ButterflyRange)
        {
            b.Root.Position = Around(focus, 5, ButterflyRange - 4, .6f, 1.8f);
            b.Target = b.Root.Position;
        }
        b.Retarget -= dt;
        if (b.Retarget <= 0 || b.Root.Position.DistanceTo(b.Target) < .3f)
        {
            // A new flower to visit, close by and low over the grass.
            var p = b.Root.Position;
            float angle = (float)random.NextDouble() * Mathf.Tau;
            float reach = 1.5f + (float)random.NextDouble() * 4;
            var next = new Vector3(p.X + Mathf.Cos(angle) * reach, 0, p.Z + Mathf.Sin(angle) * reach);
            next.Y = ground.Height(next.X, next.Z) + .4f + (float)random.NextDouble() * 1.4f;
            b.Target = next;
            b.Retarget = 2 + (float)random.NextDouble() * 3;
        }
        var toward = b.Target - b.Root.Position;
        var step = toward.LimitLength(b.Speed * dt);
        // A fluttering flight: bobbing up and down as the wings beat.
        float bob = Mathf.Sin(time * 7 + b.Phase) * .25f * dt * 6;
        b.Root.Position += step + Vector3.Up * bob;
        float floor = ground.Height(b.Root.Position.X, b.Root.Position.Z) + .25f;
        if (b.Root.Position.Y < floor)
            b.Root.Position = b.Root.Position with { Y = floor };
        if (step.LengthSquared() > .00001f)
            b.Root.Rotation = new Vector3(0, Mathf.Atan2(step.X, step.Z), 0);
        float flap = Mathf.Sin(time * 22 + b.Phase) * .9f + .25f;
        b.Left.Rotation = new Vector3(0, 0, flap);
        b.Right.Rotation = new Vector3(0, 0, -flap);
        b.Root.Scale = Vector3.One * Mathf.Max(.001f, butterflyShow * ButterflySize);
    }

    private void MoveLeaf(Leaf leaf, float dt, Vector3 focus, Vector2 wind)
    {
        leaf.Root.Visible = leafShow > 0;
        if (!leaf.Root.Visible)
            return;
        var p = leaf.Root.Position;
        float floor = ground.Height(p.X, p.Z);
        var flat = new Vector2(p.X - focus.X, p.Z - focus.Z);
        if (flat.Length() > LeafRange || p.Y < floor + .05f)
        {
            // It comes back upwind of the car, high in the air, to blow past again.
            var upwind = wind.LengthSquared() > .01f ? -wind.Normalized() : Vector2.Right;
            var side = new Vector2(-upwind.Y, upwind.X) * ((float)random.NextDouble() * 2 - 1) * LeafRange * .8f;
            var start = new Vector2(focus.X, focus.Z) + upwind * LeafRange * .8f + side;
            p = new Vector3(start.X, ground.Height(start.X, start.Y) + 2 + (float)random.NextDouble() * 4, start.Y);
        }
        float swirl = Mathf.Sin(time * 2.3f + leaf.Drift * 7);
        var velocity = new Vector3(wind.X * 3.2f, -.55f + swirl * .45f, wind.Y * 3.2f);
        velocity += new Vector3(Mathf.Cos(time * 1.7f + leaf.Drift), 0, Mathf.Sin(time * 1.3f + leaf.Drift)) * .6f;
        leaf.Root.Position = p + velocity * dt;
        leaf.Root.Rotation += leaf.Spin * dt;
        leaf.Root.Scale = Vector3.One * Mathf.Max(.001f, leafShow * LeafSize);
    }

    /// <summary>A point between <paramref name="near"/> and <paramref name="far"/> metres from the car, in the air.</summary>
    private Vector3 Around(Vector3 focus, float near, float far, float low, float high)
    {
        float angle = (float)random.NextDouble() * Mathf.Tau;
        float distance = near + (float)random.NextDouble() * (far - near);
        float x = focus.X + Mathf.Cos(angle) * distance,
            z = focus.Z + Mathf.Sin(angle) * distance;
        return new Vector3(x, ground.Height(x, z) + low + (float)random.NextDouble() * (high - low), z);
    }

    private Butterfly NewButterfly(string color)
    {
        var root = new Node3D { Name = "Butterfly" };
        AddChild(root);
        Art.Ball(root, Vector3.Zero, new(.025f, .025f, .1f), "4a3b3b");
        var left = new Node3D { Position = new(-.02f, 0, 0) };
        var right = new Node3D { Position = new(.02f, 0, 0) };
        root.AddChild(left);
        root.AddChild(right);
        foreach (var (wing, side) in new[] { (left, -1f), (right, 1f) })
        {
            Art.Ball(wing, new(side * .1f, 0, .03f), new(.1f, .01f, .085f), color);
            Art.Ball(wing, new(side * .08f, 0, -.07f), new(.07f, .01f, .06f), color);
            Art.Ball(wing, new(side * .1f, .006f, .03f), new(.035f, .006f, .03f), "ffffff");
        }
        foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false))
            ((MeshInstance3D)node).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        var butterfly = new Butterfly
        {
            Root = root,
            Left = left,
            Right = right,
            Phase = (float)random.NextDouble() * 10,
            Speed = 1.1f + (float)random.NextDouble() * .8f,
        };
        // Far away to begin with, so the first frame puts it somewhere near the car.
        root.Position = new Vector3(100000, 0, 0);
        return butterfly;
    }

    private Leaf NewLeaf(string color)
    {
        var root = new Node3D { Name = "BlownLeaf" };
        AddChild(root);
        var blade = Art.Ball(root, Vector3.Zero, new(.09f, .012f, .055f), color);
        blade.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        root.Position = new Vector3(100000, -1000, 0);
        return new Leaf
        {
            Root = root,
            Spin = new Vector3(
                (float)random.NextDouble() * 6 - 3,
                (float)random.NextDouble() * 4 - 2,
                (float)random.NextDouble() * 6 - 3
            ),
            Drift = (float)random.NextDouble() * 10,
        };
    }
}
