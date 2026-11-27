using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The tide of the sea (<c>docs/SHELLS_PLAN.md</c>), from the clock of the day: twice a day the sea falls by
/// <see cref="Range"/> and rises again. It is high at the start of the day and at its middle, and low a quarter of a day
/// later; every high tide is a tide of its own, which brings the next shells.
/// </summary>
internal static class Tide
{
    /// <summary>How far the sea falls from high to low tide.</summary>
    public const float Range = 1.5f;

    /// <summary>The seconds from one high tide to the next: half a day.</summary>
    public const float Period = Weather.DayLength / 2;

    /// <summary>How far below <see cref="ValleyBorders.SeaLevel"/> the sea is at <paramref name="clock"/>, 0 to -Range.</summary>
    public static float Offset(float clock) => -Range * (1 - Mathf.Cos(Mathf.Tau * clock / Period)) / 2;

    /// <summary>The tide of the day the clock is in: 0 in its first half, 1 in its second.</summary>
    public static int HalfOfDay(float clock) => Mathf.Clamp(Mathf.FloorToInt(clock / Period), 0, 1);

    /// <summary>How low the sea is, 0 at high tide and 1 at low tide.</summary>
    public static float Ebb(float clock) => -Offset(clock) / Range;
}
