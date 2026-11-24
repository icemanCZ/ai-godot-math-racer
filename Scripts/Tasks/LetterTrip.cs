using Godot;
using MathRacer.Characters;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Tasks;

/// <summary>
/// The way of a letter or a reply (<see cref="Post"/>): the road arrows from the car to the character it is for, a keeper of
/// a border or a resident of the village. It is a scene node only while something is carried; its arrows are hidden while a
/// delivery or a trailer trip shows its own.
/// </summary>
internal sealed partial class LetterTrip : Node3D
{
    private readonly RouteGuide guide;
    private bool shown = true;

    /// <summary>The character the letter or reply is for.</summary>
    internal Character Addressee { get; }

    internal LetterTrip(World world, Vehicle car, Character addressee)
    {
        Name = "LetterTrip";
        Addressee = addressee;
        guide = new RouteGuide(world, car, this, () => Addressee.Position);
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
