using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

/// <summary>What the heron's panel shows: the landing still to be built, the atlas at home, or the island's buttons.</summary>
internal enum BirdwatcherMode
{
    Build,
    Home,
    Island,
}

/// <summary>
/// The ferry to the bird island (<c>docs/FERRY_PLAN.md</c>): the heron's panel, made to be understood without reading,
/// and the view through the telescope of the hide.
/// </summary>
public partial class GameUi
{
    private TelescopeOverlay telescope = null!;

    private void SetupFerry()
    {
        telescope = new TelescopeOverlay { Name = "Telescope", Visible = false };
        root.AddChild(telescope);
        root.MoveChild(telescope, 0);
        telescope.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    /// <summary>
    /// The view through the telescope: <paramref name="hint"/> is the way to turn for the nearest bird still hidden (-1
    /// left, 1 right, 0 in view), <paramref name="progress"/> how far the ring in the middle has filled and
    /// <paramref name="birds"/> the birds of the crossing with whether they are seen.
    /// </summary>
    internal void TelescopeHud(bool visible, int hint, float progress, (int Kind, bool Seen)[] birds)
    {
        telescope.Visible = visible;
        if (!visible)
            return;
        telescope.Hint = hint;
        telescope.Progress = progress;
        telescope.Birds = birds;
        int seen = 0;
        foreach (var bird in birds)
            if (bird.Seen)
                seen++;
        telescope.AccessibilityName = Texts.FerryTelescopeSeen(seen, birds.Length);
    }

    /// <summary>Whether the view through the telescope shows, for the checks.</summary>
    internal bool TelescopeHudShown => telescope.Visible;

    internal void BirdwatcherHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.BirdwatcherHint;
    }

    /// <summary>
    /// The heron's panel. While the landing is built: the punt, the five loads (the delivered ones bright), then the
    /// green check mark that sends the trailer, the trip under way, or the raw material the works needs. At home: the
    /// atlas of the eight birds (faint until seen, with how many), the kingfisher they bring, and the green check mark
    /// for the crossing. On the island: the four birds of the crossing, the green check mark of the telescope and the
    /// punt that goes home. What to do is said aloud when the panel opens.
    /// </summary>
    internal void BirdwatcherPanel(
        Character heron,
        BirdwatcherMode mode,
        int stage,
        HaulTrip? trip,
        bool canStart,
        Works? missing,
        int[] kinds,
        (int Kind, bool Seen)[] visit,
        CollectorPet? kingfisher,
        bool canSail
    )
    {
        Clear();
        Text(heron.DisplayName, 30);
        if (mode == BirdwatcherMode.Build)
        {
            FerryPicture(BirdPicture.Punt, false, 120);
            var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "FerryLoads" };
            loads.AddThemeConstantOverride("separation", 6);
            content.AddChild(loads);
            for (int i = 0; i < FerryPlans.Trips; i++)
            {
                var picture = Picture(loads, LoadIcon(FerryPlans.CargoOf(i)))!;
                picture.CustomMinimumSize = new(56, 50);
                if (i >= stage)
                    picture.Modulate = new Color(1, 1, 1, .32f);
            }
            Text($"{stage} / {FerryPlans.Trips}", 30);
            if (trip != null)
                Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
            else if (canStart)
                AcceptButton(() => Send(new UiIntent.FerryStart()), "StartFerryTrip", Texts.ZaprahnoutVozik)
                    .GrabFocus();
            else if (missing != null)
            {
                var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
                need.AddThemeConstantOverride("separation", 10);
                content.AddChild(need);
                Picture(need, LoadIcon(missing.Spec.Input))!.CustomMinimumSize = new(100, 84);
                var arrow = Picture(need, "arrow")!;
                arrow.CustomMinimumSize = new(70, 64);
                arrow.Direction = Mathf.Pi / 2;
                Picture(need, WorksCatalog.ProductIcon(missing.Spec.Product))!.CustomMinimumSize = new(100, 84);
                AcceptButton(
                        () => Send(new UiIntent.MissingSupply()),
                        "StartMissingSupply",
                        Texts.ZaprahnoutVozikProSurovinu
                    )
                    .GrabFocus();
            }
            else
                Text("…", 30);
            var back = BackButton();
            if (trip != null || !canStart && missing == null)
                back.GrabFocus();
            return;
        }
        if (mode == BirdwatcherMode.Island)
        {
            var row = new HBoxContainer { Name = "IslandBirds", Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 10);
            content.AddChild(row);
            for (int i = 0; i < visit.Length; i++)
                AtlasCell(row, $"IslandBird{i}", visit[i].Kind, visit[i].Seen, visit[i].Seen ? "✓" : "?", 76);
        }
        else
            FerryPicture(BirdPicture.Punt, false, 100);
        var atlas = new HBoxContainer { Name = "Atlas", Alignment = BoxContainer.AlignmentMode.Center };
        atlas.AddThemeConstantOverride("separation", 4);
        content.AddChild(atlas);
        for (int kind = 0; kind < FerryPlans.Kinds; kind++)
        {
            int count = kind < kinds.Length ? kinds[kind] : 0;
            AtlasCell(atlas, $"AtlasBird{kind}", kind, count > 0, count > 0 ? count.ToString() : "", 54);
        }
        PetGoal(kingfisher, "binoculars");
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 16);
        content.AddChild(buttons);
        Button? first = null;
        if (mode == BirdwatcherMode.Island)
        {
            first = AcceptButton(
                () => Send(new UiIntent.FerryTelescope()),
                "Telescope",
                Texts.FerryTelescopeButton,
                buttons
            );
            var home = Button("", () => Send(new UiIntent.FerryHome()), buttons);
            home.Name = "FerryHome";
            home.CustomMinimumSize = new(150, 76);
            home.AccessibilityName = Texts.FerryHomeButton;
            var punt = new BirdPicture { Kind = BirdPicture.Punt, MouseFilter = Control.MouseFilterEnum.Ignore };
            home.AddChild(punt);
            punt.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }
        else if (canSail)
            first = AcceptButton(() => Send(new UiIntent.FerryStart()), "FerrySail", Texts.FerrySailButton, buttons);
        var close = BackButton(parent: buttons);
        (first ?? close).GrabFocus();
    }

    private void FerryPicture(int kind, bool faint, float size)
    {
        var middle = new CenterContainer();
        content.AddChild(middle);
        middle.AddChild(
            new BirdPicture
            {
                Kind = kind,
                Faint = faint,
                CustomMinimumSize = new(size, size * .8f),
            }
        );
    }

    private static void AtlasCell(Container row, string name, int kind, bool seen, string mark, float size)
    {
        var cell = new VBoxContainer
        {
            Name = name,
            Alignment = BoxContainer.AlignmentMode.Center,
            TooltipText = seen ? FerryPlans.Name(kind) : "?",
        };
        row.AddChild(cell);
        cell.AddChild(
            new BirdPicture
            {
                Kind = kind,
                Faint = !seen,
                CustomMinimumSize = new(size, size),
            }
        );
        var label = new Label { Text = mark, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 18);
        cell.AddChild(label);
    }
}
