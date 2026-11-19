using System.Collections.Generic;

namespace MathRacer.Garage;

/// <summary>The garage's car items and the player's coins that buy them.</summary>
internal sealed class CarRewards
{
    /// <summary>Owned items; the starting paint and the classic car are always owned.</summary>
    public HashSet<CarReward> Owned { get; } = new() { new(CarLook.All[0].Id, false), new("classic", false, true) };

    public int Coins { get; private set; }

    public void AddCoins(int amount) => Coins += System.Math.Max(0, amount);

    /// <summary>Buys a priced item the player can afford. Returns false when it is owned, special or too expensive.</summary>
    public bool Buy(CarReward reward)
    {
        if (Owned.Contains(reward) || reward.Price is not int price || Coins < price)
            return false;
        Coins -= price;
        Owned.Add(reward);
        return true;
    }

    /// <summary>Pays for something that is not a garage item. Returns false, and keeps the coins, when there are too few.</summary>
    public bool Spend(int amount)
    {
        if (amount < 0 || Coins < amount)
            return false;
        Coins -= amount;
        return true;
    }

    public void Restore(IEnumerable<CarReward> owned, int coins)
    {
        Owned.Clear();
        Owned.UnionWith(owned);
        Coins = System.Math.Max(0, coins);
    }
}
