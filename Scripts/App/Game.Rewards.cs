using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Garage;

namespace MathRacer.App;

/// <summary>What finishing a building brings: coins for all three new buildings, and a living mill.</summary>
public partial class Game
{
    internal const int BuildCoins = 30;

    /// <summary>The extra coins the baker pays once the mill grinds flour for him.</summary>
    internal const int BakerBonus = 2;

    private bool MillFinished => world.Projects.Length > BuildPlans.Mill && world.Projects[BuildPlans.Mill].Complete;

    /// <summary>Coins a finished shop order brings: the baker pays more once the mill is built.</summary>
    internal int TradeReward(Animal shop) =>
        TradeCoins + (shop.Role is TraderRole { Index: 0 } && MillFinished ? BakerBonus : 0);

    /// <summary>The garage item that a finished building brings, or null: flour sack, binoculars or the water paint.</summary>
    internal static CarReward? BuildItem(int project) =>
        project switch
        {
            BuildPlans.Mill => new CarReward(CarAccessory.FlourSack.Id, true),
            BuildPlans.Tower => new CarReward(CarAccessory.Binoculars.Id, true),
            BuildPlans.Jetty => new CarReward(CarLook.Water.Id, false),
            _ => null,
        };

    internal static string BuildItemName(int project) =>
        project switch
        {
            BuildPlans.Mill => CarAccessory.FlourSack.Name,
            BuildPlans.Tower => CarAccessory.Binoculars.Name,
            BuildPlans.Jetty => CarLook.Water.Name,
            _ => "",
        };

    /// <summary>
    /// Pays the reward for a finished mill, tower or jetty: coins and a garage item (the bridge has its trailer).
    /// The jetty also brings a duck, which needs nothing here: it belongs to anyone whose jetty is finished.
    /// </summary>
    private void PayBuildReward(BuildProject project)
    {
        if (project.Index == BuildPlans.Bridge)
            return;
        AddCoins(BuildCoins);
        if (project is Jetty && world.Duck is { } duck)
            PlaceCollectorPet(duck);
        if (BuildItem(project.Index) is { } item)
            economy.Owned.Add(item);
    }

    /// <summary>The sails of a finished mill turn faster in the wind.</summary>
    private void TickMill()
    {
        if (world.Projects.Length > BuildPlans.Mill && world.Projects[BuildPlans.Mill] is Mill { Complete: true } mill)
            mill.SailSpeed = .08f + .22f * Weather.WindStrength;
    }
}
