using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Valley;

namespace MathRacer.Settlement;

/// <summary>One lamp that stands on the ground: its kind (<see cref="GlassLamps"/>), where and which way it faces.</summary>
internal readonly record struct LampSpot(string Kind, float X, float Z, float Yaw);

/// <summary>
/// The lamps the player carries and the lamps that stand on the ground, wherever the player put them. A lamp is taken
/// from the glass lamp maker into the bag, put down beside the car and taken up again; each kind that is used a second
/// time is simply drawn again. At night every lamp shines, and the few that are nearest to the car also light the
/// ground (a game with the compatibility renderer can only light so many things at once).
/// </summary>
public partial class LampField : Node3D
{
    internal const int MaxCarried = 8;
    internal const int MaxPlaced = 30;

    /// <summary>The nearest lamps that get a real light by night; the rest only shine and have a halo.</summary>
    private const int LitLamps = 5;

    private const float LightRange = 80;

    /// <summary>The darkness from which the lamps shine (the windows of the village come on a little later).</summary>
    private const float LampDarkness = .3f;

    private sealed class Lamp
    {
        public required string Kind;
        public required Node3D Node;
        public required Node3D Glow;
        public required OmniLight3D? Light;
        public float X,
            Z,
            Yaw;
    }

    private readonly List<Lamp> placed = new();
    private readonly List<string> carried = new();
    private readonly List<Lamp> displays = new();
    private float darkness;
    private bool glowing;

    /// <summary>The height of the ground (and of a bridge) at a point; the world supplies it.</summary>
    internal Func<float, float, float> Surface { get; set; } = (_, _) => 0;

    /// <summary>The kinds in the bag, in the order they were taken; the first is the next to be put down.</summary>
    internal IReadOnlyList<string> Carried => carried;

    internal int PlacedCount => placed.Count;

    internal IReadOnlyList<LampSpot> Spots =>
        placed.Select(lamp => new LampSpot(lamp.Kind, lamp.X, lamp.Z, lamp.Yaw)).ToArray();

    internal Node3D NodeAt(int index) => placed[index].Node;

    internal string KindAt(int index) => placed[index].Kind;

    internal Vector3 PositionAt(int index) => placed[index].Node.GlobalPosition;

    /// <summary>Puts a lamp of this kind in the bag; false for an unknown kind or a full bag.</summary>
    internal bool TryTake(string kind)
    {
        if (GlassLamps.Find(kind) == null || carried.Count >= MaxCarried)
            return false;
        carried.Add(kind);
        return true;
    }

    /// <summary>Whether a lamp can stand here: on land (a bridge is land), inside the valley and clear of another lamp.</summary>
    internal bool CanStandAt(float x, float z)
    {
        if (!ValleyBounds.Contains(x, z, 6))
            return false;
        if (River.Distance(x, z) < River.HalfWidth(z) + .5f && !River.OnBridge(x, z))
            return false;
        return placed.All(lamp => new Vector2(lamp.X - x, lamp.Z - z).Length() > .9f);
    }

    /// <summary>
    /// Takes the first lamp out of the bag and stands it at this point. False when the bag is empty, the valley already
    /// holds <see cref="MaxPlaced"/> lamps or the spot is not free.
    /// </summary>
    internal bool PutDown(float x, float z, float yaw)
    {
        if (carried.Count == 0 || placed.Count >= MaxPlaced || !CanStandAt(x, z))
            return false;
        string kind = carried[0];
        carried.RemoveAt(0);
        Stand(kind, x, z, yaw);
        return true;
    }

    /// <summary>The nearest lamp within <paramref name="range"/> of a point (flat distance), or -1.</summary>
    internal int NearestIndex(Vector3 point, float range)
    {
        int best = -1;
        float distance = range * range;
        for (int i = 0; i < placed.Count; i++)
        {
            float d = new Vector2(placed[i].X - point.X, placed[i].Z - point.Z).LengthSquared();
            if (d <= distance)
            {
                distance = d;
                best = i;
            }
        }
        return best;
    }

    /// <summary>Takes a standing lamp up into the bag; false when the bag is full or there is no such lamp.</summary>
    internal bool PickUp(int index)
    {
        if (index < 0 || index >= placed.Count || carried.Count >= MaxCarried)
            return false;
        var lamp = placed[index];
        placed.RemoveAt(index);
        carried.Add(lamp.Kind);
        Discard(lamp);
        return true;
    }

    /// <summary>
    /// Sets the bag and the standing lamps to exactly these. Unknown kinds and surplus are ignored, so that a damaged
    /// list can never grow the field beyond its limits.
    /// </summary>
    internal void Restore(IEnumerable<string>? bag, IEnumerable<LampSpot>? spots)
    {
        foreach (var lamp in placed.ToArray())
            Discard(lamp);
        placed.Clear();
        carried.Clear();
        foreach (string kind in bag ?? Enumerable.Empty<string>())
            if (GlassLamps.Find(kind) != null && carried.Count < MaxCarried)
                carried.Add(kind);
        foreach (var spot in spots ?? Enumerable.Empty<LampSpot>())
            if (GlassLamps.Find(spot.Kind) != null && placed.Count < MaxPlaced)
                Stand(spot.Kind, spot.X, spot.Z, spot.Yaw);
        glowing = false;
        ApplyGlow(true);
    }

    /// <summary>A lamp that stands on the maker's table: it is never taken and shines at night like the others.</summary>
    internal void AdoptDisplay(Node3D node, Node3D glow) =>
        displays.Add(
            new Lamp
            {
                Kind = node.Name,
                Node = node,
                Glow = glow,
                Light = glow.GetNodeOrNull<OmniLight3D>("LampLight"),
            }
        );

    /// <summary>The lamps at a given darkness (0 by day, 1 at night) with the car, or the camera, at <paramref name="focus"/>.</summary>
    internal void SetDarkness(float value, Vector3 focus)
    {
        darkness = value;
        ApplyGlow(false, focus);
    }

    /// <summary>Whether the lamps shine at all at the present darkness.</summary>
    internal bool IsGlowing => glowing;

    /// <summary>The lamps, standing and on a table, that have a light switched on at the moment.</summary>
    internal int LitCount => placed.Concat(displays).Count(lamp => lamp.Light is { Visible: true });

    private void Stand(string kind, float x, float z, float yaw)
    {
        var definition = GlassLamps.Find(kind)!;
        var node = new Node3D { Name = "Lamp_" + kind, Rotation = new Vector3(0, yaw, 0) };
        AddChild(node);
        node.GlobalPosition = new Vector3(x, Surface(x, z) + .02f, z);
        var glow = definition.Build(node);
        glow.Visible = glowing;
        placed.Add(
            new Lamp
            {
                Kind = kind,
                Node = node,
                Glow = glow,
                Light = glow.GetNodeOrNull<OmniLight3D>("LampLight"),
                X = x,
                Z = z,
                Yaw = yaw,
            }
        );
    }

    private void Discard(Lamp lamp)
    {
        if (lamp.Node.GetParent() != null)
            lamp.Node.GetParent().RemoveChild(lamp.Node);
        lamp.Node.QueueFree();
    }

    private void ApplyGlow(bool force, Vector3? focus = null)
    {
        bool on = darkness > LampDarkness;
        bool changed = on != glowing || force;
        if (changed)
        {
            glowing = on;
            foreach (var lamp in placed.Concat(displays))
                lamp.Glow.Visible = on;
        }
        // By day nothing moves: the lights only need to be put out once.
        if (!on && !changed)
            return;
        float energy = Mathf.Clamp((darkness - .25f) / .55f, 0, 1) * 1.8f;
        var near = on
            ? placed
                .Concat(displays)
                .Where(lamp => lamp.Light != null)
                .Select(lamp => (lamp, d: lamp.Node.GlobalPosition.DistanceTo(focus ?? Vector3.Zero)))
                .Where(pair => pair.d < LightRange)
                .OrderBy(pair => pair.d)
                .Take(LitLamps)
                .Select(pair => pair.lamp)
                .ToHashSet()
            : new HashSet<Lamp>();
        foreach (var lamp in placed.Concat(displays))
        {
            if (lamp.Light == null)
                continue;
            bool lit = near.Contains(lamp);
            if (lamp.Light.Visible != lit)
                lamp.Light.Visible = lit;
            if (lit)
                lamp.Light.LightEnergy = energy;
        }
    }
}
