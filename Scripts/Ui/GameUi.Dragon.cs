using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

/// <summary>
/// The dragon's panel (<c>docs/DRAGON_PLAN.md</c>): while the nest is unfinished, six pictures of its loads (the delivered
/// ones bright) and the green check mark that sends the trailer, or the trip under way; then the egg and the button of the
/// riddle, with the answers given so far; once the egg has hatched, the button of the flight.
/// </summary>
public partial class GameUi
{
    internal void DragonHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + OutlandTexts.DragonHint;
    }

    internal void DragonPanel(Character dragon, DragonLair lair, HaulTrip? trip, bool canStart, bool canFly)
    {
        Clear();
        Text(dragon.DisplayName, 30);
        if (!lair.Complete)
        {
            StatusPicture("dragon");
            Text(OutlandTexts.DragonNestUnfinished, 20);
            var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "DragonLoads" };
            loads.AddThemeConstantOverride("separation", 6);
            content.AddChild(loads);
            for (int i = 0; i < DragonPlans.Trips; i++)
            {
                var picture = Picture(loads, LoadIcon(DragonPlans.CargoOf(i)))!;
                picture.CustomMinimumSize = new(56, 50);
                if (i >= lair.Stage)
                    picture.Modulate = new Color(1, 1, 1, .32f);
            }
            Text($"{lair.Stage} / {DragonPlans.Trips}", 30);
            if (trip != null)
                Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
            else if (canStart)
            {
                var start = IconButton("check", () => Send(new UiIntent.DragonTrip()), "StartDragonTrip");
                start.AccessibilityName = Texts.ZaprahnoutVozik;
                start.GrabFocus();
                BackButton();
                return;
            }
            else
                Text("…", 30);
            BackButton().GrabFocus();
            return;
        }
        if (!lair.Hatched)
        {
            StatusPicture("egg");
            Text(OutlandTexts.DragonRiddleWanted, 20);
            Text($"{lair.Riddle} / {DragonPlans.Riddles}", 30);
            var riddle = IconButton("check", () => Send(new UiIntent.DragonRiddle()), "StartDragonRiddle");
            riddle.AccessibilityName = OutlandTexts.DragonRiddleStart;
            riddle.GrabFocus();
            BackButton();
            return;
        }
        StatusPicture("dragon");
        Text(OutlandTexts.DragonFlightWanted, 20);
        if (canFly)
        {
            var fly = AcceptButton(
                () => Send(new UiIntent.DragonFlight()),
                "StartDragonFlight",
                OutlandTexts.DragonFlightStart
            );
            fly.GrabFocus();
            BackButton();
            return;
        }
        BackButton().GrabFocus();
    }
}
