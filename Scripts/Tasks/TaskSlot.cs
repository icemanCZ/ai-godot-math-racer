using System;
using Godot;
using MathRacer.Buildings;
using MathRacer.Ui;
using MathRacer.Vehicles;

namespace MathRacer.Tasks;

/// <summary>
/// The single task slot: a delivery, a collection or a trailer trip is under way, and never more than one. The slot
/// puts the task in the scene and takes it out again together with its cargo, and tells the HUD and the task
/// markers when that happens. Whoever starts a new task cancels the one under way first.
/// </summary>
internal sealed class TaskSlot
{
    private readonly Node parent;
    private readonly Func<Trailer?> trailer;
    private readonly GameUi ui;
    private readonly Action refreshGuides;

    public TaskSlot(Node parent, Func<Trailer?> trailer, GameUi ui, Action refreshGuides)
    {
        this.parent = parent;
        this.trailer = trailer;
        this.ui = ui;
        this.refreshGuides = refreshGuides;
    }

    /// <summary>The delivery or collection under way.</summary>
    public Expedition? Delivery { get; private set; }

    /// <summary>The trailer trip under way for a building, a works, a house or a plot.</summary>
    public HaulTrip? Haul { get; private set; }

    /// <summary>A delivery, collection or trailer trip is under way; only one runs at a time.</summary>
    public bool Active => Delivery != null || Haul != null;

    /// <summary>Puts a delivery or collection in the scene. The task under way must have been cancelled.</summary>
    public void Begin(Expedition delivery)
    {
        Delivery = delivery;
        parent.AddChild(delivery);
    }

    /// <summary>Puts a trailer trip in the scene and hitches the trailer to it.</summary>
    public void Begin(HaulTrip trip)
    {
        EndHaul(refreshUi: false);
        Haul = trip;
        parent.AddChild(trip);
        var bed = trailer()!;
        if (!bed.Attached)
            bed.Attach(true);
        // Keep the bed lowering after a dump; only a different load replaces the contents.
        HaulLoad? load = trip.Loaded ? trip.Load : null;
        if (bed.Load != load)
            bed.SetLoad(load);
        ui.HaulHud(trip);
        refreshGuides();
    }

    /// <summary>
    /// Removes the active expedition from the scene. With <paramref name="refreshUi"/> it also hides the
    /// delivery HUD and refreshes the task markers; callers that still change related state pass false.
    /// </summary>
    public void EndExpedition(bool refreshUi = true)
    {
        if (Delivery == null)
            return;
        Delivery.ReleaseCargo();
        parent.RemoveChild(Delivery);
        Delivery.QueueFree();
        Delivery = null;
        if (!refreshUi)
            return;
        ui.DeliveryHud(null);
        refreshGuides();
    }

    /// <summary>Removes the active trailer trip from the scene; the trailer stays hitched until it is idle.</summary>
    public void EndHaul(bool refreshUi = true)
    {
        if (Haul == null)
            return;
        parent.RemoveChild(Haul);
        Haul.QueueFree();
        Haul = null;
        if (!refreshUi)
            return;
        ui.DeliveryHud(null);
        refreshGuides();
    }

    /// <summary>Accepting a new task switches off the one under way, together with its cargo.</summary>
    public void Cancel()
    {
        EndExpedition(refreshUi: false);
        EndHaul(refreshUi: false);
        trailer()!.SetLoad(null);
    }
}
