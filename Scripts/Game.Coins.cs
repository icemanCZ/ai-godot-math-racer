namespace MathRacer;

public partial class Game
{
    internal const int RoadsideDeliveryCoins = 10;
    internal const int TradeCoins = 8;
    internal const int HouseTaskCoins = 5;
    internal const int CollectorDeliveryCoins = 5;
    internal const int GolemCoins = 10;
    internal const int PostcardCoins = 5;
    internal const int BanditCoins = 5;
    internal const int CourseCoins = 5;

    /// <summary>Paid on top of <see cref="CourseCoins"/> for beating the ghost on a driving course.</summary>
    internal const int GhostCoins = 5;
    internal const int WorldCoinValue = 1;

    internal int Coins => rewards.Coins;

    /// <summary>The coins paid for the task on the open reward panel.</summary>
    private int rewardCoins;
    internal readonly WorldCoins WorldCoins = new();

    private void SetupCoins()
    {
        world.AddChild(WorldCoins);
        WorldCoins.Generate(world, vehicle.Position);
    }

    /// <summary>Pays coins for a finished task or a coin found in the valley and shows them in the HUD.</summary>
    internal void AddCoins(int amount)
    {
        rewards.AddCoins(amount);
        ui.CoinCount(Coins, amount);
    }

    private void TickCoins(double delta)
    {
        WorldCoins.Animate((float)delta, State is not (GameState.Paused or GameState.Title), vehicle.Position);
        if (State != GameState.Exploring)
            return;
        int found = WorldCoins.Collect(vehicle.Position);
        if (found > 0)
            AddCoins(found * WorldCoinValue);
    }

    /// <summary>Buys a priced car item in the garage and equips it right away.</summary>
    private bool BuyCarItem(CarReward reward)
    {
        if (!rewards.Buy(reward))
            return false;
        ui.CoinCount(Coins, 0);
        Speak("Koupeno!");
        return true;
    }
}
