using Godot;
using MathRacer.Valley;

namespace MathRacer.Vehicles;

public partial class FollowCamera : Camera3D
{
    public Vehicle Target = null!;
    public Terrain Ground = null!;
    private Vector3 focus;
    private Vector3 lastTargetPosition;
    private float moment;
    private Vector3 momentFocus,
        momentSide;

    /// <summary>Set by the game while it is paused: a moment and a look from the tower wait, and the mouse is free.</summary>
    internal bool MomentPaused;

    private float panorama,
        panoramaTotal;
    private Vector3 panoramaCenter;
    private float panoramaYaw,
        panoramaElevation;
    private bool panoramaSteered;
    private uint panoramaHidden;

    /// <summary>
    /// The camera stands at <paramref name="center"/> and turns once round over <paramref name="seconds"/>; a positive
    /// <paramref name="pitch"/> (the height of the line of sight over its length) lifts the view towards the sky. The
    /// render layers in <paramref name="hidden"/> (a roof right above the camera) are not drawn until the turn ends.
    /// Dragging the mouse with either button stops the turn and looks where the player wants; from then on the view
    /// has no end of its own and lasts until <see cref="EndPanorama"/>.
    /// </summary>
    internal void ShowPanorama(Vector3 center, float seconds, float startAngle, float pitch = -.16f, uint hidden = 0)
    {
        StopDragging();
        panorama = panoramaTotal = seconds;
        panoramaCenter = center;
        panoramaYaw = startAngle;
        panoramaElevation = Mathf.Atan(pitch);
        panoramaSteered = false;
        CullMask |= panoramaHidden;
        panoramaHidden = hidden;
        CullMask &= ~hidden;
    }

    internal bool PanoramaActive => panorama > 0;

    /// <summary>Whether the player has taken the look over with the mouse, so that it does not end by itself.</summary>
    internal bool PanoramaSteered => panoramaSteered;

    /// <summary>Where the camera on the platform looks: the angle round (0 along +Z) and the angle over the horizon.</summary>
    internal Vector2 PanoramaDirection => new(panoramaYaw, panoramaElevation);

    /// <summary>Ends the look from the tower, if one is on, and goes back behind the car.</summary>
    internal void EndPanorama()
    {
        if (panoramaHidden == 0 && panorama <= 0)
            return;
        panorama = 0;
        CullMask |= panoramaHidden;
        panoramaHidden = 0;
        Snap();
    }

    internal void ShowMoment(Vector3 target, Vector3 outward)
    {
        moment = 3;
        momentFocus = target;
        momentSide = outward;
    }

    private const float ReturnDelay = 3f;
    private const float Sensitivity = .006f;
    private static readonly float DefaultPitch = Mathf.Atan2(4.3f, 10.5f);
    private static readonly float OrbitDistance = new Vector2(10.5f, 4.3f).Length();
    private float yaw;
    private float pitch = DefaultPitch;
    private float idleTime;
    private bool dragging;
    private MouseButton dragButton;
    private Input.MouseModeEnum previousMouseMode;

    /// <summary>The car camera turns while the car can drive; a look from the tower while the game is not paused.</summary>
    private bool Steerable => panorama > 0 ? !MomentPaused : Target.Active;

    public override void _UnhandledInput(InputEvent e)
    {
        // Round the car the right button turns the camera (the left one is for the game); on the tower either does.
        if (
            Steerable
            && e is InputEventMouseButton { Pressed: true } press
            && (press.ButtonIndex == MouseButton.Right || (panorama > 0 && press.ButtonIndex == MouseButton.Left))
        )
        {
            if (!dragging)
            {
                previousMouseMode = Input.MouseMode;
                dragButton = press.ButtonIndex;
            }
            dragging = true;
            idleTime = 0;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent e)
    {
        // Release must also work over UI controls, before they consume the event.
        if (e is InputEventMouseButton { Pressed: false } release && release.ButtonIndex == dragButton)
            StopDragging();
        if (!Steerable)
        {
            StopDragging();
            return;
        }
        if (dragging && e is InputEventMouseMotion motion && motion.Relative != Vector2.Zero)
        {
            if (panorama > 0)
                LookRound(motion.Relative);
            else
            {
                yaw = Mathf.Wrap(yaw - motion.Relative.X * Sensitivity, -Mathf.Pi, Mathf.Pi);
                pitch = Mathf.Clamp(pitch + motion.Relative.Y * Sensitivity, Mathf.DegToRad(10), Mathf.DegToRad(70));
            }
            idleTime = 0;
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The player turns the view on the tower: the turn by itself stops, and so does the time of the look.</summary>
    private void LookRound(Vector2 relative)
    {
        panoramaSteered = true;
        panoramaYaw = Mathf.Wrap(panoramaYaw - relative.X * Sensitivity, -Mathf.Pi, Mathf.Pi);
        panoramaElevation = Mathf.Clamp(
            panoramaElevation - relative.Y * Sensitivity,
            Mathf.DegToRad(-40),
            Mathf.DegToRad(65)
        );
    }

    private void StopDragging()
    {
        if (!dragging)
            return;
        dragging = false;
        Input.MouseMode = previousMouseMode;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationExitTree)
            StopDragging();
    }

    public void Snap()
    {
        StopDragging();
        moment = 0;
        yaw = 0;
        pitch = DefaultPitch;
        idleTime = 0;
        focus = Target.Position + Vector3.Up * 1.5f;
        lastTargetPosition = Target.Position;
        Position = Target.Position + Target.Transform.Basis.Z * 10.5f + Vector3.Up * 5.7f;
        LookAt(focus);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (panorama > 0)
        {
            // The pause menu needs the mouse, and the look waits until the game goes on.
            if (MomentPaused)
            {
                StopDragging();
                return;
            }
            if (!panoramaSteered)
            {
                panorama = Mathf.Max(0, panorama - dt);
                panoramaYaw += dt / panoramaTotal * Mathf.Tau;
            }
            Position = panoramaCenter;
            float level = Mathf.Cos(panoramaElevation);
            LookAt(
                panoramaCenter
                    + new Vector3(
                        Mathf.Sin(panoramaYaw) * level,
                        Mathf.Sin(panoramaElevation),
                        Mathf.Cos(panoramaYaw) * level
                    ) * 60
            );
            if (panorama <= 0)
                EndPanorama();
            return;
        }
        if (moment > 0)
        {
            StopDragging();
            if (MomentPaused)
                return;
            moment -= dt;
            var view = momentFocus + momentSide * 17 + Vector3.Up * 8;
            view.Y = Mathf.Max(view.Y, Ground.Height(view.X, view.Z) + 3);
            Position = Position.Lerp(view, 1 - Mathf.Exp(-dt * 4));
            focus = focus.Lerp(momentFocus, 1 - Mathf.Exp(-dt * 4));
            LookAt(focus);
            if (Target.Active)
                moment = 0;
            lastTargetPosition = Target.Position;
            return;
        }
        if (!Target.Active)
            StopDragging();
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
        var desired =
            Target.Position
            + orbit * (Mathf.Cos(pitch) * OrbitDistance)
            + Vector3.Up * (1.4f + Mathf.Sin(pitch) * OrbitDistance);
        desired.Y = Mathf.Max(desired.Y, Ground.Height(desired.X, desired.Z) + 3);
        // Follow translation immediately, but smooth the orbit in polar coordinates.
        // A straight lerp across opposite viewpoints cuts through the car and looks
        // straight down, especially after a sharp turn or returning from inspection.
        var movement = Target.Position - lastTargetPosition;
        var offset = Position - lastTargetPosition;
        lastTargetPosition = Target.Position;
        var desiredOffset = desired - Target.Position;
        float angle = Mathf.LerpAngle(
            Mathf.Atan2(offset.X, offset.Z),
            Mathf.Atan2(desiredOffset.X, desiredOffset.Z),
            blend
        );
        float radius = Mathf.Lerp(
            new Vector2(offset.X, offset.Z).Length(),
            new Vector2(desiredOffset.X, desiredOffset.Z).Length(),
            blend
        );
        radius = Mathf.Max(radius, 3);
        var next =
            Target.Position
            + new Vector3(
                Mathf.Sin(angle) * radius,
                Mathf.Lerp(offset.Y, desiredOffset.Y, blend),
                Mathf.Cos(angle) * radius
            );
        next.Y = Mathf.Max(next.Y, Ground.Height(next.X, next.Z) + 3);
        Position = next;
        focus += movement;
        // Center the car when looking around so front and roof accessories stay in view.
        float inspection = Mathf.Clamp(Mathf.Abs(yaw) + Mathf.Abs(pitch - DefaultPitch) * 2, 0, 1);
        focus = focus.Lerp(Target.Position + Vector3.Up * 1.4f - behind * (3 * (1 - inspection)), blend);
        LookAt(focus);
    }
}
