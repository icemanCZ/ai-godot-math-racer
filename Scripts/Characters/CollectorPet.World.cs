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
/// hauled), each works (twenty pieces made in all, however many of them were taken out of the stock since) and each
/// building (its completion; the jetty's is the duck).
/// </summary>
internal static class WorldPetCatalog
{
    /// <summary>The loads of a raw material that earn its pet.</summary>
    public const int SourceLoads = 10;

    /// <summary>The pieces a works has to make in all for its pet. The stock holds only <see cref="Works.Capacity"/>.</summary>
    public const int WorksPieces = 20;

    /// <summary>The loads a works takes in for its pet, which at <see cref="Works.PerLoad"/> pieces a load is twenty pieces.</summary>
    public const int WorksLoads = WorksPieces / Works.PerLoad;

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

    public static readonly WorldPetSpec Fawn = new(CollectorPetKind.Fawn, "Koloušek Puntík", "8 chycených zvířátek");

    public static readonly WorldPetSpec Pigeon = new(
        CollectorPetKind.Pigeon,
        "Holoubek Psaníčko",
        "známka od každého strážce hranic"
    );

    public static readonly WorldPetSpec Gull = new(CollectorPetKind.Gull, "Racek Vlnka", "postavený maják");

    public static readonly WorldPetSpec Dragonling = new(
        CollectorPetKind.Dragonling,
        "Dráček Jiskřička",
        "vylíhnuté dračí vejce"
    );

    public static readonly WorldPetSpec Rooster = new(
        CollectorPetKind.Rooster,
        "Kohoutek Kokrháček",
        $"{BalloonPlans.RoosterStars} hvězdiček z balónu"
    );

    public static readonly WorldPetSpec Squirrel = new(
        CollectorPetKind.Squirrel,
        "Veverka Oříška",
        $"{TrackingPlans.Animals} vystopovaných zvířátek"
    );

    public static readonly WorldPetSpec Sheepdog = new(
        CollectorPetKind.Sheepdog,
        "Ovčácký pejsek Alík",
        $"{SheepPlans.Sheep} oveček doma v ohradě"
    );

    public static readonly WorldPetSpec Otter = new(
        CollectorPetKind.Otter,
        "Vydra Šplouchalka",
        $"zlatá medaile ve všech {RallyPlans.Stages} etapách rallye"
    );

    public static readonly WorldPetSpec Owlet = new(
        CollectorPetKind.Owlet,
        "Sovička Houkalka",
        $"všech {ExplorerPlans.Landmarks} divů krajiny v mapě"
    );

    public static readonly WorldPetSpec Crab = new(
        CollectorPetKind.Crab,
        "Krab Klepetko",
        $"mušle všech {ShellPlans.Kinds} druhů z pláže"
    );

    public static readonly WorldPetSpec Kingfisher = new(
        CollectorPetKind.Kingfisher,
        "Ledňáček Modroušek",
        $"všech {FerryPlans.Kinds} druhů ptáků v atlasu z ostrůvku"
    );

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
            case CollectorPetKind.Fawn:
                BuildFawn();
                break;
            case CollectorPetKind.Pigeon:
                BuildPigeon();
                break;
            case CollectorPetKind.Gull:
                BuildGull();
                break;
            case CollectorPetKind.Dragonling:
                BuildDragonling();
                break;
            case CollectorPetKind.Rooster:
                BuildRooster();
                break;
            case CollectorPetKind.Squirrel:
                BuildSquirrel();
                break;
            case CollectorPetKind.Sheepdog:
                BuildSheepdog();
                break;
            case CollectorPetKind.Otter:
                BuildOtter();
                break;
            case CollectorPetKind.Owlet:
                BuildOwlet();
                break;
            case CollectorPetKind.Crab:
                BuildCrab();
                break;
            case CollectorPetKind.Kingfisher:
                BuildKingfisher();
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

    private void BuildFawn()
    {
        // A little fawn: a warm brown body with white spots, long legs, a big head with large ears, a white muzzle and a
        // short white tail.
        Ball(new(0, .62f, -.05f), new(.44f, .38f, .66f), "c98a52");
        foreach (
            var (x, y, z) in new[] { (.14f, .76f, .1f), (-.12f, .78f, -.08f), (.1f, .74f, -.24f), (-.15f, .72f, .14f) }
        )
            Ball(new(x, y, z), new(.07f, .04f, .07f), "fff6e6");
        foreach (int side in new[] { -1, 1 })
        {
            Foot(new(side * .14f, .46f, .2f), .44f, .09f, "b07443", side);
            Foot(new(side * .14f, .46f, -.28f), .44f, .09f, "b07443", -side);
            var ear = Ball(new(side * .2f, 1.18f, .22f), new(.1f, .24f, .06f), "c98a52");
            ear.RotateZ(side * -.75f);
            Ball(new(side * .2f, 1.18f, .245f), new(.05f, .16f, .02f), "f2c4b8").RotateZ(side * -.75f);
        }
        Ball(new(0, .9f, .24f), new(.2f, .3f, .2f), "c98a52");
        Ball(new(0, 1.06f, .3f), new(.4f, .36f, .38f), "c98a52");
        Ball(new(0, .98f, .48f), new(.2f, .14f, .14f), "fff6e6");
        Ball(new(0, 1.01f, .55f), new(.07f, .05f, .04f), "34404a");
        Ball(new(0, .7f, -.4f), new(.1f, .12f, .08f), "fff6e6");
        Eyes(.09f, 1.11f, .47f);
        Smile(.94f, .545f, .05f);
    }

    private void BuildPigeon()
    {
        // A grey-blue carrier pigeon with a shimmering green and purple neck, dark bars on its wings, pink feet and a tiny
        // letter with a red stamp held in its pale beak.
        Ball(new(0, .5f, -.04f), new(.62f, .56f, .84f), "8f9bb0");
        Ball(new(0, .5f, -.5f), new(.36f, .12f, .38f), "6f7a8c");
        Ball(new(0, .74f, .2f), new(.42f, .3f, .38f), "8a5f9a");
        Ball(new(0, .82f, .2f), new(.4f, .3f, .36f), "4f9a7a");
        Ball(new(0, .98f, .26f), new(.36f, .34f, .36f), "9aa6ba");
        Ball(new(0, .95f, .47f), new(.1f, .07f, .14f), "e8d8c0");
        Ball(new(0, 1.0f, .41f), new(.08f, .04f, .05f), "f4f0f6");
        Art.Block(model, new(0, .9f, .58f), new(.26f, .16f, .02f), "fbf6ea");
        Art.Block(model, new(.07f, .93f, .595f), new(.07f, .06f, .01f), "d9432f");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .3f, .55f, -.06f), new(.16f, .32f, .6f), "7d899e").RotateZ(side * .35f);
            foreach (float z in new[] { -.08f, -.24f })
                Ball(new(side * .37f, .55f, z), new(.05f, .2f, .06f), "3b4250").RotateZ(side * .35f);
            Foot(new(side * .12f, .26f, .02f), .26f, .1f, "d9606a", side);
        }
        Eyes(.11f, 1.02f, .4f);
    }

    private void BuildGull()
    {
        // A white gull with grey wings tipped in black with white spots, and a yellow beak with a red dot near its tip.
        Ball(new(0, .52f, -.04f), new(.6f, .56f, .9f), "f7f7f4");
        Ball(new(0, .56f, -.55f), new(.3f, .12f, .36f), "f7f7f4");
        Ball(new(0, .98f, .22f), new(.4f, .38f, .4f), "fbfbf8");
        Ball(new(0, .93f, .48f), new(.1f, .08f, .26f), "f2c14e");
        Ball(new(0, .9f, .55f), Vector3.One * .05f, "d9432f");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .3f, .6f, -.1f), new(.16f, .3f, .72f), "a9b3bd").RotateZ(side * .3f);
            Ball(new(side * .28f, .62f, -.5f), new(.12f, .16f, .28f), "23252b").RotateZ(side * .3f);
            Ball(new(side * .3f, .66f, -.56f), Vector3.One * .05f, "fbfbf8");
            Foot(new(side * .13f, .27f, .02f), .27f, .1f, "f2a65a", side);
        }
        Eyes(.12f, 1.04f, .38f);
    }

    private void BuildDragonling()
    {
        // A little green dragon with a yellow belly, orange spikes, two stubby horns, small wings and a curled tail.
        Ball(new(0, .5f, -.02f), new(.42f, .4f, .5f), "5fae5a");
        Ball(new(0, .46f, .2f), new(.3f, .3f, .3f), "f4cf7e");
        Ball(new(0, .96f, .2f), new(.36f, .32f, .36f), "6dbb63");
        Ball(new(0, .86f, .48f), new(.2f, .14f, .16f), "77c46c");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .06f, .9f, .62f), Vector3.One * .025f, "2c3a2a");
            Art.Cylinder(model, new(side * .14f, 1.28f, .12f), .05f, .01f, .2f, "fff2d0").RotationDegrees = new(
                -20,
                0,
                side * -18
            );
            var wing = Ball(new(side * .36f, .68f, -.12f), new(.04f, .2f, .26f), "f5a04a");
            wing.Rotation = new(.3f, 0, side * -.5f);
            Foot(new(side * .15f, .2f, .06f), .2f, .11f, "3f8a4a", side);
        }
        for (int i = 0; i < 3; i++)
            Ball(new(0, .9f - i * .14f, -.32f - i * .05f), Vector3.One * (.06f - i * .01f), "f08a3c");
        for (int i = 0; i < 4; i++)
            Ball(
                new(Mathf.Sin(i * .8f) * .18f, .3f + i * .03f, -.5f - i * .14f),
                Vector3.One * (.13f - i * .025f),
                "5fae5a"
            );
        Eyes(.11f, 1.02f, .45f);
        Smile(.86f, .62f, .07f);
    }

    private void BuildRooster()
    {
        // A little rooster, one of the first passengers of a balloon: a red comb and wattle, a yellow beak, a brown and
        // golden body, a tail of green and blue feathers, and a tiny rainbow balloon tied to his back.
        Ball(new(0, .55f, -.02f), new(.4f, .4f, .48f), "b5562e");
        Ball(new(0, .5f, .16f), new(.3f, .3f, .28f), "e2a447");
        Ball(new(0, .98f, .2f), new(.28f, .3f, .28f), "e9b85a");
        Ball(new(0, .95f, .46f), new(.08f, .06f, .14f), "f2c14e").RotationDegrees = new(10, 0, 0);
        Ball(new(0, .82f, .42f), new(.06f, .1f, .05f), "d9322a");
        for (int i = 0; i < 3; i++)
            Ball(new(0, 1.25f + (i == 1 ? .05f : 0), .08f + i * .1f), new(.05f, .1f, .06f), "e02e2a");
        for (int i = 0; i < 4; i++)
        {
            var feather = Ball(
                new(0, .78f + i * .06f, -.4f - i * .04f),
                new(.06f, .34f - i * .04f, .1f),
                i % 2 == 0 ? "2f7a55" : "2e5aa8"
            );
            feather.RotationDegrees = new(-30 - i * 14, 0, 0);
        }
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .3f, .58f, -.04f), new(.1f, .24f, .34f), "8f3f22").RotationDegrees = new(0, 0, side * 12);
            Foot(new(side * .13f, .26f, .04f), .26f, .08f, "f2a65a", side);
        }
        Art.Cylinder(model, new(0, 1.35f, -.2f), .008f, .008f, .9f, "f4f1e6");
        Ball(new(0, 1.95f, -.2f), new(.24f, .28f, .24f), "e8453c");
        Ball(new(0, 1.95f, -.2f), new(.245f, .2f, .12f), "f6d03f");
        Ball(new(0, 1.95f, -.2f), new(.12f, .21f, .25f), "3f8fd8");
        Art.Block(model, new(0, 1.62f, -.2f), new(.1f, .08f, .1f), "b07a43");
        Eyes(.1f, 1.04f, .4f);
    }

    private void BuildSquirrel()
    {
        // A little red squirrel of the forest: a round body, a pale belly, tufted ears, a hazelnut held in her paws and a
        // big bushy tail curled up over her back.
        Ball(new(0, .5f, 0), new(.4f, .5f, .38f), "c65f2a");
        Ball(new(0, .48f, .14f), new(.26f, .34f, .14f), "f3e2c4");
        Ball(new(0, .98f, .08f), new(.36f, .34f, .34f), "c65f2a");
        Ball(new(0, .92f, .27f), new(.16f, .12f, .12f), "f3e2c4");
        Ball(new(0, .95f, .33f), new(.05f, .04f, .03f), "3b2418");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .12f, 1.2f, .04f), new(.08f, .16f, .05f), "b4521f").RotationDegrees = new(0, 0, -side * 12);
            Ball(new(side * .13f, 1.32f, .03f), new(.03f, .08f, .03f), "7a3214");
            Ball(new(side * .1f, .62f, .24f), new(.08f, .08f, .1f), "b4521f");
            Foot(new(side * .12f, .2f, .06f), .18f, .1f, "b4521f", side);
        }
        Ball(new(0, .66f, .3f), new(.1f, .12f, .1f), "9a6a35");
        Ball(new(0, .72f, .3f), new(.08f, .04f, .08f), "6e4520");
        for (int i = 0; i < 4; i++)
        {
            float a = i * .55f;
            Ball(
                new(0, .45f + Mathf.Sin(a) * .62f, -.28f - Mathf.Cos(a) * .3f + i * .04f),
                new(.22f - i * .015f, .26f, .22f - i * .015f),
                i % 2 == 0 ? "d06a32" : "c65f2a"
            );
        }
        Eyes(.08f, 1.02f, .26f);
    }

    private void BuildSheepdog()
    {
        // A fluffy black and white sheepdog puppy sitting up: a white chest and blaze, floppy ears, a pink tongue, a
        // little red neckerchief and a bushy tail with a white tip curled up beside him.
        Ball(new(0, .44f, -.06f), new(.46f, .5f, .52f), "2f2a2a");
        Ball(new(0, .5f, .16f), new(.3f, .4f, .18f), "fbf6ec");
        Ball(new(0, .96f, .06f), new(.44f, .4f, .4f), "2f2a2a");
        Ball(new(0, .9f, .22f), new(.16f, .3f, .14f), "fbf6ec");
        Ball(new(0, .84f, .3f), new(.22f, .16f, .16f), "fbf6ec");
        Ball(new(0, .88f, .39f), new(.08f, .06f, .05f), "1d1a1a");
        Ball(new(0, .77f, .35f), new(.06f, .07f, .03f), "f08a8a");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .2f, 1.02f, .02f), new(.12f, .24f, .08f), "2f2a2a").RotationDegrees = new(0, 0, side * 28);
            Ball(new(side * .12f, .64f, .22f), new(.1f, .1f, .1f), "c0392b");
            Foot(new(side * .14f, .22f, .12f), .22f, .11f, "fbf6ec", side);
            Ball(new(side * .2f, .18f, -.12f), new(.16f, .14f, .26f), "2f2a2a");
        }
        Ball(new(0, .66f, .24f), new(.1f, .1f, .06f), "c0392b");
        for (int i = 0; i < 3; i++)
            Ball(
                new(.24f + i * .04f, .26f + i * .14f, -.34f - i * .04f),
                new(.14f - i * .02f, .16f, .14f - i * .02f),
                i == 2 ? "fbf6ec" : "2f2a2a"
            );
        Eyes(.1f, .98f, .25f);
    }

    private void BuildOtter()
    {
        // A sleek brown otter of the border river sitting up on her tail: a cream face and chest, small round ears,
        // whiskers, a little fish held in her paws and a long tapering tail curled round in front of her feet.
        Ball(new(0, .46f, -.04f), new(.4f, .58f, .38f), "7a5032");
        Ball(new(0, .5f, .12f), new(.28f, .42f, .16f), "e9d6b4");
        Ball(new(0, .98f, .04f), new(.36f, .32f, .32f), "7a5032");
        Ball(new(0, .92f, .19f), new(.24f, .18f, .16f), "e9d6b4");
        Ball(new(0, .96f, .27f), new(.07f, .05f, .04f), "2b1d14");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .15f, 1.12f, .0f), new(.08f, .08f, .05f), "5e3c24");
            for (int w = 0; w < 2; w++)
                Art.Block(
                    model,
                    new(side * .13f, .9f - w * .03f, .28f),
                    new(.12f, .008f, .008f),
                    "f6efe2"
                ).RotationDegrees = new(0, 0, side * (8 - w * 16));
            Ball(new(side * .1f, .66f, .2f), new(.08f, .1f, .1f), "6b4529");
            Foot(new(side * .12f, .18f, .1f), .16f, .12f, "5e3c24", side);
        }
        // The fish: a silver body with a blue back and a forked tail.
        Ball(new(0, .7f, .27f), new(.24f, .07f, .06f), "b9c9d4");
        Ball(new(0, .72f, .27f), new(.2f, .03f, .05f), "5b84a8");
        Ball(new(.14f, .7f, .27f), new(.05f, .08f, .02f), "5b84a8");
        for (int i = 0; i < 5; i++)
        {
            // From behind her round to the front, beside her feet.
            float a = Mathf.Pi - i * .6f;
            Ball(
                new(Mathf.Sin(a) * .32f, .1f, Mathf.Cos(a) * .32f - .02f),
                new(.16f - i * .022f, .12f - i * .014f, .16f - i * .022f),
                "6b4529"
            );
        }
        Eyes(.08f, 1.0f, .2f);
    }

    private void BuildOwlet()
    {
        // A round fluffy little owl: a speckled body that is also the head, two ear tufts, big eyes in a pale face, a
        // tiny beak, wings folded at the sides and yellow feet; on her head a little map rolled up like a hat.
        Ball(new(0, .55f, 0), new(.62f, .78f, .56f), "9a7652");
        Ball(new(0, .48f, .17f), new(.42f, .5f, .26f), "eedcbc");
        Ball(new(0, .86f, .16f), new(.5f, .34f, .22f), "f3e6cc");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .18f, 1.06f, .02f), new(.1f, .24f, .08f), "7d5c3e").RotationDegrees = new(0, 0, -side * 22);
            Art.Ball(model, new(side * .11f, .88f, .27f), new(.075f, .08f, .03f), "fffdf5");
            Art.Ball(model, new(side * .11f, .88f, .295f), new(.045f, .05f, .02f), "2a2522");
            Art.Ball(model, new(side * .095f, .9f, .31f), Vector3.One * .013f, "ffffff");
            Ball(new(side * .3f, .52f, -.02f), new(.16f, .5f, .36f), "7d5c3e");
            Foot(new(side * .1f, .18f, .08f), .14f, .1f, "e8b938", side);
        }
        Ball(new(0, .8f, .3f), new(.06f, .09f, .06f), "e8a33a");
        foreach (var (x, y) in new[] { (-.08f, .55f), (.08f, .55f), (0, .42f), (-.1f, .32f), (.1f, .32f) })
            Art.Block(model, new(x, y, .3f), new(.05f, .02f, .01f), "7d5c3e");
        Art.Log(model, new(0, 1.12f, 0), Vector3.Right, .05f, .3f, "f3e6c4");
    }

    private void BuildCrab()
    {
        // A cheerful red crab of the beach: a wide shell, eyes on stalks, two big claws held up and four legs a side.
        Ball(new(0, .3f, 0), new(.7f, .32f, .52f), "e0532f");
        Ball(new(0, .36f, .04f), new(.5f, .2f, .4f), "ee6d42");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Cylinder(model, new(side * .1f, .5f, .2f), .02f, .02f, .2f, "e0532f");
            Art.Ball(model, new(side * .1f, .62f, .2f), Vector3.One * .05f, "fffdf5");
            Art.Ball(model, new(side * .1f, .63f, .235f), Vector3.One * .028f, "2a2522");
            var arm = Art.Block(model, new(side * .36f, .38f, .22f), new(.2f, .06f, .06f), "d24a28");
            arm.RotationDegrees = new Vector3(0, -side * 30, side * 30);
            Ball(new(side * .46f, .52f, .3f), new(.2f, .18f, .14f), "e0532f");
            Ball(new(side * .52f, .6f, .33f), new(.1f, .1f, .08f), "d24a28").RotationDegrees = new(0, 0, side * 30);
            for (int leg = 0; leg < 4; leg++)
            {
                var piece = Art.Block(model, new(side * .38f, .16f, .08f - leg * .1f), new(.26f, .04f, .04f), "c9432a");
                piece.RotationDegrees = new Vector3(0, side * (leg * 12 - 15), -side * 35);
            }
        }
        Art.Block(model, new(0, .26f, .265f), new(.14f, .02f, .01f), "8e2a18");
    }

    private void BuildKingfisher()
    {
        // A small, round kingfisher: a shining blue back and head, an orange breast, a white throat, a long dark beak,
        // little red feet and a short tail; on a little stick, as it sits on a branch over the water.
        Ball(new(0, .55f, 0), new(.46f, .5f, .62f), "2a86c9");
        Ball(new(0, .5f, .12f), new(.36f, .4f, .4f), "f08a3a");
        Ball(new(0, .9f, .1f), new(.4f, .36f, .4f), "2f95d8");
        Ball(new(0, .8f, .26f), new(.18f, .1f, .12f), "fffdf5");
        foreach (int side in new[] { -1, 1 })
        {
            Ball(new(side * .2f, .55f, -.04f), new(.12f, .34f, .4f), "1f6fa8");
            Art.Ball(model, new(side * .12f, .95f, .24f), new(.06f, .06f, .03f), "2a2522");
            Art.Ball(model, new(side * .11f, .97f, .255f), Vector3.One * .015f, "ffffff");
            Ball(new(side * .17f, .9f, .2f), new(.06f, .05f, .05f), "f08a3a");
            Foot(new(side * .07f, .18f, .06f), .1f, .08f, "d84a3a", side);
        }
        Art.Block(model, new(0, .9f, .48f), new(.06f, .06f, .36f), "2f2a26");
        Ball(new(0, .42f, -.36f), new(.14f, .08f, .24f), "1f6fa8");
        Art.Log(model, new(0, .12f, 0), Vector3.Right, .05f, .7f, "7a5536");
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
