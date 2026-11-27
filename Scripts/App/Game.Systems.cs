using System;
using System.Collections.Generic;

namespace MathRacer.App;

/// <summary>A part of the game that does something every frame, such as moving the companions or the coins.</summary>
internal sealed record GameSystem(string Name, Action<double> Tick);

public partial class Game
{
    private readonly List<GameSystem> systems = new();

    /// <summary>
    /// The parts of the game that run every frame once the car, the trailer and the task markers have been looked
    /// after, in the order they run. A new feature that needs a tick registers it here instead of changing
    /// <see cref="_Process"/>.
    /// </summary>
    private void RegisterSystems()
    {
        Register("speech", _ => TickSpeech());
        Register("companion stop", delta => TickCompanionStop((float)delta));
        Register("golem", delta => TickGolem((float)delta));
        Register("collector pets", delta => TickCollectorPets((float)delta));
        Register("driving events", TickEvents);
        Register("postcards", TickPostcards);
        Register("bandits", delta => TickBandits((float)delta));
        Register("coins", TickCoins);
        Register("hidden friends", delta => TickHidden((float)delta));
        Register("playground", _ => TickPlayground());
        Register("railway", _ => TickRailway());
        Register("zoo", _ => TickZoo());
        Register("mill", _ => TickMill());
        Register("lookout", delta => TickLookout((float)delta));
        Register("lamps", delta => TickLamps((float)delta));
        Register("treasure", delta => TickTreasure((float)delta));
        Register("fireflies", delta => TickFireflies((float)delta));
        Register("hunt", delta => TickHunt((float)delta));
        Register("letter", TickLetter);
        Register("lighthouse", TickLighthouse);
        Register("dragon", TickDragon);
        Register("balloon", TickBalloon);
        Register("tracking", TickTracking);
        Register("sheep", TickSheep);
        Register("rally", TickRally);
        Register("explorer", TickExplorer);
        Register("shells", TickShells);
        Register("flutters", delta => TickFlutters((float)delta));
        Register("trampling", delta => TickTrampling((float)delta));
        // The meshes built after the valley: more of them a frame while the title or the pause is shown.
        Register("far parts", _ => world.FarParts.Run(State is GameState.Title or GameState.Paused ? 25 : 5));
        Register("distant details", _ => DistantDetails.Tick());
        Register("outland colliders", _ => world.Ground.StreamColliders(vehicle.GlobalPosition));
        // Round the camera, from which the plants are drawn in full or simply: the car's, or a view of its own.
        Register("outland plants", _ =>
            world.Plants.StreamOutland(GetViewport().GetCamera3D()?.GlobalPosition ?? vehicle.GlobalPosition)
        );
    }

    private void Register(string name, Action<double> tick) => systems.Add(new GameSystem(name, tick));
}
