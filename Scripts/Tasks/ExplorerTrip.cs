using Godot;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Tasks;

/// <summary>
/// The way to a wonder of the land beyond the old valley that the owl has shown on her map
/// (<c>docs/EXPLORER_PLAN.md</c>): the road arrows from the car to the point of the road the wonder is seen from. It is a
/// scene node only while the owl leads somewhere; its arrows are hidden while a delivery, a trailer trip or a letter
/// shows its own.
/// </summary>
internal sealed partial class ExplorerTrip : Node3D
{
    private readonly RouteGuide guide;
    private bool shown = true;

    /// <summary>The wonder the arrows lead to.</summary>
    internal Landmark Landmark { get; }

    internal ExplorerTrip(World world, Vehicle car, Landmark landmark)
    {
        Name = "ExplorerTrip";
        Landmark = landmark;
        guide = new RouteGuide(world, car, this, () => Landmark.Road);
        guide.Retarget();
    }

    public override void _Ready() => guide.CreateArrows();

    /// <summary>Moves the arrows on; with <paramref name="visible"/> false they are hidden.</summary>
    internal void Tick(double delta, bool visible)
    {
        if (!visible)
        {
            if (shown)
                guide.HideAll();
            shown = false;
            return;
        }
        shown = true;
        guide.Tick(delta);
    }
}
