using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Tasks;

namespace MathRacer.App;

/// <summary>
/// The little railway round the playground: the stationmaster sends the trailer for the next of six deliveries of goods,
/// and each one adds to the track, the station or the train. The sixth finishes it, with coins and a chimney for the
/// car. These trips share the single task slot with expeditions and the other trailer trips.
/// </summary>
public partial class Game
{
    private Railway? RailwayOf(Character? actor) => (actor?.Role as StationRole)?.Railway;

    private HaulTrip? TripOf(Railway railway) => Haul != null && Haul.Railway == railway ? Haul : null;

    /// <summary>The goods the railway's next delivery needs, or null once it is finished.</summary>
    private static HaulLoad? NextLoad(Railway railway) => railway.Complete ? null : RailwayPlans.CargoOf(railway.Stage);

    /// <summary>Whether the next delivery can be fetched: the works must have a unit of the goods in stock.</summary>
    private bool RailwayStepReady(Railway railway) =>
        NextLoad(railway) is { } load && GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;

    internal void OpenRailway(Character master)
    {
        if (RailwayOf(master) is not { } railway)
            return;
        ClearChallenge();
        EnterInteraction(GameState.RailwayStatus, master);
        ShowRailway();
        if (railway.Complete)
        {
            Speak(Texts.VlacekJezdiDekujuZaNadrazi, master);
            railway.Whistle();
            return;
        }
        if (Haul != null)
            return;
        Speak(
            !RailwayStepReady(railway) ? Texts.ZboziJesteNeniVyrobenePosluTe
                : railway.Stage == 0 ? master.Greeting
                : Texts.PrivezesMiDalsiMaterial,
            master
        );
    }

    private void ShowRailway()
    {
        if (RailwayOf(nearby) is not { } railway)
            return;
        var trip = TripOf(railway);
        bool free = Haul == null && !railway.Complete;
        bool ready = RailwayStepReady(railway);
        ui.RailwayPanel(
            nearby!,
            railway,
            trip,
            free && ready,
            free && !ready && NextLoad(railway) is { } load ? GoodsWorks(load) : null
        );
    }

    internal void BeginRailwayTrip() => BeginRailwayTrip(false);

    private void BeginRailwayTrip(bool quizzed)
    {
        if (
            State != GameState.RailwayStatus
            || RailwayOf(nearby) is not { Complete: false } railway
            || Haul != null
            || !RailwayStepReady(railway)
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Railway);
            return;
        }
        StartTrip(railway);
    }

    /// <summary>The load is tipped out: the next part of the track or the train is there.</summary>
    internal void FinishRailwayDump(HaulTrip trip)
    {
        var railway = trip.Railway!;
        railway.SetStage(trip.Step + 1);
        railway.Helper.Celebrate();
        railway.CheerAll();
        if (railway.Complete)
        {
            CompleteRailway(railway);
            return;
        }
        if (railway.TrainRunning)
            railway.Whistle();
        CelebrateAt(
            railway.Drop + Vector3.Up * 2,
            Texts.RailwayPart(RailwayPlans.Parts[trip.Step], railway.Stage, RailwayPlans.Trips)
        );
        // The next load waits until the player asks the stationmaster for it again.
        EndHaul();
        Speak(Texts.DekujuAzBudesChtitPrivezDalsi, railway.Helper);
        SaveProgress();
    }

    /// <summary>The sixth load has been tipped out: the railway is finished and the chimney is the car's.</summary>
    private void CompleteRailway(Railway railway)
    {
        EndHaul();
        AddCoins(RailwayPlans.Coins);
        economy.Owned.Add(new CarReward(CarAccessory.Chimney.Id, true));
        CelebrateAt(railway.Focus, Texts.RailwayDone(RailwayPlans.Coins, CarAccessory.Chimney.Name));
        Speak(Texts.HuraNadraziJeHotove, railway.Helper);
        railway.Whistle();
        var outward = followCamera.GlobalPosition - railway.Focus;
        outward.Y = 0;
        followCamera.ShowMoment(railway.Focus, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    /// <summary>The train whistles when the car drives up to the railway.</summary>
    private void TickRailway() => world.Railway?.Tick(vehicle.Position);
}
