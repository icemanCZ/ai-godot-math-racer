using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

public partial class GameUi
{
    internal void PlaygroundHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.HristeHint;
    }

    /// <summary>
    /// The playground's panel: six pictures show the loads of its toys (the delivered ones bright), then either the play
    /// button, the trip under way, or, when the goods for the next delivery are not made yet, the raw material that the
    /// works needs.
    /// </summary>
    internal void PlaygroundPanel(Character keeper, Playground yard, HaulTrip? trip, bool canStart, Works? missing)
    {
        Clear();
        Text(keeper.DisplayName, 30);
        var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "PlaygroundLoads" };
        loads.AddThemeConstantOverride("separation", 6);
        content.AddChild(loads);
        for (int i = 0; i < Playground.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(PlaygroundPlans.CargoOf(i)))!;
            picture.CustomMinimumSize = new(76, 64);
            if (i >= yard.Stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text(yard.Complete ? $"✓  {yard.Stage} / {Playground.Trips}" : $"{yard.Stage} / {Playground.Trips}", 30);
        if (yard.Complete)
            Text("Hotovo!", 24);
        else if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("check", () => Send(new UiIntent.PlaygroundTrip()), "StartPlaygroundTrip");
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
