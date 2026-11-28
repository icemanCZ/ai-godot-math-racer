using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The ship Albatros (<c>docs/SHIP_PLAN.md</c>): a two-masted sailing ship raised on the slipway of the shipyard with
/// fourteen deliveries, from the keel through the ribs, four strakes of planks, the deck, the cabin with the wheel and the
/// lanterns, the anchor and the masts to the sails, and then launched. In its own frame +Z is the bow, the keel lies on
/// y = 0 and the sea reaches up to <see cref="Draft"/>. The sails fill with the wind as the helm wants them
/// (<see cref="SetSails"/>); the treasures of the captain's cabin stand on a shelf on the deck and the cat sits on the bow
/// once the cabin is full. The game moves the node: on the slipway, at anchor, on a voyage.
/// </summary>
public sealed partial class Ship : Node3D
{
    /// <summary>Half the length of the hull, and the most it is wide on each side.</summary>
    internal const float HalfLength = 13,
        HalfBeam = 3.4f;

    /// <summary>How deep the hull lies in the water when it floats.</summary>
    internal const float Draft = 1.2f;

    /// <summary>The height of the deck over the keel.</summary>
    internal const float DeckHeight = 3.05f;

    private readonly Node3D keel = new() { Name = "Keel" };
    private readonly Node3D bowRibs = new() { Name = "BowRibs" };
    private readonly Node3D sternRibs = new() { Name = "SternRibs" };
    private readonly Node3D[] strakes = new Node3D[ShipPlans.Rows];
    private readonly Node3D deck = new() { Name = "Deck" };
    private readonly Node3D cabin = new() { Name = "Cabin" };
    private readonly Node3D lanterns = new() { Name = "Lanterns" };
    private readonly Node3D anchor = new() { Name = "ShipAnchor" };
    private readonly Node3D masts = new() { Name = "Masts" };
    private readonly Node3D foreSails = new() { Name = "ForeSails" };
    private readonly Node3D mainSails = new() { Name = "MainSails" };
    private readonly Node3D wheel = new() { Name = "Wheel" };
    private readonly Node3D flag = new() { Name = "Flag" };
    private readonly Node3D cat = new() { Name = "ShipCat" };
    private readonly Node3D arrow = new() { Name = "BuoyArrow" };
    private readonly Node3D[] shelf = new Node3D[ShipPlans.Kinds];
    private readonly List<Node3D> sails = new();
    private readonly List<OmniLight3D> lights = new();
    private float time;
    private float fill = .5f;

    /// <summary>How many deliveries the ship has had, 0 to <see cref="ShipPlans.Trips"/>.</summary>
    internal int Stage { get; private set; }

    internal bool Complete => Stage >= ShipPlans.Trips;

    /// <summary>Whether it floats (bobbing on the waves) rather than standing on the slipway.</summary>
    internal bool Afloat { get; set; }

    /// <summary>The captain, Mrož kapitán Vojta, who sends the trailer and steers beside the player.</summary>
    internal Character Captain { get; set; } = null!;

    /// <summary>Which parts stand, for the checks.</summary>
    internal bool KeelShown => keel.Visible;
    internal bool RibsShown => bowRibs.Visible && sternRibs.Visible;
    internal int StrakesShown => Array.FindAll(strakes, s => s.Visible).Length;
    internal bool DeckShown => deck.Visible;
    internal bool CabinShown => cabin.Visible;
    internal bool AnchorShown => anchor.Visible;
    internal bool MastsShown => masts.Visible;
    internal bool SailsShown => foreSails.Visible && mainSails.Visible;
    internal bool CatShown => cat.Visible;
    internal bool ArrowShown => arrow.Visible;

    /// <summary>How full the sails stand now, 0 furled to 1 full.</summary>
    internal float Fill => fill;

    internal Ship()
    {
        Name = "Ship";
        foreach (
            var part in new[] { keel, bowRibs, sternRibs, deck, cabin, lanterns, anchor, masts, foreSails, mainSails }
        )
            AddChild(part);
        BuildKeel();
        BuildRibs();
        for (int row = 0; row < ShipPlans.Rows; row++)
        {
            strakes[row] = new Node3D { Name = $"Strake{row}" };
            AddChild(strakes[row]);
            strakes[row].AddChild(Strake(row));
        }
        BuildDeck();
        BuildCabin();
        BuildLanterns();
        BuildAnchor();
        BuildMasts();
        BuildCat();
        BuildArrow();
        SetStage(0);
    }

    /// <summary>The half width of the hull at <paramref name="z"/>: sharp at the bow, fuller at the stern.</summary>
    internal static float Beam(float z)
    {
        float u = Mathf.Clamp(z / HalfLength, -1, 1);
        float shape = 1 - Mathf.Pow(Mathf.Abs(u), u > 0 ? 1.7f : 2.6f);
        return HalfBeam * Mathf.Sqrt(Mathf.Max(0, shape));
    }

    /// <summary>The height of the gunwale at <paramref name="z"/>: the ends rise a little.</summary>
    internal static float Sheer(float z)
    {
        float u = Mathf.Clamp(z / HalfLength, -1, 1);
        return DeckHeight + .35f + .9f * u * u;
    }

    /// <summary>A point of the hull's side (<paramref name="side"/> ±1) at <paramref name="z"/> and height share <paramref name="t"/>.</summary>
    private static Vector3 Hull(float z, float t, int side)
    {
        float round = 1 - (1 - t) * (1 - t);
        return new Vector3(side * Beam(z) * round, t * Sheer(z), z);
    }

    private static StandardMaterial3D HullMaterial() =>
        new()
        {
            VertexColorUseAsAlbedo = true,
            Roughness = .9f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

    /// <summary>A strake of planks along both sides: from height share row / 4 to (row + 1) / 4.</summary>
    private static MeshInstance3D Strake(int row)
    {
        const int along = 28,
            up = 3;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var light = new Color(row % 2 == 0 ? "9a6a40" : "8a5c36");
        var dark = new Color("5e3c22");
        float from = row / (float)ShipPlans.Rows,
            to = (row + 1) / (float)ShipPlans.Rows;
        foreach (int side in new[] { -1, 1 })
            for (int i = 0; i < along; i++)
            for (int j = 0; j < up; j++)
            {
                float z0 = -HalfLength + 2 * HalfLength * i / along,
                    z1 = -HalfLength + 2 * HalfLength * (i + 1) / along;
                float t0 = Mathf.Lerp(from, to, j / (float)up),
                    t1 = Mathf.Lerp(from, to, (j + 1) / (float)up);
                // The seam at the top of each strake is darker, so the planks show.
                var c0 = j == up - 1 ? light.Lerp(dark, .35f) : light;
                Vector3 a = Hull(z0, t0, side),
                    b = Hull(z1, t0, side),
                    c = Hull(z0, t1, side),
                    d = Hull(z1, t1, side);
                foreach (var (p, color) in new[] { (a, light), (b, light), (c, c0), (b, light), (d, c0), (c, c0) })
                {
                    surface.SetColor(color);
                    surface.AddVertex(p);
                }
            }
        surface.GenerateNormals();
        return new MeshInstance3D
        {
            Name = "Planks",
            Mesh = surface.Commit(),
            MaterialOverride = HullMaterial(),
        };
    }

    private void BuildKeel()
    {
        const string wood = "6b4a2e";
        Art.Block(keel, new(0, .2f, 0), new(.5f, .4f, 2 * HalfLength), wood);
        Art.Beam(keel, new(0, 0, HalfLength - .2f), new(0, Sheer(HalfLength) + .6f, HalfLength + .9f), .45f, wood);
        Art.Beam(keel, new(0, 0, -HalfLength + .2f), new(0, Sheer(-HalfLength) + .2f, -HalfLength - .5f), .45f, wood);
    }

    private void BuildRibs()
    {
        const string wood = "7a5434";
        for (int k = -4; k <= 4; k++)
        {
            float z = k * HalfLength / 5;
            var parent = z > 0 ? bowRibs : sternRibs;
            foreach (int side in new[] { -1, 1 })
                for (int j = 0; j < 4; j++)
                    Art.Beam(parent, Hull(z, j / 4f, side), Hull(z, (j + 1) / 4f, side), .22f, wood);
            // The beam across under the deck.
            Art.Block(parent, new(0, DeckHeight - .25f, z), new(2 * Beam(z), .2f, .22f), wood);
        }
    }

    private void BuildDeck()
    {
        const string boards = "c49a66",
            rail = "7a5434";
        int pieces = 12;
        for (int i = 0; i < pieces; i++)
        {
            float z0 = -HalfLength + 1 + (2 * HalfLength - 2) * i / pieces,
                z1 = -HalfLength + 1 + (2 * HalfLength - 2) * (i + 1) / pieces;
            float z = (z0 + z1) / 2;
            Art.Block(deck, new(0, DeckHeight, z), new(2 * Beam(z) * .96f, .14f, z1 - z0 + .02f), boards);
        }
        // The railing along both gunwales: posts and a rail on them.
        foreach (int side in new[] { -1, 1 })
            for (int i = 0; i < 12; i++)
            {
                float z0 = -HalfLength + 1 + (2 * HalfLength - 2) * i / 12f,
                    z1 = -HalfLength + 1 + (2 * HalfLength - 2) * (i + 1) / 12f;
                var top0 = new Vector3(side * Beam(z0) * .97f, Sheer(z0) + .7f, z0);
                var top1 = new Vector3(side * Beam(z1) * .97f, Sheer(z1) + .7f, z1);
                Art.Beam(deck, top0, top1, .12f, rail);
                Art.Beam(deck, top0 with { Y = Sheer(z0) - .05f }, top0, .1f, rail);
            }
        // The shelf of the captain's treasures beside the cabin, and the name on the stern.
        Art.Block(deck, new(1.7f, DeckHeight + .55f, -5.4f), new(1.3f, .08f, 2.6f), "8a6040");
        Art.Block(deck, new(1.7f, DeckHeight + .3f, -5.4f), new(1.3f, .5f, .1f), "7a5434");
        for (int kind = 0; kind < ShipPlans.Kinds; kind++)
        {
            var item = new Node3D
            {
                Name = $"Treasure{kind}",
                Position = new Vector3(1.4f + (kind % 2) * .6f, DeckHeight + .62f, -6.4f + kind / 2 * .65f),
                Visible = false,
            };
            deck.AddChild(item);
            SouvenirModels.Build(item, kind, .45f);
            shelf[kind] = item;
        }
    }

    private void BuildCabin()
    {
        const string walls = "b8865a",
            roof = "5e3c22";
        float z = -HalfLength + 3.6f;
        Art.Block(cabin, new(0, DeckHeight + 1.1f, z), new(4.2f, 2.2f, 4.4f), walls);
        Art.Block(cabin, new(0, DeckHeight + 2.3f, z), new(4.6f, .22f, 4.8f), roof);
        Art.Block(cabin, new(0, DeckHeight + .9f, z + 2.22f), new(.9f, 1.7f, .08f), "6a4a30");
        foreach (int side in new[] { -1, 1 })
            Art.Window(cabin, new(side * 2.12f, DeckHeight + 1.35f, z), new(.06f, .6f, 1.4f), "9ed0e6");
        Art.Window(cabin, new(-1, DeckHeight + 1.35f, z - 2.22f), new(.9f, .6f, .06f), "9ed0e6");
        Art.Window(cabin, new(1, DeckHeight + 1.35f, z - 2.22f), new(.9f, .6f, .06f), "9ed0e6");
        Art.Sign(cabin, new(0, Sheer(-HalfLength) + 1.1f, -HalfLength - .9f), "ALBATROS", 36);
        // The wheel before the cabin: a ring of spokes on a post, which turns with the rudder.
        Art.Block(cabin, new(0, DeckHeight + .55f, z + 3.2f), new(.3f, 1.1f, .3f), "6a4a30");
        cabin.AddChild(wheel);
        wheel.Position = new Vector3(0, DeckHeight + 1.25f, z + 3.4f);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.Tau / 8;
            var tip = new Vector3(Mathf.Cos(a) * .62f, Mathf.Sin(a) * .62f, 0);
            Art.Beam(wheel, Vector3.Zero, tip, .07f, "8a6040");
            Art.Ball(wheel, tip * 1.15f, Vector3.One * .1f, "8a6040");
        }
        Art.Cylinder(wheel, Vector3.Zero, .14f, .14f, .14f, "e0b04a").RotationDegrees = new Vector3(90, 0, 0);
        // The rudder at the stern.
        Art.Block(cabin, new(0, 1.2f, -HalfLength - .6f), new(.2f, 2.2f, 1.2f), "6b4a2e");
    }

    private void BuildLanterns()
    {
        foreach (int side in new[] { -1, 1 })
        {
            var at = new Vector3(side * 1.8f, Sheer(-HalfLength + 1) + 1, -HalfLength + 1);
            Art.Beam(lanterns, at with { Y = at.Y - 1 }, at, .1f, "30363a");
            Art.Window(lanterns, at + Vector3.Up * .25f, new(.32f, .42f, .32f), "ffe9a8");
            Art.Block(lanterns, at + Vector3.Up * .52f, new(.4f, .1f, .4f), "30363a");
            var light = new OmniLight3D
            {
                Position = at + Vector3.Up * .3f,
                LightColor = new Color("ffd27a"),
                OmniRange = 9,
                LightEnergy = 0,
                ShadowEnabled = false,
            };
            lanterns.AddChild(light);
            lights.Add(light);
        }
        Art.Window(lanterns, new(0, Sheer(HalfLength - 1) + 1.1f, HalfLength - 1), new(.3f, .4f, .3f), "ffe9a8");
    }

    private void BuildAnchor()
    {
        const string iron = "4a5258";
        var at = new Vector3(-Beam(HalfLength - 3) - .25f, DeckHeight - .6f, HalfLength - 3);
        Art.Block(anchor, at, new(.2f, 1.6f, .2f), iron);
        Art.Block(anchor, at + new Vector3(0, .75f, 0), new(.2f, .16f, .9f), iron);
        foreach (int side in new[] { -1, 1 })
            Art.Beam(anchor, at + new Vector3(0, -.8f, 0), at + new Vector3(0, -.35f, side * .55f), .18f, iron);
        for (int i = 0; i < 5; i++)
            Art.Ball(anchor, at + new Vector3(.12f, .9f + i * .22f, 0), new(.12f, .14f, .08f), "6a757c");
    }

    private void BuildMasts()
    {
        const string wood = "8a6040";
        foreach (var (z, height, sailSet) in new[] { (5.5f, 15f, foreSails), (-1.5f, 18f, mainSails) })
        {
            Art.Cylinder(masts, new(0, DeckHeight + height / 2, z), .26f, .18f, height, wood);
            for (int yard = 0; yard < 2; yard++)
            {
                float y = DeckHeight + height * (yard == 0 ? .42f : .8f);
                float width = yard == 0 ? 8 : 6;
                Art.Block(masts, new(0, y, z + .25f), new(width, .18f, .18f), wood);
                // The sail hangs under its yard down to the yard below, or to over the deck.
                float bottom = yard == 0 ? DeckHeight + 1.9f : DeckHeight + height * .46f;
                var sail = new Node3D { Name = $"Sail{yard}", Position = new Vector3(0, y - .1f, z + .3f) };
                sailSet.AddChild(sail);
                sail.AddChild(SailMesh(width * .96f, y - bottom, yard == 0));
                sails.Add(sail);
            }
            Art.Ball(masts, new(0, DeckHeight + height + .15f, z), Vector3.One * .32f, "e0b04a");
        }
        // The flag on the top of the main mast.
        masts.AddChild(flag);
        flag.Position = new Vector3(0, DeckHeight + 17.4f, -1.5f);
        Art.Block(flag, new(0, 0, -.8f), new(.04f, .7f, 1.5f), "d23b2f");
        Art.Block(flag, new(.03f, 0, -.8f), new(.02f, .24f, 1.52f), "f4f1ea");
    }

    /// <summary>A square sail as a sheet that bulges forward by a metre; its node's Z scale is how full it stands.</summary>
    private static MeshInstance3D SailMesh(float width, float height, bool stripe)
    {
        const int across = 8,
            down = 6;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var cloth = new Color("f4ecd8");
        var red = new Color("d23b2f");
        Vector3 Point(int i, int j)
        {
            float u = i / (float)across,
                v = j / (float)down;
            float bulge = Mathf.Sin(u * Mathf.Pi) * Mathf.Sin(Mathf.Min(1, v * 1.2f) * Mathf.Pi * .5f + .3f);
            return new Vector3((u - .5f) * width * (1 + v * .08f), -v * height, bulge);
        }
        for (int i = 0; i < across; i++)
        for (int j = 0; j < down; j++)
        {
            var color = stripe && j is 2 ? red : cloth;
            foreach (var (a, b) in new[] { (i, j), (i + 1, j), (i, j + 1), (i + 1, j), (i + 1, j + 1), (i, j + 1) })
            {
                surface.SetColor(color);
                surface.AddVertex(Point(a, b));
            }
        }
        surface.GenerateNormals();
        return new MeshInstance3D
        {
            Name = "Cloth",
            Mesh = surface.Commit(),
            MaterialOverride = HullMaterial(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
    }

    /// <summary>The ship's cat on the bow: tabby, with a little captain's cap; its eyes glow at night.</summary>
    private void BuildCat()
    {
        AddChild(cat);
        cat.Position = new Vector3(0, Sheer(HalfLength - 1.5f) + .05f, HalfLength - 1.8f);
        const string fur = "c9874a",
            stripe = "8a5530";
        Art.Ball(cat, new(0, .35f, -.15f), new(.5f, .5f, .7f), fur);
        Art.Ball(cat, new(0, .78f, .18f), new(.42f, .38f, .38f), fur);
        foreach (int side in new[] { -1, 1 })
        {
            var ear = Art.Block(cat, new(side * .14f, 1.0f, .18f), new(.12f, .18f, .06f), fur);
            ear.RotationDegrees = new Vector3(0, 0, side * -18);
            Art.Lamp(cat, new(side * .1f, .82f, .36f), new(.07f, .08f, .03f), "9be36a");
        }
        for (int i = 0; i < 3; i++)
            Art.Block(cat, new(0, .58f, -.3f + i * .18f), new(.52f, .05f, .07f), stripe);
        Art.Beam(cat, new(0, .3f, -.5f), new(0, .8f, -.85f), .09f, fur);
        Art.Cylinder(cat, new(0, 1.0f, .16f), .2f, .18f, .12f, "2f3f66");
        Art.Block(cat, new(0, .96f, .32f), new(.32f, .03f, .16f), "1f2a44");
        cat.Visible = false;
    }

    /// <summary>The arrow over the bow that points to the next buoy on a voyage.</summary>
    private void BuildArrow()
    {
        AddChild(arrow);
        arrow.Position = new Vector3(0, 11, HalfLength - 2);
        var body = new Node3D { Name = "ArrowBody" };
        arrow.AddChild(body);
        Art.Lamp(body, new(0, 0, 0), new(.5f, .3f, 2.6f), "ffd23a");
        foreach (int side in new[] { -1, 1 })
        {
            var head = Art.Lamp(body, new(side * .42f, 0, 1.05f), new(.4f, .3f, 1.3f), "ffd23a");
            head.RotationDegrees = new Vector3(0, side * 38, 0);
        }
        arrow.Visible = false;
    }

    /// <summary>Shows the parts of the ship that this many deliveries brought.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, ShipPlans.Trips);
        bool Has(int step) => Stage > step;
        keel.Visible = Has(0);
        bowRibs.Visible = Has(1);
        sternRibs.Visible = Has(2);
        for (int row = 0; row < ShipPlans.Rows; row++)
            strakes[row].Visible = Has(3 + row);
        deck.Visible = Has(7);
        cabin.Visible = Has(8);
        lanterns.Visible = Has(9);
        anchor.Visible = Has(ShipPlans.AnchorStep);
        masts.Visible = Has(11);
        foreSails.Visible = Has(12);
        mainSails.Visible = Has(13);
    }

    /// <summary>Puts the treasures found so far on the shelf, and the cat on the bow once there is one of each.</summary>
    internal void SetTreasures(int[] kinds)
    {
        int found = 0;
        for (int kind = 0; kind < shelf.Length; kind++)
        {
            bool has = kind < kinds.Length && kinds[kind] > 0;
            shelf[kind].Visible = has;
            found += has ? 1 : 0;
        }
        cat.Visible = found >= ShipPlans.Kinds;
    }

    /// <summary>The sails stand furled (0) to full (1); the wheel turns with the rudder.</summary>
    internal void SetSails(float full, float rudder)
    {
        fill = Mathf.Clamp(full, 0, 1);
        foreach (var sail in sails)
            sail.Scale = new Vector3(1, .18f + .82f * fill, .15f + .95f * fill);
        wheel.Rotation = new Vector3(0, 0, -rudder * 2.4f);
    }

    /// <summary>The arrow over the bow points at <paramref name="target"/>, or is hidden.</summary>
    internal void PointArrow(Vector3? target)
    {
        arrow.Visible = target != null;
        if (target is not { } at)
            return;
        var d = at - GlobalPosition;
        arrow.Rotation = new Vector3(0, Mathf.Atan2(d.X, d.Z) - Rotation.Y, 0);
    }

    /// <summary>Where the car stands on the deck, in the world, and the way it faces.</summary>
    internal (Vector3 Seat, float Heading) CarSeat => (ToGlobal(new Vector3(0, DeckHeight + .12f, 2.2f)), Rotation.Y);

    /// <summary>Where the captain stands at the wheel, in the world.</summary>
    internal Vector3 HelmSpot => ToGlobal(new Vector3(.9f, DeckHeight + .08f, -HalfLength + 7.4f));

    /// <summary>The flag waves, the ship rocks on the waves when it floats, and the lanterns burn at night.</summary>
    internal void Tick(float delta, float night)
    {
        time += delta;
        flag.Rotation = new Vector3(0, Mathf.Sin(time * 2.3f) * .25f, 0);
        foreach (var light in lights)
            light.LightEnergy = night * 1.6f;
        if (arrow.Visible)
            arrow.GetChild<Node3D>(0).Position = new Vector3(0, Mathf.Sin(time * 3) * .3f, 0);
    }

    /// <summary>The ship floats with its middle at <paramref name="at"/> (on the water) heading <paramref name="heading"/>.</summary>
    internal void Float(Vector3 at, float heading, float sea)
    {
        float roll = Mathf.Sin(time * .9f) * .025f,
            pitch = Mathf.Sin(time * .7f + 1) * .015f;
        Position = new Vector3(at.X, sea - Draft + Mathf.Sin(time * 1.1f) * .08f, at.Z);
        Rotation = new Vector3(pitch, heading, roll);
    }
}
