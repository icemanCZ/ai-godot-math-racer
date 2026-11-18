using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Quizzes;

namespace MathRacer.Ui;

/// <summary>The garage: the car, the kinds of questions, the pets and the golem.</summary>
public partial class GameUi
{
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
                Send(new UiIntent.CloseInteraction());
            },
            "LeaveGarage",
            header
        );
        leave.CustomMinimumSize = new(120, 60);
        leave.AccessibilityName = Texts.VyjetZGaraze;
        var title = new Label { Text = Texts.Garaz, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 32);
        header.AddChild(title);
        header.AddChild(
            new Label
            {
                Text =
                    $"{owned.Count} / {CarLook.Catalogue.Count + CarBody.All.Count + CarAccessory.Catalogue.Count + CarTrail.All.Count}   ·",
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
        catalogue.AddChild(new Label { Text = Texts.TypyUkoluAlesponJedenZapnuty });
        var quizRow = new HBoxContainer();
        quizRow.AddThemeConstantOverride("separation", 8);
        catalogue.AddChild(quizRow);
        foreach (
            var (type, label, icon) in new[]
            {
                (QuizType.Letters, Texts.Pismenka, "quiz-letters"),
                (QuizType.Syllables, "Slabiky", "quiz-syllables"),
                (QuizType.Reading, Texts.Cteni, "quiz-reading"),
                (QuizType.Math, Texts.Priklady, "quiz-math"),
            }
        )
        {
            bool active = quizzes.HasFlag(type);
            QuizButton(
                quizRow,
                $"GarageQuiz_{type}",
                label,
                active,
                () => Send(new UiIntent.ToggleQuizType(type)),
                icon,
                required: quizzes == type
            );
        }
        catalogue.AddChild(new Label { Text = Texts.UrovenPrikladu });
        var levelRow = new HBoxContainer();
        levelRow.AddThemeConstantOverride("separation", 8);
        catalogue.AddChild(levelRow);
        QuizButton(levelRow, "GarageMathLevel1", Texts.N1DveCisla, !threeTermMath, () =>
            Send(new UiIntent.MathLevel(false))
        );
        QuizButton(levelRow, "GarageMathLevel2", Texts.N2TriCisla, threeTermMath, () =>
            Send(new UiIntent.MathLevel(true))
        );

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
                string status = Texts.Switch(label, active);
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
                button.TooltipText = Texts.AlesponJedenTypUkoluMusiZustat;
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
                bool unlocked = owned.Contains(reward) || (reward.IsTrail && reward.Id == CarTrail.Plain.Id);
                bool active =
                    reward.IsTrail ? appearance.CurrentTrailId == reward.Id
                    : reward.IsModel ? appearance.CurrentModelId == reward.Id
                    : reward.IsAccessory ? appearance.HasAccessory(reward.Id)
                    : appearance.CurrentLookId == reward.Id;
                var card = new VBoxContainer();
                grid.AddChild(card);
                Control sample;
                if (reward.IsTrail)
                    sample = new DeliveryPicture { Icon = "trail-" + reward.Id, CustomMinimumSize = new(205, 160) };
                else
                {
                    var preview = new CarLookPreview
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
                        preview.Material = gray;
                    sample = preview;
                }
                card.AddChild(sample);
                int? price = unlocked ? null : reward.Price;
                bool affordable = price <= coins;
                var button = Button(
                    (active ? "✓ " : "") + name,
                    () =>
                    {
                        garageScroll = scroll.ScrollVertical;
                        garageFocus = reward.ButtonName;
                        Send(new UiIntent.GarageReward(reward));
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
                    button.AccessibilityName = Texts.BuyNamed(name, cost);
                    button.TooltipText = affordable ? Texts.BuyFor(cost) : Texts.PriceHint(cost);
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
                        button.TooltipText = Texts.TutoOdmenuZiskasZaZvlastniUkol;
                }
                else
                    first ??= button;
                if (!button.Disabled && garageFocus == reward.ButtonName)
                    selectedFocus = button;
            }
        }
        catalogue.AddChild(new Label { Text = Texts.Mazlicci });
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
                : Texts.ZatimNeziskan;
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
                pet.GarageHint,
                () => Send(new UiIntent.ToggleCollectorPet(pet.Kind))
            );
        var golemPreview = GolemModel.Create();
        golemPreview.Scale = Vector3.One * .64f;
        golemPreview.RotateY(Mathf.Pi);
        PetButton("Golem", Texts.MalyGolem, golemPreview, golemOwned, golemActive, Texts.CernokneznikBaltazar, () =>
            Send(new UiIntent.ToggleGolem())
        );
        if (trailer is { } towing)
            GarageTrailerRow(catalogue, scroll, towing, ref selectedFocus);
        Category("Laky", System.Linq.Enumerable.Select(CarLook.Catalogue, l => (new CarReward(l.Id, false), l.Name)));
        Category("Modely", System.Linq.Enumerable.Select(CarBody.All, b => (new CarReward(b.Id, false, true), b.Name)));
        Category(
            Texts.Doplnky,
            System.Linq.Enumerable.Select(CarAccessory.Catalogue, a => (new CarReward(a.Id, true), a.Name))
        );
        Category(
            Texts.StopyKol,
            System.Linq.Enumerable.Select(CarTrail.Catalogue, t => (new CarReward(t.Id, false, false, true), t.Name))
        );
        (selectedFocus ?? firstQuiz ?? firstPet ?? first)?.GrabFocus();
        scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, (int)garageScroll);
    }
}
