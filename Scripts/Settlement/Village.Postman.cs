using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Settlement;

/// <summary>
/// Where the postman stands in a valley that has no room for the post office (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>,
/// phase 2): on the village square in front of the fountain, left of the entrance, beside a yellow letter box. Like the
/// captain he stands in the square's own frame, inside the ground the square already reserves, and draws no random
/// numbers, so nothing in the valley moves for him.
/// </summary>
public partial class Village
{
    /// <summary>
    /// Where the postman stands on the square (x, z): in front of the fountain, left of the entrance, more than the range
    /// of an interaction from the mayor, the lamp maker and the captain.
    /// </summary>
    private static readonly Vector2 PostmanSpot = new(-2.6f, 5.2f);

    internal void PlacePostmanOnSquare(Character postman)
    {
        var spot = Square.Transform * new Vector3(PostmanSpot.X, 0, PostmanSpot.Y);
        AddChild(postman);
        postman.Position = ApproachSpot(ground.At(spot.X, spot.Z));
        postman.Rotation = Square.Transform.Basis.GetEuler();

        // The yellow letter box on a post, with the post horn on its front.
        var box = SquareProp("PostBox", PostmanSpot.X - 1.7f, PostmanSpot.Y - .2f);
        Art.Cylinder(box, new(0, .55f, 0), .07f, .07f, 1.1f, "4d4d4d");
        Art.Block(box, new(0, 1.32f, 0), new(.62f, .58f, .42f), "f2c230");
        Art.Block(box, new(0, 1.66f, 0), new(.68f, .1f, .48f), "d9a521");
        Art.Block(box, new(0, 1.46f, .215f), new(.36f, .05f, .02f), "2d2d2d");
        Art.Ball(box, new(0, 1.24f, .22f), new(.13f, .09f, .02f), "1f4e8c");
        Art.Ball(box, new(.07f, 1.24f, .225f), new(.05f, .05f, .02f), "f2c230");
    }
}
