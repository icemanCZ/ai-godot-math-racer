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
