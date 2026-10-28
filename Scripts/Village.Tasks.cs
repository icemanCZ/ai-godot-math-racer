using System;
using System.Collections.Generic;

namespace MathRacer;

public enum VillageTaskKind { Math, Letters, Fruit }

public partial class Village
{
    private readonly VillageTaskKind[][] tasks = new VillageTaskKind[4][];
    public VillageTaskKind TaskAt(int house) => tasks[house][Level(house)];
    public IReadOnlyList<VillageTaskKind> Tasks(int house) => Array.AsReadOnly(tasks[house]);

    internal static VillageTaskKind[] CreateTasks(int seed, int house)
    {
        var random = new Random(unchecked(seed + 7319 + house * 173));
        var result = new[] { VillageTaskKind.Math, VillageTaskKind.Math, VillageTaskKind.Math,
            VillageTaskKind.Letters, VillageTaskKind.Letters, VillageTaskKind.Letters,
            VillageTaskKind.Fruit, VillageTaskKind.Fruit, VillageTaskKind.Fruit,
            (VillageTaskKind)random.Next(3) };
        for (int i = result.Length - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);
            (result[i], result[other]) = (result[other], result[i]);
        }
        return result;
    }
}
