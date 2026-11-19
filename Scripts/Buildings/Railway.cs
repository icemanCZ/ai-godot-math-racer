using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The little railway: a loop of track round the whole playground, a station with a platform beside the road, a tunnel
/// in a green hill and a steam train with two wagons that runs round it on its own. It is raised with six deliveries of
/// goods to the same ring as the playground, and it lies in the yard's frame (+Z away from the road, +X to the right),
/// inside the ground that the playground already clears, so nothing else in a valley moves for it.
/// </summary>
public sealed partial class Railway : Node3D
{
    public const int Trips = RailwayPlans.Trips;

    // The loop is a rounded rectangle in the frame of the yard (x across, z along), outside every toy and inside the
    // ground that is cleared for the playground (14 m to each side, from 6 m before the ring to 21 m behind it).
    internal const float HalfWidth = 12,
        FrontZ = -3.5f,
        RearZ = 19.8f,
        Corner = 3.5f;

    private const float BedHalf = .95f,
        BedTop = .06f,
        SleeperTop = .12f,
        RailLateral = .4f,
        RailTop = .19f,
        SampleStep = .5f;

    private const float TrainSpeed = 2.7f,
        StationSpeed = 1.3f;

    // The station: a raised area with the platform beside the track and a little brick house behind it.
    private const float StationX0 = 3.6f,
        StationX1 = 8.8f,
        StationZ0 = -2.4f,
        StationZ1 = 2.6f,
        HouseX0 = 4.4f,
        HouseX1 = 8.6f,
        HouseZ0 = -.4f,
        HouseZ1 = 2.4f,
        HouseHeight = 2.5f;

    // The tunnel hill over the front straight, to the left of the path to the ring.
    private const float TunnelX0 = -7.9f,
        TunnelX1 = -3.5f,
        TunnelRadius = 2f;

    /// <summary>How far from the yard a car makes the train whistle.</summary>
    private const float WhistleDistance = 36;

    private sealed class TrainCar
    {
        public Node3D Root = null!;
        public float Offset;
        public float WheelRadius;
        public int After;
        public readonly List<Node3D> Wheels = new();
    }

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D[] stages = new Node3D[Trips];
    private readonly List<CollisionShape3D>[] solids = new List<CollisionShape3D>[Trips];
    private readonly List<(int Stage, Character Who)> children = new();
    private readonly List<TrainCar> cars = new();
    private readonly MeshInstance3D[] puffs = new MeshInstance3D[9];
    private readonly float[] puffAge = new float[9];

    private Node3D marks = null!;
    private Label3D sign = null!;
    private Label3D whistle = null!;
    private Vector3[] track = Array.Empty<Vector3>();
    private float step;
    private float stationS,
        whistleS;
    private float stationTop;
    private float time;
    private float cheerTimer = 2;
    private float puffTimer;
    private float whistleTimer;
    private float engineS;
    private float travel;
    private bool wasNear;
    private Character? waiter;

    internal PlaygroundSite Site { get; }

    /// <summary>Deliveries made so far.</summary>
    public int Stage { get; private set; }

    public bool Complete => Stage >= Trips;

    /// <summary>The stationmaster who sends the trailer and thanks the driver.</summary>
    internal Character Helper { get; set; } = null!;

    /// <summary>The ring where the trailer tips out its load: the playground's own.</summary>
    internal Vector3 Drop => Site.Position;

    internal Vector3 Road => Site.Road;

    /// <summary>The middle of the loop, for celebrations and the camera.</summary>
    internal Vector3 Focus => ToGlobal(new Vector3(0, 2.5f, 8));

    /// <summary>Where the stationmaster stands, on the platform at the end nearest the road.</summary>
    internal Vector3 HelperSpot => Position + basis * new Vector3(7.7f, stationTop, -1.5f);

    /// <summary>The loop of track, in the yard's frame, one point every half metre (heights are the ground's).</summary>
    internal IReadOnlyList<Vector3> Track => track;

    internal float TrackLength => track.Length * step;

    /// <summary>How far the engine has gone round the loop, which a check reads to see that the train moves.</summary>
    internal float EngineDistance => engineS;

    internal bool TrainRunning => Stage >= RailwayPlans.EngineAfter;

    /// <summary>The wagons and the engine that run now.</summary>
    internal int CarsRunning
    {
        get
        {
            int count = 0;
            foreach (var car in cars)
                if (Stage >= car.After)
                    count++;
            return count;
        }
    }

    internal Vector3 EnginePosition => cars.Count > 0 ? cars[0].Root.GlobalPosition : Position;

    internal Vector3 CarPosition(int index) => cars[index].Root.GlobalPosition;

    /// <summary>The animals who ride or wait on the platform, once their part is built.</summary>
    internal int PeopleAboard
    {
        get
        {
            int count = 0;
            foreach (var (stage, _) in children)
                if (stage < Stage)
                    count++;
            return count;
        }
    }

    internal bool WhistleShown => whistleTimer > 0;

    internal Railway(PlaygroundSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "Railway";
        Position = site.Position;
        float yaw = Mathf.Atan2(site.Inward.X, site.Inward.Z);
        Rotation = new Vector3(0, yaw, 0);
        basis = new Basis(Vector3.Up, yaw);
        for (int i = 0; i < Trips; i++)
            solids[i] = new List<CollisionShape3D>();
        stationTop = Highest(StationX0, StationX1, StationZ0, StationZ1) + .3f;
    }

    /// <summary>Shows the parts of <paramref name="stage"/> delivered loads; a restored game simply has them.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Trips);
        if (IsInsideTree())
            Refresh();
    }

    // ----- the ground ----------------------------------------------------------------------------------------------

    /// <summary>Height above the ring of the ground below a point given in the yard's own frame.</summary>
    private float Floor(float x, float z)
    {
        var global = Position + basis * new Vector3(x, 0, z);
        return ground.Height(global.X, global.Z) - Position.Y;
    }

    private float Highest(float x0, float x1, float z0, float z1)
    {
        float top = float.MinValue;
        for (int i = 0; i <= 3; i++)
        for (int j = 0; j <= 3; j++)
            top = Mathf.Max(top, Floor(Mathf.Lerp(x0, x1, i / 3f), Mathf.Lerp(z0, z1, j / 3f)));
        return top;
    }

    private void Solid(int stage, Vector3 center, Vector3 size)
    {
        var body = Obstacles.Add(stages[stage], center, new BoxShape3D { Size = size });
        var shape = (CollisionShape3D)body.GetChild(0);
        shape.Disabled = stage >= Stage;
        solids[stage].Add(shape);
    }

    private Character Person(int stage, Node3D parent, Vector3 position, float yaw, int kind, string fur, float size)
    {
        var person = Character.Create("", "", kind, fur);
        person.Position = position;
        person.Rotation = new Vector3(0, yaw, 0);
        person.Scale = Vector3.One * size;
        person.Guide(false);
        parent.AddChild(person);
        children.Add((stage, person));
        return person;
    }

    // ----- the loop ------------------------------------------------------------------------------------------------

    /// <summary>Walks the rounded rectangle once and keeps a point every half metre, with the ground's height.</summary>
    private void BuildTrack()
    {
        var dense = new List<Vector2>();
        void Straight(Vector2 from, Vector2 to)
        {
            int n = Mathf.Max(1, (int)(from.DistanceTo(to) / .05f));
            for (int i = 0; i < n; i++)
                dense.Add(from.Lerp(to, i / (float)n));
        }
        void Arc(Vector2 center, float from, float to)
        {
            int n = (int)(Corner * Mathf.Abs(to - from) / .05f);
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Lerp(from, to, i / (float)n);
                dense.Add(center + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * Corner);
            }
        }
        float left = -HalfWidth + Corner,
            right = HalfWidth - Corner,
            front = FrontZ + Corner,
            rear = RearZ - Corner;
        Straight(new(left, FrontZ), new(right, FrontZ));
        Arc(new(right, front), -Mathf.Pi / 2, 0);
        Straight(new(HalfWidth, front), new(HalfWidth, rear));
        Arc(new(right, rear), 0, Mathf.Pi / 2);
        Straight(new(right, RearZ), new(left, RearZ));
        Arc(new(left, rear), Mathf.Pi / 2, Mathf.Pi);
        Straight(new(-HalfWidth, rear), new(-HalfWidth, front));
        Arc(new(left, front), Mathf.Pi, Mathf.Pi * 1.5f);

        var cumulative = new float[dense.Count + 1];
        for (int i = 0; i < dense.Count; i++)
            cumulative[i + 1] = cumulative[i] + dense[i].DistanceTo(dense[(i + 1) % dense.Count]);
        float length = cumulative[^1];
        int count = Mathf.RoundToInt(length / SampleStep);
        step = length / count;
        var flat = new Vector2[count];
        int at = 0;
        for (int k = 0; k < count; k++)
        {
            float target = k * step;
            while (cumulative[at + 1] < target)
                at++;
            float f = (target - cumulative[at]) / Mathf.Max(.0001f, cumulative[at + 1] - cumulative[at]);
            flat[k] = dense[at].Lerp(dense[(at + 1) % dense.Count], f);
        }
        // The ground is smoothed so that the rails and the train follow its shape without jolts.
        var height = new float[count];
        for (int k = 0; k < count; k++)
            height[k] = Floor(flat[k].X, flat[k].Y);
        for (int pass = 0; pass < 3; pass++)
        {
            var next = new float[count];
            for (int k = 0; k < count; k++)
            {
                float sum = 0;
                for (int d = -3; d <= 3; d++)
                    sum += height[(k + d + count) % count];
                next[k] = sum / 7;
            }
            height = next;
        }
        track = new Vector3[count];
        for (int k = 0; k < count; k++)
            track[k] = new Vector3(flat[k].X, height[k], flat[k].Y);
        stationS = DistanceAlongTo(6.2f, FrontZ);
        whistleS = DistanceAlongTo(TunnelX1, FrontZ);
    }

    /// <summary>The distance along the loop of the point nearest to (x, z) in the yard's frame.</summary>
    private float DistanceAlongTo(float x, float z)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        for (int k = 0; k < track.Length; k++)
        {
            float d = new Vector2(track[k].X - x, track[k].Z - z).LengthSquared();
            if (d < bestDistance)
                (best, bestDistance) = (k, d);
        }
        return best * step;
    }

    private float Wrap(float s)
    {
        float length = TrackLength;
        s %= length;
        return s < 0 ? s + length : s;
    }

    /// <summary>The point of the loop (on the bed, not on the rails) at a distance along it.</summary>
    private Vector3 PointAt(float s)
    {
        s = Wrap(s);
        int i = Mathf.Min((int)(s / step), track.Length - 1);
        float f = s / step - i;
        return track[i].Lerp(track[(i + 1) % track.Length], f);
    }

    private Vector3 SideAt(int k)
    {
        var tangent = track[(k + 1) % track.Length] - track[(k - 1 + track.Length) % track.Length];
        tangent.Y = 0;
        tangent = tangent.Normalized();
        return Vector3.Up.Cross(tangent);
    }

    private static Material DoubleSided(string color)
    {
        var material = (StandardMaterial3D)Art.Mat(color).Duplicate();
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        return material;
    }

    private static void Quad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        foreach (var p in new[] { a, b, c, a, c, d })
            st.AddVertex(p);
    }

    private static void BoxFaces(SurfaceTool st, Vector3 center, Vector3 a, Vector3 b, Vector3 u)
    {
        Vector3 P(int sa, int sb, int su) => center + a * sa + b * sb + u * su;
        Quad(st, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));
        Quad(st, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1));
        Quad(st, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1));
        Quad(st, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1));
        Quad(st, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1));
        Quad(st, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1));
    }

    private static void Commit(Node3D parent, SurfaceTool st, string color, string name)
    {
        st.GenerateNormals();
        parent.AddChild(
            new MeshInstance3D
            {
                Name = name,
                Mesh = st.Commit(),
                MaterialOverride = DoubleSided(color),
            }
        );
    }

    /// <summary>Stage 1: the bed of gravel that runs all the way round.</summary>
    private void BuildBed(Node3D stage)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        int n = track.Length;
        for (int k = 0; k < n; k++)
        {
            int next = (k + 1) % n;
            var a = track[k] + Vector3.Up * BedTop;
            var b = track[next] + Vector3.Up * BedTop;
            Quad(
                st,
                a + SideAt(k) * BedHalf,
                b + SideAt(next) * BedHalf,
                b - SideAt(next) * BedHalf,
                a - SideAt(k) * BedHalf
            );
        }
        Commit(stage, st, "b3ad9d", "RailBed");
    }

    /// <summary>Stage 2: a sleeper every half metre and two shining rails.</summary>
    private void BuildRails(Node3D stage)
    {
        var sleepers = new SurfaceTool();
        sleepers.Begin(Mesh.PrimitiveType.Triangles);
        var rails = new SurfaceTool();
        rails.Begin(Mesh.PrimitiveType.Triangles);
        int n = track.Length;
        for (int k = 0; k < n; k++)
        {
            int next = (k + 1) % n;
            var side = SideAt(k);
            var along = (track[next] - track[(k - 1 + n) % n]).Normalized();
            BoxFaces(
                sleepers,
                track[k] + Vector3.Up * ((BedTop + SleeperTop) / 2),
                along * .1f,
                side * .78f,
                Vector3.Up * ((SleeperTop - BedTop) / 2)
            );
            foreach (float lateral in new[] { -RailLateral, RailLateral })
            {
                var a = track[k];
                var b = track[next];
                var sa = SideAt(k);
                var sb = SideAt(next);
                var topA = Vector3.Up * RailTop;
                var lowA = Vector3.Up * SleeperTop;
                const float half = .04f;
                Quad(
                    rails,
                    a + sa * (lateral - half) + topA,
                    b + sb * (lateral - half) + topA,
                    b + sb * (lateral + half) + topA,
                    a + sa * (lateral + half) + topA
                );
                foreach (float edge in new[] { -half, half })
                    Quad(
                        rails,
                        a + sa * (lateral + edge) + lowA,
                        b + sb * (lateral + edge) + lowA,
                        b + sb * (lateral + edge) + topA,
                        a + sa * (lateral + edge) + topA
                    );
            }
        }
        Commit(stage, sleepers, "9c6f45", "RailSleepers");
        Commit(stage, rails, "c9d1d6", "RailSteel");
        BuildCrossing(stage);
    }

    /// <summary>Crossbuck signs on both sides of the path to the ring, where the track crosses it.</summary>
    private void BuildCrossing(Node3D stage)
    {
        foreach (float x in new[] { -2.6f, 2.6f })
        {
            float y = Floor(x, FrontZ + 1.2f);
            Art.Cylinder(stage, new(x, y + .8f, FrontZ + 1.2f), .05f, .05f, 1.6f, "ffffff");
            foreach (float turn in new[] { 38f, -38f })
            {
                var board = Art.Block(stage, new(x, y + 1.6f, FrontZ + 1.2f), new(.95f, .15f, .035f), "d9432f");
                board.RotationDegrees = new(0, 0, turn);
                var stripe = Art.Block(stage, new(x, y + 1.6f, FrontZ + 1.2f + .02f), new(.9f, .06f, .03f), "fff5df");
                stripe.RotationDegrees = new(0, 0, turn);
            }
        }
    }

    // ----- the station ---------------------------------------------------------------------------------------------

    /// <summary>Stage 3: a raised platform beside the track and a little brick station with a clock.</summary>
    private void BuildStation(Node3D stage)
    {
        float top = stationTop;
        float cx = (StationX0 + StationX1) / 2,
            cz = (StationZ0 + StationZ1) / 2;
        Art.Block(stage, new(cx, top - 2.5f, cz), new(StationX1 - StationX0, 5, StationZ1 - StationZ0), "b7ab96");
        Art.Block(stage, new(cx, top + .01f, StationZ0 + .14f), new(StationX1 - StationX0, .04f, .28f), "f2c14e");
        Art.Block(stage, new(cx, top - .3f, StationZ0 + .02f), new(StationX1 - StationX0, .6f, .06f), "9c9382");

        float depth = HouseZ1 - HouseZ0,
            hz = (HouseZ0 + HouseZ1) / 2;
        const float thick = .2f;
        void Block(float x0, float x1, float y0, float y1, float z, string color = "c9795c") =>
            Art.Block(stage, new((x0 + x1) / 2, top + (y0 + y1) / 2, z), new(x1 - x0, y1 - y0, thick), color);
        Block(HouseX0, HouseX1, 0, HouseHeight, HouseZ1 - thick / 2);
        foreach (float x in new[] { HouseX0 + thick / 2, HouseX1 - thick / 2 })
            Art.Block(stage, new(x, top + HouseHeight / 2, hz), new(thick, HouseHeight, depth), "c9795c");
        // The front wall towards the track has a door in the middle and a window on each side.
        float z = HouseZ0 + thick / 2;
        float[] edges = { HouseX0, 4.9f, 5.7f, 6.0f, 7.0f, 7.3f, 8.1f, HouseX1 };
        Block(edges[0], edges[1], 0, HouseHeight, z);
        Block(edges[1], edges[2], 0, .9f, z);
        Block(edges[1], edges[2], 1.7f, HouseHeight, z);
        Block(edges[2], edges[3], 0, HouseHeight, z);
        Block(edges[3], edges[4], 1.95f, HouseHeight, z);
        Block(edges[4], edges[5], 0, HouseHeight, z);
        Block(edges[5], edges[6], 0, .9f, z);
        Block(edges[5], edges[6], 1.7f, HouseHeight, z);
        Block(edges[6], edges[7], 0, HouseHeight, z);
        Art.Block(stage, new(6.5f, top + .95f, HouseZ0 + .16f), new(.92f, 1.9f, .06f), "7b4f32");
        Art.Ball(stage, new(6.2f, top + .95f, HouseZ0 + .1f), Vector3.One * .05f, "f5d36b");
        foreach (float wx in new[] { 5.3f, 7.7f })
        {
            Art.Window(stage, new(wx, top + 1.3f, HouseZ0 + .14f), new(.78f, .78f, .04f), "bfe6f2");
            Art.Block(stage, new(wx, top + .86f, HouseZ0 - .1f), new(1f, .1f, .26f), "a87b61");
            foreach (float flower in new[] { -.3f, 0, .3f })
                Art.Ball(stage, new(wx + flower, top + 1.0f, HouseZ0 - .1f), Vector3.One * .1f, "f56f8f");
        }
        // The clock over the door.
        Art.Cylinder(stage, new(6.5f, top + 2.2f, HouseZ0 - .05f), .3f, .3f, .08f, "fff5df").RotationDegrees = new(
            90,
            0,
            0
        );
        Art.Block(stage, new(6.5f, top + 2.28f, HouseZ0 - .1f), new(.04f, .2f, .02f), "30363a");
        Art.Block(stage, new(6.58f, top + 2.2f, HouseZ0 - .1f), new(.16f, .04f, .02f), "30363a");

        // The roof is a prism along the station: red tiles, a chimney and wide eaves.
        float ridge = top + HouseHeight + 1.0f,
            low = top + HouseHeight - .05f,
            x0 = HouseX0 - .35f,
            x1 = HouseX1 + .35f,
            front = HouseZ0 - .45f,
            back = HouseZ1 + .3f,
            middle = (HouseZ0 + HouseZ1) / 2;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var a = new Vector3(x0, low, front);
        var b = new Vector3(x1, low, front);
        var c = new Vector3(x0, ridge, middle);
        var d = new Vector3(x1, ridge, middle);
        var e = new Vector3(x0, low, back);
        var f = new Vector3(x1, low, back);
        Quad(st, a, b, d, c);
        Quad(st, c, d, f, e);
        foreach (var p in new[] { a, e, c, b, d, f })
            st.AddVertex(p);
        Commit(stage, st, "5f7f9b", "StationRoof");
        Art.Block(stage, new(8f, ridge + .15f, middle + .3f), new(.45f, 1f, .45f), "b8452e");
        Art.Block(stage, new(8f, ridge + .7f, middle + .3f), new(.55f, .1f, .55f), "8f3320");
        // The whole raised area blocks the car, so that it cannot climb onto the platform.
        Solid(2, new(cx, top + .55f, cz), new(StationX1 - StationX0, 3.6f, StationZ1 - StationZ0));
    }

    /// <summary>The pegs and rope that mark where the station will stand, until it is built.</summary>
    private void BuildMarks()
    {
        var corners = new Vector2[]
        {
            new(StationX0, StationZ0),
            new(StationX0, StationZ1),
            new(StationX1, StationZ1),
            new(StationX1, StationZ0),
        };
        var pegs = new Vector3[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            pegs[i] = new Vector3(corners[i].X, Floor(corners[i].X, corners[i].Y), corners[i].Y);
            Art.Cylinder(marks, pegs[i] + Vector3.Up * .5f, .07f, .06f, 1, "a77a59");
            Art.Ball(marks, pegs[i] + Vector3.Up * 1.02f, new(.11f, .07f, .11f), "d9432f");
        }
        for (int i = 0; i < 3; i++)
            Art.Beam(marks, pegs[i] + Vector3.Up * .8f, pegs[i + 1] + Vector3.Up * .8f, .035f, "f2ddb0");
    }

    /// <summary>The post with the sign of the station; it stands from the first day.</summary>
    private void BuildPost()
    {
        float x = StationX0 - .5f,
            z = StationZ0 - .1f;
        float y = Floor(x, z);
        Art.Cylinder(this, new(x, y + 1.7f, z), .09f, .11f, 3.4f, "b0855c");
        Art.Block(this, new(x, y + 3.4f, z), new(1.5f, .22f, .22f), "c39771");
        Art.Ball(this, new(x - .75f, y + 3.4f, z), new(.16f, .16f, .16f), "d9432f");
        Art.Ball(this, new(x + .75f, y + 3.4f, z), new(.16f, .16f, .16f), "4f8fb8");
        sign = Art.Sign(this, new(x, y + 4.1f, z), "", 30);
    }

    /// <summary>Stage 4: a bench, a lamp and a semaphore beside the station, a traveller on the platform, the first wagon.</summary>
    private void BuildPlatformThings(Node3D stage)
    {
        float top = stationTop;
        // A bench against the wall of the station, with its back to it.
        Art.Block(stage, new(5.4f, top + .38f, HouseZ0 - .45f), new(1.7f, .08f, .42f), "bc8b60");
        Art.Block(stage, new(5.4f, top + .75f, HouseZ0 - .24f), new(1.7f, .4f, .06f), "cd9e70");
        foreach (float leg in new[] { -.75f, .75f })
            Art.Block(stage, new(5.4f + leg, top + .19f, HouseZ0 - .45f), new(.08f, .38f, .36f), "526f71");
        // A lamp post on the platform.
        Art.Cylinder(stage, new(8.3f, top + 1.3f, -2.0f), .05f, .07f, 2.6f, "526f71");
        Art.Block(stage, new(8.3f, top + 2.7f, -2.0f), new(.36f, .44f, .36f), "ffe3a0");
        Art.Cylinder(stage, new(8.3f, top + 3.0f, -2.0f), .3f, .04f, .14f, "526f71");
        // A semaphore beside the track, outside the loop, whose red arm points up.
        float sy = Floor(1.8f, FrontZ - 1.5f);
        Art.Cylinder(stage, new(1.8f, sy + 1.6f, FrontZ - 1.5f), .06f, .08f, 3.2f, "526f71");
        var arm = Art.Block(stage, new(2.1f, sy + 3.1f, FrontZ - 1.5f), new(.9f, .14f, .05f), "d9432f");
        arm.RotationDegrees = new(0, 0, 28);
        Art.Block(stage, new(2.35f, sy + 3.2f, FrontZ - 1.5f), new(.2f, .1f, .06f), "fff5df").RotationDegrees = new(
            0,
            0,
            28
        );
        Art.Ball(stage, new(1.8f, sy + 2.6f, FrontZ - 1.46f), Vector3.One * .1f, "6bd16b");
        waiter = Person(3, stage, new(4.4f, top, -1.5f), Mathf.Pi, 2, "c5a9db", .34f);
    }

    /// <summary>Stage 5: a tunnel through a green hill, with a brick face at each end and a dark mouth.</summary>
    private void BuildTunnel(Node3D stage)
    {
        float length = TunnelX1 - TunnelX0,
            cx = (TunnelX0 + TunnelX1) / 2;
        float baseY = Highest(TunnelX0, TunnelX1, FrontZ - TunnelRadius, FrontZ + TunnelRadius) - .3f;
        float railY = track[(int)(DistanceAlongTo(cx, FrontZ) / step) % track.Length].Y;
        float centre = Mathf.Max(baseY, railY) + .1f;
        Art.Cylinder(stage, new(cx, centre, FrontZ), TunnelRadius, TunnelRadius, length, "6fae5c").RotationDegrees =
            new(0, 0, 90);
        foreach (float end in new[] { TunnelX0, TunnelX1 })
        {
            float outward = end < cx ? -1 : 1;
            Art.Cylinder(
                stage,
                new(end + outward * .1f, centre, FrontZ),
                TunnelRadius + .12f,
                TunnelRadius + .12f,
                .3f,
                "c9795c"
            ).RotationDegrees = new(0, 0, 90);
            float mouth = end + outward * .27f;
            Art.Block(stage, new(mouth, centre + .35f, FrontZ), new(.06f, 1.4f, 1.5f), "23272c");
            Art.Cylinder(stage, new(mouth, centre + 1.05f, FrontZ), .75f, .75f, .06f, "23272c").RotationDegrees = new(
                0,
                0,
                90
            );
        }
        // A few bushes on the hill.
        foreach (var (x, z, size) in new[] { (-6.9f, -.2f, .55f), (-5.2f, .3f, .45f), (-6.1f, -.9f, .38f) })
            Art.Ball(stage, new(x, centre + TunnelRadius * .85f, FrontZ + z), new(size, size * .7f, size), "5aa06a");
        Solid(4, new(cx, centre + .4f, FrontZ), new(length + .4f, TunnelRadius * 2 + 1, TunnelRadius * 2));
    }

    private static readonly string[] FlagColors = { "e2563b", "f2c14e", "4f8fb8", "5aa86f", "d9718f", "ffffff" };

    /// <summary>Stage 6: a flagpole and a string of pennants from the station roof to the lamp, and the second wagon.</summary>
    private void BuildFlags(Node3D stage)
    {
        float top = stationTop;
        float py = Floor(8.7f, -2.2f);
        Art.Cylinder(stage, new(8.7f, py + 2.2f, -2.2f), .05f, .07f, 4.4f, "ffffff");
        Art.Ball(stage, new(8.7f, py + 4.45f, -2.2f), Vector3.One * .1f, "f2c14e");
        Art.Block(stage, new(9.15f, py + 4.0f, -2.2f), new(.9f, .55f, .03f), "d9432f");
        Art.Block(stage, new(9.15f, py + 4.0f, -2.19f), new(.9f, .1f, .03f), "fff5df");
        var from = new Vector3(HouseX0 - .3f, top + HouseHeight + .05f, HouseZ0 - .45f);
        var to = new Vector3(8.7f, py + 4.2f, -2.2f);
        Art.Beam(stage, from, to, .03f, "f2ddb0");
        const int count = 14;
        for (int i = 1; i < count; i++)
        {
            float t = i / (float)count;
            var p = from.Lerp(to, t) + Vector3.Down * (.3f + Mathf.Sin(t * Mathf.Pi) * .2f);
            var pennant = Art.Block(stage, p, new(.28f, .34f, .03f), FlagColors[i % FlagColors.Length]);
            pennant.Rotation = new Vector3(0, 0, Mathf.Pi / 4);
        }
        Person(5, stage, new(5.9f, top, -1.5f), Mathf.Pi, 1, "e9a16a", .34f);
    }

    // ----- the train -----------------------------------------------------------------------------------------------

    private TrainCar AddCar(float offset, int after, float wheelRadius)
    {
        var car = new TrainCar
        {
            Root = new Node3D { Name = "TrainCar" + cars.Count },
            Offset = offset,
            WheelRadius = wheelRadius,
            After = after,
        };
        AddChild(car.Root);
        cars.Add(car);
        return car;
    }

    private static void Wheel(TrainCar car, float x, float z, string color)
    {
        var pivot = new Node3D { Position = new(x, car.WheelRadius, z) };
        car.Root.AddChild(pivot);
        Art.Cylinder(pivot, Vector3.Zero, car.WheelRadius, car.WheelRadius, .09f, "30363a").RotationDegrees = new(
            0,
            0,
            90
        );
        Art.Cylinder(
            pivot,
            new(Mathf.Sign(x) * .05f, 0, 0),
            car.WheelRadius * .55f,
            car.WheelRadius * .55f,
            .05f,
            color
        ).RotationDegrees = new(0, 0, 90);
        Art.Block(pivot, new(Mathf.Sign(x) * .08f, 0, 0), new(.03f, car.WheelRadius * 1.5f, .05f), color);
        car.Wheels.Add(pivot);
    }

    /// <summary>The engine: a red boiler, a black chimney with a golden rim, a cab with a fox at the controls.</summary>
    private void BuildEngine()
    {
        var car = AddCar(0, RailwayPlans.EngineAfter, .26f);
        var r = car.Root;
        Art.Block(r, new(0, .38f, 0), new(1f, .14f, 2.6f), "30363a");
        foreach (float x in new[] { -.46f, .46f })
        foreach (float z in new[] { -.75f, -.05f, .65f })
            Wheel(car, x, z, "d9432f");
        Art.Cylinder(r, new(0, .86f, .3f), .38f, .38f, 1.6f, "d9432f").RotationDegrees = new(90, 0, 0);
        foreach (float z in new[] { .0f, .55f })
            Art.Cylinder(r, new(0, .86f, z), .395f, .395f, .06f, "f2c14e").RotationDegrees = new(90, 0, 0);
        Art.Cylinder(r, new(0, .86f, 1.18f), .41f, .41f, .3f, "3b4247").RotationDegrees = new(90, 0, 0);
        Art.Cylinder(r, new(0, 1.42f, 1.12f), .12f, .17f, .5f, "30363a");
        Art.Cylinder(r, new(0, 1.68f, 1.12f), .22f, .22f, .07f, "f2c14e");
        Art.Ball(r, new(0, 1.24f, .55f), new(.17f, .15f, .17f), "f2c14e");
        Art.Ball(r, new(0, .88f, 1.34f), Vector3.One * .1f, "ffe08a");
        Art.Block(r, new(0, .36f, 1.45f), new(.9f, .1f, .4f), "3b4247").RotationDegrees = new(-18, 0, 0);
        // The cab: low sides, a back and a front wall and a flat roof on four posts.
        foreach (float x in new[] { -.47f, .47f })
        {
            Art.Block(r, new(x, .75f, -.85f), new(.05f, .5f, .85f), "d9432f");
            foreach (float z in new[] { -1.2f, -.45f })
                Art.Block(r, new(x, 1.12f, z), new(.07f, .75f, .07f), "b8321f");
        }
        Art.Block(r, new(0, .88f, -1.25f), new(.96f, .95f, .05f), "d9432f");
        Art.Block(r, new(0, .88f, -.42f), new(.96f, .95f, .05f), "d9432f");
        Art.Block(r, new(0, 1.52f, -.82f), new(1.12f, .07f, 1.05f), "3b4247");
        Person(2, r, new(0, .44f, -.82f), 0, 1, "e9a16a", .3f);
    }

    /// <summary>A wagon without a roof, with benches and two small passengers who wave to both sides.</summary>
    private void BuildWagon(
        float offset,
        int after,
        string color,
        (int Kind, string Fur) first,
        (int Kind, string Fur) second
    )
    {
        var car = AddCar(offset, after, .22f);
        var r = car.Root;
        Art.Block(r, new(0, .36f, 0), new(1f, .12f, 1.9f), "30363a");
        foreach (float x in new[] { -.46f, .46f })
        foreach (float z in new[] { -.55f, .55f })
            Wheel(car, x, z, color);
        Art.Block(r, new(0, .46f, 0), new(1f, .06f, 1.9f), "9c6f45");
        foreach (float x in new[] { -.47f, .47f })
        {
            Art.Block(r, new(x, .74f, 0), new(.06f, .5f, 1.9f), color);
            Art.Block(r, new(x, 1.0f, 0), new(.09f, .05f, 1.9f), "fff5df");
        }
        foreach (float z in new[] { -.92f, .92f })
            Art.Block(r, new(0, .74f, z), new(.88f, .5f, .06f), color);
        foreach (float z in new[] { -.78f, .78f })
            Art.Block(r, new(0, .56f, z), new(.7f, .08f, .3f), "bc8b60");
        foreach (float z in new[] { -1.07f, 1.07f })
            Art.Block(r, new(0, .4f, z), new(.1f, .1f, .35f), "30363a");
        Person(after - 1, r, new(.0f, .5f, -.4f), Mathf.Pi / 2, first.Kind, first.Fur, .27f);
        Person(after - 1, r, new(.0f, .5f, .4f), -Mathf.Pi / 2, second.Kind, second.Fur, .27f);
    }

    private void BuildTrain()
    {
        BuildEngine();
        BuildWagon(2.5f, RailwayPlans.FirstWagonAfter, "f2c14e", (0, "f3e8d5"), (1, "e8bf73"));
        BuildWagon(4.65f, RailwayPlans.SecondWagonAfter, "4f8fb8", (2, "97c9b0"), (0, "d9c7f0"));
        for (int i = 0; i < puffs.Length; i++)
        {
            var material = new StandardMaterial3D
            {
                AlbedoColor = new Color(1, 1, 1, .7f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            puffs[i] = new MeshInstance3D
            {
                Name = "Steam" + i,
                Mesh = new SphereMesh
                {
                    Radius = 1,
                    Height = 2,
                    RadialSegments = 8,
                    Rings = 4,
                },
                MaterialOverride = material,
                Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            puffAge[i] = -1;
            AddChild(puffs[i]);
        }
        whistle = new Label3D
        {
            Name = "Whistle",
            Text = "Tuuut!",
            FontSize = 56,
            PixelSize = .012f,
            OutlineSize = 10,
            Modulate = new Color("fff5d9"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Visible = false,
            Layers = Art.SignLayer,
        };
        AddChild(whistle);
    }

    // ----- building ------------------------------------------------------------------------------------------------

    public override void _Ready()
    {
        BuildTrack();
        marks = new Node3D { Name = "RailwayMarks" };
        AddChild(marks);
        BuildMarks();
        BuildPost();
        for (int i = 0; i < Trips; i++)
        {
            stages[i] = new Node3D { Name = "RailwayStage" + i, Visible = false };
            AddChild(stages[i]);
        }
        BuildBed(stages[0]);
        BuildRails(stages[1]);
        BuildStation(stages[2]);
        BuildPlatformThings(stages[3]);
        BuildTunnel(stages[4]);
        BuildFlags(stages[5]);
        BuildTrain();
        Refresh();
    }

    private void Refresh()
    {
        for (int i = 0; i < Trips; i++)
        {
            stages[i].Visible = i < Stage;
            foreach (var shape in solids[i])
                shape.Disabled = i >= Stage;
        }
        marks.Visible = Stage < RailwayPlans.EngineAfter;
        foreach (var car in cars)
            car.Root.Visible = Stage >= car.After;
        if (Stage >= RailwayPlans.EngineAfter)
            PlaceTrain(0);
        else
            foreach (var puff in puffs)
                puff.Visible = false;
        sign.Text = Complete ? "✓  Nádraží" : $"Nádraží  {Stage} / {Trips}";
    }

    // ----- life ----------------------------------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        if (Helper != null && Helper.AnimationPaused)
            return;
        float dt = (float)delta;
        time += dt;
        if (TrainRunning)
        {
            MoveTrain(dt);
            TickSteam(dt);
        }
        if (whistleTimer > 0)
        {
            whistleTimer -= dt;
            whistle.Visible = whistleTimer > 0;
        }
        cheerTimer -= dt;
        if (cheerTimer <= 0)
        {
            cheerTimer = 4.5f;
            if (Stage > 3)
                waiter?.Celebrate();
        }
    }

    private void MoveTrain(float dt)
    {
        float behind = Wrap(engineS - stationS + TrackLength / 2) - TrackLength / 2;
        float speed = Mathf.Lerp(StationSpeed, TrainSpeed, Mathf.Clamp(Mathf.Abs(behind) / 7f, 0, 1));
        float before = engineS;
        float moved = speed * dt;
        engineS = Wrap(engineS + moved);
        travel += moved;
        // The whistle sounds as the engine comes out of the tunnel, on its way to the crossing.
        float since = Wrap(engineS - whistleS);
        float sinceBefore = Wrap(before - whistleS);
        if (since < sinceBefore)
            Whistle();
        PlaceTrain(travel);
    }

    private void PlaceTrain(float rolled)
    {
        foreach (var car in cars)
        {
            float s = engineS - car.Offset;
            var position = PointAt(s) + Vector3.Up * RailTop;
            var forward = PointAt(s + .6f) - PointAt(s - .6f);
            if (forward.LengthSquared() < .0001f)
                continue;
            car.Root.Basis = Basis.LookingAt(-forward.Normalized(), Vector3.Up);
            car.Root.Position = position;
            foreach (var wheel in car.Wheels)
                wheel.Rotation = new Vector3((rolled - car.Offset) / car.WheelRadius, 0, 0);
        }
    }

    /// <summary>The train whistles: "Tuuut!" over the engine and everyone aboard waves.</summary>
    internal void Whistle()
    {
        if (!TrainRunning)
            return;
        whistleTimer = 2.2f;
        whistle.Visible = true;
        PositionWhistle();
        foreach (var (stage, who) in children)
            if (stage < Stage)
                who.Celebrate();
    }

    private void PositionWhistle() => whistle.Position = ToLocal(cars[0].Root.ToGlobal(new Vector3(0, 2.6f, .6f)));

    private void TickSteam(float dt)
    {
        if (whistleTimer > 0)
            PositionWhistle();
        puffTimer -= dt;
        if (puffTimer <= 0)
        {
            puffTimer = .28f;
            for (int i = 0; i < puffs.Length; i++)
                if (puffAge[i] < 0)
                {
                    puffAge[i] = 0;
                    puffs[i].Position = ToLocal(cars[0].Root.ToGlobal(new Vector3(0, 1.8f, 1.12f)));
                    puffs[i].Visible = true;
                    break;
                }
        }
        const float life = 2.4f;
        for (int i = 0; i < puffs.Length; i++)
        {
            if (puffAge[i] < 0)
                continue;
            puffAge[i] += dt;
            float t = puffAge[i] / life;
            if (t >= 1)
            {
                puffAge[i] = -1;
                puffs[i].Visible = false;
                continue;
            }
            puffs[i].Position += new Vector3(.25f, 1.0f, .1f) * dt;
            puffs[i].Scale = Vector3.One * (.13f + .34f * t);
            ((StandardMaterial3D)puffs[i].MaterialOverride!).AlbedoColor = new Color(
                1,
                1,
                1,
                .72f * Mathf.Pow(1 - t, 1.3f)
            );
        }
    }

    /// <summary>The people cheer and the train whistles when the car comes near, each time it arrives.</summary>
    internal void Tick(Vector3 car)
    {
        var middle = ToGlobal(new Vector3(0, 0, 8));
        bool near = new Vector2(car.X - middle.X, car.Z - middle.Z).Length() < WhistleDistance;
        if (near && !wasNear)
            Whistle();
        wasNear = near;
    }

    /// <summary>Everyone hops at once, as when the last part has been built.</summary>
    internal void CheerAll()
    {
        foreach (var (_, who) in children)
            who.Celebrate();
    }
}
