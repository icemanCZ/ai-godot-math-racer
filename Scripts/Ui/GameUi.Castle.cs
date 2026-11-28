using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>What the princess's panel shows: the castle still to be built, the beacons, the guests, or the feast held.</summary>
internal enum CastleMode
{
    Build,
    Beacons,
    Guests,
    Done,
}

/// <summary>The castle on the hill (<c>docs/CASTLE_PLAN.md</c>): the princess's panel, made to be understood without reading.</summary>
public partial class GameUi
{
    internal void CastellanHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + OutlandTexts.CastellanHint;
    }

    /// <summary>
    /// The princess's panel. While the castle is built: the castle, the sixteen loads (the delivered ones bright), then
    /// the green check mark that sends the trailer, the trip under way, or the raw material the works needs. Then the
    /// beacons, a border each (bright once its fire burns); then the guests of the feast, a portrait each (bright once at
    /// the table), with the ghost they bring. What to do is said aloud when the panel opens.
    /// </summary>
    internal void CastlePanel(
        Character princess,
        CastleMode mode,
        int stage,
        HaulTrip? trip,
        bool canStart,
        Works? missing,
        (Border Border, bool Built)[] beacons,
        (Character Guest, bool Seated)[] guests,
        CollectorPet? ghost
    )
    {
        Clear();
        Text(princess.DisplayName, 30);
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(
            new DeliveryPicture
            {
                Icon = mode == CastleMode.Beacons ? "beacon" : "castle",
                CustomMinimumSize = new(150, 125),
                Modulate = mode == CastleMode.Build ? new Color(1, 1, 1, .45f) : Colors.White,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        if (mode == CastleMode.Build)
        {
            var loads = new GridContainer { Columns = 8, Name = "CastleLoads" };
            loads.AddThemeConstantOverride("h_separation", 4);
            loads.AddThemeConstantOverride("v_separation", 4);
            var row = new CenterContainer();
            content.AddChild(row);
            row.AddChild(loads);
            for (int i = 0; i < CastlePlans.Trips; i++)
            {
                var picture = Picture(loads, LoadIcon(CastlePlans.CargoOf(i)))!;
                picture.CustomMinimumSize = new(52, 44);
                if (i >= stage)
                    picture.Modulate = new Color(1, 1, 1, .32f);
            }
            Text($"{stage} / {CastlePlans.Trips}", 30);
        }
        else if (mode == CastleMode.Beacons)
        {
            var row = new HBoxContainer { Name = "CastleBeacons", Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 12);
            content.AddChild(row);
            foreach (var (border, built) in beacons)
            {
                var cell = new VBoxContainer
                {
                    Name = $"Beacon{border}",
                    Alignment = BoxContainer.AlignmentMode.Center,
                };
                row.AddChild(cell);
                var picture = BorderPicture(border, new(84, 70));
                if (!built)
                    picture.Modulate = new Color(1, 1, 1, .35f);
                cell.AddChild(picture);
                var label = new Label { Text = built ? "✓" : "", HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeFontSizeOverride("font_size", 20);
                cell.AddChild(label);
            }
        }
        else
        {
            var row = new HBoxContainer { Name = "CastleGuests", Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 4);
            content.AddChild(row);
            for (int i = 0; i < guests.Length; i++)
            {
                var (guest, seated) = guests[i];
                var picture = new DeliveryPicture
                {
                    Name = $"Guest{i}",
                    Character = guest,
                    CustomMinimumSize = new(70, 60),
                    TooltipText = guest.DisplayName,
                    MouseFilter = Control.MouseFilterEnum.Pass,
                };
                if (!seated)
                    picture.Modulate = new Color(1, 1, 1, .35f);
                row.AddChild(picture);
            }
            PetGoal(ghost, "guest");
        }
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
            AcceptButton(() => Send(new UiIntent.CastleStart()), "StartCastleTrip", Texts.ZaprahnoutVozik).GrabFocus();
        else if (missing != null)
        {
            var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
            need.AddThemeConstantOverride("separation", 10);
            content.AddChild(need);
            Picture(need, LoadIcon(missing.Spec.Input))!.CustomMinimumSize = new(100, 84);
            var arrow = Picture(need, "arrow")!;
            arrow.CustomMinimumSize = new(70, 64);
            arrow.Direction = Mathf.Pi / 2;
            Picture(need, WorksCatalog.ProductIcon(missing.Spec.Product))!.CustomMinimumSize = new(100, 84);
            AcceptButton(
                    () => Send(new UiIntent.MissingSupply()),
                    "StartMissingSupply",
                    Texts.ZaprahnoutVozikProSurovinu
                )
                .GrabFocus();
        }
        else if (mode != CastleMode.Done)
            Text("…", 30);
        var back = BackButton();
        if (trip != null || !canStart && missing == null)
            back.GrabFocus();
    }
}
