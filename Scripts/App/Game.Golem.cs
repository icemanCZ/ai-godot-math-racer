using System;
using System.Linq;
using Godot;
using MathRacer.Characters;

namespace MathRacer.App;

public partial class Game
{
    internal bool GolemOwned { get; private set; }
    internal bool GolemActive { get; private set; }
    private Node3D golem = null!;
    private float golemPhase;

    private void SetupGolem()
    {
        golem = GolemModel.Create();
        world.AddChild(golem);
        golem.Hide();
    }

    private void RestoreGolem(bool owned, bool active)
    {
        GolemOwned = owned;
        GolemActive = owned && active;
        companionWanders[GolemWanderIndex] = default;
        golem.Visible = GolemActive;
        golem.Position = vehicle.Position + vehicle.Transform.Basis.Z * 5;
    }

    internal void ToggleGolem()
    {
        if (State != GameState.Garage || !GolemOwned)
            return;
        GolemActive = !GolemActive;
        golem.Visible = GolemActive;
        companionWanders[GolemWanderIndex] = default;
        if (GolemActive)
            golem.Position = vehicle.Position + vehicle.Transform.Basis.Z * 5;
        ShowGarage();
        SaveProgress();
    }

    private float CompanionSurface(Vector3 point) =>
        world.CrossingSurface(
            point.X,
            point.Z,
            world.Routes.SurfaceHeight(point, world.Ground.Height(point.X, point.Z))
        );

    private void TickGolem(float delta)
    {
        if (!GolemActive || State != GameState.Exploring)
            return;
        var target = CompanionTarget(
            GolemWanderIndex,
            golem.Position,
            vehicle.Position + vehicle.Transform.Basis.Z * (5 + TrailerTrailShift),
            delta
        );
        target.Y = CompanionSurface(target);
        var offset = target - golem.Position;
        offset.Y = 0;
        if (offset.LengthSquared() > 80 * 80)
        {
            companionWanders[GolemWanderIndex] = default;
            golem.Position = target;
            return;
        }
        float speed = Mathf.Clamp(offset.Length() * 2.8f, 0, companionStopTime >= .65f ? 5 : 35);
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
        nearby = world.Sorcerer;
        vehicle.Active = false;
        State = GameState.SorcererStatus;
        ShowSorcerer();
        Speak(
            GolemOwned ? Texts.GolemJeHotovy
                : Delivery?.Golem == true ? Texts.GolemProgress(Delivery.Collected)
                : Texts.GolemNabidka,
            world.Sorcerer
        );
    }

    private void ShowSorcerer() => ui.Sorcerer(world.Sorcerer, Delivery, GolemOwned);

    internal void BeginGolemQuest()
    {
        if (State != GameState.SorcererStatus || Delivery?.Golem == true || GolemOwned)
            return;
        // Like every trip, the way for the golem's pieces starts with a quiz.
        quiz.Departure = Departure.Golem;
        BeginQuiz();
    }

    private void CompleteGolemQuest()
    {
        if (Delivery == null || !Delivery.Golem || !Delivery.CanDeliver)
            return;
        EndExpedition(refreshUi: false);
        RestoreGolem(true, true);
        AddCoins(GolemCoins);
        world.Sorcerer.Celebrate();
        world.Sorcerer.Guide(false);
        ui.DeliveryHud(null);
        UpdateDeliveryGuides();
        State = GameState.SorcererStatus;
        ShowSorcerer();
        Speak(Texts.GolemJeHotovy, world.Sorcerer);
        SaveProgress();
    }
}
