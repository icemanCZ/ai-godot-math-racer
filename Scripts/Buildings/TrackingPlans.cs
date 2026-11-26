using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The animals of the forest keeper's book of tracks (<c>docs/TRACKING_PLAN.md</c>), their names and where they hide, and
/// what finding them pays: coins for every animal coaxed out, a bonus for the last one, and the squirrel Oříška once
/// the book is full.
/// </summary>
internal static class TrackingPlans
{
    /// <summary>How many animals the book has, one for every trail.</summary>
    public const int Animals = 6;

    /// <summary>The coins for every animal coaxed out of its den.</summary>
    public const int CoinsPerAnimal = 6;

    /// <summary>The bonus when the book is full.</summary>
    public const int BookCoins = 20;

    /// <summary>The animal's name, as it is called in the book and over its den.</summary>
    public static string Name(TrackingAnimal animal) =>
        animal switch
        {
            TrackingAnimal.Hare => "Zajíc Ušák",
            TrackingAnimal.Fox => "Liška Ryška",
            TrackingAnimal.Deer => "Srnka Lenka",
            TrackingAnimal.Badger => "Jezevec Pruhoun",
            TrackingAnimal.Boar => "Prasátko Chrochtík",
            _ => "Rys Ostrovid",
        };

    /// <summary>Whose tracks they are, as the keeper says it: "stopa …".</summary>
    public static string Whose(TrackingAnimal animal) =>
        animal switch
        {
            TrackingAnimal.Hare => "zajíce",
            TrackingAnimal.Fox => "lišky",
            TrackingAnimal.Deer => "srnky",
            TrackingAnimal.Badger => "jezevce",
            TrackingAnimal.Boar => "divokého prasátka",
            _ => "rysa",
        };

    /// <summary>Where the animal hides, which the keeper tells the player.</summary>
    public static string Hideout(TrackingAnimal animal) =>
        animal switch
        {
            TrackingAnimal.Hare => "pod hustým keřem",
            TrackingAnimal.Fox => "v noře pod mezí",
            TrackingAnimal.Deer => "v kapradí u břízky",
            TrackingAnimal.Badger => "v hluboké noře s hromádkou písku",
            TrackingAnimal.Boar => "v blátivém kališti",
            _ => "na skalce u padlého kmene",
        };

    /// <summary>A thing about the animal's tracks, which the book shows beside them.</summary>
    public static string About(TrackingAnimal animal) =>
        animal switch
        {
            TrackingAnimal.Hare => "Zajíc skáče: dlouhé zadní tlapky dopadnou až před ty přední.",
            TrackingAnimal.Fox => "Liška šlape jako po provázku, stopa za stopou v jedné řadě.",
            TrackingAnimal.Deer => "Srnka má kopýtka ze dvou půlek, stopa vypadá jako srdíčko.",
            TrackingAnimal.Badger => "Jezevec má pět prstů a dlouhé drápy na hrabání.",
            TrackingAnimal.Boar => "Prasátko má kopýtka a za nimi dva malé paspárky.",
            _ => "Rys má kulaté tlapky a drápy schová, proto ve stopě nejsou.",
        };
}
