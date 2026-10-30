using Godot;

namespace MathRacer;

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
        if (GodotObject.IsInstanceValid(highlightedPart)) highlightedPart!.Scale = Vector3.One;
        highlightedPart = null;
    }
    private void ShowCelebrationView()
    {
        if (celebrationTime <= 0) return;
        celebrationPanel = State; ui.HideModal();
    }
    private void RestoreCelebrationPanel()
    {
        if (celebrationPanel != State) { celebrationPanel = null; return; }
        celebrationPanel = null;
        if (State == GameState.Reward) ShowRewards();
        else if (State == GameState.HouseReward && nearby != null) ui.HouseReward(nearby, world.Village.Level(nearby.HouseIndex));
        else if (State == GameState.FarmStatus) ui.FarmStatus(world.Village, Delivery, true);
        else if (State == GameState.GardenStatus) ui.GardenStatus(world.Village, Delivery, true);
        else if (State == GameState.CollectionStatus && nearby != null) ui.CollectionStatus(world.Village, nearby, Delivery, true);
    }
    private void CelebrateCompletion(Animal owner, string message, bool unload = false, Node3D? building = null)
    {
        owner.Celebrate();
        CelebrateAt(building != null ? building.GlobalPosition + building.GlobalBasis.Z * 3.5f + Vector3.Up * 3 : owner.GlobalPosition, message,
            unload ? Delivery?.DetachCargo() : null, owner.GlobalPosition + Vector3.Up);
        if (building != null)
        {
            followCamera.ShowMoment(building.GlobalPosition + Vector3.Up * 3, building.GlobalBasis.Z);
            highlightedPart = building.GetNode<Node3D>("Upgrades").GetChild<Node3D>(world.Village.Level(owner.HouseIndex) - 1);
        }
        else if (unload)
        {
            var center = (owner.GlobalPosition + vehicle.GlobalPosition) / 2 + Vector3.Up * 1.5f;
            var outward = followCamera.GlobalPosition - center; outward.Y = 0;
            followCamera.ShowMoment(center, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        }
    }
    private void CelebrateAt(Vector3 position, string message, Node3D? cargo = null, Vector3 target = default)
    {
        ClearHighlight();
        celebrationFx?.QueueFree();
        celebrationFx = new Celebration(); AddChild(celebrationFx); celebrationFx.Setup(position, cargo, target);
        celebrationTime = 3; ui.CelebrationMessage(message);
    }
    private bool InteractWithEvent()
    {
        if (Events.Active >= 0) { Events.Cancel(); ui.EventHud(""); return true; }
        int course = Events.Nearby(vehicle.Position);
        if (course < 0 || nearby != null || AtGarage) return false;
        Events.Begin(course); return true;
    }
    private void TickEvents(double delta)
    {
        bool running = State != GameState.Paused && State != GameState.Title;
        followCamera.MomentPaused = !running;
        if (running && celebrationTime > 0)
        {
            celebrationFx?.Tick(delta); celebrationTime -= delta;
            if (GodotObject.IsInstanceValid(highlightedPart)) highlightedPart!.Scale = Vector3.One * (1 + .025f * Mathf.Sin((float)(3 - celebrationTime) * 8));
            if (celebrationTime <= 0) { ClearHighlight(); celebrationFx?.QueueFree(); celebrationFx = null; ui.CelebrationMessage(""); RestoreCelebrationPanel(); }
        }
        ui.SetEventVisible(State == GameState.Exploring);
        if (State != GameState.Exploring) return;
        int finished = Events.Tick(vehicle.Position);
        if (finished >= 0) { CelebrateAt(vehicle.Position, $"{DrivingEvents.Title(finished)} — krásná jízda!"); SaveProgress(); }
        if (Events.Active >= 0)
        {
            float distance = vehicle.Position.DistanceTo(Events.Points[Events.Active, Events.Next]);
            ui.EventHud($"{DrivingEvents.Title(Events.Active)} · Brána {Events.Next + 1} / {DrivingEvents.GateCount} · {distance:0} m\nBez spěchu · Tlačítkem úkolu ukončíš jízdu");
            ui.EventHint("Ukončit jízdu");
        }
        else
        {
            int course = Events.Nearby(vehicle.Position);
            ui.EventHud(course >= 0 ? $"{DrivingEvents.Title(course)}\nDobrovolná jízda bez časového limitu" : "");
            if (course >= 0 && nearby == null && !AtGarage) ui.EventHint("Zahájit jízdu");
        }
    }
}
