using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Settlement;

/// <summary>
/// The pirate captain on the village square, behind the fountain, with a rug, a shovel in a heap of earth and a small
/// treasure chest. Like the mayor and the lamp maker she is placed in the square's own frame inside the ground that the
/// square already reserves and draws no random numbers, so nothing else in the valley moves.
/// </summary>
public partial class Village
{
    /// <summary>
    /// Where the captain stands on the square (x, z): behind the fountain, more than the range of an interaction from the
    /// mayor and the lamp maker, so that each of them is the nearest character in their own corner.
    /// </summary>
    private static readonly Vector2 TreasurerSpot = new(0, -4f);

    internal Character Treasurer { get; private set; } = null!;

    private void GenerateTreasurer()
    {
        var spot = Square.Transform * new Vector3(TreasurerSpot.X, 0, TreasurerSpot.Y);
        var rug = SquareProp("TreasurerRug", TreasurerSpot.X, TreasurerSpot.Y);
        Art.Cylinder(rug, new(0, .03f, 0), 1.15f, 1.15f, .06f, "2b4f8a");
        Art.Cylinder(rug, new(0, .065f, 0), .95f, .95f, .05f, "f0cf74");
        Art.Cylinder(rug, new(0, .09f, 0), .85f, .85f, .05f, "2b4f8a");
        Treasurer = Character.Create(CharacterCatalog.Treasurer);
        Treasurer.Role = new TreasureRole();
        Treasurer.DressAsPirate();
        AddChild(Treasurer);
        Treasurer.Position = ground.At(spot.X, spot.Z);
        Treasurer.Rotation = Square.Transform.Basis.GetEuler();

        // A shovel stuck in a heap of earth, and a chest to show what the hunt is for.
        var heap = SquareProp("TreasurerShovel", TreasurerSpot.X + 1.8f, TreasurerSpot.Y - .4f);
        Art.Ball(heap, new(0, .12f, 0), new(.7f, .3f, .7f), "8b6a47");
        var handle = Art.Cylinder(heap, new(.1f, .75f, 0), .035f, .035f, 1.3f, "a77a59");
        handle.RotationDegrees = new(0, 0, -12);
        Art.Block(heap, new(.25f, 1.45f, 0), new(.28f, .05f, .05f), "a77a59");
        Art.Block(heap, new(0, .45f, 0), new(.26f, .34f, .04f), "9fb3c0");
    }
}
