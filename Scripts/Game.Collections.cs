namespace MathRacer;

public partial class Game
{
    private void OpenCollection(Animal owner)
    {
        nearby = owner; CurrentChallenge = null; rejectedAnswers.Clear(); vehicle.Active = false;
        if (Delivery?.Sender == owner && Delivery.CanDeliver) { BeginDeliveryQuestion(); return; }
        bool toys = owner.IsToymaker;
        int count = world.Village.CollectionCount(toys);
        if (Delivery != null || count >= Village.CollectionNames(toys).Length)
        {
            State = GameState.CollectionStatus; ui.CollectionStatus(world.Village, owner, Delivery); return;
        }
        BeginQuiz();
    }
}
