using System;
using Godot;
using MathRacer.Buildings;
using MathRacer.Graphics;
using MathRacer.Tasks;

namespace MathRacer.Characters;

/// <summary>One pet that the world gives: its kind, its name and what the player does to earn it.</summary>
internal sealed record WorldPetSpec(CollectorPetKind Kind, string Name, string Reward);

/// <summary>
/// The pets that are earned by hauling and building rather than by a collection: one for each raw material (ten loads
/// hauled), each works (twenty pieces made in all, however many of them were taken out of the stock since) and each
/// building (its completion; the jetty's is the duck).
/// </summary>
internal static class WorldPetCatalog
{
    /// <summary>The loads of a raw material that earn its pet.</summary>
    public const int SourceLoads = 10;

    /// <summary>The pieces a works has to make in all for its pet. The stock holds only <see cref="Works.Capacity"/>.</summary>
    public const int WorksPieces = 20;

    /// <summary>The loads a works takes in for its pet, which at <see cref="Works.PerLoad"/> pieces a load is twenty pieces.</summary>
    public const int WorksLoads = WorksPieces / Works.PerLoad;

    public static readonly WorldPetSpec Logs = new(CollectorPetKind.Logs, "Kládička", "10 odvezených klád");
    public static readonly WorldPetSpec Stones = new(CollectorPetKind.Stones, "Kamínek", "10 odvezených kamenů");
    public static readonly WorldPetSpec Soil = new(CollectorPetKind.Soil, "Hliněnka", "10 odvezených nákladů hlíny");
    public static readonly WorldPetSpec Sand = new(CollectorPetKind.Sand, "Pískulka", "10 odvezených nákladů písku");

    public static readonly WorldPetSpec Sawmill = new(CollectorPetKind.Sawmill, "Prkýnko", "20 vyrobených prken");
    public static readonly WorldPetSpec Brickworks = new(
        CollectorPetKind.Brickworks,
        "Cihlička",
        "20 vyrobených cihel"
    );
    public static readonly WorldPetSpec GravelWorks = new(CollectorPetKind.GravelWorks, "Štěrkáček", "20 kusů štěrku");
    public static readonly WorldPetSpec Glassworks = new(
        CollectorPetKind.Glassworks,
        "Sklenička",
        "20 vyrobených skleniček"
    );

    public static readonly WorldPetSpec Wheat = new(CollectorPetKind.Wheat, "Zrníčko", "10 odvezených nákladů obilí");
    public static readonly WorldPetSpec Bakery = new(CollectorPetKind.Bakery, "Housánek", "20 upečených chlebů");

    public static readonly WorldPetSpec Stationmaster = new(
        CollectorPetKind.Stationmaster,
        "Výpravčí Píšťalka",
        "dostavěné nádraží"
    );

    public static readonly WorldPetSpec PandaCub = new(CollectorPetKind.PandaCub, "Pandička Bambulka", "dostavěná zoo");

    public static readonly WorldPetSpec Fawn = new(CollectorPetKind.Fawn, "Koloušek Puntík", "8 chycených zvířátek");

    public static readonly WorldPetSpec Pigeon = new(
        CollectorPetKind.Pigeon,
        "Holoubek Psaníčko",
        "známka od každého strážce hranic"
    );

    public static readonly WorldPetSpec Gull = new(CollectorPetKind.Gull, "Racek Vlnka", "postavený maják");

    public static readonly WorldPetSpec Dragonling = new(
        CollectorPetKind.Dragonling,
        "Dráček Jiskřička",
        "vylíhnuté dračí vejce"
    );

    public static readonly WorldPetSpec Rooster = new(
        CollectorPetKind.Rooster,
        "Kohoutek Kokrháček",
        $"{BalloonPlans.RoosterStars} hvězdiček z balónu"
    );

    public static readonly WorldPetSpec Squirrel = new(
        CollectorPetKind.Squirrel,
        "Veverka Oříška",
        $"{TrackingPlans.Animals} vystopovaných zvířátek"
    );

    public static readonly WorldPetSpec Sheepdog = new(
        CollectorPetKind.Sheepdog,
        "Ovčácký pejsek Alík",
        $"{SheepPlans.Sheep} oveček doma v ohradě"
    );

    public static readonly WorldPetSpec Otter = new(
        CollectorPetKind.Otter,
        "Vydra Šplouchalka",
        $"zlatá medaile ve všech {RallyPlans.Stages} etapách rallye"
    );

    public static readonly WorldPetSpec Owlet = new(
        CollectorPetKind.Owlet,
        "Sovička Houkalka",
        $"všech {ExplorerPlans.Landmarks} divů krajiny v mapě"
    );

    public static readonly WorldPetSpec Crab = new(
        CollectorPetKind.Crab,
        "Krab Klepetko",
        $"mušle všech {ShellPlans.Kinds} druhů z pláže"
    );

    public static readonly WorldPetSpec Kingfisher = new(
        CollectorPetKind.Kingfisher,
        "Ledňáček Modroušek",
        $"všech {FerryPlans.Kinds} druhů ptáků v atlasu z ostrůvku"
    );

    public static readonly WorldPetSpec CastleGhost = new(
        CollectorPetKind.CastleGhost,
        "Strašidýlko Bubáček",
        "všichni hosté u hradní hostiny"
    );

    public static readonly WorldPetSpec Mole = new(
        CollectorPetKind.Mole,
        "Krteček Pražec",
        $"všech {Buildings.MainLinePlans.Trips} nákladů velké trati přes údolí"
    );

    public static readonly WorldPetSpec Bridge = new(CollectorPetKind.Bridge, "Mostíček", "dostavěný most");
    public static readonly WorldPetSpec Mill = new(CollectorPetKind.Mill, "Mlýnek", "dostavěný mlýn");
    public static readonly WorldPetSpec Tower = new(CollectorPetKind.Tower, "Věžička", "dostavěnou rozhlednu");

    // The small friends who hide beside the roads: finding all eight of a kind earns that animal.
    public static readonly WorldPetSpec Ladybug = new(
        CollectorPetKind.Ladybug,
        "Beruška Tečka",
        "8 nalezených berušek"
    );
    public static readonly WorldPetSpec Hedgehog = new(
        CollectorPetKind.Hedgehog,
        "Ježek Bodlínek",
        "8 nalezených ježků"
    );
    public static readonly WorldPetSpec Frog = new(CollectorPetKind.Frog, "Žabka Kvákalka", "8 nalezených žabek");

    /// <summary>The pet of a kind of hidden friend, in the order ladybug, hedgehog, frog.</summary>
    public static WorldPetSpec ForHidden(int kind) =>
        kind switch
        {
            0 => Ladybug,
            1 => Hedgehog,
            _ => Frog,
        };

    // The ducklings of the jetty's duck: one for every third delivery to the jetty, the duck itself for the tenth.
    public static readonly WorldPetSpec Duckling1 = new(
        CollectorPetKind.Duckling1,
        "Kachňátko Žlutka",
        "3 dodávky na molo"
    );
    public static readonly WorldPetSpec Duckling2 = new(
        CollectorPetKind.Duckling2,
        "Kachňátko Pírko",
        "6 dodávek na molo"
    );
    public static readonly WorldPetSpec Duckling3 = new(
        CollectorPetKind.Duckling3,
        "Kachňátko Bublinka",
        "9 dodávek na molo"
    );

    /// <summary>The deliveries to the jetty that earn each duckling, in the order of the ducklings.</summary>
    public static readonly int[] DucklingDeliveries = { 3, 6, 9 };

    public static WorldPetSpec ForDuckling(int index) =>
        index switch
        {
            0 => Duckling1,
            1 => Duckling2,
            _ => Duckling3,
        };

    public static WorldPetSpec ForSource(HaulLoad load) =>
        load switch
        {
            HaulLoad.Stones => Stones,
            HaulLoad.Soil => Soil,
            HaulLoad.Sand => Sand,
            HaulLoad.Wheat => Wheat,
            _ => Logs,
        };

    /// <summary>The pet of a works, in the order of <see cref="WorksCatalog.All"/>.</summary>
    public static WorldPetSpec ForWorks(int index) =>
        index switch
        {
            (int)Product.Planks => Sawmill,
            (int)Product.Bricks => Brickworks,
            (int)Product.Gravel => GravelWorks,
            (int)Product.Bread => Bakery,
            _ => Glassworks,
        };

    public static WorldPetSpec ForBuilding(int project) =>
        project switch
        {
            BuildPlans.Bridge => Bridge,
            BuildPlans.Mill => Mill,
            _ => Tower,
        };

    /// <summary>Where a pet of the world trails behind the car: rows of five behind the older pets.</summary>
    public static Vector3 Trail(int index) => new(((index % 5) - 2) * 1.8f, 0, 13.5f + index / 5 * 3.1f);
}
