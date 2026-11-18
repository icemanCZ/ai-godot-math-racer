using Godot;

namespace MathRacer.Ui;

public partial class GameUi
{
    private Label coinCount = null!;
    private Label coinGain = null!;
    private Control coinHud = null!;
    private Tween? coinTween;

    private void SetupCoins()
    {
        var panel = new PanelContainer { Name = "CoinHud", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", CoinPanelStyle());
        root.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        panel.OffsetLeft = -240;
        panel.OffsetRight = -32;
        panel.OffsetTop = 112;
        panel.OffsetBottom = 180;
        coinHud = panel;
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);
        row.AddChild(
            new DeliveryPicture
            {
                Icon = "coin",
                CustomMinimumSize = new(52, 48),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        coinCount = new Label
        {
            Name = "CoinCount",
            Text = "0",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        coinCount.AddThemeFontSizeOverride("font_size", 34);
        row.AddChild(coinCount);
        coinGain = new Label
        {
            Name = "CoinGain",
            VerticalAlignment = VerticalAlignment.Center,
            Modulate = Colors.Transparent,
        };
        coinGain.AddThemeFontSizeOverride("font_size", 28);
        coinGain.AddThemeColorOverride("font_color", new Color("d99a1e"));
        row.AddChild(coinGain);
    }

    private static StyleBoxFlat CoinPanelStyle()
    {
        var style = Style("fff5df", 24);
        style.ContentMarginTop = 8;
        style.ContentMarginBottom = 8;
        style.ContentMarginLeft = 18;
        style.ContentMarginRight = 18;
        style.BorderColor = new Color("edb941");
        style.BorderWidthBottom = style.BorderWidthTop = style.BorderWidthLeft = style.BorderWidthRight = 3;
        return style;
    }

    /// <summary>Shows the player's coins; a positive <paramref name="gained"/> briefly shows how many were added.</summary>
    internal void CoinCount(int total, int gained)
    {
        coinCount.Text = total.ToString();
        coinHud.AccessibilityName = $"Penízky: {total}";
        if (gained <= 0 || !IsInsideTree())
            return;
        coinGain.Text = $"+{gained}";
        coinTween?.Kill();
        coinHud.PivotOffset = coinHud.Size / 2;
        coinHud.Scale = Vector2.One * 1.15f;
        coinGain.Modulate = Colors.White;
        coinTween = CreateTween();
        coinTween.TweenProperty(coinHud, "scale", Vector2.One, .35f);
        coinTween.TweenInterval(1.2f);
        coinTween.TweenProperty(coinGain, "modulate", Colors.Transparent, .5f);
    }

    /// <summary>The panel after a finished delivery: the coins it paid and the way back to driving.</summary>
    internal void CoinReward(int coins)
    {
        Clear();
        Text("✓", 38);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 14);
        content.AddChild(row);
        row.AddChild(
            new DeliveryPicture
            {
                Icon = "coin",
                CustomMinimumSize = new(120, 100),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        var amount = new Label
        {
            Name = "RewardCoins",
            Text = $"+{coins}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        amount.AddThemeFontSizeOverride("font_size", 56);
        amount.AddThemeColorOverride("font_color", new Color("c98a16"));
        row.AddChild(amount);
        Text("Za penízky si v garáži koupíš nová vylepšení.", 20);
        var back = IconButton("check", () => CloseInteractionRequested?.Invoke(), "TakeCoins");
        back.AccessibilityName = $"Získáváš {coins} penízků";
        back.GrabFocus();
    }
}
