using Godot;
using System.Collections.Generic;

namespace MathRacer;

public partial class Village
{
    public static readonly string[] TradeCargo = { "bread", "parcel", "cake" };
    public static readonly string[] TradeSigns = { "PEKAŘSTVÍ", "OBCHOD", "CUKRÁRNA" };
    public List<Animal> Traders { get; } = new();
    public List<Node3D> Shops { get; } = new();
    public int[] TradeCounts { get; private set; } = new int[3];
    public void RestoreTrades(int[] counts) => TradeCounts = (int[])counts.Clone();

    private void GenerateTrades()
    {
        string[] names = { "Pekař František", "Obchodník Kamil", "Cukrář Oliver" };
        string[] colors = { "f4c57c", "83cbb7", "eab0ca" };
        for (int i = 0; i < 3; i++)
        {
            var (plot, road) = FindCollectionPlot(110, -5, 5, 1200);
            var shop = CreateBuilding("Shop" + i, plot, new(8, 7));
            Shops.Add(shop); plot = shop.Transform; plots.Add(plot);
            Art.Block(shop, new(0, 1.9f, 0), new(8, 3.8f, 6), colors[i]);
            Obstacles.Box(shop, new(0, 1.9f, 0), new(8, 3.8f, 6));
            Art.Block(shop, new(0, 4, 0), new(8.8f, .5f, 7), "c87e69");
            Art.Block(shop, new(-2.3f, 1.3f, 3.05f), new(1.5f, 2.6f, .15f), "fff0d3");
            Art.Block(shop, new(.9f, 2, 3.06f), new(3.7f, 1.7f, .18f), "fff0d3");
            Art.Block(shop, new(.9f, 2, 3.18f), new(3.4f, 1.4f, .1f), "9ddce5");
            for (int stripe = 0; stripe < 8; stripe++)
                Art.Block(shop, new(-3.5f + stripe, 3.25f, 3.65f), new(1, .18f, 1.5f), stripe % 2 == 0 ? colors[i] : "fff4df");
            Art.Sign(shop, new(0, 6.5f, 0), TradeSigns[i], 36);
            var display = new Node3D { Position = new(.8f, 4.7f, 0), Scale = Vector3.One * 2.5f };
            shop.AddChild(display); Expedition.AddCargo(display, Vector3.Zero, TradeCargo[i]);
            var owner = Animal.Create(names[i], "Vyřeš příklad a rozvez mou objednávku.\nZa doručení si vybereš odměnu pro auto!", i, colors[i]);
            owner.TradeIndex = i; Traders.Add(owner); AddChild(owner);
            owner.Position = plot * new Vector3(-2.3f, 0, 6);
            owner.Rotation = plot.Basis.GetEuler();
            Art.Block(owner, new(0, .95f, .5f), new(.75f, 1, .12f), "fff4df");
            if (i != 1)
            {
                Art.Cylinder(owner, new(0, 2.75f, 0), .48f, .48f, .35f, "fff4df");
                Art.Ball(owner, new(0, 3, 0), new(.65f, .35f, .5f), "fff4df");
            }
            else Art.Ball(owner, new(0, 2.7f, 0), new(.65f, .18f, .5f), "527b98");
            Path(plot * new Vector3(-2.3f, 0, 3.2f), road, 3.2f);
        }
    }
}
