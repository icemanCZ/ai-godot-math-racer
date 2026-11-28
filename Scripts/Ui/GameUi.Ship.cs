using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>What the captain's panel shows: the ship still to be built, the voyage once it floats, or the way home.</summary>
internal enum CaptainMode
{
    Build,
    Sail,
    Island,
}

/// <summary>The ship Albatros (<c>docs/SHIP_PLAN.md</c>): the captain's panel, made to be understood without reading.</summary>
public partial class GameUi
{
    internal void CaptainHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + OutlandTexts.CaptainHint;
    }

    /// <summary>
    /// The captain's panel. While the ship is built: the fourteen loads (the delivered ones bright), the forge with the
    /// anchor when the anchor is next, the green check mark that sends the trailer (for stone when the anchor is not forged
    /// yet), the trip under way, or the raw material the works needs. Once the ship floats: the ship, the eight treasures of
    /// the cabin (the found ones bright, with how many) and the voyage button under the turtle. On the island: the ship and
    /// the way home. What to do is said aloud when the panel opens.
    /// </summary>
    internal void CaptainPanel(
        Character captain,
        CaptainMode mode,
        int stage,
        HaulTrip? trip,
        bool canStart,
        bool forge,
        bool anchorForged,
        Works? missing,
        int[] kinds,
        bool canSail
    )
    {
        Clear();
        Text(captain.DisplayName, 30);
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(
            new DeliveryPicture
            {
                Icon = "ship",
                CustomMinimumSize = new(150, 125),
                Modulate = stage >= ShipPlans.Trips ? Colors.White : new Color(1, 1, 1, .6f),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        if (mode == CaptainMode.Island)
        {
            Cabin(kinds);
            IconButton("ship", () => Send(new UiIntent.ShipHome()), "ShipHomeSail").GrabFocus();
            BackButton();
            return;
        }
        if (mode == CaptainMode.Sail)
        {
            Cabin(kinds);
            var sail = new VBoxContainer { Name = "ShipVoyage", Alignment = BoxContainer.AlignmentMode.Center };
            content.AddChild(sail);
            var turtle = new CenterContainer();
            sail.AddChild(turtle);
            turtle.AddChild(
                new DeliveryPicture
                {
                    Icon = "turtle",
                    CustomMinimumSize = new(96, 80),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            var back = BackButton();
            if (canSail)
            {
                var go = IconButton("ship", () => Send(new UiIntent.ShipSail()), "ShipSailButton", sail);
                go.AccessibilityName = OutlandTexts.ShipSailButton;
                go.CustomMinimumSize = new(120, 66);
                go.GrabFocus();
            }
            else
                back.GrabFocus();
            return;
        }
        var loads = new GridContainer { Columns = 7, Name = "ShipLoads" };
        loads.AddThemeConstantOverride("h_separation", 4);
        loads.AddThemeConstantOverride("v_separation", 4);
        var row = new CenterContainer();
        content.AddChild(row);
        row.AddChild(loads);
        for (int i = 0; i < ShipPlans.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(ShipPlans.CargoOf(i)))!;
            picture.CustomMinimumSize = new(52, 44);
            if (i >= stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text($"{stage} / {ShipPlans.Trips}", 30);
        // The forge: stone becomes the anchor, shown bright once it is forged.
        if (stage == ShipPlans.AnchorStep && (forge || anchorForged))
        {
            var smith = new HBoxContainer { Name = "ShipForge", Alignment = BoxContainer.AlignmentMode.Center };
            smith.AddThemeConstantOverride("separation", 8);
            content.AddChild(smith);
            Picture(smith, "stones")!.CustomMinimumSize = new(52, 44);
            var arrow = Picture(smith, "arrow")!;
            arrow.CustomMinimumSize = new(40, 36);
            arrow.Direction = Mathf.Pi / 2;
            Picture(smith, "forge")!.CustomMinimumSize = new(72, 60);
            var then = Picture(smith, "arrow")!;
            then.CustomMinimumSize = new(40, 36);
            then.Direction = Mathf.Pi / 2;
            var anchor = Picture(smith, "anchor")!;
            anchor.CustomMinimumSize = new(52, 44);
            if (!anchorForged)
                anchor.Modulate = new Color(1, 1, 1, .28f);
        }
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
            AcceptButton(
                    () => Send(new UiIntent.ShipStart()),
                    "StartShipTrip",
                    forge ? OutlandTexts.ShipForgeButton : Texts.ZaprahnoutVozik
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
        var backButton = BackButton();
        if (trip != null || !canStart && missing == null)
            backButton.GrabFocus();
    }

    /// <summary>The eight treasures of the captain's cabin: the found ones bright, each with how many there are.</summary>
    private void Cabin(int[] kinds)
    {
        var cabin = new GridContainer { Columns = 4, Name = "ShipCabin" };
        cabin.AddThemeConstantOverride("h_separation", 6);
        cabin.AddThemeConstantOverride("v_separation", 4);
        var row = new CenterContainer();
        content.AddChild(row);
        row.AddChild(cabin);
        for (int kind = 0; kind < ShipPlans.Kinds; kind++)
        {
            int count = kind < kinds.Length ? kinds[kind] : 0;
            var cell = new VBoxContainer { Name = $"Treasure{kind}", Alignment = BoxContainer.AlignmentMode.Center };
            cabin.AddChild(cell);
            var picture = Picture(cell, $"souvenir{kind}")!;
            picture.CustomMinimumSize = new(62, 52);
            picture.AccessibilityName = ShipPlans.Name(kind);
            if (count == 0)
                picture.Modulate = new Color(1, 1, 1, .25f);
            var label = new Label
            {
                Text = count > 0 ? count.ToString() : " ",
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            label.AddThemeFontSizeOverride("font_size", 20);
            cell.AddChild(label);
        }
    }
}
