using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>The great railway across the valley (<c>docs/MAINLINE_PLAN.md</c>): the engine driver's panel, made to be understood without reading.</summary>
public partial class GameUi
{
    internal void EngineerHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + OutlandTexts.EngineerHint;
    }

    /// <summary>
    /// The engine driver's panel. At a terminal: the arm and the way back to the hub. At the hub: the train, the seventeen
    /// loads of the line (the delivered ones bright), the forge with the rails it holds, the green check mark that sends the
    /// trailer (for stone when the forge has no rails), the trip under way, or the raw material the works needs; and a
    /// ride button under the picture of each finished arm. What to do is said aloud when the panel opens.
    /// </summary>
    /// <param name="atTerminal">The arm whose terminal the car stands at, -1 at the hub, -2 while something is under way.</param>
    internal void EngineerPanel(
        Character engineer,
        int stage,
        HaulTrip? trip,
        bool canStart,
        bool forge,
        Works? missing,
        int rails,
        bool[] arms,
        int atTerminal,
        CollectorPet? mole
    )
    {
        Clear();
        Text(engineer.DisplayName, 30);
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(
            new DeliveryPicture
            {
                Icon = "train",
                CustomMinimumSize = new(150, 125),
                Modulate = stage >= MainLinePlans.Trips ? Colors.White : new Color(1, 1, 1, .6f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        if (atTerminal >= 0)
        {
            var home = new VBoxContainer { Name = "LineHome", Alignment = BoxContainer.AlignmentMode.Center };
            content.AddChild(home);
            var picture = BorderPicture(MainLinePlans.ArmBorder(atTerminal), new(120, 100));
            home.AddChild(picture);
            IconButton("train", () => Send(new UiIntent.MainLineHome()), "LineHomeRide", home).GrabFocus();
            BackButton();
            return;
        }
        if (stage < MainLinePlans.Trips)
        {
            var loads = new GridContainer { Columns = 9, Name = "LineLoads" };
            loads.AddThemeConstantOverride("h_separation", 4);
            loads.AddThemeConstantOverride("v_separation", 4);
            var row = new CenterContainer();
            content.AddChild(row);
            row.AddChild(loads);
            for (int i = 0; i < MainLinePlans.Trips; i++)
            {
                var picture = Picture(loads, LoadIcon(MainLinePlans.CargoOf(i)))!;
                picture.CustomMinimumSize = new(52, 44);
                if (i >= stage)
                    picture.Modulate = new Color(1, 1, 1, .32f);
            }
            Text($"{stage} / {MainLinePlans.Trips}", 30);
            // The forge: stone becomes rails; one bar for each rail it holds.
            var smith = new HBoxContainer { Name = "LineForge", Alignment = BoxContainer.AlignmentMode.Center };
            smith.AddThemeConstantOverride("separation", 8);
            content.AddChild(smith);
            Picture(smith, "stones")!.CustomMinimumSize = new(52, 44);
            var arrow = Picture(smith, "arrow")!;
            arrow.CustomMinimumSize = new(40, 36);
            arrow.Direction = Mathf.Pi / 2;
            Picture(smith, "forge")!.CustomMinimumSize = new(72, 60);
            Picture(smith, "arrow")!.CustomMinimumSize = new(40, 36);
            smith.GetChild<DeliveryPicture>(smith.GetChildCount() - 1).Direction = Mathf.Pi / 2;
            for (int i = 0; i < MainLinePlans.RailCapacity; i++)
            {
                var bar = Picture(smith, "rails")!;
                bar.CustomMinimumSize = new(38, 34);
                if (i >= rails)
                    bar.Modulate = new Color(1, 1, 1, .28f);
            }
            if (trip != null)
                Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
            else if (canStart)
                AcceptButton(
                        () => Send(new UiIntent.MainLineStart()),
                        "StartLineTrip",
                        forge ? OutlandTexts.LineForgeButton : Texts.ZaprahnoutVozik
                    )
                    .GrabFocus();
            else if (missing != null)
            {
                var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
                need.AddThemeConstantOverride("separation", 10);
                content.AddChild(need);
                Picture(need, LoadIcon(missing.Spec.Input))!.CustomMinimumSize = new(100, 84);
                var toward = Picture(need, "arrow")!;
                toward.CustomMinimumSize = new(70, 64);
                toward.Direction = Mathf.Pi / 2;
                Picture(need, WorksCatalog.ProductIcon(missing.Spec.Product))!.CustomMinimumSize = new(100, 84);
                AcceptButton(
                        () => Send(new UiIntent.MissingSupply()),
                        "StartMissingSupply",
                        Texts.ZaprahnoutVozikProSurovinu
                    )
                    .GrabFocus();
            }
            else
                Text("…", 30);
        }
        else
            PetGoal(mole, "train");
        // A ride on the train to the end of each finished arm.
        if (atTerminal == -1)
        {
            var rides = new HBoxContainer { Name = "LineRides", Alignment = BoxContainer.AlignmentMode.Center };
            rides.AddThemeConstantOverride("separation", 14);
            content.AddChild(rides);
            for (int arm = 0; arm < arms.Length; arm++)
            {
                if (!arms[arm])
                    continue;
                int target = arm;
                var cell = new VBoxContainer { Name = $"Ride{arm}", Alignment = BoxContainer.AlignmentMode.Center };
                rides.AddChild(cell);
                cell.AddChild(BorderPicture(MainLinePlans.ArmBorder(arm), new(96, 80)));
                var ride = IconButton("train", () => Send(new UiIntent.MainLineRide(target)), $"RideButton{arm}", cell);
                ride.AccessibilityName = OutlandTexts.LineRideButton;
                ride.CustomMinimumSize = new(110, 62);
            }
        }
        var back = BackButton();
        if (trip != null || !canStart && missing == null)
            back.GrabFocus();
    }
}
