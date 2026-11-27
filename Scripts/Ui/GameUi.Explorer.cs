using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Vehicles;

namespace MathRacer.Ui;

/// <summary>
/// The owl's map of the valley (<c>docs/EXPLORER_PLAN.md</c>): her panel with the map she draws and the eight wonders
/// under it, and the corner panel while her road arrows lead to a wonder, with the arrow to it, its picture, how far it is
/// and how many wonders are in the map.
/// </summary>
public partial class GameUi
{
    private PanelContainer explorerHud = null!;
    private DeliveryPicture explorerArrow = null!;
    private Control explorerGoal = null!;
    private Label explorerCount = null!;
    private Label explorerDistance = null!;
    private int explorerGoalKind = -1;

    private void SetupExplorer()
    {
        explorerHud = new PanelContainer
        {
            Name = "ExplorerHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("5a4a7a", .9f);
        explorerHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(explorerHud);
        explorerHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        explorerHud.OffsetLeft = -200;
        explorerHud.OffsetRight = 200;
        explorerHud.OffsetTop = 96;
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        explorerHud.AddChild(row);
        explorerArrow = new DeliveryPicture
        {
            Name = "ExplorerArrow",
            Icon = "arrow",
            CustomMinimumSize = new(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(explorerArrow);
        explorerGoal = new Control
        {
            Name = "ExplorerGoal",
            CustomMinimumSize = new(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(explorerGoal);
        explorerDistance = HudLabel("ExplorerDistance", 26);
        explorerDistance.AddThemeColorOverride("font_color", new Color("fff3c4"));
        row.AddChild(explorerDistance);
        row.AddChild(
            new DeliveryPicture
            {
                Name = "ExplorerFlag",
                Icon = "flag",
                CustomMinimumSize = new(40, 40),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        explorerCount = HudLabel("ExplorerCount", 26);
        row.AddChild(explorerCount);
    }

    /// <summary>
    /// The corner panel while the owl's arrows lead to a wonder: the arrow turned to <paramref name="target"/> as seen from
    /// the camera, the picture of the wonder of <paramref name="kind"/>, the <paramref name="distance"/> and how many
    /// wonders are <paramref name="recorded"/> in the map. It sits under the corner panels of a trail and of the sheep.
    /// </summary>
    internal void ExplorerHud(bool visible, Vehicle car, Vector3 target, int kind, int recorded, string distance)
    {
        explorerHud.Visible = visible && !modal.Visible;
        if (!explorerHud.Visible)
            return;
        float top = 96;
        foreach (var above in new Control[] { trackingHud, sheepHud })
            if (above.Visible)
                top = Mathf.Max(top, above.OffsetTop + above.Size.Y + 10);
        explorerHud.OffsetTop = top;
        if (kind != explorerGoalKind)
        {
            explorerGoalKind = kind;
            foreach (var child in explorerGoal.GetChildren())
            {
                explorerGoal.RemoveChild(child);
                child.QueueFree();
            }
            var picture = new LandmarkPicture { Name = "ExplorerGoalPicture", Kind = kind };
            explorerGoal.AddChild(picture);
            picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        explorerDistance.Text = distance;
        explorerCount.Text = $"{recorded} / {ExplorerPlans.Landmarks}";
        explorerHud.AccessibilityName =
            $"{ExplorerPlans.Name(kind)}, {distance}. {Texts.ExplorerOnMap(recorded, ExplorerPlans.Landmarks)}";
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
        explorerArrow.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        explorerArrow.QueueRedraw();
    }

    /// <summary>Whether the corner panel of the owl's map shows, for the checks.</summary>
    internal bool ExplorerHudShown => explorerHud.Visible;

    internal void CartographerHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.CartographerHint;
    }

    /// <summary>
    /// The owl's panel, made to be understood without reading: her map of the valley with the car on it, a row of the
    /// eight wonders under it, faint while hidden, bright once seen and with a check mark once in the map; the little owl
    /// they bring; the green check mark that writes the wonders seen into the map (a question first) and the magnifying
    /// glass that lights the road arrows to the nearest wonder not yet seen. When only the arrows can be had, the check
    /// mark lights them. What to do is said aloud when the panel opens.
    /// </summary>
    internal void CartographerPanel(
        Character owl,
        ValleyMapData map,
        LandmarkStatus[] status,
        Vector2 car,
        int guided,
        CollectorPet? owlet,
        bool canRecord,
        bool canGuide
    )
    {
        Clear();
        Text(owl.DisplayName, 30);
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(
            new ValleyMapPicture
            {
                Name = "ValleyMap",
                Map = map,
                Status = status,
                Car = car,
                Guided = guided,
                CustomMinimumSize = new(380, 380),
                TooltipText = Texts.ExplorerMap,
            }
        );
        var row = new HBoxContainer { Name = "Landmarks", Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 4);
        content.AddChild(row);
        for (int i = 0; i < status.Length; i++)
        {
            var cell = new VBoxContainer
            {
                Name = $"Landmark{i}",
                Alignment = BoxContainer.AlignmentMode.Center,
                TooltipText = status[i] == LandmarkStatus.Hidden ? "?" : ExplorerPlans.Name(i),
            };
            row.AddChild(cell);
            cell.AddChild(
                new LandmarkPicture
                {
                    Kind = i,
                    Faint = status[i] == LandmarkStatus.Hidden,
                    CustomMinimumSize = new(56, 56),
                }
            );
            var mark = new Label
            {
                Text = status[i] switch
                {
                    LandmarkStatus.Recorded => "✓",
                    LandmarkStatus.Seen => "!",
                    _ => "?",
                },
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            mark.AddThemeFontSizeOverride("font_size", 18);
            cell.AddChild(mark);
        }
        PetGoal(owlet, "flag");
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        content.AddChild(buttons);
        Button? first = null;
        if (canRecord)
            first = AcceptButton(
                () => Send(new UiIntent.ExplorerRecord()),
                "RecordLandmarks",
                Texts.ExplorerRecordButton,
                buttons
            );
        if (canGuide)
        {
            var guide = canRecord
                ? IconButton("search", () => Send(new UiIntent.ExplorerGuide()), "GuideLandmark", buttons)
                : AcceptButton(
                    () => Send(new UiIntent.ExplorerGuide()),
                    "GuideLandmark",
                    Texts.ExplorerGuideButton,
                    buttons
                );
            guide.AccessibilityName = Texts.ExplorerGuideButton;
            first ??= guide;
        }
        var back = BackButton(parent: buttons);
        (first ?? back).GrabFocus();
    }
}
