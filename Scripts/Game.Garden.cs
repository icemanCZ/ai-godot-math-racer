namespace MathRacer;

public partial class Game
{
    private void OpenGarden()
    {
        nearby = world.Village.Gardener;
        CurrentChallenge = null; rejectedAnswers.Clear(); vehicle.Active = false;
        if (Delivery?.Garden == true && Delivery.CanDeliver) { BeginDeliveryQuestion(); return; }
        if (Delivery != null || world.Village.GardenCount >= Village.FlowerNames.Length)
        {
            State = GameState.GardenStatus; ui.GardenStatus(world.Village, Delivery); return;
        }
        CurrentChallenge = world.Village.GardenCount % 2 == 0 ? challenges.Next() : letterChallenges.Next();
        feedback = $"Přivez {Village.FlowerNames[world.Village.GardenCount]}. Nejdřív vyřeš úkol a pak vyrazíme!";
        State = GameState.Challenge; ShowChallenge();
    }
}
