using MathRacer.Buildings;
using MathRacer.Settlement;

namespace MathRacer.Characters;

/// <summary>
/// The IDs under which characters are stored in saved games. This is the only place that holds the numbers; every
/// other name for an ID (<c>World.FarmerId</c>, <c>Game.BanditActorId</c>…) is an alias of one of these.
/// A saved game stores only the ID, so an ID is never renumbered, reused for another character, or moved to another
/// kind of character. A new kind of character takes the next free number at the end. <c>contract-checks</c> holds
/// the numbers and the role of every ID, and <c>World.Register</c> refuses an ID that is already taken.
/// </summary>
internal static class ActorIds
{
    /// <summary>The roadside friends use their array index. Saves from before there were ten reserved 0–29.</summary>
    public const int FirstRoadside = 0;

    /// <summary>The four village residents, by house.</summary>
    public const int FirstResident = 30;

    public const int Farmer = 34;
    public const int Gardener = 35;
    public const int GemCollector = 36;
    public const int Toymaker = 37;

    /// <summary>The three shop owners, by shop.</summary>
    public const int FirstTrader = 38;

    public const int Witch = 41;
    public const int Sorcerer = 42;

    /// <summary>The bandit representative. Game restores it together with its encounter; World does not register it.</summary>
    public const int Bandit = 43;

    /// <summary>The beaver, who builds the bridge.</summary>
    public const int Beaver = 44;

    /// <summary>The workers of the sawmill, the brickworks and the gravel works.</summary>
    public const int FirstWorks = 45;

    public const int LastWorks = FirstWorks + WorksCatalog.Count - 1;

    /// <summary>The builders of the mill, the tower and the jetty (the bridge's builder is the beaver).</summary>
    public const int FirstBuilder = 48;

    public const int LastBuilder = FirstBuilder + BuildPlans.Count - 2;

    /// <summary>The workers at the quarry, the clay pit and the log yard.</summary>
    public const int FirstSource = 51;

    /// <summary>The workers at the second sites: quarry, clay pit, sand pit and log yard.</summary>
    public const int FirstExtraSource = FirstSource + SourceCatalog.Count;

    public const int LastSource = FirstExtraSource + 4 - 1;

    /// <summary>The newcomers of the building plots, by plot.</summary>
    public const int FirstPlotResident = LastSource + 1;

    public const int LastPlotResident = FirstPlotResident + PlotPlans.Count - 1;

    /// <summary>The worker of the glassworks, which a valley gets after everything else (no actor has an ID between).</summary>
    public const int Glassworks = LastPlotResident + 1;

    /// <summary>The worker of the bakery, the fifth works, which a valley with a wheat field gets after the glassworks.</summary>
    public const int Bakery = Glassworks + 1;

    /// <summary>The farmer at the wheat field outside the village, who loads the trailer for the bakery.</summary>
    public const int FieldFarmer = Bakery + 1;

    /// <summary>The keeper of the playground at the edge of the village, which a valley gets after everything else.</summary>
    public const int Playground = FieldFarmer + 1;

    /// <summary>The mayor on the village square, who sells decorations for the town. Every valley has her.</summary>
    public const int Mayor = Playground + 1;

    /// <summary>The glass lamp maker on the village square, who hands out candles and lamps. Every valley has him.</summary>
    public const int Glassmaker = Mayor + 1;

    /// <summary>The stationmaster of the little railway round the playground, which a valley with a playground gets last.</summary>
    public const int Station = Glassmaker + 1;

    /// <summary>The keeper of the zoo at the edge of the village, which a valley with room for it gets after the stationmaster.</summary>
    public const int ZooKeeper = Station + 1;
}
