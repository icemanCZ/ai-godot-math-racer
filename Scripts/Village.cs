using Godot;
using System.Collections.Generic;

namespace MathRacer;

public partial class Village : Node3D
{
    public static readonly string[] Upgrades = { "Květiny pod okny", "Barevné okenice", "Zlatá klika", "Uvítací rohožka", "Stříška nad dveřmi", "Sloupky verandy", "Balkon", "Podkrovní okénko", "Větrná korouhvička", "Slavnostní výzdoba" };
    public List<Animal> Residents { get; } = new();
    public List<Node3D> Houses { get; } = new();
    private readonly List<Node3D> decorations = new();
    private readonly List<Label3D> progress = new();
    private readonly int[] levels = new int[4];
    public int Level(int house) => levels[house];
    public int[] Levels => (int[])levels.Clone();
    public void RestoreLevels(int[] saved)
    {
        for (int i = 0; i < Houses.Count; i++) SetLevel(i, i < saved.Length ? saved[i] : 0);
    }
    public void Upgrade(int house) => SetLevel(house, levels[house] + 1);
    private Terrain ground = null!;
    private Roads roads = null!;
    private readonly List<Transform3D> plots = new();
    private readonly List<(Vector3 From, Vector3 To, float HalfWidth)> paths = new();
    private readonly List<(Vector3 A, Vector3 B, Vector3 C)> walkwayTriangles = new();
    public Vector3 Center { get; private set; }
    public Vector3 GreetingSpot { get; private set; }
    public Vector3 GarageEntrance { get; private set; }
    public Node3D GarageBuilding { get; private set; } = null!;
    internal readonly List<(Node3D Building, Vector2 Footprint)> BuildingFoundations = new();
    internal Rect2 TerrainArea
    {
        get
        {
            var area = new Rect2(new Vector2(Center.X, Center.Z), Vector2.Zero);
            // Include all yards, collection displays, the square and the connecting road.
            foreach (var plot in plots)
                foreach (float x in new[] { -13f, 13f })
                    foreach (float z in new[] { -10f, 10f })
                    {
                        var p = plot * new Vector3(x, 0, z);
                        area = area.Expand(new Vector2(p.X, p.Z));
                    }
            return area.Grow(Settings.TerrainStep * 2);
        }
    }

    private Node3D CreateBuilding(string name, Transform3D plot, Vector2 footprint)
    {
        float highest = float.NegativeInfinity, lowest = float.PositiveInfinity;
        var size = footprint + Vector2.One * .4f;
        int columns = Mathf.CeilToInt(size.X / .25f), rows = Mathf.CeilToInt(size.Y / .25f);
        for (int x = 0; x <= columns; x++)
            for (int z = 0; z <= rows; z++)
            {
                var p = plot * new Vector3(size.X * (x / (float)columns - .5f), 0, size.Y * (z / (float)rows - .5f));
                float y = ground.Height(p.X, p.Z);
                highest = Mathf.Max(highest, y); lowest = Mathf.Min(lowest, y);
            }
        plot.Origin = new Vector3(plot.Origin.X, highest + .15f, plot.Origin.Z);
        var building = new Node3D { Name = name, Transform = plot }; AddChild(building);
        float height = plot.Origin.Y - lowest + .25f;
        var foundation = Art.Block(building, Vector3.Down * height / 2, new(size.X, height, size.Y), "b7ab96");
        foundation.Name = "Foundation";
        Obstacles.Add(building, foundation.Position, new BoxShape3D { Size = foundation.Scale }).Name = "FoundationCollision";
        BuildingFoundations.Add((building, size));
        return building;
    }

    public void Generate(Terrain terrain, Roads roads)
    {
        ground = terrain;
        this.roads = roads;
        var garageFrame = roads.SampleMain(29);
        var garageOutward = garageFrame.Forward.Cross(Vector3.Up);
        var garagePosition = ground.At(garageFrame.Center.X + garageOutward.X * 11, garageFrame.Center.Z + garageOutward.Z * 11);
        float garageYaw = Mathf.Atan2(-garageOutward.X, -garageOutward.Z);
        var garagePlot = new Transform3D(Basis.FromEuler(new(0, garageYaw, 0)), garagePosition);
        GarageBuilding = CreateBuilding("Garage", garagePlot, new(9, 8));
        garagePlot = GarageBuilding.Transform; plots.Add(garagePlot);
        Art.Block(GarageBuilding, new(0, .08f, 0), new(9, .16f, 8), "dfd4bd");
        foreach (float x in new[] { -3.7f, 3.7f })
        {
            Art.Block(GarageBuilding, new(x, 2, 0), new(.45f, 4, 6), "79bba7");
            Obstacles.Box(GarageBuilding, new(x, 2, 0), new(.45f, 4, 6));
        }
        Art.Block(GarageBuilding, new(0, 2, -2.8f), new(7.5f, 4, .4f), "79bba7");
        Obstacles.Box(GarageBuilding, new(0, 2, -2.8f), new(7.5f, 4, .4f));
        Art.Block(GarageBuilding, new(0, 4.1f, 0), new(8.5f, .4f, 7), "edb45e");
        Art.Block(GarageBuilding, new(0, 3.5f, 3), new(7.5f, .7f, .4f), "fff1d2");
        var garageSign = Art.Sign(GarageBuilding, new(0, 3.5f, 3.6f), "GARÁŽ", 38);
        garageSign.NoDepthTest = true;
        Art.TaskMarker(GarageBuilding, new(0, 6.5f, 0), "⌂");
        var entrancePoint = garagePlot * new Vector3(0, 0, 4.5f);
        GarageEntrance = ground.At(entrancePoint.X, entrancePoint.Z);
        Path(garagePlot * new Vector3(0, .16f, 3.8f), garageFrame.Center, 5.8f);
        // Paired homes frame the village crossroads, leaving its four mouths open.
        (float Distance, float Side, string Color)[] sites =
        {
            (63, -1, "f7ddac"), (63, 1, "68cfee"),
            (117, -1, "ffbb7d"), (117, 1, "ffe58a")
        };
        foreach (var site in sites)
        {
            var frame = roads.SampleMain(site.Distance);
            var outward = frame.Forward.Cross(Vector3.Up) * site.Side;
            var position = ground.At(frame.Center.X + outward.X * 11, frame.Center.Z + outward.Z * 11);
            var inward = -outward;
            float yaw = Mathf.Atan2(inward.X, inward.Z);
            var plot = new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)), position);
            House(position.X, position.Z, site.Color, yaw);
            plot = Houses[^1].Transform; plots.Add(plot);
            int index = Residents.Count;
            tasks[index] = CreateTasks(Settings.Seed, index);
            string[] names = { "Králíček stavitel Vojtík", "Liška zahradnice Róza", "Medvídek kutil Hugo", "Králíček malíř Fialka" };
            string[] fur = { "a9d8e8", "df92ac", "97c9b0", "c5b4e3" };
            var resident = Animal.Create(names[index], "Pomůžeš mi zkrášlit domeček?", index % 3, fur[index]);
            resident.HouseIndex = index;
            AddChild(resident); Residents.Add(resident);
            var spot = plot * new Vector3(1.6f, 0, 6.3f);
            resident.Position = ground.At(spot.X, spot.Z);
            resident.Rotation = new Vector3(0, yaw, 0);
            Art.Ball(resident, new(0, 1.3f, 0), new(.6f, .13f, .6f), site.Color);
            progress.Add(Art.Sign(Houses[index], new(0, 8.5f, 0), "", 27));
            SetLevel(index, 0);
            var doorway = plot * new Vector3(0, 0, 3.02f);
            Path(doorway, frame.Center);
            Fence(plot, -5, -1.4f);
            Fence(plot, 1.4f, 5);
            var shrub = plot * new Vector3(-4.4f, 0, 1.5f);
            Art.Ball(this, ground.At(shrub.X, shrub.Z, .4f), new(.7f, .6f, .7f), "56c980");
            HouseMailbox(plot, site.Color);
        }
        var center = WorldLayout.Plan.VillageJunction;
        Center = ground.At(center.X, center.Z);
        var entrance = roads.SampleMain(49);
        var greeting = entrance.Center - entrance.Forward.Cross(Vector3.Up) * 6.8f;
        GreetingSpot = ground.At(greeting.X, greeting.Z);
        GenerateFarm();
        GenerateGarden();
        GenerateCollections();
        GenerateTrades();
        GenerateSquare();
        AddVillageDetails();
        foreach (var trader in Traders) trader.Position = ApproachSpot(trader.Position);
        foreach (var resident in Residents) resident.Position = ApproachSpot(resident.Position);
        foreach (var resident in new[] { Farmer, Gardener, GemCollector, Toymaker }) resident.Position = ApproachSpot(resident.Position);
        GarageEntrance = ApproachSpot(GarageEntrance);
    }

    private Vector3 ApproachSpot(Vector3 point)
    {
        point.Y = ground.Height(point.X, point.Z);
        var flat = new Vector2(point.X, point.Z);
        foreach (var (a, b, c) in walkwayTriangles)
        {
            var origin = new Vector2(a.X, a.Z);
            var ab = new Vector2(b.X, b.Z) - origin; var ac = new Vector2(c.X, c.Z) - origin;
            float area = ab.Cross(ac);
            if (Mathf.Abs(area) < .000001f) continue;
            float u = (flat - origin).Cross(ac) / area, v = ab.Cross(flat - origin) / area;
            if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f)
                point.Y = Mathf.Max(point.Y, a.Y + (b.Y - a.Y) * u + (c.Y - a.Y) * v);
        }
        return point;
    }

    public const float TaskAnimalClearance = 100;

    public bool IsNearVillage(Vector3 point)
    {
        // Leave a quiet belt around every house and the garage for village residents.
        foreach (var plot in plots)
            if (new Vector2(point.X - plot.Origin.X, point.Z - plot.Origin.Z).Length() < TaskAnimalClearance) return true;
        return false;
    }

    public bool IsReserved(Vector3 point)
    {
        if (Square != null)
        {
            var local = Square.Transform.AffineInverse() * point;
            if (Mathf.Abs(local.X) < 11.5f && Mathf.Abs(local.Z) < 7.5f) return true;
        }
        foreach (var plot in collectionPlots)
        {
            var local = plot.AffineInverse() * point;
            if (local.X > -5 && local.X < 17 && Mathf.Abs(local.Z) < 8) return true;
        }
        if (GardenBuilding != null)
        {
            var gardenLocal = gardenPlot.AffineInverse() * point;
            if (gardenLocal.X > -5 && gardenLocal.X < 17 && Mathf.Abs(gardenLocal.Z) < 8) return true;
        }
        if (FarmBuilding != null)
        {
            var farmLocal = farmPlot.AffineInverse() * point;
            if (farmLocal.X > -6 && farmLocal.X < 19 && Mathf.Abs(farmLocal.Z) < 8) return true;
        }
        foreach (var path in paths)
        {
            var a = new Vector2(path.From.X, path.From.Z);
            var b = new Vector2(path.To.X, path.To.Z);
            var p = new Vector2(point.X, point.Z);
            var delta = b - a;
            float t = Mathf.Clamp((p - a).Dot(delta) / delta.LengthSquared(), 0, 1);
            if (p.DistanceTo(a + delta * t) < path.HalfWidth + .35f) return true;
        }
        foreach (var plot in plots)
        {
            var local = plot.AffineInverse() * point;
            if (Mathf.Abs(local.X) < 6.5f && Mathf.Abs(local.Z) < 7.7f) return true;
        }
        return false;
    }

    private void Path(Vector3 from, Vector3 to, float width = 1.8f)
    {
        var direction = new Vector3(to.X - from.X, 0, to.Z - from.Z);
        var side = direction.Normalized().Cross(Vector3.Up);
        paths.Add((from, to, width / 2));
        int columns = Mathf.CeilToInt(width / .3f);
        int segments = Mathf.CeilToInt(direction.Length() / .3f);
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        // Find the real meshed road edge for every strip, including curved/widened verges.
        // A small overlap joins the surfaces without painting a path across the carriageway.
        Vector3 End(float lateral)
        {
            var start = from + side * lateral;
            var end = to + side * lateral;
            float outside = 0, inside = 1;
            // Stop at the first road edge, including a branch before the intended road.
            int probes = Mathf.CeilToInt(direction.Length() / .2f);
            for (int i = 0; i <= probes; i++)
            {
                float t = i / (float)probes;
                if (roads.Contains(start.Lerp(end, t))) { inside = t; break; }
                outside = t;
            }
            for (int i = 0; i < 16; i++)
            {
                float middle = (outside + inside) / 2;
                if (roads.Contains(start.Lerp(end, middle))) inside = middle;
                else outside = middle;
            }
            return start.Lerp(end, Mathf.Min(1, inside + .4f / direction.Length()));
        }
        Vector3 Point(Vector3 start, Vector3 end, float t)
        {
            var p = start.Lerp(end, t);
            float terrain = ground.Height(p.X, p.Z);
            p.Y = Mathf.Max(terrain + .06f, roads.SurfaceHeight(p, terrain) + .015f);
            if (roads.Contains(p)) { p.Y = roads.SurfaceHeight(p, terrain) + .015f; return p; }
            // Meet the garage slab at the threshold rather than leaving a gap below it.
            float endTerrain = ground.Height(end.X, end.Z);
            float endHeight = Mathf.Max(endTerrain + .06f, roads.SurfaceHeight(end, endTerrain) + .015f);
            p.Y = Mathf.Max(p.Y, Mathf.Lerp(start.Y + .015f, endHeight, t));
            return p;
        }
        for (int column = 0; column < columns; column++)
        {
            float left = Mathf.Lerp(-width / 2, width / 2, (float)column / columns);
            float right = Mathf.Lerp(-width / 2, width / 2, (float)(column + 1) / columns);
            var leftStart = from + side * left; var rightStart = from + side * right;
            var leftEnd = End(left); var rightEnd = End(right);
            for (int i = 0; i < segments; i++)
            {
                float t = (float)i / segments, next = (float)(i + 1) / segments;
                var a = Point(leftStart, leftEnd, t); var b = Point(rightStart, rightEnd, t);
                var c = Point(leftStart, leftEnd, next); var d = Point(rightStart, rightEnd, next);
                walkwayTriangles.Add((a, c, b)); walkwayTriangles.Add((b, c, d));
                surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(b);
                surface.AddVertex(b); surface.AddVertex(c); surface.AddVertex(d);
            }
        }
        surface.GenerateNormals();
        var walkway = new MeshInstance3D { Name = "Walkway", Mesh = surface.Commit(), MaterialOverride = Art.Mat("e9cf9e"), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(walkway); walkway.CreateTrimeshCollision();
    }

    private void Fence(Transform3D plot, float left, float right)
    {
        var detail = DetailRoot(this);
        Vector3 Position(float x, float lift)
        {
            var p = plot * new Vector3(x, 0, 4.8f);
            return ground.At(p.X, p.Z, lift);
        }
        for (int i = 0; i <= 2; i++)
        {
            var center = Position(Mathf.Lerp(left, right, i / 2f), .65f);
            Art.Cylinder(this, center, .1f, .085f, 1.3f, "f2ddb0");
            Art.Cylinder(detail, center + Vector3.Up * .72f, .15f, .025f, .18f, "fff1d2");
            Obstacles.Trunk(this, center - Vector3.Up * .65f, .1f, 1.3f);
        }
        foreach (float lift in new[] { .45f, .95f })
        {
            var from = Position(left, lift);
            var to = Position(right, lift);
            Art.Beam(this, from, to, .13f, "f2ddb0");
            Obstacles.Box(this, (from + to) / 2, new Vector3(.13f, .13f, from.DistanceTo(to)),
                Basis.LookingAt(to - from, Vector3.Up));
        }
        int pickets = Mathf.CeilToInt((right - left) / .4f);
        for (int i = 1; i < pickets; i++)
        {
            var picket = Art.Block(detail, Position(Mathf.Lerp(left, right, i / (float)pickets), .72f), new(.1f, .85f, .09f), "fff1d2");
            picket.Rotation = plot.Basis.GetEuler();
        }
        BakeDetails(detail);
    }
    private void House(float x, float z, string color, float yaw)
    {
        var h = CreateBuilding($"House{Houses.Count}", new Transform3D(Basis.FromEuler(new(0, yaw, 0)), ground.At(x, z)), new(7, 6.6f));
        h.Name = $"House{Houses.Count}"; Houses.Add(h);
        var details = new Node3D { Name = "Upgrades" }; h.AddChild(details); decorations.Add(details);
        Art.Block(h, new(0, 2.1f, 0), new(7, 4.2f, 6), color);
        // A gabled roof, with a deliberately generous overhang.
        var st = new SurfaceTool(); st.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] v = { new(-4, 4, -3.6f), new(4, 4, -3.6f), new(0, 6.7f, -3.6f), new(-4, 4, 3.6f), new(4, 4, 3.6f), new(0, 6.7f, 3.6f) };
        int[] indices = { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 2, 5, 4, 2, 4, 1 };
        foreach (int i in indices) st.AddVertex(v[i]); st.GenerateNormals();
        var roof = Art.Mesh(h, st.Commit(), Vector3.Zero, Vector3.One, "eb644b");
        var roofMaterial = (StandardMaterial3D)Art.Mat("eb644b").Duplicate();
        roofMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled; roof.MaterialOverride = roofMaterial;
        Art.Block(h, new(0, 1.25f, 3.04f), new(1.35f, 2.5f, .15f), "a87b61");
        foreach (float wx in new[] { -2.15f, 2.15f })
        {
            Art.Block(h, new(wx, 2.5f, 3.08f), new(1.5f, 1.5f, .18f), "fff1d2");
            Art.Block(h, new(wx, 2.5f, 3.2f), new(1.12f, 1.12f, .1f), "88c7d8");
            Art.Block(h, new(wx, 2.5f, 3.28f), new(.12f, 1.2f, .1f), "fff1d2");
        }
        Art.Block(h, new(2, 5.3f, 0), new(.85f, 3, 1), "f7ddac");
        var body = new StaticBody3D(); h.AddChild(body); body.AddChild(new CollisionShape3D { Position = new(0, 2, 0), Shape = new BoxShape3D { Size = new(7, 4, 6) } });
    }
}

