namespace MathRacer;

public partial class Game
{
    private void ShowChallenge()
    {
        if (InBanditQuiz && banditArrival > 0)
        {
            ui.HideModal();
            return;
        }
        ui.Challenge(CurrentChallenge!, rejectedAnswers, syllableSelection, InBanditQuiz ? banditSolved + 1 : 0);
    }

    internal void OpenHouse(Animal resident)
    {
        int house = resident.HouseIndex;
        if (Delivery?.Sender == resident)
        {
            if (Delivery.CanDeliver)
            {
                BeginDeliveryQuestion();
                return;
            }
            ClearChallenge();
            EnterInteraction(GameState.HouseCollection, resident);
            ui.HouseCollection(resident, Delivery.Collected);
            return;
        }
        if (!resident.RewardAvailable)
        {
            EnterInteraction(GameState.HouseReward, resident);
            ui.HouseReward(resident, world.Village.Level(house));
            return;
        }
        if (world.Village.TaskAt(house) == VillageTaskKind.Fruit)
        {
            ClearChallenge();
            EnterInteraction(GameState.HouseFruitOffer, resident);
            ui.HouseFruitOffer(resident, world.Village.Level(house), Delivery == null);
            return;
        }
        BeginChallenge(resident);
    }

    internal void CompleteHouseQuiz(Animal resident)
    {
        CompleteHouseTask(resident);
        SaveProgress();
    }

    internal void BeginVillageCollection()
    {
        if (
            State != GameState.HouseFruitOffer
            || nearby == null
            || nearby.HouseIndex < 0
            || !nearby.RewardAvailable
            || Delivery != null
            || world.Village.TaskAt(nearby.HouseIndex) != VillageTaskKind.Fruit
        )
            return;
        deliveryReward = false;
        StartExpedition(nearby, true);
        SaveProgress();
    }

    private void CompleteHouseTask(Animal owner)
    {
        int house = owner.HouseIndex;
        world.Village.Upgrade(house);
        AddCoins(HouseTaskCoins);
        nearby = owner;
        string upgrade = Village.Upgrades[world.Village.Level(house) - 1];
        CelebrateCompletion(
            owner,
            $"Nové vylepšení: {upgrade}",
            unload: Delivery?.Sender == owner,
            building: world.Village.Houses[house]
        );
        ClearChallenge();
        deliveryReward = false;
        EnterInteraction(GameState.HouseReward);
        ui.HouseReward(owner, world.Village.Level(house));
        ShowCelebrationView();
    }
}
