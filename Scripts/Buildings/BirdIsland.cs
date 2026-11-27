using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The bird island before the mouth of the border river (<c>docs/FERRY_PLAN.md</c>): a low oval of sand and grass with a
/// model and a collision of its own, so that no ground of the borders changes; its pier on the side of the river, where the
/// punt lands; the hide on piles in its middle with the telescope in its window; the twelve clumps of reeds round it in
/// the water; and the birds of the crossing that hide in them (<see cref="Lay"/>). The swan and the grebe swim and rise and
/// fall with the tide, the others stand in their reeds; a bird the telescope sees jumps up and flaps its wings.
/// </summary>
public partial class BirdIsland : Node3D
{
    /// <summary>How much bigger than a real bird the birds are, so that the telescope finds them far off.</summary>
    private const float BirdSize = 1.7f;

    private readonly FerrySite site;
    private readonly IReadOnlyList<ReedSpot> spots;
    private readonly List<Laid> laid = new();
    private float time;

    private sealed class Laid
    {
        public IslandBird Bird = null!;
        public Node3D Model = null!;
        public Node3D Body = null!;
        public readonly List<Node3D> Wings = new();
        public Vector3 Rest;
        public float Joy = -1;
    }

    /// <summary>How many kinds of bird the atlas has, for the kingfisher who waits for all of them.</summary>
    internal int KindsFound { get; set; }

    private Node3D telescope = null!;

    /// <summary>While the player looks through the telescope, its own tube beside the eye is not drawn.</summary>
    internal bool LookingThrough
    {
        set => telescope.Visible = !value;
    }

    internal BirdIsland(FerrySite site, IReadOnlyList<ReedSpot> spots)
    {
        this.site = site;
        this.spots = spots;
        BuildIsland();
        BuildPier();
        BuildHide();
        BuildShore();
        foreach (var spot in spots)
            BuildReeds(spot);
    }

    /// <summary>The birds now laid out, for the checks.</summary>
    internal IReadOnlyList<IslandBird> Birds => laid.ConvertAll(entry => entry.Bird);

    /// <summary>Where the head of a bird of the crossing is now, which the telescope has to find.</summary>
    internal Vector3 HeadOf(int number)
    {
        var entry = laid.Find(e => e.Bird.Number == number);
        return entry == null
            ? site.Island
            : entry.Model.Position + Vector3.Up * Height(entry.Bird.Kind) * BirdSize * .85f;
    }

    /// <summary>Lays out the birds of a crossing in their reeds, in place of those before.</summary>
    internal void Lay(IEnumerable<IslandBird> birds)
    {
        foreach (var entry in laid)
        {
            RemoveChild(entry.Model);
            entry.Model.QueueFree();
        }
        laid.Clear();
        foreach (var bird in birds)
        {
            if (bird.Spot < 0 || bird.Spot >= spots.Count)
                continue;
            var spot = spots[bird.Spot].Position;
            // In front of the reeds as seen from the hide, so that the telescope finds the bird among them.
            var towards = (site.Hide - spot) with { Y = 0 };
            towards = towards.LengthSquared() > .01f ? towards.Normalized() : Vector3.Back;
            var rest = spot + towards * (FerryPlans.Swims(bird.Kind) ? 3.4f : 1.4f);
            var model = new Node3D
            {
                Name = $"Bird{bird.Number}",
                Position = rest,
                Rotation = new Vector3(0, bird.Yaw, 0),
                Scale = Vector3.One * BirdSize,
            };
            AddChild(model);
            var entry = new Laid
            {
                Bird = bird,
                Model = model,
                Rest = rest,
            };
            entry.Body = new Node3D { Name = "Body" };
            model.AddChild(entry.Body);
            BuildBird(entry, bird.Kind);
            Art.Outline(model, .02f);
            laid.Add(entry);
        }
    }

    /// <summary>The telescope has seen bird <paramref name="number"/>: it jumps up out of its reeds and flaps its wings.</summary>
    internal void Cheer(int number)
    {
        var entry = laid.Find(e => e.Bird.Number == number);
        if (entry != null)
            entry.Joy = 0;
    }

    /// <summary>The swimmers ride the sea at <paramref name="sea"/>; the birds the telescope saw jump and flap.</summary>
    internal void Tick(double delta, float sea)
    {
        time += (float)delta;
        foreach (var entry in laid)
        {
            var rest = entry.Rest;
            if (FerryPlans.Swims(entry.Bird.Kind))
                rest.Y = sea + Mathf.Sin(time * 1.6f + entry.Bird.Number) * .04f;
            float hop = 0,
                flap = 0;
            if (entry.Joy >= 0)
            {
                entry.Joy += (float)delta;
                float t = entry.Joy;
                if (t < 2.4f)
                {
                    hop = Mathf.Sin(Mathf.Min(t, 1.2f) / 1.2f * Mathf.Pi) * 1.2f;
                    flap = Mathf.Sin(t * 18) * .9f * (1 - t / 2.4f);
                }
                else
                    entry.Joy = -1;
            }
            else
            {
                // Now and then a bird looks round in its reeds.
                entry.Body.Rotation = new Vector3(0, Mathf.Sin(time * .7f + entry.Bird.Number * 1.7f) * .35f, 0);
            }
            entry.Model.Position = rest + Vector3.Up * hop;
            for (int i = 0; i < entry.Wings.Count; i++)
                entry.Wings[i].Rotation = new Vector3(0, 0, (i % 2 == 0 ? 1 : -1) * flap);
        }
    }

    /// <summary>How tall each kind stands, to the top of its head, before it is made bigger.</summary>
    private static float Height(int kind) =>
        kind switch
        {
            FerryPlans.Swan => .9f,
            FerryPlans.Stork => 1.35f,
            FerryPlans.Cormorant => .95f,
            FerryPlans.Tern => .5f,
            FerryPlans.Plover => .38f,
            FerryPlans.Grebe => .55f,
            FerryPlans.Pelican => 1.15f,
            _ => 1.5f,
        };

    /// <summary>The height of the island's top at a fraction of its oval: flat, then falling into the water.</summary>
    private float IslandHeight(float fraction) =>
        site.Island.Y - Mathf.SmoothStep(.72f, 1.2f, fraction) * (FerrySite.IslandRise + 3);

    /// <summary>The island's ground: grass on the top, sand round it, falling under the water, with its collision.</summary>
    private void BuildIsland()
    {
        const int rings = 16,
            sectors = 56;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var grass = new Color("7fae55");
        var sand = new Color("e3cf98");
        var wet = new Color("b9a274");
        Vector3 Point(int ring, int sector)
        {
            float f = 1.25f * ring / rings;
            float a = sector * Mathf.Tau / sectors;
            // The edge of the island is not drawn with a ruler.
            float wobble = 1 + .06f * Mathf.Sin(a * 3 + 1.3f) + .04f * Mathf.Sin(a * 7);
            float x = Mathf.Sin(a) * f * FerrySite.IslandHalfWidth * wobble,
                z = Mathf.Cos(a) * f * FerrySite.IslandHalfLength * wobble;
            return new Vector3(site.Island.X + x, IslandHeight(f), site.Island.Z + z);
        }
        Color Tint(int ring)
        {
            float f = 1.25f * ring / rings;
            return f < .55f ? grass
                : f < .7f ? grass.Lerp(sand, (f - .55f) / .15f)
                : sand.Lerp(wet, Mathf.Clamp((f - .8f) / .2f, 0, 1));
        }
        void Add(int ring, int sector)
        {
            surface.SetColor(Tint(ring));
            surface.AddVertex(Point(ring, sector % sectors));
        }
        for (int ring = 0; ring < rings; ring++)
        for (int sector = 0; sector < sectors; sector++)
        {
            Add(ring, sector);
            Add(ring, sector + 1);
            Add(ring + 1, sector);
            Add(ring, sector + 1);
            Add(ring + 1, sector + 1);
            Add(ring + 1, sector);
        }
        surface.GenerateNormals();
        var mesh = surface.Commit();
        var land = new MeshInstance3D
        {
            Name = "IslandGround",
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = .95f },
        };
        AddChild(land);
        var body = new StaticBody3D { Name = "IslandBody" };
        AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
    }

    /// <summary>The island's pier on the side of the river, from the shore out to where the punt lands.</summary>
    private void BuildPier()
    {
        var pier = new Node3D { Name = "IslandPier" };
        AddChild(pier);
        float deck = ValleyBorders.SeaLevel + .9f;
        float from = site.IslandBerth.Z + 2,
            to = site.Island.Z - FerrySite.IslandHalfLength * .8f;
        float x = site.IslandBerth.X;
        Art.Block(pier, new Vector3(x, deck, (from + to) / 2), new Vector3(3.2f, .14f, to - from), "c49a66");
        for (float z = from; z <= to; z += 3)
            foreach (int side in new[] { -1, 1 })
                Art.Cylinder(pier, new Vector3(x + side * 1.5f, deck - 1.6f, z), .13f, .13f, 3.4f, "6b4a2e");
        foreach (int side in new[] { -1, 1 })
            Art.Cylinder(pier, new Vector3(x + side * 1.5f, deck + .45f, from), .16f, .18f, 1, "6b4a2e");
    }

    /// <summary>The hide on four piles with a ladder, a roof and the telescope on its stand in the window.</summary>
    private void BuildHide()
    {
        var hide = new Node3D { Name = "Hide", Position = site.Hide };
        AddChild(hide);
        const float floor = 3.8f;
        foreach (var (x, z) in new[] { (-1.5f, -1.5f), (1.5f, -1.5f), (-1.5f, 1.5f), (1.5f, 1.5f) })
            Art.Cylinder(hide, new Vector3(x, floor / 2, z), .16f, .18f, floor, "6b4a2e");
        Art.Block(hide, new Vector3(0, floor, 0), new Vector3(3.6f, .2f, 3.6f), "a87b4c");
        // Low walls round the floor with a wide window all round, and the posts of the roof at the corners.
        foreach (
            var (x, z, sx, sz) in new[]
            {
                (0f, -1.75f, 3.6f, .1f),
                (0f, 1.75f, 3.6f, .1f),
                (-1.75f, 0f, .1f, 3.6f),
                (1.75f, 0f, .1f, 3.6f),
            }
        )
            Art.Block(hide, new Vector3(x, floor + .55f, z), new Vector3(sx, .9f, sz), "c49a66");
        foreach (var (x, z) in new[] { (-1.7f, -1.7f), (1.7f, -1.7f), (-1.7f, 1.7f), (1.7f, 1.7f) })
            Art.Block(hide, new Vector3(x, floor + 1.25f, z), new Vector3(.14f, 2.3f, .14f), "6b4a2e");
        Art.Block(hide, new Vector3(0, floor + 2.45f, 0), new Vector3(4.2f, .16f, 4.2f), "4f7a3c");
        var roof = Art.Block(hide, new Vector3(0, floor + 2.9f, 0), new Vector3(3, .9f, 3), "5d8c46");
        roof.RotationDegrees = new Vector3(0, 45, 0);
        roof.Scale = new Vector3(2.9f, .9f, 2.9f);
        // The ladder up on the side of the pier.
        foreach (int side in new[] { -1, 1 })
        {
            var rail = Art.Block(
                hide,
                new Vector3(side * .45f, floor / 2, -2.4f),
                new Vector3(.1f, floor + .6f, .1f),
                "8a5a35"
            );
            rail.RotationDegrees = new Vector3(-14, 0, 0);
        }
        for (float y = .4f; y < floor; y += .45f)
            Art.Block(hide, new Vector3(0, y, -2.4f + (y - floor / 2) * .25f), new Vector3(.9f, .07f, .1f), "a87b4c");
        // The telescope on its stand, looking out of the window towards the sea.
        var stand = new Node3D { Name = "Telescope", Position = new Vector3(.6f, floor + .1f, .9f) };
        hide.AddChild(stand);
        telescope = stand;
        foreach (float a in new[] { 0f, 2.1f, 4.2f })
            Art.Block(
                stand,
                new Vector3(Mathf.Sin(a) * .2f, .5f, Mathf.Cos(a) * .2f),
                new(.05f, 1, .05f),
                "3a3a3a"
            ).RotationDegrees = new Vector3(Mathf.Cos(a) * 12, 0, -Mathf.Sin(a) * 12);
        var tube = Art.Cylinder(stand, new Vector3(0, 1.1f, .3f), .1f, .14f, 1.1f, "2f4f7a");
        tube.RotationDegrees = new Vector3(80, 0, 0);
        Art.Cylinder(tube, new Vector3(0, .58f, 0), .15f, .15f, .08f, "c9a25a");
        var body = new StaticBody3D { Name = "HideBody" };
        hide.AddChild(body);
        body.AddChild(
            new CollisionShape3D
            {
                Position = new Vector3(0, 1.6f, -.3f),
                Shape = new BoxShape3D { Size = new Vector3(3.6f, 3.2f, 4.4f) },
            }
        );
    }

    /// <summary>Reeds along the island's shore, a few bushes and stones on its top.</summary>
    private void BuildShore()
    {
        var shore = new Node3D { Name = "Shore" };
        AddChild(shore);
        var random = new Random(4211);
        for (int i = 0; i < 26; i++)
        {
            float a = (float)random.NextDouble() * Mathf.Tau;
            // Leave the side of the pier open, where the car drives off the punt.
            if (Mathf.Abs(Mathf.Wrap(a - Mathf.Pi, -Mathf.Pi, Mathf.Pi)) < .45f)
                continue;
            float f = .8f + (float)random.NextDouble() * .12f;
            var p = new Vector3(
                site.Island.X + Mathf.Sin(a) * f * FerrySite.IslandHalfWidth,
                IslandHeight(f),
                site.Island.Z + Mathf.Cos(a) * f * FerrySite.IslandHalfLength
            );
            Tuft(shore, p, random, 5, 1.3f);
        }
        foreach (var (x, z, size) in new[] { (-12f, 6f, 1.4f), (14f, -3f, 1.1f), (9f, 9f, 1.2f), (-16f, -6f, 1f) })
        {
            var p = site.Island + new Vector3(x, 0, z);
            Art.Ball(shore, p + Vector3.Up * size * .6f, new Vector3(size * 1.6f, size * 1.2f, size * 1.5f), "5f8f43");
            Art.Ball(shore, p + new Vector3(size * .5f, size * .9f, 0), Vector3.One * size, "6f9f4d");
        }
        foreach (var (x, z) in new[] { (-7f, -9f), (18f, 6f), (4f, 13f) })
            Art.Ball(shore, site.Island + new Vector3(x, .15f, z), new Vector3(1.1f, .6f, .9f), "a39d92");
    }

    /// <summary>A tuft of reeds: thin green stalks, some with a brown head.</summary>
    private static void Tuft(Node3D parent, Vector3 foot, Random random, int stalks, float tall)
    {
        for (int s = 0; s < stalks; s++)
        {
            float h = tall * (.7f + (float)random.NextDouble() * .6f);
            var at = foot + new Vector3((float)random.NextDouble() - .5f, 0, (float)random.NextDouble() - .5f) * .9f;
            var stalk = Art.Cylinder(parent, at + Vector3.Up * h / 2, .03f, .04f, h, s % 2 == 0 ? "7d9a4a" : "93a95a");
            stalk.RotationDegrees = new Vector3(
                ((float)random.NextDouble() - .5f) * 16,
                0,
                ((float)random.NextDouble() - .5f) * 16
            );
            if (s % 3 == 0)
                Art.Cylinder(parent, at + Vector3.Up * (h + .15f), .07f, .07f, .35f, "6b4a2e");
        }
    }

    /// <summary>A clump of reeds in the water: a low mound of mud and a thick stand of reeds on it.</summary>
    private void BuildReeds(ReedSpot spot)
    {
        var clump = new Node3D { Name = $"Reeds{spot.Number}", Position = spot.Position };
        AddChild(clump);
        Art.Ball(clump, new Vector3(0, -.45f, 0), new Vector3(5.5f, 1.2f, 4.6f), "6f6146");
        var random = new Random(spot.Number * 131 + 17);
        for (int t = 0; t < 7; t++)
        {
            float a = t * Mathf.Tau / 7 + (float)random.NextDouble() * .5f;
            float r = 1 + (float)random.NextDouble() * 1.1f;
            Tuft(clump, new Vector3(Mathf.Sin(a) * r, 0, Mathf.Cos(a) * r), random, 6, 2.4f);
        }
    }

    /// <summary>Builds a bird of one of the eight kinds; its wings go into the list, so that it can flap them.</summary>
    private static void BuildBird(Laid entry, int kind)
    {
        var b = entry.Body;
        void Wings(Vector3 at, Vector3 size, string color, float tilt = 0)
        {
            foreach (int side in new[] { -1, 1 })
            {
                var pivot = new Node3D { Position = at with { X = side * at.X } };
                b.AddChild(pivot);
                var wing = Art.Ball(pivot, new Vector3(side * size.X * .35f, 0, 0), size, color);
                wing.RotationDegrees = new Vector3(tilt, 0, -side * 10);
                entry.Wings.Add(pivot);
            }
        }
        void Legs(float length, string color, float apart = .08f)
        {
            foreach (int side in new[] { -1, 1 })
                Art.Cylinder(b, new Vector3(side * apart, length / 2, 0), .02f, .02f, length, color);
        }
        void Eyes(Vector3 head, float spread, float size = .035f)
        {
            foreach (int side in new[] { -1, 1 })
                Art.Ball(b, head + new Vector3(side * spread, .02f, .03f), Vector3.One * size, "1d1d1d");
        }
        switch (kind)
        {
            case FerryPlans.Swan:
                Art.Ball(b, new Vector3(0, .25f, 0), new Vector3(.5f, .36f, .8f), "fbfbf8");
                Wings(new Vector3(.18f, .32f, -.05f), new Vector3(.28f, .2f, .62f), "f1f1ec");
                Art.Cylinder(b, new Vector3(0, .55f, .33f), .06f, .07f, .5f, "fbfbf8").RotationDegrees = new(-12, 0, 0);
                Art.Ball(b, new Vector3(0, .82f, .3f), new Vector3(.17f, .15f, .22f), "fbfbf8");
                Art.Ball(b, new Vector3(0, .8f, .46f), new Vector3(.07f, .06f, .16f), "f08a2c");
                Art.Ball(b, new Vector3(0, .84f, .38f), new Vector3(.06f, .06f, .06f), "1d1d1d");
                Eyes(new Vector3(0, .84f, .33f), .07f);
                break;
            case FerryPlans.Stork:
                Legs(.6f, "d83b2c", .07f);
                Art.Ball(b, new Vector3(0, .78f, 0), new Vector3(.34f, .32f, .62f), "fbfbf8");
                Wings(new Vector3(.15f, .82f, -.12f), new Vector3(.18f, .2f, .55f), "222222");
                Art.Cylinder(b, new Vector3(0, 1.02f, .22f), .05f, .06f, .36f, "fbfbf8").RotationDegrees = new(
                    -20,
                    0,
                    0
                );
                Art.Ball(b, new Vector3(0, 1.22f, .3f), new Vector3(.14f, .13f, .16f), "fbfbf8");
                Art.Cylinder(b, new Vector3(0, 1.2f, .5f), .015f, .04f, .36f, "d83b2c").RotationDegrees = new(80, 0, 0);
                Eyes(new Vector3(0, 1.24f, .32f), .06f);
                break;
            case FerryPlans.Cormorant:
                Legs(.2f, "222222");
                Art.Ball(b, new Vector3(0, .48f, 0), new Vector3(.3f, .56f, .3f), "262a2c");
                // It holds its wings out to dry in the sun.
                Wings(new Vector3(.16f, .6f, -.04f), new Vector3(.62f, .34f, .1f), "1c2022");
                Art.Ball(b, new Vector3(0, .84f, .05f), new Vector3(.15f, .15f, .17f), "262a2c");
                Art.Ball(b, new Vector3(0, .84f, .18f), new Vector3(.09f, .09f, .06f), "f3d36b");
                Art.Cylinder(b, new Vector3(0, .83f, .3f), .015f, .03f, .22f, "4b4b4b").RotationDegrees = new(85, 0, 0);
                Eyes(new Vector3(0, .87f, .1f), .06f, .03f);
                break;
            case FerryPlans.Tern:
                Legs(.12f, "e0502e");
                Art.Ball(b, new Vector3(0, .24f, 0), new Vector3(.2f, .17f, .42f), "fbfbf8");
                Wings(new Vector3(.08f, .28f, -.08f), new Vector3(.12f, .06f, .44f), "b9c4cc");
                Art.Block(b, new Vector3(0, .26f, -.3f), new Vector3(.12f, .03f, .22f), "fbfbf8").RotationDegrees = new(
                    -10,
                    0,
                    0
                );
                Art.Ball(b, new Vector3(0, .38f, .17f), new Vector3(.12f, .11f, .13f), "fbfbf8");
                Art.Ball(b, new Vector3(0, .42f, .15f), new Vector3(.12f, .07f, .13f), "1d1d1d");
                Art.Cylinder(b, new Vector3(0, .37f, .3f), .01f, .025f, .16f, "e0502e").RotationDegrees = new(88, 0, 0);
                break;
            case FerryPlans.Plover:
                Legs(.1f, "f0a03a", .05f);
                Art.Ball(b, new Vector3(0, .18f, 0), new Vector3(.2f, .16f, .26f), "fbfbf8");
                Art.Ball(b, new Vector3(0, .22f, -.02f), new Vector3(.2f, .12f, .26f), "9c8461");
                Art.Ball(b, new Vector3(0, .3f, .1f), new Vector3(.11f, .1f, .11f), "9c8461");
                Art.Block(b, new Vector3(0, .25f, .12f), new Vector3(.17f, .03f, .06f), "1d1d1d");
                Art.Cylinder(b, new Vector3(0, .29f, .19f), .01f, .02f, .07f, "f0a03a").RotationDegrees = new(88, 0, 0);
                Eyes(new Vector3(0, .32f, .12f), .05f, .025f);
                Wings(new Vector3(.08f, .23f, -.04f), new Vector3(.08f, .06f, .2f), "8a7352");
                break;
            case FerryPlans.Grebe:
                Art.Ball(b, new Vector3(0, .12f, 0), new Vector3(.26f, .18f, .44f), "6b4f3a");
                Wings(new Vector3(.1f, .17f, -.05f), new Vector3(.14f, .08f, .32f), "5a4130");
                Art.Cylinder(b, new Vector3(0, .3f, .15f), .05f, .05f, .3f, "fbfbf8").RotationDegrees = new(-8, 0, 0);
                Art.Ball(b, new Vector3(0, .48f, .17f), new Vector3(.12f, .11f, .13f), "3a2c22");
                foreach (int side in new[] { -1, 1 })
                    Art.Ball(b, new Vector3(side * .08f, .44f, .14f), new Vector3(.08f, .1f, .08f), "d86a2a");
                Art.Ball(b, new Vector3(0, .56f, .13f), new Vector3(.06f, .08f, .08f), "1d1d1d");
                Art.Cylinder(b, new Vector3(0, .47f, .29f), .012f, .025f, .12f, "c94f4f").RotationDegrees = new(
                    86,
                    0,
                    0
                );
                break;
            case FerryPlans.Pelican:
                Legs(.25f, "e0a03a", .1f);
                Art.Ball(b, new Vector3(0, .58f, 0), new Vector3(.48f, .44f, .75f), "fbfbf8");
                Wings(new Vector3(.2f, .62f, -.1f), new Vector3(.24f, .24f, .62f), "e9e6df");
                Art.Cylinder(b, new Vector3(0, .86f, .3f), .07f, .08f, .28f, "fbfbf8");
                Art.Ball(b, new Vector3(0, 1.04f, .32f), new Vector3(.18f, .16f, .2f), "fbfbf8");
                // The long beak with the big orange pouch under it.
                Art.Block(b, new Vector3(0, 1.0f, .68f), new Vector3(.08f, .05f, .6f), "f0c040");
                Art.Ball(b, new Vector3(0, .88f, .6f), new Vector3(.12f, .14f, .46f), "f08a2c");
                Eyes(new Vector3(0, 1.06f, .36f), .08f);
                break;
            default:
                // The flamingo stands on one long leg, with the other folded up.
                Art.Cylinder(b, new Vector3(.05f, .45f, 0), .02f, .02f, .9f, "f48aa4");
                var folded = Art.Cylinder(b, new Vector3(-.06f, .7f, -.08f), .02f, .02f, .4f, "f48aa4");
                folded.RotationDegrees = new Vector3(70, 0, 0);
                Art.Ball(b, new Vector3(0, .98f, 0), new Vector3(.3f, .26f, .52f), "f6a6b8");
                Wings(new Vector3(.13f, 1.02f, -.1f), new Vector3(.15f, .15f, .44f), "ee7f98");
                Art.Cylinder(b, new Vector3(0, 1.22f, .15f), .035f, .04f, .4f, "f6a6b8").RotationDegrees = new(
                    -25,
                    0,
                    0
                );
                Art.Ball(b, new Vector3(0, 1.43f, .25f), new Vector3(.11f, .11f, .13f), "f6a6b8");
                var beak = Art.Cylinder(b, new Vector3(0, 1.38f, .36f), .02f, .035f, .17f, "1d1d1d");
                beak.RotationDegrees = new Vector3(120, 0, 0);
                Eyes(new Vector3(0, 1.45f, .26f), .05f, .025f);
                break;
        }
    }
}
