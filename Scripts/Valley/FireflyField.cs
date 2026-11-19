using System;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Vehicles;

namespace MathRacer.Valley;

/// <summary>
/// The scene of the firefly hunt: the lantern on a pole above the car, which shines brighter with every firefly in it, and a
/// handful of fireflies that hover over open ground nearby, blinking, and drift about. A firefly is caught by driving into it;
/// it then flies into the lantern and a new one appears further off. Nothing is drawn while no hunt is on. The places of the
/// fireflies come from a random generator of their own, and nothing here is saved.
/// </summary>
internal sealed partial class FireflyField : Node3D
{
    /// <summary>How many fireflies are about at once.</summary>
    public const int Count = 6;

    /// <summary>How close the car must come to a firefly (along the ground) to catch it.</summary>
    public const float CatchRadius = 3.4f;

    /// <summary>A firefly that is farther than this is moved to a place near the car again.</summary>
    public const float StrayDistance = 80;

    private const float HoverHeight = 1.5f;

    private sealed class Spark
    {
        public Node3D Root = null!;
        public MeshInstance3D Core = null!;
        public MeshInstance3D Halo = null!;
        public Vector3 Home;
        public float Phase;
        public float Speed;
        public float Flying = -1;
        public Vector3 FlyFrom;
        public bool Placed;
    }

    private readonly Spark[] sparks = new Spark[Count];
    private readonly Random random = new(Settings.Seed + 7771);
    private Node3D lantern = null!;
    private Node3D lanternGlow = null!;
    private OmniLight3D lanternLight = null!;
    private Node3D fireflies = null!;
    private Vehicle car = null!;
    private float time;
    private float shown;
    private float flash;

    /// <summary>Finds a place on the ground for a firefly near a point; null when there is none (set by the world).</summary>
    internal Func<Vector3, Random, Vector3?> FindSpot { get; set; } = (_, _) => null;

    /// <summary>The lantern is on its pole and the fireflies are drawn.</summary>
    internal bool Shown => lantern.Visible;

    /// <summary>The lantern's strength, from 0 (not lit) to 1 (full): the glow of its halo follows it.</summary>
    internal float LanternLevel => shown;

    /// <summary>How many fireflies are hovering and can be caught.</summary>
    internal int Hovering
    {
        get
        {
            int count = 0;
            foreach (var spark in sparks)
                if (spark.Placed && spark.Flying < 0)
                    count++;
            return count;
        }
    }

    /// <summary>Where firefly <paramref name="index"/> is, for the checks.</summary>
    internal Vector3 PositionOf(int index) => sparks[index].Root.GlobalPosition;

    /// <summary>Puts a firefly at a place of the checks and takes it into the lantern's reach.</summary>
    internal void Place(int index, Vector3 ground)
    {
        var spark = sparks[index];
        spark.Home = ground + Vector3.Up * HoverHeight;
        spark.Placed = true;
        spark.Flying = -1;
        spark.Root.Position = spark.Home;
    }

    public override void _Ready()
    {
        Name = "FireflyField";
        Visible = true;
        fireflies = new Node3D { Name = "Fireflies" };
        AddChild(fireflies);
        var halo = Halo();
        for (int i = 0; i < Count; i++)
        {
            var spark = new Spark
            {
                Root = new Node3D
                {
                    Name = "Firefly" + i,
                    Visible = false,
                    TopLevel = true,
                },
                Phase = random.NextSingle() * Mathf.Tau,
                Speed = .8f + random.NextSingle() * .5f,
            };
            fireflies.AddChild(spark.Root);
            spark.Core = Art.Ball(spark.Root, Vector3.Zero, Vector3.One * .13f, "f6ff8a");
            spark.Core.MaterialOverride = Shine("f6ff8a");
            spark.Core.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            spark.Halo = new MeshInstance3D
            {
                Name = "Halo",
                Mesh = new QuadMesh { Size = Vector2.One * 2.4f },
                MaterialOverride = halo,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            spark.Root.AddChild(spark.Halo);
            sparks[i] = spark;
        }
    }

    /// <summary>Hangs the lantern on the car.</summary>
    internal void Setup(Vehicle vehicle)
    {
        car = vehicle;
        lantern = new Node3D { Name = "Lantern", Visible = false };
        vehicle.AddChild(lantern);
        Art.Cylinder(lantern, new(0, 1.97f, .2f), .035f, .035f, 1.15f, "5a4a3c");
        Art.Block(lantern, new(0, 2.5f, .2f), new(.34f, .05f, .34f), "3f4f55");
        foreach (float x in new[] { -.14f, .14f })
        foreach (float z in new[] { -.14f, .14f })
            Art.Block(lantern, new(x, 2.72f, .2f + z), new(.03f, .4f, .03f), "3f4f55");
        Art.Block(lantern, new(0, 2.94f, .2f), new(.38f, .05f, .38f), "3f4f55");
        Art.Cylinder(lantern, new(0, 3.02f, .2f), .02f, .16f, .1f, "3f4f55");
        var glass = Art.Ball(lantern, new(0, 2.72f, .2f), new(.22f, .3f, .22f), "fff3b0");
        glass.MaterialOverride = Shine("fff3b0");
        glass.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        lanternGlow = new Node3D { Name = "Glow", Position = new(0, 2.72f, .2f) };
        lantern.AddChild(lanternGlow);
        lanternGlow.AddChild(
            new MeshInstance3D
            {
                Name = "Halo",
                Mesh = new QuadMesh { Size = Vector2.One },
                MaterialOverride = Halo(),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
        lanternLight = new OmniLight3D
        {
            Name = "LanternLight",
            LightColor = new Color("fff0a0"),
            OmniRange = 8,
            OmniAttenuation = 1.2f,
            LightEnergy = 0,
            ShadowEnabled = false,
            Visible = false,
        };
        lanternGlow.AddChild(lanternLight);
    }

    /// <summary>
    /// Moves the fireflies and the lantern by <paramref name="dt"/> seconds. <paramref name="active"/> is whether a hunt is on
    /// and <paramref name="strength"/> the lantern's level (0 to 1). Returns how many fireflies the car caught in this step.
    /// </summary>
    internal int Tick(float dt, bool active, float strength, bool running)
    {
        if (!running)
            dt = 0;
        time += dt;
        shown = Mathf.MoveToward(shown, active ? .25f + .75f * strength : 0, dt * .7f);
        flash = Mathf.MoveToward(flash, 0, dt * 2.2f);
        lantern.Visible = shown > .01f;
        fireflies.Visible = active;
        if (lantern.Visible)
        {
            float glow = shown + flash * .6f;
            lanternGlow.Scale = Vector3.One * (1.6f + 5f * glow);
            lanternLight.Visible = true;
            lanternLight.LightEnergy = .5f + 2.6f * glow;
            lanternLight.OmniRange = 5 + 8 * glow;
        }
        else
            lanternLight.Visible = false;
        if (!active)
        {
            foreach (var spark in sparks)
            {
                spark.Root.Visible = false;
                spark.Placed = false;
                spark.Flying = -1;
            }
            return 0;
        }
        int caught = 0;
        var carPosition = car.GlobalPosition;
        var lanternTop = lanternGlow.GlobalPosition;
        foreach (var spark in sparks)
        {
            if (!spark.Placed || FlatDistance(spark.Home, carPosition) > StrayDistance)
                Spawn(spark, carPosition);
            if (!spark.Placed)
            {
                spark.Root.Visible = false;
                continue;
            }
            spark.Root.Visible = true;
            if (spark.Flying >= 0)
            {
                // Into the lantern: a quick curve that shrinks, then a new firefly appears far off.
                spark.Flying += dt / .45f;
                float t = Mathf.Clamp(spark.Flying, 0, 1);
                spark.Root.GlobalPosition = spark.FlyFrom.Lerp(lanternTop, t * t * (3 - 2 * t));
                spark.Root.Scale = Vector3.One * (1 - t * .8f);
                if (spark.Flying >= 1)
                    Spawn(spark, carPosition);
                continue;
            }
            Animate(spark);
            if (
                running
                && FlatDistance(spark.Root.GlobalPosition, carPosition) < CatchRadius
                && Mathf.Abs(spark.Root.GlobalPosition.Y - carPosition.Y) < 4
            )
            {
                spark.Flying = 0;
                spark.FlyFrom = spark.Root.GlobalPosition;
                flash = 1;
                caught++;
            }
        }
        return caught;
    }

    private void Animate(Spark spark)
    {
        float t = time * spark.Speed + spark.Phase;
        spark.Root.Scale = Vector3.One;
        spark.Root.GlobalPosition =
            spark.Home + new Vector3(Mathf.Sin(t * .9f) * 1.6f, Mathf.Sin(t * 1.3f) * .35f, Mathf.Cos(t * .7f) * 1.6f);
        // It glows for a moment and goes dim again, so a child can follow the blinking.
        float blink = Mathf.Clamp(Mathf.Sin(time * 2.1f * spark.Speed + spark.Phase * 3) * 1.6f + .5f, 0, 1);
        spark.Core.Scale = Vector3.One * (.07f + .06f * blink);
        spark.Halo.Scale = Vector3.One * (.25f + 1.75f * blink);
    }

    private void Spawn(Spark spark, Vector3 around)
    {
        spark.Placed = false;
        spark.Flying = -1;
        spark.Root.Scale = Vector3.One;
        if (FindSpot(around, random) is not { } ground)
            return;
        spark.Home = ground + Vector3.Up * HoverHeight;
        spark.Root.GlobalPosition = spark.Home;
        spark.Placed = true;
    }

    /// <summary>The lantern flashes: every catch makes it swell for a moment.</summary>
    internal void Flash() => flash = 1;

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private static StandardMaterial3D? halo;

    private static StandardMaterial3D Shine(string color) =>
        new() { AlbedoColor = new Color(color).SrgbToLinear(), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

    /// <summary>A soft round yellow-green glow that always faces the camera and adds its light to what is behind it.</summary>
    private static StandardMaterial3D Halo()
    {
        if (halo != null)
            return halo;
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, .3f, 1f },
            Colors = new[]
            {
                new Color(.95f, 1f, .55f, .9f),
                new Color(.85f, 1f, .4f, .3f),
                new Color(.8f, 1f, .3f, 0),
            },
        };
        halo = new StandardMaterial3D
        {
            AlbedoTexture = new GradientTexture2D
            {
                Gradient = gradient,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(.5f, .5f),
                FillTo = new Vector2(1, .5f),
                Width = 64,
                Height = 64,
            },
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            DisableReceiveShadows = true,
        };
        return halo;
    }
}
