using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer;

/// <summary>
/// Spinning coins scattered at random along the roads. A collected coin reappears at another random
/// road spot after a while, so the valley never runs out. Positions are not saved.
/// </summary>
public partial class WorldCoins : Node3D
{
    public const int Count = 45;
    internal const float PickupRadius = 2.6f;
    private const float RespawnTime = 40;
    private const float MinSpacing = 14;
    private const float RespawnDistance = 45;
    private const float StartClearance = 20;
    private readonly Random random = new();
    private readonly List<Coin> coins = new();
    private World world = null!;
    private float phase;

    private sealed class Coin
    {
        public Node3D Node = null!;
        public Node3D Spinner = null!;
        public float Respawn;
        public bool Active => Respawn <= 0;
    }

    /// <summary>Diagnostics switch pickups off so that their exact coin totals stay predictable.</summary>
    internal bool PickupEnabled { get; set; } = true;
    internal int ActiveCount => coins.FindAll(c => c.Active).Count;
    internal IEnumerable<Vector3> ActivePositions
    {
        get
        {
            foreach (var coin in coins)
                if (coin.Active)
                    yield return coin.Node.Position;
        }
    }

    public void Generate(World world, Vector3 start)
    {
        this.world = world;
        Name = "WorldCoins";
        // A soft glow keeps the gold readable in shade and against the sandy road.
        var gold = Gold("ffcf3a", .45f);
        var emboss = Gold("fff0a0", .6f);
        for (int i = 0; i < Count; i++)
        {
            var node = new Node3D { Name = "Coin" + i };
            AddChild(node);
            var spinner = new Node3D { Position = Vector3.Up * 1.1f };
            node.AddChild(spinner);
            var face = Art.Cylinder(spinner, Vector3.Zero, .6f, .6f, .12f, "ffcf3a");
            face.MaterialOverride = gold;
            face.RotationDegrees = new Vector3(90, 0, 0);
            face.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var center = Art.Cylinder(spinner, Vector3.Zero, .3f, .3f, .16f, "fff0a0");
            center.MaterialOverride = emboss;
            center.RotationDegrees = new Vector3(90, 0, 0);
            center.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            var coin = new Coin { Node = node, Spinner = spinner };
            coins.Add(coin);
            Place(coin, start, StartClearance);
        }
    }

    /// <summary>Collects every visible coin within reach of the car and returns how many were found.</summary>
    public int Collect(Vector3 car)
    {
        int found = 0;
        foreach (var coin in coins)
        {
            if (!PickupEnabled || !coin.Active || Flat(coin.Node.Position).DistanceTo(Flat(car)) > PickupRadius)
                continue;
            found++;
            coin.Respawn = RespawnTime;
            coin.Node.Hide();
        }
        return found;
    }

    /// <summary>Spins the coins; while the game runs it also returns collected coins at new spots.</summary>
    public void Animate(float delta, bool running, Vector3 car)
    {
        if (!running)
            return;
        phase = (phase + delta * 2.4f) % Mathf.Tau;
        foreach (var coin in coins)
        {
            if (coin.Active)
            {
                coin.Spinner.Rotation = new Vector3(0, phase, 0);
                coin.Spinner.Position = Vector3.Up * (1.1f + .18f * Mathf.Sin(phase * 1.5f));
                continue;
            }
            coin.Respawn -= delta;
            if (coin.Respawn <= 0)
                Place(coin, car, RespawnDistance);
        }
    }

    private static Vector2 Flat(Vector3 p) => new(p.X, p.Z);

    private static StandardMaterial3D Gold(string color, float glow) =>
        new()
        {
            AlbedoColor = new Color(color).SrgbToLinear(),
            Metallic = .35f,
            Roughness = .35f,
            EmissionEnabled = true,
            Emission = new Color(color).SrgbToLinear(),
            EmissionEnergyMultiplier = glow,
        };

    private void Place(Coin coin, Vector3 avoid, float clearance)
    {
        var samples = world.Routes.Samples;
        Vector3 spot = samples[random.Next(samples.Count)];
        for (int attempt = 0; attempt < 60; attempt++)
        {
            var candidate = samples[random.Next(samples.Count)];
            if (candidate.DistanceTo(avoid) < clearance || candidate.DistanceTo(world.Village.GarageEntrance) < 12)
                continue;
            if (world.Village.IsReserved(candidate))
                continue;
            bool crowded = false;
            foreach (var other in coins)
                if (other != coin && other.Active && other.Node.Position.DistanceTo(candidate) < MinSpacing)
                {
                    crowded = true;
                    break;
                }
            spot = candidate;
            if (!crowded)
                break;
        }
        float y = world.Routes.SurfaceHeight(spot, world.Ground.Height(spot.X, spot.Z));
        if (River.OnBridge(spot.X, spot.Z))
            y = Mathf.Max(y, River.BridgeSurfaceHeight(spot.X));
        coin.Node.Position = new Vector3(spot.X, y, spot.Z);
        coin.Respawn = 0;
        coin.Node.Show();
    }
}
