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
        CurrentChallenge = challenges.Next();
        feedback = "Vyřeš příklad a naložíme objednávku.\nZa rozvoz si vybereš doplněk, barvu nebo typ auta.";
        State = GameState.Challenge; ShowChallenge();
    }

    private void StartTrade()
    {
        if (Delivery != null || nearby?.IsTrader != true) return;
        Delivery = new Expedition(world, vehicle, nearby, false); AddChild(Delivery);
        CurrentChallenge = null; rejectedAnswers.Clear(); lastCollected = Delivery.Collected;
        ui.DeliveryHud(Delivery); Start(); UpdateDeliveryGuides(); SaveProgress();
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

    // Stable IDs preserve the existing save format and add the three new residents at the end.
    private int ActorId(Animal? actor) => actor == null ? -1 : actor.IsTrader ? 38 + actor.TradeIndex :
        actor.IsToymaker ? 37 : actor.IsGemCollector ? 36 : actor.IsGardener ? 35 : actor.IsFarmer ? 34 :
        actor.HouseIndex >= 0 ? 30 + actor.HouseIndex : System.Array.IndexOf(world.Animals, actor);

    private Animal? ActorAt(int id) => id < 0 ? null : id >= 38 ? world.Village.Traders[id - 38] :
        id == 37 ? world.Village.Toymaker : id == 36 ? world.Village.GemCollector :
        id == 35 ? world.Village.Gardener : id == 34 ? world.Village.Farmer :
        id >= 30 ? world.Village.Residents[id - 30] : world.Animals[id];
}
