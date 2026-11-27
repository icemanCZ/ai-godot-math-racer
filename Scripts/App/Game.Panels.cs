using System;
using System.Collections.Generic;
using MathRacer.Characters;

namespace MathRacer.App;

/// <summary>
/// The panel of each interaction state (<see cref="GameState"/>). After a pause, a load or a celebration the game
/// shows the panel of its state again from the context it has; a panel whose context is missing (the character it
/// is about, the question it asks) is not shown. A new interaction state registers its panel here, and
/// <c>contract-checks</c> fails when an interaction state has none.
/// </summary>
public partial class Game
{
    /// <summary>Shows the panel from the current context and returns whether it could; the flag marks a status shown right after a delivery.</summary>
    private readonly Dictionary<GameState, Func<bool, bool>> panels = new();

    private void RegisterPanels()
    {
        Panel(GameState.Challenge, _ => nearby != null && CurrentChallenge != null && Shown(ShowChallenge));
        Panel(GameState.Reward, _ => nearby != null && Shown(ShowRewards));
        Panel(GameState.ExpeditionChoice, _ =>
            nearby is { } actor && Shown(() => ui.ExpeditionChoice(actor, vehicle.Appearance))
        );
        Panel(GameState.Garage, _ => Shown(ShowGarage));
        Panel(GameState.HouseReward, _ =>
            nearby is { Role: HouseResidentRole house } actor
            && Shown(() => ui.HouseReward(actor, world.Village.Level(house.House)))
        );
        Panel(GameState.HouseCollection, _ =>
            Delivery is { } delivery && Shown(() => ui.HouseCollection(delivery.Sender, delivery.Collected))
        );
        Panel(GameState.HouseFruitOffer, _ =>
            nearby is { Role: HouseResidentRole house } actor
            && Shown(() => ui.HouseFruitOffer(actor, world.Village.Level(house.House), true))
        );
        Panel(GameState.HouseMaterialOffer, _ => nearby?.Role is HouseResidentRole && ShowHouseMaterial());
        Panel(GameState.FarmStatus, returned => Shown(() => ui.FarmStatus(world.Village, Delivery, returned)));
        Panel(GameState.GardenStatus, returned => Shown(() => ui.GardenStatus(world.Village, Delivery, returned)));
        Panel(GameState.CollectionStatus, returned =>
            nearby is { } actor && Shown(() => ui.CollectionStatus(world.Village, actor, Delivery, returned))
        );
        Panel(GameState.TradeStatus, _ => nearby?.Role is TraderRole && Shown(ShowTrade));
        Panel(GameState.WeatherChoice, _ => Shown(ShowWeatherChoice));
        Panel(GameState.SorcererStatus, _ => Shown(ShowSorcerer));
        Panel(GameState.BuildStatus, _ => nearby?.Role is BuilderRole && Shown(ShowBuilder));
        Panel(GameState.PlotStatus, _ => nearby?.Role is PlotResidentRole && Shown(ShowPlot));
        Panel(GameState.PlaygroundStatus, _ => nearby?.Role is PlaygroundRole && Shown(ShowPlayground));
        Panel(GameState.RailwayStatus, _ => nearby?.Role is StationRole && Shown(ShowRailway));
        Panel(GameState.ZooStatus, _ => nearby?.Role is ZooRole && Shown(ShowZoo));
        Panel(GameState.TreasureStatus, _ => nearby?.Role is TreasureRole && Shown(ShowTreasure));
        Panel(GameState.FireflyStatus, _ => nearby?.Role is WitchRole && Shown(ShowFireflies));
        Panel(GameState.HunterStatus, _ => nearby?.Role is HunterRole && Shown(ShowHunter));
        Panel(GameState.KeeperStatus, _ => nearby?.Role is BorderKeeperRole && Shown(ShowKeeper));
        Panel(GameState.PostStatus, _ => nearby?.Role is PostmanRole && Shown(ShowPost));
        Panel(GameState.LighthouseStatus, _ =>
            nearby?.Role is BorderKeeperRole && Lighthouse != null && Shown(ShowLighthouse)
        );
        Panel(GameState.DragonStatus, _ => nearby?.Role is DragonRole && DragonLair != null && Shown(ShowDragon));
        Panel(GameState.BalloonStatus, _ =>
            nearby?.Role is BalloonistRole && BalloonStation != null && Shown(ShowBalloon)
        );
        Panel(GameState.TrackingStatus, _ =>
            nearby?.Role is BorderKeeperRole { Border: Valley.Border.Forest } && TrackingWanted && Shown(ShowTracking)
        );
        Panel(GameState.ShepherdStatus, _ =>
            nearby?.Role is BorderKeeperRole { Border: Valley.Border.Mountains } && SheepWanted && Shown(ShowShepherd)
        );
        Panel(GameState.RallyStatus, _ =>
            nearby?.Role is BorderKeeperRole { Border: Valley.Border.River } && RallyWanted && Shown(ShowRally)
        );
        Panel(GameState.ExplorerStatus, _ =>
            nearby?.Role is CartographerRole && ExplorerWanted && Shown(ShowCartographer)
        );
        Panel(GameState.FerryStatus, _ => nearby?.Role is BirdwatcherRole && FerryWanted && Shown(ShowBirdwatcher));
        Panel(GameState.MainLineStatus, _ => nearby?.Role is EngineerRole && LineWanted && Shown(ShowEngineer));
        Panel(GameState.CastleStatus, _ => nearby?.Role is CastleRole && CastleWanted && Shown(ShowCastle));
        Panel(GameState.MayorShop, _ => nearby?.Role is MayorRole && Shown(ShowMayor));
        Panel(GameState.GlassmakerShop, _ => nearby?.Role is GlassmakerRole && Shown(ShowGlassmaker));
        Panel(GameState.SourceStatus, _ => nearby?.Role is SourceRole && Shown(ShowSource));
        Panel(GameState.WorksStatus, _ => nearby?.Role is WorksRole && Shown(ShowWorks));
        Panel(GameState.PostcardAlbum, _ => pendingPhoto < 0 && Shown(ShowPostcards));
    }

    private void Panel(GameState state, Func<bool, bool> show) => panels.Add(state, show);

    private static bool Shown(Action show)
    {
        show();
        return true;
    }

    /// <summary>Whether an interaction state has a panel registered.</summary>
    internal bool HasPanel(GameState state) => panels.ContainsKey(state);

    /// <summary>
    /// Shows the panel of an interaction state from the current context, e.g. after pause, loading or a celebration.
    /// <paramref name="returned"/> marks a collection status shown right after a delivery.
    /// Returns false when the state has no panel or its context is missing.
    /// </summary>
    private bool ShowPanel(GameState state, bool returned = false) =>
        panels.TryGetValue(state, out var show) && show(returned);
}
