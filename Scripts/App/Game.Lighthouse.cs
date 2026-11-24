using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The lighthouse on the shore (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phases 3 and 4). The first letter to the keeper of
/// the sea makes the keeper want one; from then on the keeper's panel (state <see cref="GameState.LighthouseStatus"/>)
/// shows its eight deliveries and, with a green check mark, sends the trailer for the next: five loads of stone from the
/// coast quarry beside the road to the sea, then planks, bricks and glasses from the works of the old valley. The eighth
/// finishes it, with coins and the gull; by night its lantern turns its beam over the valley and a boat with lit windows
/// sails past. These trips share the single task slot with the others. Saved as <c>SaveData.Lighthouse</c>.
/// </summary>
public partial class Game
{
    private Lighthouse? Lighthouse => world.Lighthouse;

    /// <summary>The keeper of the sea has had a letter and wants the lighthouse: the panel shows its stages.</summary>
    private bool LighthouseWanted => Lighthouse != null && Post.Delivered[(int)Border.Sea] > 0;

    private HaulTrip? TripOf(Lighthouse lighthouse) => Haul != null && Haul.Lighthouse == lighthouse ? Haul : null;

    private static HaulLoad? NextLoad(Lighthouse lighthouse) =>
        lighthouse.Complete ? null : LighthousePlans.CargoOf(lighthouse.Stage);

    /// <summary>Stone can always be broken; goods must stand in their works.</summary>
    private bool LighthouseStepReady(Lighthouse lighthouse) =>
        NextLoad(lighthouse) is { } load
        && (load == HaulLoad.Stones || GoodsWorks(load) is { } works && works.Stock >= Works.PerTrip);

    /// <summary>The keeper of the sea shows the lighthouse once it is wanted; false when the keeper's own panel is shown.</summary>
    private bool OpenLighthouse(Character keeper)
    {
        if (!LighthouseWanted || Lighthouse is not { } lighthouse)
            return false;
        EnterInteraction(GameState.LighthouseStatus, keeper);
        ShowLighthouse();
        if (Haul != null)
            return true;
        Speak(
            lighthouse.Complete ? Texts.LighthouseShining
                : !LighthouseStepReady(lighthouse) ? Texts.ZboziJesteNeniVyrobenePosluTe
                : lighthouse.Stage == 0 ? Texts.LighthouseWanted
                : Texts.PrivezesMiDalsiMaterial,
            keeper
        );
        return true;
    }

    private void ShowLighthouse()
    {
        if (nearby is not { Role: BorderKeeperRole } keeper || Lighthouse is not { } lighthouse)
            return;
        bool free = Haul == null;
        bool ready = LighthouseStepReady(lighthouse);
        ui.LighthousePanel(
            keeper,
            lighthouse.Stage,
            TripOf(lighthouse),
            free && ready,
            free && !ready && NextLoad(lighthouse) is { } load ? GoodsWorks(load) : null,
            Post.Stamps((int)Border.Sea)
        );
    }

    /// <summary>The first letter to the keeper of the sea: the lighthouse is wanted from now on.</summary>
    private void UnlockLighthouse()
    {
        if (Lighthouse is { Complete: false, Stage: 0 })
            Speak(Texts.LighthouseWanted, world.LighthouseKeeper, interrupt: false);
    }

    internal void BeginLighthouseTrip() => BeginLighthouseTrip(false);

    private void BeginLighthouseTrip(bool quizzed)
    {
        if (
            State != GameState.LighthouseStatus
            || nearby is not { Role: BorderKeeperRole }
            || Lighthouse is not { Complete: false } lighthouse
            || Haul != null
            || !LighthouseStepReady(lighthouse)
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Lighthouse);
            return;
        }
        StartTrip(lighthouse);
    }

    /// <summary>The load is tipped out by the keeper's gate: the next part of the lighthouse stands.</summary>
    internal void FinishLighthouseDump(HaulTrip trip)
    {
        var lighthouse = trip.Lighthouse!;
        lighthouse.SetStage(trip.Step + 1);
        lighthouse.Keeper.Celebrate();
        if (lighthouse.Complete)
        {
            CompleteLighthouse(lighthouse);
            return;
        }
        CelebrateAt(
            lighthouse.Focus,
            Texts.LighthousePart(LighthousePlans.Parts[trip.Step], lighthouse.Stage, LighthousePlans.Trips)
        );
        // The next load waits until the player asks the keeper for it again.
        EndHaul();
        Speak(Texts.DekujuAzBudesChtitPrivezDalsi, lighthouse.Keeper);
        SaveProgress();
    }

    /// <summary>The eighth load: the lantern is up, coins are paid, the gull comes and the camera looks up at the tower.</summary>
    private void CompleteLighthouse(Lighthouse lighthouse)
    {
        EndHaul();
        AddCoins(LighthousePlans.Coins);
        CelebrateAt(lighthouse.TopFocus, WithPets(Texts.LighthouseDone(LighthousePlans.Coins), EarnPets()));
        Speak(Texts.LighthouseDoneSpeech, lighthouse.Keeper);
        var outward = followCamera.GlobalPosition - lighthouse.Focus;
        outward.Y = 0;
        followCamera.ShowMoment(lighthouse.Focus, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    /// <summary>Puts the lighthouse back after a load, before the trip and the pets are restored.</summary>
    private void RestoreLighthouse(int stage) =>
        Lighthouse?.SetStage(world.LighthouseBuildable ? stage : Buildings.Lighthouse.Stages);

    /// <summary>By night the lantern turns its beam and the boat sails past; by day both rest.</summary>
    private void TickLighthouse(double delta)
    {
        float night = State is GameState.Paused or GameState.Title ? -1 : Weather.Darkness;
        if (night < 0)
            return;
        if (Lighthouse is { } lighthouse)
        {
            lighthouse.Lit = lighthouse.Complete && LighthouseShown;
            lighthouse.Tick(delta, night);
        }
        world.NightBoat?.Tick(delta, LighthouseShown && Lighthouse is { Complete: true } ? night : 0);
    }

    /// <summary>Whether the lantern and the boat are drawn: the benchmark switches them off to measure their cost.</summary>
    internal bool LighthouseShown { get; set; } = true;
}
