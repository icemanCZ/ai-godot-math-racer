using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Quizzes;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Ui;

/// <summary>
/// The ferrywoman's rally (<c>docs/RALLY_PLAN.md</c>): her card with the four stages, the medal and the best time of each
/// and a green arrow that sends the player to its start; and the corner panel of a drive, with the arrow to the start,
/// the next gate or the finish, the clock, a mark for each gate of the stage, a note, and before a gate its question
/// with the three answers in the colours of the lanes, left to right as they hang over the road.
/// </summary>
public partial class GameUi
{
    private PanelContainer rallyHud = null!;
    private DeliveryPicture rallyArrow = null!;
    private HBoxContainer rallyStage = null!;
    private (Border From, Border To)? rallyStageShown;
    private Label rallyClock = null!;
    private RallyStrip rallyStrip = null!;
    private Label rallyDistance = null!;
    private Label rallyNote = null!;
    private VBoxContainer rallyQuestion = null!;
    private IChallenge? rallyAsked;

    private void SetupRally()
    {
        rallyHud = new PanelContainer
        {
            Name = "RallyHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("2f5873", .92f);
        rallyHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(rallyHud);
        // On the left, so that the gate ahead in the middle of the picture stays in sight.
        rallyHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        rallyHud.OffsetLeft = 32;
        rallyHud.OffsetRight = 32 + 440;
        rallyHud.OffsetTop = 80;
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 6);
        rallyHud.AddChild(column);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        column.AddChild(row);
        rallyArrow = new DeliveryPicture
        {
            Name = "RallyArrow",
            Icon = "arrow",
            CustomMinimumSize = new(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(rallyArrow);
        var text = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        row.AddChild(text);
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        top.AddThemeConstantOverride("separation", 14);
        text.AddChild(top);
        rallyStage = new HBoxContainer { Name = "RallyStage", MouseFilter = Control.MouseFilterEnum.Ignore };
        rallyStage.AddThemeConstantOverride("separation", 2);
        top.AddChild(rallyStage);
        rallyClock = HudLabel("RallyClock", 30);
        rallyClock.AddThemeColorOverride("font_color", new Color("fff3c4"));
        top.AddChild(rallyClock);
        rallyDistance = HudLabel("RallyDistance", 24);
        top.AddChild(rallyDistance);
        rallyStrip = new RallyStrip
        {
            Name = "RallyStrip",
            CustomMinimumSize = new(320, 40),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        text.AddChild(rallyStrip);
        rallyNote = HudLabel("RallyNote", 19);
        rallyNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rallyNote.CustomMinimumSize = new(320, 0);
        text.AddChild(rallyNote);
        rallyQuestion = new VBoxContainer
        {
            Name = "RallyQuestion",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        rallyQuestion.AddThemeConstantOverride("separation", 6);
        column.AddChild(rallyQuestion);
    }

    /// <summary>
    /// The corner panel of a drive, made to be followed without reading: the arrow turned to <paramref name="target"/>
    /// as seen from the camera (none for a note alone), the pictures of the borders the stage runs between, the clock,
    /// the <paramref name="distance"/> to the start, the strip of the stage with the car on it (<paramref
    /// name="progress"/>, 0 at the start, 1 at the finish) and its gates at <paramref name="gates"/> (green right, red
    /// wrong, white ahead), a note, and the question of the next gate, if it is near. It sits on the left, under the
    /// corner panels of a treasure or a hunt.
    /// </summary>
    internal void RallyHud(
        bool visible,
        Vehicle car,
        Vector3? target,
        (Border From, Border To)? stage,
        string clock,
        float[] gates,
        bool?[] answers,
        float? progress,
        string distance,
        string note,
        IChallenge? question
    )
    {
        rallyHud.Visible = visible && !modal.Visible;
        if (!rallyHud.Visible)
            return;
        float top = 80;
        if (treasureHud.Visible)
            top = treasureHud.OffsetTop + treasureHud.Size.Y + 10;
        if (huntHud.Visible)
            top = Mathf.Max(top, huntHud.OffsetTop + huntHud.Size.Y + 10);
        rallyHud.OffsetTop = top;
        rallyArrow.Visible = target != null;
        if (stage != rallyStageShown)
        {
            rallyStageShown = stage;
            foreach (var child in rallyStage.GetChildren())
            {
                rallyStage.RemoveChild(child);
                child.QueueFree();
            }
            if (stage is { } shown)
            {
                rallyStage.AddChild(BorderPicture(shown.From, new(46, 40)));
                rallyStage.AddChild(
                    new DeliveryPicture
                    {
                        Icon = "arrow",
                        Direction = Mathf.Pi / 2,
                        CustomMinimumSize = new(26, 26),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    }
                );
                rallyStage.AddChild(BorderPicture(shown.To, new(46, 40)));
            }
        }
        rallyStage.Visible = stage != null;
        rallyClock.Text = clock;
        rallyClock.Visible = clock != "";
        rallyDistance.Text = distance;
        rallyDistance.Visible = distance != "";
        rallyStrip.Visible = stage != null;
        rallyStrip.Show(gates, answers, progress);
        rallyNote.Text = note;
        rallyNote.Visible = note != "";
        ShowRallyQuestion(question);
        rallyHud.AccessibilityName = $"{clock}. {note}";
        if (target is not { } goal)
            return;
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return;
        var delta = goal - car.Position;
        var forward = -camera.GlobalBasis.Z;
        forward.Y = 0;
        forward = forward.Normalized();
        var right = camera.GlobalBasis.X;
        right.Y = 0;
        right = right.Normalized();
        rallyArrow.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        rallyArrow.QueueRedraw();
    }

    /// <summary>The question of the next gate, built again only when it changes: its text and its three answers.</summary>
    private void ShowRallyQuestion(IChallenge? question)
    {
        rallyQuestion.Visible = question != null;
        if (ReferenceEquals(question, rallyAsked))
            return;
        rallyAsked = question;
        foreach (var child in rallyQuestion.GetChildren())
        {
            rallyQuestion.RemoveChild(child);
            child.QueueFree();
        }
        if (question == null)
            return;
        var pictures = question as IPictureChallenge;
        var expression = HudLabel("RallyExpression", pictures != null ? 46 : 40);
        expression.Text = question.Expression;
        expression.HorizontalAlignment = HorizontalAlignment.Center;
        rallyQuestion.AddChild(expression);
        var row = new HBoxContainer
        {
            Name = "RallyAnswers",
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 14);
        rallyQuestion.AddChild(row);
        for (int i = 0; i < 3; i++)
        {
            int choice = question.Choices[i];
            var tile = new PanelContainer
            {
                Name = $"RallyAnswer{i}",
                CustomMinimumSize = pictures != null ? new(150, 122) : new(130, 84),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var frame = Style("ffffff", 14);
            frame.ContentMarginLeft = frame.ContentMarginRight = 8;
            frame.ContentMarginTop = frame.ContentMarginBottom = 6;
            frame.BorderColor = new Color(RallyGates.LaneColors[i]);
            frame.BorderWidthLeft = frame.BorderWidthRight = frame.BorderWidthTop = frame.BorderWidthBottom = 7;
            tile.AddThemeStyleboxOverride("panel", frame);
            row.AddChild(tile);
            if (pictures != null)
                tile.AddChild(
                    new WordPicture
                    {
                        PictureId = pictures.Pictures[choice].Id,
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    }
                );
            else
            {
                var label = new Label
                {
                    Text = RallyGates.ChoiceText(question, choice),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                label.AddThemeFontSizeOverride("font_size", 46);
                label.AddThemeColorOverride("font_color", new Color("2b3a40"));
                tile.AddChild(label);
            }
        }
    }

    /// <summary>Whether the corner panel of the rally shows, its note and whether it asks a question, for the checks.</summary>
    internal bool RallyHudShown => rallyHud.Visible;

    internal string RallyHudNote => rallyHud.Visible ? rallyNote.Text : "";

    internal bool RallyHudAsks => rallyHud.Visible && rallyQuestion.Visible;

    /// <summary>
    /// The ferrywoman's card, made to be understood without reading: for every stage its medal, the pictures of the
    /// borders it runs from and to, its best time and a green check mark that sends the player to its start (read aloud
    /// when it is chosen, with the medal); the stage under way shows the car instead of a time. Below, the gold medals
    /// that bring the otter and the stamps of the post.
    /// </summary>
    internal void RallyPanel(
        Character keeper,
        (Border From, Border To)[] stages,
        string[] names,
        float[] best,
        RallyMedal[] medals,
        int driving,
        CollectorPet? otter,
        int stamps
    )
    {
        Clear();
        Text(keeper.DisplayName, 30);
        var grid = new GridContainer { Columns = 6, Name = "RallyCard" };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 8);
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(grid);
        Button? first = null;
        Button? firstWithoutGold = null;
        for (int i = 0; i < stages.Length; i++)
        {
            grid.AddChild(new MedalPicture { Medal = medals[i], CustomMinimumSize = new(48, 60) });
            grid.AddChild(BorderPicture(stages[i].From, new(70, 58)));
            grid.AddChild(
                new DeliveryPicture
                {
                    Icon = "arrow",
                    Direction = Mathf.Pi / 2,
                    CustomMinimumSize = new(40, 40),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            grid.AddChild(BorderPicture(stages[i].To, new(70, 58)));
            if (i == driving)
                grid.AddChild(
                    new DeliveryPicture
                    {
                        Name = "RallyDriving",
                        Icon = "vehicle",
                        CustomMinimumSize = new(100, 56),
                        MouseFilter = Control.MouseFilterEnum.Ignore,
                    }
                );
            else
            {
                var time = new Label
                {
                    Text = best[i] > 0 ? RallyPlans.Clock(best[i]) : "",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    CustomMinimumSize = new(100, 0),
                };
                time.AddThemeFontSizeOverride("font_size", 24);
                grid.AddChild(time);
            }
            int stage = i;
            var go = AcceptButton(
                () => Send(new UiIntent.RallyStart(stage)),
                $"RallyStart{i}",
                OutlandTexts.RallyStageSpoken(i + 1, names[i], medals[i]),
                grid
            );
            go.CustomMinimumSize = new(110, 58);
            // The first stage never driven is offered first, else the first one without gold.
            if (first == null && best[i] <= 0)
                first = go;
            if (firstWithoutGold == null && medals[i] != RallyMedal.Gold)
                firstWithoutGold = go;
        }
        PetGoal(otter, new MedalPicture { Medal = RallyMedal.Gold });
        KeeperStamps(stamps);
        var back = BackButton();
        (first ?? firstWithoutGold ?? back).GrabFocus();
    }

    /// <summary>The picture of a border: the sea, the mountains, the river or the forest.</summary>
    private static DeliveryPicture BorderPicture(Border border, Vector2 size) =>
        new()
        {
            Icon = BorderIcon((int)border),
            CustomMinimumSize = size,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
}

/// <summary>A medal drawn as a disc on a ribbon: gold, silver or bronze, or a faint empty ring for no medal yet.</summary>
public partial class MedalPicture : Control
{
    public RallyMedal Medal { get; init; }

    public override void _Ready() => Resized += QueueRedraw;

    public override void _Draw()
    {
        float r = Mathf.Min(Size.X, Size.Y) * .32f;
        var center = new Vector2(Size.X / 2, Size.Y - r - 3);
        if (Medal == RallyMedal.None)
        {
            DrawArc(center, r, 0, Mathf.Tau, 32, new Color(1, 1, 1, .35f), 3, true);
            return;
        }
        var (face, rim) = Medal switch
        {
            RallyMedal.Gold => ("f2c23a", "b5851a"),
            RallyMedal.Silver => ("d9dde2", "8c949c"),
            _ => ("d38b4e", "8f5527"),
        };
        DrawColoredPolygon(
            new[]
            {
                new Vector2(center.X - r * .9f, 2),
                new Vector2(center.X - r * .1f, 2),
                new Vector2(center.X + r * .25f, center.Y - r * .6f),
                new Vector2(center.X - r * .35f, center.Y - r * .5f),
            },
            new Color("4a86d4")
        );
        DrawColoredPolygon(
            new[]
            {
                new Vector2(center.X + r * .9f, 2),
                new Vector2(center.X + r * .1f, 2),
                new Vector2(center.X - r * .25f, center.Y - r * .6f),
                new Vector2(center.X + r * .35f, center.Y - r * .5f),
            },
            new Color("e2533f")
        );
        DrawCircle(center, r, new Color(rim));
        DrawCircle(center, r * .8f, new Color(face));
        DrawArc(center, r * .55f, -2.4f, -.7f, 12, new Color(1, 1, 1, .55f), 2, true);
    }
}

/// <summary>
/// The strip of a rally stage in the corner panel: a road from the chequered start to the chequered finish, a post for
/// every gate where it stands (a white ring ahead, a green one with a check mark for a right answer, a red one with a
/// cross for a wrong one) and the car as a red dot where it is.
/// </summary>
public partial class RallyStrip : Control
{
    private float[] gates = System.Array.Empty<float>();
    private bool?[] answers = System.Array.Empty<bool?>();
    private float? progress;

    /// <summary>Draws the strip again when anything on it moved.</summary>
    internal void Show(float[] gates, bool?[] answers, float? progress)
    {
        bool moved =
            progress.HasValue != this.progress.HasValue
            || (progress is { } now && this.progress is { } before && Mathf.Abs(now - before) > .002f);
        bool changed =
            !System.Linq.Enumerable.SequenceEqual(gates, this.gates)
            || !System.Linq.Enumerable.SequenceEqual(answers, this.answers);
        if (!moved && !changed)
            return;
        this.gates = (float[])gates.Clone();
        this.answers = (bool?[])answers.Clone();
        this.progress = progress;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float left = 18,
            right = Size.X - 18,
            y = Size.Y / 2 + 4;
        DrawLine(new(left, y), new(right, y), new Color("e7d6a8"), 8, true);
        Chequer(new(left, y));
        Chequer(new(right, y));
        float At(float fraction) => Mathf.Lerp(left, right, Mathf.Clamp(fraction, 0, 1));
        for (int i = 0; i < gates.Length; i++)
        {
            var center = new Vector2(At(gates[i]), y);
            bool? answer = i < answers.Length ? answers[i] : null;
            if (answer == null)
            {
                DrawCircle(center, 11, new Color("2f5873"));
                DrawArc(center, 10, 0, Mathf.Tau, 24, Colors.White, 3, true);
                continue;
            }
            DrawCircle(center, 12, new Color(answer.Value ? "4f9d5e" : "d1695c"));
            DrawArc(center, 12, 0, Mathf.Tau, 24, Colors.White, 2, true);
            if (answer.Value)
                DrawPolyline(
                    new[] { center + new Vector2(-6, 0), center + new Vector2(-2, 5), center + new Vector2(6, -5) },
                    Colors.White,
                    3,
                    true
                );
            else
            {
                DrawLine(center + new Vector2(-5, -5), center + new Vector2(5, 5), Colors.White, 3, true);
                DrawLine(center + new Vector2(-5, 5), center + new Vector2(5, -5), Colors.White, 3, true);
            }
        }
        if (progress is { } position)
        {
            var car = new Vector2(At(position), y);
            DrawCircle(car, 10, Colors.White);
            DrawCircle(car, 8, new Color("e2533f"));
        }
    }

    /// <summary>A small chequered flag standing on the road: the start and the finish.</summary>
    private void Chequer(Vector2 foot)
    {
        DrawLine(foot + new Vector2(0, 8), foot + new Vector2(0, -18), new Color("fbf6ee"), 2, true);
        for (int row = 0; row < 2; row++)
        for (int column = 0; column < 3; column++)
            DrawRect(
                new Rect2(foot + new Vector2(1 + column * 4, -18 + row * 4), new Vector2(4, 4)),
                (row + column) % 2 == 0 ? new Color("1f1f24") : new Color("fbf6ee")
            );
    }
}
