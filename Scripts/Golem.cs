using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class World
{
    private void PlaceSorcerer()
    {
        // Pick a high road verge with enough room for a tower and a reachable visitor.
        float best = float.NegativeInfinity;
        Vector3 chosen = Vector3.Zero, facing = Vector3.Forward, tower = Vector3.Zero;
        for (int i = 0; i < Routes.Samples.Count - 1; i += 3)
        {
            var road = Routes.Samples[i]; var tangent = Routes.Samples[i + 1] - road;
            tangent.Y = 0;
            if (tangent.LengthSquared() < .1f || tangent.LengthSquared() > 9) continue;
            var side = tangent.Normalized().Cross(Vector3.Up);
            foreach (int sign in new[] { -1, 1 })
            {
                var visitor = road + side * (6 * sign);
                var building = road + side * (17 * sign);
                visitor.Y = Ground.Height(visitor.X, visitor.Z); building.Y = Ground.Height(building.X, building.Z);
                if (new Vector2(building.X, building.Z).Length() > Settings.PlayableRadius - 22 ||
                    Village.IsNearVillage(building) || Village.IsReserved(building) ||
                    FlatDistance(building, Witch.Position) < 35 || Routes.Distance(building) < 12 ||
                    River.Distance(building.X, building.Z) < River.HalfWidth(building.Z) + 16 ||
                    Mathf.Abs(visitor.Y - road.Y) > 2.5f) continue;
                float score = building.Y - Mathf.Abs(building.Y - visitor.Y) * 2;
                if (score <= best) continue;
                best = score; chosen = visitor; tower = building; facing = -side * sign;
            }
        }
        if (float.IsNegativeInfinity(best)) throw new InvalidOperationException("No place for the sorcerer's tower.");
        Sorcerer = Animal.Create("Černokněžník Baltazar", "Přivez šest dílků golema a získáš malého kamaráda.", 2, "9f93ad");
        Sorcerer.DressAsSorcerer(); Sorcerer.Position = chosen;
        Sorcerer.Rotation = new(0, Mathf.Atan2(facing.X, facing.Z), 0); AddChild(Sorcerer);
        Tower = new Node3D { Name = "SorcererTower", Position = tower }; AddChild(Tower);
        float baseY = 0;
        for (int x = -4; x <= 4; x += 2) for (int z = -4; z <= 4; z += 2)
            baseY = Mathf.Min(baseY, Ground.Height(tower.X + x, tower.Z + z) - tower.Y);
        Art.Cylinder(Tower, new(0, (baseY + 7) / 2, 0), 4.4f, 3.7f, 7 - baseY, "696578");
        Art.Cylinder(Tower, new(0, 7.2f, 0), 4.4f, 4.4f, .5f, "4e495f");
        Art.Cylinder(Tower, new(0, 10.5f, 0), 4.6f, .1f, 6.4f, "514269");
        Art.Ball(Tower, new(0, 13.8f, 0), Vector3.One * .28f, "f3cf70");
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.Tau / 12;
            Art.Block(Tower, new(Mathf.Sin(a) * 4.15f, 7.65f, Mathf.Cos(a) * 4.15f), new(.7f, .75f, .7f), "777185");
        }
        // Windows face out in every direction so the tower reads from the road.
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi / 2; var window = Art.Block(Tower, new(Mathf.Sin(a) * 3.9f, 4.7f, Mathf.Cos(a) * 3.9f), new(.85f, 1.5f, .12f), "f4d989");
            window.Rotation = new(0, a, 0);
        }
        Obstacles.Box(Tower, new(0, 3, 0), new(7, 6, 7));
    }
}

internal static class GolemModel
{
    internal static void Part(Node3D parent, int index, Vector3 position)
    {
        var piece = new Node3D { Position = position, Scale = Vector3.One * .65f }; parent.AddChild(piece);
        Art.Block(piece, Vector3.Zero, index == 0 ? new(1, .8f, .65f) : index == 1 ? new(.75f, .7f, .7f) : new(.35f, .75f, .4f), "9b9c91");
        Art.Block(piece, new(0, .08f, -.36f), new(.18f, .25f, .04f), "75d7c2");
    }
    internal static Node3D Create()
    {
        var golem = new Node3D { Name = "GolemPet" };
        Art.Block(golem, new(0, 1, 0), new(1.15f, 1.15f, .7f), "92978e");
        Art.Block(golem, new(0, 1.95f, 0), new(.9f, .8f, .75f), "aaa99c");
        foreach (float x in new[] { -.25f, .25f }) Art.Ball(golem, new(x, 2, -.39f), new(.09f, .11f, .06f), "78e6d0");
        Art.Block(golem, new(0, 1.15f, -.36f), new(.2f, .3f, .04f), "78e6d0");
        foreach (float x in new[] { -.72f, .72f }) Art.Block(golem, new(x, 1, 0), new(.3f, .9f, .35f), "858b82");
        foreach (float x in new[] { -.32f, .32f })
        {
            var leg = new Node3D { Name = x < 0 ? "LeftLeg" : "RightLeg", Position = new(x, .5f, 0) }; golem.AddChild(leg);
            Art.Block(leg, new(0, -.22f, 0), new(.36f, .55f, .48f), "777f78");
        }
        return golem;
    }
}

public partial class Game
{
    internal const int SorcererId = 42;
    internal bool GolemOwned { get; private set; }
    internal bool GolemActive { get; private set; }
    private Node3D golem = null!;
    private float golemPhase;
    private void SetupGolem() { golem = GolemModel.Create(); world.AddChild(golem); golem.Hide(); }
    private void RestoreGolem(bool owned, bool active)
    {
        GolemOwned = owned; GolemActive = owned && active;
        golem.Visible = GolemActive;
        golem.Position = vehicle.Position + vehicle.Transform.Basis.Z * 5;
    }
    internal void ToggleGolem()
    {
        if (State != GameState.Garage || !GolemOwned) return;
        GolemActive = !GolemActive; golem.Visible = GolemActive;
        if (GolemActive) golem.Position = vehicle.Position + vehicle.Transform.Basis.Z * 5;
        ShowGarage(); SaveProgress();
    }
    private float CompanionSurface(Vector3 point)
    {
        float height = world.Routes.SurfaceHeight(point, world.Ground.Height(point.X, point.Z));
        return River.OnBridge(point.X, point.Z) ? Mathf.Max(height, River.BridgeSurfaceHeight(point.X)) : height;
    }
    private void TickGolem(float delta)
    {
        if (!GolemActive || State != GameState.Exploring) return;
        var target = vehicle.Position + vehicle.Transform.Basis.Z * 5;
        target.Y = CompanionSurface(target);
        var offset = target - golem.Position; offset.Y = 0;
        if (offset.LengthSquared() > 80 * 80) { golem.Position = target; return; }
        float speed = Mathf.Clamp(offset.Length() * 2.8f, 0, 35);
        if (offset.LengthSquared() > .25f)
        {
            var step = offset.Normalized() * Mathf.Min(offset.Length(), speed * delta);
            golem.Position += step;
            golem.Rotation = new(0, Mathf.Atan2(-step.X, -step.Z), 0);
            golemPhase += delta * (5 + speed * .7f);
        }
        golem.Position = new(golem.Position.X, CompanionSurface(golem.Position), golem.Position.Z);
        float swing = offset.LengthSquared() > .25f ? Mathf.Sin(golemPhase) * .65f : 0;
        golem.GetNode<Node3D>("LeftLeg").Rotation = new(swing, 0, 0);
        golem.GetNode<Node3D>("RightLeg").Rotation = new(-swing, 0, 0);
    }
    internal void OpenSorcerer()
    {
        nearby = world.Sorcerer; vehicle.Active = false; State = GameState.SorcererStatus; ShowSorcerer();
    }
    private void ShowSorcerer() => ui.Sorcerer(world.Sorcerer, Delivery, GolemOwned);
    internal void BeginGolemQuest()
    {
        if (State != GameState.SorcererStatus || Delivery != null || GolemOwned) return;
        Delivery = new Expedition(world, vehicle, world.Sorcerer, true); AddChild(Delivery);
        lastCollected = 0; ui.DeliveryHud(Delivery); Start(); UpdateDeliveryGuides(); SaveProgress();
    }
    private void CompleteGolemQuest()
    {
        if (Delivery == null || !Delivery.Golem || !Delivery.CanDeliver) return;
        Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null;
        RestoreGolem(true, true); world.Sorcerer.Celebrate(); world.Sorcerer.Guide(false);
        ui.DeliveryHud(null); UpdateDeliveryGuides(); State = GameState.SorcererStatus; ShowSorcerer(); SaveProgress();
    }
}

public partial class GameUi
{
    internal void SorcererHint() { talkButton.Visible = !modal.Visible; talkButton.Text = controller ? "A · Černokněžník" : "E · Černokněžník"; }
    internal void Sorcerer(Animal sorcerer, Expedition? delivery, bool owned)
    {
        Clear(); Text(sorcerer.DisplayName, 30);
        if (owned) Text("Děkuji za všechny dílky! Malý golem je tvůj kamarád. V garáži v nabídce Peti ho můžeš vypnout nebo znovu zapnout.", 23);
        else if (delivery?.Golem == true) Text($"Najdi všech šest dílků golema a přivez mi je. Nasbíráno: {delivery.Collected} / 6.", 23);
        else if (delivery != null) Text("Až dokončíš svou výpravu, vrať se za mnou. Potřebuji šest dílků golema.", 23);
        else { Text("Přivez mi šest dílků golema. Za odměnu ti dám malého golema, který poběží za tvým autem!", 23); Button("Vyrazit pro dílky", () => GolemQuestRequested?.Invoke()).GrabFocus(); }
        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke());
    }
}
