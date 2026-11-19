using System.Collections.Generic;

namespace MathRacer.Garage;

/// <summary>
/// The player's coins, the garage items they have bought, and the coins shown on the reward panel that is open. It
/// holds no scene nodes: <see cref="Game"/> shows the coins and speaks.
/// </summary>
internal sealed class Economy
{
    public CarRewards Garage { get; } = new();

    public int Coins => Garage.Coins;

    public HashSet<CarReward> Owned => Garage.Owned;

    /// <summary>The coins paid for the task on the open reward panel. They are already counted in <see cref="Coins"/>.</summary>
    public int RewardCoins { get; set; }

    public void Add(int amount) => Garage.AddCoins(amount);

    /// <summary>Pays the coins of a finished task and remembers them for the reward panel.</summary>
    public void Pay(int coins)
    {
        RewardCoins = coins;
        Add(coins);
    }

    /// <summary>Buys a priced item the player can afford; false when it is owned, special or too dear.</summary>
    public bool Buy(CarReward reward) => Garage.Buy(reward);

    /// <summary>Pays coins for something that is not a garage item, such as a decoration of the town.</summary>
    public bool Spend(int amount) => Garage.Spend(amount);

    public void Restore(IEnumerable<CarReward> owned, int coins, int rewardCoins)
    {
        Garage.Restore(owned, coins);
        RewardCoins = rewardCoins;
    }
}
