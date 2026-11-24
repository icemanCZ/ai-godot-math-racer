using Godot;
using MathRacer.App;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The village post office (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 2): a small yellow house with a red tiled
/// gable towards the road, a blue door under the sign <c>POŠTA</c> and the post horn, a bench of parcels, a bicycle
/// against the wall and a yellow letter box at the front of the paved forecourt. The node stands at
/// <see cref="PostOfficeSite.Front"/>, turned so that +Z points away from the road; the front of the house faces -Z.
/// </summary>
/// <remarks>
/// The ground is not levelled (the world fingerprints hold the terrain), so the house stands on a plinth from the lowest
/// ground under it to above the highest, and the props stand on the ground at their own spots. Nothing here moves.
/// </remarks>
public partial class PostOffice : Node3D
{
    private const string Wall = "f3d77e";
    private const string Trim = "fff1d2";
    private const string PostBlue = "1f4e8c";
    private const string PostYellow = "f2c230";

    private readonly Terrain ground;
    private readonly Basis basis;
    private float floor;

    /// <summary>The front of the house above the door, for the camera.</summary>
    internal Vector3 Focus => Position + basis * new Vector3(0, floor + 2.4f, PostOfficeSite.HouseAlong);

    internal PostOffice(PostOfficeSite site, Terrain ground)
    {
        this.ground = ground;
        Name = "PostOffice";
        Position = site.Front;
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        BuildHouse();
        BuildForecourt();
        LetterBox(At(-2.5f, -.6f));
    }

    /// <summary>Height above the node of the ground under a point of the site's frame.</summary>
    private float Floor(float x, float z)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    private Vector3 At(float x, float z, float lift = 0) => new(x, Floor(x, z) + lift, z);

    private void BuildHouse()
    {
        float width = PostOfficeSite.HouseWidth,
            depth = PostOfficeSite.HouseDepth,
            middle = PostOfficeSite.HouseAlong;
        float low = float.MaxValue,
            high = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
        {
            float h = Floor(dx * (width + .6f), middle + dz * (depth + .6f));
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);
        }
        float y = high + .3f,
            bottom = low - .4f;
        floor = y;
        Art.Block(this, new(0, (y + bottom) / 2, middle), new(width + .6f, y - bottom, depth + .6f), "a49d92");
        Art.Block(this, new(0, y - .05f, middle), new(width + .74f, .1f, depth + .74f), "bdb5a9");
        const float height = 3.6f;
        Art.Wall(this, new(0, y + height / 2, middle), new(width, height, depth), Wall, FacadeStyle.Plaster);
        // A blue band under the eaves and white corners, the colours of the post.
        Art.Block(this, new(0, y + height - .2f, middle), new(width + .08f, .26f, depth + .08f), PostBlue);
        foreach (float x in new[] { -width / 2, width / 2 })
        foreach (float z in new[] { middle - depth / 2, middle + depth / 2 })
            Art.Block(this, new(x, y + height / 2, z), new(.3f, height, .3f), Trim);

        // The gable faces the road: the ridge runs from front to back.
        var roof = Art.Mesh(
            this,
            new PrismMesh { Size = new Vector3(width + 1.2f, 2.5f, depth + 1.1f) },
            new(0, y + height + 1.25f, middle),
            Vector3.One,
            "c4553a"
        );
        roof.MaterialOverride = Art.RoofTiles("c4553a", Wall);
        Art.Block(this, new(1.8f, y + height + 1.7f, middle + 1.2f), new(.7f, 1.8f, .7f), "b7563f");
        Art.ChimneySmoke(this, new(1.8f, y + height + 2.7f, middle + 1.2f));

        float front = middle - depth / 2;
        // The door with steps down to the forecourt, and the sign and the post horn above it.
        Art.Block(this, new(0, y + 1.15f, front - .03f), new(1.5f, 2.3f, .08f), Trim);
        Art.Block(this, new(0, y + 1.05f, front - .08f), new(1.2f, 2.1f, .08f), PostBlue);
        Art.Block(this, new(0, y + 1.05f, front - .13f), new(.06f, 2.0f, .04f), "173b69");
        Art.Ball(this, new(.42f, y + 1.05f, front - .15f), Vector3.One * .07f, PostYellow);
        float foot = Floor(0, front - 1.2f);
        int steps = Mathf.Clamp(Mathf.CeilToInt((y - foot) / .3f), 1, 5);
        for (int step = 0; step < steps; step++)
        {
            float top = y - step * (y - foot) / steps;
            Art.Block(
                this,
                new(0, (top + foot - .1f) / 2, front - .5f - step * .4f),
                new(1.8f, top - foot + .1f, .42f),
                "bdb5a9"
            );
        }
        Art.Block(this, new(0, y + 2.75f, front - .06f), new(3.1f, .62f, .1f), PostBlue);
        Art.Sign(this, new(0, y + 2.75f, front - .3f), Texts.SignPost, 40);
        PostHorn(new(0, y + height + .75f, front - .6f), .55f);
        foreach (float x in new[] { -2.3f, 2.3f })
        {
            Art.Block(this, new(x, y + 1.7f, front - .04f), new(1.45f, 1.45f, .08f), Trim);
            Art.Window(this, new(x, y + 1.7f, front - .1f), new(1.12f, 1.12f, .06f), "a4dceb");
            Art.Block(this, new(x, y + 1.7f, front - .15f), new(.1f, 1.15f, .04f), Trim);
            Art.Block(this, new(x, y + .95f, front - .2f), new(1.4f, .22f, .32f), "c98e71");
            for (int i = 0; i < 4; i++)
                Art.Ball(
                    this,
                    new(x - .45f + i * .3f, y + 1.13f, front - .22f),
                    Vector3.One * .13f,
                    i % 2 == 0 ? "f4b1c6" : "fff0ce"
                );
        }
        foreach (int side in new[] { -1, 1 })
        {
            float x = side * (width / 2 + .04f);
            Art.Window(this, new(x, y + 1.7f, middle), new(.06f, 1.1f, 1.1f), "a4dceb");
            Art.Block(this, new(x, y + 1.7f, middle), new(.04f, 1.35f, 1.35f), Trim);
        }
        Obstacles.Box(
            this,
            new(0, (bottom + y + height) / 2, middle),
            new(width + .6f, y + height - bottom, depth + .6f)
        );
    }

    /// <summary>The paved forecourt from the verge to the door, a bench of parcels and a bicycle by the wall.</summary>
    private void BuildForecourt()
    {
        float front = PostOfficeSite.HouseAlong - PostOfficeSite.HouseDepth / 2;
        for (int x = -4; x < 4; x++)
        for (int z = -2; z < 1; z++)
            Art.Block(this, At(x + .5f, z + .5f, .04f), new(.97f, .12f, .97f), (x + z) % 2 == 0 ? "e7d5b7" : "d9c3a0");
        // A bench with parcels tied with string, right of the door.
        var bench = At(2.6f, front - .9f);
        Art.Block(this, bench + new Vector3(0, .5f, 0), new(1.6f, .1f, .5f), "bc8b60");
        foreach (float leg in new[] { -.7f, .7f })
            Art.Block(this, bench + new Vector3(leg, .25f, 0), new(.1f, .5f, .45f), "526f71");
        Parcel(bench + new Vector3(-.45f, .73f, 0), new(.5f, .36f, .4f), "c79a64");
        Parcel(bench + new Vector3(.2f, .68f, .02f), new(.55f, .26f, .38f), "d6ad78");
        Parcel(bench + new Vector3(.15f, .96f, 0), new(.34f, .3f, .3f), "b98a58");
        Obstacles.Box(this, bench + new Vector3(0, .5f, 0), new(1.6f, 1, .55f));
        // The postman's bicycle leans on the wall left of the door.
        var bike = new Node3D { Name = "PostBicycle", Position = At(-1.6f, front - .45f) };
        AddChild(bike);
        foreach (float along in new[] { -.55f, .55f })
        {
            var wheel = Art.Cylinder(bike, new(along, .36f, 0), .36f, .36f, .05f, "2d2d2d");
            wheel.Rotation = new(Mathf.Pi / 2, 0, 0);
            var hub = Art.Cylinder(bike, new(along, .36f, 0), .08f, .08f, .07f, "c0c0c0");
            hub.Rotation = new(Mathf.Pi / 2, 0, 0);
        }
        Art.Beam(bike, new(-.55f, .36f, 0), new(0, .75f, 0), .04f, PostBlue);
        Art.Beam(bike, new(.55f, .36f, 0), new(0, .75f, 0), .04f, PostBlue);
        Art.Beam(bike, new(-.55f, .36f, 0), new(.45f, .85f, 0), .04f, PostBlue);
        Art.Block(bike, new(.48f, .95f, 0), new(.08f, .06f, .5f), "2d2d2d");
        Art.Block(bike, new(-.05f, .82f, 0), new(.28f, .06f, .14f), "4a3527");
        Art.Block(bike, new(.66f, .7f, 0), new(.34f, .24f, .4f), PostYellow);
        bike.Rotation = new(.12f, 0, 0);
    }

    private void Parcel(Vector3 at, Vector3 size, string color)
    {
        Art.Block(this, at, size, color);
        Art.Block(this, at, size + new Vector3(.02f, .02f, -size.Z + .03f), "f3e6cb");
        Art.Block(this, at, size + new Vector3(-size.X + .03f, .02f, .02f), "f3e6cb");
    }

    /// <summary>The post horn: a yellow disc with the blue horn curled on it, facing -Z.</summary>
    private void PostHorn(Vector3 at, float radius)
    {
        var disc = Art.Cylinder(this, at, radius, radius, .06f, PostYellow);
        disc.Rotation = new(Mathf.Pi / 2, 0, 0);
        for (int i = 0; i < 9; i++)
        {
            float angle = Mathf.Pi * .15f + i * Mathf.Pi * 1.4f / 8;
            float r = radius * .48f;
            Art.Ball(
                this,
                at + new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * .8f, -.05f),
                Vector3.One * radius * (.12f + i * .012f),
                PostBlue
            );
        }
        Art.Ball(
            this,
            at + new Vector3(radius * .55f, -radius * .3f, -.05f),
            new Vector3(.16f, .2f, .05f) * radius * 1.4f,
            PostBlue
        );
    }

    /// <summary>The yellow letter box on a post, with the post horn on its front.</summary>
    private void LetterBox(Vector3 at)
    {
        var box = new Node3D { Name = "LetterBox", Position = at };
        AddChild(box);
        Art.Cylinder(box, new(0, .55f, 0), .07f, .07f, 1.1f, "4d4d4d");
        Art.Block(box, new(0, 1.32f, 0), new(.62f, .58f, .42f), PostYellow);
        Art.Block(box, new(0, 1.66f, 0), new(.68f, .1f, .48f), "d9a521");
        Art.Block(box, new(0, 1.46f, -.215f), new(.36f, .05f, .02f), "2d2d2d");
        Art.Ball(box, new(0, 1.24f, -.22f), new(.13f, .09f, .02f), PostBlue);
        Art.Ball(box, new(-.07f, 1.24f, -.225f), new(.05f, .05f, .02f), PostYellow);
        Obstacles.Box(box, new(0, .85f, 0), new(.68f, 1.7f, .5f));
    }
}
