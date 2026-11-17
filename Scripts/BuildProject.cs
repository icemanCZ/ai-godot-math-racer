using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer;

/// <summary>
/// A building the player raises with ten trailer deliveries: the beaver's bridge, the mill, the lookout tower
/// or the jetty. Each delivery shows the next stage of the work; the last one completes the building.
/// </summary>
public abstract partial class BuildProject : Node3D
{
    public const int Trips = BuildPlans.Trips;

    internal int Index { get; }

    /// <summary>The character who gives the work: the beaver, the miller, the stargazer or the fisherman.</summary>
    internal Animal Builder { get; set; } = null!;

    /// <summary>Deliveries made so far.</summary>
    public int Stage { get; private set; }

    public bool Complete => Stage >= Trips;

    internal IReadOnlyList<BuildStep> Steps => BuildPlans.For(Index);

    private protected BuildProject(int index) => Index = index;

    /// <summary>Where the load of a step is tipped out.</summary>
    internal abstract Vector3 DropFor(int step);

    /// <summary>The road point from which the way to <see cref="DropFor"/> starts.</summary>
    internal abstract Vector3 DropRoadFor(int step);

    /// <summary>The middle of the building, for celebrations and the camera.</summary>
    internal abstract Vector3 Focus { get; }

    /// <summary>Shows the work for <paramref name="stage"/> delivered loads; all ten complete the building.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Trips);
        if (IsInsideTree())
            Refresh();
    }

    /// <summary>Updates the stage nodes and collision to <see cref="Stage"/>.</summary>
    private protected abstract void Refresh();
}
