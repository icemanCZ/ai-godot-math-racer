using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Valley;

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

    /// <summary>A collected coin comes back at least this far from the car, so that it is not seen to appear.</summary>
    private const float RespawnDistance = 120;

    private const float StartClearance = 20;

    /// <summary>How long a coin that comes back takes to grow from nothing to its full size.</summary>
    private const float GrowTime = 1.2f;

    /// <summary>
    /// A coin far away is a few pixels across and, turned edge on, hardly one, so it seemed to appear only close by. A
    /// golden glint over it keeps this many pixels across on screen beyond <see cref="GlintNear"/> metres.
    /// </summary>
    private const float GlintPixels = 9;

    private const float GlintNear = 35;
    private const float GlintFull = 70;
    private const float GlintFar = 320;
    private readonly Random random = new();
    private readonly List<Coin> coins = new();
    private World world = null!;
    private float phase;
    private float flash;

    private sealed class Coin
    {
        public Node3D Node = null!;
        public Node3D Spinner = null!;
        public MeshInstance3D Glint = null!;
        public float Respawn;
        public float Grow = 1;
        public Node3D? Beacon;
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
        var glintMesh = new QuadMesh { Size = Vector2.One, Material = GlintMaterial() };
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
            var glint = new MeshInstance3D
            {
                Name = "Glint",
                Mesh = glintMesh,
                Position = Vector3.Up * 1.1f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Visible = false,
            };
            node.AddChild(glint);
            var coin = new Coin
            {
                Node = node,
                Spinner = spinner,
                Glint = glint,
            };
            coins.Add(coin);
            Place(coin, start, StartClearance);
        }
    }

    /// <summary>Lights a tall beam over every coin for a while, so that they can be seen from far away.</summary>
    internal void Flash(float seconds) => flash = seconds;

    internal bool Flashing => flash > 0;

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
        flash = Mathf.Max(0, flash - delta);
        var camera = GetViewport().GetCamera3D();
        // The size of a metre at one metre from the camera, in pixels: what keeps the glint the same on screen.
        float pixelsPerMetre =
            camera == null
                ? 0
                : GetViewport().GetVisibleRect().Size.Y / (2 * Mathf.Tan(Mathf.DegToRad(camera.Fov) / 2));
        foreach (var coin in coins)
        {
            coin.Grow = Mathf.MoveToward(coin.Grow, 1, delta / GrowTime);
            coin.Spinner.Scale = Vector3.One * Mathf.Max(.001f, coin.Grow);
            Glint(coin, camera, pixelsPerMetre);
            if (flash > 0 && coin.Active)
            {
                coin.Beacon ??= Art.Beacon(coin.Node, "ffe36a");
                coin.Beacon.Visible = true;
            }
            else if (coin.Beacon != null)
                coin.Beacon.Visible = false;
            if (coin.Active)
            {
                coin.Spinner.Rotation = new Vector3(0, phase, 0);
                coin.Spinner.Position = Vector3.Up * (1.1f + .18f * Mathf.Sin(phase * 1.5f));
                continue;
            }
            coin.Respawn -= delta;
            if (coin.Respawn <= 0)
            {
                Place(coin, car, RespawnDistance);
                coin.Grow = 0;
            }
        }
    }

    /// <summary>Shows the glint of a far coin at a steady size on screen; near the car the coin itself is seen.</summary>
    private static void Glint(Coin coin, Camera3D? camera, float pixelsPerMetre)
    {
        float distance = camera == null ? 0 : camera.GlobalPosition.DistanceTo(coin.Glint.GlobalPosition);
        float show =
            Mathf.SmoothStep(GlintNear, GlintFull, distance)
            * (1 - Mathf.SmoothStep(GlintFar * .8f, GlintFar, distance));
        coin.Glint.Visible = coin.Active && pixelsPerMetre > 0 && show > 0;
        if (coin.Glint.Visible)
            coin.Glint.Scale = Vector3.One * (GlintPixels * distance / pixelsPerMetre * show * coin.Grow);
    }

    /// <summary>A soft round golden dot that always faces the camera and glows a little.</summary>
    private static StandardMaterial3D GlintMaterial()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1, .95f, .7f, 1));
        gradient.SetColor(1, new Color(1, .8f, .2f, 0));
        gradient.AddPoint(.45f, new Color(1, .85f, .3f, .85f));
        var dot = new GradientTexture2D
        {
            Gradient = gradient,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(.5f, .5f),
            FillTo = new Vector2(.5f, 0),
            Width = 64,
            Height = 64,
        };
        return new StandardMaterial3D
        {
            AlbedoTexture = dot,
            AlbedoColor = new Color(1.6f, 1.3f, .6f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
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
