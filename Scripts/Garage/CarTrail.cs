using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer.Garage;

/// <summary>
/// What the rear wheels leave on the ground: the plain tyre marks that every car starts with, or a row of flowers,
/// hearts or stars that is bought in the garage. The ids are saved, so they never change.
/// </summary>
public sealed record CarTrail(string Id, string Name)
{
    /// <summary>The tyre marks the car always has; the only trail that is not a stamp.</summary>
    public static CarTrail Plain { get; } = new("tyres", "Obyčejné stopy");

    public static IReadOnlyList<CarTrail> All { get; } =
        System.Array.AsReadOnly(
            new[]
            {
                new CarTrail("flowers", "Kytičkové stopy"),
                new CarTrail("hearts", "Srdíčkové stopy"),
                new CarTrail("stars", "Hvězdičkové stopy"),
            }
        );

    public static IReadOnlyList<CarTrail> Catalogue { get; } =
        System.Array.AsReadOnly(new[] { Plain }.Concat(All).ToArray());

    public static bool IsKnown(string? id) => id != null && Catalogue.Any(t => t.Id == id);
}

/// <summary>
/// The outlines of the stamps in a unit circle, in the order they are painted (a later piece lies on an earlier one):
/// the garage draws them flat, the valley lays them on the ground as a mesh.
/// </summary>
public static class TrailShapes
{
    public readonly record struct Piece(Vector2[] Outline, Color Color);

    public static IReadOnlyList<Piece> Of(string id) =>
        id switch
        {
            "flowers" => Flower,
            "hearts" => Heart,
            _ => Star,
        };

    private static readonly IReadOnlyList<Piece> Flower = MakeFlower();
    private static readonly IReadOnlyList<Piece> Heart = new[] { new Piece(HeartOutline(), new Color("ff5f8f")) };
    private static readonly IReadOnlyList<Piece> Star = new[] { new Piece(StarOutline(), new Color("ffd23f")) };

    private static IReadOnlyList<Piece> MakeFlower()
    {
        var pieces = new List<Piece>();
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5;
            var outline = Ellipse(new Vector2(0, .56f).Rotated(angle), new Vector2(.34f, .44f), angle);
            pieces.Add(new Piece(outline, new Color(i % 2 == 0 ? "ffb3d1" : "fff1f7")));
        }
        pieces.Add(new Piece(Ellipse(Vector2.Zero, new Vector2(.3f, .3f), 0), new Color("ffcf3a")));
        return pieces;
    }

    private static Vector2[] Ellipse(Vector2 center, Vector2 radius, float rotation) =>
        Enumerable
            .Range(0, 20)
            .Select(i =>
                center
                + new Vector2(
                    Mathf.Cos(i * Mathf.Tau / 20) * radius.X,
                    Mathf.Sin(i * Mathf.Tau / 20) * radius.Y
                ).Rotated(rotation)
            )
            .ToArray();

    /// <summary>The classic heart curve, scaled to fit the unit circle with its point towards +Y of the outline.</summary>
    private static Vector2[] HeartOutline() =>
        Enumerable
            .Range(0, 40)
            .Select(i =>
            {
                float t = i * Mathf.Tau / 40;
                float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
                float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                return new Vector2(x, -(y - 2.5f)) / 16.5f;
            })
            .ToArray();

    private static Vector2[] StarOutline() =>
        Enumerable
            .Range(0, 10)
            .Select(i => new Vector2(0, i % 2 == 0 ? -1f : -.45f).Rotated(i * Mathf.Pi / 5))
            .ToArray();
}
