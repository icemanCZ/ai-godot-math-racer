using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer;

public enum VillageTaskKind
{
    Math,
    Letters,
    Fruit,

    /// <summary>The resident wants a load of planks from the sawmill, brought on the trailer.</summary>
    Planks,

    /// <summary>The resident wants a load of bricks from the brickworks, brought on the trailer.</summary>
    Bricks,
}

public partial class Village
{
    /// <summary>Each kind of task appears this often in the ten tasks of a house.</summary>
    internal const int TasksPerKind = 2;

    private readonly VillageTaskKind[][] tasks = new VillageTaskKind[4][];

    /// <summary>Where the trailer tips a house's building material: beside the road in front of the house.</summary>
    private readonly List<Vector3> houseDrops = new();

    public VillageTaskKind TaskAt(int house) => tasks[house][Level(house)];

    public IReadOnlyList<VillageTaskKind> Tasks(int house) => Array.AsReadOnly(tasks[house]);

    /// <summary>The ring in front of a house where its material is tipped out.</summary>
    internal Vector3 HouseDrop(int house) => houseDrops[house];

    /// <summary>The goods a task asks for, or null for a quiz or an apple collection.</summary>
    internal static HaulLoad? MaterialOf(VillageTaskKind kind) =>
        kind switch
        {
            VillageTaskKind.Planks => HaulLoad.Planks,
            VillageTaskKind.Bricks => HaulLoad.Bricks,
            _ => null,
        };

    internal static VillageTaskKind[] CreateTasks(int seed, int house)
    {
        var random = new Random(unchecked(seed + 7319 + house * 173));
        var result = new VillageTaskKind[Upgrades.Length];
        foreach (var kind in Enum.GetValues<VillageTaskKind>())
            for (int i = 0; i < TasksPerKind; i++)
                result[(int)kind * TasksPerKind + i] = kind;
        for (int i = result.Length - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);
            (result[i], result[other]) = (result[other], result[i]);
        }
        return result;
    }
}
