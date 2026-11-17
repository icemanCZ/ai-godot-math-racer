using System;
using Godot;

namespace MathRacer;

public partial class GameUi
{
    public event Action? PlotTripRequested;

    internal void PlotHint(bool complete)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + (complete ? "Soused" : "Parcela");
    }

    /// <summary>
    /// A building plot's panel: six pictures show the loads of the house (the delivered ones bright), then either the
    /// play button, the trip under way, or, when the goods for the next delivery are not made yet, the raw material
    /// that the works needs.
    /// </summary>
    internal void PlotPanel(Animal resident, HousePlot plot, PlotTrip? trip, bool canStart, Works? missing)
    {
        Clear();
        Text(resident.DisplayName, 30);
        var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "PlotLoads" };
        loads.AddThemeConstantOverride("separation", 6);
        content.AddChild(loads);
        for (int i = 0; i < HousePlot.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(PlotPlans.CargoOf(i)))!;
            picture.CustomMinimumSize = new(76, 64);
            if (i >= plot.Stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text(plot.Complete ? $"✓  {plot.Stage} / {HousePlot.Trips}" : $"{plot.Stage} / {HousePlot.Trips}", 30);
        if (plot.Complete)
            Text("Hotovo!", 24);
        else if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("play", () => PlotTripRequested?.Invoke(), "StartPlotTrip");
            start.AccessibilityName = "Zapřáhnout vozík";
            start.GrabFocus();
        }
        else if (missing != null)
        {
            // The next load is goods that the works has not made: show what the works needs.
            var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
            need.AddThemeConstantOverride("separation", 10);
            content.AddChild(need);
            Picture(need, LoadIcon(missing.Spec.Input))!.CustomMinimumSize = new(100, 84);
            var arrow = Picture(need, "arrow")!;
            arrow.CustomMinimumSize = new(70, 64);
            arrow.Direction = Mathf.Pi / 2;
            Picture(need, WorksCatalog.ProductIcon(missing.Spec.Product))!.CustomMinimumSize = new(100, 84);
            var fetch = IconButton("play", () => MissingSupplyRequested?.Invoke(), "StartMissingSupply");
            fetch.AccessibilityName = "Zapřáhnout vozík pro surovinu";
            fetch.GrabFocus();
        }
        else
            Text("…", 30);
        var back = BackButton();
        if (!canStart && missing == null)
            back.GrabFocus();
    }
}
