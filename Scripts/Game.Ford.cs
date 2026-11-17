using System;
using Godot;

namespace MathRacer;

/// <summary>
/// Trailer trips for buildings: each building has a builder who sends the next of ten deliveries (the bridge's
/// builder is the beaver). The trips share the single task slot with expeditions and supply trips.
/// </summary>
public partial class Game
{
    private Trailer trailer = null!;
    private bool trailerParade;
    private readonly Random loadRandom = new(Settings.Seed + 2313);

    /// <summary>
    /// The active trailer trip for a building or a works, which takes the same single task slot as
    /// an expedition.
    /// </summary>
    internal HaulTrip? Haul { get; private set; }

    /// <summary>The trailer is won by finishing the bridge; it is lent for every trip before that.</summary>
    internal bool TrailerOwned { get; private set; }
    internal Trailer Trailer => trailer;

    /// <summary>A delivery, collection or trailer trip is under way; only one runs at a time.</summary>
    internal bool TaskActive => Delivery != null || Haul != null;

    /// <summary>Accepting a new task switches off the one under way, together with its cargo.</summary>
    private void CancelActiveTask()
    {
        EndExpedition(refreshUi: false);
        EndHaul(refreshUi: false);
        trailer.SetLoad(null);
        deliveryReward = false;
    }

    /// <summary>Companions trail further back while the trailer is hitched.</summary>
    private float TrailerTrailShift => trailer?.Attached == true ? 3.5f : 0;

    private void SetupTrailer()
    {
        trailer = new Trailer(world, vehicle);
        world.AddChild(trailer);
        ui.BuildQuestRequested += BeginBuildTrip;
        ui.SupplyQuestRequested += BeginSupplyTrip;
        ui.MissingSupplyRequested += BeginMissingSupply;
        ui.LookoutRequested += BeginLookout;
        ui.TrailerToggleRequested += ToggleTrailer;
        ui.TrailerPaintSelected += SelectTrailerPaint;
    }

    private BuildProject? ProjectOf(Animal? actor) =>
        actor?.Role is BuilderRole role && role.Project < world.Projects.Length ? world.Projects[role.Project] : null;

    /// <summary>The works that makes a goods load, or null for any other load.</summary>
    private Works? GoodsWorks(HaulLoad load) =>
        HaulLoads.IsGoods(load) && (int)HaulLoads.ProductOf(load) < world.Industry.Length
            ? world.Industry[(int)HaulLoads.ProductOf(load)]
            : null;

    /// <summary>Whether the next delivery of a project can be fetched: goods need units in the works' stock.</summary>
    private bool StepReady(BuildProject project) =>
        project.Complete
        || !HaulLoads.IsGoods(project.Steps[project.Stage].Cargo)
        || GoodsWorks(project.Steps[project.Stage].Cargo) is { } works && works.Stock >= Works.PerTrip;

    internal void OpenBuilder(Animal builder)
    {
        if (ProjectOf(builder) is not { } project)
            return;
        ClearChallenge();
        EnterInteraction(GameState.BuildStatus, builder);
        ShowBuilder();
        if (Haul != null || project.Complete)
            return;
        Speak(
            !StepReady(project) ? "Zboží ještě není vyrobené. Pošlu tě pro surovinu!"
                : project.Stage == 0 ? builder.Greeting
                : "Přivezeš mi další náklad?",
            builder
        );
    }

    private void ShowBuilder()
    {
        if (ProjectOf(nearby) is not { } project)
            return;
        var trip = Haul is BuildTrip current && current.Project == project ? current : null;
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

    internal void BeginBuildTrip()
    {
        if (
            State != GameState.BuildStatus
            || ProjectOf(nearby) is not { Complete: false } project
            || Haul != null
            || !StepReady(project)
        )
            return;
        CancelActiveTask();
        StartHaul(new BuildTrip(world, vehicle, project, project.Stage, loaded: false));
        Start();
        Speak(TripInstruction(Haul!), project.Builder);
        SaveProgress();
    }

    private void StartHaul(HaulTrip trip)
    {
        EndHaul(refreshUi: false);
        Haul = trip;
        AddChild(Haul);
        if (!trailer.Attached)
            trailer.Attach(true);
        // Keep the bed lowering after a dump; only a different load replaces the contents.
        HaulLoad? load = trip.Loaded ? trip.Load : null;
        if (trailer.Load != load)
            trailer.SetLoad(load);
        ui.HaulHud(Haul);
        UpdateDeliveryGuides();
    }

    private void EndHaul(bool refreshUi = true)
    {
        if (Haul == null)
            return;
        RemoveChild(Haul);
        Haul.QueueFree();
        Haul = null;
        if (!refreshUi)
            return;
        ui.DeliveryHud(null);
        UpdateDeliveryGuides();
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
        foreach (var works in world.Industry)
            works.Worker.Guide(false);
        for (int i = 0; i < SourceCatalog.Count; i++)
            world.SourceWorkers[i]?.Guide(Haul == null && WorksOfSource(world.SourceWorkers[i]) is { Full: false });
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
            Speak("Zboží došlo. Zkus to znovu později.", trip.Asker);
            return;
        }
        trip.SetLoaded(true);
        trailer.Fill(trip.Load);
        ui.HaulHud(trip);
        UpdateHaulGuides();
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
        switch (trip)
        {
            case BuildTrip buildTrip:
                FinishBuildDump(buildTrip);
                break;
            case SupplyTrip supplyTrip:
                FinishSupplyDump(supplyTrip);
                break;
        }
    }

    private void FinishBuildDump(BuildTrip trip)
    {
        var project = trip.Project;
        project.SetStage(trip.Step + 1);
        project.Builder.Celebrate();
        if (project.Complete)
        {
            CompleteProject(project);
            return;
        }
        CelebrateAt(
            project.DropFor(trip.Step) + Vector3.Up * 2,
            $"{project.Builder.DisplayName} děkuje!  {project.Stage} / {BuildProject.Trips}"
        );
        // The next load waits until the player asks the builder for it again.
        EndHaul();
        Speak("Děkuju! Až budeš chtít, přijeď za mnou pro další náklad.", project.Builder);
        SaveProgress();
    }

    /// <summary>The last delivery has been tipped out: the building is finished.</summary>
    private void CompleteProject(BuildProject project)
    {
        EndHaul();
        PayBuildReward(project);
        if (project.Index == BuildPlans.Bridge)
        {
            TrailerOwned = true;
            trailerParade = false;
            CelebrateAt(project.Focus, "Most je hotový! Vozík je tvůj.");
            Speak("Hurá, most je hotový! Teď můžeš přejet řeku. Vozík si nech, najdeš ho v garáži.", project.Builder);
        }
        else
        {
            CelebrateAt(project.Focus, $"Stavba je hotová!  +{BuildCoins}  ·  {BuildItemName(project.Index)}");
            Speak("Hurá, je hotovo! Moc děkuju.", project.Builder);
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
            world.Projects[i].SetStage(data.BuildStages![i]);
        TrailerOwned = world.Ford is { Passable: true } && data.TrailerOwned;
        trailerParade = TrailerOwned && data.TrailerAttached;
        trailer.SetPaint(data.TrailerPaint);
        if (world.Duck is { } duck)
        {
            duck.Disabled = duck.IsOwned && data.DisabledCollectorPets.Contains(duck.Kind.ToString());
            PlaceCollectorPet(duck);
        }
        foreach (var works in world.Industry)
            works.SetStock(data.WorksStock[works.Site.Index]);
        if (data.BuildTrip >= 0 && data.BuildTrip < world.Projects.Length && !world.Projects[data.BuildTrip].Complete)
        {
            var project = world.Projects[data.BuildTrip];
            trailer.SetLoad(null);
            StartHaul(new BuildTrip(world, vehicle, project, project.Stage, data.BuildTripLoaded));
            trailer.Snap();
        }
        else if (data.SupplyTrip >= 0 && data.SupplyTrip < world.Industry.Length)
        {
            trailer.SetLoad(null);
            StartHaul(new SupplyTrip(world, vehicle, world.Industry[data.SupplyTrip], data.SupplyTripLoaded));
            trailer.Snap();
        }
        else
        {
            trailer.SetLoad(null);
            trailer.Attach(trailerParade);
        }
    }

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
            BuildTrip { Loaded: true, FarBank: true } => "Teď odvez náklad na druhý břeh řeky. Přejeď po velkém mostě.",
            BuildTrip { Loaded: true } => "Teď odvez náklad na stavbu a vysyp ho do žlutého kruhu.",
            { Loaded: true } => "Teď odvez náklad do žlutého kruhu před závodem.",
            _ => trip.Load switch
            {
                HaulLoad.Stones => "Jeď s vozíkem pro kamení do lomu.",
                HaulLoad.Soil => "Jeď s vozíkem pro hlínu.",
                HaulLoad.Logs => "Jeď s vozíkem pro klády na skládku.",
                HaulLoad.Planks => "Jeď s vozíkem pro prkna na pilu.",
                HaulLoad.Bricks => "Jeď s vozíkem pro cihly do cihelny.",
                HaulLoad.Gravel => "Jeď s vozíkem pro štěrk do štěrkovny.",
                HaulLoad.Fabric => "Jeď s vozíkem pro látku k farmáři Tondovi.",
                HaulLoad.Capstone => "Jeď s vozíkem pro kámen ke sběrateli drahokamů.",
                HaulLoad.Flag => "Jeď s vozíkem pro vlaječku k hračkáři Pepovi.",
                _ => "Jeď s vozíkem pro písek.",
            },
        };
}
