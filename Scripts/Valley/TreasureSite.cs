using Godot;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The place where a treasure is buried, made of nodes only: a heap of fresh earth with a red cross that shows when the car
/// is close, and the chest that comes out of it. It is built once and moved to the next treasure. A site that has no
/// treasure is hidden, and a site that is restored from a save is not shown with an open chest.
/// </summary>
internal sealed partial class TreasureSite : Node3D
{
    /// <summary>How close the car must be for the cross on the ground to show.</summary>
    public const float CrossRange = 22;

    private Node3D heap = null!;
    private Node3D cross = null!;
    private Node3D chest = null!;
    private Node3D lid = null!;
    private Node3D sparkles = null!;
    private float time;
    private float openness;
    private bool opening;
    private float shake;

    public bool IsOpen => opening;

    public bool CrossShown => cross.Visible;

    public bool ChestShown => chest.Visible;

    public float LidAngle => lid.RotationDegrees.X;

    public override void _Ready()
    {
        Name = "TreasureSite";
        Visible = false;
        heap = new Node3D { Name = "Heap" };
        AddChild(heap);
        Art.Ball(heap, new(0, .1f, 0), new(1.2f, .42f, 1.2f), "7d5c3e");
        Art.Ball(heap, new(.55f, .08f, .3f), new(.55f, .25f, .55f), "8b6a47");
        Art.Ball(heap, new(-.5f, .07f, -.3f), new(.5f, .22f, .5f), "6f5035");

        cross = new Node3D { Name = "Cross" };
        AddChild(cross);
        foreach (float angle in new[] { 40f, -40f })
        {
            var plank = Art.Block(cross, new(0, .62f, 0), new(1.9f, .12f, .26f), "d9432f");
            plank.RotationDegrees = new(0, angle, 0);
        }
        Art.Cylinder(cross, new(0, .3f, 0), .05f, .05f, .6f, "a77a59");

        chest = new Node3D { Name = "Chest", Visible = false };
        AddChild(chest);
        Art.Block(chest, new(0, .38f, 0), new(1.3f, .76f, .85f), "8a5a35");
        Art.Block(chest, new(0, .2f, 0), new(1.36f, .1f, .9f), "d9a02a");
        Art.Block(chest, new(0, .66f, 0), new(1.36f, .1f, .9f), "d9a02a");
        foreach (float x in new[] { -.5f, .5f })
            Art.Block(chest, new(x, .38f, 0), new(.1f, .8f, .92f), "d9a02a");
        // The gold inside shows once the lid is up.
        for (int i = 0; i < 7; i++)
            Art.Ball(
                chest,
                new(-.45f + i % 4 * .3f, .8f + i / 4 * .06f, i % 2 * .22f - .1f),
                new(.2f, .14f, .2f),
                "ffd84a"
            );
        lid = new Node3D { Name = "Lid", Position = new(0, .76f, -.42f) };
        chest.AddChild(lid);
        Art.Block(lid, new(0, .18f, .42f), new(1.3f, .36f, .85f), "9a6a3f");
        Art.Ball(lid, new(0, .3f, .42f), new(.66f, .24f, .42f), "9a6a3f");
        foreach (float x in new[] { -.5f, .5f })
            Art.Block(lid, new(x, .2f, .42f), new(.1f, .4f, .92f), "d9a02a");
        Art.Ball(lid, new(0, .08f, .86f), new(.14f, .2f, .06f), "ffd84a");

        sparkles = new Node3D { Name = "Sparkles", Visible = false };
        AddChild(sparkles);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6;
            Art.Ball(sparkles, new(Mathf.Sin(angle) * .9f, 1.2f, Mathf.Cos(angle) * .9f), Vector3.One * .13f, "fff3a0");
        }
    }

    /// <summary>Buries a treasure here: the earth is heaped up, the chest is out of sight and the lid is shut.</summary>
    public void Bury(Vector3 position, float yaw)
    {
        GlobalPosition = position;
        Rotation = new(0, yaw, 0);
        Visible = true;
        heap.Visible = true;
        cross.Visible = false;
        chest.Visible = false;
        sparkles.Visible = false;
        opening = false;
        openness = 0;
        shake = 0;
        lid.RotationDegrees = Vector3.Zero;
    }

    /// <summary>The treasure is gone from the valley (a new hunt has not begun).</summary>
    public void Clear() => Visible = false;

    /// <summary>The car starts digging: the earth shakes for a moment.</summary>
    public void Dig() => shake = 1;

    /// <summary>The chest comes out of the earth and its lid swings open.</summary>
    public void OpenChest()
    {
        Visible = true;
        chest.Visible = true;
        heap.Visible = true;
        cross.Visible = false;
        sparkles.Visible = true;
        opening = true;
    }

    /// <summary>Animates the cross, the shaking earth, the lid and the sparkles; <paramref name="car"/> is where the car is.</summary>
    public void Tick(float delta, Vector3 car, bool running)
    {
        if (!Visible || !running)
            return;
        time += delta;
        if (!opening)
        {
            var flat = new Vector2(car.X - GlobalPosition.X, car.Z - GlobalPosition.Z);
            cross.Visible = flat.Length() < CrossRange;
            cross.Scale = Vector3.One * (1 + .06f * Mathf.Sin(time * 5));
        }
        if (shake > 0)
        {
            shake = Mathf.Max(0, shake - delta * 1.6f);
            heap.Position = new(Mathf.Sin(time * 60) * .06f * shake, 0, Mathf.Cos(time * 53) * .06f * shake);
        }
        else
            heap.Position = Vector3.Zero;
        if (opening)
        {
            openness = Mathf.Min(1, openness + delta * 1.6f);
            lid.RotationDegrees = new(-112 * Mathf.SmoothStep(0, 1, openness), 0, 0);
            sparkles.Rotation = new(0, time * 1.5f, 0);
            sparkles.Position = new(0, Mathf.Sin(time * 3) * .12f, 0);
        }
    }
}
