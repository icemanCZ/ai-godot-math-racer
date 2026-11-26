using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The six animals whose tracks the forest keeper teaches the player to follow (<c>docs/TRACKING_PLAN.md</c>), in the
/// order their trails are followed. The names are not saved; only how many were found is.
/// </summary>
internal enum TrackingAnimal
{
    Hare,
    Fox,
    Deer,
    Badger,
    Boar,
    Lynx,
}

/// <summary>One footprint (or, for the hare, one group of four) of a trail, lying on the ground.</summary>
/// <param name="Position">Where it lies, a little above the ground.</param>
/// <param name="Forward">The way the animal went, along the ground.</param>
/// <param name="Normal">The ground's up there, which the print lies flat on.</param>
/// <param name="Side">-1 for a left foot, 1 for a right one, 0 where the feet land together.</param>
/// <param name="Along">How far along the trail it lies, from its start.</param>
internal readonly record struct TrackingPrint(Vector3 Position, Vector3 Forward, Vector3 Normal, int Side, float Along);

/// <summary>
/// The trail of one animal: from where the previous one hid (for the first, from beside the forest keeper's homestead)
/// over the land beyond the old valley to its den. The way keeps off the water, the trees, the steep slopes and the
/// places of the borders, so the car can follow it all the way; it ends a few metres short of the den, where the car
/// stops and the animal is coaxed out.
/// </summary>
/// <param name="Animal">Whose tracks these are.</param>
/// <param name="Path">The middle of the way, on the ground, from its start to where the prints end.</param>
/// <param name="Prints">The footprints along the way, in order.</param>
/// <param name="Den">The middle of the den, beyond the end of the prints.</param>
/// <param name="Talk">Where the animal is talked to: in front of the den, on the way the car comes.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z from the den towards the arriving trail.</param>
/// <param name="Length">How long the way is, on the flat.</param>
internal sealed record TrackingTrail(
    TrackingAnimal Animal,
    Vector3[] Path,
    TrackingPrint[] Prints,
    Vector3 Den,
    Vector3 Talk,
    float Yaw,
    float Length
)
{
    /// <summary>The ground of the den that no plant or grass grows on, round <see cref="Den"/>.</summary>
    public const float DenRadius = 7;

    /// <summary>How far short of the den's middle the prints end.</summary>
    public const float PrintsEndShort = 5.5f;

    /// <summary>How far in front of the den's middle the animal is talked to.</summary>
    public const float TalkAhead = 2.6f;

    /// <summary>The prints within <paramref name="radius"/> of a point on the flat, nearest first.</summary>
    public IEnumerable<int> PrintsNear(Vector3 p, float radius)
    {
        var near = new List<(float Distance, int Index)>();
        for (int i = 0; i < Prints.Length; i++)
        {
            float d = FlatMath.Length(Prints[i].Position.X - p.X, Prints[i].Position.Z - p.Z);
            if (d < radius)
                near.Add((d, i));
        }
        near.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        foreach (var (_, index) in near)
            yield return index;
    }
}
