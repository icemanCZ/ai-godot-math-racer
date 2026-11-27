using Godot;
using MathRacer.Characters;
using MathRacer.Garage;

namespace MathRacer.Ui;

public partial class GameUi
{
    private static ImageTexture? activePetBadge;

    private static ShaderMaterial? petGray;

    /// <summary>Draws a pet in grey, for a pet not earned yet.</summary>
    private static ShaderMaterial PetGray =>
        petGray ??= new ShaderMaterial
        {
            Shader = new Shader
            {
                Code =
                    "shader_type canvas_item; void fragment() { vec4 c = texture(TEXTURE, UV); float g = dot(c.rgb, vec3(0.299, 0.587, 0.114)); COLOR = vec4(vec3(g), c.a * 0.75); }",
            },
        };

    /// <summary>
    /// The pet that a panel's task brings, for a child who cannot read: what is counted (<paramref name="unit"/>) and how
    /// many of how many, an arrow, and the pet itself, grey until it is earned and with a check mark once it is.
    /// </summary>
    private void PetGoal(CollectorPet? pet, Control unit)
    {
        if (pet == null)
        {
            unit.QueueFree();
            return;
        }
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "PetGoal" };
        row.AddThemeConstantOverride("separation", 10);
        row.TooltipText = pet.RewardHint;
        content.AddChild(row);
        unit.CustomMinimumSize = new(56, 56);
        unit.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        unit.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(unit);
        var count = new Label
        {
            Name = "PetGoalCount",
            Text = $"{Mathf.Min(pet.Count, pet.Goal)} / {pet.Goal}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        count.AddThemeFontSizeOverride("font_size", 26);
        row.AddChild(count);
        row.AddChild(
            new DeliveryPicture
            {
                Icon = "arrow",
                Direction = Mathf.Pi / 2,
                CustomMinimumSize = new(44, 40),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        var portrait = new Control
        {
            Name = "PetGoalPortrait",
            CustomMinimumSize = new(120, 112),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(portrait);
        AddPetPreview(portrait, pet.CreatePreviewModel(), pet.IsOwned ? null : PetGray, pet.IsOwned, 1.45f);
    }

    /// <summary>The pet goal counted in pictures of <paramref name="icon"/>.</summary>
    private void PetGoal(CollectorPet? pet, string icon) => PetGoal(pet, new DeliveryPicture { Icon = icon });

    private static void AddPetPreview(
        Control button,
        Node3D model,
        Material? material,
        bool active,
        float frame = 1.95f
    )
    {
        var viewport = new SubViewport
        {
            Size = new(224, 224),
            OwnWorld3D = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        button.AddChild(viewport);
        var studio = CarLookPreview.CreateStudio(viewport);
        studio.AddChild(model);
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = frame,
            Position = new(2.4f, 1.9f, 4),
            Current = true,
        };
        studio.AddChild(camera);
        camera.LookAt(Vector3.Up * .72f);

        var portrait = new TextureRect
        {
            Name = "PetPortrait",
            Texture = viewport.GetTexture(),
            Material = material,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        button.AddChild(portrait);
        portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (!active)
            return;

        if (activePetBadge == null)
        {
            var mark = new Image();
            mark.LoadSvgFromString(
                "<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'>"
                    + "<circle cx='12' cy='12' r='11' fill='#285f51' stroke='#fff5df' stroke-width='2'/>"
                    + "<path d='M7 12l3 3 7-7' fill='none' stroke='#fff5df' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/></svg>",
                2
            );
            activePetBadge = ImageTexture.CreateFromImage(mark);
        }
        var badge = new TextureRect
        {
            Name = "PetActive",
            Texture = activePetBadge,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        button.AddChild(badge);
        badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        badge.OffsetLeft = -32;
        badge.OffsetRight = -8;
        badge.OffsetTop = 8;
        badge.OffsetBottom = 32;
    }
}
