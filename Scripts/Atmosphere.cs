using Godot;

namespace MathRacer;

/// <summary>Daylight and distant cloud silhouettes, independent of world landmarks.</summary>
public partial class Atmosphere : Node3D
{
    private static readonly Color ClearHorizon = new Color("8cbfed").SrgbToLinear();
    private static readonly Color ClearSky = new Color("397fb9").SrgbToLinear();
    private static readonly Color CloudyHorizon = new Color("a0aebf").SrgbToLinear();
    private static readonly Color CloudySky = new Color("576984").SrgbToLinear();
    private static readonly Color ClearFog = new Color("a7c9df").SrgbToLinear();
    private static readonly Color CloudyFog = new Color("8897af").SrgbToLinear();
    private static readonly Color CloudTint = new("8995a8");
    private ProceduralSkyMaterial sky = null!;
    private Godot.Environment environment = null!;
    private DirectionalLight3D sun = null!;
    private StandardMaterial3D cloudMaterial = null!;

    internal void ApplyWeather(float cover, float flash)
    {
        var horizon = ClearHorizon.Lerp(CloudyHorizon, cover);
        sky.SkyTopColor = ClearSky.Lerp(CloudySky, cover);
        sky.SkyHorizonColor = horizon;
        sky.GroundHorizonColor = horizon;
        environment.AmbientLightEnergy = .42f - cover * .12f + flash * .22f;
        environment.FogLightColor = ClearFog.Lerp(CloudyFog, cover);
        environment.FogDensity = .00055f + cover * .0035f;
        sun.LightEnergy = .6f - cover * .41f + flash * .53f;
        cloudMaterial.AlbedoColor = Colors.White.Lerp(CloudTint, cover);
    }

    public void Generate()
    {
        sky = new ProceduralSkyMaterial
        {
            SkyTopColor = ClearSky,
            SkyHorizonColor = ClearHorizon,
            SkyCurve = .3f,
            GroundHorizonColor = ClearHorizon,
            GroundBottomColor = new Color("739fa3").SrgbToLinear()
        };
        environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("edf5ff"),
            AmbientLightEnergy = .42f,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            FogEnabled = true,
            FogLightColor = ClearFog,
            FogDensity = .00055f
        };
        AddChild(new WorldEnvironment { Environment = environment });
        sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, -28, 0),
            LightColor = new Color("fff5e5"),
            LightEnergy = .6f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 100
        };
        AddChild(sun);

        cloudMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("ffffff"),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        var puff = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 24, Rings = 12 };
        var random = new RandomNumberGenerator { Seed = (ulong)Settings.Seed + 81 };
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.Tau / 24;
            float radius = random.RandfRange(620, 850);
            var cloud = new Node3D
            {
                Position = new Vector3(Mathf.Sin(angle) * radius, random.RandfRange(90, 130), Mathf.Cos(angle) * radius),
                Rotation = new Vector3(0, angle, 0),
                Scale = Vector3.One * random.RandfRange(2.5f, 4f)
            };
            AddChild(cloud);
            Puff(new Vector3(0, 0, 0), new Vector3(9, 1.8f, 3.4f));
            Puff(new Vector3(-4, 1.2f, 0), new Vector3(3.8f, 2.8f, 3.2f));
            Puff(new Vector3(.5f, 2.2f, 0), new Vector3(4.1f, 3.5f, 3.5f));
            Puff(new Vector3(4.8f, .8f, 0), new Vector3(3.2f, 2.5f, 2.8f));
            void Puff(Vector3 position, Vector3 scale)
            {
                cloud.AddChild(new MeshInstance3D
                {
                    Mesh = puff, MaterialOverride = cloudMaterial, Position = position, Scale = scale,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                });
            }
        }
    }
}

