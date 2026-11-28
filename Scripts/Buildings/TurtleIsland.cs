using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The turtle island far out at sea (<c>docs/SHIP_PLAN.md</c>): an oval of sand and grass with a model and a collision of
/// its own, so that no ground changes; palms, a pier on the side of the coast where the ship lies, and three nests on the
/// beach. On each voyage two turtles sit in two of them, each on a treasure for the captain's cabin
/// (<see cref="Lay"/>); a turtle whose question is solved crawls off into the sea and the treasure under it sparkles.
/// </summary>
public sealed partial class TurtleIsland : Node3D
{
    private readonly ShipSite site;
    private readonly Nest[] nests = new Nest[ShipPlans.Nests];
    private float time;

    private sealed class Nest
    {
        public Node3D Turtle = null!;
        public Node3D Treasure = null!;
        public Vector3 Home;
        public Vector3 Sea;
        public int Kind = -1;
        public float Gone = -1;
    }

    internal TurtleIsland(ShipSite site)
    {
        this.site = site;
        Name = "TurtleIsland";
        BuildGround();
        BuildPalms();
        BuildPier();
        for (int nest = 0; nest < nests.Length; nest++)
            BuildNest(nest);
    }

    /// <summary>The height of the island's ground at fraction <paramref name="f"/> of its oval, falling into the sea at its edge.</summary>
    private float Height(float f) => site.Island.Y - Mathf.SmoothStep(.7f, 1.2f, f) * (ShipSite.IslandRise + 3.5f);

    /// <summary>Where the island's ground is under a point on it.</summary>
    internal Vector3 GroundAt(Vector3 p) => p with { Y = Height(site.IslandFraction(p)) };

    private void BuildGround()
    {
        const int rings = 18,
            sectors = 64;
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var grass = new Color("6f9e4c");
        var sand = new Color("e3cf98");
        var wet = new Color("c2a978");
        Vector3 Point(int ring, int sector)
        {
            float f = 1.25f * ring / rings;
            float a = sector * Mathf.Tau / sectors;
            float wobble = 1 + .05f * Mathf.Sin(a * 3 + .7f) + .03f * Mathf.Sin(a * 8);
            var p =
                site.Island
                + site.Along * (Mathf.Sin(a) * f * ShipSite.IslandHalfLength * wobble)
                + site.Seaward * (Mathf.Cos(a) * f * ShipSite.IslandHalfWidth * wobble);
            return p with { Y = Height(f) };
        }
        Color Tint(int ring)
        {
            float f = 1.25f * ring / rings;
            return f < .45f ? grass
                : f < .62f ? grass.Lerp(sand, (f - .45f) / .17f)
                : sand.Lerp(wet, Mathf.Clamp((f - .82f) / .2f, 0, 1));
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
        AddChild(
            new MeshInstance3D
            {
                Name = "IslandGround",
                Mesh = mesh,
                MaterialOverride = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    Roughness = .95f,
                    CullMode = BaseMaterial3D.CullModeEnum.Back,
                },
            }
        );
        var body = new StaticBody3D { Name = "IslandBody" };
        AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = mesh.CreateTrimeshShape() });
    }

    /// <summary>Palms round the middle, leaning a little, away from the landing and the nests.</summary>
    private void BuildPalms()
    {
        for (int i = 0; i < 11; i++)
        {
            float a = i * Mathf.Tau / 11 + .4f;
            float f = i % 2 == 0 ? .25f : .45f;
            var foot =
                site.Island
                + site.Along * (Mathf.Sin(a) * f * ShipSite.IslandHalfLength)
                + site.Seaward * (Mathf.Cos(a) * f * ShipSite.IslandHalfWidth);
            if (foot.DistanceTo(site.IslandLanding) < 12)
                continue;
            bool clear = true;
            for (int nest = 0; nest < ShipPlans.Nests; nest++)
                clear &= foot.DistanceTo(site.Nest(nest)) > 9;
            if (!clear)
                continue;
            foot = GroundAt(foot);
            var palm = new Node3D { Name = $"Palm{i}", Position = foot };
            AddChild(palm);
            float lean = .25f + (i % 3) * .08f;
            var top = new Vector3(Mathf.Sin(a) * lean * 3, 7 + (i % 3), Mathf.Cos(a) * lean * 3);
            for (int k = 0; k < 6; k++)
            {
                var p0 = top * (k / 6f) + new Vector3(0, 0, 0);
                var p1 = top * ((k + 1) / 6f);
                p0.Y = top.Y * k / 6f;
                p1.Y = top.Y * (k + 1) / 6f;
                Art.Beam(palm, p0, p1, .42f - k * .03f, k % 2 == 0 ? "9a7a52" : "8a6a44");
            }
            for (int leaf = 0; leaf < 7; leaf++)
            {
                float b = leaf * Mathf.Tau / 7;
                var tip = top + new Vector3(Mathf.Cos(b) * 3.4f, -1.3f, Mathf.Sin(b) * 3.4f);
                Art.Beam(palm, top, (top + tip) / 2 + Vector3.Up * .5f, .5f, "4f9a3e");
                Art.Beam(palm, (top + tip) / 2 + Vector3.Up * .5f, tip, .38f, "5aa848");
            }
            foreach (var nut in new[] { new Vector3(.3f, -.4f, 0), new Vector3(-.25f, -.45f, .2f) })
                Art.Ball(palm, top + nut, Vector3.One * .36f, "6a4a30");
            Obstacles.Trunk(palm, Vector3.Zero, .45f, 6);
        }
    }

    private void BuildPier()
    {
        var pier = new Node3D { Name = "IslandPier" };
        AddChild(pier);
        float deck = ValleyBorders.SeaLevel + 1.1f;
        var from = (site.IslandBerth + site.Seaward * 3) with { Y = deck };
        var to = site.Island - site.Seaward * (ShipSite.IslandHalfWidth * .82f);
        to.Y = deck;
        float length = (to - from).Length();
        var board = Art.Block(pier, (from + to) / 2, new(3, .14f, length), "c49a66");
        board.Rotation = new Vector3(0, site.Yaw, 0);
        for (float k = 0; k <= length; k += 3.5f)
        {
            var p = from + (to - from).Normalized() * k;
            foreach (int side in new[] { -1, 1 })
                Art.Cylinder(pier, p + site.Along * side * 1.4f - Vector3.Up * 1.8f, .14f, .14f, 3.8f, "6b4a2e");
        }
    }

    private void BuildNest(int index)
    {
        var home = GroundAt(site.Nest(index));
        var away = (home - site.Island) with { Y = 0 };
        away = away.LengthSquared() > .01f ? away.Normalized() : site.Seaward;
        var nest = new Nest
        {
            Home = home,
            Sea = GroundAt(site.Island + away * (ShipSite.IslandHalfWidth * 1.15f)) with { Y = 0 },
        };
        // A ring of pebbles round a hollow in the sand.
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            Art.Ball(
                this,
                home + new Vector3(Mathf.Cos(a) * 3.1f, .1f, Mathf.Sin(a) * 3.1f),
                new(.5f, .3f, .45f),
                "b9b0a0"
            );
        }
        nest.Treasure = new Node3D
        {
            Name = $"Treasure{index}",
            Position = home,
            Visible = false,
        };
        AddChild(nest.Treasure);
        nest.Turtle = BuildTurtle(index);
        nest.Turtle.Position = home;
        nest.Turtle.Rotation = new Vector3(0, Mathf.Atan2(-away.X, -away.Z), 0);
        nests[index] = nest;
    }

    /// <summary>A big sea turtle: a domed green shell with a pattern, a head and four flippers.</summary>
    private Node3D BuildTurtle(int index)
    {
        var turtle = new Node3D { Name = $"Turtle{index}", Visible = false };
        AddChild(turtle);
        Art.Ball(turtle, new(0, .5f, 0), new(2.2f, .9f, 2.6f), "5f8a4a");
        Art.Ball(turtle, new(0, .72f, 0), new(1.6f, .6f, 1.9f), "7aa35a");
        foreach (var (x, z) in new[] { (0f, 0f), (.45f, .5f), (-.45f, .5f), (.45f, -.5f), (-.45f, -.5f) })
            Art.Ball(turtle, new(x, .98f, z), new(.5f, .12f, .5f), "4a6e3a");
        Art.Ball(turtle, new(0, .5f, 1.55f), new(.7f, .6f, .8f), "a8b87a");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(turtle, new(side * .2f, .65f, 1.85f), new(.12f, .12f, .08f), "2b2b2e");
            var front = Art.Ball(turtle, new(side * 1.15f, .25f, .7f), new(1.1f, .2f, .5f), "a8b87a");
            front.RotationDegrees = new Vector3(0, side * -25, 0);
            Art.Ball(turtle, new(side * .8f, .2f, -.9f), new(.6f, .16f, .4f), "a8b87a");
        }
        return turtle;
    }

    /// <summary>Lays the treasures of a voyage: a kind for each nest, -1 for an empty one; their turtles sit on them.</summary>
    internal void Lay(int[] kinds)
    {
        for (int i = 0; i < nests.Length; i++)
        {
            var nest = nests[i];
            nest.Kind = i < kinds.Length ? kinds[i] : -1;
            nest.Gone = -1;
            foreach (var child in nest.Treasure.GetChildren())
            {
                nest.Treasure.RemoveChild(child);
                child.QueueFree();
            }
            if (nest.Kind >= 0)
                SouvenirModels.Build(nest.Treasure, nest.Kind, 1.1f);
            nest.Treasure.Visible = false;
            nest.Turtle.Visible = nest.Kind >= 0;
            nest.Turtle.Position = nest.Home;
        }
    }

    /// <summary>Whether a turtle sits in nest <paramref name="nest"/>, waiting for its question.</summary>
    internal bool Waiting(int nest) => nests[nest].Kind >= 0 && nests[nest].Gone < 0;

    internal int KindIn(int nest) => nests[nest].Kind;

    internal Vector3 NestAt(int nest) => nests[nest].Home;

    /// <summary>The question of a nest is solved: its turtle crawls off to the sea and the treasure sparkles.</summary>
    internal void Reveal(int nest)
    {
        if (nests[nest].Kind < 0 || nests[nest].Gone >= 0)
            return;
        nests[nest].Gone = 0;
        nests[nest].Treasure.Visible = true;
    }

    internal void Tick(float delta)
    {
        time += delta;
        foreach (var nest in nests)
        {
            if (nest.Gone >= 0)
            {
                nest.Gone += delta;
                float u = Mathf.Clamp(nest.Gone / 5, 0, 1);
                var p = nest.Home.Lerp(nest.Sea with { Y = nest.Home.Y }, u);
                p.Y = Mathf.Lerp(nest.Home.Y, ValleyBorders.SeaLevel - 1, u * u);
                nest.Turtle.Position = p;
                nest.Turtle.Visible = u < 1;
                nest.Treasure.Position = nest.Home + Vector3.Up * (.3f + Mathf.Sin(time * 2) * .15f);
                nest.Treasure.Rotation = new Vector3(0, time * .8f, 0);
            }
            else if (nest.Turtle.Visible)
                nest.Turtle.Position = nest.Home + Vector3.Up * (Mathf.Sin(time * 1.4f) * .03f);
        }
    }
}
