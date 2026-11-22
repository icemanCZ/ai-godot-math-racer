using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.App;

public partial class Game
{
    /// <summary>
    /// Whether the baker of the village shop sells the bread of the bakery: once the finished mill grinds flour for it, the
    /// wheat of the field becomes flour at the mill, bread at the bakery and an order for his customers. Before that, and
    /// in a valley without the bakery or the mill, he bakes his own as he always did.
    /// </summary>
    private bool BakesFromBakery(Character owner) =>
        owner.Role is TraderRole { Index: 0 } && world.Bakery != null && world.FlourMill is { Grinding: true };

    /// <summary>Whether the bakery has the loaves for the baker's next order.</summary>
    private bool OrderBaked(Character owner) => world.Bakery!.Stock >= Expedition.OrderAmount(world, owner);

    internal void OpenTrade(Character owner)
    {
        nearby = owner;
        ClearChallenge();
        // Only this trader's own order shows its status; any other task is replaced after the quiz.
        if (Delivery?.Sender == owner)
        {
            vehicle.Active = false;
            State = GameState.TradeStatus;
            ui.TradeStatus(owner, Delivery);
            Speak(Texts.ObjednavkaJeNaCesteDorucJi, owner);
            return;
        }
        vehicle.Active = false;
        // The bakery has too little bread for the baker's order: his panel sends the player along the chain instead.
        if (BakesFromBakery(owner) && !OrderBaked(owner))
        {
            State = GameState.TradeStatus;
            ShowTrade();
            Speak(
                BreadSupply() is not { } supply || Haul != null ? Texts.FrantisekNoBreadWait
                    : supply is FlourMill ? Texts.FrantisekNoBreadWheat
                    : Texts.FrantisekNoBreadFlour,
                owner
            );
            return;
        }
        BeginQuiz();
    }

    /// <summary>
    /// The shop's panel: the order under way, or for the baker whose bakery has too little bread the loaves he needs and
    /// the trip that brings them closer (flour from the mill, or wheat for the mill), or the trip already under way.
    /// </summary>
    private void ShowTrade()
    {
        if (nearby is not { } owner)
            return;
        if (Delivery?.Sender != owner && BakesFromBakery(owner) && !OrderBaked(owner))
        {
            var supply = Haul == null ? BreadSupply() : null;
            ui.BakerNeedsBread(
                owner,
                Expedition.OrderAmount(world, owner),
                BreadSupplyUnderWay ? Haul : null,
                supply?.LoadFor(0)
            );
            return;
        }
        ui.TradeStatus(owner, Delivery);
    }

    internal void StartTrade()
    {
        if (nearby?.Role is not TraderRole || Delivery?.Sender == nearby)
            return;
        // The baker's order is the bakery's bread: the loaves are taken from its trays when the order sets off.
        if (BakesFromBakery(nearby))
        {
            var bakery = world.Bakery!;
            CountStockMade(bakery);
            if (!bakery.Take(Expedition.OrderAmount(world, nearby)))
            {
                var baker = nearby;
                CloseInteraction();
                Speak(Texts.FrantisekNoBreadWait, baker);
                return;
            }
        }
        StartExpedition(nearby, false);
        SaveProgress();
    }

    private void CompleteTrade()
    {
        if (Delivery == null || Delivery.Sender.Role is not TraderRole || deliveryReward)
            return;
        deliveryReward = true;
        ClearChallenge();
        CelebrateCompletion(Delivery.Recipient, Texts.ObjednavkaDorucenaDekujeme, true);
        Delivery.Sender.Celebrate();
        PayReward(TradeReward(Delivery.Sender));
    }
}
