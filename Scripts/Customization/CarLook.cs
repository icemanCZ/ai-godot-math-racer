using System.Collections.Generic;

namespace MathRacer;

public sealed record CarLook(string Id, string Name, string BodyColor, string RoofColor, string TrimColor, string? StripeColor = null)
{
    public static IReadOnlyList<CarLook> All { get; } = System.Array.AsReadOnly(new[]
    {
        new CarLook("strawberry", "Jahůdka", "f54257", "ff8992", "fff0ce"),
        new CarLook("blueberry", "Borůvka", "4976ed", "a9c9ff", "f0f5ff"),
        new CarLook("sunshine", "Sluníčko", "ffce38", "fff0a1", "fff8e4"),
        new CarLook("mint", "Mátový bonbón", "46c6a0", "baf3d8", "fff8e4"),
        new CarLook("lavender", "Levandule", "9871db", "dfc9ff", "fff1fb"),
        new CarLook("tangerine", "Mandarinka", "ff873e", "ffd0a0", "fff5df"),
        new CarLook("ocean", "Mořské pruhy", "238ebd", "9ce5ed", "e9fbff", "ffffff"),
        new CarLook("racer", "Závodní pruhy", "ed4762", "ffadb5", "fff2dc", "fff0b0")
    });
}
