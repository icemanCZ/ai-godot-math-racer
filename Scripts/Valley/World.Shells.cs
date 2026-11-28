using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>A shell on the wet sand: its number among those of its tide, its kind, where it lies and how it is turned.</summary>
internal sealed record BeachShell(int Number, int Kind, Vector3 Position, float Yaw);

/// <summary>
/// The shells of the beach (<c>docs/SHELLS_PLAN.md</c>): a cove of the beach below the homestead of the keeper of the sea,
/// which the car may drive down to past the edge of the valley as far as the water lets it, and on its wet sand, which
/// only the low tide uncovers, the shells every high tide leaves behind. Where the shells of a tide lie follows from the
/// valley and the tide's number with random numbers of their own (<see cref="ShellSeed"/>), so they are not saved, and
/// nothing planned before them moves. The cove and the first two tides are held by <c>Tests/Worlds/seed-N.shells.txt</c>.
/// </summary>
public partial class World
{
    /// <summary>The random numbers of the shells are their own, so nothing planned before them changes.</summary>
    private const int ShellSeed = 9311;

    /// <summary>How far to each side of its middle the cove's shells lie.</summary>
    internal const float CoveHalfWidth = 130;

    /// <summary>The shells lie this far out from the high water line, on sand that only the low tide uncovers.</summary>
    private const float ShellNearest = 3,
        ShellFurthest = 28;

    /// <summary>The middle of the cove at the high water line, or null in a valley without a beach to drive down to.</summary>
    internal Vector3? ShellCove { get; private set; }

    /// <summary>The sign at the edge of the valley above the cove.</summary>
    internal Vector3? ShellSign { get; private set; }

    /// <summary>The shells of the tide on the sand and the sign above them.</summary>
    internal BeachShells? BeachShells { get; private set; }

    /// <summary>How far above its level the sea is now with the tide: 0 at high tide, down to -<see cref="Tide.Range"/>.</summary>
    internal float TideOffset { get; private set; }

    /// <summary>The surface of the sea now.</summary>
    internal float SeaSurface => ValleyBorders.SeaLevel + TideOffset;

    /// <summary>
    /// Chooses the cove: below the homestead of the keeper of the sea, or, without it, where the coast comes nearest the
    /// middle; and the place of the sign at the edge of the valley above it.
    /// </summary>
    private void PlanShells()
    {
        var borders = WorldLayout.Plan.Borders;
        float limit = ValleyBounds.HalfSize - CoveHalfWidth - 20;
        float x;
        if (BorderHomes[(int)Border.Sea] is { } home)
            x = home.Road.X;
        else
        {
            x = 0;
            for (float along = -limit; along <= limit; along += 10)
                if (borders.Coast(along) < borders.Coast(x))
                    x = along;
        }
        x = Mathf.Clamp(x, -limit, limit);
        float z = borders.Coast(x);
        ShellCove = new Vector3(x, ValleyBorders.SeaLevel, z);
        foreach (float offset in new[] { 24f, -24f, 36f, -36f, 48f, -48f, 60f, -60f })
        {
            var p = new Vector3(x + offset, 0, ValleyBounds.HalfSize - 4);
            if (
                InBorderfolkZone(p)
                || Routes.Distance(p) < 6
                || OutlandRoutes.Distance(p) < 6
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 6
                || Ground.Height(p.X, p.Z) < ValleyBorders.SeaLevel + 1
            )
                continue;
            ShellSign = Ground.At(p.X, p.Z);
            break;
        }
        GD.Print($"SHELLS cove={x:0.0},{z:0.0} sign={ShellSign != null}");
    }

    /// <summary>
    /// The shells that the high tide of number <paramref name="tide"/> leaves on the wet sand of the cove: each of a kind
    /// drawn by how often it is washed up, on sand that only the low tide uncovers, off the mouth of the valley's river,
    /// the lighthouse and one another.
    /// </summary>
    internal BeachShell[] ShellsOfTide(int tide)
    {
        if (ShellCove is not { } cove)
            return System.Array.Empty<BeachShell>();
        var borders = WorldLayout.Plan.Borders;
        var rng = new RandomNumberGenerator { Seed = (ulong)(Settings.Seed + ShellSeed) * 7919UL + (ulong)tide };
        int sum = ShellPlans.Weights.Sum();
        var shells = new List<BeachShell>();
        for (int number = 0; number < ShellPlans.PerTide; number++)
        {
            int roll = (int)rng.RandiRange(0, sum - 1),
                kind = 0;
            while (roll >= ShellPlans.Weights[kind])
                roll -= ShellPlans.Weights[kind++];
            float yaw = rng.RandfRange(0, Mathf.Tau);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float x = cove.X + rng.RandfRange(-CoveHalfWidth, CoveHalfWidth);
                float z = borders.Coast(x) + rng.RandfRange(ShellNearest, ShellFurthest);
                float h = Ground.Height(x, z);
                var p = new Vector3(x, h, z);
                if (
                    Mathf.Abs(x) > ValleyBounds.HalfSize - 12
                    || h > ValleyBorders.SeaLevel - .05f
                    || h < ValleyBorders.SeaLevel - Tide.Range + .3f
                    || River.Distance(x, z) < River.HalfWidth(z) + 8
                    || LighthousePlace is { } light && FlatDistance(p, light.Foot) < LighthouseSite.Radius + 6
                    || shells.Any(other => FlatDistance(other.Position, p) < 6)
                )
                    continue;
                shells.Add(new BeachShell(number, kind, p, yaw));
                break;
            }
        }
        return shells.ToArray();
    }

    /// <summary>
    /// How deep the sea is over the ground at <paramref name="p"/> beyond the edge of the valley by the sea, where the car
    /// may drive down the beach as far as the water lets it: negative on dry ground, and infinite where the car may
    /// not go at all (beyond the other edges, or far out at sea).
    /// </summary>
    internal float SeaDepth(Vector3 p)
    {
        // On the bird island, where the punt has brought the car, it may drive about but not off it.
        if (IslandOpen)
            return OnIsland(p) ? -1 : float.PositiveInfinity;
        // On the turtle island, where the ship has brought the car, likewise.
        if (TurtleIslandOpen)
            return OnTurtleIsland(p) ? -1 : float.PositiveInfinity;
        if (
            p.Z < ValleyBounds.HalfSize - 1
            || Mathf.Abs(p.X) > ValleyBounds.HalfSize
            || p.Z > WorldLayout.Plan.Borders.Coast(p.X) + 60
        )
            return float.PositiveInfinity;
        return SeaSurface - Ground.Height(p.X, p.Z);
    }

    /// <summary>The sea and the boat on it follow the tide at <paramref name="clock"/>.</summary>
    internal void SetTide(float clock)
    {
        TideOffset = Tide.Offset(clock);
        BorderWater.SetTide(TideOffset);
        if (NightBoat != null)
            NightBoat.TideLift = TideOffset;
    }

    /// <summary>The sign above the cove, where no plant grows.</summary>
    internal bool InShellsZone(Vector3 p) => ShellSign is { } sign && FlatDistance(p, sign) < 2.5f;

    /// <summary>Lays out the shells' node and the crab, who waits by the keeper of the sea until every kind is found.</summary>
    private void BuildShells()
    {
        if (ShellCove == null)
            return;
        var shells = new BeachShells(ShellSign) { Name = "BeachShells" };
        AddChild(shells);
        BeachShells = shells;
        if (LighthouseKeeper is not { } keeper)
            return;
        var crab = new CollectorPet(
            WorldPetCatalog.Crab,
            keeper,
            () => shells.KindsFound,
            ShellPlans.Kinds,
            worldPets.Count
        );
        var seat = keeper.Transform * new Vector3(1.4f, 0, .6f);
        crab.SetHome(Ground.At(seat.X, seat.Z), keeper.Rotation.Y);
        AddChild(crab);
        AddWorldPet(crab);
    }
}
