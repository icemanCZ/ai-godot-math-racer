using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Tasks;
using MathRacer.Vehicles;

namespace MathRacer.App;

public partial class Game
{
    internal bool CelebrationShowing => celebrationTime > 0;
    internal readonly DrivingEvents Events = new();
    private Celebration? celebrationFx;
    private FollowCamera followCamera = null!;
    private double celebrationTime;
    private GameState? celebrationPanel;
    private Node3D? highlightedPart;

    private void ClearHighlight()
    {
        if (GodotObject.IsInstanceValid(highlightedPart))
            highlightedPart!.Scale = Vector3.One;
        highlightedPart = null;
    }

    private void ShowCelebrationView()
    {
        if (celebrationTime <= 0)
            return;
        celebrationPanel = State;
        ui.HideModal();
    }

    private void RestoreCelebrationPanel()
    {
        bool sameState = celebrationPanel == State;
        celebrationPanel = null;
        if (sameState)
            ShowPanel(State, returned: true);
    }

    private void CelebrateCompletion(Character owner, string message, bool unload = false, Node3D? building = null)
    {
        owner.Celebrate();
        Speak(message, owner);
        var position =
            building != null
                ? building.GlobalPosition + building.GlobalBasis.Z * 3.5f + Vector3.Up * 3
                : owner.GlobalPosition;
        CelebrateAt(position, message, unload ? Delivery?.DetachCargo() : null, owner.GlobalPosition + Vector3.Up);
        if (building != null)
        {
            followCamera.ShowMoment(building.GlobalPosition + Vector3.Up * 3, building.GlobalBasis.Z);
            int upgrade = world.Village.Level(((HouseResidentRole)owner.Role).House) - 1;
            highlightedPart = building.GetNode<Node3D>("Upgrades").GetChild<Node3D>(upgrade);
        }
        else if (unload)
        {
            var center = (owner.GlobalPosition + vehicle.GlobalPosition) / 2 + Vector3.Up * 1.5f;
            var outward = followCamera.GlobalPosition - center;
            outward.Y = 0;
            followCamera.ShowMoment(center, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        }
    }

    private void CelebrateAt(Vector3 position, string message, Node3D? cargo = null, Vector3 target = default)
    {
        ClearHighlight();
        celebrationFx?.QueueFree();
        celebrationFx = new Celebration();
        AddChild(celebrationFx);
        celebrationFx.Setup(position, cargo, target);
        celebrationTime = 3;
        ui.CelebrationMessage(message);
    }

    private void EndCelebration()
    {
        ClearHighlight();
        celebrationFx?.QueueFree();
        celebrationFx = null;
        ui.CelebrationMessage("");
        RestoreCelebrationPanel();
    }

    private bool InteractWithEvent()
    {
        if (Events.Active >= 0)
        {
            Events.Cancel();
            ui.EventHud("");
            return true;
        }
        int course = Events.Nearby(vehicle.Position);
        if (course < 0 || nearby != null || AtGarage)
            return false;
        Events.Begin(course);
        return true;
    }

    private void TickEvents(double delta)
    {
        bool running = State != GameState.Paused && State != GameState.Title;
        followCamera.MomentPaused = !running;
        if (running && celebrationTime > 0)
        {
            celebrationFx?.Tick(delta);
            celebrationTime -= delta;
            if (GodotObject.IsInstanceValid(highlightedPart))
                highlightedPart!.Scale = Vector3.One * (1 + .025f * Mathf.Sin((float)(3 - celebrationTime) * 8));
            if (celebrationTime <= 0)
                EndCelebration();
        }
        ui.SetEventVisible(State == GameState.Exploring);
        if (State != GameState.Exploring)
            return;
        Events.Advance(delta);
        int finished = Events.Tick(vehicle.Position);
        if (finished >= 0)
        {
            var (time, ghostTime, beat) = Events.Result;
            string result = DrivingEvents.FormatTime(time);
            CelebrateAt(
                vehicle.Position,
                beat
                    ? Texts.CourseBeat(DrivingEvents.Title(finished), result)
                    : Texts.CourseFinished(DrivingEvents.Title(finished), result, DrivingEvents.FormatTime(ghostTime))
            );
            AddCoins(CourseCoins + (beat ? GhostCoins : 0));
            SaveProgress();
        }
        if (Events.Active >= 0)
        {
            float distance = vehicle.Position.DistanceTo(Events.Points[Events.Active, Events.Next]);
            string hud = Texts.CourseHud(
                Events.Next + 1,
                DrivingEvents.GateCount,
                distance,
                DrivingEvents.FormatTime(Events.Elapsed)
            );
            if (!float.IsNaN(Events.Gap))
                hud +=
                    Events.Gap <= 0
                        ? Texts.AheadOfGhost(DrivingEvents.FormatTime(-Events.Gap))
                        : Texts.BehindGhost(DrivingEvents.FormatTime(Events.Gap));
            ui.EventHud(hud);
            ui.EventHint(Texts.UkoncitJizdu);
        }
        else
        {
            int course = Events.Nearby(vehicle.Position);
            ui.EventHud(
                course >= 0
                    ? Texts.CourseGhostTime(
                        DrivingEvents.Title(course),
                        DrivingEvents.FormatTime(Events.GhostTime(course))
                    )
                    : ""
            );
            if (course >= 0 && nearby == null && !AtGarage)
                ui.EventHint(Texts.ZahajitJizdu);
        }
    }
}
