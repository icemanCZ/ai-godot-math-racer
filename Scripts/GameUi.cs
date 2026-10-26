using Godot;
using System;

namespace MathRacer;

public partial class GameUi : CanvasLayer
{
    public event Action? PlayRequested;
    public event Action? ResumeRequested;
    public event Action? CloseInteractionRequested;
    public event Action<int>? AnswerSelected;
    public event Action<string>? LookSelected;
    public event Action<string>? AccessorySelected;
    public event Action<string>? ModelSelected;
    private Control root = null!;
    private PanelContainer modal = null!;
    private VBoxContainer content = null!;
    private Label hint = null!;
    private Label animalCount = null!;
    private Label controls = null!;
    private bool controller;
    private Vector2 menuStick;
    private Side? menuDirection;
    private float menuRepeat;
    public override void _Input(InputEvent e)
    {
        bool? next = e is InputEventJoypadButton || e is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) > .25f
            ? true : e is InputEventKey or InputEventMouseButton ? false : null;
        if (next.HasValue && controller != next.Value) { controller = next.Value; UpdateControls(); }
        if (modal.Visible && e is InputEventJoypadMotion stick && (stick.Axis == JoyAxis.LeftX || stick.Axis == JoyAxis.LeftY))
        {
            if (stick.Axis == JoyAxis.LeftX) menuStick.X = stick.AxisValue;
            else menuStick.Y = stick.AxisValue;
            Side? direction = menuStick.Length() < .5f ? null : Mathf.Abs(menuStick.X) > Mathf.Abs(menuStick.Y)
                ? (menuStick.X < 0 ? Side.Left : Side.Right) : (menuStick.Y < 0 ? Side.Top : Side.Bottom);
            if (direction != menuDirection)
            {
                menuDirection = direction; menuRepeat = .4f;
                if (direction.HasValue) MoveFocus(direction.Value);
            }
            GetViewport().SetInputAsHandled();
        }
    }
    private void MoveFocus(Side direction)
        => GetViewport().GuiGetFocusOwner()?.FindValidFocusNeighbor(direction)?.GrabFocus();
    public override void _Process(double delta)
    {
        if (!modal.Visible || !menuDirection.HasValue) return;
        menuRepeat -= (float)delta;
        if (menuRepeat <= 0) { MoveFocus(menuDirection.Value); menuRepeat = .15f; }
    }
    private void UpdateControls() => controls.Text = controller
        ? "Levá páčka · jízda a řízení   RT / LT · plyn / zpátečka   X · smyk   Y · návrat   Pravá páčka · kamera\nA · potvrdit / úkol   B · zpět   Menu · pauza   Páčka / směrový kříž · výběr"
        : "WASD / šipky · jízda   Mezerník · smyk   R · návrat   Pravé tlačítko + myš · kamera   Esc · pauza";
    public void Setup()
    {
        root = new Control(); AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); root.MouseFilter = Control.MouseFilterEnum.Ignore;
        var theme = new Theme(); theme.DefaultFontSize = 24;
        var panel = Style("fff5df", 28); theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetColor("font_color", "Label", new Color("3c5960"));
        theme.SetColor("font_color", "Button", new Color("ffffff"));
        theme.SetStylebox("normal", "Button", Style("5f9e8c", 20)); theme.SetStylebox("hover", "Button", Style("74b5a0", 20));
        theme.SetStylebox("pressed", "Button", Style("4b8575", 20)); root.Theme = theme;
        theme.SetStylebox("disabled", "Button", Style("ead8ab", 20));
        theme.SetStylebox("focus", "Button", new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("edb941"), BorderWidthLeft = 4, BorderWidthRight = 4, BorderWidthTop = 4, BorderWidthBottom = 4, CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomLeft = 20, CornerRadiusBottomRight = 20 });
        theme.SetColor("font_disabled_color", "Button", new Color("3c5960"));
        var brand = new Label { Text = "MATHRACER  /  Kouzelné údolí", Position = new Vector2(32, 24) };
        brand.AddThemeFontSizeOverride("font_size", 25);
        brand.AddThemeColorOverride("font_color", new Color("fff7e4"));
        brand.AddThemeColorOverride("font_outline_color", new Color("245693"));
        brand.AddThemeConstantOverride("outline_size", 3);
        root.AddChild(brand);
        var counterPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(counterPanel);
        counterPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        counterPanel.OffsetLeft = -450; counterPanel.OffsetRight = -32;
        counterPanel.OffsetTop = 20; counterPanel.OffsetBottom = 100;
        animalCount = new Label { Name = "AnimalCount", HorizontalAlignment = HorizontalAlignment.Center };
        animalCount.AddThemeFontSizeOverride("font_size", 25);
        counterPanel.AddChild(animalCount);
        controls = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        controller = Input.GetConnectedJoypads().Count > 0; UpdateControls();
        root.AddChild(controls); controls.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide); controls.OffsetTop = -68; controls.OffsetBottom = -12;
        controls.AddThemeFontSizeOverride("font_size", 20);
        hint = new Label { HorizontalAlignment = HorizontalAlignment.Center }; root.AddChild(hint);
        hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide); hint.OffsetTop = -112; hint.OffsetBottom = -70;
        var center = new CenterContainer(); root.AddChild(center); center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); center.MouseFilter = Control.MouseFilterEnum.Ignore;
        modal = new PanelContainer { CustomMinimumSize = new Vector2(570, 0) }; center.AddChild(modal);
        content = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; content.AddThemeConstantOverride("separation", 18); modal.AddChild(content);
    }
    private static StyleBoxFlat Style(string color, int radius)
        => new() { BgColor = new Color(color), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ContentMarginLeft = 32, ContentMarginRight = 32, ContentMarginTop = 22, ContentMarginBottom = 22, ShadowColor = new Color(0, 0, 0, .12f), ShadowSize = 8 };
    private void Clear()
    {
        menuStick = Vector2.Zero; menuDirection = null;
        foreach (Node c in content.GetChildren()) { content.RemoveChild(c); c.QueueFree(); }
        modal.Show(); hint.Text = "";
    }
    private void Text(string text, int size)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center }; l.AddThemeFontSizeOverride("font_size", size); content.AddChild(l);
    }
    private Button Button(string text, Action action, Container? parent = null)
    {
        var b = new Button { Text = text, ActionMode = BaseButton.ActionModeEnum.Press, CustomMinimumSize = new Vector2(0, 62) }; b.Pressed += action; (parent ?? content).AddChild(b);
        if (root.GetViewport().GuiGetFocusOwner() == null) b.GrabFocus();
        return b;
    }
    public void Title() { Clear(); Text("Vítej v kouzelném údolí", 34); Text("Malé autíčko. Velká dobrodružství.\nProzkoumej cestičky a poznej zvířátka.", 23); Button("Hrát", () => PlayRequested?.Invoke()); }
    public void Pause() { Clear(); Text("Chvilka odpočinku", 34); Button("Pokračovat", () => ResumeRequested?.Invoke()); Button("Konec hry", () => GetTree().Quit()); }
    public void Challenge(Animal animal, IChallenge question, string feedback, System.Collections.Generic.IReadOnlySet<int> rejected)
    {
        Clear(); Text(animal.DisplayName, 30);
        Text(question is LetterChallenge ? "Co začíná na toto písmenko? Vyber obrázek." : "Kolik to je? Vyber odpověď.", 23);
        Text(question.Expression, question is LetterChallenge ? 78 : 54);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 16); content.AddChild(row);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            int answer = choice;
            var button = Button(question is LetterChallenge ? "" : answer.ToString(), () => AnswerSelected?.Invoke(answer), row);
            button.Name = $"Answer{answer}";
            button.CustomMinimumSize = question is LetterChallenge ? new Vector2(220, 180) : new Vector2(140, 84);
            button.AddThemeFontSizeOverride("font_size", 40); button.Disabled = rejected.Contains(answer);
            if (question is LetterChallenge letters)
            {
                button.AddThemeStyleboxOverride("normal", Style("ffffff", 20));
                button.AddThemeStyleboxOverride("hover", Style("e2f3e7", 20));
                button.AddThemeStyleboxOverride("pressed", Style("c4e4d3", 20));
                button.AddThemeStyleboxOverride("focus", new StyleBoxFlat { BgColor = Colors.Transparent, BorderColor = new Color("edb941"), BorderWidthLeft = 4, BorderWidthRight = 4, BorderWidthTop = 4, BorderWidthBottom = 4, CornerRadiusTopLeft = 20, CornerRadiusTopRight = 20, CornerRadiusBottomLeft = 20, CornerRadiusBottomRight = 20 });
                var picture = new WordPicture { PictureId = letters.Pictures[answer].Id, MouseFilter = Control.MouseFilterEnum.Ignore,
                    Modulate = button.Disabled ? new Color(1, 1, 1, .3f) : Colors.White };
                button.AddChild(picture); picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            if (!button.Disabled) first ??= button;
        }
        var message = new Label { Text = feedback, Name = "ChallengeFeedback", HorizontalAlignment = HorizontalAlignment.Center, CustomMinimumSize = new Vector2(0, 32) };
        message.AddThemeFontSizeOverride("font_size", 22); content.AddChild(message);
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke()).Name = "LeaveChallenge";
        first?.GrabFocus();
    }
    public void Reward(Animal animal, VehicleAppearance appearance, System.Collections.Generic.IReadOnlyList<CarReward> rewards)
    {
        Clear(); Text("Správně! To se ti povedlo!", 32);
        Text($"{animal.DisplayName} má pro tebe odměnu.\nVyber si jedno vylepšení autíčka.", 22);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 12); content.AddChild(row);
        Button? first = null;
        var currentLook = System.Linq.Enumerable.First(CarLook.All, look => look.Id == appearance.CurrentLookId);
        foreach (var reward in rewards)
        {
            var card = new VBoxContainer { CustomMinimumSize = new Vector2(155, 0) }; card.AddThemeConstantOverride("separation", 10); row.AddChild(card);
            string name;
            string action;
            Action selectReward;
            if (reward.IsModel)
            {
                var body = System.Linq.Enumerable.First(CarBody.All, option => option.Id == reward.Id);
                card.AddChild(new CarLookPreview { Look = currentLook, ModelId = body.Id });
                name = body.Name; action = "Vyměnit auto";
                selectReward = () => ModelSelected?.Invoke(body.Id);
            }
            else if (reward.IsAccessory)
            {
                var accessory = System.Linq.Enumerable.First(CarAccessory.All, option => option.Id == reward.Id);
                card.AddChild(new CarLookPreview { Look = currentLook, ModelId = appearance.CurrentModelId, AccessoryId = accessory.Id });
                name = accessory.Name;
                action = accessory.Id == "wheels" ? (appearance.HasAccessory(accessory.Id) ? "Menší kola" : "Zvětšit") :
                    appearance.HasAccessory(accessory.Id) ? "Sundat" : "Připnout";
                selectReward = () => AccessorySelected?.Invoke(accessory.Id);
            }
            else
            {
                var look = System.Linq.Enumerable.First(CarLook.All, option => option.Id == reward.Id);
                card.AddChild(new CarLookPreview { Look = look, ModelId = appearance.CurrentModelId });
                name = look.Name; action = "Vybrat";
                selectReward = () => LookSelected?.Invoke(look.Id);
            }
            var label = new Label { Text = name, HorizontalAlignment = HorizontalAlignment.Center }; label.AddThemeFontSizeOverride("font_size", 23); card.AddChild(label);
            var select = Button(action, selectReward, card); select.Name = reward.ButtonName; first ??= select;
        }
        Button("Nechat současný vzhled", () => CloseInteractionRequested?.Invoke()).Name = "KeepLook";
        first?.GrabFocus();
    }
    public void HideModal() => modal.Hide();
    public void AnimalCount(int remaining, int total) => animalCount.Text = remaining == 0
        ? "Všechna zvířátka navštívena!"
        : $"Zbývá zvířátek: {remaining} / {total}";
    public void Hint(bool visible, bool available) => hint.Text = !visible ? "" : available
        ? (controller ? "Stiskni A pro úkol a odměnu" : "Stiskni E pro úkol a odměnu") : "Toto zvířátko už jsi navštívil. Sleduj další šipky!";
}
