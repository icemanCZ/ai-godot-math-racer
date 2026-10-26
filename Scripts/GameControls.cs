using Godot;

namespace MathRacer;

internal static class GameControls
{
    public static void Setup()
    {
        Keys("accelerate", Key.W, Key.Up); Keys("reverse", Key.S, Key.Down);
        Keys("left", Key.A, Key.Left); Keys("right", Key.D, Key.Right);
        Keys("brake", Key.Space); Keys("reset", Key.R); Keys("talk", Key.E);
        Keys("pause", Key.Escape); Keys("back");
        Axis("accelerate", JoyAxis.TriggerRight, 1, .08f);
        Axis("reverse", JoyAxis.TriggerLeft, 1, .08f);
        Axis("stick_forward", JoyAxis.LeftY, -1); Axis("stick_reverse", JoyAxis.LeftY, 1);
        Axis("left", JoyAxis.LeftX, -1); Axis("right", JoyAxis.LeftX, 1);
        Button("brake", JoyButton.X); Button("reset", JoyButton.Y);
        Button("talk", JoyButton.A); Button("pause", JoyButton.Start); Button("back", JoyButton.B);
        Axis("camera_left", JoyAxis.RightX, -1); Axis("camera_right", JoyAxis.RightX, 1);
        Axis("camera_up", JoyAxis.RightY, -1); Axis("camera_down", JoyAxis.RightY, 1);
        // Keep keyboard navigation, but make the Xbox menu mapping explicit.
        foreach (var action in new[] { "ui_accept", "ui_up", "ui_down", "ui_left", "ui_right" })
            foreach (var e in InputMap.ActionGetEvents(action))
                if (e is InputEventJoypadButton or InputEventJoypadMotion) InputMap.ActionEraseEvent(action, e);
        Button("ui_accept", JoyButton.A);
        Button("ui_up", JoyButton.DpadUp); Button("ui_down", JoyButton.DpadDown);
        Button("ui_left", JoyButton.DpadLeft); Button("ui_right", JoyButton.DpadRight);
        Axis("ui_up", JoyAxis.LeftY, -1, .5f); Axis("ui_down", JoyAxis.LeftY, 1, .5f);
        Axis("ui_left", JoyAxis.LeftX, -1, .5f); Axis("ui_right", JoyAxis.LeftX, 1, .5f);
    }

    private static void Keys(string action, params Key[] keys)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action, .2f);
        foreach (var key in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
    private static void Button(string action, JoyButton button)
        => InputMap.ActionAddEvent(action, new InputEventJoypadButton { Device = -1, ButtonIndex = button });
    private static void Axis(string action, JoyAxis axis, float value, float deadzone = .2f)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action, deadzone);
        InputMap.ActionSetDeadzone(action, deadzone);
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Device = -1, Axis = axis, AxisValue = value });
    }
}
