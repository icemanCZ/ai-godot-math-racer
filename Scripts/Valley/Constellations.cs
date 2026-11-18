using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// A picture drawn with stars. <paramref name="Stars"/> are offsets in degrees of sky from the middle of the picture
/// and <paramref name="Lines"/> joins them two by two.
/// </summary>
internal sealed record Constellation(string Name, Vector2[] Stars, int[] Lines)
{
    /// <summary>The stargazer shows them one after another, a different picture each night.</summary>
    public static IReadOnlyList<Constellation> All { get; } =
        new[]
        {
            new Constellation(
                "Králíček",
                new Vector2[] { new(-5, 13), new(-3, 5), new(5, 13), new(3, 5), new(-6, 0), new(6, 0), new(0, -6) },
                new[] { 0, 1, 1, 3, 2, 3, 1, 4, 3, 5, 4, 6, 5, 6 }
            ),
            new Constellation(
                "Autíčko",
                new Vector2[] { new(-10, -1), new(10, -1), new(-4, 5), new(4, 5), new(-6, -6), new(6, -6) },
                new[] { 0, 2, 2, 3, 3, 1, 1, 0, 0, 4, 1, 5 }
            ),
            new Constellation(
                "Srdíčko",
                new Vector2[]
                {
                    new(0, -8),
                    new(-6, -2),
                    new(-9, 3),
                    new(-5, 7),
                    new(0, 3),
                    new(5, 7),
                    new(9, 3),
                    new(6, -2),
                },
                new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 0 }
            ),
            new Constellation(
                "Hvězda",
                Enumerable.Range(0, 5).Select(i => Vector2.Down.Rotated(i * Mathf.Tau / 5) * 10).ToArray(),
                new[] { 0, 2, 2, 4, 4, 1, 1, 3, 3, 0 }
            ),
        };
}
