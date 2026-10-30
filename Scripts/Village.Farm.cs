using Godot;
using System;
using System.Collections.Generic;

namespace MathRacer;

public partial class Village
{
    public static readonly WildKind[] FarmKinds = { WildKind.Rabbit, WildKind.Bird, WildKind.Fox, WildKind.Deer, WildKind.Rabbit, WildKind.Bird, WildKind.Fox, WildKind.Deer, WildKind.Rabbit, WildKind.Bird, WildKind.Fox, WildKind.Deer };
    public static readonly string[] FarmNames = { "králíčka Ušáčka", "ptáčka Pírko", "lišku Zrzečku", "srnku Puntíkatou", "králíčka Hopíka", "ptáčka Zpěváčka", "lišku Bystroušku", "srnku Jiskřičku", "králíčka Chlupáčka", "ptáčka Modráska", "lišku Ryšku", "srnku Hvězdičku" };
    public Animal Farmer { get; private set; } = null!;
    public Node3D FarmBuilding { get; private set; } = null!;
    public int FarmCount => farmAnimals.Count;
    private Transform3D farmPlot;
    private readonly List<WildAnimal> farmAnimals = new();
    private Label3D farmProgress = null!;

    private void GenerateFarm()
    {
        var (plot, road) = FindCollectionPlot(130, -6, 19.5f);
        farmPlot = plot;
        FarmBuilding = CreateBuilding("Farm", farmPlot, new(8, 6.6f));
        farmPlot = FarmBuilding.Transform; plots.Add(farmPlot);
        // The enclosure has its own reserved plot so trees and grass leave it clear.
        plots.Add(new(farmPlot.Basis, farmPlot * new Vector3(12, 0, 0)));
        Art.Block(FarmBuilding, new(0, 2, 0), new(8, 4, 6), "ce674e");
        Obstacles.Box(FarmBuilding, new(0, 2, 0), new(8, 4, 6));
        foreach (int side in new[] { -1, 1 })
        {
            var roof = Art.Block(FarmBuilding, new(side * 2.1f, 5, 0), new(5.1f, .3f, 7.2f), "725742");
            roof.RotationDegrees = new(0, 0, side * -28);
            Art.Block(FarmBuilding, new(side * 1.1f, 1.5f, 3.08f), new(2, 3, .16f), "f2ddb0");
            Art.Beam(FarmBuilding, new(side * 2, .1f, 3.2f), new(side * .2f, 2.9f, 3.2f), .13f, "a77a59");
        }
        Art.Sign(FarmBuilding, new(0, 4.1f, 3.7f), "FARMA", 42);
        farmProgress = Art.Sign(FarmBuilding, new(11.5f, 3.8f, 0), $"Zvířátka: 0 / {FarmKinds.Length}", 28);
        Farmer = Animal.Create("Farmář Tonda", "Pomůžeš mi najít zvířátka pro naši farmu?", 2, "c5a170");
        Farmer.IsFarmer = true; AddChild(Farmer);
        var spot = farmPlot * new Vector3(-1.8f, 0, 6.1f);
        Farmer.Position = ground.At(spot.X, spot.Z); Farmer.Rotation = farmPlot.Basis.GetEuler();
        Art.Cylinder(Farmer, new(0, 2.65f, 0), .8f, .8f, .12f, "edc66b");
        Art.Cylinder(Farmer, new(0, 2.84f, 0), .48f, .43f, .35f, "edc66b");
        Path(farmPlot * new Vector3(0, 0, 3.1f), road, 3);
        void FenceSide(Vector3 a, Vector3 b)
        {
            Vector3 Grounded(Vector3 p, float y) { p = farmPlot * p; return ground.At(p.X, p.Z, y); }
            int sections = Mathf.CeilToInt(a.DistanceTo(b) / 2);
            for (int i = 0; i <= sections; i++)
            {
                var p = Grounded(a.Lerp(b, (float)i / sections), 0);
                Art.Cylinder(this, p + Vector3.Up * .8f, .1f, .12f, 1.6f, "f2ddb0");
                Obstacles.Trunk(this, p, .12f, 1.6f);
                if (i == sections) continue;
                foreach (float y in new[] { .5f, 1.1f })
                {
                    var from = Grounded(a.Lerp(b, (float)i / sections), y);
                    var to = Grounded(a.Lerp(b, (float)(i + 1) / sections), y);
                    Art.Beam(this, from, to, .13f, "f2ddb0");
                    Obstacles.Box(this, (from + to) / 2, new(.13f, .13f, from.DistanceTo(to)), Basis.LookingAt(to - from, Vector3.Up));
                }
            }
        }
        Vector3[] corners = { new(6, 0, -5), new(17, 0, -5), new(17, 0, 5), new(6, 0, 5) };
        for (int i = 0; i < 4; i++) FenceSide(corners[i], corners[(i + 1) % 4]);
        var hay = farmPlot * new Vector3(-4.8f, 0, 0);
        Art.Ball(this, ground.At(hay.X, hay.Z, .65f), new(.8f, .7f, 1), "e9c66b");
    }

    public void RestoreFarm(int count)
    {
        foreach (var animal in farmAnimals) { RemoveChild(animal); animal.QueueFree(); }
        farmAnimals.Clear();
        for (int i = 0; i < Math.Clamp(count, 0, FarmKinds.Length); i++)
        {
            var p = farmPlot * new Vector3(7.4f + i % 4 * 2.7f, 0, -3 + i / 4 * 3);
            var animal = new WildAnimal(FarmKinds[i], i + .5f) { Position = ground.At(p.X, p.Z), Rotation = farmPlot.Basis.GetEuler() };
            AddChild(animal); farmAnimals.Add(animal); animal.Animate(0, false);
        }
        Farmer.SetRewardAvailable(FarmCount < FarmKinds.Length);
        farmProgress.Text = $"Zvířátka: {FarmCount} / {FarmKinds.Length}";
    }

    public override void _Process(double delta)
    {
        foreach (var animal in farmAnimals) { animal.Phase += (float)delta; animal.Animate((float)delta, false); }
    }
}
