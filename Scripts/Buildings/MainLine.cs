using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The great railway across the valley (<c>docs/MAINLINE_PLAN.md</c>), raised by <see cref="MainLinePlans.Trips"/>
/// deliveries: the bed and platform of the hub, its station, then for each of three arms three stretches of track laid
/// one after another, each where the one before ends, the terminal station and the lamps and semaphores. Until a part
/// stands, the way it will take is marked by stakes, and a flag stands where the next load is tipped out. A finished arm
/// carries the car on the train.
/// </summary>
/// <remarks>
/// The node stands at the origin and everything is made at once in world coordinates; <see cref="SetStage"/> only shows
/// and hides the parts, so it may be called before the node is in the tree and as often as needed. The track has no
/// collision, so the car drives over it, and nothing of the ground changes: the bed rides on an embankment, and over deep
/// dips and the river on pillars.
/// </remarks>
public sealed partial class MainLine : Node3D
{
    internal const int Stages = MainLinePlans.Trips;

    private const float BedWidth = 3.2f,
        PlatformWidth = 4.2f,
        PlatformRise = .72f,
        PillarFrom = 1.25f;

    private const string Ballast = "9a9488",
        Sleeper = "5d4632",
        Steel = "6f7a82",
        Concrete = "c9c3b6",
        Brick = "b86a4c",
        RoofRed = "7a3a30",
        Stake = "ff7a1a";

    private readonly Terrain ground;
    private readonly LineRoute hubRoute;
    private readonly Node3D hubBed = new() { Name = "HubBed" };
    private readonly Node3D hubStation = new() { Name = "HubStation" };
    private readonly Node3D[][] sections;
    private readonly Node3D[] terminals;
    private readonly Node3D[] lamps;
    private readonly Node3D[][] stakes;
    private readonly Node3D[] terminalStakes;
    private readonly LineRoute[] toTerminal;
    private readonly LineRoute[] toHub;
    private readonly LineRoute hubTrack;
    private readonly Node3D flagPole = new() { Name = "NextLoadFlag" };
    private readonly List<(MeshInstance3D Mesh, string Color)> glows = new();
    private float time;

    internal LineSite Site { get; }

    /// <summary>The deliveries the line has had, 0 to <see cref="Stages"/>.</summary>
    internal int Stage { get; private set; }

    internal bool Complete => Stage >= Stages;

    /// <summary>The engine driver, who sends the trailer and rides the train.</summary>
    internal Character Engineer { get; set; } = null!;

    /// <summary>The forge that makes the rails.</summary>
    internal LineForge Forge { get; set; } = null!;

    /// <summary>The train: the engine, the tender and the flatbed that carries the car.</summary>
    internal LineTrain Train { get; }

    /// <summary>The track of the hub, from its rear end to the way out.</summary>
    internal LineRoute HubTrack => hubTrack;

    internal MainLine(LineSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "MainLine";
        hubRoute = HubPoints(site.Hub);
        hubTrack = hubRoute;
        AddChild(hubBed);
        AddChild(hubStation);
        sections = new Node3D[MainLinePlans.Arms][];
        stakes = new Node3D[MainLinePlans.Arms][];
        terminals = new Node3D[MainLinePlans.Arms];
        terminalStakes = new Node3D[MainLinePlans.Arms];
        lamps = new Node3D[MainLinePlans.Arms];
        toTerminal = new LineRoute[MainLinePlans.Arms];
        toHub = new LineRoute[MainLinePlans.Arms];
        BuildHub(site.Hub);
        for (int arm = 0; arm < MainLinePlans.Arms; arm++)
        {
            var route = site.Arms[arm];
            var terminal = site.Terminals[arm];
            sections[arm] = new Node3D[MainLinePlans.Sections];
            stakes[arm] = new Node3D[MainLinePlans.Sections];
            int goal = GoalIndex(arm);
            for (int k = 0; k < MainLinePlans.Sections; k++)
            {
                sections[arm][k] = new Node3D { Name = $"Arm{arm}Section{k}" };
                AddChild(sections[arm][k]);
                var (from, to) = SectionRange(arm, k);
                BuildTrack(sections[arm][k], route, from, to);
                stakes[arm][k] = new Node3D { Name = $"Arm{arm}Stakes{k}" };
                AddChild(stakes[arm][k]);
                BuildStakes(stakes[arm][k], route, from, to);
            }
            terminals[arm] = new Node3D { Name = $"Terminal{arm}" };
            AddChild(terminals[arm]);
            var terminalRoute = Slice(route, goal, route.Count - 1);
            BuildTrack(terminals[arm], route, goal, route.Count - 1);
            BuildStation(terminals[arm], terminalRoute, terminal, MainLinePlans.ArmSign(arm));
            terminalStakes[arm] = new Node3D { Name = $"Terminal{arm}Stakes" };
            AddChild(terminalStakes[arm]);
            BuildStakes(terminalStakes[arm], route, goal, route.Count - 1);
            lamps[arm] = new Node3D { Name = $"Arm{arm}Lamps" };
            AddChild(lamps[arm]);
            BuildLamps(lamps[arm], arm);
            // The ways of the train: from the hub's rear end over the trunk and the arm to the terminal, and back.
            toTerminal[arm] = LineRoute.Join(hubRoute, hubRoute.Length, route.FromHub());
            toHub[arm] = toTerminal[arm].Reversed();
        }
        BuildFlag();
        AddChild(flagPole);
        Train = new LineTrain { Name = "LineTrain" };
        AddChild(Train);
        SetStage(0);
    }

    /// <summary>The track of the hub: straight along its platform, the bed following the ground.</summary>
    private LineRoute HubPoints(LineStationSite hub)
    {
        var points = new List<Vector3>();
        int count = (int)MathF.Ceiling(hub.Half * 2 / LineRoute.Step);
        for (int i = 0; i <= count; i++)
        {
            var p = hub.Local(0, -hub.Half + i * LineRoute.Step);
            points.Add(ground.At(p.X, p.Z));
        }
        return LineRoute.Make(points, -1, null, 0);
    }

    /// <summary>The number of the point of an arm where its track proper ends and its terminal station begins.</summary>
    private int GoalIndex(int arm) => Site.Arms[arm].Nearest(Site.Terminals[arm].Start);

    /// <summary>The points of one stretch of an arm, a third of its track proper each.</summary>
    internal (int From, int To) SectionRange(int arm, int section)
    {
        int goal = GoalIndex(arm);
        return (goal * section / MainLinePlans.Sections, goal * (section + 1) / MainLinePlans.Sections);
    }

    private static LineRoute Slice(LineRoute route, int from, int to)
    {
        var points = new List<Vector3>();
        for (int i = from; i <= to; i++)
            points.Add(route.Ground(i));
        return LineRoute.Make(points, route.Parent, null, 0);
    }

    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, Stages);
        hubBed.Visible = Stage >= 1;
        hubStation.Visible = MainLinePlans.HubStands(Stage);
        for (int arm = 0; arm < MainLinePlans.Arms; arm++)
        {
            int built = MainLinePlans.SectionsBuilt(Stage, arm);
            for (int k = 0; k < MainLinePlans.Sections; k++)
            {
                sections[arm][k].Visible = k < built;
                // The stakes mark the way of a stretch, but only where the track of the hub or the stretch before it stands.
                stakes[arm][k].Visible = k >= built;
            }
            terminals[arm].Visible = MainLinePlans.TerminalStands(Stage, arm);
            terminalStakes[arm].Visible = !MainLinePlans.TerminalStands(Stage, arm);
            lamps[arm].Visible = MainLinePlans.ArmDone(Stage, arm);
        }
        Train.Visible = MainLinePlans.HubStands(Stage);
        flagPole.Visible = !Complete;
        if (!Complete)
        {
            var at = Front(Stage);
            flagPole.Position = ground.At(at.X, at.Z);
        }
        ParkAtHub();
    }

    /// <summary>Whether a stretch of an arm, the hub's bed, its station, a terminal or an arm's lamps are drawn now.</summary>
    internal bool SectionShown(int arm, int section) => sections[arm][section].Visible;

    internal bool StakesShown(int arm, int section) => stakes[arm][section].Visible;

    internal bool HubBedShown => hubBed.Visible;

    internal bool HubStationShown => hubStation.Visible;

    internal bool TerminalShown(int arm) => terminals[arm].Visible;

    internal bool LampsShown(int arm) => lamps[arm].Visible;

    /// <summary>The place where the load of a delivery is tipped out: the end of the track laid so far.</summary>
    internal Vector3 Front(int step)
    {
        var plan = MainLinePlans.Step(step);
        if (plan.Part is LinePart.HubBed or LinePart.HubStation)
            return Site.Hub.Drop;
        if (plan.Part is LinePart.Terminal or LinePart.Lamps)
            return Site.Terminals[plan.Arm].Drop;
        var route = Site.Arms[plan.Arm];
        // A little beyond the end of what stands, on the stretch to be laid.
        int index = Math.Min(SectionRange(plan.Arm, plan.Section).From + 2, route.Count - 1);
        return route.Ground(index);
    }

    /// <summary>The way the track runs where a delivery's load is tipped out, on the flat.</summary>
    internal Vector3 FrontWay(int step)
    {
        var plan = MainLinePlans.Step(step);
        if (plan.Part is LinePart.HubBed or LinePart.HubStation)
            return Site.Hub.Along;
        if (plan.Part is LinePart.Terminal or LinePart.Lamps)
            return Site.Terminals[plan.Arm].Along;
        var route = Site.Arms[plan.Arm];
        return route.Forward(SectionRange(plan.Arm, plan.Section).From * LineRoute.Step + 5);
    }

    /// <summary>Where the camera looks for a celebration at a delivery: the middle of the part it brought.</summary>
    internal Vector3 Focus(int step)
    {
        var plan = MainLinePlans.Step(step);
        if (plan.Part is LinePart.HubBed or LinePart.HubStation)
            return Site.Hub.Position + Vector3.Up * 4;
        var route = Site.Arms[plan.Arm];
        if (plan.Part == LinePart.Section)
        {
            var (from, to) = SectionRange(plan.Arm, plan.Section);
            return route.Ground((from + to) / 2) + Vector3.Up * 3;
        }
        return Site.Terminals[plan.Arm].Position + Vector3.Up * 4;
    }

    /// <summary>The height at which a character or the engine stands on the platform of a station.</summary>
    internal float StationTop(LineStationSite station)
    {
        var track = ReferenceEquals(station, Site.Hub) ? hubRoute : StationRoute(station);
        return track.At(track.Length * .45f).Y + PlatformRise;
    }

    private LineRoute StationRoute(LineStationSite station)
    {
        for (int arm = 0; arm < MainLinePlans.Arms; arm++)
            if (ReferenceEquals(station, Site.Terminals[arm]))
                return Slice(Site.Arms[arm], GoalIndex(arm), Site.Arms[arm].Count - 1);
        return hubRoute;
    }

    /// <summary>The way the train takes from the hub to the end of an arm, and back.</summary>
    internal LineRoute RideTo(int arm) => toTerminal[arm];

    internal LineRoute RideBack(int arm) => toHub[arm];

    /// <summary>The train waits at the hub.</summary>
    internal void ParkAtHub() => Train.Stand(hubRoute, hubRoute.Length * .5f);

    /// <summary>The train waits at the platform of a terminal.</summary>
    internal void ParkAtTerminal(int arm)
    {
        var route = toTerminal[arm];
        Train.Stand(route, route.Length - 18);
    }

    /// <summary>The place where the engine driver waits at a terminal.</summary>
    internal Vector3 TalkAt(int arm)
    {
        var terminal = Site.Terminals[arm];
        var p = terminal.Talk;
        return new Vector3(p.X, StationTop(terminal), p.Z);
    }

    internal Vector3 TalkAtHub()
    {
        var p = Site.Hub.Talk;
        return new Vector3(p.X, StationTop(Site.Hub), p.Z);
    }

    internal void Tick(float delta, float night)
    {
        time += delta;
        Train.Tick(delta);
        // The flag at the next load waves.
        flagPole.Rotation = new Vector3(0, Mathf.Sin(time * 2) * .25f, 0);
    }

    // ---- parts -----------------------------------------------------------------------------------------------------

    private static Basis Along(Vector3 from, Vector3 to) => Basis.LookingAt(to - from, Vector3.Up);

    private MultiMeshInstance3D Instances(Node3D parent, string name, string color, List<Transform3D> transforms)
    {
        var multi = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = Vector3.One, Material = Art.Mat(color) },
            InstanceCount = transforms.Count,
        };
        for (int i = 0; i < transforms.Count; i++)
            multi.SetInstanceTransform(i, transforms[i]);
        var instance = new MultiMeshInstance3D
        {
            Name = name,
            Multimesh = multi,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(instance);
        return instance;
    }

    /// <summary>
    /// The track between two points of a route: a ballast bed on an embankment (on pillars where it rides high above the
    /// ground), sleepers and two rails.
    /// </summary>
    private void BuildTrack(Node3D parent, LineRoute route, int from, int to)
    {
        var bed = new List<Transform3D>();
        var sleepers = new List<Transform3D>();
        var rails = new List<Transform3D>();
        var pillars = new List<Transform3D>();
        for (int i = from; i < to; i++)
        {
            var a = route.Ground(i) with { Y = route.Bed(i) };
            var b = route.Ground(i + 1) with { Y = route.Bed(i + 1) };
            var middle = (a + b) / 2;
            float length = a.DistanceTo(b);
            var basis = Along(a, b);
            float groundMiddle = (route.Ground(i).Y + route.Ground(i + 1).Y) / 2;
            float rise = middle.Y - groundMiddle;
            float thickness = rise > PillarFrom ? .5f : Mathf.Clamp(rise + .55f, .55f, 3f);
            var bedBasis = basis * Basis.FromScale(new Vector3(BedWidth, thickness, length + .08f));
            bed.Add(new Transform3D(bedBasis, middle - Vector3.Up * (thickness / 2)));
            if (rise > PillarFrom && i % 2 == 0)
                foreach (float side in new[] { -1f, 1f })
                {
                    var foot = middle + basis.X * side * (BedWidth / 2 - .35f);
                    float low = ground.Height(foot.X, foot.Z) - .6f;
                    float high = middle.Y - .5f;
                    pillars.Add(
                        new Transform3D(
                            Basis.FromScale(new Vector3(.8f, Mathf.Max(.2f, high - low), .8f)),
                            new Vector3(foot.X, (low + high) / 2, foot.Z)
                        )
                    );
                }
            var top = Vector3.Up * .06f;
            foreach (float t in new[] { .25f, .75f })
                sleepers.Add(
                    new Transform3D(
                        basis * Basis.FromScale(new Vector3(2.5f, .13f, .3f)),
                        a.Lerp(b, t) + top + Vector3.Up * .0f
                    )
                );
            foreach (float side in new[] { -.72f, .72f })
                rails.Add(
                    new Transform3D(
                        basis * Basis.FromScale(new Vector3(.11f, .15f, length + .04f)),
                        middle + basis.X * side + Vector3.Up * .2f
                    )
                );
        }
        if (bed.Count == 0)
            return;
        Instances(parent, "Bed", Ballast, bed);
        Instances(parent, "Sleepers", Sleeper, sleepers);
        Instances(parent, "Rails", Steel, rails);
        if (pillars.Count > 0)
            Instances(parent, "Pillars", "8f8a80", pillars);
    }

    /// <summary>Stakes with an orange head every twenty metres along a way that is still to be laid.</summary>
    private void BuildStakes(Node3D parent, LineRoute route, int from, int to)
    {
        var posts = new List<Transform3D>();
        var heads = new List<Transform3D>();
        for (int i = from + 2; i <= to; i += 8)
            foreach (float side in new[] { -1f, 1f })
            {
                var p = route.Ground(i);
                var way = route.Ground(Math.Min(i + 1, route.Count - 1)) - route.Ground(Math.Max(i - 1, 0));
                way.Y = 0;
                if (way.LengthSquared() < .01f)
                    continue;
                var across = Vector3.Up.Cross(way.Normalized()) * side * 1.9f;
                var at = p + across;
                at.Y = ground.Height(at.X, at.Z);
                posts.Add(new Transform3D(Basis.FromScale(new Vector3(.08f, 1.3f, .08f)), at + Vector3.Up * .65f));
                heads.Add(new Transform3D(Basis.FromScale(new Vector3(.2f, .3f, .2f)), at + Vector3.Up * 1.25f));
            }
        if (posts.Count == 0)
            return;
        Instances(parent, "Posts", "d9c9a0", posts);
        Instances(parent, "Heads", Stake, heads);
    }

    /// <summary>The flag where the next load is tipped out: a tall pole with an orange pennant.</summary>
    private void BuildFlag()
    {
        Art.Cylinder(flagPole, new(0, 3.5f, 0), .07f, .07f, 7, "e6e1d3");
        Art.Block(flagPole, new(.9f, 6.3f, 0), new(1.7f, .9f, .05f), Stake);
        Art.Block(flagPole, new(.9f, 6.3f, .02f), new(1.7f, .25f, .06f), "fff1b8");
    }

    /// <summary>A platform of a station: concrete boxes along its track with a pale edge.</summary>
    private void BuildPlatform(Node3D parent, LineRoute track, LineStationSite station)
    {
        var boxes = new List<Transform3D>();
        var edges = new List<Transform3D>();
        for (int i = 0; i < track.Count - 1; i++)
        {
            var a = track.Ground(i) with { Y = track.Bed(i) };
            var b = track.Ground(i + 1) with { Y = track.Bed(i + 1) };
            var middle = (a + b) / 2;
            var basis = Along(a, b);
            float length = a.DistanceTo(b);
            float top = middle.Y + PlatformRise;
            float groundMiddle = (track.Ground(i).Y + track.Ground(i + 1).Y) / 2;
            float height = Mathf.Max(.6f, top - groundMiddle + .5f);
            // LookingAt turns −Z along the track, so its X points to the track's left; the platform is on the road's side.
            var side = -basis.X * station.Side;
            boxes.Add(
                new Transform3D(
                    basis * Basis.FromScale(new Vector3(PlatformWidth, height, length + .06f)),
                    middle + side * (3.0f + PlatformWidth / 2) - Vector3.Up * (height / 2 - PlatformRise)
                )
            );
            edges.Add(
                new Transform3D(
                    basis * Basis.FromScale(new Vector3(.3f, .04f, length + .06f)),
                    middle + side * (3.0f + .3f) + Vector3.Up * (PlatformRise + .02f)
                )
            );
        }
        Instances(parent, "Platform", Concrete, boxes);
        Instances(parent, "PlatformEdge", "f2c14e", edges);
    }

    /// <summary>The hub's bed and platform, and its building with a canopy over the platform.</summary>
    private void BuildHub(LineStationSite hub)
    {
        BuildTrack(hubBed, hubRoute, 0, hubRoute.Count - 1);
        BuildPlatform(hubBed, hubRoute, hub);
        BuildBuilding(hubStation, hub, hubRoute, 17, 7.4f, "NÁDRAŽÍ", true);
        // Buffer stops at the rear end.
        var rear = hubRoute.Ground(0) with { Y = hubRoute.Bed(0) };
        Art.Block(hubBed, rear + new Vector3(0, .6f, 0), new(2.8f, .4f, .3f), "8a2f2a");
        BuildLamps(hubStation, hubRoute, hub);
    }

    /// <summary>A terminal station: its platform and its building; its lamps come with the arm's glass.</summary>
    private void BuildStation(Node3D parent, LineRoute track, LineStationSite station, string sign)
    {
        BuildPlatform(parent, track, station);
        BuildBuilding(parent, station, track, 9, 5.6f, sign, false);
    }

    /// <summary>A station building across the track from the platform: brick walls, a gabled roof, a clock and a sign.</summary>
    private void BuildBuilding(
        Node3D parent,
        LineStationSite station,
        LineRoute track,
        float length,
        float height,
        string sign,
        bool canopy
    )
    {
        float mid = track.Length * .5f;
        var middle = track.At(mid);
        var way = track.Forward(mid);
        var across = Vector3.Up.Cross(way);
        var side = -station.Side;
        float baseY = middle.Y - .35f;
        var center = middle + across * side * 9;
        var house = new Node3D { Name = "Building" };
        parent.AddChild(house);
        house.Position = new Vector3(center.X, baseY, center.Z);
        house.Basis = Basis.LookingAt(way * -1, Vector3.Up);
        // Local +Z of the house runs along the track backwards; its front looks at the track.
        float depth = 5.2f;
        float groundY = ground.Height(center.X, center.Z);
        float footing = Mathf.Max(0, baseY - groundY) + .8f;
        Art.Wall(house, new(0, height / 2 - .3f, 0), new(depth, height - .6f, length), Brick, FacadeStyle.Bricks);
        Art.Block(house, new(0, -footing / 2 - .3f, 0), new(depth + .3f, footing, length + .3f), "8f8a80");
        // Windows and a door on the track side (the house's local +X faces the track when it is turned this way).
        var toTrack = across * -side;
        float faceX = Vector3.Right.Dot(house.Basis.Inverse() * toTrack) >= 0 ? 1 : -1;
        for (int i = 0; i < (int)(length / 3.4f); i++)
        {
            float z = -length / 2 + 1.9f + i * 3.4f;
            if (Mathf.Abs(z) < .9f)
                continue;
            Art.Window(house, new(faceX * (depth / 2 + .02f), height * .5f, z), new(.06f, 1.4f, 1.1f), "9ed0e6");
        }
        Art.Block(house, new(faceX * (depth / 2 + .03f), 1.15f, 0), new(.08f, 2.3f, 1.3f), "5a3d26");
        // The roof: two sloping boards with a ridge along the house, overhanging the walls.
        foreach (float half in new[] { -1f, 1f })
        {
            var board = Art.Block(
                house,
                new(half * depth * .27f, height + .75f, 0),
                new(depth * .62f, .22f, length + 1.2f),
                RoofRed
            );
            board.RotationDegrees = new Vector3(0, 0, -half * 28);
        }
        Art.Block(house, new(0, height + 1.5f, 0), new(.4f, .3f, length + 1.3f), "5c2b24");
        // A chimney and a sign board above the door, and a round clock on the front.
        Art.Block(house, new(-faceX * .8f, height + 1.2f, length * .3f), new(.7f, 1.8f, .7f), "8a4a38");
        var board2 = Art.Block(house, new(faceX * (depth / 2 + .15f), height - .1f, 0), new(.12f, .9f, 4.4f), "2f5d62");
        var label = Art.Sign(house, new(faceX * (depth / 2 + .6f), height - .1f, 0), sign, 52);
        label.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
        label.Rotation = new Vector3(0, faceX > 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2, 0);
        label.DoubleSided = true;
        Art.Cylinder(
            house,
            new(faceX * (depth / 2 + .1f), height + .6f, -length * .3f),
            .55f,
            .55f,
            .1f,
            "fff7e0"
        ).RotationDegrees = new Vector3(0, 0, 90);
        if (canopy)
        {
            // A canopy over the platform on posts.
            var platformMiddle = middle + across * station.Side * (3.0f + PlatformWidth / 2);
            var roofNode = new Node3D { Name = "Canopy" };
            parent.AddChild(roofNode);
            roofNode.Position = platformMiddle with { Y = middle.Y + PlatformRise };
            roofNode.Basis = Basis.LookingAt(way, Vector3.Up);
            for (int i = -2; i <= 2; i++)
                foreach (float x in new[] { -1.6f, 1.6f })
                    Art.Block(roofNode, new(x, 1.7f, i * 6f), new(.18f, 3.4f, .18f), "6a6f73");
            Art.Block(roofNode, new(0, 3.5f, 0), new(4.8f, .22f, 26), "4d6f84");
        }
    }

    /// <summary>
    /// Lamp posts along a platform (shining at night like the windows) and a semaphore at the track's end.
    /// </summary>
    private void BuildLamps(Node3D parent, LineRoute track, LineStationSite station)
    {
        for (float d = 4; d < track.Length - 2; d += 10)
        {
            var p = track.At(d);
            var across = Vector3.Up.Cross(track.Forward(d)) * station.Side;
            var at = p + across * 6.9f + Vector3.Up * PlatformRise;
            Art.Cylinder(parent, at + Vector3.Up * 1.4f, .06f, .06f, 2.8f, "30363a");
            Art.Window(parent, at + Vector3.Up * 2.95f, new(.34f, .28f, .34f), "ffe9a0");
        }
    }

    /// <summary>The lamps of an arm: a semaphore beside the track where each stretch ends and the lamps of its terminal's platform.</summary>
    private void BuildLamps(Node3D parent, int arm)
    {
        var route = Site.Arms[arm];
        for (int k = 1; k <= MainLinePlans.Sections; k++)
        {
            int i = Math.Min(Math.Max(SectionRange(arm, k - 1).To - 3, 1), route.Count - 2);
            var p = route.Ground(i) with { Y = route.Bed(i) };
            var way = route.Ground(i + 1) - route.Ground(i - 1);
            way.Y = 0;
            var side = Vector3.Up.Cross(way.Normalized()) * 2.3f;
            var at = p + side;
            Art.Cylinder(parent, at + Vector3.Up * 2.2f, .07f, .07f, 4.4f, "30363a");
            var arm1 = Art.Block(parent, at + Vector3.Up * 4.0f, new(.1f, .24f, 1.1f), "e8453c");
            arm1.Rotation = new Vector3(0, Mathf.Atan2(way.X, way.Z), 0);
            Art.Window(parent, at + Vector3.Up * 4.5f, new(.3f, .3f, .3f), "6fd36f");
        }
        var terminal = Site.Terminals[arm];
        BuildLamps(parent, StationRoute(terminal), terminal);
    }
}
