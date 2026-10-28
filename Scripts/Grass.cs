using Godot;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Seeded, bounded meadow tiles; all blade animation runs on the GPU.</summary>
public partial class Grass : Node3D
{
    private const int TileSize = 16, TrailSize = 8;
    private readonly Dictionary<(Vector2I Key, int Lod), MultiMeshInstance3D> tiles = new();
    private readonly Queue<(Vector2I Key, int Lod)> pending = new();
    private readonly ShaderMaterial material = new() { Shader = GD.Load<Shader>("res://Shaders/Grass.gdshader") };
    private readonly Vector4[] trail = new Vector4[TrailSize];
    private World world = null!;
    private Vehicle car = null!;
    private readonly Mesh[] tufts = new Mesh[3];
    private Vector3 lastFocus = new(float.PositiveInfinity, 0, 0);
    private float refreshTimer;
    private Vector3 previousCar;
    private int trailIndex;
    private float trailTimer;
    private float grassTime;
    internal int TileCount => System.Linq.Enumerable.Count(System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(tiles.Keys, key => key.Key)));
    internal int TuftCount { get; private set; }
    internal int LodTileCount(int lod) => System.Linq.Enumerable.Count(tiles.Keys, key => key.Lod == lod);
    internal Dictionary<(Vector2I Key, int Lod), ulong> LayerIdentities()
    {
        var result = new Dictionary<(Vector2I, int), ulong>();
        foreach (var pair in tiles) result[pair.Key] = pair.Value.GetInstanceId();
        return result;
    }

    public void Setup(World world, Vehicle car)
    {
        this.world = world; this.car = car;
        previousCar = car.GlobalPosition;
        for (int lod = 0; lod < 3; lod++) tufts[lod] = CreateTuft(lod);
        UpdateInteraction(0);
    }

    public override void _Process(double delta)
    {
        if (world == null) return;
        UpdateInteraction((float)delta);
        grassTime += (float)delta;
        material.SetShaderParameter("grass_time", grassTime);
        var focus = GetViewport().GetCamera3D()?.GlobalPosition ?? car.GlobalPosition;
        refreshTimer -= (float)delta;
        if (refreshTimer <= 0 || focus.DistanceSquaredTo(lastFocus) > 16)
        {
            RefreshTiles(focus);
            lastFocus = focus; refreshTimer = .3f;
        }
        // Budget by complexity: distant tiles contain far fewer instances and triangles.
        int budget = 24;
        while (pending.TryPeek(out var job))
        {
            int cost = job.Lod == 0 ? 6 : job.Lod == 1 ? 2 : 1;
            if (cost > budget) break;
            pending.Dequeue(); budget -= cost;
            AddTile(job.Key, job.Lod);
        }
    }

    private void RefreshTiles(Vector3 focus)
    {
        var cell = new Vector2I(Mathf.FloorToInt(focus.X / TileSize), Mathf.FloorToInt(focus.Z / TileSize));
        var wanted = new List<Vector2I>();
        for (int z = -18; z <= 18; z++)
        for (int x = -18; x <= 18; x++)
        {
            var key = cell + new Vector2I(x, z);
            if (Distance(key) < 282 && new Vector2(key.X * TileSize + 8, key.Y * TileSize + 8).Length() < Settings.PlayableRadius + 12)
                wanted.Add(key);
        }
        var wantedSet = new HashSet<Vector2I>(wanted);
        foreach (var job in new List<(Vector2I Key, int Lod)>(tiles.Keys))
            if (!wantedSet.Contains(job.Key) || Distance(job.Key) > LayerRadius(job.Lod) + 16)
            {
                TuftCount -= tiles[job].Multimesh.InstanceCount;
                tiles[job].QueueFree(); tiles.Remove(job);
            }
        wanted.Sort((a, b) => (a - cell).LengthSquared().CompareTo((b - cell).LengthSquared()));
        pending.Clear();
        foreach (var key in wanted)
        {
            // Stable, disjoint density layers: no tile ever swaps its mesh or root positions.
            // Load outside the shader fade band so normal travel cannot expose a tile edge.
            for (int lod = 0; lod < 3; lod++)
                if (Distance(key) < LayerRadius(lod) && !tiles.ContainsKey((key, lod)))
                    pending.Enqueue((key, lod));
        }
        float Distance(Vector2I key) => new Vector2(key.X * TileSize + 8 - focus.X, key.Y * TileSize + 8 - focus.Z).Length();
    }

    private static float LayerRadius(int lod) => lod == 0 ? 110 : lod == 1 ? 190 : 282;

    internal bool CanGrow(Vector3 p)
    {
        foreach (var spot in world.SpecialPickupSpots)
            if (new Vector2(p.X - spot.X, p.Z - spot.Z).LengthSquared() < 4) return false;
        if (new Vector2(p.X, p.Z).Length() > Settings.PlayableRadius - 3 ||
            River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 3 || River.OnBridge(p.X, p.Z) ||
            world.Village.IsReserved(p) || world.Routes.SurfaceHeight(p, p.Y) > p.Y + .05f ||
            world.Plants.NearTrunk(p, .65f)) return false;
        foreach (var animal in world.Animals)
            if (new Vector2(p.X - animal.Position.X, p.Z - animal.Position.Z).LengthSquared() < 5) return false;
        return true;
    }

    private void AddTile(Vector2I key, int lod)
    {
        var rng = new RandomNumberGenerator { Seed = unchecked((ulong)(Settings.Seed + key.X * 73856093L + key.Y * 19349663L)) };
        var transforms = new List<Transform3D>();
        var colors = new List<Color>();
        var origin = new Vector3(key.X * TileSize, 0, key.Y * TileSize);
        const int rows = 28;
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < rows; x++)
        {
            float px = origin.X + (x + rng.Randf()) * TileSize / rows;
            float pz = origin.Z + (z + rng.Randf()) * TileSize / rows;
            float height = rng.RandfRange(.65f, 1.25f);
            float yaw = rng.RandfRange(0, Mathf.Tau), width = rng.RandfRange(.8f, 1.25f);
            var color = new Color(rng.Randf(), rng.Randf(), rng.Randf(), 1);
            // Persistent far roots, a middle-density layer, and extra near roots.
            // Each root belongs to exactly one layer and retains its shape at every distance.
            int layer = x % 4 == 0 && z % 4 == 0 ? 2 : x % 2 == 0 && z % 2 == 0 ? 1 : 0;
            if (layer != lod) continue;
            color.G = lod * .5f;
            color.B = grassTime;
            var p = world.Ground.At(px, pz, -.025f);
            if (!CanGrow(p)) continue;
            transforms.Add(new Transform3D(new Basis(Vector3.Up, yaw)
                .ScaledLocal(new Vector3(width * (1 + lod * .55f), height, 1 + lod * .55f)), p - origin));
            colors.Add(color);
        }
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true, Mesh = tufts[lod], InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++)
        {
            mm.SetInstanceTransform(i, transforms[i]); mm.SetInstanceCustomData(i, colors[i]);
        }
        var node = new MultiMeshInstance3D { Position = origin, Multimesh = mm, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 2 };
        AddChild(node); tiles[(key, lod)] = node; TuftCount += transforms.Count;
    }

    internal void UpdateInteraction(float dt)
    {
        var position = car.GlobalPosition;
        float distance = position.DistanceTo(previousCar);
        if (distance > 8) { System.Array.Clear(trail); trailTimer = 0; }
        for (int i = 0; i < TrailSize; i++) trail[i].Z = Mathf.Max(0, trail[i].Z - dt / 1.6f);
        trailTimer += dt;
        if (car.Active && distance > .01f && distance < 8 && trailTimer >= .15f && car.IsOnFloor())
        {
            trail[trailIndex] = new Vector4(position.X, position.Z, 1, position.Y);
            trailIndex = (trailIndex + 1) % TrailSize; trailTimer = 0;
        }
        previousCar = position;
        material.SetShaderParameter("car_position", position);
        material.SetShaderParameter("car_forward", -car.GlobalBasis.Z);
        material.SetShaderParameter("car_speed", car.Active ? Mathf.Abs(car.Speed) : 0);
        material.SetShaderParameter("trail", trail);
    }

    private static Mesh CreateTuft(int lod)
    {
        var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
        for (int blade = 0; blade < (lod == 0 ? 5 : lod == 1 ? 3 : 2); blade++)
        {
            float a = blade * 2.39996f;
            var side = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var root = new Vector3(Mathf.Sin(a) * .19f, 0, Mathf.Cos(a) * .19f);
            float height = .43f + blade % 3 * .1f;
            var middle = root + Vector3.Up * height * .55f + side * .025f;
            var tip = root + Vector3.Up * height + side * .11f;
            void V(Vector3 p, float t)
            {
                st.SetUV(new Vector2(0, t)); st.SetNormal(Vector3.Up); st.AddVertex(p);
            }
            if (lod > 0)
            {
                V(root - side * .055f, 0); V(tip, 1); V(root + side * .055f, 0);
                continue;
            }
            V(root - side * .045f, 0); V(middle - side * .028f, .55f); V(root + side * .045f, 0);
            V(root + side * .045f, 0); V(middle - side * .028f, .55f); V(middle + side * .028f, .55f);
            V(middle - side * .028f, .55f); V(tip, 1); V(middle + side * .028f, .55f);
        }
        return st.Commit();
    }
}
