using System;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

public partial class GameUi
{
    internal void WorksHint(int index)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + WorksCatalog.All[index].Name;
    }

    internal void SourceHint(HaulLoad load)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text =
            (controller ? "A · " : "E · ")
            + (
                load switch
                {
                    HaulLoad.Stones => "Lom",
                    HaulLoad.Soil => Texts.Hliniste,
                    HaulLoad.Sand => Texts.Piskovna,
                    HaulLoad.Wheat => Texts.Pole,
                    _ => Texts.Klady,
                }
            );
    }

    /// <summary>
    /// The finished mill, the link of the bread chain between the field and the bakery: wheat becomes flour and the flour
    /// bread. A row of sacks shows the flour that stands beside the mill. The farmer's play button sends the trailer for
    /// wheat (<paramref name="forBakery"/> false), the miller's loads flour for the bakery. The button is missing while
    /// another trailer trip is under way, before the mill is finished and when there is nothing to haul or no room.
    /// </summary>
    internal void MillPanel(Character speaker, FlourMill mill, HaulTrip? trip, bool canStart, bool forBakery)
    {
        Clear();
        Text(speaker.DisplayName, 30);
        var process = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MillChain" };
        process.AddThemeConstantOverride("separation", 10);
        content.AddChild(process);
        string[] chain = { "wheat", "flour", "bread" };
        for (int i = 0; i < chain.Length; i++)
        {
            if (i > 0)
            {
                var arrow = Picture(process, "arrow")!;
                arrow.CustomMinimumSize = new(70, 70);
                arrow.Direction = Mathf.Pi / 2;
            }
            var step = Picture(process, chain[i])!;
            step.CustomMinimumSize = new(110, 92);
            // The farmer's panel is about the wheat and the flour, the miller's about the flour and the bread.
            if (i == (forBakery ? 0 : 2))
                step.Modulate = new Color(1, 1, 1, .45f);
        }
        if (!mill.Grinding)
        {
            // The mill is still being built: nothing is ground yet, the builder's work comes first.
            Text("…", 30);
            BackButton().GrabFocus();
            return;
        }
        var stock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "FlourStock" };
        stock.AddThemeConstantOverride("separation", 3);
        content.AddChild(stock);
        for (int i = 0; i < FlourMill.Capacity; i++)
        {
            var unit = Picture(stock, "flour")!;
            unit.CustomMinimumSize = new(50, 42);
            if (i >= mill.Stock)
                unit.Modulate = new Color(1, 1, 1, .28f);
        }
        Text(mill.Full ? $"✓  {mill.Stock} / {FlourMill.Capacity}" : $"{mill.Stock} / {FlourMill.Capacity}", 30);
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("check", () => Send(new UiIntent.SupplyQuest()), "StartSupplyTrip");
            start.AccessibilityName = Texts.ZaprahnoutVozik;
            start.GrabFocus();
        }
        var back = BackButton();
        if (!canStart)
            back.GrabFocus();
    }

    /// <summary>
    /// A panel for a works or for the worker at its raw-material site: raw material becomes goods, a row of pictures
    /// shows the stock and, at the source, the play button sends the trailer. The button is missing while another
    /// trailer trip is under way and when the stock is full.
    /// </summary>
    internal void WorksPanel(Character speaker, Works works, HaulTrip? trip, bool canStart, int made, string pet)
    {
        var spec = works.Spec;
        Clear();
        Text(speaker.DisplayName, 30);
        var process = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        process.AddThemeConstantOverride("separation", 10);
        content.AddChild(process);
        Picture(process, LoadIcon(works.Input))!.CustomMinimumSize = new(120, 100);
        var arrow = Picture(process, "arrow")!;
        arrow.CustomMinimumSize = new(90, 80);
        arrow.Direction = Mathf.Pi / 2;
        Picture(process, WorksCatalog.ProductIcon(spec.Product))!.CustomMinimumSize = new(120, 100);
        // One small picture per unit in store; empty places are dimmed.
        var stock = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "WorksStock" };
        stock.AddThemeConstantOverride("separation", 3);
        content.AddChild(stock);
        for (int i = 0; i < Works.Capacity; i++)
        {
            var unit = Picture(stock, WorksCatalog.ProductIcon(spec.Product))!;
            unit.CustomMinimumSize = new(50, 42);
            if (i >= works.Stock)
                unit.Modulate = new Color(1, 1, 1, .28f);
        }
        Text(works.Full ? $"✓  {works.Stock} / {Works.Capacity}" : $"{works.Stock} / {Works.Capacity}", 30);
        // Everything the works has made since the start counts for its pet, not only what stands in the stock.
        Text(Texts.MadeInAll(made, WorldPetCatalog.WorksPieces, pet), 22);
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("check", () => Send(new UiIntent.SupplyQuest()), "StartSupplyTrip");
            start.AccessibilityName = Texts.ZaprahnoutVozik;
            start.GrabFocus();
        }
        var back = BackButton();
        if (!canStart)
            back.GrabFocus();
    }
}
