using System;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>What a works makes from one hauled load. Values index <see cref="WorksCatalog.All"/>.</summary>
public enum Product
{
    Planks = 0,
    Bricks = 1,
    Gravel = 2,

    /// <summary>The glassworks, which stands apart from the original three: it makes glasses from sand.</summary>
    Glass = 3,

    /// <summary>The bakery, the fifth works: it bakes bread from the flour that the mill grinds from the farmer's wheat.</summary>
    Bread = 4,
}

/// <summary>One kind of works: what it takes in, what it stores and who runs it.</summary>
internal sealed record WorksSpec(
    Product Product,
    HaulLoad Input,
    string Name,
    string Sign,
    string Goods,
    string Worker,
    string Greeting,
    int Kind,
    string Fur,
    string Hat
)
{
    /// <summary>The works' own task-marker icon: a saw blade, a brick wall, a hopper of gravel, a glass or a loaf.</summary>
    public string Icon =>
        Product switch
        {
            Product.Planks => "sawmill",
            Product.Bricks => "brickworks",
            Product.Gravel => "gravelworks",
            Product.Bread => "bakery",
            _ => "glassworks",
        };
}

internal static class WorksCatalog
{
    /// <summary>The works of the first valleys: the sawmill, the brickworks and the gravel works (IDs 45–47, stock of the save).</summary>
    public const int Count = 3;

    /// <summary>The glassworks follows them, has an actor ID and a stock of its own and is placed after everything else.</summary>
    public const int GlassIndex = 3;

    /// <summary>The bakery is the fifth works, planned after the farmer's field and the glassworks.</summary>
    public const int BreadIndex = 4;

    public const int Total = 5;

    /// <summary>Indexed by <see cref="Product"/>: the sawmill, the brickworks, the gravel works, the glassworks and the bakery.</summary>
    public static readonly WorksSpec[] All =
    {
        new(
            Product.Planks,
            HaulLoad.Logs,
            "Pila",
            "PILA",
            "Prkna",
            "Medvěd Pilař",
            "Přivez mi klády z lesa, nařežu z nich prkna.",
            2,
            "8a5f3f",
            "d9423a"
        ),
        new(
            Product.Bricks,
            HaulLoad.Soil,
            "Cihelna",
            "CIHELNA",
            "Cihly",
            "Liška Cihlářka",
            "Přivez mi hlínu, vypálím z ní cihly.",
            1,
            "d9824a",
            "e0d24a"
        ),
        new(
            Product.Gravel,
            HaulLoad.Stones,
            "Štěrkovna",
            "ŠTĚRKOVNA",
            "Štěrk",
            "Zajíc Štěrkař",
            "Přivez mi kamení, nadrtím z něj štěrk.",
            0,
            "d9d4c6",
            "4aa3d9"
        ),
        new(
            Product.Glass,
            HaulLoad.Sand,
            "Sklárna",
            "SKLÁRNA",
            "Skleničky",
            "Králice Sklenářka",
            "Přivez mi písek, vyfouknu z něj skleničky.",
            0,
            "d9cfe6",
            "6bc4d9"
        ),
        new(
            Product.Bread,
            HaulLoad.Flour,
            "Pekárna",
            "PEKÁRNA",
            "Chléb",
            "Králíček pekař Rohlík",
            "Přivez mi mouku ze mlýna, upeču z ní voňavý chléb.",
            0,
            "f3e0c0",
            "fffaf0"
        ),
    };

    public static string ProductIcon(Product product) =>
        product switch
        {
            Product.Planks => "planks",
            Product.Bricks => "bricks",
            Product.Gravel => "gravel",
            Product.Bread => "bread",
            _ => "glass",
        };

    /// <summary>
    /// The works whose spec takes a load: logs go to the sawmill, soil to the brickworks, stones to the gravel works, sand
    /// to the glassworks and flour to the bakery; -1 for any other load (the wheat goes to the mill, see <c>Works.Input</c>).
    /// </summary>
    public static int ForInput(HaulLoad load) => Array.FindIndex(All, spec => spec.Input == load);
}

/// <summary>The worker at a raw-material site, who starts the trip that takes the load to its works.</summary>
internal sealed record SourceSpec(HaulLoad Load, string Name, string Greeting, int Kind, string Fur, string Hat);

internal static class SourceCatalog
{
    public const int Count = 3;

    /// <summary>The quarry, the clay pit and the log yard. The first sand pit has no worker; the second one's is in <see cref="Extra"/>.</summary>
    public static readonly SourceSpec[] All =
    {
        new(HaulLoad.Stones, "Králíček lomař Kvido", "Naložím ti kamení pro štěrkovnu.", 0, "b9c2c9", "8f8f8a"),
        new(HaulLoad.Soil, "Liška hlinařka Hanka", "Naložím ti hlínu pro cihelnu.", 1, "c98a5a", "a05a3a"),
        new(HaulLoad.Logs, "Medvěd dřevorubec Drvoš", "Naložím ti klády pro pilu.", 2, "9a6a46", "3f7f4a"),
    };

    public static int IndexOf(HaulLoad load) => Array.FindIndex(All, spec => spec.Load == load);

    /// <summary>
    /// The farmer at the wheat field, who loads the trailer for the mill. He is not in <see cref="All"/>, whose order and
    /// length give the IDs of the other workers: he has an ID of his own (<c>ActorIds.FieldFarmer</c>).
    /// </summary>
    public static readonly SourceSpec Field = new(
        HaulLoad.Wheat,
        "Králíček sedlák Klásek",
        "Naložím ti obilí pro mlýn. Mlynář z něj semele mouku.",
        0,
        "e9d8b4",
        "d9b84a"
    );

    /// <summary>
    /// The workers at the second site of each raw material, in <see cref="ExtraLoads"/> order. The sand pit's worker
    /// sends the sand to the glassworks, like the others send theirs to their works, and loads the sand for the bridge.
    /// </summary>
    public static readonly SourceSpec[] Extra =
    {
        new(HaulLoad.Stones, "Zajíc lomař Zdeněk", "Tady je druhý lom. Naložím ti kamení.", 0, "c9c4d0", "7f7f7a"),
        new(HaulLoad.Soil, "Liška hlinař Honza", "Tady je druhé hliniště. Naložím ti hlínu.", 1, "d6925f", "b46a3a"),
        new(
            HaulLoad.Sand,
            "Medvěd písečník Pepík",
            "Naložím ti písek pro sklárnu i na stavbu mostu.",
            2,
            "c9a96a",
            "e0c46a"
        ),
        new(
            HaulLoad.Logs,
            "Králík dřevorubec Dušan",
            "Tady je druhá skládka. Naložím ti klády.",
            0,
            "b89a76",
            "3f7f4a"
        ),
    };

    /// <summary>The raw loads that have a second site, in the order of <see cref="Extra"/>.</summary>
    public static readonly HaulLoad[] ExtraLoads = { HaulLoad.Stones, HaulLoad.Soil, HaulLoad.Sand, HaulLoad.Logs };

    public static int ExtraIndexOf(HaulLoad load) => Array.IndexOf(ExtraLoads, load);

    /// <summary>The task-marker icon of a raw-material site; the first sand pit has one too, though it has no worker.</summary>
    public static string IconOf(HaulLoad load) =>
        load switch
        {
            HaulLoad.Stones => "quarry",
            HaulLoad.Soil => "clay",
            HaulLoad.Sand => "sand",
            HaulLoad.Wheat => "wheat",
            _ => "logs",
        };
}

/// <summary>
/// Where a works stands: <see cref="Position"/> is the unloading ring, <see cref="Road"/> the road point its field
/// path starts from and <see cref="Inward"/> the direction away from the road, in which the buildings stand.
/// </summary>
internal sealed record WorksSite(int Index, Vector3 Position, Vector3 Road, Vector3 Inward, Vector3 WorkerSpot);

/// <summary>
/// The sawmill, brickworks, gravel works, glassworks or bakery. Every load tipped on its ring is turned into
/// <see cref="PerLoad"/> units of goods. The stock stands on the yard beside the ring, one stack or heap
/// per unit, so the player can see how much is stored. Local +Z points away from the road.
/// </summary>
public partial class Works : Node3D
{
    public const int Capacity = 12;
    public const int PerLoad = 2;

    /// <summary>Units of goods one building trip takes from the stock.</summary>
    public const int PerTrip = 1;

    /// <summary>Places on the stock yard; the second layer of units stands on top of the first.</summary>
    private const int YardPlaces = 6;

    private const float BuildingZ = 12;
    private const float YardX = 7.6f;
    private const float YardZ = 3.8f;
    private const float RawX = -8.8f;
    private const float RawZ = 9;

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D[] slots = new Node3D[Capacity];
    private readonly float[] grown = new float[Capacity];
    private readonly float[] delay = new float[Capacity];
    private Node3D? spinner;
    private CpuParticles3D? exhaust;
    private float working;

    internal WorksSite Site { get; }
    internal WorksSpec Spec => WorksCatalog.All[Site.Index];

    /// <summary>
    /// What is hauled to this works: its <see cref="WorksSpec.Input"/>, except at the bakery of a valley without the mill,
    /// which takes the farmer's wheat itself because nobody grinds it into flour.
    /// </summary>
    internal HaulLoad Input { get; set; }

    internal Character Worker { get; set; } = null!;
    public int Stock { get; private set; }
    public bool Full => Stock >= Capacity;

    /// <summary>The stacks, bundles or heaps shown on the yard; always equals <see cref="Stock"/>.</summary>
    internal int ShownUnits => slots.Count(slot => slot is { Visible: true });

    internal bool Working => working > 0;

    internal Works(WorksSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Input = Spec.Input;
        Name = Spec.Product + "Works";
        Position = site.Position;
        float yaw = Mathf.Atan2(site.Inward.X, site.Inward.Z);
        Rotation = new Vector3(0, yaw, 0);
        basis = new Basis(Vector3.Up, yaw);
    }

    /// <summary>Tips one load into the works: the stock grows and the machinery runs for a while.</summary>
    internal void Deliver()
    {
        Show(Math.Min(Capacity, Stock + PerLoad), animate: true);
        working = 5;
    }

    /// <summary>Takes goods out of the stock for a building; false when there are not enough.</summary>
    internal bool Take(int amount)
    {
        if (amount <= 0 || amount > Stock)
            return false;
        Show(Stock - amount, animate: false);
        return true;
    }

    internal void SetStock(int stock) => Show(Math.Clamp(stock, 0, Capacity), animate: false);

    private void Show(int stock, bool animate)
    {
        int before = Stock;
        Stock = stock;
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null)
                continue;
            slots[i].Visible = i < Stock;
            if (i >= Stock)
                grown[i] = 0;
            else if (animate && i >= before)
            {
                grown[i] = 0;
                delay[i] = (i - before) * .45f;
                slots[i].Scale = Vector3.One * .001f;
            }
            else
            {
                grown[i] = 1;
                slots[i].Scale = Vector3.One;
            }
        }
    }

    /// <summary>Height of the ground below a point given in the works' own frame, relative to the ring.</summary>
    private float Floor(float x, float z)
    {
        var global = Position + basis * new Vector3(x, 0, z);
        return ground.Height(global.X, global.Z) - Position.Y;
    }

    /// <summary>A thick flat slab that levels a corner of the site; returns the height of its top.</summary>
    private float Pad(float x, float z, float width, float depth, string color)
    {
        float top = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
            top = Mathf.Max(top, Floor(x + dx * width, z + dz * depth));
        top += .05f;
        Art.Block(this, new(x, top - 2, z), new(width, 4, depth), color);
        return top;
    }

    public override void _Ready()
    {
        var building = Pad(0, BuildingZ, 9, 7.4f, "8f8f8a");
        switch (Site.Index)
        {
            case (int)Product.Planks:
                BuildSawmill(building);
                break;
            case (int)Product.Bricks:
                BuildBrickworks(building);
                break;
            case (int)Product.Glass:
                BuildGlassworks(building);
                break;
            case (int)Product.Bread:
                BuildBakery(building);
                break;
            default:
                BuildGravelWorks(building);
                break;
        }
        BuildYard();
        BuildRawPile();
        var post = new Vector3(4.6f, Floor(4.6f, 1.2f), 1.2f);
        Art.Cylinder(this, post + Vector3.Up * 1.5f, .12f, .12f, 3, "a77a59");
        Obstacles.Trunk(this, post, .12f, 3);
        Art.Sign(this, post + Vector3.Up * 3.3f, Spec.Sign, 36);
        Show(Stock, animate: false);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (working > 0)
            working = Mathf.Max(0, working - dt);
        if (spinner != null && working > 0)
        {
            if (Site.Index == (int)Product.Planks)
                spinner.RotateZ(dt * 16);
            else
                spinner.RotateX(dt * 5);
        }
        if (exhaust != null)
            exhaust.Emitting = working > 0;
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] is not { Visible: true } slot || grown[i] >= 1)
                continue;
            if (delay[i] > 0)
            {
                delay[i] = Mathf.Max(0, delay[i] - dt);
                slot.Scale = Vector3.One * .001f;
                continue;
            }
            grown[i] = Mathf.Min(1, grown[i] + dt * 2.5f);
            slot.Scale = Vector3.One * Mathf.Max(.001f, Mathf.SmoothStep(0, 1, grown[i]));
        }
    }

    // ---- The three kinds of buildings ----

    private void Roof(Vector3 center, float width, float depth, string color, string ridge)
    {
        const float angle = .38f;
        float half = width / 2;
        float length = half / Mathf.Cos(angle) + .3f;
        foreach (int side in new[] { -1, 1 })
        {
            var panel = Art.Wall(
                this,
                new(center.X + side * half / 2, center.Y + half / 2 * Mathf.Tan(angle) + .1f, center.Z),
                new(length, .16f, depth),
                color,
                FacadeStyle.Shingles
            );
            panel.RotationDegrees = new Vector3(0, 0, -side * Mathf.RadToDeg(angle));
        }
        Art.Block(this, center + new Vector3(0, half * Mathf.Tan(angle) + .22f, 0), new(.34f, .2f, depth + .1f), ridge);
    }

    private CpuParticles3D Emitter(Vector3 position, Color color, float rise, float lifetime = 1.6f)
    {
        var particles = new CpuParticles3D
        {
            Name = "Exhaust",
            Emitting = false,
            Amount = 24,
            Lifetime = lifetime,
            Position = position,
            Mesh = new SphereMesh
            {
                Radius = .2f,
                Height = .4f,
                RadialSegments = 6,
                Rings = 3,
            },
            Direction = new Vector3(0, 1, 0),
            Spread = 25,
            InitialVelocityMin = rise * .6f,
            InitialVelocityMax = rise * 1.2f,
            Gravity = new Vector3(0, .2f, 0),
            ScaleAmountMin = .7f,
            ScaleAmountMax = 1.7f,
            LocalCoords = false,
        };
        particles.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(particles);
        return particles;
    }

    private void BuildSawmill(float y)
    {
        const float z = BuildingZ;
        const string wall = "b99a6a";
        Art.Wall(this, new(0, y + 1.6f, z + 2.95f), new(8, 3.2f, .3f), wall, FacadeStyle.Boards);
        foreach (float x in new[] { -3.85f, 3.85f })
        {
            Art.Wall(this, new(x, y + 1.6f, z), new(.3f, 3.2f, 6), wall, FacadeStyle.Boards);
            Art.Cylinder(this, new(x, y + 1.6f, z - 2.95f), .17f, .17f, 3.2f, "7a4b32");
        }
        Roof(new(0, y + 3.2f, z), 8.8f, 6.6f, "7a4b32", "5c3826");
        Art.Block(this, new(0, y + .45f, z), new(3.6f, .9f, 1.3f), "8d6a46");
        Art.Block(this, new(0, y + .95f, z), new(3.9f, .1f, 1.6f), "c39770");
        spinner = new Node3D { Name = "Blade", Position = new(0, y + 1.75f, z) };
        AddChild(spinner);
        var disc = Art.Cylinder(spinner, Vector3.Zero, 1, 1, .06f, "b9c2c9");
        disc.RotationDegrees = new Vector3(90, 0, 0);
        Art.Cylinder(spinner, Vector3.Zero, .2f, .2f, .1f, "5a646b").RotationDegrees = new Vector3(90, 0, 0);
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.Tau / 10;
            var tooth = Art.Block(
                spinner,
                new(Mathf.Cos(angle) * 1.02f, Mathf.Sin(angle) * 1.02f, 0),
                new(.2f, .2f, .08f),
                "8f9aa2"
            );
            tooth.Rotation = new Vector3(0, 0, angle + .6f);
        }
        Art.Log(this, new(-1.4f, y + 1.25f, z), Vector3.Right, .3f, 2.7f, "8a5f3f");
        Obstacles.Box(this, new(0, y + 1.6f, z), new(8.4f, 3.2f, 6.4f));
        exhaust = Emitter(new(0, y + 1.5f, z - .9f), new Color(.86f, .72f, .5f, .5f), 1.1f, 1.1f);
    }

    private void BuildBrickworks(float y)
    {
        const float z = BuildingZ;
        Art.Wall(this, new(0, y + 1.9f, z + .5f), new(6, 3.8f, 5), "b5533b", FacadeStyle.Bricks);
        Art.Block(this, new(0, y + 3.95f, z + .5f), new(6.5f, .3f, 5.5f), "8a3f2d");
        Art.Block(this, new(0, y + 1.1f, z - 2.02f), new(2.4f, 2.2f, .12f), "3a2a26");
        Art.Block(this, new(0, y + 2.4f, z - 2.02f), new(1.7f, .6f, .12f), "3a2a26");
        Art.Block(this, new(0, y + .7f, z - 2.0f), new(1.3f, .6f, .1f), "ff9a3d");
        foreach (float x in new[] { -2.4f, 2.4f })
            Art.Block(this, new(x, y + 2, z - 2.03f), new(.5f, 3.7f, .12f), "9b4a35");
        Art.Cylinder(this, new(1.8f, y + 6.6f, z + 2.2f), .8f, .55f, 5.4f, "9b4a35");
        Art.Cylinder(this, new(1.8f, y + 9.4f, z + 2.2f), .72f, .72f, .3f, "5a2f25");
        // Loose bricks lie in front of the kiln.
        for (int i = 0; i < 6; i++)
            Art.Block(
                this,
                new(-2.6f + i * .9f, y + .1f, z - 3.5f - (i % 2) * .4f),
                new(.7f, .2f, .35f),
                i % 2 == 0 ? "c4623f" : "b5533b"
            ).RotationDegrees = new Vector3(0, i * 23, 0);
        Obstacles.Box(this, new(0, y + 1.9f, z + .5f), new(6.2f, 3.8f, 5.2f));
        exhaust = Emitter(new(1.8f, y + 9.7f, z + 2.2f), new Color(.55f, .55f, .58f, .55f), 1.4f, 2.4f);
    }

    private void BuildGlassworks(float y)
    {
        const float z = BuildingZ;
        // A pale hall with a glass roof, a furnace glowing orange in front and a tall chimney.
        Art.Wall(this, new(0, y + 1.7f, z + .5f), new(7, 3.4f, 5), "d9d2c4", FacadeStyle.Plaster);
        Art.Block(this, new(0, y + 3.5f, z + .5f), new(7.4f, .3f, 5.4f), "6f8b96");
        for (int i = 0; i < 5; i++)
            Art.Window(this, new(-2.8f + i * 1.4f, y + 3.85f, z + .5f), new(1.2f, .5f, 4.6f), "bfe3ee");
        Art.Block(this, new(0, y + 1.1f, z - 2.02f), new(2.4f, 2.2f, .12f), "3a2a26");
        Art.Block(this, new(0, y + .75f, z - 2.0f), new(1.3f, .7f, .1f), "ff9a3d");
        foreach (float x in new[] { -2.5f, 2.5f })
            Art.Window(this, new(x, y + 1.8f, z - 2.03f), new(1.2f, 1.1f, .1f), "9ed5df");
        Art.Cylinder(this, new(-2.2f, y + 6, z + 2.4f), .6f, .45f, 5.2f, "8f8a7d");
        Art.Cylinder(this, new(-2.2f, y + 8.7f, z + 2.4f), .55f, .55f, .3f, "5a646b");
        // Finished glasses stand on a table at the door.
        Art.Block(this, new(3.4f, y + .5f, z - 3.4f), new(1.6f, .1f, 1), "8d6a46");
        foreach (float x in new[] { 2.8f, 3.4f, 4f })
            Art.Cylinder(this, new(x, y + .75f, z - 3.4f), .13f, .17f, .38f, "cfeaf4");
        Obstacles.Box(this, new(0, y + 1.7f, z + .5f), new(7.2f, 3.4f, 5.2f));
        exhaust = Emitter(new(-2.2f, y + 9, z + 2.4f), new Color(.8f, .85f, .88f, .5f), 1.2f, 2.2f);
    }

    private void BuildBakery(float y)
    {
        const float z = BuildingZ;
        // A cosy bakery: cream walls, a warm red roof, a brick oven with a chimney and a window full of loaves.
        Art.Wall(this, new(0, y + 1.55f, z + .4f), new(6.4f, 3.1f, 5), "f1dfb8", FacadeStyle.Plaster);
        Art.Block(this, new(0, y + .25f, z + .4f), new(6.6f, .5f, 5.2f), "b8895f");
        Roof(new(0, y + 3.1f, z + .4f), 7.2f, 5.8f, "c9553f", "8f3a2a");
        Art.Block(this, new(-1.9f, y + 1.1f, z - 2.12f), new(1.2f, 2.2f, .12f), "8d5a3b");
        Art.Ball(this, new(-1.55f, y + 1.1f, z - 2.2f), Vector3.One * .09f, "f5d36b");
        Art.Block(this, new(1.5f, y + 1.8f, z - 2.12f), new(2.6f, 1.4f, .1f), "fff6e0");
        Art.Window(this, new(1.5f, y + 1.8f, z - 2.17f), new(2.3f, 1.1f, .08f), "8fb8c9");
        Art.Block(this, new(1.5f, y + 1.2f, z - 2.4f), new(2.7f, .12f, .55f), "8d5a3b");
        for (int i = 0; i < 4; i++)
            Art.Ball(
                this,
                new(.55f + i * .62f, y + 1.38f, z - 2.4f),
                new(.24f, .16f, .2f),
                i % 2 == 0 ? "d9923f" : "c27a2c"
            );
        // The oven glows through a little arched door at the side, and its chimney smokes.
        Art.Block(this, new(3.22f, y + 1, z + .4f), new(.1f, 1.2f, 1.5f), "3a2a26");
        Art.Block(this, new(3.28f, y + .8f, z + .4f), new(.1f, .6f, .9f), "ff9a3d");
        Art.Cylinder(this, new(2.2f, y + 5.1f, z + 2.2f), .6f, .5f, 4.4f, "a35a45");
        Art.Cylinder(this, new(2.2f, y + 7.4f, z + 2.2f), .6f, .6f, .3f, "5a2f25");
        // A baker's sign hangs over the door: a golden pretzel is too fiddly, so a loaf on a board.
        Art.Block(this, new(-1.9f, y + 2.7f, z - 2.35f), new(1.3f, .6f, .1f), "8d5a3b");
        Art.Ball(this, new(-1.9f, y + 2.7f, z - 2.45f), new(.45f, .2f, .08f), "d9923f");
        Obstacles.Box(this, new(0, y + 1.55f, z + .4f), new(6.6f, 3.1f, 5.2f));
        exhaust = Emitter(new(2.2f, y + 7.7f, z + 2.2f), new Color(.95f, .93f, .9f, .55f), 1.1f, 2.2f);
    }

    private void BuildGravelWorks(float y)
    {
        const float z = BuildingZ;
        Art.Block(this, new(0, y + 1.1f, z + 1), new(4, 2.2f, 3), "6f7c85");
        Art.Block(this, new(0, y + 2.25f, z + 1), new(4.3f, .15f, 3.3f), "4f5961");
        foreach (float x in new[] { -1.6f, 1.6f })
        foreach (float dz in new[] { -.1f, 2.1f })
            Art.Cylinder(this, new(x, y + 3.9f, z + dz), .13f, .13f, 3.4f, "4f5961");
        Art.Cylinder(this, new(0, y + 5.6f, z + 1), .55f, 1.8f, 1.8f, "7d8a94");
        spinner = new Node3D { Name = "Drum", Position = new(0, y + 1.3f, z - .55f) };
        AddChild(spinner);
        var drum = Art.Cylinder(spinner, Vector3.Zero, .75f, .75f, 3.2f, "5a646b");
        drum.RotationDegrees = new Vector3(0, 0, 90);
        foreach (float angle in new[] { 0f, 120f, 240f })
        {
            var stripe = Art.Block(spinner, Vector3.Zero, new(3.3f, .14f, .3f), "e8d37a");
            stripe.Position = new Vector3(
                0,
                Mathf.Cos(Mathf.DegToRad(angle)) * .76f,
                Mathf.Sin(Mathf.DegToRad(angle)) * .76f
            );
            stripe.RotationDegrees = new Vector3(angle - 90, 0, 0);
        }
        // A conveyor carries the boulders from the raw heap up into the hopper.
        Art.Beam(this, new(RawX + 2.2f, y + .7f, RawZ + 1.2f), new(-.8f, y + 6.5f, z + .8f), .9f, "4f5961");
        foreach (float t in new[] { .35f, .7f })
        {
            var p = new Vector3(RawX + 2.2f, y + .7f, RawZ + 1.2f).Lerp(new(-.8f, y + 6.5f, z + .8f), t);
            Art.Cylinder(this, p + Vector3.Down * (p.Y - y) / 2, .1f, .1f, p.Y - y, "4f5961");
        }
        Art.Beam(this, new(0, y + 1.1f, z - 1.3f), new(4.8f, y + .5f, YardZ + 2), .7f, "4f5961");
        Obstacles.Box(this, new(0, y + 1.2f, z + 1), new(4.2f, 2.4f, 3.2f));
        exhaust = Emitter(new(0, y + 2.6f, z - .6f), new Color(.78f, .74f, .64f, .45f), .9f, 1.2f);
    }

    // ---- The stock yard and the heap of raw material ----

    private void BuildYard()
    {
        string pad = Spec.Product switch
        {
            Product.Planks => "bba87f",
            Product.Bricks => "9d9382",
            Product.Glass => "b9c4c9",
            Product.Bread => "d9c6a0",
            _ => "8e8a7d",
        };
        float width = 3 * 2.3f + .8f;
        float depth = 2 * 2.6f + .8f;
        float cx = YardX + 2.3f;
        float cz = YardZ + 1.3f;
        float y = Pad(cx, cz, width, depth, pad);
        for (int i = 0; i < Capacity; i++)
        {
            int place = i % YardPlaces;
            int layer = i / YardPlaces;
            var slot = new Node3D
            {
                Name = "Stock" + i,
                Position = new(YardX + place % 3 * 2.3f, y, YardZ + place / 3 * 2.6f),
            };
            AddChild(slot);
            FillSlot(slot, layer);
            slots[i] = slot;
        }
        Obstacles.Box(this, new(cx, y + .6f, cz), new(width - .6f, 1.2f, depth - .6f));
    }

    private void FillSlot(Node3D slot, int layer)
    {
        switch (Spec.Product)
        {
            case Product.Planks:
            {
                float lift = layer * .56f;
                string[] tones = { "d9b27a", "c99d63", "e0bd88" };
                for (int i = 0; i < 3; i++)
                    Art.Block(slot, new(0, lift + .1f + i * .17f, 0), new(1.7f, .15f, 2.3f), tones[i]);
                foreach (float strap in new[] { -.7f, .7f })
                    Art.Block(slot, new(0, lift + .28f, strap), new(1.74f, .6f, .08f), "6f5a46");
                break;
            }
            case Product.Bricks:
            {
                float lift = layer * .74f;
                if (layer == 0)
                    Art.Block(slot, new(0, .08f, 0), new(1.8f, .16f, 1.6f), "a77a59");
                Art.Block(slot, new(0, lift + .46f, 0), new(1.6f, .58f, 1.4f), "c4623f");
                foreach (float band in new[] { .3f, .5f, .68f })
                    Art.Block(slot, new(0, lift + band, 0), new(1.64f, .04f, 1.44f), "e0a384");
                break;
            }
            case Product.Bread:
            {
                // A wooden tray with four round loaves and two long ones, a second tray on top for the next layer.
                float lift = layer * .5f;
                Art.Block(slot, new(0, lift + .04f, 0), new(1.9f, .08f, 1.7f), "8d6a46");
                for (int i = 0; i < 4; i++)
                {
                    float x = -.5f + i % 2 * 1f,
                        z = -.42f + i / 2 * .84f;
                    Art.Ball(slot, new(x, lift + .24f, z), new(.4f, .22f, .36f), i % 2 == 0 ? "d9923f" : "c27a2c");
                    Art.Ball(slot, new(x, lift + .34f, z), new(.28f, .1f, .24f), "e8b062");
                }
                foreach (float x in new[] { -.15f, .15f })
                    Art.Ball(slot, new(x, lift + .18f, 0), new(.13f, .13f, .72f), "dca04a");
                break;
            }
            case Product.Glass:
            {
                // A tray of glasses: six pale-blue tumblers, two rows to a layer.
                float lift = layer * .5f;
                Art.Block(slot, new(0, lift + .04f, 0), new(1.9f, .08f, 1.7f), "8d6a46");
                for (int i = 0; i < 6; i++)
                {
                    float x = -.55f + i % 3 * .55f,
                        z = -.38f + i / 3 * .76f;
                    Art.Cylinder(slot, new(x, lift + .3f, z), .13f, .17f, .44f, "cfeaf4");
                    Art.Cylinder(slot, new(x, lift + .17f, z), .12f, .13f, .2f, "8fd3ea");
                }
                break;
            }
            default:
                if (layer == 0)
                {
                    Art.Ball(slot, new(0, .45f, 0), new(1.5f, .9f, 1.5f), "b9b4a5");
                    foreach (float dx in new[] { -.8f, .8f })
                        Art.Ball(slot, new(dx, .12f, dx * .4f), new(.45f, .22f, .4f), "8f8a7d");
                }
                else
                    Art.Ball(slot, new(0, .95f, 0), new(1, .65f, 1), "cfcabb");
                break;
        }
    }

    private void BuildRawPile()
    {
        float y = Pad(RawX, RawZ, 5.4f, 4.6f, "7a6a52");
        switch (Input)
        {
            case HaulLoad.Logs:
                for (int row = 0; row < 3; row++)
                for (int i = 0; i < 4 - row; i++)
                    Art.Log(
                        this,
                        new(RawX + (i - (3 - row) / 2f) * .8f, y + .4f + row * .68f, RawZ),
                        Vector3.Back,
                        .38f,
                        3.6f,
                        (i + row) % 2 == 0 ? "8a5f3f" : "a77a59"
                    );
                break;
            case HaulLoad.Soil:
                Art.Ball(this, new(RawX, y + .6f, RawZ), new(3.2f, 1.7f, 2.8f), "8a6142");
                Art.Ball(this, new(RawX - 1, y + .4f, RawZ + 1), new(1.9f, 1.1f, 1.7f), "6f4b33");
                Art.Ball(this, new(RawX + 1.2f, y + .4f, RawZ - .8f), new(1.7f, 1, 1.5f), "9c7150");
                break;
            case HaulLoad.Wheat:
            case HaulLoad.Flour:
            {
                // Sacks tied at the neck: of wheat with a little spilt grain, or of white flour with a dusting of it.
                bool flour = Input == HaulLoad.Flour;
                for (int i = 0; i < 5; i++)
                {
                    float px = RawX + (i % 3 - 1) * 1.25f + (i / 3) * .6f - .3f;
                    float lift = i / 3 * .75f;
                    float pz = RawZ + (i / 3 - .5f) * .3f + (i % 2) * .2f;
                    string sack = flour ? (i % 2 == 0 ? "f6f2ea" : "ece6da") : (i % 2 == 0 ? "e8dcc0" : "dccfae");
                    Art.Ball(this, new(px, y + .45f + lift, pz), new(.62f, .5f, .5f), sack);
                    Art.Ball(this, new(px, y + .92f + lift, pz), new(.22f, .15f, .2f), flour ? "d8d0c0" : "c9b78e");
                }
                Art.Ball(
                    this,
                    new(RawX - 1.6f, y + .25f, RawZ + 1.4f),
                    new(.9f, .3f, .8f),
                    flour ? "fbf8f2" : "f2c64a"
                );
                break;
            }
            case HaulLoad.Sand:
                Art.Ball(this, new(RawX, y + .55f, RawZ), new(3.4f, 1.5f, 2.9f), "e3c98a");
                Art.Ball(this, new(RawX - 1, y + .35f, RawZ + 1), new(2f, 1, 1.8f), "d6b873");
                Art.Ball(this, new(RawX + 1.2f, y + .35f, RawZ - .8f), new(1.8f, .9f, 1.6f), "ecd79e");
                break;
            default:
                for (int i = 0; i < 6; i++)
                    Art.Ball(
                        this,
                        new(RawX + (i % 3 - 1) * 1.5f, y + .6f + (i / 3) * .7f, RawZ + (i / 3 - .5f) * 1.5f),
                        new(1.7f - i % 2 * .3f, 1.1f, 1.5f),
                        i % 2 == 0 ? "8f8f8a" : "a7a6a0"
                    );
                break;
        }
        Obstacles.Box(this, new(RawX, y + 1, RawZ), new(4.6f, 2, 3.8f));
    }
}
