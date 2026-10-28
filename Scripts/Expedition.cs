using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public sealed record DeliveryChallenge(string Cargo, int Answer) : IChallenge
{
    public IReadOnlyList<int> Choices => new[] { 1, 2, 3 };
    public string Expression => "";
}

/// <summary>A roadside delivery. Navigation follows the connected road samples, including the bridge.</summary>
public partial class Expedition : Node3D
{
    public Animal Sender { get; }
    public Animal Recipient { get; }
    public bool Collection { get; }
    public bool Farm => Sender.IsFarmer;
    public bool Garden => Sender.IsGardener;
    public bool Gems => Sender.IsGemCollector;
    public bool Toys => Sender.IsToymaker;
    public bool SpecialCollection => Farm || Garden || Gems || Toys;
    public int FarmIndex { get; }
    public string FarmName => Village.FarmNames[FarmIndex];
    public string FlowerName => Village.FlowerNames[FarmIndex];
    public string CollectionName => Village.CollectionNames(Toys)[FarmIndex];
    public string Cargo => Collection ? "apple" : Sender.Kind == 0 ? "carrot" : Sender.Kind == 1 ? "apple" : "honey";
    public int Amount { get; }
    public int Collected { get; private set; }
    public bool CanDeliver => !Collection || Collected == Amount;
    internal Vector3[] PickupPositions { get; }
    private readonly bool[] picked;
    internal bool[] Picked => (bool[])picked.Clone();
    private Node3D[] pickupModels = Array.Empty<Node3D>();
    public Vector3 Destination => CanDeliver ? Recipient.Position : PickupPositions[Array.FindIndex(picked, p => !p)];
    public bool Arrived { get; set; }
    private readonly World world;
    private readonly Vehicle car;
    private Node3D? load;
    private WildAnimal? transportedAnimal;
    private WildAnimal? waitingAnimal;
    private readonly List<int>[] edges;
    private readonly int[] next;
    private readonly Dictionary<(int Anchor, bool Reverse), MeshInstance3D> routeArrows = new();
    private readonly List<MeshInstance3D> visibleArrows = new();
    private Mesh arrowMesh = null!;
    private Material arrowMaterial = null!;
    private float refresh;
    public Vector3 Waypoint { get; private set; }
    internal bool CargoVisible => load != null && load.IsVisibleInTree();
    internal int RouteLength { get; private set; }

    public Expedition(World world, Vehicle car, Animal sender, bool collection, bool[]? restoredPickups = null)
    {
        this.world = world; this.car = car; Sender = sender;
        // Existing clearings guarantee that both ends are reachable from a road.
        var candidates = world.Animals.Where(a => a != sender).OrderBy(a => a.Position.DistanceSquaredTo(sender.Position)).Take(5).ToArray();
        int index = sender.HouseIndex >= 0 ? world.Animals.Length + sender.HouseIndex : Array.IndexOf(world.Animals, sender);
        Collection = collection;
        FarmIndex = Farm ? world.Village.FarmCount : Garden ? world.Village.GardenCount : Gems ? world.Village.GemCount : Toys ? world.Village.ToyCount : 0;
        Recipient = Collection ? sender : candidates[index % candidates.Length];
        Amount = SpecialCollection ? 1 : Collection ? 3 : (index / 3 + index) % 3 + 1;
        Collected = Collection ? 0 : Amount;
        PickupPositions = Collection ? candidates.Take(3).Select(a => world.Routes.Samples[Nearest(a.Position)]).ToArray() : Array.Empty<Vector3>();
        if (SpecialCollection)
        {
            Recipient = sender;
            int offset = Garden ? 6 : Gems ? 12 : Toys ? 18 : 0;
            PickupPositions = new[] { world.SpecialPickupSpots[FarmIndex + offset] };
        }
        picked = new bool[PickupPositions.Length];
        if (Collection && restoredPickups != null)
        {
            Array.Copy(restoredPickups, picked, picked.Length);
            Collected = picked.Count(p => p);
        }
        var samples = world.Routes.Samples;
        edges = Enumerable.Range(0, samples.Count).Select(_ => new List<int>()).ToArray();
        next = Enumerable.Repeat(-1, samples.Count).ToArray();
        var buckets = new Dictionary<Vector2I, List<int>>();
        for (int i = 0; i < samples.Count; i++)
        {
            var p = samples[i]; var key = new Vector2I(Mathf.FloorToInt(p.X / 4), Mathf.FloorToInt(p.Z / 4));
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
                if (buckets.TryGetValue(key + new Vector2I(x, z), out var neighbors))
                    foreach (int j in neighbors)
                        if (p.DistanceSquaredTo(samples[j]) < 10.24f) { edges[i].Add(j); edges[j].Add(i); }
            if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = new();
            bucket.Add(i);
        }
        RebuildRoute();
    }
    private void RebuildRoute()
    {
        var samples = world.Routes.Samples;
        Array.Fill(next, -1);
        int target = Nearest(Destination);
        var costs = Enumerable.Repeat(float.PositiveInfinity, samples.Count).ToArray();
        var queue = new PriorityQueue<int, float>(); costs[target] = 0; queue.Enqueue(target, 0);
        while (queue.TryDequeue(out int i, out float cost))
        {
            if (cost > costs[i]) continue;
            foreach (int j in edges[i])
            {
                float candidate = cost + samples[i].DistanceTo(samples[j]);
                if (candidate >= costs[j]) continue;
                costs[j] = candidate; next[j] = i; queue.Enqueue(j, candidate);
            }
        }
        if (costs.Any(float.IsPositiveInfinity)) throw new InvalidOperationException("Delivery road navigation is disconnected.");
    }

    private int Nearest(Vector3 p)
    {
        int best = 0; float distance = float.PositiveInfinity;
        for (int i = 0; i < world.Routes.Samples.Count; i++)
        {
            float d = world.Routes.Samples[i].DistanceSquaredTo(p);
            if (d < distance) { best = i; distance = d; }
        }
        return best;
    }

    public override void _Ready()
    {
        load = new Node3D { Name = "DeliveryCargo", Position = new(0, 2.4f, .3f) };
        car.AddChild(load);
        foreach (float x in new[] { -.65f, .65f }) Art.Block(load, new(x, -.23f, 0), new(.1f, .4f, .8f), "795b43");
        Art.Block(load, Vector3.Zero, new(1.8f, .28f, 1.05f), "bc8b61");
        for (int i = 0; i < Collected; i++) AddLoadAnimalOrCargo(new(-.6f + i * .6f, .5f, 0));
        pickupModels = new Node3D[PickupPositions.Length];
        for (int i = 0; i < PickupPositions.Length; i++)
        {
            var p = PickupPositions[i]; float y = SpecialCollection ? world.Ground.Height(p.X, p.Z) : world.Routes.SurfaceHeight(p, world.Ground.Height(p.X, p.Z));
            if (River.OnBridge(p.X, p.Z)) y = Mathf.Max(y, River.BridgeSurfaceHeight(p.X));
            PickupPositions[i] = new(p.X, y, p.Z);
            var pickup = new Node3D { Name = (Gems ? "CollectibleGem" : Toys ? "CollectibleToy" : Garden ? "CollectibleFlower" : Farm ? "CatchableAnimal" : "CollectibleApple") + i, Position = PickupPositions[i] }; AddChild(pickup); pickupModels[i] = pickup;
            if (Farm)
            {
                var animal = new WildAnimal(Village.FarmKinds[FarmIndex], .5f) { Scale = Vector3.One * 1.5f };
                waitingAnimal = animal;
                pickup.AddChild(animal); animal.Animate(0, false);
                Art.Sign(pickup, Vector3.Up * 3.5f, FarmName, 28);
            }
            else if (Gems || Toys)
            {
                var item = Toys ? Village.CreateToy(pickup, Vector3.Zero, FarmIndex) : Village.CreateGem(pickup, Vector3.Zero, FarmIndex);
                item.Scale = Vector3.One * 1.5f;
                Art.Sign(pickup, Vector3.Up * 3.5f, CollectionName, 28);
            }
            else if (Garden)
            {
                var flower = Village.CreateFlower(pickup, Vector3.Zero, FarmIndex); flower.Scale = Vector3.One * 1.7f;
                Art.Sign(pickup, Vector3.Up * 3.5f, FlowerName, 28);
            }
            else
            {
                var fruit = new Node3D { Scale = Vector3.One * 3 }; pickup.AddChild(fruit); AddCargo(fruit, Vector3.Up * .45f, Cargo);
            }
            Art.TaskMarker(pickup, Vector3.Up * 5, "!", true);
            if (picked[i]) pickup.Hide();
        }
        var arrowBuilder = new SurfaceTool(); arrowBuilder.Begin(Mesh.PrimitiveType.Triangles);
        // A flat chevron points along local -Z; double-sided material also covers steep grades.
        var outline = new Vector3[] { new(0, 0, -1.2f), new(.72f, 0, -.35f), new(.28f, 0, -.35f), new(.28f, 0, .85f), new(-.28f, 0, .85f), new(-.28f, 0, -.35f), new(-.72f, 0, -.35f) };
        foreach (int vertex in new[] { 0, 1, 2, 0, 2, 5, 0, 5, 6, 2, 3, 4, 2, 4, 5 }) arrowBuilder.AddVertex(outline[vertex]);
        arrowMesh = arrowBuilder.Commit();
        arrowMaterial = new StandardMaterial3D { AlbedoColor = new Color("27bd62"), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        UpdateGuidance();
    }

    internal static void AddCargo(Node3D parent, Vector3 p, string cargo)
    {
        if (cargo == "carrot")
        {
            Art.Cylinder(parent, p, .04f, .19f, .75f, "f59242");
            for (int k = -1; k <= 1; k++) Art.Ball(parent, p + new Vector3(k * .12f, .5f, 0), new(.08f, .25f, .07f), "70b765");
        }
        else if (cargo == "apple")
        {
            Art.Ball(parent, p, new(.25f, .28f, .25f), "ed776b");
            Art.Cylinder(parent, p + Vector3.Up * .29f, .035f, .035f, .18f, "795b43");
            Art.Ball(parent, p + new Vector3(.12f, .36f, 0), new(.17f, .045f, .08f), "70b765");
        }
        else
        {
            Art.Cylinder(parent, p, .23f, .21f, .48f, "f1bb43");
            Art.Cylinder(parent, p + Vector3.Up * .28f, .25f, .25f, .12f, "a27452");
            Art.Ball(parent, p + new Vector3(0, 0, -.2f), new(.12f, .14f, .04f), "fff5df");
        }
    }

    private void AddLoadAnimalOrCargo(Vector3 p)
    {
        if (Gems || Toys)
        {
            var item = Toys ? Village.CreateToy(load!, Vector3.Up * .15f, FarmIndex) : Village.CreateGem(load!, Vector3.Up * .15f, FarmIndex);
            item.Scale = Vector3.One * .85f; return;
        }
        if (Garden)
        {
            Art.Cylinder(load!, new(0, .3f, 0), .25f, .4f, .6f, "cd8962");
            Village.CreateFlower(load!, new(0, .6f, 0), FarmIndex); return;
        }
        if (!Farm) { AddCargo(load!, p, Cargo); return; }
        var animal = new WildAnimal(Village.FarmKinds[FarmIndex], .5f) { Position = new(0, .15f, 0), Scale = Vector3.One * .85f };
        load!.AddChild(animal); animal.Animate(0, false);
        transportedAnimal = animal;
    }

    internal void ReleaseCargo()
    {
        if (load == null || !GodotObject.IsInstanceValid(load)) return;
        load.Hide();
        load.GetParent()?.RemoveChild(load); load.QueueFree(); load = null;
    }
    public override void _ExitTree() => ReleaseCargo();
    public void Tick(double delta)
    {
        AnimateFarmAnimal(waitingAnimal, (float)delta);
        AnimateFarmAnimal(transportedAnimal, (float)delta);
        if (Collection && !CanDeliver)
        {
            bool changed = false;
            for (int i = 0; i < picked.Length; i++)
                if (!picked[i] && car.Position.DistanceTo(PickupPositions[i]) < (SpecialCollection ? 2.8f : 4))
                {
                    picked[i] = true; pickupModels[i].Hide();
                    AddLoadAnimalOrCargo(new(-.6f + Collected * .6f, .5f, 0)); Collected++; changed = true;
                }
            if (changed) { RebuildRoute(); refresh = 0; }
        }
        refresh -= (float)delta;
        if (refresh <= 0) { refresh = .25f; UpdateGuidance(); }
    }
    private static void AnimateFarmAnimal(WildAnimal? animal, float delta)
    {
        if (animal == null || !GodotObject.IsInstanceValid(animal)) return;
        animal.Phase += delta; animal.Animate(delta, false);
    }
    private void UpdateGuidance()
    {
        var points = new List<Vector3>();
        var nodes = new List<int>();
        int node = Nearest(car.Position); RouteLength = 0;
        while (node >= 0 && RouteLength++ < next.Length)
        {
            nodes.Add(node); points.Add(world.Routes.Samples[node]); node = next[node];
        }
        points.Add(Destination);
        // Aim ahead along the road instead of across the river or through trees.
        Waypoint = points[Math.Min(8, points.Count - 1)];
        foreach (var arrow in visibleArrows) arrow.Hide();
        visibleArrows.Clear();
        var shownAnchors = new HashSet<int>();
        for (int sample = 0; sample < Math.Min(54, nodes.Count - 1); sample++)
        {
            int anchorIndex = world.Routes.SampleAnchors[nodes[sample]];
            if (!shownAnchors.Add(anchorIndex)) continue;
            var anchor = world.Routes.NavigationAnchors[anchorIndex];
            bool reverse = anchor.Forward.Dot(points[sample + 1] - points[sample]) < 0;
            var key = (anchorIndex, reverse);
            if (!routeArrows.TryGetValue(key, out var arrow))
            {
                var p = anchor.Position;
                var forward = reverse ? -anchor.Forward : anchor.Forward;
                if (forward.LengthSquared() < .001f) continue;
                float y = world.Routes.SurfaceHeight(p, world.Ground.Height(p.X, p.Z));
                if (River.OnBridge(p.X, p.Z)) y = Mathf.Max(y, River.BridgeSurfaceHeight(p.X));
                arrow = new MeshInstance3D
                {
                    Name = "RouteArrow" + routeArrows.Count, Mesh = arrowMesh, MaterialOverride = arrowMaterial,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(p.X, y + .35f, p.Z),
                    Rotation = new Vector3(0, Mathf.Atan2(-forward.X, -forward.Z), 0)
                };
                routeArrows.Add(key, arrow); AddChild(arrow);
            }
            arrow.Show(); visibleArrows.Add(arrow);
        }
    }
}
