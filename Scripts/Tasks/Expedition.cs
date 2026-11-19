using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Tasks;

public sealed record DeliveryChallenge(string Cargo, int Answer) : IChallenge
{
    public IReadOnlyList<int> Choices => new[] { 1, 2, 3 };
    public string Expression => "";

    public bool Accepts(int choice) => choice == Answer;
}

/// <summary>A roadside delivery. Navigation follows the connected road samples, including the bridge.</summary>
public partial class Expedition : Node3D
{
    public Character Sender { get; }
    public Character Recipient { get; }
    public bool Collection { get; }
    public bool Farm => Sender.Role is FarmerRole;
    public bool Garden => Sender.Role is GardenerRole;
    public bool Gems => Sender.Role is ExhibitCollectorRole { Toys: false };
    public bool Toys => Sender.Role is ExhibitCollectorRole { Toys: true };
    public bool Golem => Sender.Role is SorcererRole;
    public bool SpecialCollection => Farm || Garden || Gems || Toys || Golem;
    public int ItemIndex { get; }
    public string FarmName => Village.FarmNames[ItemIndex];
    public string FlowerName => Village.FlowerNames[ItemIndex];
    public string CollectionName => Village.CollectionNames(Toys)[ItemIndex];

    /// <summary>What the item is called on a sign and on a panel: the name itself, not the form after "přivez".</summary>
    public string FarmTitle => Village.FarmTitles[ItemIndex];
    public string FlowerTitle => Village.FlowerTitles[ItemIndex];
    public string CollectionTitle => Village.CollectionTitles(Toys)[ItemIndex];
    public string Cargo =>
        Sender.Role is TraderRole shop ? Village.TradeCargo[shop.Index]
        : Collection ? "apple"
        : Sender.Kind == 0 ? "carrot"
        : Sender.Kind == 1 ? "apple"
        : "honey";
    public int Amount { get; }
    public int Collected { get; private set; }
    public bool CanDeliver => !Collection || Collected == Amount;
    internal Vector3[] PickupPositions { get; }
    private readonly bool[] picked;
    private readonly int specialPickupOffset;
    internal bool[] Picked => (bool[])picked.Clone();
    private Node3D[] pickupModels = Array.Empty<Node3D>();
    public Vector3 Destination => CanDeliver ? Recipient.Position : PickupPositions[Array.FindIndex(picked, p => !p)];
    public bool Arrived { get; set; }
    private readonly World world;
    private readonly Vehicle car;
    private Node3D? load;
    private bool cargoDetached;
    private WildAnimal? transportedAnimal;
    private WildAnimal? waitingAnimal;
    private readonly RouteGuide guide;
    public Vector3 Waypoint => guide.Waypoint;
    internal bool CargoVisible => !cargoDetached && load != null && load.IsVisibleInTree();
    internal int RouteLength => guide.RouteLength;

    /// <summary>
    /// A stable number for this sender's trip that picks the delivery recipient and amount. It depends only
    /// on saved progress, so a restored expedition matches the one that was saved. Traders count their
    /// finished orders to vary repeat trips; residents follow the roadside friends.
    /// </summary>
    private static int TripNumber(World world, Character sender) =>
        sender.Role switch
        {
            TraderRole trader => trader.Index + world.Village.TradeCounts[trader.Index],
            HouseResidentRole resident => world.Animals.Length + resident.House,
            _ => Array.IndexOf(world.Animals, sender),
        };

    public Expedition(World world, Vehicle car, Character sender, bool collection, bool[]? restoredPickups = null)
    {
        this.world = world;
        this.car = car;
        Sender = sender;
        // Existing clearings guarantee that both ends are reachable from a road.
        var candidates = world
            .Animals.Where(a => a != sender)
            .OrderBy(a => a.Position.DistanceSquaredTo(sender.Position))
            .Take(5)
            .ToArray();
        int trip = TripNumber(world, sender);
        Collection = collection;
        var collector = sender.Role as CollectorRole;
        ItemIndex = collector?.Count(world.Village) ?? 0;
        Recipient = Collection ? sender : candidates[trip % candidates.Length];
        Amount =
            Golem ? 6
            : SpecialCollection ? 1
            : Collection ? 3
            : (trip / 3 + trip) % 3 + 1;
        Collected = Collection ? 0 : Amount;
        PickupPositions = Collection
            ? candidates
                .Take(3)
                .Select(a => world.Routes.Samples[RouteGuide.NearestSample(world, a.Position)])
                .ToArray()
            : Array.Empty<Vector3>();
        if (Golem)
        {
            Recipient = sender;
            PickupPositions = world.SpecialPickupSpots.Take(6).ToArray();
        }
        else if (collector != null)
        {
            Recipient = sender;
            specialPickupOffset = collector.PickupSlotOffset + ItemIndex;
            PickupPositions = new[] { world.SpecialPickupSpots[specialPickupOffset] };
        }
        picked = new bool[PickupPositions.Length];
        if (Collection && restoredPickups != null)
        {
            Array.Copy(restoredPickups, picked, picked.Length);
            Collected = picked.Count(p => p);
        }
        guide = new RouteGuide(world, car, this, () => Destination);
        RebuildRoute();
    }

    private void RebuildRoute() =>
        guide.Retarget(
            SpecialCollection && !CanDeliver
                ? world.PickupApproaches[specialPickupOffset + Array.FindIndex(picked, collected => !collected)].Road
                : null
        );

    public override void _Ready()
    {
        load = new Node3D { Name = "DeliveryCargo", Position = new(0, 2.4f, .3f) };
        car.AddChild(load);
        foreach (float x in new[] { -.65f, .65f })
            Art.Block(load, new(x, -.23f, 0), new(.1f, .4f, .8f), "795b43");
        Art.Block(load, Vector3.Zero, new(1.8f, .28f, 1.05f), "bc8b61");
        int loaded = 0;
        for (int i = 0; i < picked.Length; i++)
            if (picked[i])
            {
                AddLoadAnimalOrCargo(
                    Golem
                        ? new(-.65f + (loaded % 3) * .65f, .5f, -.25f + (loaded / 3) * .5f)
                        : new(-.6f + loaded * .6f, .5f, 0),
                    i
                );
                loaded++;
            }
        if (!Collection)
            for (int i = 0; i < Collected; i++)
                AddLoadAnimalOrCargo(new(-.6f + i * .6f, .5f, 0));
        pickupModels = new Node3D[PickupPositions.Length];
        for (int i = 0; i < PickupPositions.Length; i++)
        {
            var p = PickupPositions[i];
            float y = SpecialCollection
                ? world.Ground.Height(p.X, p.Z)
                : world.Routes.SurfaceHeight(p, world.Ground.Height(p.X, p.Z));
            if (River.OnBridge(p.X, p.Z))
                y = Mathf.Max(y, River.BridgeSurfaceHeight(p.X));
            PickupPositions[i] = new(p.X, y, p.Z);
            var pickup = new Node3D
            {
                Name =
                    (
                        Golem ? "GolemPart"
                        : Gems ? "CollectibleGem"
                        : Toys ? "CollectibleToy"
                        : Garden ? "CollectibleFlower"
                        : Farm ? "CatchableAnimal"
                        : "CollectibleApple"
                    ) + i,
                Position = PickupPositions[i],
            };
            AddChild(pickup);
            pickupModels[i] = pickup;
            if (Golem)
            {
                GolemModel.Part(pickup, i, Vector3.Up * .7f);
                Art.Sign(pickup, Vector3.Up * 3.5f, "Dílek golema", 28);
            }
            else if (Farm)
            {
                var animal = new WildAnimal(Village.FarmKinds[ItemIndex], .5f) { Scale = Vector3.One * 1.5f };
                waitingAnimal = animal;
                pickup.AddChild(animal);
                animal.Animate(0, false);
                Art.Sign(pickup, Vector3.Up * 3.5f, FarmTitle, 28);
            }
            else if (Gems || Toys)
            {
                var item = Toys
                    ? Village.CreateToy(pickup, Vector3.Zero, ItemIndex)
                    : Village.CreateGem(pickup, Vector3.Zero, ItemIndex);
                item.Scale = Vector3.One * 1.5f;
                Art.Sign(pickup, Vector3.Up * 3.5f, CollectionTitle, 28);
            }
            else if (Garden)
            {
                var flower = Village.CreateFlower(pickup, Vector3.Zero, ItemIndex);
                flower.Scale = Vector3.One * 1.7f;
                Art.Sign(pickup, Vector3.Up * 3.5f, FlowerTitle, 28);
            }
            else
            {
                var fruit = new Node3D { Scale = Vector3.One * 3 };
                pickup.AddChild(fruit);
                AddCargo(fruit, Vector3.Up * .45f, Cargo);
            }
            Art.TaskMarker(pickup, Vector3.Up * 5, "!", true);
            if (picked[i])
                pickup.Hide();
        }
        guide.CreateArrows();
    }

    internal static void AddCargo(Node3D parent, Vector3 p, string cargo)
    {
        if (cargo == "bread")
        {
            Art.Ball(parent, p, new(.33f, .22f, .22f), "d99b52");
            for (int i = -1; i <= 1; i++)
                Art.Block(parent, p + new Vector3(i * .14f, .2f, 0), new(.045f, .025f, .25f), "ffe1a4");
        }
        else if (cargo == "parcel")
        {
            Art.Block(parent, p, new(.53f, .5f, .46f), "bf9565");
            Art.Block(parent, p + Vector3.Up * .26f, new(.12f, .025f, .47f), "fff0cf");
            Art.Block(parent, p + new Vector3(0, 0, .24f), new(.12f, .5f, .025f), "fff0cf");
        }
        else if (cargo == "cake")
        {
            Art.Cylinder(parent, p, .27f, .27f, .38f, "e9a0be");
            Art.Cylinder(parent, p + Vector3.Up * .22f, .28f, .28f, .1f, "fff2dc");
            Art.Ball(parent, p + Vector3.Up * .34f, Vector3.One * .1f, "ed586e");
        }
        else if (cargo == "carrot")
        {
            Art.Cylinder(parent, p, .04f, .19f, .75f, "f59242");
            for (int k = -1; k <= 1; k++)
                Art.Ball(parent, p + new Vector3(k * .12f, .5f, 0), new(.08f, .25f, .07f), "70b765");
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

    private void AddLoadAnimalOrCargo(Vector3 p, int pieceIndex = 0)
    {
        if (Golem)
        {
            GolemModel.Part(load!, pieceIndex, p);
            return;
        }
        if (Gems || Toys)
        {
            var item = Toys
                ? Village.CreateToy(load!, Vector3.Up * .15f, ItemIndex)
                : Village.CreateGem(load!, Vector3.Up * .15f, ItemIndex);
            item.Scale = Vector3.One * .85f;
            return;
        }
        if (Garden)
        {
            Art.Cylinder(load!, new(0, .3f, 0), .25f, .4f, .6f, "cd8962");
            Village.CreateFlower(load!, new(0, .6f, 0), ItemIndex);
            return;
        }
        if (!Farm)
        {
            AddCargo(load!, p, Cargo);
            return;
        }
        var animal = new WildAnimal(Village.FarmKinds[ItemIndex], .5f)
        {
            Position = new(0, .15f, 0),
            Scale = Vector3.One * .85f,
        };
        load!.AddChild(animal);
        animal.Animate(0, false);
        transportedAnimal = animal;
    }

    internal Node3D? DetachCargo()
    {
        if (load == null || cargoDetached)
            return null;
        cargoDetached = true;
        var delivered = new Node3D { Name = "HandedOverCargo", Transform = load.Transform };
        car.AddChild(delivered);
        // The first three meshes form the carrier. Only its contents fly to the recipient.
        foreach (var item in load.GetChildren().Skip(3).ToArray())
            if (item is Node3D model)
                model.Reparent(delivered, true);
        return delivered;
    }

    internal void ReleaseCargo()
    {
        if (load == null || !GodotObject.IsInstanceValid(load))
            return;
        load.Hide();
        load.GetParent()?.RemoveChild(load);
        load.QueueFree();
        load = null;
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
                    picked[i] = true;
                    pickupModels[i].Hide();
                    AddLoadAnimalOrCargo(new(-.65f + (Collected % 3) * .65f, .5f, -.25f + (Collected / 3) * .5f), i);
                    Collected++;
                    changed = true;
                }
            if (changed)
                RebuildRoute();
        }
        guide.Tick(delta);
    }

    private static void AnimateFarmAnimal(WildAnimal? animal, float delta)
    {
        if (animal == null || !GodotObject.IsInstanceValid(animal))
            return;
        animal.Phase += delta;
        animal.Animate(delta, false);
    }
}
