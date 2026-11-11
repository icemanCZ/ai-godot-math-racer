using Godot;

namespace MathRacer;

public partial class TaskMarker : Label3D
{
    internal Vector3 AnchorPosition { get; set; }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || GetViewport().GetCamera3D() is not { } camera ||
            GetParentNode3D() is not { } parent) return;

        var anchor = parent.ToGlobal(AnchorPosition);
        var separation = anchor - camera.GlobalPosition;
        float distance = new Vector2(separation.X, separation.Z).Length();
        float lift = Mathf.Clamp((distance - 20f) * .035f, 0f, 8f);
        GlobalPosition = anchor + Vector3.Up * lift;
    }
}
