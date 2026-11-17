using Godot;

namespace MathRacer;

/// <summary>
/// The village's building plots: the newcomer on each one sends the trailer for the next of six deliveries of goods,
/// and when the last is tipped out the house is finished and the newcomer moves in. These trips share the single
/// task slot with expeditions and the other trailer trips.
/// </summary>
public partial class Game
{
    /// <summary>Coins paid when a house is finished and its resident has moved in.</summary>
    internal const int PlotCoins = 10;

    private HousePlot? PlotOf(Animal? actor) => (actor?.Role as PlotResidentRole)?.Plot;

    private PlotTrip? TripOf(HousePlot plot) => Haul is PlotTrip trip && trip.Plot == plot ? trip : null;

    /// <summary>The goods the plot's next delivery needs, or null once the house is finished.</summary>
    private static HaulLoad? NextLoad(HousePlot plot) => plot.Complete ? null : PlotPlans.CargoOf(plot.Stage);

    /// <summary>Whether the next delivery can be fetched: the works must have a unit of the goods in stock.</summary>
    private bool PlotStepReady(HousePlot plot) =>
        NextLoad(plot) is { } load && GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;

    internal void OpenPlot(Animal resident)
    {
        if (PlotOf(resident) is not { } plot)
            return;
        ClearChallenge();
        EnterInteraction(GameState.PlotStatus, resident);
        ShowPlot();
        if (plot.Complete)
        {
            Speak("Moc děkuju za domeček! Bydlí se mi tu skvěle.", resident);
            return;
        }
        if (Haul != null)
            return;
        Speak(
            !PlotStepReady(plot) ? "Zboží ještě není vyrobené. Pošlu tě pro surovinu!"
                : plot.Stage == 0 ? resident.Greeting
                : "Přivezeš mi další materiál?",
            resident
        );
    }

    private void ShowPlot()
    {
        if (PlotOf(nearby) is not { } plot)
            return;
        var trip = TripOf(plot);
        bool free = Haul == null && !plot.Complete;
        bool ready = PlotStepReady(plot);
        ui.PlotPanel(
            nearby!,
            plot,
            trip,
            free && ready,
            free && !ready && NextLoad(plot) is { } load ? GoodsWorks(load) : null
        );
    }

    internal void BeginPlotTrip() => BeginPlotTrip(false);

    private void BeginPlotTrip(bool quizzed)
    {
        if (
            State != GameState.PlotStatus
            || PlotOf(nearby) is not { Complete: false } plot
            || Haul != null
            || !PlotStepReady(plot)
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Plot);
            return;
        }
        CancelActiveTask();
        StartHaul(new PlotTrip(world, vehicle, plot, plot.Stage, loaded: false));
        Start();
        Speak(TripInstruction(Haul!), plot.Resident);
        SaveProgress();
    }

    /// <summary>The load is tipped out on the plot: the house rises another stage, the last one lets the newcomer in.</summary>
    private void FinishPlotDump(PlotTrip trip)
    {
        var plot = trip.Plot;
        plot.SetStage(trip.Step + 1, walkIn: true);
        plot.Resident.Celebrate();
        if (plot.Complete)
        {
            CompletePlot(plot);
            return;
        }
        CelebrateAt(
            plot.Drop + Vector3.Up * 2,
            $"{plot.Resident.DisplayName} děkuje!  {plot.Stage} / {HousePlot.Trips}"
        );
        // The next load waits until the player asks the newcomer for it again.
        EndHaul();
        Speak("Děkuju! Až budeš chtít, přivez další materiál.", plot.Resident);
        SaveProgress();
    }

    /// <summary>The sixth load has been tipped out: the house is finished and the newcomer moves in.</summary>
    private void CompletePlot(HousePlot plot)
    {
        EndHaul();
        AddCoins(PlotCoins);
        CelebrateAt(plot.Focus, $"Domeček je hotový! {plot.Resident.DisplayName} se nastěhoval.  +{PlotCoins}");
        Speak("Hurá, domeček je hotový! Teď tu budu bydlet. Moc děkuju!", plot.Resident);
        var outward = followCamera.GlobalPosition - plot.Focus;
        outward.Y = 0;
        followCamera.ShowMoment(plot.Focus, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    /// <summary>The deliveries made to each of the six plots.</summary>
    private int[] PlotStages() => world.Village.PlotStages;
}
