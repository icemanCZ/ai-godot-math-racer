using Godot;

namespace MathRacer;

public partial class GameUi
{
    private Label eventHud = null!;
    private Label celebrationMessage = null!;
    private void SetupEvents()
    {
        eventHud = new Label { Position = new(28, 140), MouseFilter = Control.MouseFilterEnum.Ignore };
        eventHud.AddThemeFontSizeOverride("font_size", 22);
        eventHud.AddThemeColorOverride("font_color", new Color("fff5df"));
        eventHud.AddThemeColorOverride("font_outline_color", new Color("245693"));
        eventHud.AddThemeConstantOverride("outline_size", 7); root.AddChild(eventHud);
        celebrationMessage = new Label { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        celebrationMessage.AddThemeFontSizeOverride("font_size", 28);
        celebrationMessage.AddThemeColorOverride("font_color", new Color("ffe58a"));
        celebrationMessage.AddThemeColorOverride("font_outline_color", new Color("245693"));
        celebrationMessage.AddThemeConstantOverride("outline_size", 8); root.AddChild(celebrationMessage);
        celebrationMessage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        celebrationMessage.OffsetTop = 104; celebrationMessage.OffsetBottom = 145;
    }
    internal void EventHud(string text) => eventHud.Text = text;
    internal void SetEventVisible(bool visible) => eventHud.Visible = visible;
    internal void CelebrationMessage(string text) => celebrationMessage.Text = text;
    internal void EventHint(string text)
    {
        talkButton.Show(); talkButton.Text = controller ? $"A · {text}" : $"E · {text}";
    }
}
