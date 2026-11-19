using Godot;
using MathRacer.Garage;
using MathRacer.Settlement;

namespace MathRacer.Ui;

/// <summary>A decoration of the town as it will stand on the square, drawn by the same code, in a softly lit studio.</summary>
public partial class DecorationPreview : SubViewportContainer
{
    internal TownDecoration? Decoration { get; set; }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(205, 160);
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
    }

    public override void _Process(double delta)
    {
        // Build only once the panel survives a frame, avoiding unused render scenes.
        if (!IsVisibleInTree() || Decoration == null)
            return;
        SetProcess(false);
        var viewport = new SubViewport
        {
            Size = new Vector2I(410, 320),
            OwnWorld3D = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        AddChild(viewport);
        var studio = CarLookPreview.CreateStudio(viewport);
        var piece = new Node3D();
        studio.AddChild(piece);
        Decoration.Build(piece, false);
        Aabb? bounds = null;
        foreach (var node in piece.FindChildren("*", "MeshInstance3D", true, false))
        {
            var mesh = (MeshInstance3D)node;
            var box = mesh.GlobalTransform * mesh.GetAabb();
            bounds = bounds is { } known ? known.Merge(box) : box;
        }
        var area = bounds ?? new Aabb(Vector3.Zero, Vector3.One);
        var center = area.GetCenter();
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = Mathf.Max(1.6f, area.Size.Length() * .8f),
            Position = center + new Vector3(.8f, .5f, 1).Normalized() * 14,
            Current = true,
        };
        studio.AddChild(camera);
        camera.LookAt(center);
    }
}
