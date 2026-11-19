using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Quizzes;

namespace MathRacer.Ui;

public partial class GameUi : CanvasLayer
{
    /// <summary>
    /// Everything the player asks for with a button, as one event: the game answers it in one place
    /// (<c>Game.HandleIntent</c>). A panel only says what was asked, never what happens next.
    /// </summary>
    internal event Action<UiIntent>? Intended;

    private void Send(UiIntent intent) => Intended?.Invoke(intent);

    private float garageScroll;
    private string garageFocus = "";
    private ScrollContainer? garageCatalogue;
    private float garageStick;
    private HBoxContainer deliveryHud = null!;
    private DeliveryPicture? directionPicture;
    private Button talkButton = null!;
    private Control root = null!;
    private PanelContainer modal = null!;
    private VBoxContainer content = null!;
    private Label hint = null!;
    private Label controls = null!;
    private bool controller;
    private Vector2 menuStick;
    private Side? menuDirection;
    private float menuRepeat;

    public void Setup()
    {
        root = new Control();
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        var theme = new Theme();
        theme.DefaultFontSize = 24;
        var panel = Style("fff5df", 28);
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetColor("font_color", "Label", new Color("3c5960"));
        theme.SetColor("font_color", "Button", new Color("ffffff"));
        theme.SetStylebox("normal", "Button", Style("5f9e8c", 20));
        theme.SetStylebox("hover", "Button", Style("74b5a0", 20));
        theme.SetStylebox("pressed", "Button", Style("4b8575", 20));
        root.Theme = theme;
        theme.SetStylebox("disabled", "Button", Style("ead8ab", 20));
        theme.SetStylebox(
            "focus",
            "Button",
            new StyleBoxFlat
            {
                BgColor = Colors.Transparent,
                BorderColor = new Color("edb941"),
                BorderWidthLeft = 4,
                BorderWidthRight = 4,
                BorderWidthTop = 4,
                BorderWidthBottom = 4,
                CornerRadiusTopLeft = 20,
                CornerRadiusTopRight = 20,
                CornerRadiusBottomLeft = 20,
                CornerRadiusBottomRight = 20,
            }
        );
        theme.SetColor("font_disabled_color", "Button", new Color("3c5960"));
        var brand = new Label { Text = Texts.MathracerKouzelneUdoli, Position = new Vector2(32, 24) };
        brand.AddThemeFontSizeOverride("font_size", 25);
        brand.AddThemeColorOverride("font_color", new Color("fff7e4"));
        brand.AddThemeColorOverride("font_outline_color", new Color("245693"));
        brand.AddThemeConstantOverride("outline_size", 3);
        root.AddChild(brand);
        controls = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        controller = Input.GetConnectedJoypads().Count > 0;
        UpdateControls();
        root.AddChild(controls);
        controls.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        controls.OffsetTop = -68;
        controls.OffsetBottom = -12;
        controls.AddThemeFontSizeOverride("font_size", 20);
        hint = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        root.AddChild(hint);
        hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        hint.OffsetTop = -112;
        hint.OffsetBottom = -70;
        var center = new CenterContainer();
        root.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.MouseFilter = Control.MouseFilterEnum.Ignore;
        modal = new PanelContainer { CustomMinimumSize = new Vector2(570, 0) };
        center.AddChild(modal);
        content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        content.AddThemeConstantOverride("separation", 18);
        modal.AddChild(content);
        deliveryHud = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        root.AddChild(deliveryHud);
        deliveryHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        deliveryHud.OffsetLeft = -154;
        deliveryHud.OffsetRight = 154;
        deliveryHud.OffsetTop = 20;
        var deliveryBackground = Style("fff5df", 24);
        deliveryHud.Draw += () =>
            deliveryHud.DrawStyleBox(deliveryBackground, new Rect2(Vector2.Zero, deliveryHud.Size));
        deliveryHud.Resized += deliveryHud.QueueRedraw;
        talkButton = new Button
        {
            Visible = false,
            Text = "",
            CustomMinimumSize = new(130, 100),
        };
        talkButton.Pressed += () => Send(new UiIntent.Talk());
        root.AddChild(talkButton);
        talkButton.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        talkButton.OffsetLeft = -65;
        talkButton.OffsetRight = 65;
        talkButton.OffsetTop = -245;
        talkButton.OffsetBottom = -130;
        talkButton.Name = "PrimaryTask";
        SetupEvents();
        SetupCoins();
        SetupLamps();
        SetupTreasure();
        SetupFireflies();
    }

    private static StyleBoxFlat Style(string color, int radius) =>
        new()
        {
            BgColor = new Color(color),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 32,
            ContentMarginRight = 32,
            ContentMarginTop = 22,
            ContentMarginBottom = 22,
            ShadowColor = new Color(0, 0, 0, .12f),
            ShadowSize = 8,
        };

    private void Clear()
    {
        deliveryHud.Hide();
        talkButton.Hide();
        garageCatalogue = null;
        garageStick = 0;
        menuStick = Vector2.Zero;
        menuDirection = null;
        foreach (Node c in content.GetChildren())
        {
            content.RemoveChild(c);
            c.QueueFree();
        }
        modal.Show();
        hint.Text = "";
    }

    private void Text(string text, int size)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", size);
        content.AddChild(l);
    }

    private Button Button(string text, Action action, Container? parent = null, bool narrate = true)
    {
        var b = new Button
        {
            Text = text,
            ActionMode = BaseButton.ActionModeEnum.Press,
            CustomMinimumSize = new Vector2(0, 62),
        };
        b.Pressed += () =>
        {
            GetViewport().SetInputAsHandled();
            action();
        };
        (parent ?? content).AddChild(b);
        if (root.GetViewport().GuiGetFocusOwner() == null)
            b.GrabFocus();
        if (narrate)
            NarrateHighlight(b, () =>
                string.IsNullOrEmpty(b.AccessibilityName) ? b.Text.TrimStart('✓', ' ') : b.AccessibilityName
            );
        return b;
    }

    private void NarrateHighlight(Button button, Func<string> label)
    {
        void ReadLabel()
        {
            if (button.Disabled || !button.IsVisibleInTree())
                return;
            string text = label();
            if (!string.IsNullOrWhiteSpace(text))
                Send(new UiIntent.Speak(text));
        }
        button.FocusEntered += () =>
        {
            if (!button.IsHovered())
                ReadLabel();
        };
        button.MouseEntered += ReadLabel;
    }

    public void Title(bool saved = false)
    {
        Clear();
        Text(Texts.KouzelneUdoli, 34);
        Text(Texts.ValleySeed(Settings.Seed), 20);
        string primary = saved ? Texts.Pokracovat : Texts.Hrat;
        Button(primary, () => Send(new UiIntent.Play())).GrabFocus();
        Button(Texts.NovaHra, () => Send(new UiIntent.NewGame())).Name = "NewGame";
        if (saved)
            Text(Texts.NovaHraNahradiUlozenyPostup, 18);
        Send(new UiIntent.Speak($"{Texts.KouzelneUdoli}. {primary}."));
    }

    public void Pause()
    {
        Clear();
        Text("Ⅱ", 40);
        Button(Texts.Pokracovat, () => Send(new UiIntent.Resume())).GrabFocus();
        Button(Texts.NovaHra, () => Send(new UiIntent.NewGame())).Name = "NewGame";
        Text(Texts.NovaHraNahradiUlozenyPostup, 18);
        Button(Texts.KonecHry, () => GetTree().Quit());
        Send(new UiIntent.Speak($"{Texts.Pauza}. {Texts.Pokracovat}."));
    }

    public void LoadingWorld()
    {
        Clear();
        Text("…", 40);
    }

    public void HideModal()
    {
        modal.Hide();
        deliveryHud.Visible = directionPicture != null;
    }

    public void Hint(bool visible, bool available) =>
        hint.Text =
            !visible ? ""
            : available ? (controller ? Texts.StiskniAProUkolAOdmenu : Texts.StiskniENeboMezernikProUkol)
            : Texts.TotoZviratkoUzJsiNavstivilSleduj;

    private DeliveryPicture? Picture(Container parent, string icon, Character? animal = null, int count = 1)
    {
        if (icon == "car")
        {
            var frame = new CenterContainer();
            parent.AddChild(frame);
            frame.AddChild(
                new WordPicture
                {
                    PictureId = "car",
                    CustomMinimumSize = new(180, 140),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            return null;
        }
        var picture = new DeliveryPicture
        {
            Icon = icon,
            Character = animal,
            Count = count,
            CustomMinimumSize = new(150, 125),
        };
        parent.AddChild(picture);
        return picture;
    }

    /// <summary>
    /// A button that is only a picture. Every dialog answers the same way: a green check mark accepts (starts the
    /// task, takes the reward, confirms the choice) and a red X declines (leaves the panel). No other picture is used
    /// for these two answers, so a child learns them once.
    /// </summary>
    private Button IconButton(string icon, Action action, string name, Container? parent = null)
    {
        var button = Button("", action, parent);
        button.Name = name;
        button.CustomMinimumSize = new(150, 76);
        if (icon == "check")
            TintAnswer(button, "4f9d5e", "62b571", "3f8a4e");
        else if (icon == "cross")
            TintAnswer(button, "d1695c", "e07f72", "b9584c");
        var picture = new DeliveryPicture { Icon = icon, MouseFilter = Control.MouseFilterEnum.Ignore };
        button.AddChild(picture);
        picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return button;
    }

    private static void TintAnswer(Button button, string normal, string hover, string pressed)
    {
        button.AddThemeStyleboxOverride("normal", Style(normal, 20));
        button.AddThemeStyleboxOverride("hover", Style(hover, 20));
        button.AddThemeStyleboxOverride("pressed", Style(pressed, 20));
    }

    /// <summary>The green check mark that accepts what the panel offers; <paramref name="spoken"/> is read aloud.</summary>
    private Button AcceptButton(Action action, string name, string spoken, Container? parent = null)
    {
        var button = IconButton("check", action, name, parent);
        button.AccessibilityName = spoken;
        return button;
    }

    /// <summary>The red X that declines and leaves the panel.</summary>
    private Button BackButton(string name = "Back", Container? parent = null)
    {
        var button = IconButton("cross", () => Send(new UiIntent.CloseInteraction()), name, parent);
        button.AccessibilityName = Texts.Zpet;
        return button;
    }
}
