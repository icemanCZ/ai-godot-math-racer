using Godot;

namespace MathRacer.Vehicles;

/// <summary>
/// How the vehicle that carries a gadget drives in this frame: its <paramref name="Speed"/> in metres a second
/// (negative backwards), how hard the engine works (<paramref name="Effort"/>, 0 to 1, eased so a let-go pedal does not
/// snap), how fast it turns (<paramref name="Turn"/>, radians a second, positive to the left) and the
/// <paramref name="Time"/> the gadget has been running.
/// </summary>
internal readonly record struct Drive(float Dt, float Speed, float Effort, float Turn, float Time);

/// <summary>
/// What makes each model special moves by how it is driven: the tank's turret runs ahead into a turn, the plane's
/// propeller blurs and its rudder turns, the rocket's flame roars, the saucer's ring of lights spins, the retro car's
/// wind-up key unwinds, the buggy's engine pumps, the dog in the pickup wags and flaps its ears and the child in the
/// wagon holds a pinwheel out of the window. The steam engine has its smoke instead (<see cref="TrainSmoke"/>).
/// A gadget only moves while its model is on the car. In the garage picture and on the ghost car there is no vehicle,
/// so it moves as on a car that stands with its engine running.
/// </summary>
public partial class Gadget : Node3D
{
    private Vehicle? vehicle;
    private float time,
        effort;

    /// <summary>Moves the parts of the model by one frame of driving.</summary>
    internal System.Action<Drive>? Move { get; init; }

    public override void _Ready()
    {
        for (Node? node = GetParent(); node != null; node = node.GetParent())
            if (node is Vehicle car)
            {
                vehicle = car;
                break;
            }
    }

    public override void _Process(double delta)
    {
        if (IsVisibleInTree())
            Step(Mathf.Min((float)delta, .1f), vehicle?.Speed ?? 0, vehicle?.Effort ?? 0, vehicle?.Turn ?? 0);
    }

    /// <summary>Moves the gadget on by <paramref name="dt"/> seconds of driving; the checks call it directly.</summary>
    internal void Step(float dt, float speed, float work, float turn)
    {
        effort = Mathf.MoveToward(effort, work, (work > effort ? 6f : 3.5f) * dt);
        time += dt;
        Move?.Invoke(new Drive(dt, speed, effort, turn, time));
    }

    /// <summary>The share of <paramref name="rate"/> that a moving value covers towards its target in one frame.</summary>
    internal static float Ease(float rate, float dt) => 1 - Mathf.Exp(-rate * dt);
}
