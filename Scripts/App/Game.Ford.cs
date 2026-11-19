using System;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Tasks;
using MathRacer.Vehicles;

namespace MathRacer.App;

/// <summary>
/// Trailer trips for buildings: each building has a builder who sends the next of ten deliveries (the bridge's
/// builder is the beaver). The trips share the single task slot with expeditions and supply trips.
/// </summary>
public partial class Game
{
    private Trailer trailer = null!;
    private bool trailerParade;

    /// <summary>The trailer is won by finishing the bridge; it is lent for every trip before that.</summary>
    internal bool TrailerOwned { get; private set; }
    internal Trailer Trailer => trailer;

    /// <summary>Companions trail further back while the trailer is hitched.</summary>
    private float TrailerTrailShift => trailer?.Attached == true ? 3.5f : 0;

    private void SetupTrailer()
    {
        trailer = new Trailer(world, vehicle);
        world.AddChild(trailer);
    }

    private BuildProject? ProjectOf(Character? actor) =>
        actor?.Role is BuilderRole role && role.Project < world.Projects.Length ? world.Projects[role.Project] : null;

    /// <summary>The works that makes a goods load, or null for any other load.</summary>
    private Works? GoodsWorks(HaulLoad load) =>
        HaulLoads.IsGoods(load) ? world.WorksFor(HaulLoads.ProductOf(load)) : null;

    /// <summary>Whether the next delivery of a project can be fetched: goods need units in the works' stock.</summary>
    private bool StepReady(BuildProject project) =>
        project.Complete
        || !HaulLoads.IsGoods(project.Steps[project.Stage].Cargo)
        || GoodsWorks(project.Steps[project.Stage].Cargo) is { } works && works.Stock >= Works.PerTrip;

    internal void OpenBuilder(Character builder)
    {
        if (ProjectOf(builder) is not { } project)
            return;
        ClearChallenge();
        EnterInteraction(GameState.BuildStatus, builder);
        ShowBuilder();
        if (Haul != null || project.Complete)
            return;
        Speak(
            !StepReady(project) ? Texts.ZboziJesteNeniVyrobenePosluTe
                : project.Stage == 0 ? builder.Greeting
                : Texts.PrivezesMiDalsiNaklad,
            builder
        );
    }

    private void ShowBuilder()
    {
        if (ProjectOf(nearby) is not { } project)
            return;
        var trip = Haul != null && Haul.Project == project ? Haul : null;
        bool free = Haul == null && !project.Complete;
        bool ready = StepReady(project);
        ui.BuilderPanel(
            nearby!,
            project,
            trip,
            free && ready,
            free && !ready ? GoodsWorks(project.Steps[project.Stage].Cargo) : null,
            project.Index == BuildPlans.Tower ? LookoutState : 0
        );
    }

    internal void BeginBuildTrip() => BeginBuildTrip(false);

    private void BeginBuildTrip(bool quizzed)
    {
        if (
            State != GameState.BuildStatus
            || ProjectOf(nearby) is not { Complete: false } project
            || Haul != null
            || !StepReady(project)
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Build);
            return;
        }
        StartTrip(project);
    }

    /// <summary>Only a trailer trip needs the trailer: once it is over (and the bed has settled) it is unhitched.</summary>
    private void UnhitchIdleTrailer()
    {
        if (Haul == null && !trailerParade && trailer.Attached && !trailer.Dumping)
            trailer.Attach(false);
    }

    private void UpdateHaulGuides()
    {
        foreach (var project in world.Projects)
            project.Builder.Guide(Haul == null && !project.Complete);
        foreach (var plot in world.Village.BuildPlots)
            plot.Resident.Guide(Haul == null && !plot.Complete);
        if (world.Playground is { } yard)
            yard.Helper.Guide(Haul == null && !yard.Complete);
        foreach (var works in world.AllWorks)
            works.Worker.Guide(true);
        // The sand pit's worker supplies the glassworks like the others supply their works.
        foreach (var worker in world.AllSourceWorkers)
            worker.Guide(Haul == null && WorksOfSource(worker) is { Full: false });
    }

    private void TickHaul(double delta)
    {
        if (Haul == null)
            return;
        Haul.Tick(delta);
        ui.DeliveryDirection(Haul.Waypoint, vehicle);
        if (!Haul.Reached(vehicle.Position))
        {
            if (!trailer.Dumping)
                Haul.Arrived = false;
            return;
        }
        if (Haul.Arrived)
            return;
        Haul.Arrived = true;
        if (Haul.Loaded)
            BeginDump();
        else
            BeginLoading();
    }

    /// <summary>The loading question; whoever loads the cargo asks it from afar, like a radio call.</summary>
    private void BeginLoading()
    {
        if (Haul is not { Loaded: false } trip || State != GameState.Exploring)
            return;
        nearby = trip.Asker;
        vehicle.Active = false;
        BeginQuiz(load: HaulLoads.IsSpecial(trip.Load) ? null : trip.Load);
    }

    internal void CompleteHaulLoading()
    {
        ClearChallenge();
        if (Haul is not { Loaded: false } trip)
        {
            CloseInteraction();
            return;
        }
        // Goods come out of the works' stock the moment they are loaded.
        if (GoodsWorks(trip.Load) is { } works && !works.Take(Works.PerTrip))
        {
            EndHaul();
            CloseInteraction();
            Speak(Texts.ZboziDosloZkusToZnovuPozdeji, trip.Asker);
            return;
        }
        trip.SetLoaded(true);
        trailer.Fill(trip.Load);
        ui.HaulHud(trip);
        UpdateDeliveryGuides();
        CloseInteraction();
        Speak(TripInstruction(trip), trip.Contractor);
        SaveProgress();
    }

    private void BeginDump()
    {
        if (Haul is not { Loaded: true } trip || trailer.Dumping)
            return;
        // The car waits while the bed tips; the callback returns control.
        vehicle.Active = false;
        trailer.Dump(() => FinishDump(trip));
    }

    private void FinishDump(HaulTrip trip)
    {
        if (State == GameState.Exploring)
            vehicle.Active = true;
        if (Haul != trip)
            return;
        trip.Target.FinishDump(this, trip);
    }

    internal void FinishBuildDump(HaulTrip trip)
    {
        var project = trip.Project!;
        world.Tally.AddRaw(trip.Load);
        project.SetStage(trip.Step + 1);
        project.Builder.Celebrate();
        if (project.Complete)
        {
            CompleteProject(project);
            return;
        }
        CelebrateAt(
            project.DropFor(trip.Step) + Vector3.Up * 2,
            WithPets(Texts.BuilderThanks(project.Builder.DisplayName, project.Stage, BuildProject.Trips), EarnPets())
        );
        // The next load waits until the player asks the builder for it again.
        EndHaul();
        Speak(Texts.DekujuAzBudesChtitPrijedZa, project.Builder);
        SaveProgress();
    }

    /// <summary>The last delivery has been tipped out: the building is finished.</summary>
    private void CompleteProject(BuildProject project)
    {
        EndHaul();
        PayBuildReward(project);
        string? pets = EarnPets();
        if (project.Index == BuildPlans.Bridge)
        {
            TrailerOwned = true;
            trailerParade = false;
            CelebrateAt(project.Focus, WithPets(Texts.MostJeHotovyVozikJeTvuj, pets));
            Speak(Texts.HuraMostJeHotovyTedMuzes, project.Builder);
        }
        else
        {
            CelebrateAt(project.Focus, WithPets(Texts.BuildDone(BuildCoins, BuildItemName(project.Index)), pets));
            Speak(Texts.HuraJeHotovoMocDekuju, project.Builder);
        }
        var outward = followCamera.GlobalPosition - project.Focus;
        outward.Y = 0;
        followCamera.ShowMoment(project.Focus, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    internal void ToggleTrailer()
    {
        if (State != GameState.Garage || !TrailerOwned || Haul != null)
            return;
        trailerParade = !trailerParade;
        trailer.Attach(trailerParade);
        ShowGarage();
        SaveProgress();
    }

    internal void SelectTrailerPaint(string id)
    {
        if (State != GameState.Garage || !TrailerOwned || !Array.Exists(TrailerModel.Paints, p => p.Id == id))
            return;
        trailer.SetPaint(id);
        ShowGarage();
        SaveProgress();
    }

    private void RestoreBuilds(SaveData data)
    {
        EndHaul(refreshUi: false);
        for (int i = 0; i < world.Projects.Length; i++)
            world.Projects[i].SetStage(data.Buildings.Stages![i]);
        TrailerOwned = world.Ford is { Passable: true } && data.Buildings.TrailerOwned;
        trailerParade = TrailerOwned && data.Buildings.TrailerAttached;
        trailer.SetPaint(data.Buildings.TrailerPaint);
        RestoreWorldPets(data);
        foreach (var works in world.Industry)
            works.SetStock(data.Buildings.WorksStock[works.Site.Index]);
        world.Glassworks?.SetStock(data.Buildings.GlassStock);
        world.Bakery?.SetStock(data.Buildings.BreadStock);
        world.Village.RestorePlots(data.Buildings.PlotStages!);
        world.Playground?.SetStage(data.Playground.Stage);
        trailer.SetLoad(null);
        if (data.Task.Trip is { } saved && TripTarget(saved) is { } target)
        {
            StartHaul(new HaulTrip(world, vehicle, target, target.NextStep, saved.Loaded));
            trailer.Snap();
        }
        else
            trailer.Attach(trailerParade);
    }

    /// <summary>
    /// What a saved trip is for, or null when it no longer has a target that needs a delivery: a finished building or
    /// plot, or a house that asks for something else now.
    /// </summary>
    private ITripTarget? TripTarget(SavedTrip saved) =>
        saved.Kind switch
        {
            TripKinds.Build
                when saved.Index >= 0 && saved.Index < world.Projects.Length && !world.Projects[saved.Index].Complete =>
                world.Projects[saved.Index],
            TripKinds.Supply when world.WorksByIndex(saved.Index) is { } supplied => supplied,
            TripKinds.Plot
                when saved.Index >= 0
                    && saved.Index < world.Village.BuildPlots.Count
                    && !world.Village.BuildPlots[saved.Index].Complete => world.Village.BuildPlots[saved.Index],
            TripKinds.Playground when saved.Index == 0 && world.Playground is { Complete: false } yard => yard,
            TripKinds.House
                when saved.Index >= 0
                    && saved.Index < world.Village.Residents.Count
                    && HouseMaterial(saved.Index) is { } material => new HouseTripTarget(world, saved.Index, material),
            _ => null,
        };

    /// <summary>The deliveries made to each of the four buildings; zeros for buildings this valley lacks.</summary>
    private int[] BuildStages()
    {
        var stages = new int[BuildPlans.Count];
        foreach (var project in world.Projects)
            stages[project.Index] = project.Stage;
        return stages;
    }

    private static string TripInstruction(HaulTrip trip) =>
        trip switch
        {
            { Loaded: true, FarBank: true } => Texts.TedOdvezNakladNaDruhyBreh,
            { Loaded: true, Project: not null } => Texts.TedOdvezNakladNaStavbuA,
            { Loaded: true, House: >= 0 } => Texts.TedOdvezNakladPredDomecekA,
            { Loaded: true, Plot: not null } => Texts.TedOdvezNakladNaParceluA,
            { Loaded: true, Playground: not null } => Texts.TedOdvezNakladNaHristeA,
            { Loaded: true } => Texts.TedOdvezNakladDoZlutehoKruhu,
            _ => trip.Load switch
            {
                HaulLoad.Stones => Texts.JedSVozikemProKameniDo,
                HaulLoad.Soil => Texts.JedSVozikemProHlinu,
                HaulLoad.Logs => Texts.JedSVozikemProKladyNa,
                HaulLoad.Planks => Texts.JedSVozikemProPrknaNa,
                HaulLoad.Bricks => Texts.JedSVozikemProCihlyDo,
                HaulLoad.Gravel => Texts.JedSVozikemProSterkDo,
                HaulLoad.Glass => Texts.JedSVozikemProSklenickyDo,
                HaulLoad.Bread => Texts.JedSVozikemProChlebDo,
                HaulLoad.Wheat => Texts.JedSVozikemProObiliNa,
                HaulLoad.Fabric => Texts.JedSVozikemProLatkuK,
                HaulLoad.Capstone => Texts.JedSVozikemProKamenKe,
                HaulLoad.Flag => Texts.JedSVozikemProVlajeckuK,
                _ => Texts.JedSVozikemProPisek,
            },
        };
}
