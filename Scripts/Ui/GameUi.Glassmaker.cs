using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.Ui;

/// <summary>
/// The glass lamp maker's shop: a card with a picture and a button for the candle and the lamp, free to take, the row of
/// the glasses he blows them from, and the small panel in the corner that tells how many lamps are in the bag and what
/// the next one is.
/// </summary>
public partial class GameUi
{
    private PanelContainer lampHud = null!;
    private Label lampHudText = null!;
    private string lampFocus = "";

    private void SetupLamps()
    {
        lampHud = new PanelContainer
        {
            Name = "LampHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        lampHud.AddThemeStyleboxOverride("panel", CoinPanelStyle());
        root.AddChild(lampHud);
        lampHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        lampHud.OffsetLeft = -450;
        lampHud.OffsetRight = -32;
        lampHud.OffsetTop = 168;
        lampHud.OffsetBottom = 248;
        lampHudText = new Label
        {
            Name = "LampHudText",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        lampHudText.AddThemeFontSizeOverride("font_size", 20);
        lampHud.AddChild(lampHudText);
    }

    /// <summary>The corner panel: shown while the bag holds a lamp or a message about a lamp is due.</summary>
    internal void LampHud(int carried, string next, string message)
    {
        lampHud.Visible = carried > 0 || message != "";
        string status = carried > 0 ? Texts.LampsHud(carried, next, controller) : "";
        lampHudText.Text =
            message == "" ? status
            : status == "" ? message
            : message + "\n" + status;
    }

    internal void GlassmakerHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? Texts.ASvetlar : Texts.ESvetlar;
    }

    internal void LampPickupHint(string name)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text =
            (controller ? "A · " : "E · ") + Texts.SebratLampu + (name == "" ? "" : " " + name.ToLowerInvariant());
    }

    internal void LampPutHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.PostavitLampu;
    }

    /// <param name="buysGlass">Whether the valley has the glassworks, whose glasses the lamp maker takes.</param>
    /// <param name="glassTrip">The trip for his glasses (or for the sand they are made from) under way, or null.</param>
    /// <param name="canFetchGlass">Whether the trailer can be sent for a load of glasses now.</param>
    /// <param name="missingGlass">The glassworks while it has no glasses, so that the player is sent for sand; else null.</param>
    internal void GlassmakerShop(
        Character maker,
        IReadOnlyList<string> carried,
        int placed,
        bool buysGlass,
        HaulTrip? glassTrip,
        bool canFetchGlass,
        Works? missingGlass
    )
    {
        Clear();
        Text(maker.DisplayName, 30);
        Text(
            Texts.SklenenaSvetla
                + "   ·   "
                + Texts.LampsBag(carried.Count, LampField.MaxCarried)
                + "   ·   "
                + Texts.LampsOut(placed, LampField.MaxPlaced),
            20
        );
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        content.AddChild(row);
        Button? first = null,
            selected = null;
        bool full = carried.Count >= LampField.MaxCarried;
        foreach (var lamp in GlassLamps.All)
        {
            int inBag = 0;
            foreach (string kind in carried)
                if (kind == lamp.Id)
                    inBag++;
            var card = new VBoxContainer();
            row.AddChild(card);
            card.AddChild(new LampPreview { Lamp = lamp });
            var button = Button(
                inBag > 0 ? $"{Texts.Vzit} · {lamp.Name} ({inBag})" : $"{Texts.Vzit} · {lamp.Name}",
                () =>
                {
                    lampFocus = "Lamp_" + lamp.Id;
                    Send(new UiIntent.TakeLamp(lamp.Id));
                },
                card
            );
            button.Name = "Lamp_" + lamp.Id;
            button.CustomMinimumSize = new(250, 56);
            button.AddThemeFontSizeOverride("font_size", 18);
            button.Disabled = full;
            button.AccessibilityName = lamp.Name;
            button.TooltipText = full ? Texts.VakJePlny : lamp.Name;
            if (!full)
            {
                button.AddThemeStyleboxOverride("normal", Style("d9a02a", 16));
                button.AddThemeStyleboxOverride("hover", Style("e8b23a", 16));
                button.AddThemeStyleboxOverride("pressed", Style("c08a1e", 16));
                first ??= button;
                if (lampFocus == button.Name)
                    selected = button;
            }
        }
        if (buysGlass)
            GlassOrder(glassTrip, canFetchGlass, missingGlass);
        var back = BackButton();
        (selected ?? first ?? back).GrabFocus();
    }

    /// <summary>The glasses for the lamp maker: the trip under way, the button that sends the trailer or the way to sand.</summary>
    private void GlassOrder(HaulTrip? trip, bool canStart, Works? missing)
    {
        Text(Texts.SklenickyNaNoveLampy, 20);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "GlassOrder" };
        row.AddThemeConstantOverride("separation", 10);
        content.AddChild(row);
        if (missing != null && trip == null && !canStart)
        {
            // The glassworks has none: show the sand it needs and send the player for it.
            var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
            need.AddThemeConstantOverride("separation", 10);
            row.AddChild(need);
            Picture(need, LoadIcon(missing.Input))!.CustomMinimumSize = new(100, 84);
            var arrow = Picture(need, "arrow")!;
            arrow.CustomMinimumSize = new(70, 64);
            arrow.Direction = Mathf.Pi / 2;
            Picture(need, LoadIcon(HaulLoad.Glass))!.CustomMinimumSize = new(100, 84);
            IconButton("check", () => Send(new UiIntent.MissingSupply()), "StartMissingSupply", row).AccessibilityName =
                Texts.ZaprahnoutVozikProSurovinu;
            return;
        }
        Picture(row, LoadIcon(HaulLoad.Glass))!.CustomMinimumSize = new(100, 84);
        if (trip != null)
            Picture(row, trip.Loaded ? LoadIcon(trip.Load) : "trailer")!.CustomMinimumSize = new(100, 84);
        else if (canStart)
            IconButton(
                "check",
                () => Send(new UiIntent.GlassmakerTrip()),
                "StartGlassmakerTrip",
                row
            ).AccessibilityName = Texts.ZaprahnoutVozikProSklenicky;
    }
}
