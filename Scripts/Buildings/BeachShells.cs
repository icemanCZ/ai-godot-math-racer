using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The shells of a tide on the wet sand of the cove (<c>docs/SHELLS_PLAN.md</c>), each with a little twinkle above it so
/// that a child sees it from the car, shown only while the low tide uncovers it and until it is picked up; and the sign at
/// the edge of the valley above the cove, with a scallop painted on it.
/// </summary>
public partial class BeachShells : Node3D
{
    /// <summary>How much bigger than the models below the shells lie on the sand.</summary>
    private const float ShellSize = 2.2f;

    private readonly List<(BeachShell Shell, Node3D Model, Node3D Twinkle)> laid = new();
    private float time;

    /// <summary>How many kinds of shell the player has, for the crab who waits for all of them.</summary>
    internal int KindsFound { get; set; }

    internal BeachShells(Vector3? sign)
    {
        if (sign is { } foot)
            BuildSign(foot);
    }

    /// <summary>The shells now laid out, for the checks.</summary>
    internal IReadOnlyList<BeachShell> Laid => laid.ConvertAll(entry => entry.Shell);

    /// <summary>Whether a shell laid out shows, for the checks.</summary>
    internal bool Shown(int number) => laid.Exists(entry => entry.Shell.Number == number && entry.Model.Visible);

    /// <summary>Lays out the shells of a tide in place of those before.</summary>
    internal void Lay(IEnumerable<BeachShell> shells)
    {
        foreach (var (_, model, _) in laid)
        {
            RemoveChild(model);
            model.QueueFree();
        }
        laid.Clear();
        foreach (var shell in shells)
        {
            var model = new Node3D
            {
                Name = $"Shell{shell.Number}",
                Position = shell.Position,
                Rotation = new Vector3(0, shell.Yaw, 0),
                // Bigger than a real shell, so that a child sees it from the car.
                Scale = Vector3.One * ShellSize,
                Visible = false,
            };
            AddChild(model);
            BuildShell(model, shell.Kind);
            var twinkle = new Node3D { Name = "Twinkle", Position = new Vector3(0, 1.1f, 0) };
            model.AddChild(twinkle);
            Art.Lamp(twinkle, Vector3.Zero, new(.22f, .22f, .22f), "fff3b0").RotationDegrees = new(45, 0, 45);
            Art.Lamp(twinkle, Vector3.Zero, new(.06f, .5f, .06f), "fffbe0");
            Art.Lamp(twinkle, Vector3.Zero, new(.5f, .06f, .06f), "fffbe0");
            laid.Add((shell, model, twinkle));
        }
    }

    /// <summary>Shows the shells not yet picked up that the sea at <paramref name="sea"/> leaves uncovered.</summary>
    internal void Refresh(Func<int, bool> picked, float sea, double delta)
    {
        time += (float)delta;
        foreach (var (shell, model, twinkle) in laid)
        {
            model.Visible = !picked(shell.Number) && shell.Position.Y > sea + .02f;
            if (!model.Visible)
                continue;
            twinkle.Position = new Vector3(0, 1.1f + Mathf.Sin(time * 3 + shell.Number) * .15f, 0);
            twinkle.Rotation = new Vector3(0, time * 2 + shell.Number, 0);
            twinkle.Scale = Vector3.One * (1 + .25f * Mathf.Sin(time * 5 + shell.Number * 2));
        }
    }

    private void BuildSign(Vector3 foot)
    {
        var sign = new Node3D { Name = "ShellSign", Position = foot };
        AddChild(sign);
        Art.Cylinder(sign, Vector3.Up * 1.2f, .09f, .09f, 2.4f, "7a5236");
        Art.Block(sign, Vector3.Up * 2.25f, new(1.5f, .8f, .1f), "f3e6c4");
        // A scallop painted on both faces of the board.
        foreach (float face in new[] { -1f, 1f })
        {
            Art.Ball(sign, new(0, 2.2f, face * .06f), new(.32f, .26f, .02f), "f08a5d");
            for (int rib = -2; rib <= 2; rib++)
                Art.Block(sign, new(rib * .1f, 2.24f, face * .075f), new(.035f, .4f, .01f), "e06b45").RotationDegrees =
                    new(0, 0, -rib * 14);
            Art.Block(sign, new(0, 1.98f, face * .07f), new(.18f, .08f, .01f), "f4b48f");
        }
        Art.Sign(sign, Vector3.Up * 3, OutlandTexts.SignShells, 32);
        Obstacles.Trunk(this, foot, .1f, 2.4f);
    }

    /// <summary>A shell of <paramref name="kind"/>, lying on the sand, about as big as a child's two hands put together.</summary>
    private static void BuildShell(Node3D at, int kind)
    {
        switch (kind)
        {
            case ShellPlans.Scallop:
                Art.Ball(at, new(0, .05f, 0), new(.36f, .07f, .3f), "f08a5d");
                for (int rib = -2; rib <= 2; rib++)
                    Art.Block(at, new(rib * .1f, .1f, -.02f), new(.04f, .03f, .5f), "e06b45").RotationDegrees = new(
                        0,
                        rib * 16,
                        0
                    );
                Art.Block(at, new(0, .06f, .3f), new(.2f, .06f, .1f), "f4b48f");
                break;
            case ShellPlans.Cockle:
                Art.Ball(at, new(0, .06f, 0), new(.27f, .16f, .24f), "efe2c4");
                for (int rib = -2; rib <= 2; rib++)
                    Art.Block(at, new(rib * .09f, .14f, 0), new(.03f, .1f, .42f), "d9c49b");
                break;
            case ShellPlans.Mussel:
                Art.Ball(at, new(0, .06f, 0), new(.17f, .09f, .36f), "2d3e66");
                Art.Ball(at, new(0, .1f, .02f), new(.13f, .05f, .3f), "5c6f9a");
                break;
            case ShellPlans.Whelk:
            {
                var spiral = new Node3D { RotationDegrees = new(0, 0, 72), Position = new(0, .2f, 0) };
                at.AddChild(spiral);
                Art.Cylinder(spiral, new(0, -.1f, 0), .22f, .15f, .2f, "d8b27a");
                Art.Cylinder(spiral, new(0, .08f, 0), .15f, .08f, .18f, "c99a5e");
                Art.Cylinder(spiral, new(0, .24f, 0), .08f, .01f, .16f, "b8864a");
                Art.Ball(at, new(-.2f, .14f, .06f), new(.1f, .13f, .09f), "f6d9c0");
                break;
            }
            case ShellPlans.Cowrie:
                Art.Ball(at, new(0, .08f, 0), new(.2f, .13f, .3f), "b7774a");
                foreach (var (x, z) in new[] { (-.08f, -.1f), (.07f, .02f), (-.04f, .14f), (.09f, -.16f), (0, -.02f) })
                    Art.Ball(at, new(x, .19f, z), new(.04f, .02f, .04f), "f3e3c9");
                break;
            case ShellPlans.Starfish:
                Art.Ball(at, new(0, .05f, 0), new(.14f, .06f, .14f), "f39a4b");
                for (int arm = 0; arm < 5; arm++)
                {
                    var holder = new Node3D { Rotation = new(0, arm * Mathf.Tau / 5, 0) };
                    at.AddChild(holder);
                    Art.Ball(holder, new(0, .04f, .2f), new(.08f, .045f, .2f), "f07a2b");
                }
                break;
            case ShellPlans.Urchin:
                Art.Ball(at, new(0, .12f, 0), new(.22f, .16f, .22f), "6b3f7a");
                for (int spike = 0; spike < 12; spike++)
                {
                    float a = spike * Mathf.Tau / 12;
                    var dir = new Vector3(Mathf.Cos(a), spike % 2 == 0 ? .5f : .2f, Mathf.Sin(a)).Normalized();
                    Art.Beam(
                        at,
                        new Vector3(0, .12f, 0) + dir * .18f,
                        new Vector3(0, .12f, 0) + dir * .36f,
                        .025f,
                        "8a5a9a"
                    );
                }
                break;
            default:
                Art.Ball(at, new(0, .04f, 0), new(.3f, .06f, .26f), "c9bfc9");
                var lid = Art.Ball(at, new(0, .14f, .16f), new(.3f, .05f, .26f), "b4a8b8");
                lid.RotationDegrees = new(-38, 0, 0);
                Art.Ball(at, new(0, .11f, -.02f), Vector3.One * .085f, "fffdf8");
                break;
        }
    }
}
