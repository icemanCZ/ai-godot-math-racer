using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Tasks;

namespace MathRacer.App;

/// <summary>
/// The village playground: its keeper sends the trailer for the next of six deliveries of goods, and each one brings a
/// toy and the child who plays on it. The sixth finishes it, with coins and a bunch of balloons for the car. These trips
/// share the single task slot with expeditions and the other trailer trips.
/// </summary>
public partial class Game
{
    private Playground? PlaygroundOf(Character? actor) => (actor?.Role as PlaygroundRole)?.Playground;

    private HaulTrip? TripOf(Playground yard) => Haul != null && Haul.Playground == yard ? Haul : null;

    /// <summary>The goods the playground's next delivery needs, or null once it is finished.</summary>
    private static HaulLoad? NextLoad(Playground yard) => yard.Complete ? null : PlaygroundPlans.CargoOf(yard.Stage);

    /// <summary>Whether the next delivery can be fetched: the works must have a unit of the goods in stock.</summary>
    private bool PlaygroundStepReady(Playground yard) =>
        NextLoad(yard) is { } load && GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;

    internal void OpenPlayground(Character keeper)
    {
        if (PlaygroundOf(keeper) is not { } yard)
            return;
        ClearChallenge();
        EnterInteraction(GameState.PlaygroundStatus, keeper);
        ShowPlayground();
        if (yard.Complete)
        {
            Speak(Texts.MocDekujuZaHristeDetiSeSmeji, keeper);
            yard.CheerAll();
            return;
        }
        if (Haul != null)
            return;
        Speak(
            !PlaygroundStepReady(yard) ? Texts.ZboziJesteNeniVyrobenePosluTe
                : yard.Stage == 0 ? keeper.Greeting
                : Texts.PrivezesMiDalsiMaterial,
            keeper
        );
    }

    private void ShowPlayground()
    {
        if (PlaygroundOf(nearby) is not { } yard)
            return;
        var trip = TripOf(yard);
        bool free = Haul == null && !yard.Complete;
        bool ready = PlaygroundStepReady(yard);
        ui.PlaygroundPanel(
            nearby!,
            yard,
            trip,
            free && ready,
            free && !ready && NextLoad(yard) is { } load ? GoodsWorks(load) : null
        );
    }

    internal void BeginPlaygroundTrip() => BeginPlaygroundTrip(false);

    private void BeginPlaygroundTrip(bool quizzed)
    {
        if (
            State != GameState.PlaygroundStatus
            || PlaygroundOf(nearby) is not { Complete: false } yard
            || Haul != null
            || !PlaygroundStepReady(yard)
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Playground);
            return;
        }
        StartTrip(yard);
    }

    /// <summary>The load is tipped out in the yard: the next toy is there and its child starts to play.</summary>
    internal void FinishPlaygroundDump(HaulTrip trip)
    {
        var yard = trip.Playground!;
        yard.SetStage(trip.Step + 1);
        yard.Helper.Celebrate();
        yard.CheerAll();
        if (yard.Complete)
        {
            CompletePlayground(yard);
            return;
        }
        CelebrateAt(
            yard.Drop + Vector3.Up * 2,
            Texts.PlaygroundToy(PlaygroundPlans.Toys[trip.Step], yard.Stage, PlaygroundPlans.Trips)
        );
        // The next load waits until the player asks the keeper for it again.
        EndHaul();
        Speak(Texts.DekujuAzBudesChtitPrivezDalsi, yard.Helper);
        SaveProgress();
    }

    /// <summary>The sixth load has been tipped out: the playground is finished and the balloons are the car's.</summary>
    private void CompletePlayground(Playground yard)
    {
        EndHaul();
        AddCoins(PlaygroundPlans.Coins);
        economy.Owned.Add(new CarReward(CarAccessory.Balloons.Id, true));
        CelebrateAt(yard.Focus, Texts.PlaygroundDone(PlaygroundPlans.Coins, CarAccessory.Balloons.Name));
        Speak(Texts.HuraHristeJeHotove, yard.Helper);
        var outward = followCamera.GlobalPosition - yard.Focus;
        outward.Y = 0;
        followCamera.ShowMoment(yard.Focus, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    /// <summary>The children cheer when the car drives up to the playground.</summary>
    private void TickPlayground() => world.Playground?.Tick(vehicle.Position);
}
