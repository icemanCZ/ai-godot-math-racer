using System.Linq;
using Godot;

namespace MathRacer;

/// <summary>
/// One trailer trip: drive to a source, answer the loading question, then tip the load out at the drop
/// point. The contractor (the beaver or a works' worker) asks the question and thanks the driver. Road
/// arrows and the target ring belong to this node.
/// </summary>
public abstract partial class HaulTrip : Node3D
{
    private readonly World world;
    private readonly RouteGuide guide;
    private readonly Vector3 dropPoint;
    private readonly Vector3 dropRoad;
    private Node3D? target;

    internal HaulLoad Load { get; }
    internal Animal Contractor { get; }

    /// <summary>The character who loads the cargo and asks the loading question: the source worker, else the contractor.</summary>
    internal Animal Asker => world.PickupActor(Load, Source) ?? Contractor;
    internal bool Loaded { get; private set; }

    /// <summary>Set on reaching the current target; cleared when the car leaves it again.</summary>
    internal bool Arrived { get; set; }

    /// <summary>The pickup site the trip was sent to: the nearest one when the trip began (a raw material has two).</summary>
    internal HaulSource Source { get; private set; }
    internal Vector3 Destination => Loaded ? dropPoint : Source.Position;
    internal Vector3 Waypoint => guide.Waypoint;

    /// <summary>Short text for the HUD, such as the number of the trip; empty when there is none.</summary>
    internal virtual string Counter => "";

    /// <summary>Whether the loaded trailer is taken to the contractor (shown in the HUD) rather than elsewhere.</summary>
    internal virtual bool DropsAtContractor => true;

    private Vector3 RoadApproach => !Loaded ? Source.Road : dropRoad;

    private protected HaulTrip(
        World world,
        Vehicle car,
        HaulLoad load,
        Vector3 dropPoint,
        Vector3 dropRoad,
        Animal contractor,
        bool loaded
    )
    {
        this.world = world;
        this.dropPoint = dropPoint;
        this.dropRoad = dropRoad;
        Load = load;
        Source = world.NearestSource(load, car.Position);
        Contractor = contractor;
        Loaded = loaded;
        guide = new RouteGuide(world, car, this, () => Destination);
        guide.Retarget(RoadApproach);
    }

    public override void _Ready()
    {
        target = new Node3D { Name = "HaulTarget" };
        AddChild(target);
        Art.Cylinder(target, Vector3.Up * .05f, 3.3f, 3.3f, .1f, "f1c232");
        Art.Cylinder(target, Vector3.Up * .07f, 2.7f, 2.7f, .1f, "fff1b8");
        Art.TaskMarker(target, Vector3.Up * 5, "!", true);
        guide.CreateArrows();
        PlaceTarget();
    }

    internal void SetLoaded(bool loaded)
    {
        Loaded = loaded;
        Arrived = false;
        guide.Retarget(RoadApproach);
        PlaceTarget();
    }

    internal void Tick(double delta) => guide.Tick(delta);

    internal bool Reached(Vector3 car) => Loaded ? Near(car, Destination, 4.5f) : AnySource(car);

    private static bool Near(Vector3 car, Vector3 point, float range) =>
        new Vector2(car.X - point.X, car.Z - point.Z).Length() < range;

    /// <summary>An empty trailer is loaded at whichever site of the material it reaches first.</summary>
    private bool AnySource(Vector3 car)
    {
        float range = HaulLoads.IsSpecial(Load) ? Settings.InteractionRange : 6;
        foreach (var site in world.SourceSites(Load))
        {
            if (!Near(car, site.Position, range))
                continue;
            // The worker of the site that was reached asks the loading question, wherever the trip was sent.
            if (site != Source)
            {
                Source = site;
                PlaceTarget();
            }
            return true;
        }
        return false;
    }

    private void PlaceTarget()
    {
        if (target == null)
            return;
        var p = Destination;
        target.Position = new Vector3(p.X, world.Ground.Height(p.X, p.Z), p.Z);
    }
}

/// <summary>One of the ten deliveries to a building.</summary>
public partial class BuildTrip : HaulTrip
{
    internal BuildProject Project { get; }
    internal int Step { get; }
    internal bool FarBank => Project.Steps[Step].FarBank;

    internal override string Counter => $"{Step + 1} / {BuildProject.Trips}";
    internal override bool DropsAtContractor => !FarBank;

    internal BuildTrip(World world, Vehicle car, BuildProject project, int step, bool loaded)
        : base(
            world,
            car,
            project.Steps[step].Cargo,
            project.DropFor(step),
            project.DropRoadFor(step),
            project.Builder,
            loaded
        )
    {
        Project = project;
        Step = step;
        Name = "BuildTrip";
    }
}

/// <summary>Planks or bricks that a village resident wants for the next improvement of their house.</summary>
public partial class HouseTrip : HaulTrip
{
    internal int House { get; }

    internal HouseTrip(World world, Vehicle car, int house, HaulLoad load, bool loaded)
        : base(
            world,
            car,
            load,
            world.Village.HouseDrop(house),
            world.Village.HouseDrop(house),
            world.Village.Residents[house],
            loaded
        )
    {
        House = house;
        Name = "HouseTrip";
    }
}

/// <summary>Raw material for a works: logs, soil or stones, hauled to its ring and turned into stock.</summary>
public partial class SupplyTrip : HaulTrip
{
    internal Works Works { get; }
    internal int WorksIndex => Works.Site.Index;

    internal SupplyTrip(World world, Vehicle car, Works works, bool loaded)
        : base(world, car, works.Spec.Input, works.Site.Position, works.Site.Road, works.Worker, loaded)
    {
        Works = works;
        Name = "SupplyTrip";
    }
}
