using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// How far the player has followed the tracks (<c>docs/TRACKING_PLAN.md</c>), without nodes of the scene: whether the
/// forest keeper has sent the player out, how many animals are found and so which trail is followed now, and the
/// furthest footprint of it the car has reached. Only the first two are saved; the furthest print is found again from
/// where the car stands.
/// </summary>
internal sealed class TrackingHunt
{
    /// <summary>A print counts as reached when the car comes this near it.</summary>
    public const float Reach = 6;

    /// <summary>The car is off the trail when it is this far from every print of the stretch shown.</summary>
    public const float LostDistance = 24;

    /// <summary>When a trail is taken up again, a print this near the car counts as reached.</summary>
    private const float SeekDistance = 12;

    /// <summary>How far in front of the furthest print reached the arrow points when the car is off the trail.</summary>
    private const int LeadAhead = 6;

    private readonly IReadOnlyList<TrackingTrail> trails;

    public TrackingHunt(IReadOnlyList<TrackingTrail> trails) => this.trails = trails;

    /// <summary>Whether this follows the trails of that valley.</summary>
    public bool Follows(IReadOnlyList<TrackingTrail> other) => ReferenceEquals(trails, other);

    public int Trails => trails.Count;

    /// <summary>Whether the forest keeper has sent the player out after the tracks.</summary>
    public bool Started { get; private set; }

    /// <summary>How many animals have been found, which is also the number of the trail followed now.</summary>
    public int Found { get; private set; }

    /// <summary>The furthest print of the trail followed now that the car has reached, or -1.</summary>
    public int Progress { get; private set; } = -1;

    /// <summary>A trail is being followed.</summary>
    public bool Active => Started && Found < trails.Count;

    /// <summary>Every animal is found.</summary>
    public bool Complete => trails.Count > 0 && Found >= trails.Count;

    /// <summary>The trail followed now, or null.</summary>
    public TrackingTrail? Current => Active ? trails[Found] : null;

    /// <summary>The trail of an animal, by its number.</summary>
    public TrackingTrail TrailOf(int number) => trails[number];

    /// <summary>How much of the trail followed now lies behind the car, from 0 to 1.</summary>
    public float Fraction =>
        Current is { } trail && trail.Prints.Length > 1 ? Mathf.Max(0, Progress) / (float)(trail.Prints.Length - 1) : 0;

    /// <summary>
    /// The forest keeper sends the player out: the first trail begins beside her homestead, and only its first stretch
    /// counts as reached where the car stands, so that a trail that passes by the homestead later is not skipped.
    /// </summary>
    public void Start(Vector3 car)
    {
        if (trails.Count == 0 || Started)
            return;
        Started = true;
        Progress = -1;
        Step(car);
    }

    /// <summary>
    /// The car moves on: the furthest print of the stretch shown that the car is near is reached. Returns whether that
    /// changed.
    /// </summary>
    public bool Step(Vector3 car)
    {
        if (Current is not { } trail)
            return false;
        int last = Mathf.Min(trail.Prints.Length - 1, Progress + TrackingPrints.Ahead);
        int reached = Progress;
        for (int i = Mathf.Max(0, Progress); i <= last; i++)
        {
            var p = trail.Prints[i].Position;
            if (FlatMath.Length(p.X - car.X, p.Z - car.Z) < Reach)
                reached = i;
        }
        if (reached == Progress)
            return false;
        Progress = reached;
        return true;
    }

    /// <summary>Whether the car is far from every print of the stretch shown.</summary>
    public bool Lost(Vector3 car)
    {
        if (Current is not { } trail)
            return false;
        int first = Mathf.Max(0, Progress - TrackingPrints.Behind),
            last = Mathf.Min(trail.Prints.Length - 1, Progress + TrackingPrints.Ahead);
        for (int i = first; i <= last; i++)
        {
            var p = trail.Prints[i].Position;
            if (FlatMath.Length(p.X - car.X, p.Z - car.Z) < LostDistance)
                return false;
        }
        return true;
    }

    /// <summary>Where the arrow points the car back to the trail: a little in front of the furthest print reached.</summary>
    public Vector3 Lead =>
        Current is { } trail
            ? trail.Prints[Mathf.Clamp(Progress + LeadAhead, 0, trail.Prints.Length - 1)].Position
            : Vector3.Zero;

    /// <summary>The animal of the trail followed now is coaxed out: the next trail begins at its den.</summary>
    public void FindAnimal(Vector3 car)
    {
        if (!Active)
            return;
        Found++;
        Progress = -1;
        Step(car);
    }

    /// <summary>Puts the hunt back from a save; the furthest print reached is found again from where the car stands.</summary>
    public void Restore(bool started, int found, Vector3 car)
    {
        Started = started && trails.Count > 0;
        Found = Mathf.Clamp(found, 0, trails.Count);
        Seek(car);
    }

    /// <summary>The furthest print of the trail followed now near the car, or none.</summary>
    private void Seek(Vector3 car)
    {
        Progress = -1;
        if (Current is not { } trail)
            return;
        for (int i = trail.Prints.Length - 1; i >= 0; i--)
        {
            var p = trail.Prints[i].Position;
            if (FlatMath.Length(p.X - car.X, p.Z - car.Z) < SeekDistance)
            {
                Progress = i;
                return;
            }
        }
    }
}
