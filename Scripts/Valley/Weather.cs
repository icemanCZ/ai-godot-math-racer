using Godot;
using MathRacer.Settlement;
using MathRacer.Vehicles;

namespace MathRacer.Valley;

internal enum WeatherKind
{
    Clear,
    Rain,
    Wind,
    Storm,

    /// <summary>A low orange sun. Only the witch brings it; the weather never turns to evening by itself.</summary>
    Evening,

    /// <summary>A starry night with a moon and fireflies. Only the witch brings it.</summary>
    Night,
}

public partial class Weather : Node3D
{
    private readonly RandomNumberGenerator random = new();
    private readonly Puddles puddles = new();
    private readonly ShaderMaterial rainMaterial = new() { Shader = GD.Load<Shader>("res://Shaders/Rain.gdshader") };
    private readonly ImmediateMesh boltMesh = new();
    private readonly MeshInstance3D bolt = new() { Visible = false };
    private readonly ShaderMaterial fireflyMaterial = new()
    {
        Shader = GD.Load<Shader>("res://Shaders/Firefly.gdshader"),
    };
    private MultiMeshInstance3D rain = null!;
    private Node3D night = null!;
    private SpotLight3D headlights = null!;
    private Village village = null!;
    private Atmosphere atmosphere = null!;
    private Terrain ground = null!;
    private FollowCamera camera = null!;
    private Vehicle car = null!;
    private Vector2 windDirection;
    private float remaining,
        cover,
        wind,
        rainfall,
        wetness,
        evening,
        nightfall,
        fireflyTime,
        rainTime,
        nextBolt,
        flash;
    internal WeatherKind Current { get; private set; }

    internal void Setup(World world, Vehicle vehicle, FollowCamera view)
    {
        atmosphere = world.Atmosphere;
        ground = world.Ground;
        camera = view;
        car = vehicle;
        village = world.Village;
        random.Randomize();
        AddChild(puddles);
        puddles.Generate(world);
        CreateRain();
        CreateNight();
        bolt.Mesh = boltMesh;
        bolt.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color("e6efff"),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DisableFog = true,
        };
        bolt.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(bolt);
        Begin(WeatherKind.Clear);
        RenderingServer.GlobalShaderParameterSet("weather_wind", Vector2.Zero);
        RenderingServer.GlobalShaderParameterSet("road_wetness", 0f);
    }

    /// <summary>
    /// Starts <paramref name="kind"/> of weather. The sky changes over several seconds, or at once with
    /// <paramref name="instant"/> (a screenshot should not wait for it).
    /// </summary>
    internal void Begin(WeatherKind kind, bool instant = false)
    {
        Current = kind;
        remaining =
            kind == WeatherKind.Clear ? random.RandfRange(240, 420)
            : kind is WeatherKind.Evening or WeatherKind.Night ? 300
            : random.RandfRange(35, 60);
        nextBolt = random.RandfRange(4, 7);
        flash = 0;
        bolt.Hide();
        if (instant)
        {
            cover = CoverTarget;
            evening = kind == WeatherKind.Evening ? 1 : 0;
            nightfall = kind == WeatherKind.Night ? 1 : 0;
        }
        atmosphere.ApplyWeather(cover, 0, evening, nightfall);
        if (kind is WeatherKind.Clear or WeatherKind.Evening or WeatherKind.Night)
            return;
        windDirection = Vector2.Right.Rotated(random.RandfRange(0, Mathf.Tau));
        var drift = windDirection * (kind == WeatherKind.Storm ? 5f : 1.5f);
        rainMaterial.SetShaderParameter("drift", drift);
    }

    /// <summary>How hard the wind blows now, from 0 in calm weather to 1.5 in a storm.</summary>
    internal float WindStrength => wind;

    internal void Tick(float dt)
    {
        remaining -= dt;
        if (remaining <= 0)
        {
            float roll = random.Randf();
            Begin(
                Current != WeatherKind.Clear ? WeatherKind.Clear
                : roll < .15f ? WeatherKind.Storm
                : roll < .60f ? WeatherKind.Rain
                : WeatherKind.Wind
            );
        }

        float windTarget = Current switch
        {
            WeatherKind.Rain => .14f,
            WeatherKind.Storm => 1.5f,
            WeatherKind.Wind => 1.1f,
            _ => 0,
        };
        float rainTarget = Current switch
        {
            WeatherKind.Rain => .69f,
            WeatherKind.Storm => 1,
            _ => 0,
        };
        float previousCover = cover,
            previousFlash = flash;
        float previousEvening = evening,
            previousNight = nightfall;
        float previousWind = wind,
            previousWetness = wetness;
        cover = Mathf.MoveToward(cover, CoverTarget, dt * .23f);
        evening = Mathf.MoveToward(evening, Current == WeatherKind.Evening ? 1 : 0, dt * .12f);
        nightfall = Mathf.MoveToward(nightfall, Current == WeatherKind.Night ? 1 : 0, dt * .12f);
        wind = Mathf.MoveToward(wind, windTarget, dt * .45f);
        rainfall = Mathf.MoveToward(rainfall, rainTarget, dt * .35f);
        wetness = Mathf.MoveToward(wetness, rainTarget > 0 ? 1 : 0, dt * (rainTarget > 0 ? .075f : .028f));

        rain.Visible = rainfall > .001f;
        if (rain.Visible)
        {
            rainTime += dt;
            rain.GlobalPosition = camera.GlobalPosition;
            rainMaterial.SetShaderParameter("rain_time", rainTime);
            rainMaterial.SetShaderParameter("intensity", rainfall);
        }
        else
            rainTime = 0;
        if (wind != previousWind || wind > 0)
            RenderingServer.GlobalShaderParameterSet("weather_wind", windDirection * wind);
        if (wetness != previousWetness)
            RenderingServer.GlobalShaderParameterSet("road_wetness", wetness);
        puddles.Tick(dt, car, wetness, rainfall);

        flash = Mathf.MoveToward(flash, 0, dt * 4);
        if (Current == WeatherKind.Storm && (nextBolt -= dt) <= 0)
        {
            Strike();
            nextBolt = random.RandfRange(9, 16);
            flash = 1;
        }
        bolt.Visible = flash > .45f;
        if (
            cover != previousCover
            || flash != previousFlash
            || evening != previousEvening
            || nightfall != previousNight
        )
            atmosphere.ApplyWeather(cover, flash, evening, nightfall);
        TickNight(dt);
    }

    private float CoverTarget =>
        Current switch
        {
            WeatherKind.Rain => .62f,
            WeatherKind.Storm => 1,
            WeatherKind.Wind => .18f,
            WeatherKind.Evening => .22f,
            _ => 0,
        };

    /// <summary>How dark it is, from 0 by day to 1 at night: the car's lights and the fireflies follow it.</summary>
    internal float Darkness => Mathf.Max(evening * .6f, nightfall);

    /// <summary>The night is deep enough for the stars: the stargazer can show a constellation.</summary>
    internal bool StarsOut => nightfall > .6f;

    /// <summary>The fireflies, the headlights of the car and the lamps in the windows.</summary>
    private void TickNight(float dt)
    {
        float glow = Mathf.Clamp(nightfall + evening * .35f, 0, 1);
        night.Visible = glow > .01f;
        if (night.Visible)
        {
            fireflyTime += dt;
            night.GlobalPosition = car.GlobalPosition;
            fireflyMaterial.SetShaderParameter("glow_time", fireflyTime);
            fireflyMaterial.SetShaderParameter("intensity", glow);
        }
        headlights.Visible = Darkness > .02f;
        headlights.LightEnergy = Darkness * 4.5f;
        village.SetNightLights(Darkness > .35f);
    }

    private void CreateNight()
    {
        var specks = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh(),
            CustomAabb = new Aabb(new(-40, -3, -40), new(80, 10, 80)),
            InstanceCount = 120,
        };
        var seeds = new RandomNumberGenerator { Seed = 8321 };
        for (int i = 0; i < specks.InstanceCount; i++)
        {
            specks.SetInstanceTransform(i, Transform3D.Identity);
            specks.SetInstanceCustomData(i, new Color(seeds.Randf(), seeds.Randf(), seeds.Randf(), seeds.Randf()));
        }
        night = new Node3D { Name = "Night", Visible = false };
        night.AddChild(
            new MultiMeshInstance3D
            {
                Name = "Fireflies",
                Multimesh = specks,
                MaterialOverride = fireflyMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
        AddChild(night);
        // The car's front is -Z, as for the camera that looks over its roof from +Z.
        headlights = new SpotLight3D
        {
            Name = "Headlights",
            Position = new(0, .95f, -1.55f),
            RotationDegrees = new(-7, 0, 0),
            SpotRange = 48,
            SpotAngle = 34,
            SpotAngleAttenuation = .7f,
            LightColor = new Color("fff0c2"),
            LightEnergy = 0,
            Visible = false,
        };
        car.AddChild(headlights);
    }

    private void CreateRain()
    {
        var drops = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            Mesh = new QuadMesh(),
            CustomAabb = new Aabb(new(-25, -10, -25), new(50, 24, 50)),
            InstanceCount = 1600,
        };
        for (int i = 0; i < drops.InstanceCount; i++)
        {
            drops.SetInstanceTransform(i, Transform3D.Identity);
            drops.SetInstanceCustomData(i, new Color(random.Randf(), random.Randf(), random.Randf(), random.Randf()));
        }
        rain = new MultiMeshInstance3D
        {
            Multimesh = drops,
            MaterialOverride = rainMaterial,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(rain);
    }

    private void Strike()
    {
        var forward = -camera.GlobalBasis.Z;
        forward.Y = 0;
        var direction = forward.Normalized().Rotated(Vector3.Up, random.RandfRange(-.35f, .35f));
        var center = camera.GlobalPosition + direction * random.RandfRange(120, 175);
        float strikeHeight = Mathf.Max(center.Y - 2, ground.Height(center.X, center.Z) + 1);
        var right = camera.GlobalBasis.X;
        var tip = new Vector3(center.X, strikeHeight + 42, center.Z);
        boltMesh.ClearSurfaces();
        boltMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 10; i++)
        {
            var end = tip + right * random.RandfRange(-3.2f, 3.2f);
            end.Y = strikeHeight + (9 - i) * 4.2f + (i == 9 ? 0 : random.RandfRange(-.8f, .8f));
            Segment(tip, end, .27f - i * .022f);
            if (i is 2 or 5)
            {
                var fork = end - Vector3.Up * 3.7f + right * (i == 2 ? 3.3f : -3.7f);
                Segment(tip, fork, .11f);
                Segment(fork, fork - Vector3.Up * 4.7f + right * (i == 2 ? 3.9f : -4.2f), .065f);
            }
            tip = end;
        }
        boltMesh.SurfaceEnd();

        void Segment(Vector3 from, Vector3 to, float width)
        {
            var side = right * width;
            boltMesh.SurfaceAddVertex(from - side);
            boltMesh.SurfaceAddVertex(to - side);
            boltMesh.SurfaceAddVertex(from + side);
            boltMesh.SurfaceAddVertex(from + side);
            boltMesh.SurfaceAddVertex(to - side);
            boltMesh.SurfaceAddVertex(to + side);
        }
    }
}
