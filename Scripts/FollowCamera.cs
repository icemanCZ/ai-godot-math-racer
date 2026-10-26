using Godot;

namespace MathRacer;

public partial class FollowCamera : Camera3D
{
    public Vehicle Target = null!;
    public Terrain Ground = null!;
    private Vector3 focus;
    private const float ReturnDelay = 3f;
    private const float Sensitivity = .006f;
    private static readonly float DefaultPitch = Mathf.Atan2(4.3f, 10.5f);
    private static readonly float OrbitDistance = new Vector2(10.5f, 4.3f).Length();
    private float yaw;
    private float pitch = DefaultPitch;
    private float idleTime;
    private bool dragging;
    private Input.MouseModeEnum previousMouseMode;

    public override void _UnhandledInput(InputEvent e)
    {
        if (Target.Active && e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            if (!dragging) previousMouseMode = Input.MouseMode;
            dragging = true;
            idleTime = 0;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent e)
    {
        // Release must also work over UI controls, before they consume the event.
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false }) StopDragging();
        if (!Target.Active) { StopDragging(); return; }
        if (dragging && e is InputEventMouseMotion motion && motion.Relative != Vector2.Zero)
        {
            yaw = Mathf.Wrap(yaw - motion.Relative.X * Sensitivity, -Mathf.Pi, Mathf.Pi);
            pitch = Mathf.Clamp(pitch + motion.Relative.Y * Sensitivity, Mathf.DegToRad(10), Mathf.DegToRad(70));
            idleTime = 0;
            GetViewport().SetInputAsHandled();
        }
    }

    private void StopDragging()
    {
        if (!dragging) return;
        dragging = false;
        Input.MouseMode = previousMouseMode;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationExitTree) StopDragging();
    }

    public void Snap()
    {
        StopDragging();
        yaw = 0; pitch = DefaultPitch; idleTime = 0;
        focus = Target.Position + Vector3.Up * 1.5f;
        Position = Target.Position + Target.Transform.Basis.Z * 10.5f + Vector3.Up * 5.7f;
        LookAt(focus);
    }
    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (!Target.Active) StopDragging();
        idleTime += dt;
        var stick = Input.GetVector("camera_left", "camera_right", "camera_up", "camera_down");
        if (Target.Active && stick != Vector2.Zero)
        {
            yaw = Mathf.Wrap(yaw - stick.X * 2.2f * dt, -Mathf.Pi, Mathf.Pi);
            pitch = Mathf.Clamp(pitch + stick.Y * 1.4f * dt, Mathf.DegToRad(10), Mathf.DegToRad(70));
            idleTime = 0;
        }
        if (idleTime > ReturnDelay)
        {
            float returnBlend = 1 - Mathf.Exp(-dt * 2f);
            yaw = Mathf.LerpAngle(yaw, 0, returnBlend);
            pitch = Mathf.Lerp(pitch, DefaultPitch, returnBlend);
        }
        float blend = 1 - Mathf.Exp(-dt * 3.5f);
        var behind = Target.Transform.Basis.Z;
        var orbit = behind.Rotated(Vector3.Up, yaw);
        var desired = Target.Position + orbit * (Mathf.Cos(pitch) * OrbitDistance)
            + Vector3.Up * (1.4f + Mathf.Sin(pitch) * OrbitDistance);
        desired.Y = Mathf.Max(desired.Y, Ground.Height(desired.X, desired.Z) + 3);
        Position = Position.Lerp(desired, blend);
        // Center the car when looking around so front and roof accessories stay in view.
        float inspection = Mathf.Clamp(Mathf.Abs(yaw) + Mathf.Abs(pitch - DefaultPitch) * 2, 0, 1);
        focus = focus.Lerp(Target.Position + Vector3.Up * 1.4f - behind * (3 * (1 - inspection)), blend);
        LookAt(focus);
    }
}

