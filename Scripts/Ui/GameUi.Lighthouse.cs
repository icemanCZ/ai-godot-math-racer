using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

/// <summary>
/// The lighthouse keeper's panel while the lighthouse is wanted: eight pictures show its loads (the delivered ones bright),
/// then the green check mark that sends the trailer, the trip under way, or, when the goods for the next delivery are not
/// made yet, the raw material that the works needs. Once it stands, the keeper's stamps and the way back.
/// </summary>
public partial class GameUi
{
    internal void LighthousePanel(
        Character keeper,
        int stage,
        HaulTrip? trip,
        bool canStart,
        Works? missing,
        int stamps
    )
    {
        Clear();
        Text(keeper.DisplayName, 30);
        StatusPicture("lighthouse");
        if (stage >= LighthousePlans.Trips)
        {
            Text(Texts.LighthouseShining, 20);
            KeeperStamps(stamps);
            BackButton().GrabFocus();
            return;
        }
        Text(Texts.LighthouseUnfinished, 20);
        var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "LighthouseLoads" };
        loads.AddThemeConstantOverride("separation", 6);
        content.AddChild(loads);
        for (int i = 0; i < LighthousePlans.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(LighthousePlans.CargoOf(i)))!;
            picture.CustomMinimumSize = new(56, 50);
            if (i >= stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text($"{stage} / {LighthousePlans.Trips}", 30);
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("check", () => Send(new UiIntent.LighthouseTrip()), "StartLighthouseTrip");
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
