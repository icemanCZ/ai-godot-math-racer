namespace MathRacer;

public partial class Game
{
    private void OpenFarm()
    {
        nearby = world.Village.Farmer;
        CurrentChallenge = null; rejectedAnswers.Clear(); vehicle.Active = false;
        if (Delivery?.Farm == true && Delivery.CanDeliver) { BeginDeliveryQuestion(); return; }
        if (Delivery != null || world.Village.FarmCount >= Village.FarmKinds.Length)
        {
            State = GameState.FarmStatus; ui.FarmStatus(world.Village, Delivery); return;
        }
        // Every new trip requires a fresh quiz; an abandoned quiz never unlocks a departure.
        CurrentChallenge = world.Village.FarmCount % 2 == 0 ? challenges.Next() : letterChallenges.Next();
        feedback = $"Najdi {Village.FarmNames[world.Village.FarmCount]}. Nejdřív vyřeš úkol a pak vyrazíme!";
        State = GameState.Challenge; ShowChallenge();
    }
}
