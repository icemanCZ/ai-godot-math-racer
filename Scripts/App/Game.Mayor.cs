using MathRacer.Characters;
using MathRacer.Settlement;

namespace MathRacer.App;

/// <summary>
/// The mayor on the village square sells decorations for the town for coins. She sends no trip: talking to her opens her
/// shop, and every decoration that is bought stands on the square at once and stays there (saved as
/// <c>SaveData.Decorations</c>).
/// </summary>
public partial class Game
{
    internal void OpenMayor(Character mayor)
    {
        if (mayor.Role is not MayorRole)
            return;
        ClearChallenge();
        EnterInteraction(GameState.MayorShop, mayor);
        ShowMayor();
        Speak(
            world.Village.OwnedDecorationIds.Length == TownDecorations.All.Count ? Texts.MestoJeKrasneDekujeme
                : world.Village.OwnedDecorationIds.Length == 0 ? mayor.Greeting
                : Texts.CoSiPriVybrat,
            mayor
        );
    }

    private void ShowMayor()
    {
        if (nearby?.Role is not MayorRole)
            return;
        ui.MayorShop(nearby, world.Village.OwnedDecorationIds, Coins);
    }

    /// <summary>Buys a decoration from the open shop if it is not owned yet and the coins are enough.</summary>
    internal void BuyDecoration(string id)
    {
        if (
            State != GameState.MayorShop
            || nearby?.Role is not MayorRole
            || TownDecorations.Find(id) is not { } decoration
            || world.Village.OwnsDecoration(id)
            || !economy.Spend(decoration.Price)
        )
            return;
        world.Village.AddDecoration(id);
        ui.CoinCount(Coins, 0);
        nearby.Celebrate();
        ShowMayor();
        Speak(
            world.Village.OwnedDecorationIds.Length == TownDecorations.All.Count
                ? Texts.MestoJeKrasneDekujeme
                : Texts.DekujiHnedToOzdobime,
            nearby
        );
        SaveProgress();
    }
}
