using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The hunter's hut, which the player raises before the first hunt: like the zoo keeper, the hunter sends the trailer for
/// the next of four deliveries of goods (the foundation, the walls, the roof and the chimney), and the fourth finishes the
/// hut, with coins, after which he sends the player hunting. These trips share the single task slot with expeditions and
/// the other trailer trips.
/// </summary>
public partial class Game
{
    private HaulTrip? TripOf(HunterPen pen) => Haul != null && Haul.HunterHut == pen ? Haul : null;

    /// <summary>The goods the hut's next delivery needs, or null once it stands.</summary>
    private static HaulLoad? NextLoad(HunterPen pen) => pen.Complete ? null : HutPlans.CargoOf(pen.Stage);

    /// <summary>Whether the next delivery can be fetched: the works must have a unit of the goods in stock.</summary>
    private bool HutStepReady(HunterPen pen) =>
        NextLoad(pen) is { } load && GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip;

    /// <summary>The hunter's panel while his hut is not finished; the interaction is entered already.</summary>
    private void OpenHut(Character hunter, HunterPen pen)
    {
        ShowHunter();
        if (Haul != null)
            return;
        Speak(
            !HutStepReady(pen) ? Texts.ZboziJesteNeniVyrobenePosluTe
                : pen.Stage == 0 ? Texts.HunterHutGreeting
                : Texts.PrivezesMiDalsiMaterial,
            hunter
        );
    }

    private void ShowHut(Character hunter, HunterPen pen)
    {
        var trip = TripOf(pen);
        bool free = Haul == null;
        bool ready = HutStepReady(pen);
        ui.HunterHutPanel(
            hunter,
            pen.Stage,
            trip,
            free && ready,
            free && !ready && NextLoad(pen) is { } load ? GoodsWorks(load) : null
        );
    }

    internal void BeginHutTrip() => BeginHutTrip(false);

    private void BeginHutTrip(bool quizzed)
    {
        if (
            State != GameState.HunterStatus
            || nearby is not { Role: HunterRole }
            || world.Pen is not { Complete: false } pen
            || Haul != null
            || !HutStepReady(pen)
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Hunter);
            return;
        }
        StartTrip(pen);
    }

    /// <summary>The load is tipped out at the gate: the next part of the hut stands.</summary>
    internal void FinishHutDump(HaulTrip trip)
    {
        var pen = trip.HunterHut!;
        pen.SetStage(trip.Step + 1);
        pen.Hunter.Celebrate();
        if (pen.Complete)
        {
            CompleteHut(pen);
            return;
        }
        CelebrateAt(
            pen.Site.Drop + Vector3.Up * 2,
            Texts.HutPart(HutPlans.Parts[trip.Step], pen.Stage, HutPlans.Trips)
        );
        // The next load waits until the player asks the hunter for it again.
        EndHaul();
        Speak(Texts.DekujuAzBudesChtitPrivezDalsi, pen.Hunter);
        SaveProgress();
    }

    /// <summary>The fourth load has been tipped out: the hut stands, the chimney smokes and the hunt can begin.</summary>
    private void CompleteHut(HunterPen pen)
    {
        EndHaul();
        AddCoins(HutPlans.Coins);
        CelebrateAt(pen.HutFocus, Texts.HutDone(HutPlans.Coins));
        Speak(Texts.HunterHutDone, pen.Hunter);
        var outward = followCamera.GlobalPosition - pen.HutFocus;
        outward.Y = 0;
        followCamera.ShowMoment(pen.HutFocus, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        UpdateDeliveryGuides();
        SaveProgress();
    }
}
