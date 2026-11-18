using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.App;

/// <summary>
/// The sawmill, brickworks and gravel works. The worker at each raw-material site sends the trailer to the works
/// and asks the loading question on the spot; every load tipped on the works' ring adds goods to the visible stock.
/// The goods wait for the buildings.
/// </summary>
public partial class Game
{
    private Works? WorksOf(Character? actor) =>
        actor?.Role is WorksRole role && role.Index < world.Industry.Length ? world.Industry[role.Index] : null;

    /// <summary>The works that a source worker supplies.</summary>
    private Works? WorksOfSource(Character? actor)
    {
        if (actor?.Role is not SourceRole role)
            return null;
        int index = WorksCatalog.ForInput(role.Load);
        return index >= 0 && index < world.Industry.Length ? world.Industry[index] : null;
    }

    internal void OpenWorks(Character worker)
    {
        if (WorksOf(worker) is not { } works)
            return;
        ClearChallenge();
        EnterInteraction(GameState.WorksStatus, worker);
        ShowWorks();
        Speak(works.Full ? "Sklad je plný. Děkuju!" : worker.Greeting, worker);
    }

    private void ShowWorks()
    {
        if (WorksOf(nearby) is { } works)
            ui.WorksPanel(nearby!, works, Haul, canStart: false);
    }

    internal void OpenSource(Character worker)
    {
        // The sand pit's worker has no works to supply: he only greets the driver.
        if (worker.Role is SourceRole { Load: HaulLoad.Sand })
        {
            Speak(worker.Greeting, worker);
            return;
        }
        if (WorksOfSource(worker) is not { } works)
            return;
        ClearChallenge();
        EnterInteraction(GameState.SourceStatus, worker);
        ShowSource();
        if (works.Full)
            Speak("Sklad závodu je plný. Zkus to později.", worker);
        else if (Haul == null)
            Speak(worker.Greeting, worker);
    }

    private void ShowSource()
    {
        if (WorksOfSource(nearby) is { } works)
            ui.WorksPanel(nearby!, works, Haul, canStart: Haul == null && !works.Full);
    }

    /// <summary>The source worker starts the trip: the loading question follows at once, then the load goes to the works.</summary>
    internal void BeginSupplyTrip()
    {
        if (State != GameState.SourceStatus || WorksOfSource(nearby) is not { } works || Haul != null || works.Full)
            return;
        CancelActiveTask();
        StartHaul(new HaulTrip(world, vehicle, works, 0, loaded: false));
        Start();
        Haul!.Arrived = true;
        BeginLoading();
        SaveProgress();
    }

    /// <summary>
    /// The next delivery needs goods the works has not made: the builder sends the player straight for the raw material
    /// (logs for planks, soil for bricks, stones for gravel), so no one has to look for the right worker.
    /// </summary>
    internal void BeginMissingSupply() => BeginMissingSupply(false);

    private void BeginMissingSupply(bool quizzed)
    {
        Works works;
        Character asker;
        if (State == GameState.BuildStatus)
        {
            if (
                ProjectOf(nearby) is not { Complete: false } project
                || Haul != null
                || StepReady(project)
                || GoodsWorks(project.Steps[project.Stage].Cargo) is not { Full: false } missing
            )
                return;
            (works, asker) = (missing, project.Builder);
        }
        else if (State == GameState.PlotStatus)
        {
            // A newcomer who waits for goods that the works has not made sends the player for the raw material too.
            if (
                PlotOf(nearby) is not { Complete: false } plot
                || Haul != null
                || PlotStepReady(plot)
                || GoodsWorks(PlotPlans.CargoOf(plot.Stage)) is not { Full: false } missing
            )
                return;
            (works, asker) = (missing, plot.Resident);
        }
        else if (State == GameState.HouseMaterialOffer)
        {
            // A resident who wants goods that the works has not made does the same: the way to the raw material.
            if (
                nearby is not { Role: HouseResidentRole { House: var house }, RewardAvailable: true } resident
                || HouseMaterial(house) is not { } load
                || TripOf(resident) != null
                || GoodsWorks(load) is not { Full: false } missing
                || missing.Stock >= Works.PerTrip
                || (Haul is { Works: { } supply } && supply == missing)
            )
                return;
            (works, asker) = (missing, resident);
        }
        else
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Missing);
            return;
        }
        StartTrip(works, asker);
    }

    internal void FinishSupplyDump(HaulTrip trip)
    {
        var works = trip.Works!;
        works.Deliver();
        works.Worker.Celebrate();
        CelebrateAt(works.Site.Position + Vector3.Up * 2, $"{works.Spec.Goods}  {works.Stock} / {Works.Capacity}");
        EndHaul();
        Speak(works.Full ? "Sklad je plný, děkuju!" : "Děkuju! Až budeš chtít, přivez další náklad.", works.Worker);
        SaveProgress();
    }
}
