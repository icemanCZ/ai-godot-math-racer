using System;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class GameUi : CanvasLayer
{
    public event Action? PlayRequested;
    public event Action? NewGameRequested;
    public event Action? ResumeRequested;
    public event Action? CloseInteractionRequested;
    public event Action<int>? AnswerSelected;
    internal event Action? SyllablesConfirmed;
    internal event Action<string>? SpeechRequested;
    public event Action? TalkRequested;
    public event Action? VillageFruitRequested;
    public event Action<bool>? ExpeditionSelected;
    public event Action<CarReward>? GarageRewardSelected;
    internal event Action<QuizType>? QuizTypeSelected;
    internal event Action<bool>? MathLevelSelected;
    public event Action? GolemToggleRequested;
    internal event Action<CollectorPetKind>? CollectorPetToggleRequested;
    public event Action? GolemQuestRequested;
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
    private Label animalCount = null!;
    private Label controls = null!;
    private bool controller;
    private Vector2 menuStick;
    private Side? menuDirection;
    private float menuRepeat;

    public override void _Input(InputEvent e)
    {
        bool? next =
            e is InputEventJoypadButton || e is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) > .25f
                ? true
            : e is InputEventKey or InputEventMouseButton ? false
            : null;
        if (next.HasValue && controller != next.Value)
        {
            controller = next.Value;
            UpdateControls();
        }
        if (e is InputEventJoypadMotion garageMotion && garageMotion.Axis == JoyAxis.RightY)
        {
            garageStick = Mathf.Abs(garageMotion.AxisValue) > .2f ? garageMotion.AxisValue : 0;
            if (garageCatalogue != null && modal.Visible)
                GetViewport().SetInputAsHandled();
        }
        if (
            modal.Visible
            && e is InputEventJoypadMotion stick
            && (stick.Axis == JoyAxis.LeftX || stick.Axis == JoyAxis.LeftY)
        )
        {
            if (stick.Axis == JoyAxis.LeftX)
                menuStick.X = stick.AxisValue;
            else
                menuStick.Y = stick.AxisValue;
            Side? direction =
                menuStick.Length() < .5f ? null
                : Mathf.Abs(menuStick.X) > Mathf.Abs(menuStick.Y) ? (menuStick.X < 0 ? Side.Left : Side.Right)
                : (menuStick.Y < 0 ? Side.Top : Side.Bottom);
            if (direction != menuDirection)
            {
                menuDirection = direction;
                menuRepeat = .4f;
                if (direction.HasValue)
                    MoveFocus(direction.Value);
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!modal.Visible)
            return;
        if (e.IsEcho() && e.IsAction("ui_accept"))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        Side? arrow =
            e.IsActionPressed("ui_left", allowEcho: true) ? Side.Left
            : e.IsActionPressed("ui_right", allowEcho: true) ? Side.Right
            : e.IsActionPressed("ui_up", allowEcho: true) ? Side.Top
            : e.IsActionPressed("ui_down", allowEcho: true) ? Side.Bottom
            : null;
        if (arrow.HasValue)
        {
            MoveFocus(arrow.Value);
            GetViewport().SetInputAsHandled();
        }
    }

    private void MoveFocus(Side direction)
    {
        var buttons = content
            .FindChildren("*", "Button", true, false)
            .OfType<Button>()
            .Where(button =>
                button.IsVisibleInTree() && !button.Disabled && button.FocusMode != Control.FocusModeEnum.None
            )
            .ToArray();
        if (buttons.Length == 0)
            return;
        var focused = GetViewport().GuiGetFocusOwner();
        int index = Array.IndexOf<Control>(buttons, focused);
        if (index < 0)
        {
            buttons[0].GrabFocus();
            return;
        }
        if (
            direction is Side.Top or Side.Bottom
            && focused!.FindValidFocusNeighbor(direction) is Button neighbor
            && buttons.Contains(neighbor)
        )
        {
            neighbor.GrabFocus();
            return;
        }
        // The button order covers the entire menu, including rows clipped by a scroll container.
        int step = direction is Side.Left or Side.Top ? -1 : 1;
        buttons[(index + step + buttons.Length) % buttons.Length].GrabFocus();
    }

    public override void _Process(double delta)
    {
        if (modal.Visible && garageCatalogue != null && garageStick != 0)
        {
            garageScroll = Mathf.Clamp(
                garageScroll + garageStick * (float)delta * 650,
                0,
                Mathf.Max(
                    0,
                    (float)garageCatalogue.GetVScrollBar().MaxValue - (float)garageCatalogue.GetVScrollBar().Page
                )
            );
            garageCatalogue.ScrollVertical = (int)garageScroll;
        }
        if (!modal.Visible || !menuDirection.HasValue)
            return;
        menuRepeat -= (float)delta;
        if (menuRepeat <= 0)
        {
            MoveFocus(menuDirection.Value);
            menuRepeat = .15f;
        }
    }

    private void UpdateControls() =>
        controls.Text = controller
            ? "RT / LT   ◀ ▶    A · ?    B · ↶    Menu · Ⅱ"
            : "WASD / ↑↓←→    E / mezerník · ?    R · ↶    Esc · Ⅱ";

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
        var brand = new Label { Text = "MATHRACER  /  Kouzelné údolí", Position = new Vector2(32, 24) };
        brand.AddThemeFontSizeOverride("font_size", 25);
        brand.AddThemeColorOverride("font_color", new Color("fff7e4"));
        brand.AddThemeColorOverride("font_outline_color", new Color("245693"));
        brand.AddThemeConstantOverride("outline_size", 3);
        root.AddChild(brand);
        var counterPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(counterPanel);
        counterPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        counterPanel.OffsetLeft = -450;
        counterPanel.OffsetRight = -32;
        counterPanel.OffsetTop = 20;
        counterPanel.OffsetBottom = 100;
        animalCount = new Label { Name = "AnimalCount", HorizontalAlignment = HorizontalAlignment.Center };
        animalCount.AddThemeFontSizeOverride("font_size", 25);
        counterPanel.AddChild(animalCount);
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
        talkButton.Pressed += () => TalkRequested?.Invoke();
        root.AddChild(talkButton);
        talkButton.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        talkButton.OffsetLeft = -65;
        talkButton.OffsetRight = 65;
        talkButton.OffsetTop = -245;
        talkButton.OffsetBottom = -130;
        talkButton.Name = "PrimaryTask";
        SetupEvents();
        SetupCoins();
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
                SpeechRequested?.Invoke(text);
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
        Text("Kouzelné údolí", 34);
        Text($"Seed údolí: {Settings.Seed}", 20);
        Button(saved ? "Pokračovat" : "Hrát", () => PlayRequested?.Invoke()).GrabFocus();
        Button("Nová hra", () => NewGameRequested?.Invoke()).Name = "NewGame";
        if (saved)
            Text("Nová hra nahradí uložený postup.", 18);
    }

    public void Pause()
    {
        Clear();
        Text("Ⅱ", 40);
        Button("Pokračovat", () => ResumeRequested?.Invoke()).GrabFocus();
        Button("Nová hra", () => NewGameRequested?.Invoke()).Name = "NewGame";
        Text("Nová hra nahradí uložený postup.", 18);
        Button("Konec hry", () => GetTree().Quit());
    }

    public void LoadingWorld()
    {
        Clear();
        Text("…", 40);
    }

    public void Challenge(
        IChallenge question,
        System.Collections.Generic.IReadOnlySet<int> rejected,
        SyllableSelection selection,
        int banditStage
    )
    {
        if (question is DeliveryChallenge delivery)
        {
            DeliveryQuestion(delivery, rejected);
            return;
        }
        if (question is LoadChallenge load)
        {
            LoadQuestion(load, rejected);
            return;
        }
        Clear();
        if (banditStage > 0)
            Text($"?  {banditStage} / {Game.BanditQuestionCount}", 27);
        if (question is SyllableChallenge syllables)
        {
            content.AddChild(
                new WordPicture
                {
                    PictureId = syllables.PictureId,
                    CustomMinimumSize = new(0, 155),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            SyllableTiles(syllables, selection, answer => AnswerSelected?.Invoke(answer));
            BackButton("LeaveChallenge");
            return;
        }
        var pictures = question as IPictureChallenge;
        Text(question.Expression, pictures != null ? 78 : 54);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        content.AddChild(row);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            int answer = choice;
            bool wrong = rejected.Contains(answer);
            var button = Button(
                pictures != null ? ""
                    : wrong ? $"× {answer}"
                    : answer.ToString(),
                () => AnswerSelected?.Invoke(answer),
                row,
                narrate: false
            );
            button.Name = $"Answer{answer}";
            button.CustomMinimumSize = pictures != null ? new Vector2(220, 180) : new Vector2(140, 84);
            button.AddThemeFontSizeOverride("font_size", 40);
            button.Disabled = rejected.Contains(answer);
            if (pictures != null)
            {
                button.AddThemeStyleboxOverride("normal", Style("ffffff", 20));
                button.AddThemeStyleboxOverride("hover", Style("e2f3e7", 20));
                button.AddThemeStyleboxOverride("pressed", Style("c4e4d3", 20));
                button.AddThemeStyleboxOverride(
                    "focus",
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
                var word = pictures.Pictures[answer];
                button.AccessibilityName = word.Word;
                var picture = new WordPicture
                {
                    PictureId = word.Id,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Modulate = button.Disabled ? new Color(1, 1, 1, .3f) : Colors.White,
                };
                button.AddChild(picture);
                picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            if (!button.Disabled)
                first ??= button;
        }
        BackButton("LeaveChallenge");
        first?.GrabFocus();
        NarrateAnswerButtons(row, question);
    }

    private void NarrateAnswerButtons(Container row, IChallenge question)
    {
        for (int i = 0; i < question.Choices.Count; i++)
        {
            var button = row.GetChild<Button>(i);
            if (button.Disabled)
                continue;
            int choice = question.Choices[i];
            string label = question is IPictureChallenge pictures ? pictures.Pictures[choice].Word : choice.ToString();
            NarrateHighlight(button, () => label);
        }
    }

    public void HouseReward(Animal animal, int level)
    {
        Clear();
        Text($"✓  {level} / {Village.Upgrades.Length}", 34);
        BackButton().GrabFocus();
    }

    public void HouseCollection(Animal animal, int collected)
    {
        Clear();
        Picture(content, "apple", count: 3);
        Text($"{collected} / 3", 30);
        BackButton().GrabFocus();
    }

    public void HouseFruitOffer(Animal animal, int level, bool canStart)
    {
        Clear();
        Picture(content, "apple", count: 3);
        if (canStart)
        {
            var start = IconButton("play", () => VillageFruitRequested?.Invoke(), "VillageFruitTask");
            start.AccessibilityName = "Vyrazit pro ovoce";
        }
        else
            Text("…", 34);
        BackButton();
    }

    public void ExpeditionChoice(Animal animal, VehicleAppearance appearance)
    {
        Clear();
        talkButton.Hide();
        Text("✓", 38);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 24);
        content.AddChild(row);
        var look = System.Linq.Enumerable.First(CarLook.Catalogue, option => option.Id == appearance.CurrentLookId);
        Button? first = null;
        foreach (bool collection in new[] { false, true })
        {
            var card = new VBoxContainer();
            card.AddThemeConstantOverride("separation", 12);
            row.AddChild(card);
            card.AddChild(
                new CarLookPreview
                {
                    Look = look,
                    ModelId = appearance.CurrentModelId,
                    ExpeditionCargo =
                        collection || animal.Kind == 1 ? "apple"
                        : animal.Kind == 0 ? "carrot"
                        : "honey",
                    CargoAmount = collection ? 0 : 1,
                }
            );
            var select = Button(collection ? "Dovoz" : "Rozvoz", () => ExpeditionSelected?.Invoke(collection), card);
            select.Name = collection ? "ChooseCollection" : "ChooseDelivery";
            first ??= select;
        }
        BackButton("LeaveExpeditionChoice");
        first?.GrabFocus();
    }

    internal void Garage(
        VehicleAppearance appearance,
        System.Collections.Generic.IReadOnlySet<CarReward> owned,
        bool golemOwned,
        bool golemActive,
        System.Collections.Generic.IReadOnlyList<CollectorPet> collectorPets,
        QuizType quizzes,
        bool threeTermMath,
        int coins,
        GarageTrailer? trailer = null
    )
    {
        Clear();
        // The top bar keeps Back always in view, and the arrow keys reach it from the first row.
        var header = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        header.AddThemeConstantOverride("separation", 14);
        content.AddChild(header);
        var leave = IconButton(
            "back",
            () =>
            {
                garageScroll = 0;
                garageFocus = "";
                CloseInteractionRequested?.Invoke();
            },
            "LeaveGarage",
            header
        );
        leave.CustomMinimumSize = new(120, 60);
        leave.AccessibilityName = "Vyjet z garáže";
        var title = new Label { Text = "Garáž", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 32);
        header.AddChild(title);
        header.AddChild(
            new Label
            {
                Text =
                    $"{owned.Count} / {CarLook.Catalogue.Count + CarBody.All.Count + CarAccessory.Catalogue.Count}   ·",
            }
        );
        header.AddChild(new DeliveryPicture { Icon = "coin", CustomMinimumSize = new(40, 36) });
        header.AddChild(new Label { Name = "GarageCoins", Text = coins.ToString() });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 22);
        content.AddChild(row);
        var current = System.Linq.Enumerable.First(CarLook.Catalogue, look => look.Id == appearance.CurrentLookId);
        var car = new VBoxContainer();
        row.AddChild(car);
        var preview = new CarLookPreview
        {
            Look = current,
            ModelId = appearance.CurrentModelId,
            Accessories = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.Select(
                    System.Linq.Enumerable.Where(CarAccessory.Catalogue, a => appearance.HasAccessory(a.Id)),
                    a => a.Id
                )
            ),
        };
        car.AddChild(preview);
        var scroll = new ScrollContainer
        {
            Name = "GarageScroll",
            CustomMinimumSize = new(680, 350),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            FollowFocus = true,
        };
        row.AddChild(scroll);
        garageCatalogue = scroll;
        scroll.GetVScrollBar().ValueChanged += value => garageScroll = (float)value;
        var catalogue = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(catalogue);
        Button? first = null;
        Button? firstPet = null;
        Button? firstQuiz = null;
        Button? selectedFocus = null;
        catalogue.AddChild(new Label { Text = "Typy úkolů · alespoň jeden zapnutý" });
        var quizRow = new HBoxContainer();
        quizRow.AddThemeConstantOverride("separation", 8);
        catalogue.AddChild(quizRow);
        foreach (
            var (type, label, icon) in new[]
            {
                (QuizType.Letters, "Písmenka", "quiz-letters"),
                (QuizType.Syllables, "Slabiky", "quiz-syllables"),
                (QuizType.Reading, "Čtení", "quiz-reading"),
                (QuizType.Math, "Příklady", "quiz-math"),
            }
        )
        {
            bool active = quizzes.HasFlag(type);
            QuizButton(
                quizRow,
                $"GarageQuiz_{type}",
                label,
                active,
                () => QuizTypeSelected?.Invoke(type),
                icon,
                required: quizzes == type
            );
        }
        catalogue.AddChild(new Label { Text = "Úroveň příkladů" });
        var levelRow = new HBoxContainer();
        levelRow.AddThemeConstantOverride("separation", 8);
        catalogue.AddChild(levelRow);
        QuizButton(levelRow, "GarageMathLevel1", "1 · Dvě čísla", !threeTermMath, () =>
            MathLevelSelected?.Invoke(false)
        );
        QuizButton(levelRow, "GarageMathLevel2", "2 · Tři čísla", threeTermMath, () => MathLevelSelected?.Invoke(true));

        void QuizButton(
            HBoxContainer parent,
            string name,
            string label,
            bool active,
            Action select,
            string? icon = null,
            bool required = false
        )
        {
            var button = Button(
                icon == null ? label : "",
                () =>
                {
                    garageScroll = scroll.ScrollVertical;
                    garageFocus = name;
                    select();
                },
                parent
            );
            button.Name = name;
            if (!required)
                firstQuiz ??= button;
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.CustomMinimumSize = new(0, icon == null ? 50 : 80);
            button.AddThemeFontSizeOverride("font_size", 18);
            if (!active)
                button.AddThemeStyleboxOverride("normal", Style("62747a", 20));
            if (icon != null)
            {
                string status = $"{label} · {(active ? "zapnuto" : "vypnuto")}";
                button.AccessibilityName = status;
                button.TooltipText = status;
                var image = new DeliveryPicture { Icon = icon, MouseFilter = Control.MouseFilterEnum.Ignore };
                button.AddChild(image);
                image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                image.OffsetTop = 7;
                image.OffsetBottom = -7;
                var badge = new Label { Text = active ? "✓" : "○", MouseFilter = Control.MouseFilterEnum.Ignore };
                badge.AddThemeColorOverride("font_color", new Color(required ? "316754" : "fff5df"));
                badge.AddThemeFontSizeOverride("font_size", 19);
                button.AddChild(badge);
                badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
                badge.OffsetLeft = -29;
                badge.OffsetTop = 6;
            }
            if (required)
            {
                button.Disabled = true;
                button.AddThemeStyleboxOverride("disabled", Style("cde6cb", 20));
                button.TooltipText = "Alespoň jeden typ úkolů musí zůstat zapnutý.";
            }
            if (garageFocus == name)
                selectedFocus = button;
        }
        var gray = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code =
                    "shader_type canvas_item; void fragment() { vec4 c = texture(TEXTURE, UV); float g = dot(c.rgb, vec3(0.299, 0.587, 0.114)); COLOR = vec4(vec3(g), c.a); }",
            },
        };
        void Category(string title, System.Collections.Generic.IEnumerable<(CarReward Reward, string Name)> items)
        {
            var label = new Label { Text = title };
            catalogue.AddChild(label);
            var grid = new GridContainer { Columns = 3 };
            grid.AddThemeConstantOverride("h_separation", 8);
            grid.AddThemeConstantOverride("v_separation", 8);
            catalogue.AddChild(grid);
            foreach (var (reward, name) in items)
            {
                bool unlocked = owned.Contains(reward);
                bool active =
                    reward.IsModel ? appearance.CurrentModelId == reward.Id
                    : reward.IsAccessory ? appearance.HasAccessory(reward.Id)
                    : appearance.CurrentLookId == reward.Id;
                var card = new VBoxContainer();
                grid.AddChild(card);
                var sample = new CarLookPreview
                {
                    Look =
                        reward.IsModel || reward.IsAccessory
                            ? current
                            : System.Linq.Enumerable.First(CarLook.Catalogue, l => l.Id == reward.Id),
                    ModelId = reward.IsModel ? reward.Id : appearance.CurrentModelId,
                    AccessoryId = reward.IsAccessory ? reward.Id : null,
                    AccessoryOnly = reward.IsAccessory,
                };
                // Anything that can be bought keeps its real colours; only special prizes stay grey.
                if (!unlocked && reward.Price == null)
                    sample.Material = gray;
                card.AddChild(sample);
                int? price = unlocked ? null : reward.Price;
                bool affordable = price <= coins;
                var button = Button(
                    (active ? "✓ " : "") + name,
                    () =>
                    {
                        garageScroll = scroll.ScrollVertical;
                        garageFocus = reward.ButtonName;
                        GarageRewardSelected?.Invoke(reward);
                    },
                    card
                );
                button.Name = "Garage_" + reward.ButtonName;
                button.Disabled = !unlocked && !affordable;
                button.CustomMinimumSize = new(205, 48);
                button.AddThemeFontSizeOverride("font_size", 17);
                if (price is int cost)
                {
                    // A price tag between the preview and the button.
                    button.AccessibilityName = $"Koupit {name} za {cost} penízků";
                    button.TooltipText = affordable
                        ? $"Koupit za {cost} penízků"
                        : $"Stojí {cost} penízků. Sbírej penízky ve světě a plň úkoly.";
                    var tag = new HBoxContainer
                    {
                        Name = "Price_" + reward.ButtonName,
                        Alignment = BoxContainer.AlignmentMode.Center,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    tag.AddThemeConstantOverride("separation", 4);
                    card.AddChild(tag);
                    card.MoveChild(tag, button.GetIndex());
                    tag.AddChild(
                        new DeliveryPicture
                        {
                            Icon = "coin",
                            CustomMinimumSize = new(26, 26),
                            MouseFilter = Control.MouseFilterEnum.Ignore,
                        }
                    );
                    var priceLabel = new Label
                    {
                        Text = cost.ToString(),
                        VerticalAlignment = VerticalAlignment.Center,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    priceLabel.AddThemeFontSizeOverride("font_size", 17);
                    priceLabel.AddThemeColorOverride("font_color", new Color(affordable ? "b07812" : "8a8a8a"));
                    tag.AddChild(priceLabel);
                    if (affordable)
                    {
                        button.AddThemeStyleboxOverride("normal", Style("d9a02a", 16));
                        button.AddThemeStyleboxOverride("hover", Style("e8b23a", 16));
                        button.AddThemeStyleboxOverride("pressed", Style("c08a1e", 16));
                    }
                }
                else
                {
                    // Keeps the buttons of a grid row aligned with the price tags beside them.
                    var spacer = new Control
                    {
                        CustomMinimumSize = new(0, 26),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    };
                    card.AddChild(spacer);
                    card.MoveChild(spacer, button.GetIndex());
                }
                if (button.Disabled)
                {
                    button.AddThemeStyleboxOverride("disabled", Style("d5d5d5", 16));
                    button.AddThemeColorOverride("font_disabled_color", new Color("777777"));
                    if (price == null)
                        button.TooltipText = "Tuto odměnu získáš za zvláštní úkol.";
                }
                else
                    first ??= button;
                if (!button.Disabled && garageFocus == reward.ButtonName)
                    selectedFocus = button;
            }
        }
        catalogue.AddChild(new Label { Text = "Mazlíčci" });
        var petGrid = new GridContainer { Columns = 5 };
        petGrid.AddThemeConstantOverride("h_separation", 8);
        petGrid.AddThemeConstantOverride("v_separation", 8);
        catalogue.AddChild(petGrid);
        void PetButton(string id, string name, Node3D model, bool unlocked, bool active, string hint, Action toggle)
        {
            string status = unlocked
                ? active
                    ? "Vypnout"
                    : "Zapnout"
                : "Zatím nezískán";
            var button = Button(
                "",
                () =>
                {
                    garageScroll = scroll.ScrollVertical;
                    garageFocus = "GaragePet_" + id;
                    toggle();
                },
                petGrid
            );
            button.Name = "GaragePet_" + id;
            button.CustomMinimumSize = new(120, 112);
            button.Disabled = !unlocked;
            button.AccessibilityName = $"{name} · {status}";
            button.TooltipText = unlocked ? $"{name} · {status}" : $"{name}\n{hint}";
            if (!unlocked)
                button.AddThemeStyleboxOverride("disabled", Style("d5d5d5", 16));
            AddPetPreview(button, model, unlocked ? null : gray, active);
            if (unlocked)
                firstPet ??= button;
            if (unlocked && garageFocus == button.Name)
                selectedFocus = button;
        }
        foreach (var pet in collectorPets)
            PetButton(
                pet.Kind.ToString(),
                pet.DisplayName,
                pet.CreatePreviewModel(),
                pet.IsOwned,
                pet.IsActive,
                $"{pet.Collector.DisplayName} · {pet.Count} / {pet.Goal}",
                () => CollectorPetToggleRequested?.Invoke(pet.Kind)
            );
        var golemPreview = GolemModel.Create();
        golemPreview.Scale = Vector3.One * .64f;
        golemPreview.RotateY(Mathf.Pi);
        PetButton("Golem", "Malý golem", golemPreview, golemOwned, golemActive, "Černokněžník Baltazar", () =>
            GolemToggleRequested?.Invoke()
        );
        if (trailer is { } towing)
            GarageTrailerRow(catalogue, scroll, towing, ref selectedFocus);
        Category("Laky", System.Linq.Enumerable.Select(CarLook.Catalogue, l => (new CarReward(l.Id, false), l.Name)));
        Category("Modely", System.Linq.Enumerable.Select(CarBody.All, b => (new CarReward(b.Id, false, true), b.Name)));
        Category(
            "Doplňky",
            System.Linq.Enumerable.Select(CarAccessory.Catalogue, a => (new CarReward(a.Id, true), a.Name))
        );
        (selectedFocus ?? firstQuiz ?? firstPet ?? first)?.GrabFocus();
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, (int)garageScroll);
    }

    public void GarageHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Garáž" : "E · Garáž";
    }

    public void HouseHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Domeček" : "E · Domeček";
    }

    public void FarmHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Farma" : "E · Farma";
    }

    public void GardenHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Zahradník" : "E · Zahradník";
    }

    public void CollectionHint(bool toys)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + (toys ? "Hračkář" : "Sběratel drahokamů");
    }

    public void CollectionStatus(Village village, Animal owner, Expedition? expedition, bool returned = false)
    {
        bool toys = owner.Role is ExhibitCollectorRole { Toys: true };
        int count = village.CollectionCount(toys);
        Clear();
        Text(owner.DisplayName, 30);
        Text($"{(returned ? "✓  " : "")}{count} / {Village.CollectionNames(toys).Length}", 28);
        if (expedition?.Sender == owner)
            Text(expedition.CollectionName, 22);
        else if (expedition != null)
            Text("…", 26);
        CollectorPetStatus(village.PetFor(owner));
        BackButton().GrabFocus();
    }

    public void GardenStatus(Village village, Expedition? expedition, bool returned = false)
    {
        Clear();
        Text("Zahradník Vítek", 32);
        Text($"{(returned ? "✓  " : "")}{village.GardenCount} / {Village.FlowerNames.Length}", 28);
        if (expedition?.Garden == true)
            Text(expedition.FlowerName, 22);
        else if (expedition != null)
            Text("…", 26);
        CollectorPetStatus(village.PetFor(village.Gardener));
        BackButton().GrabFocus();
    }

    public void FarmStatus(Village village, Expedition? expedition, bool returned = false)
    {
        Clear();
        Text("Farmář Tonda", 32);
        Text($"{(returned ? "✓  " : "")}{village.FarmCount} / {Village.FarmKinds.Length}", 28);
        if (expedition?.Farm == true)
            Text(expedition.FarmName, 22);
        else if (expedition != null)
            Text("…", 26);
        CollectorPetStatus(village.PetFor(village.Farmer));
        BackButton().GrabFocus();
    }

    private void CollectorPetStatus(CollectorPet pet) =>
        Text(pet.IsOwned ? $"✓ {pet.DisplayName}" : $"{pet.DisplayName} · {pet.Goal}", 20);

    public void HideModal()
    {
        modal.Hide();
        deliveryHud.Visible = directionPicture != null;
    }

    public void AnimalCount(int remaining, int total) =>
        animalCount.Text = remaining == 0 ? "Všechna zvířátka navštívena!" : $"Zbývá zvířátek: {remaining} / {total}";

    public void Hint(bool visible, bool available) =>
        hint.Text =
            !visible ? ""
            : available ? (controller ? "Stiskni A pro úkol a odměnu" : "Stiskni E nebo mezerník pro úkol a odměnu")
            : "Toto zvířátko už jsi navštívil. Sleduj další otazníky!";

    private DeliveryPicture? Picture(Container parent, string icon, Animal? animal = null, int count = 1)
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
            Animal = animal,
            Count = count,
            CustomMinimumSize = new(150, 125),
        };
        parent.AddChild(picture);
        return picture;
    }

    private Button IconButton(string icon, Action action, string name, Container? parent = null)
    {
        var button = Button("", action, parent);
        button.Name = name;
        button.CustomMinimumSize = new(150, 76);
        var picture = new DeliveryPicture { Icon = icon, MouseFilter = Control.MouseFilterEnum.Ignore };
        button.AddChild(picture);
        picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return button;
    }

    private Button BackButton(string name = "Back", Container? parent = null)
    {
        var button = IconButton("back", () => CloseInteractionRequested?.Invoke(), name, parent);
        button.AccessibilityName = "Zpět";
        return button;
    }

    private void DeliveryQuestion(DeliveryChallenge question, System.Collections.Generic.IReadOnlySet<int> rejected)
    {
        Clear();
        Picture(content, question.Cargo, null, question.Answer);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        content.AddChild(row);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            var button = Button(
                rejected.Contains(choice) ? $"× {choice}" : choice.ToString(),
                () => AnswerSelected?.Invoke(choice),
                row,
                narrate: false
            );
            button.Name = $"Answer{choice}";
            button.CustomMinimumSize = new(180, 130);
            button.Disabled = rejected.Contains(choice);
            button.AddThemeFontSizeOverride("font_size", 40);
            if (!button.Disabled)
                first ??= button;
        }
        BackButton("LeaveChallenge");
        first?.GrabFocus();
        NarrateAnswerButtons(row, question);
    }

    public void DeliveryHud(Expedition? delivery)
    {
        foreach (Node child in deliveryHud.GetChildren())
        {
            deliveryHud.RemoveChild(child);
            child.QueueFree();
        }
        directionPicture = null;
        deliveryHud.Visible = delivery != null;
        if (delivery == null)
            return;
        directionPicture = Picture(deliveryHud, "arrow");
        directionPicture!.CustomMinimumSize = new(80, 80);
        Control? destination =
            delivery.CanDeliver ? new DeliveryPicture { Animal = delivery.Recipient }
            : delivery.Garden ? new WordPicture { PictureId = "flower" }
            : delivery.SpecialCollection || delivery.Golem ? null
            : new DeliveryPicture { Icon = delivery.Cargo };
        if (destination != null)
        {
            destination.CustomMinimumSize = new(80, 80);
            deliveryHud.AddChild(destination);
        }
        string progress = delivery.Collection ? $"{delivery.Collected} / {delivery.Amount}" : $"× {delivery.Amount}";
        var label = new Label
        {
            Name = "NavigationText",
            Text = progress,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new(100, 80),
        };
        deliveryHud.AddChild(label);
    }

    public void DeliveryDirection(Vector3 waypoint, Vehicle car)
    {
        if (directionPicture == null)
            return;
        var delta = waypoint - car.Position;
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return;
        var forward = -camera.GlobalBasis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var right = camera.GlobalBasis.X;
        right.Y = 0;
        right = right.Normalized();
        directionPicture.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        directionPicture.QueueRedraw();
    }

    public void DeliveryHint(bool visible)
    {
        talkButton.Visible = visible && !modal.Visible;
        talkButton.Text = controller ? "A · ?" : "E · ?";
        hint.Text = "";
    }
}
