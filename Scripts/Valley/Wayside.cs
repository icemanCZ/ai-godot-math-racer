using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// Little things in the grass just beyond the roads' gravel verge: dandelions, dandelion clocks, toadstools and smooth
/// pebbles. They are scenery only, with no solid parts, and come from a random generator of their own (the valley's seed
/// plus <see cref="SeedOffset"/>) after everything else is placed, so nothing else in the valley moves. Each kind is a
/// <see cref="MultiMesh"/> per block of the valley, drawn only near the camera. The flower heads of the dandelions and
/// the clocks are a MultiMesh of their own with the same instances, so that the car can knock them off
/// (<see cref="Trampling"/>): the yellow head flies apart in petals and the clock in seeds, and both grow back.
/// </summary>
internal sealed partial class Wayside : Node3D
{
    private const int SeedOffset = 9311;
    private const float Block = 96;
    private const float DrawDistance = 95;

    private enum Kind
    {
        Dandelion,
        Clock,
        Toadstool,
        Pebbles,
    }

    private static readonly float[] Chances = { .42f, .2f, .14f, .24f };

    internal int Count { get; private set; }

    public void Generate(World world) => Generate(world, world.Routes, SeedOffset, false);

    /// <summary>
    /// The little things beside the roads beyond the old valley, from a random generator of their own, so that nothing
    /// beside the old roads moves.
    /// </summary>
    public void GenerateOutland(World world) => Generate(world, world.OutlandRoutes, SeedOffset + 1, true);

    private void Generate(World world, Roads roads, int seedOffset, bool outland)
    {
        var random = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + seedOffset) };
        var meshes = new[] { Dandelion(), Clock(), Toadstool(), Pebbles() };
        var heads = new Mesh?[] { DandelionHead(), ClockHead(), null, null };
        var placed = new Dictionary<(Kind, Vector2I), List<Transform3D>>();
        var samples = roads.Samples;
        var spots = new List<Vector3>();
        foreach (var animal in world.Animals)
            spots.Add(animal.Position);
        foreach (var friend in world.Hidden.Friends)
            spots.Add(friend.GlobalPosition);
        spots.AddRange(world.SpecialPickupSpots);
        for (int i = 1; i + 1 < samples.Count; i++)
        {
            // About one thing every three metres on each side, never in a regular row.
            if (random.Randf() > .45f)
                continue;
            var forward = samples[i + 1] - samples[i - 1];
            forward.Y = 0;
            if (forward.LengthSquared() < .01f)
                continue;
            forward = forward.Normalized();
            var side = forward.Cross(Vector3.Up) * (random.Randf() < .5f ? -1 : 1);
            float target = Verges.Width + random.RandfRange(-.25f, .9f);
            var p = samples[i] + forward * random.RandfRange(-.7f, .7f) + side * 3;
            // Step out to the wanted distance from the road's edge, whatever the road's width there.
            for (int step = 0; step < 3; step++)
                p += side * (target - roads.EdgeDistance(p));
            float pick = random.Randf();
            float yaw = random.RandfRange(0, Mathf.Tau);
            float size = random.RandfRange(1.35f, 1.95f);
            if (!(outland ? FitsOutland(world, p) : Fits(world, p, spots)))
                continue;
            var kind = Kind.Pebbles;
            float sum = 0;
            for (int k = 0; k < Chances.Length; k++)
            {
                sum += Chances[k];
                if (pick < sum)
                {
                    kind = (Kind)k;
                    break;
                }
            }
            // Toadstools grow in little families of two or three.
            int copies = kind == Kind.Toadstool ? 1 + random.RandiRange(1, 2) : 1;
            for (int copy = 0; copy < copies; copy++)
            {
                var at = p + new Vector3(Mathf.Cos(yaw + copy * 2.3f), 0, Mathf.Sin(yaw + copy * 2.3f)) * (copy * .28f);
                at = world.Ground.At(at.X, at.Z, -.02f);
                float scale = size * (copy == 0 ? 1 : .7f);
                var key = (kind, new Vector2I(Mathf.FloorToInt(at.X / Block), Mathf.FloorToInt(at.Z / Block)));
                if (!placed.TryGetValue(key, out var list))
                    placed[key] = list = new();
                list.Add(new Transform3D(new Basis(Vector3.Up, yaw + copy).Scaled(Vector3.One * scale), at));
                Count++;
            }
        }
        foreach (var ((kind, block), transforms) in placed)
        {
            var origin = new Vector3((block.X + .5f) * Block, 0, (block.Y + .5f) * Block);
            var multimesh = Instances(meshes[(int)kind], transforms, origin);
            Draw($"{kind}{block.X}_{block.Y}", multimesh, origin);
            if (heads[(int)kind] is not { } head)
                continue;
            var headMultimesh = Instances(head, transforms, origin);
            Draw($"{kind}Head{block.X}_{block.Y}", headMultimesh, origin);
            var trampling = world.Plants.Trampling;
            bool clock = kind == Kind.Clock;
            int stems = trampling.AddBatch(new Color("6bb85a"), origin);
            int blossoms = clock
                ? trampling.AddBatch(
                    new Color("fbfbf4"),
                    origin,
                    perHead: 12,
                    petalSize: .022f,
                    fluff: true,
                    lift: .42f
                )
                : trampling.AddBatch(new Color("ffd23f"), origin, perHead: 7, petalSize: .045f, lift: .34f);
            trampling.Bind(stems, multimesh);
            trampling.Bind(blossoms, headMultimesh);
            for (int i = 0; i < transforms.Count; i++)
            {
                trampling.Begin(transforms[i].Origin, .2f, Trampling.Kind.Flower);
                trampling.Add(stems, i, false);
                trampling.Add(blossoms, i, true);
                trampling.End();
            }
        }
    }

    private static MultiMesh Instances(Mesh mesh, List<Transform3D> transforms, Vector3 origin)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count,
        };
        for (int i = 0; i < transforms.Count; i++)
            multimesh.SetInstanceTransform(i, transforms[i] with { Origin = transforms[i].Origin - origin });
        return multimesh;
    }

    private void Draw(string name, MultiMesh multimesh, Vector3 origin) =>
        AddChild(
            new MultiMeshInstance3D
            {
                Name = name,
                Multimesh = multimesh,
                Position = origin,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = DrawDistance,
                VisibilityRangeEndMargin = 10,
                VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Disabled,
            }
        );

    /// <summary>Off the roads and ways, out of the village, the river and the trees, and clear of the characters.</summary>
    private static bool Fits(World world, Vector3 p, List<Vector3> spots)
    {
        if (
            FlatMath.Length(p.X, p.Z) > WorldTuning.CorePlayableRadius - 4
            || world.Routes.EdgeDistance(p) < Verges.Width - .5f
            || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 3
            || River.OnBridge(p.X, p.Z)
            || world.Village.IsReserved(p)
            || world.Ford?.Covers(p) == true
            || world.Plants.NearTrunk(p, .8f)
        )
            return false;
        if (world.Tracks is { } tracks && tracks.MouthDistance(p, world.Routes.EdgeDistance(p)) < 1)
            return false;
        if (world.Tracks is { } ways && ways.Distance(new Vector2(p.X, p.Z)) < 1)
            return false;
        foreach (var spot in spots)
            if (FlatMath.Length(p.X - spot.X, p.Z - spot.Z) < 2.5f)
                return false;
        return true;
    }

    /// <summary>Beyond the old valley: off its roads and bridges, out of the water, off the beach and clear of the trees.</summary>
    private static bool FitsOutland(World world, Vector3 p)
    {
        var borders = WorldLayout.Plan.Borders;
        return FlatMath.Length(p.X, p.Z) > WorldTuning.CorePlayableRadius
            && ValleyBounds.Contains(p.X, p.Z, 4)
            && world.OutlandRoutes.EdgeDistance(p) >= Verges.Width - .5f
            && River.Distance(p.X, p.Z) >= River.HalfWidth(p.Z) + 3
            && WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 4) == null
            && borders.BorderRiverDistance(p.X, p.Z) >= ValleyBorders.BorderRiverHalfWidth + 3
            && !borders.IsSand(p.X, p.Z)
            && p.Y > ValleyBorders.SeaLevel + 1
            && !world.Plants.NearTrunk(p, .8f);
    }

    // ---- The little things, each merged into one mesh with a surface per colour ----

    private static Mesh Dandelion()
    {
        var root = new Node3D();
        Leaves(root, "5fb35a");
        Art.Cylinder(root, new(0, .16f, 0), .012f, .012f, .32f, "6bb85a");
        return Merge(root);
    }

    private static Mesh DandelionHead()
    {
        var root = new Node3D();
        Art.Ball(root, new(0, .34f, 0), new(.07f, .045f, .07f), "ffd23f");
        Art.Ball(root, new(0, .365f, 0), new(.045f, .03f, .045f), "ffe377");
        return Merge(root);
    }

    private static Mesh Clock()
    {
        var root = new Node3D();
        Leaves(root, "5aa857");
        Art.Cylinder(root, new(0, .2f, 0), .011f, .011f, .4f, "86c06b");
        return Merge(root);
    }

    private static Mesh ClockHead()
    {
        var root = new Node3D();
        Art.Ball(root, new(0, .42f, 0), Vector3.One * .075f, "fbfbf4");
        Art.Ball(root, new(0, .42f, 0), Vector3.One * .02f, "c9b98a");
        return Merge(root);
    }

    private static Mesh Toadstool()
    {
        var root = new Node3D();
        Art.Cylinder(root, new(0, .09f, 0), .045f, .035f, .18f, "fff6e6");
        Art.Ball(root, new(0, .19f, 0), new(.12f, .075f, .12f), "e8453c");
        Art.Ball(root, new(0, .17f, 0), new(.1f, .03f, .1f), "f6e7d0");
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5;
            float r = i == 0 ? 0 : .065f;
            Art.Ball(
                root,
                new(Mathf.Cos(a) * r, i == 0 ? .265f : .245f, Mathf.Sin(a) * r),
                new(.022f, .012f, .022f),
                "ffffff"
            );
        }
        return Merge(root);
    }

    private static Mesh Pebbles()
    {
        var root = new Node3D();
        Art.Ball(root, new(0, .02f, 0), new(.13f, .07f, .1f), "b9b3a7");
        Art.Ball(root, new(.15f, .01f, .06f), new(.08f, .05f, .07f), "d6cdbb");
        Art.Ball(root, new(-.06f, .01f, .14f), new(.07f, .04f, .06f), "a9b4bd");
        return Merge(root);
    }

    private static void Leaves(Node3D root, string color)
    {
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Tau / 4 + .4f;
            var leaf = Art.Ball(
                root,
                new(Mathf.Cos(a) * .08f, .02f, Mathf.Sin(a) * .08f),
                new(.1f, .015f, .035f),
                color
            );
            leaf.Rotation = new(0, -a, 0);
        }
    }

    /// <summary>One mesh with a surface for each material of the parts, which are freed.</summary>
    private static Mesh Merge(Node3D root)
    {
        var batches = new Dictionary<Material, SurfaceTool>();
        foreach (var child in root.GetChildren())
        {
            if (child is not MeshInstance3D part || part.MaterialOverride == null)
                continue;
            if (!batches.TryGetValue(part.MaterialOverride, out var surface))
            {
                surface = new SurfaceTool();
                surface.Begin(Mesh.PrimitiveType.Triangles);
                batches[part.MaterialOverride] = surface;
            }
            surface.AppendFrom(part.Mesh, 0, part.Transform);
        }
        var mesh = new ArrayMesh();
        foreach (var (material, surface) in batches)
        {
            surface.Commit(mesh);
            mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, material);
        }
        root.Free();
        return mesh;
    }
}
