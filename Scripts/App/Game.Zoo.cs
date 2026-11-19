using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.App;

/// <summary>
/// The village zoo: its keeper sends the trailer for the next of twelve deliveries of goods, and two of them
/// build an enclosure and bring the animal who lives in it. The twelfth finishes the zoo, with coins and a baby panda who follows
/// the car. These trips share the single task slot with expeditions and the other trailer trips.
/// </summary>
public partial class Game
{
    private Zoo? ZooOf(Character? actor) => (actor?.Role as ZooRole)?.Zoo;

    private HaulTrip? TripOf(Zoo zoo) => Haul != null && Haul.Zoo == zoo ? Haul : null;

    /// <summary>The goods the zoo's next delivery needs, or null once it is finished.</summary>
    private static HaulLoad? NextLoad(Zoo zoo) => zoo.Complete ? null : ZooPlans.CargoOf(zoo.Stage);

    /// <summary>Whether the next delivery can be fetched: the works must have a unit of the goods in stock.</summary>
    private bool ZooStepReady(Zoo zoo) =>
        NextLoad(zoo) is { } load && GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;

    internal void OpenZoo(Character keeper)
    {
        if (ZooOf(keeper) is not { } zoo)
            return;
        ClearChallenge();
        EnterInteraction(GameState.ZooStatus, keeper);
        ShowZoo();
        if (zoo.Complete)
        {
            Speak(Texts.ZvirataciJsouSpokojenaDekujuZaZoo, keeper);
            zoo.CheerAll();
            return;
        }
        if (Haul != null)
            return;
        Speak(
            !ZooStepReady(zoo) ? Texts.ZboziJesteNeniVyrobenePosluTe
                : zoo.Stage == 0 ? keeper.Greeting
                : Texts.PrivezesMiDalsiMaterial,
            keeper
        );
    }

    private void ShowZoo()
    {
        if (ZooOf(nearby) is not { } zoo)
            return;
        var trip = TripOf(zoo);
        bool free = Haul == null && !zoo.Complete;
        bool ready = ZooStepReady(zoo);
        ui.ZooPanel(
            nearby!,
            zoo,
            trip,
            free && ready,
            free && !ready && NextLoad(zoo) is { } load ? GoodsWorks(load) : null
        );
    }

    internal void BeginZooTrip() => BeginZooTrip(false);

    private void BeginZooTrip(bool quizzed)
    {
        if (
            State != GameState.ZooStatus
            || ZooOf(nearby) is not { Complete: false } zoo
            || Haul != null
            || !ZooStepReady(zoo)
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Zoo);
            return;
        }
        StartTrip(zoo);
    }

    /// <summary>The load is tipped out: the next enclosure stands and its animal has moved in.</summary>
    internal void FinishZooDump(HaulTrip trip)
    {
        var zoo = trip.Zoo!;
        zoo.SetStage(trip.Step + 1);
        zoo.Helper.Celebrate();
        zoo.CheerAll();
        if (zoo.Complete)
        {
            CompleteZoo(zoo);
            return;
        }
        CelebrateAt(zoo.Drop + Vector3.Up * 2, Texts.ZooPart(ZooPlans.Parts[trip.Step], zoo.Stage, ZooPlans.Trips));
        // The next load waits until the player asks the keeper for it again.
        EndHaul();
        Speak(Texts.DekujuAzBudesChtitPrivezDalsi, zoo.Helper);
        SaveProgress();
    }

    /// <summary>The last load has been tipped out: the zoo is finished and the baby panda is a new pet.</summary>
    private void CompleteZoo(Zoo zoo)
    {
        EndHaul();
        AddCoins(ZooPlans.Coins);
        CelebrateAt(zoo.Focus, WithPets(Texts.ZooDone(ZooPlans.Coins), EarnPets()));
        Speak(Texts.HuraZooJeHotova, zoo.Helper);
        var outward = followCamera.GlobalPosition - zoo.Focus;
        outward.Y = 0;
        followCamera.ShowMoment(zoo.Focus, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    /// <summary>The animals cheer when the car drives up to the zoo.</summary>
    private void TickZoo() => world.Zoo?.Tick(vehicle.Position);
}
