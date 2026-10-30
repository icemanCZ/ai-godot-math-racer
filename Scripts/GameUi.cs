using Godot;
using System;

namespace MathRacer;

public partial class GameUi : CanvasLayer
{
    public event Action? PlayRequested;
    public event Action? NewGameRequested;
    public event Action? ResumeRequested;
    public event Action? CloseInteractionRequested;
    public event Action<int>? AnswerSelected;
    public event Action<string>? LookSelected;
    public event Action<string>? AccessorySelected;
    public event Action<string>? ModelSelected;
    public event Action? TalkRequested;
    public event Action? VillageFruitRequested;
    public event Action<bool>? ExpeditionSelected;
    public event Action<CarReward>? GarageRewardSelected;
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
        bool? next = e is InputEventJoypadButton || e is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) > .25f
            ? true : e is InputEventKey or InputEventMouseButton ? false : null;
        if (next.HasValue && controller != next.Value) { controller = next.Value; UpdateControls(); }
        if (e is InputEventJoypadMotion garageMotion && garageMotion.Axis == JoyAxis.RightY)
        {
            garageStick = Mathf.Abs(garageMotion.AxisValue) > .2f ? garageMotion.AxisValue : 0;
            if (garageCatalogue != null && modal.Visible) GetViewport().SetInputAsHandled();
        }
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
        if (modal.Visible && garageCatalogue != null && garageStick != 0)
        {
            garageScroll = Mathf.Clamp(garageScroll + garageStick * (float)delta * 650, 0,
                Mathf.Max(0, (float)garageCatalogue.GetVScrollBar().MaxValue - (float)garageCatalogue.GetVScrollBar().Page));
            garageCatalogue.ScrollVertical = (int)garageScroll;
        }
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
        deliveryHud = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        root.AddChild(deliveryHud); deliveryHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        deliveryHud.OffsetLeft = -320; deliveryHud.OffsetRight = 320; deliveryHud.OffsetTop = 20;
        var deliveryBackground = Style("fff5df", 24);
        deliveryHud.Draw += () => deliveryHud.DrawStyleBox(deliveryBackground, new Rect2(Vector2.Zero, deliveryHud.Size));
        deliveryHud.Resized += deliveryHud.QueueRedraw;
        talkButton = new Button { Visible = false, Text = "", CustomMinimumSize = new(130, 100) };
        talkButton.Pressed += () => TalkRequested?.Invoke(); root.AddChild(talkButton);
        talkButton.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        talkButton.OffsetLeft = -65; talkButton.OffsetRight = 65; talkButton.OffsetTop = -245; talkButton.OffsetBottom = -130;
        talkButton.Name = "PrimaryTask";
        SetupEvents();
    }
    private static StyleBoxFlat Style(string color, int radius)
        => new() { BgColor = new Color(color), CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius, ContentMarginLeft = 32, ContentMarginRight = 32, ContentMarginTop = 22, ContentMarginBottom = 22, ShadowColor = new Color(0, 0, 0, .12f), ShadowSize = 8 };
    private void Clear()
    {
        deliveryHud.Hide();
        talkButton.Hide();
        garageCatalogue = null; garageStick = 0;
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
        var b = new Button { Text = text, ActionMode = BaseButton.ActionModeEnum.Press, CustomMinimumSize = new Vector2(0, 62) };
        b.Pressed += () => { GetViewport().SetInputAsHandled(); action(); }; (parent ?? content).AddChild(b);
        if (root.GetViewport().GuiGetFocusOwner() == null) b.GrabFocus();
        return b;
    }
    public void Title(bool saved = false)
    {
        Clear(); Text("Vítej v kouzelném údolí", 34);
        Text(saved ? "Tvoje dobrodružství je uložené. Můžeš pokračovat." : "Malé autíčko. Velká dobrodružství.", 23);
        Text($"Seed údolí: {Settings.Seed}", 20);
        Button(saved ? "Pokračovat" : "Hrát", () => PlayRequested?.Invoke()).GrabFocus();
        Button("Nová hra", () => NewGameRequested?.Invoke()).Name = "NewGame";
        if (saved) Text("Nová hra nahradí uložený postup.", 18);
    }
    public void Pause()
    {
        Clear(); Text("Chvilka odpočinku", 34); Text($"Seed údolí: {Settings.Seed}", 20);
        Button("Pokračovat", () => ResumeRequested?.Invoke()).GrabFocus();
        Button("Nová hra", () => NewGameRequested?.Invoke()).Name = "NewGame";
        Text("Nová hra nahradí uložený postup.", 18);
        Button("Konec hry", () => GetTree().Quit());
    }
    public void LoadingWorld() { Clear(); Text("Připravujeme nové údolí…", 30); }
    public void Challenge(Animal animal, IChallenge question, string feedback, System.Collections.Generic.IReadOnlySet<int> rejected)
    {
        if (question is DeliveryChallenge delivery) { DeliveryQuestion(animal, delivery, rejected); return; }
        Clear(); Text(animal.DisplayName, 30);
        if (animal.IsFarmer) Text("Výprava za zvířátkem · Nejdřív malý úkol", 23);
        if (animal.IsGardener) Text("Výprava pro květinu · Nejdřív malý úkol", 23);
        if (animal.IsCollectionOwner) Text(animal.IsToymaker ? "Výprava pro hračku · Nejdřív malý úkol" : "Výprava pro drahokam · Nejdřív malý úkol", 23);
        if (animal.HouseIndex >= 0) Text("Pomoz mi vylepšit domeček!", 23);
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
    public void HouseReward(Animal animal, int level)
    {
        Clear(); Text(animal.DisplayName, 30);
        Text(level == 10 ? "Domeček je hotový! Děkuji za pomoc!" : "Správně! Domeček je krásnější!", 30);
        Text($"Vylepšení {level} / {Village.Upgrades.Length}", 28);
        if (level > 0) Text($"✓ {Village.Upgrades[level - 1]}", 25);
        if (level < Village.Upgrades.Length) Text($"Příště: {Village.Upgrades[level]}", 23);
        Button("Prohlédnout domeček", () => CloseInteractionRequested?.Invoke()).GrabFocus();
    }
    public void HouseCollection(Animal animal, int collected)
    {
        Clear(); Text(animal.DisplayName, 30);
        Text("Nasbírej a přivez mi 3 jablka.", 28);
        Text($"Nasbíráno: {collected} / 3", 28);
        Text("Jeď za zelenými šipkami a posbírej jablka.\nPotom se vrať pro vylepšení domečku.", 22);
        Button("Pokračovat ve sběru", () => CloseInteractionRequested?.Invoke()).GrabFocus();
    }
    public void HouseFruitOffer(Animal animal, int level, bool canStart)
    {
        Clear(); Text(animal.DisplayName, 30);
        Text("Nasbírej a přivez mi 3 jablka.", 28);
        Text($"Domeček {level} / 10 · Odměna: {Village.Upgrades[level]}", 22);
        Text("Posbírej označená jablka podle šipek\na přivez je zpátky k mému domečku.", 22);
        if (canStart) Button("Vyrazit pro ovoce", () => VillageFruitRequested?.Invoke()).Name = "VillageFruitTask";
        else Text("Nejdřív dokonči rozjetou výpravu.\nTento úkol tu na tebe počká.", 22);
        Button("Později", () => CloseInteractionRequested?.Invoke());
    }
    public void Reward(Animal animal, VehicleAppearance appearance, System.Collections.Generic.IReadOnlyList<CarReward> rewards)
    {
        Clear();
        Text("Úkol splněn! Vyber si odměnu.", 32);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 12); content.AddChild(row);
        Button? first = null;
        var currentLook = System.Linq.Enumerable.First(CarLook.All, look => look.Id == appearance.CurrentLookId);
        foreach (var reward in rewards)
        {
            var card = new VBoxContainer(); card.AddThemeConstantOverride("separation", 10); row.AddChild(card);
            Action selectReward;
            string symbol;
            if (reward.IsModel)
            {
                card.AddChild(new CarLookPreview { Look = currentLook, ModelId = reward.Id });
                symbol = "vehicle";
                selectReward = () => ModelSelected?.Invoke(reward.Id);
            }
            else if (reward.IsAccessory)
            {
                card.AddChild(new CarLookPreview { Look = currentLook, ModelId = appearance.CurrentModelId, AccessoryId = reward.Id });
                symbol = "upgrade";
                selectReward = () => AccessorySelected?.Invoke(reward.Id);
            }
            else
            {
                var look = System.Linq.Enumerable.First(CarLook.All, option => option.Id == reward.Id);
                card.AddChild(new CarLookPreview { Look = look, ModelId = appearance.CurrentModelId });
                symbol = "paint";
                selectReward = () => LookSelected?.Invoke(reward.Id);
            }
            var select = IconButton(symbol, selectReward, reward.ButtonName, card);
            first ??= select;
        }
        IconButton("back", () => CloseInteractionRequested?.Invoke(), "KeepLook");
        first?.GrabFocus();
    }
    public void ExpeditionChoice(Animal animal, VehicleAppearance appearance)
    {
        Clear(); talkButton.Hide();
        Text("Správně! Vyber si úkol.", 32);
        Text("Rozvez náklad, nebo přivez jablka zpět.", 23);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; row.AddThemeConstantOverride("separation", 24); content.AddChild(row);
        var look = System.Linq.Enumerable.First(CarLook.All, option => option.Id == appearance.CurrentLookId);
        Button? first = null;
        foreach (bool collection in new[] { false, true })
        {
            var card = new VBoxContainer(); card.AddThemeConstantOverride("separation", 12); row.AddChild(card);
            card.AddChild(new CarLookPreview { Look = look, ModelId = appearance.CurrentModelId,
                ExpeditionCargo = collection || animal.Kind == 1 ? "apple" : animal.Kind == 0 ? "carrot" : "honey", CargoAmount = collection ? 0 : 1 });
            var select = Button(collection ? "Dovoz" : "Rozvoz", () => ExpeditionSelected?.Invoke(collection), card);
            select.Name = collection ? "ChooseCollection" : "ChooseDelivery";
            first ??= select;
        }
        Button("Později", () => CloseInteractionRequested?.Invoke()).Name = "LeaveExpeditionChoice";
        first?.GrabFocus();
    }
    public void Garage(VehicleAppearance appearance, System.Collections.Generic.IReadOnlySet<CarReward> owned)
    {
        Clear(); Text("Garáž", 32);
        Text($"Získáno {owned.Count} / {CarLook.All.Count + CarBody.All.Count + CarAccessory.All.Count}  ·  Šedé odměny získáš za úkoly", 20);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 22); content.AddChild(row);
        var current = System.Linq.Enumerable.First(CarLook.All, look => look.Id == appearance.CurrentLookId);
        var car = new VBoxContainer(); row.AddChild(car);
        var preview = new CarLookPreview { Look = current, ModelId = appearance.CurrentModelId,
            Accessories = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(System.Linq.Enumerable.Where(CarAccessory.All, a => appearance.HasAccessory(a.Id)), a => a.Id)) };
        car.AddChild(preview);
        var caption = new Label { Text = "Tvoje auto\n\nVyber lak nebo model.\nDoplňky zapneš i vypneš.\n\n✓ = nasazeno\n\nKolečko / pravá páčka\nprochází odměny", HorizontalAlignment = HorizontalAlignment.Center };
        caption.AddThemeFontSizeOverride("font_size", 19); car.AddChild(caption);
        var scroll = new ScrollContainer { Name = "GarageScroll", CustomMinimumSize = new(680, 350), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        row.AddChild(scroll);
        garageCatalogue = scroll;
        scroll.GetVScrollBar().ValueChanged += value => garageScroll = (float)value;
        var catalogue = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(catalogue);
        Button? first = null;
        Button? selectedFocus = null;
        var gray = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment() { vec4 c = texture(TEXTURE, UV); float g = dot(c.rgb, vec3(0.299, 0.587, 0.114)); COLOR = vec4(vec3(g), c.a); }" } };
        void Category(string title, System.Collections.Generic.IEnumerable<(CarReward Reward, string Name)> items)
        {
            var label = new Label { Text = title }; catalogue.AddChild(label);
            var grid = new GridContainer { Columns = 3 }; grid.AddThemeConstantOverride("h_separation", 8); grid.AddThemeConstantOverride("v_separation", 8); catalogue.AddChild(grid);
            foreach (var (reward, name) in items)
            {
                bool unlocked = owned.Contains(reward);
                bool active = reward.IsModel ? appearance.CurrentModelId == reward.Id : reward.IsAccessory ? appearance.HasAccessory(reward.Id) : appearance.CurrentLookId == reward.Id;
                var card = new VBoxContainer(); grid.AddChild(card);
                var sample = new CarLookPreview { Look = reward.IsModel || reward.IsAccessory ? current : System.Linq.Enumerable.First(CarLook.All, l => l.Id == reward.Id),
                    ModelId = reward.IsModel ? reward.Id : appearance.CurrentModelId, AccessoryId = reward.IsAccessory ? reward.Id : null };
                if (!unlocked) sample.Material = gray;
                card.AddChild(sample);
                var button = Button((active ? "✓ " : "") + name, () =>
                {
                    garageScroll = scroll.ScrollVertical; garageFocus = reward.ButtonName;
                    GarageRewardSelected?.Invoke(reward);
                }, card);
                button.Name = "Garage_" + reward.ButtonName; button.Disabled = !unlocked; button.CustomMinimumSize = new(205, 48);
                button.AddThemeFontSizeOverride("font_size", 17);
                if (!unlocked)
                {
                    button.AddThemeStyleboxOverride("disabled", Style("d5d5d5", 16));
                    button.AddThemeColorOverride("font_disabled_color", new Color("777777"));
                    button.TooltipText = "Tuto odměnu ještě nemáš.";
                }
                else first ??= button;
                if (unlocked && garageFocus == reward.ButtonName) selectedFocus = button;
            }
        }
        Category("Laky", System.Linq.Enumerable.Select(CarLook.All, l => (new CarReward(l.Id, false), l.Name)));
        Category("Modely", System.Linq.Enumerable.Select(CarBody.All, b => (new CarReward(b.Id, false, true), b.Name)));
        Category("Doplňky", System.Linq.Enumerable.Select(CarAccessory.All, a => (new CarReward(a.Id, true), a.Name)));
        Button("Vyjet z garáže", () => { garageScroll = 0; garageFocus = ""; CloseInteractionRequested?.Invoke(); }).Name = "LeaveGarage";
        (selectedFocus ?? first)?.GrabFocus();
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
        bool toys = owner.IsToymaker;
        int count = village.CollectionCount(toys);
        Clear(); Text(owner.DisplayName, 30);
        Text(returned ? toys ? "Děkuji! Hračka už je na hřišti!" : "Děkuji! Drahokam už je v muzeu!" : toys ? "Vítej v hračkářství!" : "Vítej v muzeu drahokamů!", 28);
        Text($"{(toys ? "Hračky na hřišti" : "Drahokamy v muzeu")}: {count} / {Village.CollectionNames(toys).Length}", 26);
        if (expedition?.Sender == owner)
            Text($"Přivez {expedition.CollectionName}.\nJeď za šipkami a sjeď z cesty k předmětu.\nPotom se vrať ke mně.", 22);
        else if (expedition != null) Text("Nejdřív dokonči rozjetou výpravu.\nTento úkol tu na tebe počká.", 22);
        else if (count == Village.CollectionNames(toys).Length) Text(toys ? "Hřiště je plné hraček! Přijeď zase na návštěvu." : "Sbírka je kompletní! Přijeď si ji zase prohlédnout.", 22);
        else Text("Přijeď za mnou pro další výpravu.\nPřed cestou tě čeká příklad nebo písmenko.", 22);
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke()).GrabFocus();
    }
    public void GardenStatus(Village village, Expedition? expedition, bool returned = false)
    {
        Clear(); Text("Zahradník Vítek", 32);
        Text(returned ? "Děkuji! Květina už roste v záhonku!" : "Vítej v zahradnictví!", 28);
        Text($"Rozkvetlé záhonky: {village.GardenCount} / {Village.FlowerNames.Length}", 26);
        if (expedition?.Garden == true)
            Text($"Přivez {expedition.FlowerName}.\nJeď za šipkami a sjeď z cesty ke květině.\nPak ji přivez zpátky ke mně.", 22);
        else if (expedition != null) Text("Nejdřív dokonči rozjetou výpravu.\nKvětiny tu na tebe počkají.", 22);
        else if (village.GardenCount == Village.FlowerNames.Length) Text("Celá zahrádka kvete. Přijeď se zase podívat!", 22);
        else Text("Přijeď za mnou pro další květinu.\nPřed cestou tě čeká příklad nebo písmenko.", 22);
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke()).GrabFocus();
    }
    public void FarmStatus(Village village, Expedition? expedition, bool returned = false)
    {
        Clear(); Text("Farmář Tonda", 32);
        Text(returned ? "Děkuji! Zvířátko už je v ohradě!" : "Vítej na farmě!", 28);
        Text($"Zvířátka v ohradě: {village.FarmCount} / {Village.FarmKinds.Length}", 26);
        if (expedition?.Farm == true)
            Text($"Najdi {expedition.FarmName}.\nJeď za šipkami a přibliž se ke zvířátku.\nPak ho přivez zpátky ke mně.", 22);
        else if (expedition != null) Text("Nejdřív dokonči rozjetou výpravu.\nZvířátka tu na tebe počkají.", 22);
        else if (village.FarmCount == Village.FarmKinds.Length) Text("Všechna zvířátka jsou doma. Přijeď je zase navštívit!", 22);
        else Text("Přijeď za mnou pro další výpravu.\nPřed cestou tě čeká příklad nebo písmenko.", 22);
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke()).GrabFocus();
    }
    public void HideModal() { modal.Hide(); deliveryHud.Visible = directionPicture != null; }
    public void AnimalCount(int remaining, int total) => animalCount.Text = remaining == 0
        ? "Všechna zvířátka navštívena!"
        : $"Zbývá zvířátek: {remaining} / {total}";
    public void Hint(bool visible, bool available) => hint.Text = !visible ? "" : available
        ? (controller ? "Stiskni A pro úkol a odměnu" : "Stiskni E pro úkol a odměnu") : "Toto zvířátko už jsi navštívil. Sleduj další otazníky!";

    private DeliveryPicture? Picture(Container parent, string icon, Animal? animal = null, int count = 1)
    {
        if (icon == "car")
        {
            var frame = new CenterContainer(); parent.AddChild(frame);
            frame.AddChild(new WordPicture { PictureId = "car", CustomMinimumSize = new(180, 140), MouseFilter = Control.MouseFilterEnum.Ignore });
            return null;
        }
        var picture = new DeliveryPicture { Icon = icon, Animal = animal, Count = count, CustomMinimumSize = new(150, 125) };
        parent.AddChild(picture); return picture;
    }
    private Button IconButton(string icon, Action action, string name, Container? parent = null)
    {
        var button = Button("", action, parent); button.Name = name; button.CustomMinimumSize = new(150, 76);
        var picture = new DeliveryPicture { Icon = icon }; button.AddChild(picture); picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return button;
    }
    private void DeliveryQuestion(Animal animal, DeliveryChallenge question, System.Collections.Generic.IReadOnlySet<int> rejected)
    {
        Clear(); Text(animal.DisplayName, 30); Text("Kolik předmětů vezeš? Vyber počet.", 23);
        Picture(content, question.Cargo, null, question.Answer);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center }; content.AddChild(row);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            var button = Button(choice.ToString(), () => AnswerSelected?.Invoke(choice), row); button.Name = $"Answer{choice}";
            button.CustomMinimumSize = new(180, 130); button.Disabled = rejected.Contains(choice);
            button.AddThemeFontSizeOverride("font_size", 40);
            if (!button.Disabled) first ??= button;
        }
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke()).Name = "LeaveChallenge"; first?.GrabFocus();
    }
    public void DeliveryHud(Expedition? delivery)
    {
        foreach (Node child in deliveryHud.GetChildren()) { deliveryHud.RemoveChild(child); child.QueueFree(); }
        directionPicture = null; deliveryHud.Visible = delivery != null;
        if (delivery == null) return;
        directionPicture = Picture(deliveryHud, "arrow");
        directionPicture!.CustomMinimumSize = new(80, 80);
        var label = new Label { Name = "NavigationText", Text = delivery.Gems || delivery.Toys
            ? delivery.CanDeliver ? $"Vrať se: {delivery.Recipient.DisplayName}\nVezeš {delivery.CollectionName}" : $"Najdi {delivery.CollectionName}\nSjeď z cesty a přijeď blíž"
            : delivery.Garden
            ? delivery.CanDeliver ? $"Vrať se k zahradníku Vítkovi\nVezeš {delivery.FlowerName}" : $"Najdi {delivery.FlowerName}\nSjeď z cesty a přijeď ke květině"
            : delivery.Farm
            ? delivery.CanDeliver ? $"Vrať se k farmáři Tondovi\nVezeš {delivery.FarmName}" : $"Najdi {delivery.FarmName}\nPřijeď blíž a chyť zvířátko"
            : delivery.Collection && !delivery.CanDeliver
            ? $"Najdi a přivez 3 jablka\nNasbíráno: {delivery.Collected} / {delivery.Amount}"
            : $"{(delivery.Collection ? "Vrať se" : "Doruč náklad")}: {delivery.Recipient.DisplayName}\n{CargoText(delivery.Cargo, delivery.Amount)}",
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new(530, 80) };
        deliveryHud.AddChild(label);
    }
    private static string CargoText(string cargo, int count) => cargo switch
    {
        "apple" => $"Jablka: {count}", "carrot" => $"Mrkve: {count}",
        "bread" => $"Bochníky chleba: {count}", "parcel" => $"Balíčky: {count}",
        "cake" => $"Dortíky: {count}", _ => $"Sklenice medu: {count}"
    };
    public void DeliveryDirection(Vector3 waypoint, Vehicle car)
    {
        if (directionPicture == null) return;
        var delta = waypoint - car.Position;
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return;
        var forward = -camera.GlobalBasis.Z; forward.Y = 0; forward = forward.Normalized();
        var right = camera.GlobalBasis.X; right.Y = 0; right = right.Normalized();
        directionPicture.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward)); directionPicture.QueueRedraw();
    }
    public void DeliveryHint(bool visible)
    {
        talkButton.Visible = visible && !modal.Visible;
        talkButton.Text = controller ? "A · Úkol" : "E · Úkol";
        hint.Text = "";
    }
}
