using System;
using Godot;
using MathRacer.Buildings;
using MathRacer.Graphics;
using MathRacer.Tasks;

namespace MathRacer.Characters;

/// <summary>One pet that the world gives: its kind, its name and what the player does to earn it.</summary>
internal sealed record WorldPetSpec(CollectorPetKind Kind, string Name, string Reward);

/// <summary>
/// The pets that are earned by hauling and building rather than by a collection: one for each raw material (ten loads
/// hauled), each works (twenty pieces made from ten loads) and each building (its completion; the jetty's is the duck).
/// </summary>
internal static class WorldPetCatalog
{
    /// <summary>The loads of a raw material that earn its pet.</summary>
    public const int SourceLoads = 10;

    /// <summary>The loads a works takes in for its pet, which at <see cref="Works.PerLoad"/> pieces a load is twenty pieces.</summary>
    public const int WorksLoads = 10;

    public static readonly WorldPetSpec Logs = new(CollectorPetKind.Logs, "Kládička", "10 odvezených klád");
    public static readonly WorldPetSpec Stones = new(CollectorPetKind.Stones, "Kamínek", "10 odvezených kamenů");
    public static readonly WorldPetSpec Soil = new(CollectorPetKind.Soil, "Hliněnka", "10 odvezených nákladů hlíny");
    public static readonly WorldPetSpec Sand = new(CollectorPetKind.Sand, "Pískulka", "10 odvezených nákladů písku");

    public static readonly WorldPetSpec Sawmill = new(CollectorPetKind.Sawmill, "Prkýnko", "20 vyrobených prken");
    public static readonly WorldPetSpec Brickworks = new(
        CollectorPetKind.Brickworks,
        "Cihlička",
        "20 vyrobených cihel"
    );
    public static readonly WorldPetSpec GravelWorks = new(CollectorPetKind.GravelWorks, "Štěrkáček", "20 kusů štěrku");
    public static readonly WorldPetSpec Glassworks = new(
        CollectorPetKind.Glassworks,
        "Sklenička",
        "20 vyrobených skleniček"
    );

    public static readonly WorldPetSpec Wheat = new(CollectorPetKind.Wheat, "Zrníčko", "10 odvezených nákladů obilí");
    public static readonly WorldPetSpec Bakery = new(CollectorPetKind.Bakery, "Housánek", "20 upečených chlebů");

    public static readonly WorldPetSpec Stationmaster = new(
        CollectorPetKind.Stationmaster,
        "Výpravčí Píšťalka",
        "dostavěné nádraží"
    );

    public static readonly WorldPetSpec PandaCub = new(CollectorPetKind.PandaCub, "Pandička Bambulka", "dostavěná zoo");

    public static readonly WorldPetSpec Bridge = new(CollectorPetKind.Bridge, "Mostíček", "dostavěný most");
    public static readonly WorldPetSpec Mill = new(CollectorPetKind.Mill, "Mlýnek", "dostavěný mlýn");
    public static readonly WorldPetSpec Tower = new(CollectorPetKind.Tower, "Věžička", "dostavěnou rozhlednu");

    // The small friends who hide beside the roads: finding all eight of a kind earns that animal.
    public static readonly WorldPetSpec Ladybug = new(
        CollectorPetKind.Ladybug,
        "Beruška Tečka",
        "8 nalezených berušek"
    );
    public static readonly WorldPetSpec Hedgehog = new(
        CollectorPetKind.Hedgehog,
        "Ježek Bodlínek",
        "8 nalezených ježků"
    );
    public static readonly WorldPetSpec Frog = new(CollectorPetKind.Frog, "Žabka Kvákalka", "8 nalezených žabek");

    /// <summary>The pet of a kind of hidden friend, in the order ladybug, hedgehog, frog.</summary>
    public static WorldPetSpec ForHidden(int kind) =>
        kind switch
        {
            0 => Ladybug,
            1 => Hedgehog,
            _ => Frog,
        };

    // The ducklings of the jetty's duck: one for every third delivery to the jetty, the duck itself for the tenth.
    public static readonly WorldPetSpec Duckling1 = new(
        CollectorPetKind.Duckling1,
        "Kachňátko Žlutka",
        "3 dodávky na molo"
    );
    public static readonly WorldPetSpec Duckling2 = new(
        CollectorPetKind.Duckling2,
        "Kachňátko Pírko",
        "6 dodávek na molo"
    );
    public static readonly WorldPetSpec Duckling3 = new(
        CollectorPetKind.Duckling3,
        "Kachňátko Bublinka",
        "9 dodávek na molo"
    );

    /// <summary>The deliveries to the jetty that earn each duckling, in the order of the ducklings.</summary>
    public static readonly int[] DucklingDeliveries = { 3, 6, 9 };

    public static WorldPetSpec ForDuckling(int index) =>
        index switch
        {
            0 => Duckling1,
            1 => Duckling2,
            _ => Duckling3,
        };

    public static WorldPetSpec ForSource(HaulLoad load) =>
        load switch
        {
            HaulLoad.Stones => Stones,
            HaulLoad.Soil => Soil,
            HaulLoad.Sand => Sand,
            HaulLoad.Wheat => Wheat,
            _ => Logs,
        };

    /// <summary>The pet of a works, in the order of <see cref="WorksCatalog.All"/>.</summary>
    public static WorldPetSpec ForWorks(int index) =>
        index switch
        {
            (int)Product.Planks => Sawmill,
            (int)Product.Bricks => Brickworks,
            (int)Product.Gravel => GravelWorks,
            (int)Product.Bread => Bakery,
            _ => Glassworks,
        };

    public static WorldPetSpec ForBuilding(int project) =>
        project switch
        {
            BuildPlans.Bridge => Bridge,
            BuildPlans.Mill => Mill,
            _ => Tower,
        };

    /// <summary>Where a pet of the world trails behind the car: rows of five behind the older pets.</summary>
    public static Vector3 Trail(int index) => new(((index % 5) - 2) * 1.8f, 0, 13.5f + index / 5 * 3.1f);
}

internal sealed partial class CollectorPet
{
    /// <summary>
    /// A pet of the world: it belongs to <paramref name="owner"/>, who stands where the pet waits, and is owned once
    /// <paramref name="count"/> reaches <paramref name="goal"/>. <paramref name="index"/> picks its place in the trail.
    /// </summary>
    internal CollectorPet(
        WorldPetSpec spec,
        Character owner,
        Func<int> count,
        int goal,
        int index,
        Vector3? trail = null
    )
    {
        externalCount = count;
        reward = spec.Reward;
        Kind = spec.Kind;
        Name = $"CollectorPet_{spec.Kind}";
        Collector = owner;
        DisplayName = spec.Name;
        Goal = goal;
        TrailOffset = trail ?? WorldPetCatalog.Trail(index);
        AddChild(model);
        switch (spec.Kind)
        {
            case CollectorPetKind.Logs:
                BuildLog();
                break;
            case CollectorPetKind.Stones:
                BuildPebble();
                break;
            case CollectorPetKind.Soil:
                BuildClod();
                break;
            case CollectorPetKind.Sand:
                BuildSandpile();
                break;
            case CollectorPetKind.Sawmill:
                BuildPlank();
                break;
            case CollectorPetKind.Brickworks:
                BuildBrick();
                break;
            case CollectorPetKind.GravelWorks:
                BuildGravelHeap();
                break;
            case CollectorPetKind.Glassworks:
                BuildGlass();
                break;
            case CollectorPetKind.Wheat:
                BuildSheaf();
                break;
            case CollectorPetKind.Bakery:
                BuildBun();
                break;
            case CollectorPetKind.Ladybug:
                BuildLadybug();
                break;
            case CollectorPetKind.Hedgehog:
                BuildHedgehog();
                break;
            case CollectorPetKind.Frog:
                BuildFrog();
                break;
            case CollectorPetKind.Duckling1:
            case CollectorPetKind.Duckling2:
            case CollectorPetKind.Duckling3:
                BuildDuckling();
                break;
            case CollectorPetKind.Stationmaster:
                BuildStationmaster();
                break;
            case CollectorPetKind.PandaCub:
                BuildPandaCub();
                break;
            case CollectorPetKind.Bridge:
                BuildBridge();
                break;
            case CollectorPetKind.Mill:
                BuildMill();
                break;
            case CollectorPetKind.Tower:
                BuildTower();
                break;
        }
    }

    /// <summary>A ball of the given full size (the spheres of <see cref="Art"/> are scaled by their radius).</summary>
    private MeshInstance3D Ball(Vector3 position, Vector3 size, string color) =>
        Art.Ball(model, position, size * .5f, color);

    /// <summary>A smile and rosy cheeks on the front of a body; <paramref name="z"/> is the surface they sit on.</summary>
    private void Smile(float y, float z, float width = .1f)
    {
        Art.Ball(model, new(0, y, z), new(width, .026f, .02f), "7a4a4a");
        foreach (int side in new[] { -1, 1 })
            Art.Ball(model, new(side * (width + .09f), y + .03f, z - .01f), new(.07f, .045f, .02f), "f4a29b");
    }

    private void BuildLog()
    {
        // A short log that stands on its feet, its pale end towards the front, with a sprout on top.
        Art.Log(model, new(0, .5f, 0), Vector3.Back, .36f, .64f, "8a5f3f");
        Art.Ball(model, new(.31f, .6f, -.1f), new(.14f, .1f, .14f), "7a4f33");
        Art.Cylinder(model, new(0, .9f, 0), .02f, .02f, .22f, "6eaa62");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(model, new(side * .1f, 1.04f, 0), new(.17f, .08f, .1f), "7bb86d").RotateZ(side * -.45f);
            Foot(new(side * .16f, .24f, .02f), .24f, .13f, "7a4f33", side);
        }
        Eyes(.125f, .56f, .325f);
        Smile(.4f, .328f);
    }

    private void BuildPebble()
    {
        Art.Ball(model, new(0, .5f, 0), new(.78f, .66f, .62f), "a3a39c");
        Art.Ball(model, new(-.2f, .72f, -.08f), new(.3f, .18f, .28f), "b7b6af");
        Art.Ball(model, new(.26f, .34f, -.05f), new(.26f, .2f, .3f), "8f8f89");
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .2f, .24f, .02f), .24f, .13f, "77756f", side);
        Eyes(.15f, .56f, .27f);
        Smile(.41f, .3f);
    }

    private void BuildClod()
    {
        Art.Ball(model, new(0, .46f, 0), new(.78f, .62f, .66f), "8a6142");
        Art.Ball(model, new(-.22f, .7f, -.06f), new(.34f, .24f, .3f), "6f4b33");
        Art.Ball(model, new(.24f, .32f, -.05f), new(.26f, .2f, .3f), "9c7150");
        Art.Cylinder(model, new(.05f, .86f, -.05f), .02f, .02f, .2f, "5d9a50");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(model, new(.05f + side * .1f, .98f, -.05f), new(.16f, .07f, .1f), "6eaa62").RotateZ(side * -.4f);
            Foot(new(side * .2f, .22f, .02f), .22f, .13f, "6f4b33", side);
        }
        Eyes(.15f, .52f, .28f);
        Smile(.38f, .315f);
    }

    private void BuildSandpile()
    {
        Art.Ball(model, new(0, .4f, 0), new(.9f, .72f, .78f), "e3c98a");
        Art.Ball(model, new(0, .62f, -.02f), new(.56f, .5f, .5f), "ecd79e");
        Art.Ball(model, new(.3f, .24f, .05f), new(.3f, .2f, .3f), "d6b873");
        // A small yellow bucket on top.
        Art.Cylinder(model, new(0, .95f, -.02f), .14f, .1f, .16f, "f2c14e");
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .22f, .2f, .04f), .2f, .14f, "d6b873", side);
        Eyes(.15f, .5f, .3f);
        Smile(.36f, .34f);
    }

    private void BuildPlank()
    {
        Art.Block(model, new(0, .62f, 0), new(.72f, 1.04f, .2f), "d9b27a");
        foreach (float y in new[] { .3f, .94f })
            Art.Block(model, new(0, y, 0), new(.74f, .06f, .22f), "6f5a46");
        // A knot and a little saw-blade hat.
        Art.Ball(model, new(.2f, .5f, .105f), new(.12f, .12f, .02f), "b8884f");
        var blade = Art.Cylinder(model, new(0, 1.2f, 0), .17f, .17f, .04f, "b9c2c9");
        blade.RotationDegrees = new Vector3(90, 0, 0);
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .2f, .12f, .02f), .2f, .13f, "7a4b32", side);
        Eyes(.14f, .78f, .1f);
        Smile(.62f, .106f, .09f);
    }

    private void BuildBrick()
    {
        Art.Block(model, new(0, .48f, 0), new(.86f, .64f, .5f), "c4623f");
        foreach (float y in new[] { .3f, .66f })
            Art.Block(model, new(0, y, .001f), new(.88f, .035f, .52f), "e0a384");
        Art.Block(model, new(0, .84f, 0), new(.8f, .06f, .44f), "d27652");
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .22f, .18f, .02f), .18f, .14f, "8a3f2d", side);
        Eyes(.17f, .56f, .25f);
        Smile(.42f, .253f);
    }

    private void BuildGravelHeap()
    {
        Art.Ball(model, new(0, .36f, 0), new(.9f, .6f, .74f), "b9b4a5");
        foreach (
            var (x, y, z, s, color) in new[]
            {
                (-.28f, .62f, -.05f, .26f, "cfcabb"),
                (.24f, .58f, -.12f, .24f, "a7a293"),
                (0f, .72f, -.1f, .22f, "d8d3c4"),
                (.34f, .24f, .22f, .2f, "8f8a7d"),
                (-.34f, .22f, .2f, .22f, "8f8a7d"),
            }
        )
            Art.Ball(model, new(x, y, z), Vector3.One * s, color);
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .22f, .2f, .02f), .2f, .13f, "77756f", side);
        Eyes(.15f, .5f, .3f);
        Smile(.35f, .34f);
    }

    private void BuildGlass()
    {
        // A pale-blue tumbler with a little water at the bottom, wider at the top.
        Art.Cylinder(model, new(0, .64f, 0), .24f, .3f, .86f, "cfeaf4");
        Art.Cylinder(model, new(0, .42f, 0), .25f, .27f, .4f, "8fd3ea");
        Art.Cylinder(model, new(0, 1.07f, 0), .31f, .31f, .03f, "e6f6fb");
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .15f, .22f, .02f), .22f, .12f, "9ec4d2", side);
        Eyes(.11f, .72f, .26f);
        Smile(.56f, .275f, .08f);
    }

    private void BuildSheaf()
    {
        // A little golden sheaf tied with a red ribbon, a face on the bundle and ears of wheat sticking out of the top.
        Art.Cylinder(model, new(0, .42f, 0), .3f, .22f, .78f, "e2bd5a");
        Art.Ball(model, new(0, .82f, 0), new(.3f, .12f, .27f), "d9b84a");
        Art.Block(model, new(0, .52f, 0), new(.58f, .09f, .52f), "d9604f");
        foreach (float lean in new[] { -.35f, 0, .35f })
        {
            var ear = Art.Ball(model, new(lean * .35f, 1.06f, 0), new(.07f, .2f, .07f), "f2c64a");
            ear.RotateZ(-lean);
        }
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .15f, .2f, .02f), .22f, .12f, "c9a13f", side);
        Eyes(.11f, .66f, .27f);
        Smile(.52f, .29f, .08f);
    }

    private void BuildBun()
    {
        // A plump round bun with a shiny top and two cuts, a face on its front and rosy cheeks.
        Art.Ball(model, new(0, .46f, 0), new(.58f, .44f, .54f), "c27a2c");
        Art.Ball(model, new(0, .58f, -.02f), new(.5f, .3f, .46f), "e0a04a");
        Art.Ball(model, new(-.14f, .84f, 0), new(.09f, .03f, .2f), "b8732a").RotateY(.5f);
        Art.Ball(model, new(.14f, .84f, 0), new(.09f, .03f, .2f), "b8732a").RotateY(-.5f);
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .2f, .16f, .02f), .22f, .12f, "9c5f1e", side);
        Eyes(.13f, .52f, .3f);
        Smile(.4f, .32f, .09f);
    }

    private void BuildLadybug()
    {
        // A red dome with black spots and a cream face on the front, two antennae and little black feet.
        Ball(new(0, .5f, -.04f), new(.86f, .72f, .8f), "d93a30");
        foreach (
            var (x, z, s) in new[] { (-.22f, -.1f, .15f), (.22f, -.12f, .15f), (0f, -.28f, .13f), (0f, .02f, .11f) }
        )
        {
            float y =
                .5f + .36f * Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow(x / .43f, 2) - Mathf.Pow((z + .04f) / .4f, 2)));
            Ball(new(x, y - .015f, z), new(s, s * .4f, s), "2b2b33");
        }
        Ball(new(0, .42f, .26f), new(.6f, .46f, .34f), "fff1d6");
        foreach (int side in new[] { -1, 1 })
        {
            var antenna = Art.Cylinder(model, new(side * .12f, .95f, .24f), .015f, .015f, .3f, "2b2b33");
            antenna.RotationDegrees = new(-10, 0, side * -22);
            Ball(new(side * .2f, 1.08f, .25f), Vector3.One * .07f, "2b2b33");
            Foot(new(side * .2f, .24f, .02f), .24f, .13f, "2b2b33", side);
        }
        Eyes(.12f, .5f, .4f);
        Smile(.37f, .42f, .07f);
    }

    private void BuildHedgehog()
    {
        // A round brown body with a ring of dark spikes over the back and a cream face.
        Ball(new(0, .5f, -.06f), new(.82f, .7f, .94f), "8a6446");
        var center = new Vector3(0, .5f, -.06f);
        var radii = new Vector3(.41f, .35f, .47f);
        foreach (var (lift, count, turn) in new[] { (25f, 7, 0f), (55f, 8, .3f), (82f, 6, .1f) })
        {
            for (int i = 0; i < count; i++)
            {
                float around = turn + i * Mathf.Tau / count;
                float flat = Mathf.Cos(Mathf.DegToRad(lift));
                var dir = new Vector3(
                    Mathf.Sin(around) * flat,
                    Mathf.Sin(Mathf.DegToRad(lift)),
                    Mathf.Cos(around) * flat
                );
                if (dir.Z > .35f && dir.Y < .85f)
                    continue;
                var surface = center + new Vector3(dir.X * radii.X, dir.Y * radii.Y, dir.Z * radii.Z);
                var spike = Art.Cylinder(model, surface + dir * .08f, .07f, 0, .3f, "4d3829");
                spike.Basis = new Basis(new Quaternion(Vector3.Up, dir.Normalized()));
            }
        }
        Ball(new(0, .4f, .42f), new(.44f, .36f, .38f), "f0d7b6");
        Ball(new(0, .43f, .62f), new(.12f, .09f, .08f), "34404a");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .22f, .72f, .36f), new(.14f, .16f, .09f), "8a6446");
            Foot(new(side * .18f, .22f, .1f), .22f, .12f, "d9bd98", side);
        }
        Eyes(.11f, .5f, .56f);
        Smile(.33f, .59f, .06f);
    }

    private void BuildFrog()
    {
        // A green frog with big eyes on top, a pale belly and a wide smile.
        Ball(new(0, .44f, 0), new(.8f, .6f, .76f), "6cbf5a");
        Ball(new(0, .34f, .2f), new(.56f, .4f, .4f), "d9f2a8");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .22f, .8f, .18f), Vector3.One * .3f, "6cbf5a");
            Ball(new(side * .22f, .84f, .26f), Vector3.One * .22f, "fffaf0");
            Ball(new(side * .22f, .84f, .37f), new(.11f, .13f, .07f), "34404a");
            Ball(new(side * .3f, .46f, .44f), new(.12f, .07f, .04f), "f4a29b");
            Foot(new(side * .26f, .26f, .08f), .26f, .14f, "5aa84b", side);
        }
        Ball(new(0, .4f, .42f), new(.4f, .04f, .05f), "7a3a3a");
    }

    private void BuildDuckling()
    {
        // A fluffy yellow chick of a duck, small enough to run in a row behind its mother.
        Ball(new(0, .5f, -.04f), new(.7f, .6f, .84f), "ffd84a");
        Ball(new(0, .86f, .22f), new(.48f, .46f, .46f), "ffe27a");
        Ball(new(0, .78f, .5f), new(.22f, .1f, .22f), "f58a2f");
        Ball(new(0, .52f, -.5f), new(.2f, .2f, .24f), "ffcc33");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .34f, .54f, -.06f), new(.16f, .28f, .5f), "f2c53a").RotateZ(side * .4f);
            Foot(new(side * .14f, .24f, .0f), .24f, .1f, "f58a2f", side);
        }
        Eyes(.1f, .92f, .36f);
        model.Scale = Vector3.One * .62f;
    }

    private void BuildBridge()
    {
        // A little stone arch bridge with a face on the front of the arch.
        Art.Block(model, new(0, .56f, 0), new(1.1f, .22f, .5f), "b9b4a5");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(model, new(side * .47f, .38f, 0), new(.24f, .5f, .48f), "a3a39c");
            Art.Block(model, new(side * .5f, .78f, .21f), new(.14f, .22f, .06f), "8f8a7d");
            Art.Block(model, new(side * .5f, .78f, -.21f), new(.14f, .22f, .06f), "8f8a7d");
            Foot(new(side * .4f, .16f, .0f), .16f, .16f, "77756f", side);
        }
        Art.Block(model, new(0, .78f, .21f), new(.86f, .05f, .05f), "8d6a46");
        Art.Block(model, new(0, .78f, -.21f), new(.86f, .05f, .05f), "8d6a46");
        Art.Ball(model, new(0, .26f, 0), new(.52f, .34f, .44f), "5a867d");
        Eyes(.11f, .44f, .22f);
        Smile(.34f, .245f, .07f);
    }

    private void BuildStationmaster()
    {
        // A tiny rabbit stationmaster: a cream body, a red cap with a white band between two long ears, a yellow
        // neckerchief and a signal paddle in the paw.
        Ball(new(0, .46f, 0), new(.62f, .56f, .52f), "f3e8d5");
        Ball(new(0, .32f, .2f), new(.36f, .26f, .16f), "fff6e6");
        Ball(new(0, .66f, .2f), new(.4f, .1f, .2f), "f2c14e");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .2f, 1.0f, 0), new(.11f, .42f, .08f), "f3e8d5").RotateZ(side * -.25f);
            Ball(new(side * .2f, 1.0f, .03f), new(.05f, .3f, .03f), "eab4ad").RotateZ(side * -.25f);
            Foot(new(side * .17f, .18f, .02f), .2f, .12f, "e0d0b8", side);
        }
        Art.Cylinder(model, new(0, .86f, 0), .24f, .27f, .16f, "d9432f");
        Art.Cylinder(model, new(0, .77f, 0), .29f, .29f, .05f, "fff5df");
        Art.Block(model, new(0, .76f, .26f), new(.36f, .03f, .15f), "b8321f");
        Ball(new(0, .96f, 0), Vector3.One * .06f, "f2c14e");
        Art.Cylinder(model, new(.36f, .62f, .12f), .015f, .015f, .56f, "a77a59");
        Art.Cylinder(model, new(.36f, .92f, .12f), .12f, .12f, .03f, "d9432f").RotationDegrees = new(90, 0, 0);
        Eyes(.11f, .52f, .245f);
        Smile(.4f, .262f, .08f);
    }

    private void BuildPandaCub()
    {
        // A round baby panda: a white body with black arms and legs, a big head with black ears and eye patches and a
        // small bamboo stalk with a leaf in the paws.
        Ball(new(0, .46f, 0), new(.66f, .6f, .56f), "f8f8f3");
        Ball(new(0, .62f, .02f), new(.7f, .16f, .5f), "23252b");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .31f, .52f, .24f), new(.17f, .26f, .2f), "23252b");
            Ball(new(side * .3f, .98f, -.04f), new(.2f, .2f, .12f), "23252b");
            Foot(new(side * .2f, .2f, .04f), .22f, .14f, "23252b", side);
        }
        Ball(new(0, .84f, .08f), new(.62f, .52f, .5f), "f8f8f3");
        foreach (int side in new[] { -1, 1 })
        {
            var patch = Ball(new(side * .13f, .86f, .31f), new(.14f, .1f, .04f), "23252b");
            patch.RotateZ(side * .5f);
        }
        Ball(new(0, .76f, .33f), new(.12f, .08f, .06f), "f1ecdd");
        Ball(new(0, .8f, .365f), new(.06f, .04f, .03f), "23252b");
        Art.Cylinder(model, new(.34f, .52f, .42f), .03f, .03f, .5f, "6dbb5f").RotationDegrees = new(-20, 0, -15);
        Ball(new(.4f, .78f, .44f), new(.16f, .04f, .08f), "7ccf7a");
        Eyes(.13f, .86f, .31f);
        Smile(.72f, .34f, .06f);
    }

    private void BuildMill()
    {
        // A tiny windmill: a round tower, a cap and four sails on its front.
        Art.Cylinder(model, new(0, .46f, 0), .3f, .4f, .8f, "e8dcc0");
        Art.Cylinder(model, new(0, 1.0f, 0), .36f, .0f, .34f, "b5533b");
        var sails = new Node3D { Name = "Sails", Position = new(0, .88f, .34f) };
        model.AddChild(sails);
        Art.Ball(sails, Vector3.Zero, Vector3.One * .1f, "5a3f2a");
        for (int i = 0; i < 4; i++)
        {
            var arm = new Node3D { Rotation = new(0, 0, i * Mathf.Pi / 2 + .5f) };
            sails.AddChild(arm);
            Art.Block(arm, new(0, .26f, 0), new(.05f, .52f, .02f), "8d6a46");
            Art.Block(arm, new(.07f, .3f, 0), new(.12f, .34f, .015f), "fff1d2");
        }
        foreach (int side in new[] { -1, 1 })
            Foot(new(side * .17f, .18f, 0), .18f, .13f, "8d6a46", side);
        Eyes(.1f, .62f, .31f);
        Smile(.5f, .335f, .07f);
    }

    private void BuildTower()
    {
        // A little lookout tower with a platform, a flag and a face on the shaft.
        Art.Block(model, new(0, .55f, 0), new(.5f, .86f, .5f), "d9d2c4");
        Art.Block(model, new(0, 1.08f, 0), new(.74f, .12f, .74f), "a3a39c");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Block(model, new(side * .33f, 1.2f, 0), new(.08f, .16f, .74f), "8f8a7d");
            Art.Block(model, new(0, 1.2f, side * .33f), new(.74f, .16f, .08f), "8f8a7d");
            Foot(new(side * .14f, .2f, 0), .2f, .13f, "77756f", side);
        }
        Art.Cylinder(model, new(0, 1.55f, 0), .02f, .02f, .66f, "ffd35d");
        Art.Block(model, new(.14f, 1.74f, 0), new(.26f, .16f, .02f), "63bfa5");
        Eyes(.1f, .66f, .255f);
        Smile(.5f, .252f, .07f);
    }
}
