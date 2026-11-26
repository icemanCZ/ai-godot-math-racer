using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Ui;

/// <summary>
/// The shepherd's lost sheep (<c>docs/SHEEP_PLAN.md</c>): his panel with the fold, a picture and a name for every sheep at
/// home or behind the car and a question mark with the place it ran to for the others; and the corner panel while they
/// are looked for, with the arrow to the next sheep (or to the one left behind, or home), how far it is, how many sheep
/// follow the car and how many are home, and a note such as a plea to drive more slowly.
/// </summary>
public partial class GameUi
{
    private PanelContainer sheepHud = null!;
    private DeliveryPicture sheepArrow = null!;
    private HBoxContainer sheepGoal = null!;
    private Label sheepCount = null!;
    private Label sheepDistance = null!;
    private Label sheepNote = null!;
    private string sheepGoalKey = "";

    private void SetupSheep()
    {
        sheepHud = new PanelContainer
        {
            Name = "SheepHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("4f6a3a", .9f);
        sheepHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(sheepHud);
        sheepHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        sheepHud.OffsetLeft = -250;
        sheepHud.OffsetRight = 250;
        sheepHud.OffsetTop = 96;
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        sheepHud.AddChild(row);
        sheepArrow = new DeliveryPicture
        {
            Name = "SheepArrow",
            Icon = "arrow",
            CustomMinimumSize = new(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(sheepArrow);
        sheepGoal = new HBoxContainer { Name = "SheepGoal", MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(sheepGoal);
        var column = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(column);
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        top.AddThemeConstantOverride("separation", 14);
        column.AddChild(top);
        sheepCount = HudLabel("SheepCount", 24);
        top.AddChild(sheepCount);
        sheepDistance = HudLabel("SheepDistance", 24);
        sheepDistance.AddThemeColorOverride("font_color", new Color("fff3c4"));
        top.AddChild(sheepDistance);
        sheepNote = HudLabel("SheepNote", 19);
        sheepNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        sheepNote.CustomMinimumSize = new(300, 0);
        column.AddChild(sheepNote);
    }

    private static Label HudLabel(string name, int size)
    {
        var label = new Label { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color("fff5df"));
        label.AddThemeColorOverride("font_outline_color", new Color("243018"));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    /// <summary>
    /// The corner panel while the sheep are looked for: the arrow turned to <paramref name="target"/> as seen from the
    /// camera, a sheep (or <paramref name="who"/>, the shepherd, when the arrow points home), the sheep behind the car and
    /// at home, the <paramref name="distance"/> and a <paramref name="note"/>. It sits under the corner panel of a trail
    /// when that shows.
    /// </summary>
    internal void SheepHud(
        bool visible,
        Vehicle car,
        Vector3 target,
        Character? who,
        int following,
        int home,
        int all,
        string distance,
        string note
    )
    {
        sheepHud.Visible = visible && !modal.Visible;
        if (!sheepHud.Visible)
            return;
        sheepHud.OffsetTop = trackingHud.Visible ? trackingHud.OffsetTop + trackingHud.Size.Y + 10 : 96;
        string key = who != null ? $"actor:{who.DisplayName}" : "sheep";
        if (key != sheepGoalKey)
        {
            sheepGoalKey = key;
            foreach (var child in sheepGoal.GetChildren())
            {
                sheepGoal.RemoveChild(child);
                child.QueueFree();
            }
            sheepGoal.AddChild(
                new DeliveryPicture
                {
                    Name = "SheepGoalPicture",
                    Icon = "sheep",
                    Character = who,
                    CustomMinimumSize = new(64, 64),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
        }
        string behind = Texts.SheepBehind(following);
        sheepCount.Text = behind == "" ? Texts.SheepHome(home, all) : $"{behind} · {Texts.SheepHome(home, all)}";
        sheepDistance.Text = distance;
        sheepNote.Text = note;
        sheepNote.Visible = note != "";
        sheepHud.AccessibilityName = $"{sheepCount.Text}. {note}";
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
        sheepArrow.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        sheepArrow.QueueRedraw();
    }

    /// <summary>Whether the corner panel of the sheep shows, and its note, for the checks.</summary>
    internal bool SheepHudShown => sheepHud.Visible;

    internal string SheepHudNote => sheepHud.Visible ? sheepNote.Text : "";

    /// <summary>
    /// The shepherd's fold: a sheep for every one that ran off, bright with its name when it is home (✓) or behind the
    /// car (…), faint with a question mark and the place it ran to while it is lost; how to look for them or how many are
    /// still out, the puppy that the whole flock brings, the stamps of the post, the green arrow that sends the player
    /// out and the button that drives the sheep behind the car into the fold.
    /// </summary>
    internal void ShepherdPanel(
        Character keeper,
        bool started,
        SheepStatus[] status,
        string[] where,
        int following,
        int stamps
    )
    {
        Clear();
        Text(keeper.DisplayName, 30);
        Text(Texts.SheepTitle, 24);
        var grid = new GridContainer { Columns = 4, Name = "SheepFold" };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 4);
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(grid);
        int home = 0;
        for (int i = 0; i < status.Length; i++)
        {
            bool known = status[i] is SheepStatus.Home or SheepStatus.Following or SheepStatus.Waiting;
            if (status[i] == SheepStatus.Home)
                home++;
            var cell = new VBoxContainer { Name = $"Sheep{i}", Alignment = BoxContainer.AlignmentMode.Center };
            grid.AddChild(cell);
            cell.AddChild(
                new DeliveryPicture
                {
                    Icon = "sheep",
                    CustomMinimumSize = new(96, 70),
                    Modulate = known ? Colors.White : new Color(1, 1, 1, .4f),
                }
            );
            var name = new Label
            {
                Text = status[i] switch
                {
                    SheepStatus.Home => "✓ " + SheepPlans.Name(i),
                    SheepStatus.Following or SheepStatus.Waiting => "… " + SheepPlans.Name(i),
                    _ => started ? $"{Texts.SheepLost} {where[i]}" : Texts.SheepLost,
                },
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            name.AddThemeFontSizeOverride("font_size", 16);
            cell.AddChild(name);
        }
        int all = status.Length;
        Text(
            !started ? Texts.SheepHowTo
                : home == all ? Texts.SheepAllHome
                : following > 0 ? Texts.SheepBrought(following)
                : Texts.SheepStillLost(all - home),
            19
        );
        Text(
            home >= all ? $"✓ {WorldPetCatalog.Sheepdog.Name}" : $"{WorldPetCatalog.Sheepdog.Name} · {home} / {all}",
            19
        );
        Text(Texts.PostStamps(stamps, Post.StampsPerKeeper), 17);
        if (!started)
        {
            var go = IconButton("play", () => Send(new UiIntent.SheepStart()), "StartSheep");
            go.AccessibilityName = Texts.SheepStart;
            go.GrabFocus();
            BackButton();
            return;
        }
        if (following > 0)
        {
            var pen = IconButton("play", () => Send(new UiIntent.SheepPen()), "PenSheep");
            pen.AccessibilityName = Texts.SheepPenButton;
            pen.GrabFocus();
            BackButton();
            return;
        }
        BackButton().GrabFocus();
    }
}
