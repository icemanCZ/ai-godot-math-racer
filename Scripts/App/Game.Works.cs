using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.App;

/// <summary>
/// The sawmill, brickworks, gravel works and glassworks. The worker at each raw-material site sends the trailer to the works
/// and asks the loading question on the spot; every load tipped on the works' ring adds goods to the visible stock.
/// The goods wait for the buildings.
/// </summary>
public partial class Game
{
    private Works? WorksOf(Character? actor) => actor?.Role is WorksRole role ? world.WorksByIndex(role.Index) : null;

    /// <summary>The works that a source worker supplies.</summary>
    private Works? WorksOfSource(Character? actor)
    {
        if (actor?.Role is not SourceRole role)
            return null;
        return world.WorksByIndex(WorksCatalog.ForInput(role.Load));
    }

    internal void OpenWorks(Character worker)
    {
        if (WorksOf(worker) is not { } works)
            return;
        ClearChallenge();
        EnterInteraction(GameState.WorksStatus, worker);
        ShowWorks();
        Speak(works.Full ? Texts.SkladJePlnyDekuju : worker.Greeting, worker);
    }

    private void ShowWorks()
    {
        if (WorksOf(nearby) is { } works)
            ui.WorksPanel(nearby!, works, Haul, canStart: false);
    }

    internal void OpenSource(Character worker)
    {
        // Where the valley has no glassworks the sand pit's worker has no works to supply: he only greets the driver.
        if (worker.Role is SourceRole { Load: HaulLoad.Sand } && WorksOfSource(worker) == null)
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
            Speak(Texts.SkladZavoduJePlnyZkusTo, worker);
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
        else if (State == GameState.PlaygroundStatus)
        {
            // The playground's keeper, too, sends the player for the raw material of goods that are not made yet.
            if (
                PlaygroundOf(nearby) is not { Complete: false } yard
                || Haul != null
                || PlaygroundStepReady(yard)
                || GoodsWorks(PlaygroundPlans.CargoOf(yard.Stage)) is not { Full: false } missing
            )
                return;
            (works, asker) = (missing, yard.Helper);
        }
        else if (State == GameState.RailwayStatus)
        {
            // So does the stationmaster, for the goods of the track and the station.
            if (
                RailwayOf(nearby) is not { Complete: false } railway
                || Haul != null
                || RailwayStepReady(railway)
                || GoodsWorks(RailwayPlans.CargoOf(railway.Stage)) is not { Full: false } missing
            )
                return;
            (works, asker) = (missing, railway.Helper);
        }
        else if (State == GameState.ZooStatus)
        {
            // And the zoo keeper, for the goods of the paths, fences and houses of the animals.
            if (
                ZooOf(nearby) is not { Complete: false } zoo
                || Haul != null
                || ZooStepReady(zoo)
                || GoodsWorks(ZooPlans.CargoOf(zoo.Stage)) is not { Full: false } missing
            )
                return;
            (works, asker) = (missing, zoo.Helper);
        }
        else if (State == GameState.HouseMaterialOffer)
        {
            // A resident who wants goods that the works has not made does the same: the way to the raw material.
            if (
                nearby is not { Role: HouseResidentRole { House: var house } } resident
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
        world.Tally.AddRaw(trip.Load);
        world.Tally.AddWorks(works.Site.Index);
        works.Worker.Celebrate();
        CelebrateAt(
            works.Site.Position + Vector3.Up * 2,
            WithPets($"{works.Spec.Goods}  {works.Stock} / {Works.Capacity}", EarnPets())
        );
        EndHaul();
        Speak(works.Full ? Texts.SkladJePlnyDekuju2 : Texts.DekujuAzBudesChtitPrivezDalsi2, works.Worker);
        SaveProgress();
    }
}
