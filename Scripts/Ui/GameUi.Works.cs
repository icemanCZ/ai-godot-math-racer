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
    /// A panel for a works or for the worker at its raw-material site: raw material becomes goods, a row of pictures
    /// shows the stock and, at the source, the play button sends the trailer. The button is missing while another
    /// trailer trip is under way and when the stock is full.
    /// </summary>
    internal void WorksPanel(Character speaker, Works works, HaulTrip? trip, bool canStart)
    {
        var spec = works.Spec;
        Clear();
        Text(speaker.DisplayName, 30);
        var process = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        process.AddThemeConstantOverride("separation", 10);
        content.AddChild(process);
        Picture(process, LoadIcon(spec.Input))!.CustomMinimumSize = new(120, 100);
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
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("play", () => Send(new UiIntent.SupplyQuest()), "StartSupplyTrip");
            start.AccessibilityName = Texts.ZaprahnoutVozik;
            start.GrabFocus();
        }
        var back = BackButton();
        if (!canStart)
            back.GrabFocus();
    }
}
