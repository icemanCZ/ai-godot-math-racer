using System.Collections.Generic;

namespace MathRacer;

public sealed record CarBody(string Id, string Name)
{
    public static IReadOnlyList<CarBody> All { get; } = System.Array.AsReadOnly(new[]
    {
        new CarBody("classic", "Retro autíčko"),
        new CarBody("buggy", "Plážová bugina"),
        new CarBody("pickup", "Veselý pickup"),
        new CarBody("wagon", "Výletní kombík")
    });
}
