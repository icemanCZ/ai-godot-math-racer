using Godot;
using MathRacer.App;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>The notebook of the small friends in the corner: how many of each kind are found.</summary>
public partial class GameUi
{
    private Label hiddenCount = null!;

    private void SetupHidden()
    {
        var panel = new PanelContainer { Name = "HiddenHud", MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", CoinPanelStyle());
        root.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        panel.OffsetLeft = -450;
        panel.OffsetRight = -32;
        panel.OffsetTop = 192;
        panel.OffsetBottom = 244;
        hiddenCount = new Label
        {
            Name = "HiddenCount",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        hiddenCount.AddThemeFontSizeOverride("font_size", 20);
        panel.AddChild(hiddenCount);
    }

    internal void HiddenCount(HiddenFriends hidden)
    {
        hiddenCount.Text = Texts.HiddenNotebook(
            hidden.FoundOf(HiddenKind.Ladybug),
            hidden.FoundOf(HiddenKind.Hedgehog),
            hidden.FoundOf(HiddenKind.Frog),
            HiddenFriends.PerKind
        );
    }
}
