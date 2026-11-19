using Godot;
using MathRacer.Garage;
using MathRacer.Settlement;

namespace MathRacer.Ui;

/// <summary>A lamp of the glass lamp maker, lit, drawn by the same code that stands it on the ground.</summary>
public partial class LampPreview : SubViewportContainer
{
    internal GlassLamp? Lamp { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(250, 220);
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
    }

    public override void _Process(double delta)
    {
        // Build only once the panel survives a frame, avoiding unused render scenes.
        if (!IsVisibleInTree() || Lamp == null)
            return;
        SetProcess(false);
        var viewport = new SubViewport
        {
            Size = new Vector2I(500, 440),
            OwnWorld3D = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(viewport);
        var studio = CarLookPreview.CreateStudio(viewport);
        var piece = new Node3D();
        studio.AddChild(piece);
        Lamp.Build(piece).Visible = true;
        Aabb? bounds = null;
        foreach (var node in piece.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mesh = (MeshInstance3D)node;
            if (mesh.Name == "Halo")
                continue;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } known ? known.Merge(box) : box;
        }
        var area = bounds ?? new Aabb(Vector3.Zero, Vector3.One);
        var center = area.GetCenter();
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = Mathf.Max(1.6f, area.Size.Length() * .8f),
            Position = center + new Vector3(.6f, .3f, 1).Normalized() * 14,
            Current = true,
        };
        studio.AddChild(camera);
        camera.LookAt(center);
    }
}
