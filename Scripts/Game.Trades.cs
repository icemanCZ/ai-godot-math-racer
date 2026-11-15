using System.Linq;

namespace MathRacer;

public partial class Game
{
    private void OpenTrade(Animal owner)
    {
        nearby = owner; CurrentChallenge = null; rejectedAnswers.Clear(); vehicle.Active = false;
        if (Delivery != null)
        {
            State = GameState.TradeStatus; ui.TradeStatus(owner, Delivery); return;
        }
        BeginQuiz();
    }

    private void StartTrade()
    {
        if (Delivery != null || nearby?.IsTrader != true) return;
        StartExpedition(nearby, false);
        SaveProgress();
    }

    private void CompleteTrade()
    {
        if (Delivery == null || !Delivery.Sender.IsTrader || deliveryReward) return;
        deliveryReward = true; CurrentChallenge = null; rejectedAnswers.Clear();
        CelebrateCompletion(Delivery.Recipient, "Objednávka doručena! Děkujeme!", true); Delivery.Sender.Celebrate();
        // Offer every category, preferring rewards that are still missing from the garage.
        CarReward Choose(System.Collections.Generic.IEnumerable<CarReward> options)
        {
            var all = options.ToArray(); var missing = all.Where(r => !OwnedRewards.Contains(r)).ToArray();
            var pool = missing.Length > 0 ? missing : all;
            return pool[rewardRandom.Next(pool.Length)];
        }
        CurrentRewards = new[] {
            Choose(CarAccessory.All.Select(a => new CarReward(a.Id, true))),
            Choose(CarLook.All.Select(l => new CarReward(l.Id, false))),
            Choose(CarBody.All.Select(b => new CarReward(b.Id, false, true)))
        };
        previousRewards = CurrentRewards;
        State = GameState.Reward; ShowRewards(); ShowCelebrationView(); SaveProgress();
    }
}
