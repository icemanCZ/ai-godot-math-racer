using Godot;

namespace MathRacer.App;

public partial class Game
{
    /// <summary>Half the width and half the length of the car's body (its collision box).</summary>
    private static readonly Vector2 CarFootprint = new(.92f, 1.55f);

    /// <summary>Half the width and half the length of the trailer's bed and wheels, round its axle.</summary>
    private static readonly Vector2 TrailerFootprint = new(1, 1.1f);

    /// <summary>
    /// The flowers and small bushes under the moving car and its trailer go down and throw up their petals
    /// (<see cref="Valley.Trampling"/>); the plants that are down stand up again and the petals fly on, also while a
    /// panel is open, but not while the game is paused.
    /// </summary>
    private void TickTrampling(float dt)
    {
        if (State is GameState.Paused or GameState.Title)
            return;
        var trampling = world.Plants.Trampling;
        if (State == GameState.Exploring)
        {
            var velocity = vehicle.Velocity;
            trampling.Touch(vehicle.Position, -vehicle.Transform.Basis.Z, CarFootprint.X, CarFootprint.Y, velocity);
            if (trailer.Attached)
                trampling.Touch(
                    trailer.Position,
                    -trailer.Transform.Basis.Z,
                    TrailerFootprint.X,
                    TrailerFootprint.Y,
                    velocity
                );
        }
        trampling.Tick(dt, Weather.Wind);
    }
}
