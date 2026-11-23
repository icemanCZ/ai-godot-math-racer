using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The jetty's little sailing boat. Once the jetty is finished it drifts up and down the river past the jetty,
/// keeping to the middle of the water and turning round at each end of its stretch.
/// </summary>
public partial class Boat : Node3D
{
    private const float Speed = 1.7f;
    private readonly float centerZ;
    private readonly float reach;
    private float progress;

    /// <summary>How far from the jetty, along the river, the boat gets.</summary>
    internal float Reach => reach;

    internal Boat(float centerZ, float reach)
    {
        Name = "Boat";
        TopLevel = true;
        this.centerZ = centerZ;
        this.reach = reach;
        Build();
        Place(0);
    }

    private void Build()
    {
        Art.Block(this, new(0, .22f, 0), new(1.15f, .3f, 3.1f), "a2492f");
        foreach (float side in new[] { -1f, 1f })
            Art.Block(this, new(side * .62f, .48f, 0), new(.1f, .5f, 3.1f), "c39770");
        Art.Ball(this, new(0, .3f, -1.75f), new(.62f, .34f, .95f), "a2492f");
        Art.Ball(this, new(0, .5f, -1.7f), new(.5f, .3f, .8f), "c39770");
        Art.Block(this, new(0, .48f, 1.55f), new(1.15f, .5f, .1f), "c39770");
        Art.Cylinder(this, new(0, 1.7f, -.3f), .06f, .06f, 2.8f, "7a4b32");
        Art.Block(this, new(0, 1.55f, .15f), new(.04f, 1.9f, 1.2f), "fff3d6");
        Art.Block(this, new(0, 1.15f, .15f), new(.045f, .35f, 1.2f), "e2563b");
        Art.Block(this, new(0, 3.05f, -.1f), new(.04f, .3f, .5f), "e2563b");
        Art.Block(this, new(0, .62f, .55f), new(.9f, .08f, .3f), "a77a59");
    }

    /// <summary>Puts the boat on the water at <paramref name="along"/> metres from the jetty, facing the way it drifts.</summary>
    private void Place(float along)
    {
        float z = Mathf.Clamp(
            centerZ + along,
            -WorldTuning.CorePlayableRadius + 20,
            WorldTuning.CorePlayableRadius - 20
        );
        float next = Mathf.Clamp(z + 1, -WorldTuning.CorePlayableRadius + 20, WorldTuning.CorePlayableRadius - 20);
        float sway = Mathf.Sin(progress * .9f) * .6f;
        Position = new Vector3(River.CenterX(z) + sway, River.Level(z) + .02f + Mathf.Sin(progress * 2.3f) * .05f, z);
        var direction = new Vector3(River.CenterX(next) - River.CenterX(z), 0, next - z);
        float heading = Mathf.Atan2(direction.X, direction.Z);
        // The bow (-Z) leads, so a boat drifting towards +Z faces that way after a half turn.
        Rotation = new Vector3(
            Mathf.Sin(progress * 1.7f) * .02f,
            heading + (Moving < 0 ? 0 : Mathf.Pi),
            Mathf.Sin(progress * 1.3f) * .03f
        );
    }

    private int Moving { get; set; } = 1;

    public override void _Process(double delta)
    {
        if (!Visible)
            return;
        progress += (float)delta;
        // A triangle wave over the stretch: out to one end, back through the jetty, out to the other, and back.
        float cycle = 4 * reach / Speed;
        float t = progress % cycle / cycle;
        float along =
            t < .25f ? t * 4
            : t < .75f ? 2 - t * 4
            : t * 4 - 4;
        Moving = (t > .25f && t < .75f) ? -1 : 1;
        Place(along * reach);
    }
}
