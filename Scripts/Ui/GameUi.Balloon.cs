using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;

namespace MathRacer.Ui;

/// <summary>
/// The balloon station (<c>docs/BALLOON_PLAN.md</c>): the balloonist's panel (the balloon, how to fly it, the stars caught
/// so far and how many more bring the rooster, and the arrow that takes off), the talk-button hint and, during a flight,
/// the corner panel with the stars caught on it and the controls of the burner and the valve.
/// </summary>
public partial class GameUi
{
    private PanelContainer balloonHud = null!;
    private Label balloonCount = null!;
    private HBoxContainer balloonControls = null!;
    private bool? balloonKeysForPad;

    private void SetupBalloon()
    {
        balloonHud = new PanelContainer
        {
            Name = "BalloonHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        balloonHud.AddThemeStyleboxOverride("panel", CoinPanelStyle());
        root.AddChild(balloonHud);
        balloonHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        balloonHud.OffsetLeft = 32;
        balloonHud.OffsetRight = 392;
        balloonHud.OffsetTop = 80;
        balloonHud.OffsetBottom = 196;
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        balloonHud.AddChild(column);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        column.AddChild(row);
        row.AddChild(
            new DeliveryPicture
            {
                Icon = "star",
                CustomMinimumSize = new(56, 46),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        balloonCount = new Label
        {
            Name = "BalloonCount",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        balloonCount.AddThemeFontSizeOverride("font_size", 38);
        row.AddChild(balloonCount);
        balloonControls = new HBoxContainer { Name = "BalloonControls", MouseFilter = Control.MouseFilterEnum.Ignore };
        balloonControls.AddThemeConstantOverride("separation", 6);
        column.AddChild(balloonControls);
    }

    /// <summary>The corner panel during a flight: the stars caught of all of the flight's, and how the balloon is flown.</summary>
    internal void BalloonHud(bool visible, int caught, int stars)
    {
        balloonHud.Visible = visible && !modal.Visible;
        if (!visible)
            return;
        balloonCount.Text = $"{caught} / {stars}";
        if (balloonKeysForPad != controller)
        {
            balloonKeysForPad = controller;
            foreach (var child in balloonControls.GetChildren())
            {
                balloonControls.RemoveChild(child);
                child.QueueFree();
            }
            BalloonKeys(balloonControls, 34);
        }
        balloonHud.AccessibilityName = Texts.BalloonCaught(caught, stars);
    }

    internal void BalloonHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.BalloonHint;
    }

    /// <summary>
    /// How the balloon is flown, in pictures: the key (or trigger) of the burner with an arrow up and the key of the valve
    /// with an arrow down.
    /// </summary>
    private void BalloonKeys(Container parent, int size)
    {
        parent.TooltipText = Texts.BalloonControls;
        foreach (var (key, pad, up) in new[] { ("W", "RT", true), ("S", "LT", false) })
        {
            var cap = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            var face = Style("fff5df", 10);
            face.BorderColor = new Color("8a6d4a");
            face.BorderWidthBottom = 4;
            face.BorderWidthLeft = face.BorderWidthRight = face.BorderWidthTop = 2;
            face.ContentMarginLeft = face.ContentMarginRight = 10;
            cap.AddThemeStyleboxOverride("panel", face);
            parent.AddChild(cap);
            var label = new Label
            {
                Text = controller ? pad : key,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new(size, size),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.AddThemeFontSizeOverride("font_size", size * 2 / 3);
            label.AddThemeColorOverride("font_color", new Color("3d4a4f"));
            cap.AddChild(label);
            parent.AddChild(
                new DeliveryPicture
                {
                    Icon = "arrow",
                    Direction = up ? 0 : Mathf.Pi,
                    CustomMinimumSize = new(size * 1.6f, size * 1.6f),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            if (up)
                parent.AddChild(new Control { CustomMinimumSize = new(size / 2, 0) });
        }
    }

    /// <summary>
    /// The balloonist's panel, made to be understood without reading: her balloon, how it is flown (the keys with arrows
    /// up and down), the stars that bring the rooster (and all the stars caught once it came), and the green check mark of
    /// a flight while the balloon stands on its field. How to fly is said aloud when the panel opens for the first time.
    /// </summary>
    internal void BalloonPanel(Character balloonist, int stars, CollectorPet? rooster, bool canFly)
    {
        Clear();
        Text(balloonist.DisplayName, 30);
        StatusPicture("balloon");
        var keys = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "BalloonKeys" };
        content.AddChild(keys);
        BalloonKeys(keys, 44);
        PetGoal(rooster, "star");
        if (stars >= BalloonPlans.RoosterStars)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "BalloonStars" };
            row.AddThemeConstantOverride("separation", 8);
            content.AddChild(row);
            Picture(row, "star")!.CustomMinimumSize = new(56, 46);
            var count = new Label { Text = stars.ToString(), VerticalAlignment = VerticalAlignment.Center };
            count.AddThemeFontSizeOverride("font_size", 30);
            row.AddChild(count);
        }
        if (canFly)
        {
            var fly = AcceptButton(
                () => Send(new UiIntent.BalloonFlight()),
                "StartBalloonFlight",
                Texts.BalloonFlightStart
            );
            fly.GrabFocus();
            BackButton();
            return;
        }
        BackButton().GrabFocus();
    }
}
