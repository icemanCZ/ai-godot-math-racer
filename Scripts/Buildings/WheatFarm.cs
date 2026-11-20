using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// Where the farmer's wheat field is: <see cref="Position"/> is the loading ring, <see cref="Road"/> the road point its
/// field path starts from and <see cref="Inward"/> the direction away from the road, in which the fields lie.
/// </summary>
internal sealed record FarmSite(Vector3 Position, Vector3 Road, Vector3 Inward)
{
    /// <summary>The farm lies in this area (along the way inward, across it), plants and rocks there are taken out.</summary>
    public const float AlongMin = -8,
        AlongMax = 30,
        AcrossMax = 17;
}

/// <summary>
/// The farm outside the village: four fields of wheat that sway in the wind around a barn and a heap of sacks, with
/// a scarecrow and a sign. The farmer loads the trailer at the ring in front. Local +Z points away from the road, as
/// for a works. The fields are made of thousands of stalks in one <see cref="MultiMesh"/> each, drawn in rows.
/// </summary>
public partial class WheatFarm : Node3D
{
    private const float PatchWidth = 10,
        PatchDepth = 8.5f,
        PatchX = 8.5f;

    /// <summary>The fields stand left and right of a lane, at two distances from the ring.</summary>
    private static readonly float[] PatchZ = { 7, 16.5f };

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly RandomNumberGenerator random = new();

    internal FarmSite Site { get; }

    internal WheatFarm(FarmSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "WheatFarm";
        Position = site.Position;
        float yaw = Mathf.Atan2(site.Inward.X, site.Inward.Z);
        Rotation = new Vector3(0, yaw, 0);
        basis = new Basis(Vector3.Up, yaw);
        random.Seed = (ulong)Settings.Seed + 2381;
    }

    /// <summary>The middles of the four fields in world space, for the soil that the ground shows under them.</summary>
    internal static Vector3[] FieldCenters(FarmSite site)
    {
        var right = Vector3.Up.Cross(site.Inward);
        var centers = new List<Vector3>();
        foreach (float z in PatchZ)
        foreach (int side in new[] { -1, 1 })
            centers.Add(site.Position + right * side * PatchX + site.Inward * z);
        return centers.ToArray();
    }

    /// <summary>The soil of a field is drawn as five round pads, so that bare earth shows under all the wheat.</summary>
    internal static IEnumerable<Vector3> SoilPads(FarmSite site)
    {
        var right = Vector3.Up.Cross(site.Inward);
        foreach (var center in FieldCenters(site))
        {
            yield return center;
            foreach (float across in new[] { -3.3f, 3.3f })
            foreach (float along in new[] { -2f, 2f })
                yield return center + right * across + site.Inward * along;
        }
    }

    /// <summary>The radius of one of the soil pads.</summary>
    internal const float SoilRadius = 3.6f;

    /// <summary>The lane of the farm yard: from the ring in front of the sacks to the barn doors.</summary>
    internal static (Vector3 From, Vector3 To) Lane(FarmSite site) => (site.Position, site.Position + site.Inward * 18);

    /// <summary>Height of the ground below a point given in the farm's own frame, relative to the ring.</summary>
    private float Floor(float x, float z)
    {
        var global = Position + basis * new Vector3(x, 0, z);
        return ground.Height(global.X, global.Z) - Position.Y;
    }

    public override void _Ready()
    {
        foreach (float z in PatchZ)
        foreach (int side in new[] { -1, 1 })
            BuildField(side * PatchX, z);
        BuildBarn(0, 20.5f);
        BuildSacks(4.4f, 2.2f);
        BuildScarecrow(-PatchX, PatchZ[0]);
        var post = new Vector3(-4.6f, Floor(-4.6f, 1.2f), 1.2f);
        Art.Cylinder(this, post + Vector3.Up * 1.5f, .12f, .12f, 3, "a77a59");
        Obstacles.Trunk(this, post, .12f, 3);
        Art.Sign(this, post + Vector3.Up * 3.3f, "POLE", 40);
    }

    // ---- The wheat ----

    private static ArrayMesh StalkMesh()
    {
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        Box(tool, new(-.022f, 0, -.022f), new(.022f, .84f, .022f), new Color("c9b45a"));
        Box(tool, new(-.05f, .6f, -.05f), new(.05f, .86f, .05f), new Color("f2c64a"));
        Box(tool, new(-.012f, .84f, -.012f), new(.012f, .98f, .012f), new Color("f7dc7a"));
        tool.GenerateNormals();
        return tool.Commit();
    }

    private static void Box(SurfaceTool tool, Vector3 min, Vector3 max, Color color)
    {
        var c = new[]
        {
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, max.Y, min.Z),
            new Vector3(min.X, max.Y, min.Z),
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, max.Z),
            new Vector3(min.X, max.Y, max.Z),
        };
        int[] faces =
        {
            0,
            3,
            2,
            0,
            2,
            1, // back
            4,
            5,
            6,
            4,
            6,
            7, // front
            0,
            4,
            7,
            0,
            7,
            3, // left
            1,
            2,
            6,
            1,
            6,
            5, // right
            3,
            7,
            6,
            3,
            6,
            2, // top
            0,
            1,
            5,
            0,
            5,
            4, // bottom
        };
        tool.SetColor(color.SrgbToLinear());
        foreach (int index in faces)
            tool.AddVertex(c[index]);
    }

    private static ShaderMaterial? wheatMaterial;

    /// <summary>A field of wheat in rows along the way inward, each stalk a little different in height and turn.</summary>
    private void BuildField(float centerX, float centerZ)
    {
        wheatMaterial ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/Wheat.gdshader") };
        var rows = new List<Transform3D>();
        for (float x = -PatchWidth / 2 + .2f; x < PatchWidth / 2; x += .42f)
        for (float z = -PatchDepth / 2 + .15f; z < PatchDepth / 2; z += .24f)
        {
            float px = centerX + x + random.RandfRange(-.07f, .07f);
            float pz = centerZ + z + random.RandfRange(-.07f, .07f);
            float height = random.RandfRange(.85f, 1.2f);
            var stalk =
                Basis.FromEuler(new Vector3(0, random.RandfRange(0, Mathf.Tau), 0))
                * Basis.FromScale(new Vector3(1, height, 1));
            rows.Add(new Transform3D(stalk, new Vector3(px, Floor(px, pz), pz)));
        }
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = StalkMesh(),
            InstanceCount = rows.Count,
        };
        for (int i = 0; i < rows.Count; i++)
            multi.SetInstanceTransform(i, rows[i]);
        AddChild(
            new MultiMeshInstance3D
            {
                Name = "WheatField",
                Multimesh = multi,
                MaterialOverride = wheatMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
    }

    // ---- The farm yard ----

    /// <summary>A red barn with a gabled roof, big doors and a hay loft, its front towards the ring.</summary>
    private void BuildBarn(float x, float z)
    {
        float y = Floor(x, z - 2.5f);
        foreach (float dx in new[] { -2.5f, 2.5f })
            y = Mathf.Max(y, Floor(x + dx, z + 2.5f));
        const float width = 6.4f,
            depth = 4.8f,
            height = 3.4f;
        Art.Wall(this, new(x, y + height / 2, z), new(width, height, depth), "b8473b", FacadeStyle.Boards);
        // White trim around the doors and corners.
        Art.Block(this, new(x, y + 1.35f, z - depth / 2 - .02f), new(2.8f, 2.7f, .1f), "f3e9d2");
        Art.Block(this, new(x, y + 1.35f, z - depth / 2 - .06f), new(2.4f, 2.4f, .08f), "8c3329");
        Art.Block(this, new(x, y + 1.35f, z - depth / 2 - .11f), new(.12f, 2.4f, .06f), "f3e9d2");
        Art.Block(this, new(x, y + 2.9f, z - depth / 2 - .06f), new(1.2f, .8f, .08f), "f3e9d2");
        Art.Block(this, new(x, y + 2.9f, z - depth / 2 - .1f), new(.9f, .55f, .08f), "3a2a26");
        foreach (float dx in new[] { -width / 2, width / 2 })
            Art.Block(this, new(x + dx, y + height / 2, z - depth / 2), new(.22f, height, .22f), "f3e9d2");
        const float angle = .5f;
        float half = width / 2;
        float length = half / Mathf.Cos(angle) + .35f;
        foreach (int side in new[] { -1, 1 })
        {
            var panel = Art.Wall(
                this,
                new(x + side * half / 2, y + height + half / 2 * Mathf.Tan(angle) + .1f, z),
                new(length, .18f, depth + .6f),
                "7b4a38",
                FacadeStyle.Shingles
            );
            panel.RotationDegrees = new Vector3(0, 0, -side * Mathf.RadToDeg(angle));
        }
        Art.Block(this, new(x, y + height + half * Mathf.Tan(angle) + .22f, z), new(.4f, .2f, depth + .7f), "5c3826");
        // Hay peeks out of the loft and a bale stands beside the doors.
        Art.Block(this, new(x + 1.1f, y + 2.85f, z - depth / 2 - .15f), new(.5f, .2f, .16f), "e2c25a");
        Art.Block(this, new(x - width / 2 - 1.2f, y + .4f, z - depth / 2 + .6f), new(1.1f, .8f, .8f), "e2c25a");
        Art.Block(this, new(x - width / 2 - 1.2f, y + .4f, z - depth / 2 + .6f), new(1.14f, .08f, .84f), "b9962f");
        Obstacles.Box(this, new(x, y + height / 2, z), new(width + .2f, height, depth + .2f));
    }

    /// <summary>Six sacks of wheat tied at the neck, three below and two above, beside the loading ring.</summary>
    private void BuildSacks(float x, float z)
    {
        float y = Floor(x, z);
        string[] colors = { "e8dcc0", "dccfae", "efe4c8" };
        for (int i = 0; i < 6; i++)
        {
            int layer = i / 3;
            float px = x + (i % 3 - 1 + layer * .5f) * .95f - (layer == 1 ? .25f : 0);
            float pz = z + (layer == 1 ? .2f : -.1f * (i % 2));
            float lift = y + layer * .75f;
            Art.Ball(this, new(px, lift + .42f, pz), new(.52f, .46f, .42f), colors[i % 3]);
            Art.Ball(this, new(px, lift + .86f, pz), new(.2f, .14f, .18f), "c9b78e");
            Art.Block(this, new(px, lift + .72f, pz), new(.32f, .04f, .28f), "b8895f");
        }
        // Some ears of wheat have spilled on the ground.
        for (int i = 0; i < 5; i++)
            Art.Block(
                this,
                new(x + 1.3f + i * .18f, y + .04f, z - .9f + (i % 2) * .15f),
                new(.5f, .05f, .08f),
                "f2c64a"
            ).RotationDegrees = new Vector3(0, i * 31, 0);
        Obstacles.Box(this, new(x, y + .7f, z), new(3.2f, 1.4f, 1.5f));
    }

    /// <summary>A scarecrow with a straw hat on a pole in the middle of the nearest field on the left.</summary>
    private void BuildScarecrow(float x, float z)
    {
        float y = Floor(x, z);
        Art.Cylinder(this, new(x, y + 1.25f, z), .06f, .06f, 2.5f, "8a5f3f");
        Art.Block(this, new(x, y + 1.85f, z), new(1.5f, .1f, .1f), "8a5f3f");
        Art.Block(this, new(x, y + 1.45f, z - .02f), new(.62f, .72f, .26f), "4f8fb8");
        foreach (int side in new[] { -1, 1 })
            Art.Block(this, new(x + side * .8f, y + 1.62f, z), new(.34f, .45f, .22f), "4f8fb8");
        Art.Ball(this, new(x, y + 2.05f, z - .02f), Vector3.One * .24f, "f1d6a3");
        Art.Cylinder(this, new(x, y + 2.28f, z), .36f, .2f, .06f, "e2c25a");
        Art.Cylinder(this, new(x, y + 2.4f, z), .17f, .17f, .2f, "d9b84a");
        foreach (int side in new[] { -1, 1 })
            Art.Ball(this, new(x + side * .08f, y + 2.07f, z - .22f), Vector3.One * .035f, "2b2118");
    }
}
