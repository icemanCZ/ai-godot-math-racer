using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Ui;

/// <summary>
/// The forest keeper's book of tracks (<c>docs/TRACKING_PLAN.md</c>): her panel with the print of every animal and the
/// names of those found, the corner panel while a trail is followed (whose trail, how far along it the car is, and the
/// arrow back to it when the car has left it) and the talk-button hint at a den.
/// </summary>
public partial class GameUi
{
    private PanelContainer trackingHud = null!;
    private TextureRect trackingPrint = null!;
    private Label trackingName = null!;
    private ProgressBar trackingBar = null!;
    private DeliveryPicture trackingArrow = null!;
    private TrackingAnimal? trackingShown;

    private static readonly Dictionary<TrackingAnimal, ImageTexture> TrackPictures = new();

    private void SetupTracking()
    {
        trackingHud = new PanelContainer
        {
            Name = "TrackingHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("3d5a2c", .88f);
        trackingHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(trackingHud);
        trackingHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        trackingHud.OffsetLeft = -230;
        trackingHud.OffsetRight = 230;
        trackingHud.OffsetTop = 96;
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        trackingHud.AddChild(row);
        trackingArrow = new DeliveryPicture
        {
            Name = "TrackingArrow",
            Icon = "arrow",
            CustomMinimumSize = new(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        row.AddChild(trackingArrow);
        trackingPrint = new TextureRect
        {
            CustomMinimumSize = new(72, 60),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(trackingPrint);
        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(column);
        trackingName = new Label { Name = "TrackingName", MouseFilter = Control.MouseFilterEnum.Ignore };
        trackingName.AddThemeFontSizeOverride("font_size", 26);
        trackingName.AddThemeColorOverride("font_color", new Color("fff5df"));
        trackingName.AddThemeColorOverride("font_outline_color", new Color("1f2e17"));
        trackingName.AddThemeConstantOverride("outline_size", 6);
        column.AddChild(trackingName);
        trackingBar = new ProgressBar
        {
            Name = "TrackingBar",
            MinValue = 0,
            MaxValue = 1,
            Step = 0,
            ShowPercentage = false,
            CustomMinimumSize = new(0, 18),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var fill = new StyleBoxFlat { BgColor = new Color("ffd54a") };
        fill.SetCornerRadiusAll(9);
        var back = new StyleBoxFlat { BgColor = new Color("1f2e17", .8f) };
        back.SetCornerRadiusAll(9);
        trackingBar.AddThemeStyleboxOverride("fill", fill);
        trackingBar.AddThemeStyleboxOverride("background", back);
        column.AddChild(trackingBar);
    }

    /// <summary>
    /// The corner panel while a trail is followed: its print, whose it is and how far along it the car is; the arrow shows
    /// only when the car has left the trail.
    /// </summary>
    internal void TrackingHud(bool visible, TrackingAnimal? animal, float fraction, bool lost)
    {
        trackingHud.Visible = visible && animal != null && !modal.Visible;
        if (!trackingHud.Visible || animal is not { } shown)
            return;
        if (trackingShown != shown)
        {
            trackingShown = shown;
            trackingPrint.Texture = TrackPicture(shown);
            trackingName.Text = Texts.TrackingTrail(shown);
            trackingHud.AccessibilityName = Texts.TrackingTrail(shown);
        }
        trackingBar.Value = fraction;
        trackingArrow.Visible = lost;
    }

    /// <summary>Whether the corner panel of a trail shows, and its arrow, for the checks.</summary>
    internal bool TrackingHudShown => trackingHud.Visible;

    internal bool TrackingArrowShown => trackingHud.Visible && trackingArrow.Visible;

    /// <summary>Turns the arrow of the corner panel towards the trail, as seen from the camera.</summary>
    internal void TrackingDirection(Vector3 target, Vehicle car)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return;
        var delta = target - car.Position;
        var forward = -camera.GlobalBasis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var right = camera.GlobalBasis.X;
        right.Y = 0;
        right = right.Normalized();
        trackingArrow.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        trackingArrow.QueueRedraw();
    }

    internal void TrackingDenHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.TrackingDenHint;
    }

    /// <summary>
    /// The forest keeper's book of tracks, made to be understood without reading: the print of every animal, with the
    /// names of those found and a question mark for the others, what the trail followed now tells, the prints that bring
    /// the squirrel, the stamps of the post, and the green check mark that sends the player out after the first trail.
    /// How to follow a trail is said aloud when the panel opens.
    /// </summary>
    internal void TrackingPanel(
        Character keeper,
        bool started,
        int found,
        TrackingAnimal? current,
        CollectorPet? squirrel,
        int stamps
    )
    {
        Clear();
        Text(keeper.DisplayName, 30);
        var grid = new GridContainer { Columns = 3, Name = "TrackingBook" };
        grid.AddThemeConstantOverride("h_separation", 18);
        grid.AddThemeConstantOverride("v_separation", 6);
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(grid);
        foreach (var animal in System.Enum.GetValues<TrackingAnimal>())
        {
            int number = (int)animal;
            var cell = new VBoxContainer { Name = $"Track{animal}", Alignment = BoxContainer.AlignmentMode.Center };
            grid.AddChild(cell);
            var print = new TextureRect
            {
                Texture = TrackPicture(animal),
                CustomMinimumSize = new(110, 84),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Modulate = number < found || animal == current ? Colors.White : new Color(1, 1, 1, .45f),
            };
            cell.AddChild(print);
            var name = new Label
            {
                Text =
                    number < found ? "✓ " + TrackingPlans.Name(animal)
                    : animal == current ? "… " + TrackingPlans.Name(animal)
                    : Texts.TrackingHidden,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            name.AddThemeFontSizeOverride("font_size", 17);
            cell.AddChild(name);
        }
        if (current is { } now)
            Text(TrackingPlans.About(now), 19);
        PetGoal(
            squirrel,
            new TextureRect
            {
                Texture = TrackPicture(TrackingAnimal.Fox),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            }
        );
        KeeperStamps(stamps);
        if (!started)
        {
            var go = AcceptButton(() => Send(new UiIntent.TrackingStart()), "StartTracking", Texts.TrackingStart);
            go.GrabFocus();
            BackButton();
            return;
        }
        BackButton().GrabFocus();
    }

    /// <summary>The picture of an animal's print in the book: two prints, a left and a right one, on a patch of mud.</summary>
    internal static ImageTexture TrackPicture(TrackingAnimal animal)
    {
        if (TrackPictures.TryGetValue(animal, out var cached))
            return cached;
        var svg = new StringBuilder(
            "<svg xmlns='http://www.w3.org/2000/svg' width='180' height='150' viewBox='0 0 180 150'>"
                + "<ellipse cx='90' cy='75' rx='84' ry='68' fill='#d9c49a'/>"
        );
        var ovals = TrackingPrints.Ovals(animal);
        bool group = animal == TrackingAnimal.Hare;
        float scale = group ? 240 : 380;
        var prints = group ? new[] { (90f, 75f, 1f) } : new[] { (64f, 92f, -1f), (116f, 58f, 1f) };
        foreach (var (cx, cy, mirror) in prints)
        foreach (var (x, z, rx, rz, turn) in ovals)
        {
            float px = cx + x * scale * mirror,
                py = cy - z * scale;
            float degrees = -Mathf.RadToDeg(turn) * mirror;
            svg.Append(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "<ellipse cx='{0:0.#}' cy='{1:0.#}' rx='{2:0.#}' ry='{3:0.#}' transform='rotate({4:0.#} {0:0.#} {1:0.#})' fill='#4f3320'/>",
                    px,
                    py,
                    rx * scale,
                    rz * scale,
                    degrees
                )
            );
        }
        svg.Append("</svg>");
        var image = new Image();
        image.LoadSvgFromString(svg.ToString(), 2);
        var texture = ImageTexture.CreateFromImage(image);
        TrackPictures[animal] = texture;
        return texture;
    }
}
