using Godot;
using System;
using System.Collections.Generic;

namespace MathRacer;

public partial class Village
{
    public static readonly string[] GemNames = { "rubín", "safír", "smaragd", "ametyst", "citrín", "růženín", "tyrkys", "opál", "topaz", "akvamarín", "olivín", "lazurit" };
    public static readonly string[] GemColors = { "ed586e", "609ff1", "5cce9c", "b084e5", "f5cd57", "f3abc9", "55c9bb", "eee7ff", "efaa66", "9adeef", "b7cf67", "4a65bd" };
    public static readonly string[] ToyNames = { "míč", "autíčko", "medvídka", "káču", "vláček", "kostky", "bubínek", "raketu", "kachničku", "robota", "letadélko", "plachetnici" };
    public Animal GemCollector { get; private set; } = null!;
    public Animal Toymaker { get; private set; } = null!;
    public Node3D MuseumBuilding { get; private set; } = null!;
    public Node3D ToyBuilding { get; private set; } = null!;
    public int GemCount => gemDisplays.Count;
    public int ToyCount => toyDisplays.Count;
    private readonly List<Transform3D> collectionPlots = new();
    private readonly List<Node3D> gemDisplays = new(), toyDisplays = new();
    private readonly List<Label3D> collectionProgress = new();
    public int CollectionCount(bool toys) => toys ? ToyCount : GemCount;
    public static string[] CollectionNames(bool toys) => toys ? ToyNames : GemNames;

    private void GenerateCollections()
    {
        foreach (bool toys in new[] { false, true })
        {
            var (plot, road) = FindCollectionPlot(toys ? 235 : 195);
            var building = CreateBuilding(toys ? "ToyWorkshop" : "GemMuseum", plot, new(7, 7.1f));
            plot = building.Transform;
            collectionPlots.Add(plot); plots.Add(plot); plots.Add(new(plot.Basis, plot * new Vector3(10, 0, 0)));
            if (toys) ToyBuilding = building; else MuseumBuilding = building;
            Art.Block(building, new(0, 1.8f, 0), new(7, 3.6f, 5.5f), toys ? "f1be72" : "b4b1df");
            Obstacles.Box(building, new(0, 1.8f, 0), new(7, 3.6f, 5.5f));
            Art.Block(building, new(0, 3.85f, 0), new(8, .45f, 6.5f), toys ? "e97e83" : "80bacb");
            Art.Block(building, new(-1.5f, 1.3f, 2.8f), new(1.5f, 2.6f, .15f), "fff0d3");
            Art.Block(building, new(1.3f, 2, 2.84f), new(2, 1.5f, .17f), "fff0d3");
            Art.Block(building, new(1.3f, 2, 2.96f), new(1.7f, 1.2f, .12f), "a4dceb");
            if (!toys)
                foreach (float x in new[] { -3f, 3f }) Art.Cylinder(building, new(x, 1.8f, 3.3f), .22f, .22f, 3.6f, "fff0d3");
            if (toys) CreateToy(building, new(0, 4.1f, 0), 0).Scale = Vector3.One * .85f;
            else CreateGem(building, new(0, 4.1f, 0), 1);
            Art.Sign(building, new(0, 6, 0), toys ? "HRAČKÁŘSTVÍ" : "MUZEUM DRAHOKAMŮ", 34);
            collectionProgress.Add(Art.Sign(building, new(10, 3, 0), $"{(toys ? "Hračky" : "Drahokamy")}: 0 / {CollectionNames(toys).Length}", 28));
            var owner = Animal.Create(toys ? "Hračkář Pepa" : "Sběratel drahokamů Jáchym", "Přivezeš mi něco do sbírky?", toys ? 2 : 1, toys ? "e8b87e" : "b2a6df");
            owner.IsToymaker = toys; owner.IsGemCollector = !toys;
            if (toys) Toymaker = owner; else GemCollector = owner;
            AddChild(owner); var spot = plot * new Vector3(-1.7f, 0, 6);
            owner.Position = ground.At(spot.X, spot.Z); owner.Rotation = plot.Basis.GetEuler();
            if (toys) Art.Ball(owner, new(0, 2.65f, 0), new(.6f, .16f, .5f), "67becb");
            else
            {
                Art.Ball(owner, new(0, 2.65f, 0), new(.65f, .25f, .55f), "e9c464");
                Art.Ball(owner, new(0, 2.7f, .52f), new(.18f, .18f, .09f), "fff4c2");
            }
            Path(plot * new Vector3(-1.5f, 0, 2.9f), road, 2.6f);
            for (int i = 0; i < CollectionNames(toys).Length; i++)
            {
                var p = CollectionSpot(toys, i);
                Art.Block(this, p + Vector3.Up * (toys ? .07f : .4f), toys ? new(2.5f, .14f, 2.5f) : new Vector3(1.6f, .8f, 1.6f), toys ? "e9d29e" : "dcd9ee");
            }
        }
    }

    private (Transform3D Plot, Vector3 Road) FindCollectionPlot(float start, float minX = -4.5f, float maxX = 16.5f, float end = 435)
    {
        for (float distance = start; distance <= end; distance += 20)
            foreach (int side in new[] { 1, -1 })
            {
                var frame = roads.SampleMain(distance); var outward = frame.Forward.Cross(Vector3.Up) * side;
                var origin = frame.Center + outward * 14;
                var plot = new Transform3D(Basis.FromEuler(new(0, Mathf.Atan2(-outward.X, -outward.Z), 0)), ground.At(origin.X, origin.Z));
                bool clear = true;
                for (float x = minX; x <= maxX && clear; x += 1.5f)
                    for (float z = -6.5f; z <= 7 && clear; z += 1.5f)
                    {
                        var p = plot * new Vector3(x, 0, z);
                        if (roads.Contains(p) || roads.Distance(p) < 5.8f || IsReserved(p) ||
                            River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 6) clear = false;
                        var bench = roads.SampleMain(180); var b = bench.Center + bench.Forward.Cross(Vector3.Up) * 9;
                        if (new Vector2(p.X - b.X, p.Z - b.Z).Length() < 3) clear = false;
                    }
                if (clear) return (plot, frame.Center);
            }
        throw new InvalidOperationException("No clear village collection plot found.");
    }

    private Vector3 CollectionSpot(bool toys, int index)
    {
        var p = collectionPlots[toys ? 1 : 0] * new Vector3(6 + index % 4 * 3, 0, -4 + index / 4 * 4);
        return ground.At(p.X, p.Z);
    }

    public static Node3D CreateGem(Node3D parent, Vector3 position, int index)
    {
        var gem = new Node3D { Name = "Gem" + index, Position = position }; parent.AddChild(gem);
        var mesh = new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Tau * i / 6, b = Mathf.Tau * (i + 1) / 6;
            var p = new Vector3(Mathf.Cos(a) * .55f, .45f, Mathf.Sin(a) * .55f);
            var q = new Vector3(Mathf.Cos(b) * .55f, .45f, Mathf.Sin(b) * .55f);
            foreach (var v in new[] { p, Vector3.Up * 1.4f, q, p, q, Vector3.Zero }) mesh.AddVertex(v);
        }
        mesh.GenerateNormals(); Art.Mesh(gem, mesh.Commit(), Vector3.Zero, Vector3.One, GemColors[index]);
        return gem;
    }

    public static Node3D CreateToy(Node3D parent, Vector3 position, int index)
    {
        var toy = new Node3D { Name = "Toy" + index, Position = position }; parent.AddChild(toy);
        if (index == 0)
        {
            Art.Ball(toy, new(0, .55f, 0), Vector3.One * .55f, "ec7985");
            Art.Ball(toy, new(0, .55f, .48f), new(.25f, .25f, .12f), "ffe3a0");
        }
        else if (index == 1 || index == 4)
        {
            Art.Block(toy, new(0, .4f, 0), new(1.15f, .35f, 1.6f), index == 1 ? "78c2de" : "e79064");
            Art.Block(toy, new(0, .8f, -.25f), new(.85f, .55f, .7f), "f5d875");
            foreach (float x in new[] { -.58f, .58f }) foreach (float z in new[] { -.5f, .5f }) Art.Ball(toy, new(x, .22f, z), new(.18f, .22f, .22f), "495c69");
            if (index == 4) Art.Cylinder(toy, new(0, .85f, .55f), .16f, .2f, .65f, "7c97bd");
        }
        else if (index == 2)
        {
            Art.Ball(toy, new(0, .55f, 0), new(.4f, .5f, .3f), "c29163");
            Art.Ball(toy, new(0, 1.1f, 0), Vector3.One * .37f, "c29163");
            foreach (float x in new[] { -.3f, .3f })
            {
                Art.Ball(toy, new(x, 1.4f, 0), Vector3.One * .15f, "c29163");
                Art.Ball(toy, new(x * .45f, 1.16f, .34f), Vector3.One * .05f, "394d52");
                Art.Ball(toy, new(x, .2f, .16f), new(.22f, .18f, .26f), "c29163");
                Art.Ball(toy, new(x * 1.5f, .65f, 0), Vector3.One * .18f, "c29163");
            }
            Art.Ball(toy, new(0, 1, .35f), new(.17f, .12f, .1f), "f3dec0");
        }
        else if (index == 3)
        {
            Art.Cylinder(toy, new(0, .3f, 0), .02f, .6f, .6f, "b18bdc");
            Art.Cylinder(toy, new(0, .75f, 0), .6f, .05f, .3f, "f3cd66");
            Art.Cylinder(toy, new(0, 1, 0), .08f, .08f, .3f, "7bbfbd");
        }
        else if (index == 5)
            for (int i = 0; i < 3; i++) Art.Block(toy, new((i - 1) * .42f, .25f + (i == 1 ? .5f : 0), 0), Vector3.One * .5f, new[] { "ef897d", "7dbfce", "f4ce70" }[i]);
        else if (index == 6)
        {
            Art.Cylinder(toy, new(0, .45f, 0), .55f, .55f, .8f, "ef897d");
            foreach (float y in new[] { .05f, .85f }) Art.Cylinder(toy, new(0, y, 0), .58f, .58f, .08f, "fff0d3");
            Art.Beam(toy, new(-.5f, 1, -.2f), new(.45f, 1.2f, .2f), .07f, "c79869");
        }
        else if (index == 7)
        {
            Art.Cylinder(toy, new(0, .65f, 0), .3f, .3f, 1, "e9edf5");
            Art.Cylinder(toy, new(0, 1.35f, 0), .3f, 0, .4f, "ef897d");
            Art.Ball(toy, new(0, .85f, .29f), new(.15f, .15f, .04f), "7dbfce");
            foreach (int side in new[] { -1, 1 }) Art.Block(toy, new(side * .35f, .25f, 0), new(.2f, .5f, .4f), "ef897d");
        }
        else if (index == 8)
        {
            Art.Ball(toy, new(0, .4f, 0), new(.5f, .4f, .65f), "f4ce70");
            Art.Ball(toy, new(0, .95f, -.3f), Vector3.One * .32f, "f4ce70");
            Art.Block(toy, new(0, .9f, -.65f), new(.35f, .12f, .3f), "ef9b58");
            foreach (int side in new[] { -1, 1 }) Art.Ball(toy, new(side * .26f, 1.03f, -.45f), Vector3.One * .045f, "394d52");
        }
        else if (index == 9)
        {
            Art.Block(toy, new(0, .65f, 0), new(.6f, .65f, .35f), "7dbfce");
            Art.Block(toy, new(0, 1.2f, 0), new(.65f, .45f, .4f), "b9c9d2");
            foreach (int side in new[] { -1, 1 })
            {
                Art.Ball(toy, new(side * .16f, 1.25f, -.22f), Vector3.One * .07f, "f4ce70");
                Art.Block(toy, new(side * .45f, .65f, 0), new(.2f, .55f, .25f), "b9c9d2");
                Art.Block(toy, new(side * .2f, .18f, 0), new(.25f, .35f, .4f), "b9c9d2");
            }
        }
        else if (index == 10)
        {
            Art.Ball(toy, new(0, .45f, 0), new(.2f, .2f, .8f), "ef897d");
            Art.Block(toy, new(0, .45f, -.1f), new(1.7f, .09f, .4f), "f4ce70");
            Art.Block(toy, new(0, .6f, .55f), new(.08f, .45f, .3f), "7dbfce");
            Art.Block(toy, new(0, .45f, .6f), new(.7f, .08f, .25f), "f4ce70");
        }
        else
        {
            Art.Ball(toy, new(0, .25f, 0), new(.45f, .25f, .8f), "7dbfce");
            Art.Cylinder(toy, new(0, .9f, 0), .04f, .04f, 1.4f, "c79869");
            var sail = new SurfaceTool(); sail.Begin(Mesh.PrimitiveType.Triangles);
            var a = new Vector3(.05f, .5f, 0); var b = new Vector3(.05f, 1.55f, 0); var c = new Vector3(.7f, .5f, 0);
            foreach (var v in new[] { a, b, c, c, b, a }) sail.AddVertex(v);
            sail.GenerateNormals(); Art.Mesh(toy, sail.Commit(), Vector3.Zero, Vector3.One, "fff0d3");
        }
        return toy;
    }

    public void RestoreCollection(bool toys, int count)
    {
        var displays = toys ? toyDisplays : gemDisplays;
        foreach (var display in displays) { RemoveChild(display); display.QueueFree(); } displays.Clear();
        for (int i = 0; i < Math.Clamp(count, 0, CollectionNames(toys).Length); i++)
        {
            var p = CollectionSpot(toys, i) + Vector3.Up * (toys ? .15f : .8f);
            displays.Add(toys ? CreateToy(this, p, i) : CreateGem(this, p, i));
        }
        (toys ? Toymaker : GemCollector).SetRewardAvailable(displays.Count < CollectionNames(toys).Length);
        collectionProgress[toys ? 1 : 0].Text = $"{(toys ? "Hračky" : "Drahokamy")}: {displays.Count} / {CollectionNames(toys).Length}";
    }
}
