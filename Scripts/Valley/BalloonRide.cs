using Godot;

namespace MathRacer.Valley;

/// <summary>
/// One flight of the hot-air balloon (<c>docs/BALLOON_PLAN.md</c>), without scene nodes: how far the wind has carried it
/// along its way (<see cref="BalloonFlight"/>), how high above the way's floor the player has taken it and how fast it
/// climbs or sinks, and which of the stars along the way were caught. The burner (the pedal) heats the air and the balloon
/// climbs, the valve (the brake or reverse) lets hot air out and it sinks faster, and left alone it cools and sinks
/// slowly. After the last star it comes down onto the field by itself. A flight is never saved.
/// </summary>
internal sealed class BalloonRide
{
    /// <summary>How high above the bottom of the basket a star is caught, the middle of the basket.</summary>
    public const float BasketMiddle = .6f;

    /// <summary>How near a star the basket must come, along the way and in height, to catch it.</summary>
    public const float CatchAlong = 6,
        CatchHeight = 4.6f;

    /// <summary>How fast the balloon climbs and sinks at most, and how fast the burner, the valve and the cooling change it.</summary>
    private const float MostClimb = 4.6f,
        Burn = 3.4f,
        Vent = 3.8f,
        Cooling = 1f;

    /// <summary>How long the slow start takes, in seconds.</summary>
    private const float StartSeconds = 3;

    private readonly bool[] caught = new bool[BalloonFlight.Stars];

    internal BalloonFlight Way { get; }

    /// <summary>The flight's number, which picks the heights of its stars (<see cref="BalloonFlight.StarHeight"/>).</summary>
    internal int Number { get; }

    /// <summary>How far along the way the wind has carried the balloon, in metres.</summary>
    internal float Distance { get; private set; }

    /// <summary>How high above the way's floor the bottom of the basket is, 0 to <see cref="BalloonFlight.Highest"/>.</summary>
    internal float Height { get; private set; }

    /// <summary>How fast the balloon climbs (above 0) or sinks, in metres a second.</summary>
    internal float Climb { get; private set; }

    internal float Seconds { get; private set; }

    /// <summary>Whether the burner was on in the last step, so that its flame shows.</summary>
    internal bool Burning { get; private set; }

    /// <summary>How many stars were caught so far.</summary>
    internal int Caught { get; private set; }

    /// <summary>The balloon stands on the field again.</summary>
    internal bool Landed => Distance >= Way.Length;

    /// <summary>A flight from the balloon's ropes, <paramref name="start"/> metres above the field.</summary>
    internal BalloonRide(BalloonFlight way, int number, float start)
    {
        Way = way;
        Number = number;
        Height = Mathf.Clamp(start, 0, BalloonFlight.Highest);
    }

    internal bool IsCaught(int star) => caught[star];

    /// <summary>How high above the floor star <paramref name="star"/> of this flight hangs.</summary>
    internal float StarHeight(int star) => BalloonFlight.StarHeight(star, Number);

    /// <summary>
    /// Carries the balloon on by <paramref name="delta"/> seconds with the burner (<paramref name="lift"/> above 0) or the
    /// valve (below 0), and returns the star caught in this step, or -1.
    /// </summary>
    internal int Step(float delta, float lift)
    {
        if (Landed || delta <= 0)
            return -1;
        Seconds += delta;
        lift = Mathf.Clamp(lift, -1, 1);
        Burning = lift > .05f;
        float change =
            lift > .05f ? Burn * lift
            : lift < -.05f ? Vent * lift
            : -Cooling;
        Climb = Mathf.Clamp((Climb + change * delta) * Mathf.Exp(-.25f * delta), -MostClimb, MostClimb);
        // Slow off the field, slow onto it again at the end.
        float left = Way.Length - Distance;
        float pace = Mathf.SmoothStep(0, StartSeconds, Seconds) * Mathf.Clamp(left / 30 + .3f, .3f, 1);
        Distance = Mathf.Min(Way.Length, Distance + BalloonFlight.Speed * Mathf.Max(pace, .08f) * delta);
        Height += Climb * delta;
        // Once the last star is behind it, the wind brings it down onto the field, whatever the burner does.
        float landing = Way.Length - 12;
        float descent = Mathf.Max(30, landing - Way.StarDistance(BalloonFlight.Stars - 1) - CatchAlong - 2);
        float ceiling = BalloonFlight.Highest * Mathf.Clamp((landing - Distance) / descent, 0, 1);
        if (Height > ceiling)
        {
            Height = ceiling;
            Climb = Mathf.Min(Climb, 0);
        }
        if (Height < 0)
        {
            Height = 0;
            Climb = Mathf.Max(Climb, 0);
        }
        for (int star = 0; star < BalloonFlight.Stars; star++)
            if (
                !caught[star]
                && Mathf.Abs(Distance - Way.StarDistance(star)) < CatchAlong
                && Mathf.Abs(Height - StarHeight(star)) < CatchHeight
            )
            {
                caught[star] = true;
                Caught++;
                return star;
            }
        return -1;
    }

    /// <summary>Where the bottom of the basket is, turned along the wind.</summary>
    internal Transform3D Pose()
    {
        var position = Way.At(Distance) + Vector3.Up * Height;
        var heading = Way.Heading(Distance);
        return new Transform3D(Basis.LookingAt(heading, Vector3.Up, true), position);
    }

    /// <summary>For the checks: puts the balloon at a place of the way, at a height, as if it had flown there.</summary>
    internal void Place(float distance, float height, float seconds = 10)
    {
        Distance = Mathf.Clamp(distance, 0, Way.Length);
        Height = Mathf.Clamp(height, 0, BalloonFlight.Highest);
        Climb = 0;
        Seconds = seconds;
    }
}
