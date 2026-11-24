using Godot;
using MathRacer.Graphics;

namespace MathRacer.Characters;

public partial class Character
{
    /// <summary>Whether the character keeps the way it was set, as the dragon does, instead of turning to the car.</summary>
    internal bool StaysTurned { get; private set; }

    /// <summary>How much higher than usual the task marker floats, above a head far higher than a friend's.</summary>
    private float markerLift;

    /// <summary>
    /// The dragon to talk to (<c>docs/DRAGON_PLAN.md</c>): a character with no body of its own, standing under the chin of
    /// the dragon's body (<see cref="Dragon"/>), so that the car meets him where it can drive. His name and his task
    /// marker float above his head, <paramref name="headHeight"/> metres above the ground where the character stands.
    /// </summary>
    internal static Character CreateDragon(CharacterSpec spec, float headHeight)
    {
        var a = new Character
        {
            DisplayName = spec.Name,
            Greeting = spec.Greeting,
            Kind = spec.Kind,
            FurColor = spec.Fur ?? "5fae5a",
            StaysTurned = true,
            Name = "DragonVoice",
        };
        a.model = new Node3D();
        a.AddChild(a.model);
        a.wavingArm = new Node3D();
        a.model.AddChild(a.wavingArm);
        a.markerLift = Mathf.Max(0, headHeight - 2.2f);
        a.taskMarker = Art.TaskMarker(a, new(0, 7.5f + a.markerLift, 0), "?");
        a.nameLabel = Art.Sign(a, new(0, headHeight + 1.9f, 0), spec.Name, 30);
        return a;
    }
}
