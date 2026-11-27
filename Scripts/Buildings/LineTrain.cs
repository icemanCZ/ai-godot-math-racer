using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The train of the great line (<c>docs/MAINLINE_PLAN.md</c>): a steam engine with a tall chimney and a cab, a tender
/// with coal and a flatbed wagon with low sides that carries the car. Its local +Z is the way it runs. It stands on a
/// track at a distance along it, or runs along it, smoking, with its wheels turning, and now and then whistles.
/// </summary>
public sealed partial class LineTrain : Node3D
{
    /// <summary>The train's length: the engine in front, then the tender and the wagon.</summary>
    internal const float Length = 17;

    private readonly List<Node3D> wheels = new();
    private readonly Node3D body = new() { Name = "Body" };
    private CpuParticles3D smoke = null!;
    private Label3D whistle = null!;
    private float whistleLeft;
    private float travelled;
    private bool running;

    /// <summary>The train is running (smoking, its wheels turning).</summary>
    internal bool Running => running;

    /// <summary>Where the car stands on the wagon and the way it faces.</summary>
    internal (Vector3 Seat, float Heading) CarSeat
    {
        get
        {
            var seat = GlobalTransform * new Vector3(0, 1.05f, -5.2f);
            var forward = GlobalTransform.Basis.Z;
            return (seat, Mathf.Atan2(forward.X, forward.Z));
        }
    }

    /// <summary>Where the engine driver sits, in the cab.</summary>
    internal Vector3 CabSeat => GlobalTransform * new Vector3(0, 1.9f, 4.1f);

    internal LineTrain()
    {
        AddChild(body);
        BuildEngine();
        BuildTender();
        BuildWagon();
        whistle = new Label3D
        {
            Text = "TÚÚÚT!",
            FontSize = 90,
            PixelSize = .012f,
            OutlineSize = 12,
            Modulate = new Color("fff5d9"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0, 6.2f, 6.4f),
            Visible = false,
        };
        body.AddChild(whistle);
        smoke = Art.ChimneySmoke(body, new Vector3(0, 4.4f, 6.5f));
        smoke.Emitting = false;
    }

    private Node3D Wheel(float x, float y, float z, float radius)
    {
        var pivot = new Node3D { Position = new Vector3(x, y, z) };
        body.AddChild(pivot);
        var wheel = Art.Cylinder(pivot, Vector3.Zero, radius, radius, .16f, "2e3438");
        wheel.RotationDegrees = new Vector3(0, 0, 90);
        Art.Block(pivot, new(x > 0 ? .1f : -.1f, 0, 0), new(.12f, radius * 1.6f, .1f), "e8453c");
        wheels.Add(pivot);
        return pivot;
    }

    private void BuildEngine()
    {
        // The boiler along the track, the cab behind it, the chimney and dome, the cowcatcher in front.
        var boiler = Art.Cylinder(body, new(0, 1.9f, 6.9f), .95f, .95f, 4.4f, "2f5d62");
        boiler.RotationDegrees = new Vector3(90, 0, 0);
        Art.Cylinder(body, new(0, 1.9f, 9.15f), .96f, .96f, .12f, "30363a").RotationDegrees = new Vector3(90, 0, 0);
        Art.Block(body, new(0, 1.2f, 6.2f), new(1.9f, 1.1f, 4.4f), "30363a");
        Art.Cylinder(body, new(0, 3.55f, 8.3f), .3f, .46f, 1.1f, "30363a");
        Art.Ball(body, new(0, 2.95f, 6.7f), new(.55f, .45f, .55f), "e0b04a");
        Art.Block(body, new(0, 2.2f, 4.1f), new(2.2f, 2.6f, 2.0f), "8a2f2a");
        Art.Block(body, new(0, 3.62f, 4.1f), new(2.5f, .2f, 2.4f), "30363a");
        foreach (float x in new[] { -1.12f, 1.12f })
            Art.Window(body, new(x, 2.5f, 4.1f), new(.06f, .8f, .9f), "9ed0e6");
        Art.Block(body, new(0, .65f, 9.3f), new(1.8f, .5f, .5f), "e8453c");
        Art.Lamp(body, new(0, 2.0f, 9.38f), new(.4f, .4f, .1f), "fff1b8");
        // Wheels: two big pairs under the boiler and a small pair in front.
        foreach (float x in new[] { -1.05f, 1.05f })
        {
            Wheel(x, .62f, 5.3f, .62f);
            Wheel(x, .62f, 6.9f, .62f);
            Wheel(x, .42f, 8.4f, .42f);
        }
        Art.Block(body, new(0, .55f, 6.2f), new(2.1f, .22f, 5.4f), "30363a");
    }

    private void BuildTender()
    {
        Art.Block(body, new(0, 1.25f, 0.6f), new(2.1f, 1.5f, 3.2f), "30363a");
        Art.Block(body, new(0, 2.15f, 0.6f), new(1.7f, .5f, 2.8f), "1d1d1d");
        foreach (float x in new[] { -1.0f, 1.0f })
        {
            Wheel(x, .5f, -.4f, .5f);
            Wheel(x, .5f, 1.6f, .5f);
        }
    }

    private void BuildWagon()
    {
        // The flatbed: a deck on two pairs of wheels with a rail along its edge, a buffer at each end.
        Art.Block(body, new(0, .95f, -5.2f), new(2.6f, .24f, 7.6f), "8a6040");
        Art.Block(body, new(0, .72f, -5.2f), new(2.2f, .24f, 7.2f), "30363a");
        foreach (float x in new[] { -1.25f, 1.25f })
        {
            Art.Block(body, new(x, 1.15f, -5.2f), new(.1f, .16f, 7.6f), "d9b27a");
            Wheel(x * .85f, .5f, -3.2f, .5f);
            Wheel(x * .85f, .5f, -7.2f, .5f);
        }
        foreach (float z in new[] { -9.05f, -1.4f })
            Art.Block(body, new(0, 1.0f, z), new(2.2f, .3f, .22f), "e8453c");
    }

    /// <summary>The train stands on a track with its middle at a distance along it.</summary>
    internal void Stand(LineRoute route, float distance)
    {
        Place(route, distance);
        running = false;
        smoke.Emitting = false;
    }

    /// <summary>The train runs on a track with its middle at a distance along it.</summary>
    internal void Run(LineRoute route, float distance, float travel)
    {
        Place(route, distance);
        running = true;
        travelled = travel;
        if (!smoke.Emitting)
            smoke.Emitting = true;
    }

    /// <summary>The engine whistles for a moment.</summary>
    internal void Whistle() => whistleLeft = 1.4f;

    private void Place(LineRoute route, float distance)
    {
        float half = Length / 2;
        var front = route.At(distance + half);
        var rear = route.At(distance - half);
        var middle = route.At(distance);
        Position = middle;
        var along = front - rear;
        if (along.LengthSquared() > .01f)
            Basis = Basis.LookingAt(-along, Vector3.Up);
    }

    internal void Tick(float delta)
    {
        if (whistleLeft > 0)
            whistleLeft = Math.Max(0, whistleLeft - delta);
        whistle.Visible = whistleLeft > 0;
        if (!running)
            return;
        foreach (var wheel in wheels)
            wheel.Rotation = new Vector3(-travelled * 1.6f, 0, 0);
    }
}
