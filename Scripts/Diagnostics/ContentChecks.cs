using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Diagnostics;

/// <summary>
/// <c>--content-checks</c>: a valley has all of the game's content, whatever the seed. The generation is what makes it
/// so (every search has a last pass that takes anything, and plants are cleared where a site needs room); this suite
/// only looks at a few seeds, and <c>./test.ps1 -Suite content -Seeds (1..150)</c> is the sweep for a change to it.
/// </summary>
public partial class ContentChecks : Node
{
    private readonly World world;
    private readonly List<string> missing = new();

    public ContentChecks(World world) => this.world = world;

    private void Need(bool present, string what)
    {
        if (!present)
            missing.Add(what);
    }

    public override void _Ready()
    {
        try
        {
            var village = world.Village;
            Need(world.Animals.Length == World.QuestAnimalCount, "the roadside friends");
            Need(world.Witch != null && world.Sorcerer != null, "the witch and the sorcerer");
            Need(village.Residents.Count == 4, "the four house residents");
            Need(village.Traders.Count == 3, "the three shops");
            Need(village.BuildPlots.Count == PlotCount, "the six building plots");
            Need(village.BuildPlots.All(plot => plot.Resident != null), "a newcomer on every plot");
            Need(village.Farmer != null && village.Gardener != null, "the farmer and the gardener");
            Need(village.GemCollector != null && village.Toymaker != null, "the gem collector and the toymaker");

            Need(
                world.Ford != null && world.Beaver != null && world.Tracks != null,
                "the bridge, the beaver and the paths"
            );
            Need(world.HaulSources.Length == 3, "the quarry, the clay pit and the sand pit");
            Need(world.LogYard != null, "the log yard");
            Need(world.Industry.Length == WorksCatalog.Count, "the sawmill, the brickworks and the gravel works");
            Need(world.Glassworks != null, "the glassworks");
            Need(world.AllWorks.Count() == WorksCatalog.Total, "all five works");
            Need(world.Bakery != null && world.Farm != null, "the wheat farm and the bakery");
            Need(world.WheatField != null && world.FieldFarmer != null, "the wheat field and its farmer");
            Need(world.Hidden.Friends.Count == HiddenFriends.Total, "the small hidden friends");
            Need(world.AllWorks.All(works => works.Worker != null), "a worker at every works");
            Need(world.SourceWorkers.All(worker => worker != null), "the workers of the first sites");
            Need(world.ExtraSources.Length == SourceCatalog.Extra.Length, "a second site for every raw material");
            Need(world.ExtraSourceWorkers.All(worker => worker != null), "the workers of the second sites");
            Need(world.Projects.Length == BuildPlans.Count, "the bridge, the mill, the tower and the jetty");
            Need(world.Projects.All(project => project?.Builder != null), "a builder for every building");
            Need(
                world.Duck != null && world.WorldPets.Count == WorldPetsExpected,
                $"the pets of the world ({world.WorldPets.Count} of {WorldPetsExpected})"
            );

            if (missing.Count > 0)
                throw new InvalidOperationException($"seed {Settings.Seed} lacks: {string.Join(", ", missing)}");
            GD.Print($"PASS: seed {Settings.Seed} has all the content");
            GD.Print("CONTENT_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private const int PlotCount = 6;

    /// <summary>The duck, five raw materials (wheat too), every works, the bridge, the mill, the tower and a pet per hidden kind.</summary>
    private static readonly int WorldPetsExpected =
        1 + 5 + WorksCatalog.Total + 3 + WorldPetCatalog.DucklingDeliveries.Length + HiddenFriends.KindCount;
}
