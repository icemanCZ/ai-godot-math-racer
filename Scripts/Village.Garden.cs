using Godot;
using System;
using System.Collections.Generic;

namespace MathRacer;

public partial class Village
{
    public static readonly string[] FlowerNames = { "kopretinu", "tulipán", "slunečnici", "vlčí mák", "fialku", "růži" };
    public static readonly string[] FlowerColors = { "fff8e5", "ed80ae", "ffd34f", "ec655d", "a58ae5", "ed9a76" };
    public Animal Gardener { get; private set; } = null!;
    public Node3D GardenBuilding { get; private set; } = null!;
    public int GardenCount => gardenFlowers.Count;
    private Transform3D gardenPlot;
    private readonly List<Node3D> gardenFlowers = new();
    private Label3D gardenProgress = null!;

    private void GenerateGarden()
    {
        var frame = FindGardenPlot();
        plots.Add(gardenPlot); plots.Add(new(gardenPlot.Basis, gardenPlot * new Vector3(10, 0, 0)));
        GardenBuilding = new Node3D { Name = "Garden", Transform = gardenPlot }; AddChild(GardenBuilding);
        Art.Block(GardenBuilding, new(0, 1.7f, 0), new(6, 3.4f, 5), "83bfa0");
        Obstacles.Box(GardenBuilding, new(0, 1.7f, 0), new(6, 3.4f, 5));
        Art.Block(GardenBuilding, new(0, 3.55f, 0), new(7, .35f, 6), "e4ad64");
        Art.Block(GardenBuilding, new(-1, 1.2f, 2.55f), new(1.5f, 2.4f, .12f), "f7e7bd");
        Art.Block(GardenBuilding, new(1.4f, 2, 2.58f), new(1.5f, 1.4f, .16f), "fff4d7");
        Art.Block(GardenBuilding, new(1.4f, 2, 2.7f), new(1.2f, 1.1f, .1f), "a8dfeb");
        Art.Sign(GardenBuilding, new(0, 4.5f, 0), "ZAHRADNICTVÍ", 34);
        gardenProgress = Art.Sign(GardenBuilding, new(10, 3.3f, 0), "Květiny: 0 / 6", 28);
        Gardener = Animal.Create("Zahradník Vítek", "Přivezeš mi květiny do zahrádky?", 0, "a9d8b3");
        Gardener.IsGardener = true; AddChild(Gardener);
        var spot = gardenPlot * new Vector3(-1.6f, 0, 5.8f);
        Gardener.Position = ground.At(spot.X, spot.Z); Gardener.Rotation = gardenPlot.Basis.GetEuler();
        Art.Cylinder(Gardener, new(0, 2.6f, 0), .77f, .77f, .1f, "e8c482");
        Art.Cylinder(Gardener, new(0, 2.8f, 0), .43f, .4f, .35f, "7db090");
        Path(gardenPlot * new Vector3(-1, 0, 2.65f), frame.Center, 2.5f);
        for (int i = 0; i < FlowerNames.Length; i++)
        {
            var p = GardenBed(i);
            var bed = new Node3D { Name = "FlowerBed" + i, Position = p, Rotation = gardenPlot.Basis.GetEuler() }; AddChild(bed);
            Art.Block(bed, Vector3.Up * .08f, new(2.7f, .16f, 2.7f), "84674c");
            foreach (int side in new[] { -1, 1 })
            {
                Art.Block(bed, new(side * 1.4f, .18f, 0), new(.12f, .36f, 2.9f), "d9b781");
                Art.Block(bed, new(0, .18f, side * 1.4f), new(2.9f, .36f, .12f), "d9b781");
            }
        }
    }

    private (Vector3 Center, Vector3 Forward) FindGardenPlot()
    {
        // Check the whole garden against every road, including branches near the village.
        foreach (float distance in new[] { 155f, 175f, 195f, 215f, 235f, 255f })
            foreach (int side in new[] { 1, -1 })
            {
                var frame = roads.SampleMain(distance);
                var outward = frame.Forward.Cross(Vector3.Up) * side;
                var origin = frame.Center + outward * 14;
                var plot = new Transform3D(Basis.FromEuler(new(0, Mathf.Atan2(-outward.X, -outward.Z), 0)), ground.At(origin.X, origin.Z));
                bool clear = true;
                for (float x = -4.5f; x <= 16.5f && clear; x += 1.5f)
                    for (float z = -6.5f; z <= 7 && clear; z += 1.5f)
                    {
                        var p = plot * new Vector3(x, 0, z);
                        if (roads.Contains(p) || roads.Distance(p) < 5.8f || IsReserved(p) ||
                            River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 6) clear = false;
                        foreach (float benchDistance in new[] { 115f, 180f })
                        {
                            var bench = roads.SampleMain(benchDistance);
                            var spot = bench.Center + bench.Forward.Cross(Vector3.Up) * 9;
                            if (new Vector2(p.X - spot.X, p.Z - spot.Z).Length() < 3) clear = false;
                        }
                    }
                if (!clear) continue;
                gardenPlot = plot; return frame;
            }
        throw new InvalidOperationException("No road-free garden plot found near the village.");
    }

    private Vector3 GardenBed(int index)
    {
        var p = gardenPlot * new Vector3(6.5f + index % 3 * 3.5f, 0, -2.2f + index / 3 * 4.3f);
        return ground.At(p.X, p.Z);
    }

    public static Node3D CreateFlower(Node3D parent, Vector3 position, int index)
    {
        var flower = new Node3D { Name = "Flower" + index, Position = position }; parent.AddChild(flower);
        float height = index == 2 ? 1.55f : .95f;
        Art.Cylinder(flower, Vector3.Up * (height / 2), .045f, .035f, height, "56a866");
        foreach (int side in new[] { -1, 1 })
        {
            var leaf = Art.Ball(flower, new(side * .19f, height * .45f, 0), new(.28f, .07f, .12f), "72bf74");
            leaf.RotationDegrees = new(0, 0, side * 24);
        }
        int petals = index == 1 ? 5 : index == 2 ? 10 : 7;
        for (int i = 0; i < petals; i++)
        {
            float angle = Mathf.Tau * i / petals;
            var petal = Art.Ball(flower, new(Mathf.Cos(angle) * .28f, height + (index == 1 ? .12f : 0), Mathf.Sin(angle) * .28f),
                index == 1 ? new(.17f, .3f, .17f) : new Vector3(.23f, .1f, .19f), FlowerColors[index]);
            petal.Rotation = new(0, -angle, 0);
        }
        Art.Ball(flower, Vector3.Up * height, new(.19f, .13f, .19f), index == 2 ? "87613f" : "f5cf53");
        return flower;
    }

    public void RestoreGarden(int count)
    {
        foreach (var flowers in gardenFlowers) { RemoveChild(flowers); flowers.QueueFree(); }
        gardenFlowers.Clear();
        for (int i = 0; i < Math.Clamp(count, 0, FlowerNames.Length); i++)
        {
            var flowers = new Node3D { Name = "PlantedFlowers" + i, Position = GardenBed(i) + Vector3.Up * .17f, Rotation = gardenPlot.Basis.GetEuler() };
            AddChild(flowers); gardenFlowers.Add(flowers);
            foreach (var offset in new[] { new Vector3(-.6f, 0, -.4f), new Vector3(.55f, 0, -.4f), new Vector3(0, 0, .6f) }) CreateFlower(flowers, offset, i);
        }
        Gardener.SetRewardAvailable(GardenCount < FlowerNames.Length);
        gardenProgress.Text = $"Květiny: {GardenCount} / {FlowerNames.Length}";
    }
}
