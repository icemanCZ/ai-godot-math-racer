using System;
using System.Linq;
using Godot;
using MathRacer.App;

namespace MathRacer.Ui;

/// <summary>Moving through the menu with the keyboard, the arrows and the sticks of a controller.</summary>
public partial class GameUi
{
    public override void _Input(InputEvent e)
    {
        bool? next =
            e is InputEventJoypadButton || e is InputEventJoypadMotion motion && Mathf.Abs(motion.AxisValue) > .25f
                ? true
            : e is InputEventKey or InputEventMouseButton ? false
            : null;
        if (next.HasValue && controller != next.Value)
        {
            controller = next.Value;
            UpdateControls();
        }
        if (e is InputEventJoypadMotion garageMotion && garageMotion.Axis == JoyAxis.RightY)
        {
            garageStick = Mathf.Abs(garageMotion.AxisValue) > .2f ? garageMotion.AxisValue : 0;
            if (garageCatalogue != null && modal.Visible)
                GetViewport().SetInputAsHandled();
        }
        if (
            modal.Visible
            && e is InputEventJoypadMotion stick
            && (stick.Axis == JoyAxis.LeftX || stick.Axis == JoyAxis.LeftY)
        )
        {
            if (stick.Axis == JoyAxis.LeftX)
                menuStick.X = stick.AxisValue;
            else
                menuStick.Y = stick.AxisValue;
            Side? direction =
                menuStick.Length() < .5f ? null
                : Mathf.Abs(menuStick.X) > Mathf.Abs(menuStick.Y) ? (menuStick.X < 0 ? Side.Left : Side.Right)
                : (menuStick.Y < 0 ? Side.Top : Side.Bottom);
            if (direction != menuDirection)
            {
                menuDirection = direction;
                menuRepeat = .4f;
                if (direction.HasValue)
                    MoveFocus(direction.Value);
            }
            GetViewport().SetInputAsHandled();
            return;
        }
        if (!modal.Visible)
            return;
        if (e.IsEcho() && e.IsAction("ui_accept"))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        Side? arrow =
            e.IsActionPressed("ui_left", allowEcho: true) ? Side.Left
            : e.IsActionPressed("ui_right", allowEcho: true) ? Side.Right
            : e.IsActionPressed("ui_up", allowEcho: true) ? Side.Top
            : e.IsActionPressed("ui_down", allowEcho: true) ? Side.Bottom
            : null;
        if (arrow.HasValue)
        {
            MoveFocus(arrow.Value);
            GetViewport().SetInputAsHandled();
        }
    }

    private void MoveFocus(Side direction)
    {
        var buttons = content
            .FindChildren("*", "Button", true, false)
            .OfType<Button>()
            .Where(button =>
                button.IsVisibleInTree() && !button.Disabled && button.FocusMode != Control.FocusModeEnum.None
            )
            .ToArray();
        if (buttons.Length == 0)
            return;
        var focused = GetViewport().GuiGetFocusOwner();
        int index = Array.IndexOf<Control>(buttons, focused);
        if (index < 0)
        {
            buttons[0].GrabFocus();
            return;
        }
        if (
            direction is Side.Top or Side.Bottom
            && focused!.FindValidFocusNeighbor(direction) is Button neighbor
            && buttons.Contains(neighbor)
        )
        {
            neighbor.GrabFocus();
            return;
        }
        // The button order covers the entire menu, including rows clipped by a scroll container.
        int step = direction is Side.Left or Side.Top ? -1 : 1;
        buttons[(index + step + buttons.Length) % buttons.Length].GrabFocus();
    }

    public override void _Process(double delta)
    {
        if (modal.Visible && garageCatalogue != null && garageStick != 0)
        {
            garageScroll = Mathf.Clamp(
                garageScroll + garageStick * (float)delta * 650,
                0,
                Mathf.Max(
                    0,
                    (float)garageCatalogue.GetVScrollBar().MaxValue - (float)garageCatalogue.GetVScrollBar().Page
                )
            );
            garageCatalogue.ScrollVertical = (int)garageScroll;
        }
        if (!modal.Visible || !menuDirection.HasValue)
            return;
        menuRepeat -= (float)delta;
        if (menuRepeat <= 0)
        {
            MoveFocus(menuDirection.Value);
            menuRepeat = .15f;
        }
    }

    private void UpdateControls() =>
        controls.Text = controller ? "RT / LT   ◀ ▶    A · ?    B · ✕    Menu · Ⅱ" : Texts.WasdEMezernikREscIi;
}
