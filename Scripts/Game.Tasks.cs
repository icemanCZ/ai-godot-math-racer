namespace MathRacer;

/// <summary>
/// The single task slot of the game (<see cref="TaskSlot"/>): what is under way, and how a new task replaces it.
/// </summary>
public partial class Game
{
    private readonly TaskSlot tasks;

    public Game()
    {
        tasks = new TaskSlot(this, () => trailer, ui, UpdateDeliveryGuides);
        RegisterSystems();
        RegisterPanels();
    }

    /// <summary>The delivery or collection under way.</summary>
    internal Expedition? Delivery => tasks.Delivery;

    /// <summary>
    /// The active trailer trip for a building, a works, a house or a plot, which takes the same single task slot as
    /// an expedition.
    /// </summary>
    internal HaulTrip? Haul => tasks.Haul;

    /// <summary>A delivery, collection or trailer trip is under way; only one runs at a time.</summary>
    internal bool TaskActive => tasks.Active;

    /// <summary>Accepting a new task switches off the one under way, together with its cargo.</summary>
    private void CancelActiveTask()
    {
        tasks.Cancel();
        deliveryReward = false;
    }

    private void EndExpedition(bool refreshUi = true) => tasks.EndExpedition(refreshUi);

    private void StartHaul(HaulTrip trip) => tasks.Begin(trip);

    private void EndHaul(bool refreshUi = true) => tasks.EndHaul(refreshUi);
}
