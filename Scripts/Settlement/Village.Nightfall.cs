using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;

namespace MathRacer.Settlement;

public partial class Village
{
    private readonly Dictionary<int, Node3D> windowLights = new();

    /// <summary>
    /// Switches the lamps on in the evening: the windows of a house whose resident was given glasses (the housewarming
    /// of the glassworks) and of every newcomer's finished house shine warm yellow. The lights are separate nodes, so
    /// the houses themselves are generated exactly as before.
    /// </summary>
    internal void SetNightLights(bool on)
    {
        for (int house = 0; house < Houses.Count; house++)
        {
            bool lit = on && glazed[house];
            if (!windowLights.TryGetValue(house, out var lights))
            {
                if (!lit)
                    continue;
                lights = new Node3D { Name = "WindowLights" };
                Houses[house].AddChild(lights);
                foreach (float x in new[] { -2.15f, 2.15f })
                    Art.Lamp(lights, new(x, 2.5f, 3.255f), new(1, 1, .02f), "ffd77a");
                windowLights[house] = lights;
            }
            if (lights.Visible != lit)
                lights.Visible = lit;
        }
        foreach (var plot in buildPlots)
            plot.ShowNightLight(on && plot.Complete);
    }
}
