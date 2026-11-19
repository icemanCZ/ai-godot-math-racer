using System.Linq;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

/// <summary>
/// Daylight, the warm evening, the starry night and distant cloud silhouettes, independent of world landmarks.
/// </summary>
public partial class Atmosphere : Node3D
{
    private static readonly Color ClearHorizon = new Color("8cbfed").SrgbToLinear();
    private static readonly Color ClearSky = new Color("397fb9").SrgbToLinear();
    private static readonly Color CloudyHorizon = new Color("a0aebf").SrgbToLinear();
    private static readonly Color CloudySky = new Color("576984").SrgbToLinear();
    private static readonly Color ClearFog = new Color("a7c9df").SrgbToLinear();
    private static readonly Color CloudyFog = new Color("8897af").SrgbToLinear();
    private static readonly Color CloudTint = new("8995a8");
    private static readonly Color DayGround = new Color("739fa3").SrgbToLinear();
    private static readonly Color DayAmbient = new("edf5ff");
    private static readonly Color DaySun = new("fff5e5");
    private static readonly Color EveningHorizon = new Color("f4a77e").SrgbToLinear();
    private static readonly Color EveningSky = new Color("6a5f9e").SrgbToLinear();
    private static readonly Color EveningFog = new Color("d9a08c").SrgbToLinear();
    private static readonly Color EveningGround = new Color("8a6a73").SrgbToLinear();
    private static readonly Color EveningAmbient = new("ffd9c8");
    private static readonly Color EveningSun = new("ff9e66");
    private static readonly Color EveningCloud = new("ffc2a4");
    private static readonly Color NightHorizon = new Color("26356f").SrgbToLinear();
    private static readonly Color NightSky = new Color("0b1238").SrgbToLinear();
    private static readonly Color NightFog = new Color("1b2652").SrgbToLinear();
    private static readonly Color NightGround = new Color("0f1a3a").SrgbToLinear();
    private static readonly Color NightAmbient = new("5f78d4");
    private static readonly Color NightSun = new("a9bcff");
    private static readonly Color NightCloud = new("46517e");
    private static readonly Vector3 DaySunAngle = new(-55, -28, 0);
    private static readonly Vector3 EveningSunAngle = new(-13, -62, 0);
    private static readonly Vector3 NightSunAngle = new(-46, 38, 0);

    /// <summary>Distance of the stars and the moon from the camera; the camera sees 1200 m.</summary>
    private const float SkyRadius = 1000;

    private ProceduralSkyMaterial sky = null!;
    private Godot.Environment environment = null!;
    private DirectionalLight3D sun = null!;
    private StandardMaterial3D cloudMaterial = null!;
    private Node3D nightSky = null!;
    private StandardMaterial3D starMaterial = null!;
    private StandardMaterial3D moonMaterial = null!;
    private Node3D moon = null!;

    /// <summary>
    /// Sets the sky for the weather. <paramref name="evening"/> and <paramref name="night"/> (0 to 1) blend the
    /// ordinary day towards a low orange sun or a moonlit night with stars.
    /// </summary>
    internal void ApplyWeather(float cover, float flash, float evening = 0, float night = 0)
    {
        var horizon = ClearHorizon.Lerp(CloudyHorizon, cover).Lerp(EveningHorizon, evening).Lerp(NightHorizon, night);
        sky.SkyTopColor = ClearSky.Lerp(CloudySky, cover).Lerp(EveningSky, evening).Lerp(NightSky, night);
        sky.SkyHorizonColor = horizon;
        sky.GroundHorizonColor = horizon;
        sky.GroundBottomColor = DayGround.Lerp(EveningGround, evening).Lerp(NightGround, night);
        environment.AmbientLightColor = DayAmbient.Lerp(EveningAmbient, evening).Lerp(NightAmbient, night);
        environment.AmbientLightEnergy =
            (.42f - cover * .12f + flash * .22f) * (1 - evening * .1f) * (1 - night * .45f);
        environment.FogLightColor = ClearFog.Lerp(CloudyFog, cover).Lerp(EveningFog, evening).Lerp(NightFog, night);
        environment.FogDensity = .00055f + cover * .0035f + evening * .0004f + night * .0006f;
        sun.LightEnergy = (.6f - cover * .41f + flash * .53f) * (1 - evening * .15f) * (1 - night * .8f);
        sun.LightColor = DaySun.Lerp(EveningSun, evening).Lerp(NightSun, night);
        sun.RotationDegrees = DaySunAngle.Lerp(EveningSunAngle, evening).Lerp(NightSunAngle, night);
        cloudMaterial.AlbedoColor = Colors
            .White.Lerp(CloudTint, cover)
            .Lerp(EveningCloud, evening)
            .Lerp(NightCloud, night);

        // The moon stands where the sun's light comes from, and the stars fade in as the sky darkens; clouds of rain or a
        // storm hide most of them.
        float stars = Mathf.Clamp((night - .15f) / .85f, 0, 1) * (1 - cover * .85f);
        nightSky.Visible = stars > 0;
        starMaterial.AlbedoColor = new Color(1, 1, 1, stars);
        moonMaterial.AlbedoColor = new Color("fff3c9") with { A = stars };
        moon.Position = sun.Basis.Z * SkyRadius;
    }

    /// <summary>The stars and the moon travel with the camera, so they never come closer.</summary>
    public override void _Process(double delta)
    {
        if (nightSky.Visible && GetViewport().GetCamera3D() is { } camera)
            nightSky.GlobalPosition = camera.GlobalPosition;
        TickCollection((float)delta);
        if (constellation == null)
            return;
        constellationTime -= (float)delta;
        if (constellationTime <= 0 || !nightSky.Visible)
        {
            constellation.QueueFree();
            constellation = null;
            return;
        }
        // It fades in over the first quarter of the time and out over the last.
        float fade = Mathf.Clamp(
            Mathf.Min(constellationTime, constellationTotal - constellationTime) / (constellationTotal * .25f),
            0,
            1
        );
        constellationMaterial.AlbedoColor = new Color("ffe9a0") with { A = fade };
    }

    private Node3D? constellation;
    private StandardMaterial3D constellationMaterial = null!;
    private float constellationTime,
        constellationTotal;

    /// <summary>
    /// Draws <paramref name="picture"/> in bright stars joined by thin lines, 27 degrees over the horizon in the
    /// direction <paramref name="azimuth"/> (the angle the camera turns to, 0 looks along +Z), for
    /// <paramref name="seconds"/>.
    /// </summary>
    internal void ShowConstellation(Constellation picture, float azimuth, float seconds)
    {
        constellation?.QueueFree();
        constellationMaterial = SkyMaterial(new Color("ffe9a0") with { A = 0 });
        constellationTotal = constellationTime = seconds;
        constellation = new Node3D { Name = "Constellation" };
        nightSky.AddChild(constellation);
        var points = SkyPoints(picture, azimuth, .47f);
        var ball = new SphereMesh
        {
            Radius = 1,
            Height = 2,
            RadialSegments = 10,
            Rings = 5,
        };
        foreach (var point in points)
            constellation.AddChild(
                new MeshInstance3D
                {
                    Mesh = ball,
                    MaterialOverride = constellationMaterial,
                    Position = point,
                    Scale = Vector3.One * 11,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
        var line = new BoxMesh { Size = Vector3.One };
        for (int i = 0; i + 1 < picture.Lines.Length; i += 2)
        {
            var from = points[picture.Lines[i]];
            var to = points[picture.Lines[i + 1]];
            var middle = (from + to) / 2;
            constellation.AddChild(
                new MeshInstance3D
                {
                    Mesh = line,
                    MaterialOverride = constellationMaterial,
                    Transform = new Transform3D(
                        Basis.LookingAt(to - from, Vector3.Up)
                            * Basis.FromScale(new Vector3(3.2f, 3.2f, from.DistanceTo(to))),
                        middle
                    ),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
        }
    }

    /// <summary>
    /// Where the stars of <paramref name="picture"/> stand on the sky sphere when its middle is at <paramref name="azimuth"/>
    /// (0 looks along +Z) and <paramref name="elevation"/> (radians over the horizon). 1 degree of sky is about 17 m there.
    /// </summary>
    private static Vector3[] SkyPoints(Constellation picture, float azimuth, float elevation)
    {
        const float radius = SkyRadius * .96f;
        var centre = new Vector3(
            Mathf.Sin(azimuth) * Mathf.Cos(elevation),
            Mathf.Sin(elevation),
            Mathf.Cos(azimuth) * Mathf.Cos(elevation)
        );
        var right = centre.Cross(Vector3.Up).Normalized();
        var up = right.Cross(centre).Normalized();
        return picture
            .Stars.Select(offset =>
                (centre + (right * offset.X + up * offset.Y) * Mathf.DegToRad(1)).Normalized() * radius
            )
            .ToArray();
    }

    /// <summary>Where picture <paramref name="index"/> of the child's collection hangs: the pictures are spread round the sky.</summary>
    internal static float CollectionAzimuth(int index) => .6f + index * Mathf.Tau / Constellation.All.Count;

    private const float CollectionElevation = .4f;
    private Node3D? collection;
    private MeshInstance3D? newestStar;
    private float newestTime;

    /// <summary>The stars of the collection that shine now, for the checks.</summary>
    internal int ShiningStars =>
        collection
            ?.GetNodeOrNull<Node3D>("Lit")
            ?.GetChildren()
            .Count(node => node.Name.ToString().StartsWith("Star", System.StringComparison.Ordinal))
        ?? 0;

    /// <summary>
    /// Draws the child's collection of stars on the night sky: picture n hangs at its own place (<see cref="CollectionAzimuth"/>),
    /// <paramref name="lit"/>[n] of its stars shine and are joined by a line wherever both ends shine, and the rest are
    /// faint dots that show where the next ones will be. The star that was lit last, <paramref name="newest"/>, swells for a
    /// few seconds. It is part of the night sky, so it shows whenever the stars are out.
    /// </summary>
    internal void ShowCollection(int[] lit, (int Picture, int Star)? newest = null)
    {
        collection?.QueueFree();
        newestStar = null;
        newestTime = 0;
        collection = new Node3D { Name = "Collection" };
        nightSky.AddChild(collection);
        var shining = new Node3D { Name = "Lit" };
        var faint = new Node3D { Name = "Faint" };
        collection.AddChild(shining);
        collection.AddChild(faint);
        collectionMaterial ??= SkyMaterial(new Color("ffe9a0"));
        collectionFaintMaterial ??= SkyMaterial(new Color("b6c6ff") with { A = .3f });
        var ball = new SphereMesh
        {
            Radius = 1,
            Height = 2,
            RadialSegments = 10,
            Rings = 5,
        };
        var line = new BoxMesh { Size = Vector3.One };
        for (int picture = 0; picture < Constellation.All.Count; picture++)
        {
            var shape = Constellation.All[picture];
            int count = picture < lit.Length ? lit[picture] : 0;
            var points = SkyPoints(shape, CollectionAzimuth(picture), CollectionElevation);
            for (int star = 0; star < points.Length; star++)
            {
                bool on = star < count;
                var mesh = new MeshInstance3D
                {
                    Name = $"Star{picture}_{star}",
                    Mesh = ball,
                    MaterialOverride = on ? collectionMaterial : collectionFaintMaterial,
                    Position = points[star],
                    Scale = Vector3.One * (on ? 11 : 4),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                (on ? shining : faint).AddChild(mesh);
                if (newest is { } latest && latest.Picture == picture && latest.Star == star)
                {
                    newestStar = mesh;
                    newestTime = 5;
                }
            }
            for (int i = 0; i + 1 < shape.Lines.Length; i += 2)
            {
                if (shape.Lines[i] >= count || shape.Lines[i + 1] >= count)
                    continue;
                var from = points[shape.Lines[i]];
                var to = points[shape.Lines[i + 1]];
                shining.AddChild(
                    new MeshInstance3D
                    {
                        Mesh = line,
                        MaterialOverride = collectionMaterial,
                        Transform = new Transform3D(
                            Basis.LookingAt(to - from, Vector3.Up)
                                * Basis.FromScale(new Vector3(2.4f, 2.4f, from.DistanceTo(to))),
                            (from + to) / 2
                        ),
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    }
                );
            }
        }
    }

    private StandardMaterial3D? collectionMaterial;
    private StandardMaterial3D? collectionFaintMaterial;

    /// <summary>The newest star swells and shrinks back for a few seconds, so that the eye finds it.</summary>
    private void TickCollection(float delta)
    {
        if (newestStar == null || !IsInstanceValid(newestStar))
            return;
        newestTime = Mathf.Max(0, newestTime - delta);
        float swell = 1 + 2.2f * newestTime / 5 * (.6f + .4f * Mathf.Sin(newestTime * 7));
        newestStar.Scale = Vector3.One * 11 * swell;
        if (newestTime <= 0)
            newestStar = null;
    }

    public void Generate()
    {
        sky = new ProceduralSkyMaterial
        {
            SkyTopColor = ClearSky,
            SkyHorizonColor = ClearHorizon,
            SkyCurve = .3f,
            GroundHorizonColor = ClearHorizon,
            GroundBottomColor = DayGround,
        };
        environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = DayAmbient,
            AmbientLightEnergy = .42f,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            FogEnabled = true,
            FogLightColor = ClearFog,
            FogDensity = .00055f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        sun = new DirectionalLight3D
        {
            RotationDegrees = DaySunAngle,
            LightColor = DaySun,
            LightEnergy = .6f,
            ShadowEnabled = true,
            DirectionalShadowMaxDistance = 100,
        };
        AddChild(sun);
        CreateNightSky();

        cloudMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color("ffffff"),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var puff = new SphereMesh
        {
            Radius = 1,
            Height = 2,
            RadialSegments = 24,
            Rings = 12,
        };
        var random = new RandomNumberGenerator { Seed = (ulong)Settings.Seed + 81 };
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.Tau / 24;
            float radius = random.RandfRange(620, 850);
            var cloud = new Node3D
            {
                Position = new Vector3(
                    Mathf.Sin(angle) * radius,
                    random.RandfRange(90, 130),
                    Mathf.Cos(angle) * radius
                ),
                Rotation = new Vector3(0, angle, 0),
                Scale = Vector3.One * random.RandfRange(2.5f, 4f),
            };
            AddChild(cloud);
            Puff(new Vector3(0, 0, 0), new Vector3(9, 1.8f, 3.4f));
            Puff(new Vector3(-4, 1.2f, 0), new Vector3(3.8f, 2.8f, 3.2f));
            Puff(new Vector3(.5f, 2.2f, 0), new Vector3(4.1f, 3.5f, 3.5f));
            Puff(new Vector3(4.8f, .8f, 0), new Vector3(3.2f, 2.5f, 2.8f));
            void Puff(Vector3 position, Vector3 scale)
            {
                cloud.AddChild(
                    new MeshInstance3D
                    {
                        Mesh = puff,
                        MaterialOverride = cloudMaterial,
                        Position = position,
                        Scale = scale,
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    }
                );
            }
        }
    }

    /// <summary>
    /// Stars and a moon on a sphere around the camera. They have a random generator of their own, so the clouds and
    /// everything else that is generated from the seed stay where they were.
    /// </summary>
    private void CreateNightSky()
    {
        nightSky = new Node3D { Name = "NightSky", Visible = false };
        AddChild(nightSky);
        starMaterial = SkyMaterial(Colors.White);
        var random = new RandomNumberGenerator { Seed = (ulong)Settings.Seed + 82 };
        var stars = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new SphereMesh
            {
                Radius = .5f,
                Height = 1,
                RadialSegments = 6,
                Rings = 3,
            },
            InstanceCount = 260,
        };
        for (int i = 0; i < stars.InstanceCount; i++)
        {
            // Above the hills: no stars lower than about eight degrees over the horizon.
            float elevation = Mathf.Asin(random.RandfRange(.14f, 1));
            float turn = random.RandfRange(0, Mathf.Tau);
            var direction = new Vector3(
                Mathf.Cos(elevation) * Mathf.Sin(turn),
                Mathf.Sin(elevation),
                Mathf.Cos(elevation) * Mathf.Cos(turn)
            );
            float size = random.RandfRange(3.4f, 8.5f);
            stars.SetInstanceTransform(i, new Transform3D(Basis.FromScale(Vector3.One * size), direction * SkyRadius));
            // A few stars are warm and a few are blue, like in a picture book.
            float roll = random.Randf();
            stars.SetInstanceColor(
                i,
                roll < .15f ? new Color("ffe29a")
                    : roll < .3f ? new Color("b9d4ff")
                    : Colors.White
            );
        }
        nightSky.AddChild(
            new MultiMeshInstance3D
            {
                Name = "Stars",
                Multimesh = stars,
                MaterialOverride = starMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
        moonMaterial = SkyMaterial(new Color("fff3c9"));
        moon = new Node3D { Name = "Moon" };
        nightSky.AddChild(moon);
        var ball = new SphereMesh
        {
            Radius = 1,
            Height = 2,
            RadialSegments = 20,
            Rings = 10,
        };
        foreach (var (material, size) in new[] { (moonMaterial, 42f) })
            moon.AddChild(
                new MeshInstance3D
                {
                    Mesh = ball,
                    MaterialOverride = material,
                    Scale = Vector3.One * size,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                }
            );
    }

    private static StandardMaterial3D SkyMaterial(Color color) =>
        new()
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            DisableFog = true,
        };
}
