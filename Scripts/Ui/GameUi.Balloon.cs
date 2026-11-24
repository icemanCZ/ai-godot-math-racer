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
    private Label balloonControls = null!;

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
        balloonControls = new Label
        {
            Name = "BalloonControls",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        balloonControls.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(balloonControls);
    }

    /// <summary>The corner panel during a flight: the stars caught of all of the flight's, and how the balloon is flown.</summary>
    internal void BalloonHud(bool visible, int caught, int stars)
    {
        balloonHud.Visible = visible && !modal.Visible;
        if (!visible)
            return;
        balloonCount.Text = $"{caught} / {stars}";
        balloonControls.Text = controller ? Texts.BalloonControlsPad : Texts.BalloonControlsKeys;
        balloonHud.AccessibilityName = Texts.BalloonCaught(caught, stars);
    }

    internal void BalloonHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.BalloonHint;
    }

    /// <summary>
    /// The balloonist's panel: her balloon, how it is flown, the stars caught in all and the rooster they bring, a fact
    /// about the first balloon, and the arrow of a flight while the balloon stands on its field.
    /// </summary>
    internal void BalloonPanel(Character balloonist, int flights, int stars, bool canFly)
    {
        Clear();
        Text(balloonist.DisplayName, 30);
        StatusPicture("balloon");
        Text(flights == 0 ? Texts.BalloonHowTo : Texts.BalloonHowToAgain, 20);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "BalloonStars" };
        row.AddThemeConstantOverride("separation", 8);
        content.AddChild(row);
        Picture(row, "star")!.CustomMinimumSize = new(56, 46);
        var count = new Label { Text = stars.ToString(), VerticalAlignment = VerticalAlignment.Center };
        count.AddThemeFontSizeOverride("font_size", 30);
        row.AddChild(count);
        Text(
            stars >= BalloonPlans.RoosterStars
                ? $"✓ {WorldPetCatalog.Rooster.Name}"
                : $"{WorldPetCatalog.Rooster.Name} · {stars} / {BalloonPlans.RoosterStars}",
            20
        );
        Text(Texts.BalloonFact, 18);
        if (canFly)
        {
            var fly = IconButton("play", () => Send(new UiIntent.BalloonFlight()), "StartBalloonFlight");
            fly.AccessibilityName = Texts.BalloonFlightStart;
            fly.GrabFocus();
            BackButton();
            return;
        }
        BackButton().GrabFocus();
    }
}
