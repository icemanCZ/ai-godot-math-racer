using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>
    /// Whether the character is not there now, as the animal at the end of the forest keeper's trail is while no trail is
    /// followed: hidden, and never the character the car is beside.
    /// </summary>
    internal bool Away
    {
        get => away;
        set
        {
            away = value;
            Visible = !value;
        }
    }

    private bool away;

    /// <summary>
    /// The animal to coax out of its den at the end of a trail (<c>docs/TRACKING_PLAN.md</c>): a character with no body of
    /// its own, standing in front of the den, whose animal is drawn by <c>TrackingDens</c>. Its name floats low, over the
    /// den, and it has no task marker, so that the den is found by its tracks and not from afar.
    /// </summary>
    internal static Character CreateTrackingVoice(CharacterSpec spec)
    {
        var a = new Character
        {
            DisplayName = spec.Name,
            Greeting = spec.Greeting,
            Kind = spec.Kind,
            FurColor = spec.Fur ?? "a5815a",
            StaysTurned = true,
            Name = "TrackingAnimal",
        };
        a.model = new Node3D();
        a.AddChild(a.model);
        a.wavingArm = new Node3D();
        a.model.AddChild(a.wavingArm);
        a.taskMarker = Art.TaskMarker(a, new(0, 7.5f, 0), "?");
        a.nameLabel = Art.Sign(a, new(0, 2.4f, 0), spec.Name, 30);
        return a;
    }

    /// <summary>Gives the character another name, over its head as well.</summary>
    internal void Rename(string name)
    {
        DisplayName = name;
        nameLabel.Text = name;
    }
}
