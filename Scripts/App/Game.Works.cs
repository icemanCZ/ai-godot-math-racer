using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The five works (the sawmill, the brickworks, the gravel works, the glassworks and the bakery) and the mill that grinds
/// flour for the bakery. The worker at each raw-material site sends the trailer to the works and asks the loading question
/// on the spot; every load tipped on the works' ring adds goods to the visible stock. The goods wait for the buildings,
/// the residents, the lamp maker and the baker.
/// </summary>
public partial class Game
{
    private Works? WorksOf(Character? actor) => actor?.Role is WorksRole role ? world.WorksByIndex(role.Index) : null;

    /// <summary>
    /// The works that a source worker supplies. The farmer's wheat goes to the mill (<see cref="World.FlourMill"/>), so
    /// he supplies a works only in a valley without the mill, whose bakery takes the wheat itself.
    /// </summary>
    private Works? WorksOfSource(Character? actor)
    {
        if (actor?.Role is not SourceRole role)
            return null;
        if (role.Load == HaulLoad.Wheat)
            return world.Bakery is { Input: HaulLoad.Wheat } bakery ? bakery : null;
        return world.WorksByIndex(WorksCatalog.ForInput(role.Load));
    }

    /// <summary>The mill that grinds the wheat of this farmer, or null for anyone else and in a valley without the mill.</summary>
    private FlourMill? MillOfFarmer(Character? actor) =>
        actor != null && actor == world.FieldFarmer ? world.FlourMill : null;

    /// <summary>The mill whose miller this is, or null for anyone else and in a valley without the bread chain.</summary>
    private FlourMill? MillOfMiller(Character? actor) =>
        world.FlourMill is { } mill && actor == mill.Miller ? mill : null;

    /// <summary>Whether the miller can load flour for the bakery now: a finished mill with flour and room in the bakery.</summary>
    private bool FlourReady(FlourMill mill) =>
        mill.Grinding && mill.Stock >= FlourMill.PerTrip && world.Bakery is { Full: false };

    /// <summary>
    /// The trip that brings the bakery closer to bread: flour from the mill when it has some, else the farmer's wheat for
    /// the mill. Null while the mill is not finished, when the mill is full of flour that the full bakery cannot take, and
    /// in a valley without the bakery. A valley without the mill takes the wheat straight to the bakery.
    /// </summary>
    private ITripTarget? BreadSupply()
    {
        if (world.Bakery is not { Full: false } bakery)
            return null;
        if (world.FlourMill is not { } mill)
            return bakery.Input == HaulLoad.Wheat ? bakery : null;
        if (!mill.Grinding)
            return null;
        if (mill.Stock >= FlourMill.PerTrip)
            return bakery;
        return mill.Full ? null : mill;
    }

    /// <summary>Whether a trip of the bread chain is under way: wheat for the mill or flour (or wheat) for the bakery.</summary>
    private bool BreadSupplyUnderWay =>
        Haul != null && (Haul.Target is FlourMill || world.Bakery != null && Haul.Works == world.Bakery);

    internal void OpenWorks(Character worker)
    {
        if (WorksOf(worker) is not { } works)
            return;
        ClearChallenge();
        EnterInteraction(GameState.WorksStatus, worker);
        ShowWorks();
        Speak(works.Full ? Texts.SkladJePlnyDekuju : worker.Greeting, worker);
    }

    /// <summary>The pieces a works has made in all, those already taken out of its stock included.</summary>
    private int WorksMade(Works works) => world.WorksMade(works);

    /// <summary>Counts the goods that stand in a works as made, before the stock changes and they can no longer be seen.</summary>
    internal void CountStockMade(Works works)
    {
        int loads = (works.Stock + Works.PerLoad - 1) / Works.PerLoad;
        world.Tally.RaiseWorks(works.Site.Index, loads);
        world.Tally.RaiseRaw(works.Input, loads);
    }

    private static string WorksPetName(Works works) => WorldPetCatalog.ForWorks(works.Site.Index).Name;

    private void ShowWorks()
    {
        if (WorksOf(nearby) is { } works)
            ui.WorksPanel(nearby!, works, Haul, canStart: false, WorksMade(works), WorksPetName(works));
    }

    internal void OpenSource(Character worker)
    {
        if (MillOfFarmer(worker) is { } mill)
        {
            OpenField(worker, mill);
            return;
        }
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

    /// <summary>The farmer at the wheat field sends the trailer to the mill, once the mill is finished and has room.</summary>
    private void OpenField(Character farmer, FlourMill mill)
    {
        ClearChallenge();
        EnterInteraction(GameState.SourceStatus, farmer);
        ShowSource();
        if (!mill.Grinding)
            Speak(Texts.MlynJesteNemele, farmer);
        else if (mill.Full)
            Speak(Texts.MlynJePlnyMouky, farmer);
        else if (Haul == null)
            Speak(farmer.Greeting, farmer);
    }

    private void ShowSource()
    {
        if (MillOfFarmer(nearby) is { } mill)
        {
            var trip = Haul is { Target: FlourMill } wheat ? wheat : null;
            ui.MillPanel(nearby!, mill, trip, Haul == null && mill.Grinding && !mill.Full, forBakery: false);
            return;
        }
        if (WorksOfSource(nearby) is { } works)
            ui.WorksPanel(
                nearby!,
                works,
                Haul,
                canStart: Haul == null && !works.Full,
                WorksMade(works),
                WorksPetName(works)
            );
    }

    /// <summary>
    /// The source worker starts the trip: the loading question follows at once, then the load goes to the works. The
    /// farmer sends his wheat to the mill, and the miller of a finished mill sends its flour to the bakery.
    /// </summary>
    internal void BeginSupplyTrip()
    {
        if (Haul != null)
            return;
        if (State == GameState.SourceStatus && MillOfFarmer(nearby) is { } mill)
        {
            if (mill.Grinding && !mill.Full)
                StartLoadingTrip(mill);
            return;
        }
        if (State == GameState.BuildStatus && MillOfMiller(nearby) is { } flour)
        {
            if (FlourReady(flour))
                StartLoadingTrip(world.Bakery!);
            return;
        }
        if (State != GameState.SourceStatus || WorksOfSource(nearby) is not { } works || works.Full)
            return;
        StartLoadingTrip(works);
    }

    /// <summary>A trip that is loaded where it is offered: the worker there asks the loading question at once.</summary>
    private void StartLoadingTrip(ITripTarget target)
    {
        CancelActiveTask();
        StartHaul(new HaulTrip(world, vehicle, target, 0, loaded: false));
        Start();
        Haul!.Arrived = true;
        BeginLoading();
        SaveProgress();
    }

    /// <summary>The finished mill's miller: flour for the bakery, and how much of it stands beside the ring.</summary>
    private void OpenMiller(Character miller, FlourMill mill)
    {
        ShowMiller(mill);
        if (Haul != null)
            return;
        Speak(
            mill.Stock < FlourMill.PerTrip ? Texts.NemamMoukuPrivezObili
                : world.Bakery!.Full ? Texts.PekarnaMaPlnoChleba
                : Texts.NalozimTiMoukuProPekarnu,
            miller
        );
    }

    private void ShowMiller(FlourMill mill)
    {
        var trip = Haul is { Load: HaulLoad.Flour } flour && flour.Works == world.Bakery ? flour : null;
        ui.MillPanel(nearby!, mill, trip, Haul == null && FlourReady(mill), forBakery: true);
    }

    /// <summary>The wheat is tipped out on the mill's ring: the mill grinds it into two more sacks of flour.</summary>
    internal void FinishMillDump(HaulTrip trip)
    {
        var mill = world.FlourMill!;
        mill.Deliver();
        world.Tally.AddRaw(trip.Load);
        mill.Miller.Celebrate();
        CelebrateAt(
            mill.Mill.Site.Position + Vector3.Up * 2,
            WithPets($"{Texts.Mouka}  {mill.Stock} / {FlourMill.Capacity}", EarnPets())
        );
        EndHaul();
        Speak(mill.Full ? Texts.MlynJePlnyDekuju : Texts.SemeluZObiliMouku, mill.Miller);
        SaveProgress();
    }

    /// <summary>
    /// The next delivery needs goods the works has not made: whoever wants them sends the player straight for the raw
    /// material (logs for planks, soil for bricks, stones for gravel, sand for glasses, flour or wheat for bread), so no one
    /// has to look for the right worker.
    /// </summary>
    internal void BeginMissingSupply() => BeginMissingSupply(false);

    private void BeginMissingSupply(bool quizzed)
    {
        ITripTarget works;
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
        else if (State == GameState.HunterStatus)
        {
            // The hunter, too, for the goods of his hut.
            if (
                world.Pen is not { Complete: false } pen
                || nearby is not { Role: HunterRole }
                || Haul != null
                || HutStepReady(pen)
                || GoodsWorks(HutPlans.CargoOf(pen.Stage)) is not { Full: false } missing
            )
                return;
            (works, asker) = (missing, pen.Hunter);
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
                || (missing == world.Bakery && BreadSupplyUnderWay)
            )
                return;
            // Bread is baked from flour and the flour ground from wheat: the resident sends the player for what is missing.
            if (missing == world.Bakery)
            {
                if (BreadSupply() is not { } bread)
                    return;
                (works, asker) = (bread, resident);
            }
            else
                (works, asker) = (missing, resident);
        }
        else if (State == GameState.GlassmakerShop)
        {
            // The lamp maker, whose glassworks has no glasses for him, sends the player for sand.
            if (nearby is not { Role: GlassmakerRole } maker || Haul != null || GlassmakerMissingGlass is not { } glass)
                return;
            (works, asker) = (glass, maker);
        }
        else if (State == GameState.TradeStatus)
        {
            // The baker of the village shop, whose bakery has run out of bread, sends the player along the chain too.
            if (
                nearby is not { Role: TraderRole } baker
                || Delivery?.Sender == baker
                || !BakesFromBakery(baker)
                || OrderBaked(baker)
                || Haul != null
                || BreadSupply() is not { } bread
            )
                return;
            (works, asker) = (bread, baker);
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
        CountStockMade(works);
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
