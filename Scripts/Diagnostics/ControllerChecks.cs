using Godot;
using System;
using System.Threading.Tasks;

namespace MathRacer;

// Feed real joypad events through Godot's input and GUI routing, without hardware.
public partial class ControllerChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    public ControllerChecks(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    private async Task Frames(int count = 2)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private async Task Button(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = true });
        Input.FlushBufferedEvents(); await Frames();
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents(); await Frames();
    }
    private static void Axis(JoyAxis axis, float value)
    {
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = value });
        Input.FlushBufferedEvents();
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        GD.Print("PASS: " + label);
    }
    public override async void _Ready()
    {
        try
        {
            await Frames();
            Check(game.State == GameState.Title, "controller starts at title");
            await Button(JoyButton.A);
            Check(game.State == GameState.Exploring, "A starts game through focused UI");
            Axis(JoyAxis.LeftX, .1f); Axis(JoyAxis.TriggerRight, .03f);
            await Frames();
            Check(Input.GetAxis("left", "right") == 0 && Input.GetActionStrength("accelerate") == 0, "stick and trigger drift stays in deadzone");
            Axis(JoyAxis.LeftX, 0); Axis(JoyAxis.TriggerRight, .5f);
            float half = Input.GetActionStrength("accelerate");
            Axis(JoyAxis.TriggerRight, 1);
            Check(half > 0 && half < Input.GetActionStrength("accelerate"), "triggers retain analog throttle");
            await Frames(60);
            Check(car.Speed > 3, "RT drives forward");
            Axis(JoyAxis.LeftX, .7f); await Frames(20);
            Check(Mathf.Abs(car.Heading) > .1f, "left stick steers car");
            Axis(JoyAxis.LeftX, 0);
            Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = JoyButton.X, Pressed = true });
            Input.FlushBufferedEvents(); await Frames(90);
            Check(Mathf.Abs(car.Speed) < .1f, "X brakes despite held RT");
            Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = JoyButton.X, Pressed = false });
            Axis(JoyAxis.TriggerRight, 0); Axis(JoyAxis.TriggerLeft, 1); await Frames(60);
            Check(car.Speed < -3, "LT reverses");
            Axis(JoyAxis.TriggerLeft, 0); await Button(JoyButton.Y);
            Check(car.Position.DistanceTo(world.Ground.At(0, 8)) < 1, "Y resets vehicle");
            Axis(JoyAxis.LeftY, -.1f); await Frames();
            Check(car.Speed == 0, "left stick throttle ignores drift");
            Axis(JoyAxis.LeftY, -.5f);
            float stickHalf = Input.GetActionStrength("stick_forward");
            Axis(JoyAxis.LeftY, -1); await Frames(60);
            Check(stickHalf > 0 && stickHalf < Input.GetActionStrength("stick_forward") && car.Speed > 3,
                "pushing left stick forward provides analog acceleration");
            Axis(JoyAxis.LeftY, 1); await Frames(120);
            Check(car.Speed < -3, "pulling left stick back reverses");
            Axis(JoyAxis.LeftY, 0); await Button(JoyButton.Y);
            var camera = (FollowCamera)GetViewport().GetCamera3D(); camera.Snap();
            Axis(JoyAxis.RightX, 1); await Frames(60); Axis(JoyAxis.RightX, 0);
            Check(Mathf.Abs(camera.Position.X - car.Position.X) > 3, "right stick orbits camera");
            await Button(JoyButton.Start);
            Check(game.State == GameState.Paused && !car.Active, "Menu pauses");
            Check(GetViewport().GuiGetFocusOwner() is Godot.Button { Text: "Pokračovat" }, "pause focuses resume instead of quit");
            await Button(JoyButton.B);
            Check(game.State == GameState.Exploring, "B resumes pause");
            car.SetPose(world.Animals[0].Position + Vector3.Right * 2, 0); await Frames(5);
            await Button(JoyButton.A);
            Check(game.State == GameState.Challenge, "A opens animal task without also answering");
            var focus = GetViewport().GuiGetFocusOwner();
            await Button(JoyButton.DpadRight);
            Check(GetViewport().GuiGetFocusOwner() != focus, "D-pad changes answer selection");
            await Frames(40); // Allow the GUI's shared navigation repeat delay to expire.
            Axis(JoyAxis.LeftX, -1); await Frames(); Axis(JoyAxis.LeftX, 0); await Frames();
            Check(GetViewport().GuiGetFocusOwner() == focus, "left stick navigates menu");
            int answer = game.CurrentChallenge!.Answer;
            ((Control)game.FindChild($"Answer{answer}", true, false)).GrabFocus();
            await Button(JoyButton.A);
            Check(game.State == GameState.Reward, "A submits correct answer through UI");
            focus = GetViewport().GuiGetFocusOwner();
            await Button(JoyButton.Start); await Button(JoyButton.Start);
            Check(game.State == GameState.Reward, "Menu restores reward screen");
            await Button(JoyButton.DpadRight);
            await Button(JoyButton.A);
            Check(game.State == GameState.Exploring, "controller selects reward and returns to driving");
            game.OpenChallenge(world.Animals[1]); await Frames(); await Button(JoyButton.B);
            Check(game.State == GameState.Exploring, "B leaves task");
            GD.Print("CONTROLLER_CHECKS_PASSED"); GetTree().Quit();
        }
        catch (Exception error) { GD.PushError("CONTROLLER_CHECKS_FAILED: " + error); GetTree().Quit(1); }
    }
}
