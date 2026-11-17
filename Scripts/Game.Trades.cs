namespace MathRacer;

public partial class Game
{
    internal void OpenTrade(Animal owner)
    {
        nearby = owner;
        ClearChallenge();
        vehicle.Active = false;
        if (Delivery != null)
        {
            State = GameState.TradeStatus;
            ui.TradeStatus(owner, Delivery);
            return;
        }
        BeginQuiz();
    }

    internal void StartTrade()
    {
        if (Delivery != null || nearby?.IsTrader != true)
            return;
        StartExpedition(nearby, false);
        SaveProgress();
    }

    private void CompleteTrade()
    {
        if (Delivery == null || !Delivery.Sender.IsTrader || deliveryReward)
            return;
        deliveryReward = true;
        ClearChallenge();
        CelebrateCompletion(Delivery.Recipient, "Objednávka doručena! Děkujeme!", true);
        Delivery.Sender.Celebrate();
        rewards.OfferOnePerCategory();
        State = GameState.Reward;
        ShowRewards();
        ShowCelebrationView();
        SaveProgress();
    }
}
