namespace MathRacer;

public partial class Game
{
    private void ShowChallenge()
    {
        if (InBanditQuiz && banditArrival > 0) { ui.HideModal(); return; }
        ui.Challenge(CurrentChallenge!, rejectedAnswers, syllableSelection, InBanditQuiz ? banditSolved + 1 : 0);
    }

    internal void BeginVillageCollection()
    {
        if (State != GameState.HouseFruitOffer || nearby == null || nearby.HouseIndex < 0 ||
            !nearby.RewardAvailable || Delivery != null || world.Village.TaskAt(nearby.HouseIndex) != VillageTaskKind.Fruit) return;
        deliveryReward = false;
        StartExpedition(nearby, true);
        SaveProgress();
    }

    private void CompleteHouseTask(Animal owner)
    {
        world.Village.Upgrade(owner.HouseIndex); nearby = owner;
        CelebrateCompletion(owner, $"Nové vylepšení: {Village.Upgrades[world.Village.Level(owner.HouseIndex) - 1]}", unload: Delivery?.Sender == owner, building: world.Village.Houses[owner.HouseIndex]);
        CurrentChallenge = null; rejectedAnswers.Clear(); deliveryReward = false;
        State = GameState.HouseReward; vehicle.Active = false;
        ui.HouseReward(owner, world.Village.Level(owner.HouseIndex));
        ShowCelebrationView();
    }
}
