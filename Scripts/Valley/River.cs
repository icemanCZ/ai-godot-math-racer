using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>Shared channel profile used by terrain, water, vegetation and the crossing.</summary>
public partial class River : Node3D
{
    public static float BridgeZ => WorldLayout.Plan.BridgeZ;

    public static float CenterX(float z) => WorldLayout.Plan.CenterX(z);

    public static float HalfWidth(float z) => 5.5f + .7f * System.MathF.Sin(z * .037f + WorldLayout.Plan.RiverPhase);

    public static float Level(float z) => -.8f - z * .008f;

    public static float Distance(float x, float z) => System.MathF.Abs(x - CenterX(z));

    public static float BridgeHeight => Level(BridgeZ) + 1;

    /// <summary>The decks of the bridges beyond the old valley lie this high over the water.</summary>
    public const float OutlandDeckRise = .97f;

    /// <summary>A road beyond the old valley comes to the river this high over the water, where its bridge begins.</summary>
    public const float OutlandBankRise = .85f;

    // Keep both the bank and its raised dirt-road surface below the wooden slab.
    public static float BridgeGroundHeight => BridgeHeight - .32f;

    public static bool OnBridge(float x, float z) =>
        System.MathF.Abs(z - BridgeZ) < 4.3f && System.MathF.Abs(x - CenterX(BridgeZ)) < 22;

    public static float BridgeSurfaceHeight(float x) =>
        BridgeHeight + .01f - Mathf.Clamp((Mathf.Abs(x - CenterX(BridgeZ)) - 19) / 3, 0, 1) * .2f;

    /// <summary>The highest the ground may stand at a point of the river's floodplain (<see cref="ShapeTerrain"/>).</summary>
    public static float ValleyCeiling(float x, float z)
    {
        float slopeDistance = Mathf.Max(0, Distance(x, z) - HalfWidth(z) - 5);
        return Level(z) + .85f + slopeDistance * .12f + slopeDistance * slopeDistance * .0008f;
    }

    public static float ShapeTerrain(float x, float z, float natural)
    {
        float given = natural;
        float distance = Distance(x, z),
            width = HalfWidth(z),
            level = Level(z);
        // A broad floodplain joins the hill feet instead of leaving tall walls at the water.
        float slopeDistance = Mathf.Max(0, distance - width - 5);
        float valleyCeiling = level + .85f + slopeDistance * .12f + slopeDistance * slopeDistance * .0008f;
        float roundness = Mathf.Max(0, 2 - Mathf.Abs(natural - valleyCeiling)) / 2;
        natural = Mathf.Min(natural, valleyCeiling) - roundness * roundness * .5f;
        float bank = Mathf.Max(natural, level + .85f);
        float shaped = natural;
        if (distance < width + 5)
            shaped = Mathf.Lerp(level - 1.2f, bank, Mathf.SmoothStep(0, width + 5, distance));
        else if (distance < width + 13)
            shaped = Mathf.Lerp(bank, natural, Mathf.SmoothStep(width + 5, width + 13, distance));
        // The deck is a straight rectangle, independent of the meandering river centre.
        // Include a full terrain cell around it so interpolated triangles cannot cut its edges.
        float along = Mathf.Abs(x - CenterX(BridgeZ)),
            across = Mathf.Abs(z - BridgeZ);
        float approach = (1 - Mathf.SmoothStep(23, 36, along)) * (1 - Mathf.SmoothStep(7, 11, across));
        if (distance > width + 5)
            shaped = Mathf.Lerp(shaped, BridgeGroundHeight, approach);
        shaped = Mathf.Lerp(shaped, Mathf.Min(shaped, BridgeGroundHeight), approach);
        // The banks at a bridge beyond the old valley lie level with the ends of its ramps.
        if (WorldLayout.Plan.Outland.BridgeAt(x, z, 14) is { } crossing)
        {
            float outlandApproach =
                (
                    1
                    - Mathf.SmoothStep(
                        crossing.HalfLength + 3,
                        crossing.HalfLength + 14,
                        Mathf.Abs(x - crossing.CenterX)
                    )
                )
                * (
                    1
                    - Mathf.SmoothStep(
                        OutlandLayout.Bridge.HalfWidth + 1,
                        OutlandLayout.Bridge.HalfWidth + 6,
                        Mathf.Abs(z - crossing.Z)
                    )
                );
            float outlandBank = level + OutlandBankRise;
            if (distance > width + 5)
                shaped = Mathf.Lerp(shaped, outlandBank, outlandApproach);
            shaped = Mathf.Lerp(shaped, Mathf.Min(shaped, outlandBank), outlandApproach);
        }
        // Up in the mountains the river rises: its bed closes into the slope.
        if (z < ValleyBorders.SpringZ)
            shaped = Mathf.Lerp(given, shaped, ValleyBorders.RiverBedWeight(z));
        // Down by the sea the river only cuts, and raises no dykes into the water.
        if (z > ValleyBorders.Untouched)
            shaped = Mathf.Min(shaped, given);
        return shaped;
    }

    /// <summary>The bed of the plain channel, as <see cref="ShapeTerrain"/> cuts it, within the water's width.</summary>
    private static float ChannelBed(float x, float z)
    {
        float level = Level(z);
        return Mathf.Lerp(level - 1.2f, level + .85f, Mathf.SmoothStep(0, HalfWidth(z) + 5, Distance(x, z)));
    }

    public void Generate(Terrain terrain)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        // The water runs on beyond the old valley through the outland and the scenery.
        for (float z = -WorldTuning.SceneryRadius; z < WorldTuning.SceneryRadius; z += 1)
        {
            const int strips = 20;
            for (int strip = 0; strip < strips; strip++)
            {
                float left = -1 + strip * 2f / strips,
                    right = left + 2f / strips;
                Vector3 Edge(float t, float side) => new(CenterX(t) + side * (HalfWidth(t) + 2), Level(t), t);
                var a = Edge(z, left);
                var b = Edge(z, right);
                var c = Edge(z + 1, left);
                var d = Edge(z + 1, right);
                Add(a);
                Add(b);
                Add(c);
                Add(b);
                Add(d);
                Add(c);
            }
        }
        void Add(Vector3 point)
        {
            // Store actual channel depth for shallow color and shoreline foam. Beyond the core the ground is not worked
            // out yet; there the channel has its plain profile, with no bridge, ford or road on it.
            float ground =
                Mathf.Abs(point.Z) <= WorldTuning.CoreRadius
                    ? terrain.Height(point.X, point.Z)
                    : ChannelBed(point.X, point.Z);
            float depth = Mathf.Clamp(Level(point.Z) - ground, 0, 3);
            surface.SetColor(new Color(depth / 3, 0, 0));
            surface.AddVertex(point);
        }
        surface.GenerateNormals();
        var shader = GD.Load<Shader>("res://Shaders/RiverWater.gdshader");
        AddChild(
            new MeshInstance3D
            {
                Mesh = surface.Commit(),
                MaterialOverride = new ShaderMaterial { Shader = shader },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
        Bridge();
    }

    /// <summary>Reed clumps with stones along both banks, leaving the crossings clear.</summary>
    public void AddBankRocks(Terrain terrain, float? fordZ)
    {
        for (int i = 0; i < 48; i++)
        {
            float z = -150 + i * 6.2f;
            if (Mathf.Abs(z - BridgeZ) < 9 || (fordZ is float ford && Mathf.Abs(z - ford) < Ford.ClearHalfWidth))
                continue;
            float x = CenterX(z) + (i % 2 == 0 ? -1 : 1) * (HalfWidth(z) + 5.5f);
            var p = terrain.At(x, z);
            var size = new Vector3(1, .6f, .85f);
            Art.Ball(this, p, size, "adaebf");
            Obstacles.Rock(this, p, size);
            for (int reed = 0; reed < 3; reed++)
                Art.Cylinder(this, p + new Vector3(.25f * reed, .5f, .3f), .035f, .025f, 1, "80c94d");
        }
    }

    private void Bridge()
    {
        var bridge = new Node3D { Position = new Vector3(CenterX(BridgeZ), BridgeHeight - .1f, BridgeZ) };
        AddChild(bridge);
        for (int i = 0; i < 38; i++)
            Art.Block(bridge, new(-18.5f + i, 0, 0), new(.97f, .22f, 7.6f), "c39770");
        // Short sloping wooden approaches join the lowered banks without a vertical step.
        foreach (float end in new[] { -1f, 1f })
        {
            float angle = -end * Mathf.Atan(.2f / 3);
            float cosine = Mathf.Cos(angle);
            for (int i = 0; i < 3; i++)
            {
                var plank = Art.Block(
                    bridge,
                    new(end * (19.5f + i), .11f - (i + .5f) * .2f / 3 - .11f * cosine, 0),
                    new(.97f / cosine, .22f, 7.6f),
                    "c39770"
                );
                plank.Rotation = new Vector3(0, 0, angle);
            }
            var ramp = new StaticBody3D
            {
                Position = new Vector3(end * 20.5f, .01f - .11f * cosine, 0),
                Rotation = new Vector3(0, 0, angle),
            };
            bridge.AddChild(ramp);
            ramp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(3 / cosine, .22f, 7.6f) } });
        }
        foreach (float side in new[] { -3.9f, 3.9f })
        {
            for (int i = 0; i < 9; i++)
                Art.Cylinder(bridge, new(-18 + i * 4.5f, .65f, side), .14f, .12f, 1.5f, "a77a59");
            Art.Beam(bridge, new(-18.5f, 1.2f, side), new(18.5f, 1.2f, side), .16f, "e2bb8b");
            Art.Beam(bridge, new(-18.5f, .55f, side), new(18.5f, .55f, side), .12f, "e2bb8b");
        }
        var body = new StaticBody3D();
        bridge.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(38, .22f, 7.6f) } });
        foreach (float side in new[] { -3.9f, 3.9f })
            body.AddChild(
                new CollisionShape3D
                {
                    Position = new(0, .65f, side),
                    Shape = new BoxShape3D { Size = new(38, 1.3f, .25f) },
                }
            );
    }
}
