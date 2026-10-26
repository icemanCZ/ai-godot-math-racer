using Godot;

namespace MathRacer;

/// <summary>Daylight and distant cloud silhouettes, independent of world landmarks.</summary>
public partial class Atmosphere : Node3D
{
    public void Generate()
    {
        var horizon = new Color("419de8").SrgbToLinear();
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("1763d1").SrgbToLinear(),
            SkyHorizonColor = horizon,
            SkyCurve = .3f,
            GroundHorizonColor = horizon,
            GroundBottomColor = new Color("4d9cc4").SrgbToLinear()
        };
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color("edf5ff"),
                AmbientLightEnergy = .42f,
                TonemapMode = Godot.Environment.ToneMapper.Linear,
                FogEnabled = true,
                FogLightColor = new Color("79b9ec").SrgbToLinear(),
                FogDensity = .00035f
            }
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55, -28, 0),
            LightColor = new Color("fff5e5"),
            LightEnergy = .6f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 100
        });

        var cloudMaterial = new StandardMaterial3D
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

