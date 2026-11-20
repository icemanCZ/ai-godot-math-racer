using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The flowers and small bushes that the car drives over: a car that stands on them or jumps over them leaves them
/// standing, a moving car knocks them down and throws up their petals, a plant stays down while the car is on it, and
/// a while later every plant stands as it was and the petals are gone. The game's own frame is run (game._Process), so
/// the system in Game.Systems is checked with the car's real velocity.
/// </summary>
public partial class TramplingChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;

    public TramplingChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private void Frames(float seconds)
    {
        for (float t = 0; t < seconds; t += .05f)
            game._Process(.05);
    }

    /// <summary>A plant of the kind with no other plant within five metres and no character near.</summary>
    private Vector3 Lonely(Trampling.Kind kind, List<Vector3> all)
    {
        var actors = world.InteractionActors.Select(a => a.Position).ToArray();
        foreach (var foot in world.Plants.Trampling.Feet(kind))
        {
            if (actors.Any(a => a.DistanceTo(foot) < 20))
                continue;
            int near = 0;
            foreach (var other in all)
                if (new Vector2(other.X - foot.X, other.Z - foot.Z).LengthSquared() < 25 && ++near > 1)
                    break;
            if (near == 1)
                return foot;
        }
        throw new InvalidOperationException($"no lonely {kind}");
    }

    /// <summary>Puts the car on <paramref name="foot"/>, going north at <paramref name="speed"/>.</summary>
    private void Over(Vector3 foot, float speed, float lift = .15f)
    {
        car.SetPose(foot + Vector3.Up * lift, 0);
        car.Velocity = -car.Transform.Basis.Z * speed;
    }

    private void Away()
    {
        car.Reset();
        car.Velocity = Vector3.Zero;
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var trampling = world.Plants.Trampling;
            Check(game.State == GameState.Exploring, "the game is driving");
            Check(
                trampling.FlowerCount > 3000 && trampling.BushCount > 500,
                $"the flowers and bushes are registered ({trampling.FlowerCount} flowers, {trampling.BushCount} bushes)"
            );
            var all = trampling.Feet(Trampling.Kind.Flower).Concat(trampling.Feet(Trampling.Kind.Bush)).ToList();
            Check(
                all.Count == trampling.FlowerCount + trampling.BushCount,
                "every registered plant can be found where it stands"
            );
            var flower = Lonely(Trampling.Kind.Flower, all);
            var bush = Lonely(Trampling.Kind.Bush, all);

            Over(flower, 0);
            Frames(.5f);
            Check(
                trampling.BentCount == 0 && trampling.PetalCount == 0,
                "a car standing on a flower leaves it standing"
            );
            Over(flower, 6, 3);
            Frames(.1f);
            Check(trampling.BentCount == 0, "a car jumping over a flower leaves it standing");

            Over(flower, 6);
            game._Process(.05);
            Check(trampling.BentCount == 1, "a moving car knocks the flower down");
            int petals = trampling.PetalCount;
            Check(petals >= 4, $"its petals fly up ({petals})");
            Frames(1);
            Check(trampling.PetalCount <= petals, "a flower that is already down throws no more petals");
            Away();
            Frames(3);
            Check(trampling.BentCount == 1, "the flower lies a while after the car has gone");
            Frames(10);
            Check(trampling.BentCount == 0, "the flower stands up again and its blossom grows back");
            Frames(.5f);
            Check(trampling.PetalCount == 0, "the petals shrink away");

            Over(flower, 6);
            game._Process(.05);
            Check(
                trampling.BentCount == 1 && trampling.PetalCount >= 4,
                "a flower that has grown back goes down again"
            );
            Away();

            Over(bush, 5);
            game._Process(.05);
            Check(trampling.BentCount == 2, "a moving car presses the bush down");
            Away();
            Frames(.2f);
            Check(trampling.BentCount == 2, "the bush springs back after the car has passed");
            Frames(5);
            Check(trampling.BentCount == 1, "the bush stands as it was");

            game.TogglePause();
            int down = trampling.BentCount;
            Frames(20);
            Check(trampling.BentCount == down && down == 1, "nothing grows while the game is paused");
            game.TogglePause();
            Frames(15);
            Check(trampling.BentCount == 0 && trampling.PetalCount == 0, "every plant is as it was");

            GD.Print("TRAMPLING_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
