using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Settlement;

public partial class Village
{
    /// <summary>How far a plot's middle lies from the centre line of its road.</summary>
    private const float PlotOffset = 13.5f;

    private readonly List<HousePlot> buildPlots = new();

    /// <summary>The building plots, in the order of their actor IDs and of <see cref="CharacterCatalog.PlotResidents"/>.</summary>
    public IReadOnlyList<HousePlot> BuildPlots => buildPlots;

    /// <summary>Deliveries made to each plot, 0–6.</summary>
    public int[] PlotStages => buildPlots.Select(plot => plot.Stage).ToArray();

    public void RestorePlots(int[] saved)
    {
        for (int i = 0; i < buildPlots.Count; i++)
            buildPlots[i].SetStage(i < saved.Length ? saved[i] : 0);
    }

    /// <summary>
    /// The benches and road signs that the world places beside the main road once the village exists (see
    /// <see cref="World.Generate"/>): distance along the road and sideways offset. A plot keeps clear of them.
    /// </summary>
    private static readonly (float Distance, float Offset)[] RoadsideProps = { (115, 9), (180, 9), (22, 6), (140, 6) };

    /// <summary>
    /// Whether the lot of a plot at <paramref name="plot"/> is free: off every road and river, clear of everything
    /// the village has reserved so far and of the roadside props.
    /// </summary>
    private bool PlotIsClear(Transform3D plot, Vector2[] props)
    {
        for (float x = -5; x <= 5; x += 1.25f)
        for (float z = -6.5f; z <= 6.5f; z += 1.5f)
        {
            var p = plot * new Vector3(x, 0, z);
            if (
                roads.Contains(p)
                || roads.Distance(p) < 5.8f
                || IsReserved(p)
                || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 8
            )
                return false;
            foreach (var prop in props)
                if (new Vector2(p.X - prop.X, p.Z - prop.Y).Length() < 3)
                    return false;
        }
        return true;
    }

    /// <summary>
    /// Lays out the building plots on free ground beside the streets, nearest to the middle of the village first,
    /// each facing its street with a walkway to it, and puts a newcomer on every one. They are placed after
    /// everything else, so the rest of the village keeps the positions it always had. The plots are reserved ground
    /// (<see cref="IsReserved"/>) but are not in <c>plots</c>, so they leave the village's terrain platform and the search
    /// for the works and landmarks as they were.
    /// </summary>
    private void GeneratePlots()
    {
        var props = RoadsideProps
            .Select(prop =>
            {
                var frame = roads.SampleMain(prop.Distance);
                var p = frame.Center + frame.Forward.Cross(Vector3.Up) * prop.Offset;
                return new Vector2(p.X, p.Z);
            })
            .ToArray();
        var middle = new Vector2(Center.X, Center.Z);
        var candidates = new List<(float Distance, Vector3 Road, Vector3 Outward)>();
        for (int i = 1; i + 1 < roads.Samples.Count; i += 2)
        {
            var road = roads.Samples[i];
            var tangent = roads.Samples[i + 1] - roads.Samples[i - 1];
            tangent.Y = 0;
            // A long step is the jump from the end of one road to the start of the next.
            if (tangent.Length() < .1f || tangent.Length() > 5)
                continue;
            float distance = new Vector2(road.X, road.Z).DistanceTo(middle);
            foreach (int side in new[] { 1, -1 })
                candidates.Add((distance, road, tangent.Normalized().Cross(Vector3.Up) * side));
        }
        var ordered = candidates.OrderBy(candidate => candidate.Distance).ToArray();
        // First only ground that the village's levelled platform already covers, so that the plots change neither
        // the platform nor the terrain around it; if that leaves too few, any free ground beside a street will do.
        var platform = TerrainArea.Grow(-Settings.TerrainStep * 2);
        foreach (bool insidePlatform in new[] { true, false })
        foreach (var (_, road, outward) in ordered)
        {
            if (buildPlots.Count == PlotPlans.Count)
                break;
            var origin = road + outward * PlotOffset;
            var plot = new Transform3D(
                Basis.FromEuler(new(0, Mathf.Atan2(-outward.X, -outward.Z), 0)),
                ground.At(origin.X, origin.Z)
            );
            if ((insidePlatform && !InsidePlatform(plot, platform)) || !PlotIsClear(plot, props))
                continue;
            AddPlot(plot, road);
        }
        if (buildPlots.Count < PlotPlans.Count)
            throw new InvalidOperationException("No clear village building plot found.");
    }

    /// <summary>Whether the corners that <see cref="TerrainArea"/> counts for a plot all lie inside <paramref name="platform"/>.</summary>
    private static bool InsidePlatform(Transform3D plot, Rect2 platform)
    {
        foreach (float x in new[] { -13f, 13f })
        foreach (float z in new[] { -10f, 10f })
        {
            var p = plot * new Vector3(x, 0, z);
            if (!platform.HasPoint(new Vector2(p.X, p.Z)))
                return false;
        }
        return true;
    }

    private void AddPlot(Transform3D lot, Vector3 road)
    {
        int index = buildPlots.Count;
        var plot = new HousePlot(index, lot, road, ground);
        AddChild(plot);
        buildPlots.Add(plot);
        // The walkway leads from the ring where the trailer tips its load to the street.
        Path(plot.Drop, road, 3.4f);
        plot.WaitSpot = ApproachSpot(plot.WaitSpot);
        plot.HomeSpot = ApproachSpot(plot.HomeSpot);
        var resident = Animal.Create(CharacterCatalog.PlotResidents[index]);
        resident.Role = new PlotResidentRole(plot);
        AddChild(resident);
        resident.Rotation = new Vector3(0, plot.Rotation.Y, 0);
        plot.Resident = resident;
        plot.SetStage(0);
    }
}
