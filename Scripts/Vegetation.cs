using Godot;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public partial class Vegetation : Node3D
{
    private readonly RandomNumberGenerator rng = new() { Seed = (ulong)Settings.Seed };
    // Art detail uses a separate random stream so tree placement stays identical.
    private readonly RandomNumberGenerator detailRng = new() { Seed = (ulong)Settings.Seed + 901 };
    // Chunked batches let the renderer cull distant groves instead of drawing the whole valley.
    private readonly Dictionary<(string Shape, string Color, int X, int Z), List<Transform3D>> batches = new();
    private readonly Dictionary<Vector2I, List<Vector3>> treePositions = new();
    private Terrain ground = null!;
    private static readonly (Vector2 Center, int Type)[] GroveRegions = CreateGroveRegions();
    public Vector3 PreviewSpot { get; private set; }
    public int TreeCount { get; private set; }
    public int BushCount { get; private set; }
    private void Leaf(Vector3 p, Vector3 scale, string color)
        => Part(p, scale, color, "leaf");
    private void Part(Vector3 p, Vector3 scale, string color, string shape, Basis? rotation = null)
    {
        var key = (shape, color, Mathf.FloorToInt(p.X / 64), Mathf.FloorToInt(p.Z / 64));
        if (!batches.TryGetValue(key, out var batch)) batches[key] = batch = new();
        buildingTree?.Parts.Add(new TreePart(key, batch.Count, new Transform3D((rotation ?? Basis.Identity).ScaledLocal(scale), p)));
        batch.Add(new Transform3D((rotation ?? Basis.Identity).ScaledLocal(scale), p));
    }
    public void Generate(Terrain terrain, Roads roads, Village village, Animal[] animals)
    {
        ground = terrain;
        bool Reserved(Vector3 p) => village.IsReserved(p) || animals.Any(a => a.Position.DistanceTo(p) < 6);
        // Dense groves with open clearings between them.
        for (int grove = 0; grove < 14; grove++)
        {
            var center = GroveRegions[7 + grove].Center;
            for (int tree = 0; tree < 180; tree++)
            {
                float a = rng.RandfRange(0, Mathf.Tau), r = Mathf.Sqrt(rng.Randf()) * 43;
                var p = terrain.At(center.X + Mathf.Cos(a) * r, center.Y + Mathf.Sin(a) * r);
                if (Reserved(p) || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 10 || roads.Distance(p) < 7) continue;
                PlantTree(p, rng.RandfRange(1, 1.8f), TreeTypeAt(p));
            }
        }
        for (int i = 0; i < 6500; i++)
        {
            float x = rng.RandfRange(-510, 510), z = rng.RandfRange(-510, 510);
            if (new Vector2(x, z).Length() > Settings.PlayableRadius - 8) continue;
            var p = terrain.At(x, z);
            if (Reserved(p) || River.Distance(x, z) < River.HalfWidth(z) + 10 || roads.Distance(p) < 7 || new Vector2(x - 100, z - 46).Length() < 20) continue;
            bool forest = Mathf.Sin(x * .018f + WorldLayout.ForestPhase) * Mathf.Cos(z * .015f - WorldLayout.ForestPhase) > -.05f;
            if (!forest && rng.Randf() < .85f) continue;
            PlantTree(p, rng.RandfRange(.8f, 1.45f), TreeTypeAt(p));
        }
        // Small groups along the routes bring the new silhouettes into the driving view.
        for (int i = 0; i < roads.Samples.Count - 1; i += 23)
        {
            var road = roads.Samples[i];
            var tangent = roads.Samples[i + 1] - road;
            if (tangent.Length() < .1f || tangent.Length() > 3) continue;
            var side = tangent.Normalized().Cross(Vector3.Up) * (i % 2 == 0 ? 1 : -1);
            var verge = road + side * rng.RandfRange(10, 15);
            var p = terrain.At(verge.X, verge.Z);
            if (!Safe(p, 9)) continue;
            if (PlantTree(p, rng.RandfRange(.95f, 1.3f), TreeTypeAt(p)) && PreviewSpot == Vector3.Zero)
                PreviewSpot = p;
        }
        for (int i = 0; i < 2100; i++)
        {
            var p = terrain.At(rng.RandfRange(-510, 510), rng.RandfRange(-510, 510));
            if (!Safe(p, 5.4f)) continue;
            Bush(p, rng.RandfRange(.65f, 1.35f), i % 3);
            if (i % 3 != 0) continue;
            var neighbor = terrain.At(p.X + rng.RandfRange(1.8f, 3), p.Z + rng.RandfRange(-2, 2));
            if (Safe(neighbor, 5.4f)) Bush(neighbor, rng.RandfRange(.5f, .85f), i % 3);
        }
        // More undergrowth beside the roads, while leaving the animal interaction clearings open.
        for (int i = 0; i < roads.Samples.Count; i += 11)
        {
            var road = roads.Samples[i];
            var p = terrain.At(road.X + rng.RandfRange(-10, 10), road.Z + rng.RandfRange(-10, 10));
            if (Safe(p, 5.4f)) Bush(p, rng.RandfRange(.6f, 1.1f), i % 3);
        }
        bool Safe(Vector3 p, float roadClearance) => new Vector2(p.X, p.Z).Length() < Settings.PlayableRadius - 8 &&
            !Reserved(p) && River.Distance(p.X, p.Z) > River.HalfWidth(p.Z) + 8 && roads.Distance(p) > roadClearance;
        for (int i = 0; i < 9000; i++)
        {
            var p = terrain.At(rng.RandfRange(-510, 510), rng.RandfRange(-510, 510));
            if (Reserved(p) || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 6 || roads.Distance(p) < 4.2f) continue;
            string color = new[] { "ff60ab", "ffd240", "fff5de", "ae78ed" }[i % 4];
            Leaf(p + Vector3.Up * .18f, new Vector3(.035f, .2f, .035f), "238e73");
            for (int petal = 0; petal < 4; petal++)
                Leaf(p + new Vector3(Mathf.Sin(petal * Mathf.Pi / 2) * .13f, .36f, Mathf.Cos(petal * Mathf.Pi / 2) * .13f), new Vector3(.13f, .06f, .13f), color);
            Leaf(p + Vector3.Up * .38f, new Vector3(.075f, .065f, .075f), "ffd240");
            if (i % 15 == 0)
            {
                var size = new Vector3(.7f, .43f, .55f);
                Leaf(p, size, "adaebf");
                Obstacles.Rock(this, p, size);
            }
            if (i % 21 == 0)
            {
                Leaf(p + Vector3.Up * .2f, new Vector3(.1f, .25f, .1f), "fff5de");
                Leaf(p + Vector3.Up * .45f, new Vector3(.32f, .15f, .32f), "f54257");
            }
        }
        var meshes = new Dictionary<string, Mesh>
        {
            ["leaf"] = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 10, Rings = 5 },
            ["canopy"] = FoliageMesh(),
            ["detail"] = new SphereMesh { Radius = 1, Height = 2, RadialSegments = 7, Rings = 4 },
            ["wood"] = new CylinderMesh { BottomRadius = 1, TopRadius = .7f, Height = 2, RadialSegments = 8 },
            ["cone"] = new CylinderMesh { BottomRadius = 1, TopRadius = .035f, Height = 2, RadialSegments = 10 }
        };
        var foliageShader = GD.Load<Shader>("res://Shaders/Foliage.gdshader");
        var foliageMaterials = new Dictionary<string, ShaderMaterial>();
        foreach (var pair in batches)
        {
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = meshes[pair.Key.Shape],
                InstanceCount = pair.Value.Count
            };
            for (int i = 0; i < pair.Value.Count; i++) mm.SetInstanceTransform(i, pair.Value[i]);
            renderedBatches[pair.Key] = mm;
            Material material = Art.Mat(pair.Key.Color);
            if (pair.Key.Shape == "canopy")
            {
                if (!foliageMaterials.TryGetValue(pair.Key.Color, out var foliage))
                {
                    foliage = new ShaderMaterial { Shader = foliageShader };
                    foliage.SetShaderParameter("leaf_color", new Color(pair.Key.Color).SrgbToLinear());
                    foliageMaterials[pair.Key.Color] = foliage;
                }
                material = foliage;
            }
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm, MaterialOverride = material,
                // Small leaves and bark marks only matter near the camera.
                VisibilityRangeEnd = pair.Key.Shape == "detail" ? 85 : 0,
                VisibilityRangeEndMargin = pair.Key.Shape == "detail" ? 10 : 0
            });
        }
        batches.Clear();
        GD.Print($"VEGETATION_READY trees={TreeCount} bushes={BushCount}");
    }

    private static (Vector2 Center, int Type)[] CreateGroveRegions()
    {
        // Inner regions are visible from the main loop; outer regions follow the dense groves.
        var regions = new List<(Vector2, int)>
        {
            (new(-85, -50), 0), (new(-35, 35), 1), (new(-35, -130), 2),
            (new(-135, 75), 3), (new(40, -45), 4), (new(60, 115), 5), (new(155, -100), 6)
        };
        var regionRandom = new System.Random(Settings.Seed + 412);
        for (int i = 0; i < regions.Count; i++)
        {
            var center = regions[i].Item1 + new Vector2(regionRandom.Next(-25, 26), regionRandom.Next(-25, 26));
            regions[i] = (center, regionRandom.Next(7));
        }
        for (int grove = 0; grove < 14; grove++)
        {
            float angle = grove * 2.39996f + WorldLayout.ForestPhase + (float)regionRandom.NextDouble() * .5f;
            float radius = regionRandom.Next(180, 406);
            regions.Add((new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, regionRandom.Next(7)));
        }
        return regions.ToArray();
    }

    private static int TreeTypeAt(Vector3 p)
    {
        // All placement passes use the same continuous species map, including roadside trees.
        // Willows form a riverbank ribbon; other species form coherent adjoining groves.
        if (River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 28) return 5;
        var point = new Vector2(p.X, p.Z);
        float best = float.MaxValue;
        int type = 0;
        foreach (var region in GroveRegions)
        {
            // Distort region boundaries gently, leaving each grove center anchored.
            var delta = point - region.Center;
            var warped = delta + new Vector2(
                Mathf.Sin(delta.Y * .025f) * 13,
                Mathf.Sin(delta.X * .021f) * 13);
            float distance = warped.LengthSquared();
            if (distance >= best) continue;
            best = distance;
            type = region.Type;
        }
        return type;
    }

    private static ArrayMesh FoliageMesh()
    {
        // A scalloped, asymmetric surface instead of an obvious scaled sphere.
        const int rings = 12, sides = 20;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetSmoothGroup(0);
        void Vertex(int ring, int side)
        {
            float v = ring / (float)rings, a = side * Mathf.Tau / sides;
            float t = v * Mathf.Pi;
            float r = 1 + .085f * Mathf.Sin(a * 5 + t * 3) + .045f * Mathf.Cos(a * 9 - t * 7);
            var position = new Vector3(Mathf.Sin(t) * Mathf.Cos(a) * r,
                Mathf.Cos(t) * (1 + .045f * Mathf.Sin(a * 4) * Mathf.Sin(t)), Mathf.Sin(t) * Mathf.Sin(a) * r);
            // Baked underside shading keeps crown volumes readable in ambient light.
            float shade = Mathf.Lerp(.69f, 1.08f, Mathf.Pow(1 - v, .7f));
            surface.SetColor(new Color(shade, shade, shade));
            surface.AddVertex(position);
        }
        for (int ring = 0; ring < rings; ring++)
        for (int side = 0; side < sides; side++)
        {
            // Godot front faces use clockwise winding.
            Vertex(ring, side); Vertex(ring + 1, side); Vertex(ring, side + 1);
            Vertex(ring, side + 1); Vertex(ring + 1, side); Vertex(ring + 1, side + 1);
        }
        surface.Index(); surface.GenerateNormals();
        return surface.Commit();
    }
    internal bool NearTrunk(Vector3 p, float clearance)
    {
        var cell = new Vector2I(Mathf.FloorToInt(p.X / 5), Mathf.FloorToInt(p.Z / 5));
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            if (treePositions.TryGetValue(cell + new Vector2I(x, z), out var nearby))
                foreach (var other in nearby)
                    if (new Vector2(other.X - p.X, other.Z - p.Z).LengthSquared() < clearance * clearance) return true;
        return false;
    }

    private bool PlantTree(Vector3 p, float s, int type)
    {
        var cell = new Vector2I(Mathf.FloorToInt(p.X / 5), Mathf.FloorToInt(p.Z / 5));
        for (int x = -1; x <= 1; x++)
        for (int z = -1; z <= 1; z++)
            if (treePositions.TryGetValue(cell + new Vector2I(x, z), out var nearby))
                foreach (var other in nearby)
                    if (new Vector2(other.X - p.X, other.Z - p.Z).Length() < 3.6f) return false;
        if (!treePositions.TryGetValue(cell, out var positions)) treePositions[cell] = positions = new();
        positions.Add(p);
        TreeCount++;
        buildingTree = new TreeVisual(p, s);
        Tree(p, s, type);
        buildingTree = null;
        if (TreeCount % 4 == 0) ForestFloor(p, s);
        return true;
    }

    private void ForestFloor(Vector3 tree, float s)
    {
        // Sample the terrain again so the tiny plants sit on hills instead of floating.
        float turn = detailRng.RandfRange(0, Mathf.Tau);
        var p = ground.At(tree.X + Mathf.Cos(turn) * .9f * s, tree.Z + Mathf.Sin(turn) * .9f * s);
        for (int frond = 0; frond < 4; frond++)
        {
            float a = turn + frond * 1.65f;
            var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = direction.Cross(Vector3.Up);
            for (int pair = 0; pair < 3; pair++)
            {
                float t = (pair + 1) / 4f;
                var center = p + (direction * t * .7f + Vector3.Up * (.12f + Mathf.Sin(t * Mathf.Pi) * .35f)) * s;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var axis = (side * sign + direction * .45f + Vector3.Up * .2f).Normalized();
                    var rotation = new Basis(new Quaternion(Vector3.Up, axis));
                    Part(center + side * sign * .1f * s, new Vector3(.065f, .22f * (1 - t * .45f), .035f) * s,
                        pair % 2 == 0 ? "89bc76" : "b0d780", "detail", rotation);
                }
            }
        }
        if (TreeCount % 12 != 0) return;
        for (int mushroom = 0; mushroom < 2; mushroom++)
        {
            var m = ground.At(tree.X + .65f + mushroom * .32f, tree.Z - .55f);
            float size = (mushroom == 0 ? 1 : .7f) * s;
            Part(m + Vector3.Up * .16f * size, new Vector3(.065f, .18f, .065f) * size, "fff0df", "detail");
            Part(m + Vector3.Up * .34f * size, new Vector3(.25f, .12f, .25f) * size, "e66a65", "detail");
            for (int dot = 0; dot < 3; dot++)
            {
                float a = dot * 2.4f;
                Part(m + new Vector3(Mathf.Cos(a) * .12f, .435f, Mathf.Sin(a) * .12f) * size,
                    new Vector3(.045f, .018f, .04f) * size, "fff0df", "detail");
            }
        }
    }

    private void Tree(Vector3 p, float s, int type)
    {
        var turn = new Basis(Vector3.Up, rng.RandfRange(0, Mathf.Tau));
        Vector3 At(float x, float y, float z) => p + turn * new Vector3(x, y, z) * s;
        void Crown(float x, float y, float z, Vector3 size, string color)
        {
            if (size.Length() < .8f) { Leaf(At(x, y, z), size * s, color); return; }
            var center = At(x, y, z);
            buildingTree!.Crowns.Add((center, size * s, color));
            var twist = turn.Rotated(Vector3.Up, detailRng.RandfRange(-.4f, .4f));
            Part(center, size * s, color, "canopy", twist);
            // Small overlapping leaf sprays break the silhouette without filling the whole crown.
            for (int leaf = 0; leaf < 7; leaf++)
            {
                float a = leaf * 2.39996f + x + z;
                float elevation = .12f + (leaf % 3) * .25f;
                var offset = new Vector3(Mathf.Cos(a) * size.X * .9f, elevation * size.Y, Mathf.Sin(a) * size.Z * .9f);
                var orientation = twist.Rotated(Vector3.Up, a).Rotated(Vector3.Forward, .35f);
                Part(center + turn * offset * s, new Vector3(.28f, .075f, .12f) * s,
                    color, "detail", orientation);
            }
        }
        void Wood(Vector3 a, Vector3 b, float width, string color)
        {
            var from = p + turn * a * s;
            var to = p + turn * b * s;
            var rotation = new Basis(new Quaternion(Vector3.Up, (to - from).Normalized()));
            Part((from + to) / 2, new Vector3(width * s, from.DistanceTo(to) / 2, width * s), color, "wood", rotation);
        }
        string bark = type == 3 ? "e9dfca" : type == 4 ? "a77978" : "93674f";
        float height = type == 3 ? 4.5f : type == 2 ? 3.8f : 3.2f;
        var trunk = Obstacles.Trunk(this, p, (type == 3 ? .23f : .34f) * s, height * s);
        treeVisuals[trunk.GetInstanceId()] = buildingTree!;
        float bend = detailRng.RandfRange(-.22f, .22f);
        Wood(new(0, -.1f, 0), new(bend, height * .48f, .08f), type == 3 ? .2f : .31f, bark);
        Wood(new(bend, height * .48f, .08f), new(.15f, height, 0), type == 3 ? .16f : .23f, bark);
        // Warm bark ridges follow the trunk rather than floating as random spots.
        if (type != 3)
        for (int ridge = 0; ridge < 5; ridge++)
        {
            float a = ridge * Mathf.Tau / 5;
            var position = At(Mathf.Cos(a) * .255f, .85f + (ridge % 2) * .35f, Mathf.Sin(a) * .255f);
            Part(position, new Vector3(.035f, .42f, .035f) * s, "b58b64", "detail", turn);
        }
        // Root flares ground the trunks; forked branches remain visible below the foliage.
        for (int n = 0; n < 3; n++)
        {
            float a = n * Mathf.Tau / 3;
            Wood(new(Mathf.Cos(a) * .65f, .04f, Mathf.Sin(a) * .65f), new(0, .65f, 0), .16f, bark);
            Part(At(Mathf.Cos(a) * .42f, .1f, Mathf.Sin(a) * .42f), new Vector3(.32f, .09f, .25f) * s,
                "7f9f60", "detail", turn);
        }
        if (type == 2) // A real tapered silhouette, with soft scalloped skirts.
        {
            string[] greens = { "287c6b", "349579", "50ad83", "7bc695" };
            for (int n = 0; n < 4; n++)
            {
                float r = 1.85f - n * .38f;
                Part(At(0, 2.05f + n * .95f, 0), new Vector3(r, 1.15f, r) * s, greens[n], "cone", turn);
                for (int tip = 0; tip < 5; tip++)
                {
                    float a = tip * Mathf.Tau / 5 + n * .4f;
                    Crown(Mathf.Cos(a) * r * .67f, 1.45f + n * .95f, Mathf.Sin(a) * r * .67f,
                        new(r * .42f, .28f, r * .42f), greens[n]);
                }
            }
            return;
        }
        if (type == 3) // Slender birch with ivory bark and dark markings.
        {
            for (int n = 0; n < 7; n++)
                Crown(n % 2 == 0 ? -.14f : .2f, .65f + n * .5f, .015f, new(.09f, .04f, .19f), "756e68");
            Wood(new(.1f, 2, 0), new(-.75f, 3.8f, .2f), .1f, bark);
            Crown(0, 4.7f, 0, new(1.05f, 1.8f, .95f), "b0d780");
            Crown(-.85f, 3.9f, .15f, new(.85f, 1.1f, .8f), "83bd69");
            Crown(.65f, 4.25f, -.2f, new(.7f, 1.15f, .75f), "c6e599");
            return;
        }
        string[] palette = type switch
        {
            1 => new[] { "4d9d60", "75bb68", "9bd47a" },
            4 => new[] { "d77eaa", "efa4c5", "f7c6d6" },
            5 => new[] { "589b79", "7bb58a", "a0cc91" },
            6 => new[] { "d5a34e", "e8bd61", "f4d887" },
            _ => new[] { "398b67", "64aa70", "93c77d" }
        };
        float spread = type == 5 ? 1.65f : type == 1 ? 1.15f : 1.4f;
        Crown(.05f, 4.1f, 0, new(1.55f, 1.15f, 1.4f), palette[2]);
        for (int n = 0; n < 5; n++)
        {
            float a = n * Mathf.Tau / 5;
            float x = Mathf.Cos(a) * spread, z = Mathf.Sin(a) * spread;
            float y = 3.1f + rng.RandfRange(-.2f, .4f);
            Wood(new(.05f, 1.65f, 0), new(x, y, z), .13f, bark);
            Crown(x, y + .35f, z, new(1.1f, .95f, 1.05f), palette[n % 3]);
            if (type == 5)
            {
                // Drooping leaf chains make willows distinct even from the driving camera.
                for (int strand = 0; strand < 2; strand++)
                {
                    float dx = x * (1.1f + strand * .18f), dz = z * (1.1f + strand * .18f);
                    Crown(dx, y - .55f, dz, new(.35f, 1.2f, .35f), palette[(n + strand) % 3]);
                    Crown(dx * 1.05f, y - 1.45f, dz * 1.05f, new(.2f, .65f, .2f), palette[2]);
                }
            }
            if (type == 1)
            {
                for (int fruit = 0; fruit < 2; fruit++)
                {
                    var apple = new Vector3(x * 1.55f, y + .05f + fruit * .4f, z * 1.55f + fruit * .22f);
                    Crown(apple.X, apple.Y, apple.Z, new(.21f, .23f, .21f), "e66a65");
                    Wood(apple + Vector3.Up * .18f, apple + new Vector3(.04f, .34f, 0), .025f, bark);
                    Part(p + turn * (apple + new Vector3(.13f, .29f, 0)) * s,
                        new Vector3(.17f, .045f, .08f) * s, "c6db86", "detail", turn);
                    Part(p + turn * (apple + new Vector3(-.07f, .06f, -.18f)) * s,
                        new Vector3(.055f, .075f, .025f) * s, "ffc4a0", "detail", turn);
                }
            }
            if (type == 4)
            {
                for (int flower = 0; flower < 4; flower++)
                {
                    float b = a + flower * .65f - .9f;
                    var normal = new Vector3(Mathf.Cos(b), .45f + flower * .1f, Mathf.Sin(b)).Normalized();
                    var center = At(x, y + .35f, z) + turn * normal * s * 1.02f;
                    Blossom(center, turn * normal, s * .22f, flower % 2 == 0 ? "fff0df" : "ffe0e5");
                }
            }
        }
        // A small knot gives the broad trunks an illustrated storybook detail.
        Crown(.03f, 1.15f, -.245f, new(.1f, .18f, .045f), "6f5147");
        Part(At(.03f, 1.15f, -.28f), new Vector3(.045f, .11f, .025f) * s, "b58b64", "detail", turn);
    }

    private void Blossom(Vector3 p, Vector3 normal, float size, string color)
    {
        var facing = new Basis(new Quaternion(Vector3.Up, normal));
        for (int petal = 0; petal < 5; petal++)
        {
            float a = petal * Mathf.Tau / 5;
            var rotation = facing * new Basis(Vector3.Up, -a);
            Part(p + facing * new Vector3(Mathf.Cos(a), .08f, Mathf.Sin(a)) * size * .65f,
                new Vector3(.72f, .18f, .43f) * size, color, "detail", rotation);
        }
        Part(p + normal * size * .18f, new Vector3(.32f, .2f, .32f) * size, "f4d887", "detail", facing);
    }

    private void Bush(Vector3 p, float s, int type)
    {
        BushCount++;
        float turn = rng.RandfRange(0, Mathf.Tau);
        for (int n = 0; n < 5; n++)
        {
            float a = turn + n * Mathf.Tau / 5;
            var center = p + new Vector3(Mathf.Cos(a) * .5f, .55f + (n % 2) * .18f, Mathf.Sin(a) * .5f) * s;
            Part(center, new Vector3(.7f, .65f, .65f) * s, n % 2 == 0 ? "559b71" : "89bc76", "canopy");
            // Layered pointed leaves make the bushes read as foliage rather than green stones.
            for (int leaf = 0; leaf < 5; leaf++)
            {
                float b = a + leaf * 1.25f;
                var outward = new Vector3(Mathf.Cos(b), .4f + leaf * .06f, Mathf.Sin(b));
                var orientation = new Basis(Vector3.Up, -b).Rotated(Vector3.Forward, .35f);
                Part(center + outward * s * .55f, new Vector3(.24f, .065f, .11f) * s,
                    leaf % 2 == 0 ? "b0d780" : "89bc76", "detail", orientation);
            }
            if (type == 0) continue;
            var detail = center + new Vector3(Mathf.Cos(a) * .48f, .32f, Mathf.Sin(a) * .48f) * s;
            if (type == 1)
            {
                Leaf(detail, Vector3.One * .13f * s, "8683c6");
                Leaf(detail + new Vector3(.16f, .07f, .03f) * s, Vector3.One * .12f * s, "b09cdb");
                Part(detail + new Vector3(0, .09f, -.09f) * s, Vector3.One * .035f * s, "d6c4ef", "detail");
            }
            else
            {
                Blossom(detail, new Vector3(Mathf.Cos(a), 1, Mathf.Sin(a)).Normalized(), .19f * s, "ffe0e5");
            }
        }
    }
}


