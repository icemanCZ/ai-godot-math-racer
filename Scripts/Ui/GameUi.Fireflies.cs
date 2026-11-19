using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.Ui;

/// <summary>
/// The witch's firefly hunt: her night panel (the collection of stars, a green check mark that lights the lantern and a sun that
/// leads on to the weather question) and the lantern in the corner while a hunt is on: a bar that fills with the fireflies
/// for the next star and a few words when a star lights.
/// </summary>
public partial class GameUi
{
    private PanelContainer fireflyHud = null!;
    private Label fireflyCount = null!;
    private Label fireflyNote = null!;
    private ProgressBar fireflyBar = null!;

    private void SetupFireflies()
    {
        fireflyHud = new PanelContainer
        {
            Name = "FireflyHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("1d2859", .88f);
        fireflyHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(fireflyHud);
        fireflyHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        fireflyHud.OffsetLeft = 32;
        fireflyHud.OffsetRight = 372;
        fireflyHud.OffsetTop = 80;
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        fireflyHud.AddChild(column);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 10);
        column.AddChild(row);
        row.AddChild(
            new DeliveryPicture
            {
                Icon = "lantern",
                CustomMinimumSize = new(48, 44),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            }
        );
        fireflyCount = new Label
        {
            Name = "FireflyCount",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        fireflyCount.AddThemeFontSizeOverride("font_size", 34);
        fireflyCount.AddThemeColorOverride("font_color", new Color("ffe9a0"));
        row.AddChild(fireflyCount);
        fireflyBar = new ProgressBar
        {
            Name = "FireflyBar",
            MinValue = 0,
            MaxValue = FireflyHunt.PerStar,
            ShowPercentage = false,
            CustomMinimumSize = new(0, 20),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        fireflyBar.AddThemeStyleboxOverride("fill", Rounded("f6e27a", 10));
        fireflyBar.AddThemeStyleboxOverride("background", Rounded("3a4682", 10));
        column.AddChild(fireflyBar);
        fireflyNote = new Label
        {
            Name = "FireflyNote",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new(300, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        fireflyNote.AddThemeFontSizeOverride("font_size", 20);
        fireflyNote.AddThemeColorOverride("font_color", new Color("fff3c9"));
        column.AddChild(fireflyNote);
    }

    private static StyleBoxFlat Rounded(string color, int radius) =>
        new()
        {
            BgColor = new Color(color),
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
        };

    /// <summary>
    /// The lantern in the corner while a hunt is on: the fireflies in it for the next star, a bar that fills as they come, and
    /// <paramref name="note"/> (a few words after a star lit, "" for none). It sits under the treasure detector when that shows.
    /// </summary>
    internal void FireflyHud(bool visible, int towards, int perStar, string note)
    {
        fireflyHud.Visible = visible && !modal.Visible;
        if (!fireflyHud.Visible)
            return;
        fireflyHud.OffsetTop = treasureHud.Visible ? 196 : 80;
        fireflyHud.OffsetBottom = fireflyHud.OffsetTop + (note == "" ? 104 : 170);
        fireflyCount.Text = Texts.FireflyCounter(towards, perStar);
        fireflyBar.Value = towards;
        fireflyNote.Visible = note != "";
        fireflyNote.Text = note;
        fireflyHud.AccessibilityName = Texts.FireflyLantern;
    }

    /// <summary>
    /// The witch's night panel: the stars of the collection that shine, then a green check mark that lights the lantern (only on
    /// a starry night, while it is not lit and the collection is not complete), the sun that leads to the weather question and
    /// the way back.
    /// </summary>
    internal void FireflyPanel(Character witch, int[] lit, bool active, bool complete, bool night)
    {
        Clear();
        Text(witch.DisplayName, 30);
        int shining = 0;
        foreach (int stars in lit)
            shining += stars;
        Text(Texts.FireflyStars(shining, FireflyHunt.TotalStars), 26);
        var grid = new GridContainer { Columns = 2, Name = "StarCollection" };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 12);
        content.AddChild(grid);
        for (int picture = 0; picture < Constellation.All.Count; picture++)
            grid.AddChild(
                new StarPicture
                {
                    Name = "Stars" + picture,
                    Picture = picture,
                    Lit = picture < lit.Length ? lit[picture] : 0,
                    CustomMinimumSize = new(250, 128),
                }
            );
        if (complete)
            Text(Texts.FireflyCollectionComplete, 22);
        else if (active)
            Text(Texts.FireflyStillOn, 22);
        else if (!night)
            Text(Texts.FireflyNeedNight, 22);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "FireflyButtons" };
        buttons.AddThemeConstantOverride("separation", 16);
        content.AddChild(buttons);
        Button? first = null;
        if (night && !active && !complete)
            first = AcceptButton(
                () => Send(new UiIntent.StartFireflyHunt()),
                "StartFireflyHunt",
                Texts.FireflyStart,
                buttons
            );
        var weather = IconButton("clear", () => Send(new UiIntent.ChangeWeather()), "ChangeWeather", buttons);
        weather.AccessibilityName = Texts.FireflyChangeWeather;
        weather.TooltipText = Texts.FireflyChangeWeather;
        (first ?? weather).GrabFocus();
        BackButton("Back", buttons);
    }
}
