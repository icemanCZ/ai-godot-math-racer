using Godot;

namespace MathRacer;

public partial class GameUi
{
    private static ImageTexture? activePetBadge;

    private static void AddPetPreview(Button button, Node3D model, Material? material, bool active)
    {
        var viewport = new SubViewport
        {
            Size = new(224, 224), OwnWorld3D = true, TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once, Msaa3D = Viewport.Msaa.Msaa4X
        };
        button.AddChild(viewport);
        var studio = CarLookPreview.CreateStudio(viewport);
        studio.AddChild(model);
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal, Size = 1.95f,
            Position = new(2.4f, 1.9f, 4), Current = true
        };
        studio.AddChild(camera);
        camera.LookAt(Vector3.Up * .72f);

        var portrait = new TextureRect
        {
            Name = "PetPortrait", Texture = viewport.GetTexture(), Material = material,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        button.AddChild(portrait);
        portrait.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        if (!active) return;

        if (activePetBadge == null)
        {
            var mark = new Image();
            mark.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'>" +
                "<circle cx='12' cy='12' r='11' fill='#285f51' stroke='#fff5df' stroke-width='2'/>" +
                "<path d='M7 12l3 3 7-7' fill='none' stroke='#fff5df' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'/></svg>", 2);
            activePetBadge = ImageTexture.CreateFromImage(mark);
        }
        var badge = new TextureRect
        {
            Name = "PetActive", Texture = activePetBadge, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        button.AddChild(badge);
        badge.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        badge.OffsetLeft = -32; badge.OffsetRight = -8;
        badge.OffsetTop = 8; badge.OffsetBottom = 32;
    }
}
