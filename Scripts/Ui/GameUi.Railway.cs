using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

public partial class GameUi
{
    internal void RailwayHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.NadraziHint;
    }

    /// <summary>
    /// The station's panel: six pictures show the loads of its parts (the delivered ones bright), then either the play
    /// button, the trip under way, or, when the goods for the next delivery are not made yet, the raw material that the
    /// works needs.
    /// </summary>
    internal void RailwayPanel(Character master, Railway railway, HaulTrip? trip, bool canStart, Works? missing)
    {
        Clear();
        Text(master.DisplayName, 30);
        var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "RailwayLoads" };
        loads.AddThemeConstantOverride("separation", 6);
        content.AddChild(loads);
        for (int i = 0; i < Railway.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(RailwayPlans.CargoOf(i)))!;
            picture.CustomMinimumSize = new(76, 64);
            if (i >= railway.Stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text(railway.Complete ? $"✓  {railway.Stage} / {Railway.Trips}" : $"{railway.Stage} / {Railway.Trips}", 30);
        if (railway.Complete)
            Text("Hotovo!", 24);
        else if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("check", () => Send(new UiIntent.RailwayTrip()), "StartRailwayTrip");
            start.AccessibilityName = Texts.ZaprahnoutVozik;
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
            var fetch = IconButton("check", () => Send(new UiIntent.MissingSupply()), "StartMissingSupply");
            fetch.AccessibilityName = Texts.ZaprahnoutVozikProSurovinu;
            fetch.GrabFocus();
        }
        else
            Text("…", 30);
        var back = BackButton();
        if (!canStart && missing == null)
            back.GrabFocus();
    }
}
