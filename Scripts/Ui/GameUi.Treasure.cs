using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>
/// The pirate captain's treasure hunt: her panel (how many treasures were found, a green check mark that gives out the next
/// one), the talk-button hints, and the detector in the corner, a word with a colour and a bar that fills as the car gets closer
/// and swells with every beep.
/// </summary>
public partial class GameUi
{
    private PanelContainer treasureHud = null!;
    private Label treasureWarmth = null!;
    private ProgressBar treasureBar = null!;
    private StyleBoxFlat treasureFill = null!;

    private void SetupTreasure()
    {
        treasureHud = new PanelContainer
        {
            Name = "TreasureHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        treasureHud.AddThemeStyleboxOverride("panel", CoinPanelStyle());
        root.AddChild(treasureHud);
        treasureHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        treasureHud.OffsetLeft = 32;
        treasureHud.OffsetRight = 372;
        treasureHud.OffsetTop = 80;
        treasureHud.OffsetBottom = 184;
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        treasureHud.AddChild(column);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        column.AddChild(row);
        row.AddChild(
            new DeliveryPicture
            {
                Icon = "coin",
                CustomMinimumSize = new(48, 44),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        treasureWarmth = new Label
        {
            Name = "TreasureWarmth",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        treasureWarmth.AddThemeFontSizeOverride("font_size", 34);
        row.AddChild(treasureWarmth);
        treasureBar = new ProgressBar
        {
            Name = "TreasureBar",
            MinValue = 0,
            MaxValue = 100,
            ShowPercentage = false,
            CustomMinimumSize = new(0, 20),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        treasureFill = new StyleBoxFlat
        {
            BgColor = new Color("5aa9e6"),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
        treasureBar.AddThemeStyleboxOverride("fill", treasureFill);
        var track = new StyleBoxFlat
        {
            BgColor = new Color("ead8ab"),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        };
        treasureBar.AddThemeStyleboxOverride("background", track);
        column.AddChild(treasureBar);
    }

    /// <summary>
    /// The detector in the corner while a treasure is being looked for: how warm the hunt is, a bar that fills as the car
    /// gets closer, and a swell (<paramref name="pulse"/> from 1 down to 0) that follows each beep.
    /// </summary>
    internal void TreasureHud(bool visible, string warmth, float closeness, string color, float pulse)
    {
        treasureHud.Visible = visible && !modal.Visible;
        if (!visible)
            return;
        treasureWarmth.Text = warmth;
        treasureWarmth.AddThemeColorOverride("font_color", new Color(color));
        treasureBar.Value = closeness * 100;
        treasureFill.BgColor = new Color(color);
        treasureHud.PivotOffset = treasureHud.Size / 2;
        treasureHud.Scale = Vector2.One * (1 + .07f * pulse);
        treasureHud.AccessibilityName = warmth;
    }

    internal void TreasureHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.TreasureHint;
    }

    internal void TreasureDigHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.TreasureDig;
    }

    /// <summary>
    /// The captain's panel: the treasures found so far (and how many more until a big chest, shown as five coins), then the
    /// green check mark that gives out the next treasure, or, while one is still being looked for, only the way back.
    /// </summary>
    internal void TreasurePanel(Character captain, int found, bool active)
    {
        Clear();
        Text(captain.DisplayName, 30);
        Text(Texts.TreasuresFound(found), 26);
        var coins = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "TreasureChests" };
        coins.AddThemeConstantOverride("separation", 6);
        content.AddChild(coins);
        int done = found % TreasureHunt.BigEvery;
        for (int i = 0; i < TreasureHunt.BigEvery; i++)
        {
            var picture = Picture(coins, "coin")!;
            picture.CustomMinimumSize = new(i == TreasureHunt.BigEvery - 1 ? 64 : 46, 42);
            if (i >= done)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text(Texts.TreasureBigOne(TreasureHunt.UntilBig(found)), 18);
        if (active)
        {
            Text(Texts.TreasureStillLooking, 22);
            BackButton().GrabFocus();
            return;
        }
        var start = AcceptButton(
            () => Send(new UiIntent.StartTreasureHunt()),
            "StartTreasureHunt",
            Texts.TreasureStart
        );
        start.GrabFocus();
        BackButton();
    }
}
