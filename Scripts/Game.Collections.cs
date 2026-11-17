namespace MathRacer;

/// <summary>Farm, garden, museum and playground: one off-road expedition per display item.</summary>
public partial class Game
{
    internal void OpenCollector(Animal owner, CollectorRole role)
    {
        nearby = owner;
        ClearChallenge();
        vehicle.Active = false;
        if (Delivery?.Sender == owner && Delivery.CanDeliver)
        {
            BeginDeliveryQuestion();
            return;
        }
        if (TaskActive || role.Count(world.Village) >= role.Total)
        {
            State = role.StatusState;
            role.ShowStatus(ui, world.Village, owner, Delivery);
            return;
        }
        // Every new trip requires a fresh quiz; an abandoned quiz never unlocks a departure.
        BeginQuiz();
    }

    internal void StartCollectorExpedition(Animal owner, CollectorRole role)
    {
        if (TaskActive || role.Count(world.Village) >= role.Total)
            return;
        StartExpedition(owner, true);
        SaveProgress();
    }

    private void CompleteCollectorDelivery(CollectorRole role)
    {
        var owner = nearby!;
        var pet = world.Village.PetFor(owner);
        int count = pet.Count + 1;
        CelebrateCompletion(
            owner,
            count == pet.Goal ? $"Nový mazlíček: {pet.DisplayName}!" : role.DeliveredMessage,
            true
        );
        EndExpedition(refreshUi: false);
        role.Restore(world.Village, count);
        AddCoins(CollectorDeliveryCoins);
        ClearChallenge();
        ui.DeliveryHud(null);
        UpdateDeliveryGuides();
        State = role.StatusState;
        role.ShowStatus(ui, world.Village, owner, null, returned: true);
        ShowCelebrationView();
        SaveProgress();
    }
}
