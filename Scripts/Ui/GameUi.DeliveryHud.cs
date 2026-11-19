using System;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Quizzes;
using MathRacer.Tasks;
using MathRacer.Vehicles;

namespace MathRacer.Ui;

/// <summary>The delivery shown on screen while driving: the order and the direction of the goal.</summary>
public partial class GameUi
{
    public void DeliveryHud(Expedition? delivery)
    {
        foreach (Node child in deliveryHud.GetChildren())
        {
            deliveryHud.RemoveChild(child);
            child.QueueFree();
        }
        directionPicture = null;
        deliveryHud.Visible = delivery != null;
        if (delivery == null)
            return;
        directionPicture = Picture(deliveryHud, "arrow");
        directionPicture!.CustomMinimumSize = new(80, 80);
        Control? destination =
            delivery.CanDeliver ? new DeliveryPicture { Character = delivery.Recipient }
            : delivery.Garden ? new WordPicture { PictureId = "flower" }
            : delivery.SpecialCollection || delivery.Golem ? null
            : new DeliveryPicture { Icon = delivery.Cargo };
        if (destination != null)
        {
            destination.CustomMinimumSize = new(80, 80);
            deliveryHud.AddChild(destination);
        }
        string progress = delivery.Collection ? $"{delivery.Collected} / {delivery.Amount}" : $"× {delivery.Amount}";
        var label = new Label
        {
            Name = "NavigationText",
            Text = progress,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new(100, 80),
        };
        deliveryHud.AddChild(label);
    }

    /// <summary>Turns the top arrow straight toward <paramref name="target"/>, as seen from the camera.</summary>
    public void DeliveryDirection(Vector3 target, Vehicle car)
    {
        if (directionPicture == null)
            return;
        var delta = target - car.Position;
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return;
        var forward = -camera.GlobalBasis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var right = camera.GlobalBasis.X;
        right.Y = 0;
        right = right.Normalized();
        directionPicture.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        directionPicture.QueueRedraw();
    }

    public void DeliveryHint(bool visible)
    {
        talkButton.Visible = visible && !modal.Visible;
        talkButton.Text = controller ? "A · ?" : "E · ?";
        hint.Text = "";
    }
}
