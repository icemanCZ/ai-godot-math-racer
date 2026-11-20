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
        float column = 5 + TrailerTrailShift;
        var target = CompanionTarget(
            GolemWanderIndex,
            golem.Position,
            CompanionTrailSpot(GolemWanderIndex, 0, column, out _),
            column,
            delta
        );
        target.Y = CompanionSurface(target);
        if (FlatDistanceSquared(target, golem.Position) > 80 * 80)
        {
            companionWanders[GolemWanderIndex] = default;
            golem.Position = target;
            return;
        }
        var position = StepCompanion(GolemWanderIndex, golem.Position, target, delta, 5, 35, 2.8f);
        golem.Position = new(position.X, CompanionSurface(position), position.Z);
        // The golem's model faces -Z, so it turns half a turn further than the pets.
        golem.Rotation = new(0, CompanionYaw(GolemWanderIndex, golem.Position, golem.Rotation.Y, Mathf.Pi, delta), 0);
        float speed = companionWanders[GolemWanderIndex].Velocity.Length();
        if (speed > .3f)
            golemPhase += delta * (5 + speed * .7f);
        float swing = speed > .3f ? Mathf.Sin(golemPhase) * .65f * Mathf.Min(speed / 2, 1) : 0;
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
