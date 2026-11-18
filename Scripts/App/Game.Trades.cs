using MathRacer.Characters;

namespace MathRacer.App;

public partial class Game
{
    internal void OpenTrade(Animal owner)
    {
        nearby = owner;
        ClearChallenge();
        vehicle.Active = false;
        // Only this trader's own order shows its status; any other task is replaced after the quiz.
        if (Delivery?.Sender == owner)
        {
            State = GameState.TradeStatus;
            ui.TradeStatus(owner, Delivery);
            return;
        }
        BeginQuiz();
    }

    internal void StartTrade()
    {
        if (nearby?.Role is not TraderRole || Delivery?.Sender == nearby)
            return;
        StartExpedition(nearby, false);
        SaveProgress();
    }

    private void CompleteTrade()
    {
        if (Delivery == null || Delivery.Sender.Role is not TraderRole || deliveryReward)
            return;
        deliveryReward = true;
        ClearChallenge();
        CelebrateCompletion(Delivery.Recipient, "Objednávka doručena! Děkujeme!", true);
        Delivery.Sender.Celebrate();
        PayReward(TradeReward(Delivery.Sender));
    }
}
