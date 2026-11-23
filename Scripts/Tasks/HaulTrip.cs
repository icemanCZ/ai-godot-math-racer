using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Settlement;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Tasks;

/// <summary>
/// One trailer trip: drive to a source, answer the loading question, then tip the load out at the drop point. The
/// contractor (a builder, a works' worker, a resident, a newcomer or the lamp maker) sends the trip and thanks the
/// driver; whoever loads the cargo asks the question (<see cref="Asker"/>). What the trip is for, and what the finished
/// delivery does, comes from its <see cref="Target"/>. Road arrows and the target ring belong to this node.
/// </summary>
public sealed partial class HaulTrip : Node3D
{
    private readonly World world;
    private readonly RouteGuide guide;
    private readonly Vector3 dropPoint;
    private readonly Vector3 dropRoad;
    private Node3D? target;

    internal ITripTarget Target { get; }

    /// <summary>The delivery of the target this trip brings: its stage when the trip began, 0 where there are no steps.</summary>
    internal int Step { get; }

    internal HaulLoad Load { get; }
    internal Character Contractor => Target.Contractor;

    /// <summary>The character who loads the cargo and asks the loading question: the source worker, else the contractor.</summary>
    internal Character Asker => world.PickupActor(Load, Source) ?? Contractor;
    internal bool Loaded { get; private set; }

    /// <summary>Set on reaching the current target; cleared when the car leaves it again.</summary>
    internal bool Arrived { get; set; }

    /// <summary>The pickup site the trip was sent to: the nearest one when the trip began (a raw material has two).</summary>
    internal HaulSource Source { get; private set; }
    internal Vector3 Destination => Loaded ? dropPoint : Source.Position;
    internal Vector3 Waypoint => guide.Waypoint;

    /// <summary>Short text for the HUD, such as the number of the trip; empty when there is none.</summary>
    internal string Counter => Target.CounterFor(Step);

    /// <summary>Whether the loaded trailer is taken to the contractor (shown in the HUD) rather than elsewhere.</summary>
    internal bool DropsAtContractor => Target.DropsAtContractor(Step);

    // What the kinds of trip hold, for the code that is about one of them.
    internal BuildProject? Project => Target as BuildProject;
    internal HousePlot? Plot => Target as HousePlot;
    internal Playground? Playground => Target as Playground;
    internal Railway? Railway => Target as Railway;
    internal Zoo? Zoo => Target as Zoo;
    internal HunterPen? HunterHut => Target as HunterPen;
    internal Works? Works => Target as Works;
    internal int House => Target is HouseTripTarget house ? house.House : -1;
    internal bool ForGlassmaker => Target is GlassmakerTripTarget;

    /// <summary>The delivery of a building goes to the far bank of the river.</summary>
    internal bool FarBank => Project is { } project && project.Steps[Step].FarBank;

    private Vector3 RoadApproach => !Loaded ? Source.Road : dropRoad;

    internal HaulTrip(World world, Vehicle car, ITripTarget target, int step, bool loaded)
    {
        this.world = world;
        Target = target;
        Step = step;
        Load = target.LoadFor(step);
        dropPoint = target.DropFor(step);
        dropRoad = target.DropRoadFor(step);
        Source = world.NearestSource(Load, car.Position);
        Loaded = loaded;
        Name = char.ToUpperInvariant(target.Kind[0]) + target.Kind[1..] + "Trip";
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
