using System;
using System.Collections.Generic;

namespace MathRacer.Buildings;

/// <summary>What a trailer can carry. Values are persisted (in saves and in open loading questions).</summary>
public enum HaulLoad
{
    Stones = 0,
    Soil = 1,
    Sand = 2,

    /// <summary>Hauled from the log yard to the sawmill, or straight to the jetty for its piles.</summary>
    Logs = 3,

    // Goods that a works has made, hauled from its yard to a building.
    Planks = 4,
    Bricks = 5,
    Gravel = 6,

    // One special load for each of the mill, the tower and the jetty, fetched from a collector.
    Fabric = 7,
    Capstone = 8,
    Flag = 9,

    /// <summary>Glasses made from sand at the glassworks, hauled from its yard to a resident's house.</summary>
    Glass = 10,

    /// <summary>Wheat from the farmer's field, hauled to the finished mill (in saves from before the mill, to the bakery).</summary>
    Wheat = 11,

    /// <summary>Loaves of bread baked at the bakery, hauled from its yard to a resident's house.</summary>
    Bread = 12,

    /// <summary>Flour ground at the finished mill from the wheat, hauled from the mill to the bakery.</summary>
    Flour = 13,

    /// <summary>A guest of the castle's feast on the bench of the trailer, from home to the castle.</summary>
    Guest = 14,
}

internal static class HaulLoads
{
    public static bool IsGoods(HaulLoad load) =>
        load is HaulLoad.Planks or HaulLoad.Bricks or HaulLoad.Gravel or HaulLoad.Glass or HaulLoad.Bread;

    public static bool IsSpecial(HaulLoad load) => load is HaulLoad.Fabric or HaulLoad.Capstone or HaulLoad.Flag;

    /// <summary>The works that makes a goods load, in <see cref="Product"/> order.</summary>
    public static Product ProductOf(HaulLoad load) =>
        load == HaulLoad.Glass ? Product.Glass
        : load == HaulLoad.Bread ? Product.Bread
        : (Product)(load - HaulLoad.Planks);

    public static HaulLoad GoodsOf(Product product) =>
        product == Product.Glass ? HaulLoad.Glass
        : product == Product.Bread ? HaulLoad.Bread
        : HaulLoad.Planks + (int)product;
}

/// <summary>One delivery to a building: what is carried and whether it goes to the far bank of the river.</summary>
internal readonly record struct BuildStep(HaulLoad Cargo, bool FarBank = false);

/// <summary>
/// What each building needs: ten deliveries in a fixed order. Goods come from the works' stock, raw loads from
/// the pits and the log yard and the last delivery of the mill, tower and jetty from a collector.
/// </summary>
internal static class BuildPlans
{
    public const int Bridge = 0;
    public const int Mill = 1;
    public const int Tower = 2;
    public const int Jetty = 3;
    public const int Count = 4;
    public const int Trips = 10;

    private static BuildStep[] Steps(params HaulLoad[] cargo)
    {
        var steps = new BuildStep[cargo.Length];
        for (int i = 0; i < cargo.Length; i++)
            steps[i] = new BuildStep(cargo[i]);
        return steps;
    }

    private static readonly BuildStep[][] plans =
    {
        // The bridge: gravel for the piers and abutments, bricks (one load for the far bank) for the ramps,
        // planks for the deck and a load of sand to finish.
        FarBankStep(
            Steps(
                HaulLoad.Gravel,
                HaulLoad.Gravel,
                HaulLoad.Gravel,
                HaulLoad.Bricks,
                HaulLoad.Bricks,
                HaulLoad.Bricks,
                HaulLoad.Planks,
                HaulLoad.Planks,
                HaulLoad.Planks,
                HaulLoad.Sand
            ),
            4
        ),
        // The mill: foundation, walls, frame and roof, then cloth for the sails.
        Steps(
            HaulLoad.Gravel,
            HaulLoad.Gravel,
            HaulLoad.Bricks,
            HaulLoad.Bricks,
            HaulLoad.Bricks,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Fabric
        ),
        // The tower: foundation, four storeys, stairs and platform, then the stone for its tip.
        Steps(
            HaulLoad.Gravel,
            HaulLoad.Gravel,
            HaulLoad.Gravel,
            HaulLoad.Bricks,
            HaulLoad.Bricks,
            HaulLoad.Bricks,
            HaulLoad.Bricks,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Capstone
        ),
        // The jetty: footing, piles from raw logs, planks, then the flag.
        Steps(
            HaulLoad.Gravel,
            HaulLoad.Gravel,
            HaulLoad.Logs,
            HaulLoad.Logs,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Planks,
            HaulLoad.Flag
        ),
    };

    private static BuildStep[] FarBankStep(BuildStep[] steps, int index)
    {
        steps[index] = steps[index] with { FarBank = true };
        return steps;
    }

    public static IReadOnlyList<BuildStep> For(int project) => plans[project];
}
