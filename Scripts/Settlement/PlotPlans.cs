using MathRacer.Buildings;

namespace MathRacer.Settlement;

/// <summary>
/// What a village building plot needs before its little house stands and a new resident moves in: six deliveries of
/// goods from the works, in a fixed order. Every plot uses the same plan for now; more kinds of house can follow.
/// </summary>
internal static class PlotPlans
{
    /// <summary>The building plots of the village.</summary>
    public const int Count = 6;

    /// <summary>Deliveries that finish one house.</summary>
    public const int Trips = 6;

    private static readonly HaulLoad[] cargo =
    {
        // The slab, the lower and the upper walls, the roof frame, the roof and the finishing touches.
        HaulLoad.Gravel,
        HaulLoad.Bricks,
        HaulLoad.Bricks,
        HaulLoad.Planks,
        HaulLoad.Planks,
        HaulLoad.Planks,
    };

    public static HaulLoad CargoOf(int step) => cargo[step];
}
