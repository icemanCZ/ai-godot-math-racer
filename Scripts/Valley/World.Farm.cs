using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;

namespace MathRacer.Valley;

/// <summary>The farmer's wheat field and the bakery, planned together so a valley gets both or neither.</summary>
internal sealed record FarmPlan(FarmSite Farm, WorksSite Bakery);

public partial class World
{
    internal const int BakeryId = ActorIds.Bakery;
    internal const int FieldFarmerId = ActorIds.FieldFarmer;

    /// <summary>The bakery, the fifth works, or null in a valley that has no room for the wheat field and the bakery.</summary>
    internal Works? Bakery { get; private set; }

    /// <summary>The farm with the wheat fields behind the village, or null where the valley has none.</summary>
    internal WheatFarm? Farm { get; private set; }

    /// <summary>The loading ring of the wheat field, where the farmer fills the trailer, or null without a farm.</summary>
    internal HaulSource? WheatField { get; private set; }

    /// <summary>The farmer at the wheat field, who asks the loading question and sends the wheat to the mill.</summary>
    internal Character? FieldFarmer { get; private set; }

    /// <summary>
    /// The mill as the link between the wheat and the bakery: it grinds the wheat into flour once it is finished. Null in
    /// a valley without the bakery or without the mill.
    /// </summary>
    internal FlourMill? FlourMill { get; private set; }

    /// <summary>Joins the mill to the bread chain where the valley has both the mill and the bakery.</summary>
    private void BuildFlourMill()
    {
        if (Bakery == null)
            return;
        if (Projects.Length > BuildPlans.Mill && Projects[BuildPlans.Mill] is Mill mill)
            FlourMill = new FlourMill(mill);
        else
            Bakery.Input = HaulLoad.Wheat;
    }

    /// <summary>Where the miller loads the flour: the unloading ring of the mill, where the wheat is tipped out too.</summary>
    private HaulSource MillSource()
    {
        var mill = FlourMill!.Mill;
        return new HaulSource(HaulLoad.Flour, mill.Site.Position, mill.Site.Road);
    }

    private FarmSite? farmSite;
    private WorksSite? bakerySite;

    /// <summary>
    /// The farm lies outside the village, a good drive from the houses but not far: a band that starts beyond the works
    /// and the landmarks. A valley that has no such place for it falls back to a wider band and then to anywhere.
    /// </summary>
    internal static readonly VillageBand FarmBand = new(72, 150);

    private static readonly VillageBand FarmFallbackBand = new(52, 240);

    /// <summary>The farm keeps this far from the glassworks and from the bakery, whose sites do not reserve any ground.</summary>
    private const float FarmGap = 55;

    /// <summary>
    /// Plans the wheat farm, then the bakery at the edge of the village, after the glassworks and with seeded searches of
    /// their own, so every older site stays where it was. Like the glassworks they reserve no ground in
    /// <see cref="haulClearings"/>: the plants are generated exactly as before and the ones that stand on the farm, the bakery
    /// and their ways are taken out afterwards (<see cref="InFarmZone"/>, <see cref="InBakeryZone"/>).
    /// </summary>
    private FarmPlan? PlanFarm(FordSite ford)
    {
        var random = new Random(Settings.Seed + 2380);
        var friends = InteractionActors.ToArray();
        Array.Clear(industryRejections);
        bool Apart(Vector3 p) => glassSite == null || FlatDistance(p, glassSite.Position) >= FarmGap * .8f;
        if (
            !FindSpur(random, friends, ford, true, out var road, out var ring, out var inward, FarmBand, Apart)
            && !FindSpur(random, friends, ford, true, out road, out ring, out inward, FarmFallbackBand, Apart)
            && !FindSpur(random, friends, ford, true, out road, out ring, out inward, accept: Apart)
        )
        {
            GD.Print(
                $"FARM_MISSING seed={Settings.Seed} rejected(bank,river,spacing,spot,footprint,path)={string.Join(",", industryRejections)}"
            );
            return null;
        }
        var farm = new FarmSite(ring, road, inward);
        Array.Clear(industryRejections);
        bool ApartFromFarm(Vector3 p) => Apart(p) && FlatDistance(p, farm.Position) >= FarmGap;
        if (
            !FindSpur(random, friends, ford, true, out road, out ring, out inward, WorksBand, ApartFromFarm)
            && !FindSpur(random, friends, ford, true, out road, out ring, out inward, accept: ApartFromFarm)
        )
        {
            GD.Print(
                $"BAKERY_MISSING seed={Settings.Seed} rejected(bank,river,spacing,spot,footprint,path)={string.Join(",", industryRejections)}"
            );
            return null;
        }
        var right = Vector3.Up.Cross(inward);
        var worker = Ground.At(ring.X - right.X * 4.8f + inward.X * 1.5f, ring.Z - right.Z * 4.8f + inward.Z * 1.5f);
        var bakery = new WorksSite(WorksCatalog.BreadIndex, ring, road, inward, worker);
        farmSite = farm;
        bakerySite = bakery;
        GD.Print(
            $"FARM_READY way={FlatDistance(farm.Road, farm.Position):0} village={Village.PlotDistance(farm.Position):0} "
                + $"bakery={Village.PlotDistance(bakery.Position):0}"
        );
        return new FarmPlan(farm, bakery);
    }

    /// <summary>Raises the farm and its farmer, and the bakery with its baker, who have the next free actor IDs.</summary>
    private void BuildFarm(FarmPlan? plan)
    {
        if (plan == null)
            return;
        Farm = new WheatFarm(plan.Farm, Ground);
        AddChild(Farm);
        WheatField = new HaulSource(HaulLoad.Wheat, plan.Farm.Position, plan.Farm.Road);
        FieldFarmer = PlaceSourceWorker(WheatField.Value, SourceCatalog.Field, FieldFarmerId);

        var site = plan.Bakery;
        var works = new Works(site, Ground);
        AddChild(works);
        var spec = works.Spec;
        var baker = Character.Create(spec.Worker, spec.Greeting, spec.Kind, spec.Fur);
        baker.Role = new WorksRole(site.Index);
        baker.DressAsWorker(spec.Hat);
        baker.Position = site.WorkerSpot;
        var facing = site.Position - site.WorkerSpot;
        baker.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
        AddChild(baker);
        works.Worker = baker;
        Register(BakeryId, baker);
        Bakery = works;
    }

    /// <summary>Whether a point lies on the farm: its fields, barn and yard, or the field path from the road.</summary>
    internal bool InFarmZone(Vector3 p)
    {
        if (farmSite is not { } site)
            return false;
        if (InSiteRectangle(p, site.Position, site.Inward, FarmSite.AlongMin, FarmSite.AlongMax, FarmSite.AcrossMax))
            return true;
        return SegmentDistance(p, site.Road, site.Position) < 5;
    }

    /// <summary>Whether a point lies on the bakery's buildings, stock yard and raw heap, or on its field path.</summary>
    internal bool InBakeryZone(Vector3 p)
    {
        if (bakerySite is not { } site)
            return false;
        if (InSiteRectangle(p, site.Position, site.Inward, -8, 23, 15))
            return true;
        return SegmentDistance(p, site.Road, site.Position) < 5;
    }
}
