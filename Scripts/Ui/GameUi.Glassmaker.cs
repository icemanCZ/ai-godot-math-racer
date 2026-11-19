using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Settlement;

namespace MathRacer.Ui;

/// <summary>
/// The glass lamp maker's shop: a card with a picture and a button for the candle and the lamp, free to take, and the
/// small panel in the corner that tells how many lamps are in the bag and what the next one is.
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
        lampHud.OffsetTop = 256;
        lampHud.OffsetBottom = 336;
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

    internal void GlassmakerShop(Character maker, IReadOnlyList<string> carried, int placed)
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
        var back = BackButton();
        (selected ?? first ?? back).GrabFocus();
    }
}
