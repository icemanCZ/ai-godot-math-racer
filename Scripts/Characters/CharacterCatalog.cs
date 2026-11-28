using System.Collections.Generic;
using MathRacer.Valley;

namespace MathRacer.Characters;

/// <summary>How a character looks and greets the player. Kind: 0 rabbit, 1 fox, 2 bear.</summary>
internal readonly record struct CharacterSpec(string Name, string Greeting, int Kind, string? Fur = null);

/// <summary>
/// Names, greetings and fur colours of the recurring characters. The list order is part of the save
/// format: roadside friends and house residents receive their actor IDs in this order.
/// </summary>
internal static class CharacterCatalog
{
    // Static fields initialize in file order, so the name and colour tables stay above the lists built from them.
    private const string ResidentGreeting = "Pomůžeš mi zkrášlit domeček?";
    private const string PlotGreeting = "Chtěl bych tady bydlet! Přivezeš mi materiál na domeček?";
    private const string TraderGreeting =
        "Vyřeš příklad a rozvez mou objednávku.\nZa doručení dostaneš penízky na vylepšení auta!";

    private static readonly string[] SpeciesNames = { "Králíček", "Liška", "Medvěd" };

    private static readonly string[][] GivenNames =
    {
        new[] { "Toník", "Pepík", "Kubík", "Vašík", "Honzík", "Vítek", "Jeník", "Štěpánek", "Filípek" },
        new[] { "Anička", "Bětka", "Klárka", "Terezka", "Barunka", "Maruška", "Zuzanka", "Kačenka", "Rozárka" },
        new[] { "Míša", "Bručoun", "Medoušek", "Bohouš", "Břéťa", "Dobromil", "Vojta", "Olda", "Radouš" },
    };

    private static readonly string[][] FurColors =
    {
        new[] { "f3e8d5", "c5b4e3", "a9d8e8" },
        new[] { "e9a16a", "e8bf73", "df92ac" },
        new[] { "b98d71", "c5a9db", "97c9b0" },
    };

    public static IReadOnlyList<CharacterSpec> RoadsideFriends { get; } = CreateRoadsideFriends();

    public static IReadOnlyList<CharacterSpec> HouseResidents { get; } =
        new CharacterSpec[]
        {
            new("Králíček stavitel Vojtík", ResidentGreeting, 0, "a9d8e8"),
            new("Liška zahradnice Róza", ResidentGreeting, 1, "df92ac"),
            new("Medvídek kutil Hugo", ResidentGreeting, 2, "97c9b0"),
            new("Králíček malíř Fialka", ResidentGreeting, 0, "c5b4e3"),
        };

    /// <summary>
    /// The newcomers who wait on the village's building plots and move into the house that is built for them.
    /// The list order gives their actor IDs (<see cref="World.FirstPlotResidentId"/> onwards).
    /// </summary>
    public static IReadOnlyList<CharacterSpec> PlotResidents { get; } =
        new CharacterSpec[]
        {
            new("Králíček Ondra", PlotGreeting, 0, "f3e8d5"),
            new("Liška Alenka", PlotGreeting, 1, "e8bf73"),
            new("Medvěd Bořek", PlotGreeting, 2, "b98d71"),
            new("Králíček Standa", PlotGreeting, 0, "d9c7f0"),
            new("Liška Dana", PlotGreeting, 1, "f0b58a"),
            new("Medvídek Jarda", PlotGreeting, 2, "a8c9a0"),
        };

    /// <summary>The keeper of the playground at the edge of the village (<see cref="World.PlaygroundId"/>).</summary>
    public static CharacterSpec PlaygroundKeeper { get; } =
        new("Medvědice Madla", "Děti chtějí hřiště! Přivezeš nám štěrk, prkna a cihly?", 2, "c98f6a");

    /// <summary>The mayor on the village square, who sells decorations for the town (<see cref="World.MayorId"/>).</summary>
    public static CharacterSpec Mayor { get; } =
        new(
            "Liška starostka Božena",
            "Dobrý den! Za pár penízků ti ráda prodám nové ozdoby do našeho města.",
            1,
            "d9825b"
        );

    /// <summary>The glass lamp maker on the village square, who hands out candles and lamps (<see cref="World.GlassmakerId"/>).</summary>
    public static CharacterSpec Glassmaker { get; } =
        new(
            "Medvěd světlař Ignác",
            "Dobrý den! Foukám sklo do svíček a lamp. Vyber si, postav je, kam chceš, a večer se samy rozsvítí.",
            2,
            "a8856b"
        );

    /// <summary>The stationmaster of the little railway round the playground (<see cref="World.StationId"/>).</summary>
    public static CharacterSpec Stationmaster { get; } =
        new(
            "Králíček výpravčí Špendlík",
            "Pííp! Děti by se chtěly svézt vláčkem! Postavíme kolem hřiště nádraží? Přivez mi štěrk, prkna a cihly.",
            0,
            "e8d9c4"
        );

    /// <summary>The keeper of the zoo at the edge of the village (<see cref="World.ZooKeeperId"/>).</summary>
    public static CharacterSpec ZooKeeper { get; } =
        new(
            "Medvěd ošetřovatel Bruno",
            "Brumm! Ve městě ještě nemáme zoo a děti by chtěly vidět zvířátka! Postavíme jim výběhy? Přivez mi štěrk, prkna a cihly.",
            2,
            "b98b5f"
        );

    /// <summary>The hunter at his pen at the edge of the village (<see cref="World.HunterId"/>).</summary>
    public static CharacterSpec Hunter { get; } =
        new(
            "Jezevec lovec Hubert",
            "Dobrý den, lovče! Mám velkou ohradu, ale je skoro prázdná. Zvířátka běhají daleko v přírodě. Dám ti síť: zastav kousek od zvířátka a mezerníkem ji hoď. Chycené zvířátko nalož a přivez mi ho!",
            2,
            "8f8a84"
        );

    /// <summary>
    /// The keepers of the four borders, in the order of <c>Border</c> (<see cref="ActorIds.FirstBorderKeeper"/>): the
    /// lighthouse keeper by the sea, the shepherd under the mountains, the ferrywoman at the border river and the forester.
    /// </summary>
    public static CharacterSpec[] BorderKeepers { get; } =
        {
            new(
                "Medvěd majákář Kryštof",
                "Ahoj, námořníku! Tady končí silnice a začíná moře. Odsud vidím každou loď, co pluje kolem.",
                2,
                "8b6d55"
            ),
            new(
                "Zajíc pastýř Bohouš",
                "Dobrý den pod horami! Pasu tu ovečky. Nahoru do hor se autem nedá, ale výhled je odsud krásný.",
                0,
                "efe4d2"
            ),
            new(
                "Liška převoznice Vendula",
                "Ahoj! Za řekou už je cizí kraj. Převážím přes ni lidi na pramici, auto by se do ní nevešlo.",
                1,
                "d9733f"
            ),
            new(
                "Medvědice hajná Božena",
                "Pšt, ať nevyplašíme srnky! Hlídám tenhle velký les a v zimě nosím zvířátkům seno do krmelce.",
                2,
                "7a5a3c"
            ),
        };

    /// <summary>The postman on the village square, who sends letters out to the keepers of the borders (<see cref="ActorIds.Postman"/>).</summary>
    public static CharacterSpec Postman { get; } =
        new(
            "Zajíc pošťák Pepa",
            "Dobrý den! Mám plnou brašnu dopisů pro strážce na kraji údolí: u moře, pod horami, u hraniční řeky a v lese. Odvezeš jim je?",
            0,
            "c9b49a"
        );

    /// <summary>The dragon on his ledge under the mountains, who has caught a cold and whose egg is getting cold (<see cref="ActorIds.Dragon"/>).</summary>
    public static CharacterSpec Dragon { get; } =
        new(
            "Drak Kvído",
            "Ahoj! Já jsem drak Kvído. Hap… hap… hapčí! Nastydl jsem a místo ohně kýchám jen kroužky kouře. A moje vejce v hnízdě stydne. Pomůžeš mi udělat teplé hnízdo?",
            2,
            "5fae5a"
        );

    /// <summary>The balloonist of the balloon station far beyond the village (<see cref="ActorIds.Balloonist"/>).</summary>
    public static CharacterSpec Balloonist { get; } =
        new(
            "Liška balonářka Amálka",
            "Ahoj! Já jsem Amálka a tohle je můj horkovzdušný balón. Vidíš ho z celého údolí, viď? Poletíme spolu nad údolím a pochytáme hvězdičky!",
            1,
            "e6a35a"
        );

    /// <summary>
    /// The animal at the end of a trail of the forest keeper (<see cref="ActorIds.TrackingAnimal"/>); its name is the
    /// animal's own once a trail is followed.
    /// </summary>
    public static CharacterSpec TrackingAnimal { get; } =
        new("Schované zvířátko", "Pšt, někdo se tu schovává!", 2, "a5815a");

    /// <summary>
    /// The owl at her stand where the road leaves the old valley (<see cref="ActorIds.Cartographer"/>), who keeps the map
    /// of the valley (<c>docs/EXPLORER_PLAN.md</c>).
    /// </summary>
    public static CharacterSpec Cartographer { get; } =
        new(
            "Sova kartografka Mapka",
            "Hú hú! Já jsem Mapka a kreslím mapu celého údolí. Za starým údolím je osm divů krajiny. Najdeš je?",
            2,
            "9a7652"
        );

    /// <summary>
    /// The heron at the landing of the ferry beside the homestead of the river border (<see cref="ActorIds.Birdwatcher"/>),
    /// who wants to go to the bird island (<c>docs/FERRY_PLAN.md</c>).
    /// </summary>
    public static CharacterSpec Birdwatcher { get; } =
        new(
            "Volavka pozorovatelka Klárka",
            "Ahoj! Já jsem Klárka a pozoruju ptáky. Na ostrůvku v deltě jich je spousta, jen se tam nedá dojet. Postavíme pramici?",
            2,
            "9aa6b2"
        );

    /// <summary>
    /// The lioness princess at the gate of the castle on the hill (<see cref="ActorIds.Castellan"/>), who wants her castle
    /// built and a feast held in it (<c>docs/CASTLE_PLAN.md</c>).
    /// </summary>
    public static CharacterSpec Castellan { get; } =
        new(
            "Lvice kněžna Libuše",
            "Vítej! Já jsem kněžna Libuše. Tady na kopci bude stát hrad, nejkrásnější v celém údolí. Pomůžeš mi ho postavit?",
            2,
            "e0a95a"
        );

    /// <summary>
    /// The badger engine driver at the hub of the great line (<see cref="ActorIds.Engineer"/>), who wants a railway across
    /// the whole valley (<c>docs/MAINLINE_PLAN.md</c>).
    /// </summary>
    public static CharacterSpec Engineer { get; } =
        new(
            "Jezevec strojvůdce Kuba",
            "Píííp! Já jsem strojvůdce Kuba. Chci postavit velkou trať přes celé údolí, k horám, k moři i k řece. Pomůžeš mi s ní?",
            2,
            "8f969b"
        );

    /// <summary>
    /// The walrus captain at the shipyard on the coast (<see cref="ActorIds.Captain"/>), who wants a big ship and sails it
    /// to the turtle island (<c>docs/SHIP_PLAN.md</c>).
    /// </summary>
    public static CharacterSpec Captain { get; } =
        new(
            "Mrož kapitán Vojta",
            "Ahoj, námořníku! Já jsem kapitán Vojta. Tady na pláži postavíme velkou loď Albatros a poplujeme na Želví ostrov. Pomůžeš mi?",
            2,
            "8c6a55"
        );

    /// <summary>The pirate captain on the village square, who hides treasures for the player to dig up (<see cref="World.TreasurerId"/>).</summary>
    public static CharacterSpec Treasurer { get; } =
        new(
            "Liška kapitánka Zlatuška",
            "Ahoj, pirátko! Zakopala jsem poklad a zapomněla kam! Vezmi si můj zlatý detektor a jeď hledat. Čím blíž, tím rychleji pípá!",
            1,
            "e0884a"
        );

    /// <summary>Baker, shopkeeper and confectioner, matching <see cref="Village.TradeCargo"/>.</summary>
    public static IReadOnlyList<CharacterSpec> Traders { get; } =
        new CharacterSpec[]
        {
            new("Pekař František", TraderGreeting, 0, "f4c57c"),
            new("Obchodník Kamil", TraderGreeting, 1, "83cbb7"),
            new("Cukrář Oliver", TraderGreeting, 2, "eab0ca"),
        };

    private static CharacterSpec[] CreateRoadsideFriends()
    {
        var friends = new List<CharacterSpec>
        {
            new("Králíček Matěj", "Ahoj! Vítej v našem údolí.\nProjeď se a najdi moje kamarády!", 0),
            new("Liška Eliška", "Jé, ty máš krásné autíčko!\nU řeky je dnes moc hezky.", 1),
            new("Medvěd Brumla", "Ahoj, kamaráde! Tady to voní květinami.\nMám pro tebe malý početní úkol!", 2),
        };
        // The remaining friends cycle through the species with their own names and colours.
        for (int i = 0; friends.Count < World.QuestAnimalCount; i++)
        {
            int kind = i % 3;
            friends.Add(
                new(
                    $"{SpeciesNames[kind]} {GivenNames[kind][i / 3]}",
                    "Ahoj! Vyřeš příklad a vyděláš si penízky.",
                    kind,
                    FurColors[kind][(i / 3 + 1) % 3]
                )
            );
        }
        return friends.ToArray();
    }
}
