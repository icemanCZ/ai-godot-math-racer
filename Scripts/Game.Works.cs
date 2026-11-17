using System.Linq;
using Godot;

namespace MathRacer;

/// <summary>
/// The sawmill, brickworks and gravel works. The worker at each raw-material site sends the trailer to the works
/// and asks the loading question on the spot; every load tipped on the works' ring adds goods to the visible stock.
/// The goods wait for the buildings.
/// </summary>
public partial class Game
{
    private Works? WorksOf(Animal? actor) =>
        actor?.Role is WorksRole role && role.Index < world.Industry.Length ? world.Industry[role.Index] : null;

    /// <summary>The works that a source worker supplies.</summary>
    private Works? WorksOfSource(Animal? actor)
    {
        if (actor?.Role is not SourceRole role || role.Index >= SourceCatalog.Count)
            return null;
        int index = WorksCatalog.ForInput(SourceCatalog.All[role.Index].Load);
        return index >= 0 && index < world.Industry.Length ? world.Industry[index] : null;
    }

    internal void OpenWorks(Animal worker)
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

    internal void OpenSource(Animal worker)
    {
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
        StartHaul(new SupplyTrip(world, vehicle, works, loaded: false));
        Start();
        Haul!.Arrived = true;
        BeginLoading();
        SaveProgress();
    }

    /// <summary>
    /// The next delivery needs goods the works has not made: the builder sends the player straight for the raw material
    /// (logs for planks, soil for bricks, stones for gravel), so no one has to look for the right worker.
    /// </summary>
    internal void BeginMissingSupply()
    {
        if (
            State != GameState.BuildStatus
            || ProjectOf(nearby) is not { Complete: false } project
            || Haul != null
            || StepReady(project)
            || GoodsWorks(project.Steps[project.Stage].Cargo) is not { Full: false } works
        )
            return;
        CancelActiveTask();
        StartHaul(new SupplyTrip(world, vehicle, works, loaded: false));
        Start();
        Speak(TripInstruction(Haul!), project.Builder);
        SaveProgress();
    }

    private void FinishSupplyDump(SupplyTrip trip)
    {
        var works = trip.Works;
        works.Deliver();
        works.Worker.Celebrate();
        CelebrateAt(works.Site.Position + Vector3.Up * 2, $"{works.Spec.Goods}  {works.Stock} / {Works.Capacity}");
        EndHaul();
        Speak(works.Full ? "Sklad je plný, děkuju!" : "Děkuju! Až budeš chtít, přivez další náklad.", works.Worker);
        SaveProgress();
    }
}
