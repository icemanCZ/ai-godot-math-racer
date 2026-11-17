using System;
using Godot;

namespace MathRacer;

/// <summary>What the garage shows about the trailer won at the ford.</summary>
internal readonly record struct GarageTrailer(bool Owned, bool Attached, string Paint);

public partial class GameUi
{
    public event Action? BuildQuestRequested;
    public event Action? MissingSupplyRequested;
    public event Action? LookoutRequested;
    public event Action? TrailerToggleRequested;
    public event Action<string>? TrailerPaintSelected;

    internal static string LoadIcon(HaulLoad load) =>
        load switch
        {
            HaulLoad.Stones => "stones",
            HaulLoad.Soil => "soil",
            HaulLoad.Logs => "logs",
            HaulLoad.Planks => "planks",
            HaulLoad.Bricks => "bricks",
            HaulLoad.Gravel => "gravel",
            HaulLoad.Fabric => "fabric",
            HaulLoad.Capstone => "capstone",
            HaulLoad.Flag => "flag",
            _ => "sand",
        };

    internal void BuilderHint(int project)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text =
            (controller ? "A · " : "E · ")
            + project switch
            {
                BuildPlans.Bridge => "Bobr",
                BuildPlans.Mill => "Mlynář",
                BuildPlans.Tower => "Hvězdář",
                _ => "Rybář",
            };
    }

    /// <summary>
    /// A builder's panel: ten pictures show the loads of the building (the delivered ones bright), then either the
    /// play button, the trip under way, or, when the goods for the next delivery are not made yet, the raw material
    /// that the works needs.
    /// </summary>
    internal void BuilderPanel(
        Animal builder,
        BuildProject project,
        BuildTrip? trip,
        bool canStart,
        Works? missing,
        int lookout = 0
    )
    {
        Clear();
        Text(builder.DisplayName, 30);
        var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "BuildLoads" };
        loads.AddThemeConstantOverride("separation", 3);
        content.AddChild(loads);
        for (int i = 0; i < BuildProject.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(project.Steps[i].Cargo))!;
            picture.CustomMinimumSize = new(58, 50);
            if (i >= project.Stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text(
            project.Complete ? $"✓  {project.Stage} / {BuildProject.Trips}" : $"{project.Stage} / {BuildProject.Trips}",
            30
        );
        if (project.Complete)
        {
            Text("Hotovo!", 24);
            if (lookout > 0)
            {
                // The finished tower lets the player look round the valley; the stargazer rests after each look.
                var look = IconButton("binoculars", () => LookoutRequested?.Invoke(), "StartLookout");
                look.AccessibilityName = "Rozhled";
                look.Disabled = lookout == 2;
                if (lookout == 1)
                    look.GrabFocus();
            }
        }
        else if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("play", () => BuildQuestRequested?.Invoke(), "StartBuildTrip");
            start.AccessibilityName = "Zapřáhnout vozík";
            start.GrabFocus();
        }
        else if (missing != null)
        {
            // The next load is goods that the works has not made: show what the works needs.
            var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
            need.AddThemeConstantOverride("separation", 10);
            content.AddChild(need);
            Picture(need, LoadIcon(missing.Spec.Input))!.CustomMinimumSize = new(100, 84);
            var arrow = Picture(need, "arrow")!;
            arrow.CustomMinimumSize = new(70, 64);
            arrow.Direction = Mathf.Pi / 2;
            Picture(need, WorksCatalog.ProductIcon(missing.Spec.Product))!.CustomMinimumSize = new(100, 84);
            // The builder sends the player for the raw material right away.
            var fetch = IconButton("play", () => MissingSupplyRequested?.Invoke(), "StartMissingSupply");
            fetch.AccessibilityName = "Zapřáhnout vozík pro surovinu";
            fetch.GrabFocus();
        }
        else
            Text("…", 30);
        var back = BackButton();
        if (!canStart && missing == null && lookout != 1)
            back.GrabFocus();
    }

    internal void HaulHud(HaulTrip? trip)
    {
        foreach (Node child in deliveryHud.GetChildren())
        {
            deliveryHud.RemoveChild(child);
            child.QueueFree();
        }
        directionPicture = null;
        deliveryHud.Visible = trip != null;
        if (trip == null)
            return;
        directionPicture = Picture(deliveryHud, "arrow");
        directionPicture!.CustomMinimumSize = new(80, 80);
        Control destination =
            trip.Loaded && trip.DropsAtContractor
                ? new DeliveryPicture { Animal = trip.Contractor }
                : new DeliveryPicture { Icon = LoadIcon(trip.Load) };
        destination.CustomMinimumSize = new(80, 80);
        deliveryHud.AddChild(destination);
        if (trip.Counter != "")
            deliveryHud.AddChild(
                new Label
                {
                    Name = "NavigationText",
                    Text = trip.Counter,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CustomMinimumSize = new(100, 80),
                }
            );
    }

    private void LoadQuestion(LoadChallenge question, System.Collections.Generic.IReadOnlySet<int> rejected)
    {
        Clear();
        var picture = Picture(content, "trailer-load", count: LoadChallenge.Capacity)!;
        picture.FilledCount = question.Present;
        picture.Cargo = LoadIcon(question.Cargo);
        picture.CustomMinimumSize = new(420, 270);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 18);
        content.AddChild(row);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            int answer = choice;
            var button = Button(
                rejected.Contains(answer) ? $"× {answer}" : answer.ToString(),
                () => AnswerSelected?.Invoke(answer),
                row,
                narrate: false
            );
            button.Name = $"Answer{answer}";
            button.CustomMinimumSize = new(150, 110);
            button.Disabled = rejected.Contains(answer);
            button.AddThemeFontSizeOverride("font_size", 48);
            if (!button.Disabled)
                first ??= button;
        }
        BackButton("LeaveChallenge");
        first?.GrabFocus();
        NarrateAnswerButtons(row, question);
    }

    /// <summary>The trailer row in the garage: hitch it for fun and pick a colour.</summary>
    private void GarageTrailerRow(
        VBoxContainer catalogue,
        ScrollContainer scroll,
        GarageTrailer trailer,
        ref Button? focus
    )
    {
        catalogue.AddChild(new Label { Text = "Vozík" });
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        catalogue.AddChild(row);
        Button Option(string name, string text, Action action)
        {
            var button = Button(
                text,
                () =>
                {
                    garageScroll = scroll.ScrollVertical;
                    garageFocus = name;
                    action();
                },
                row
            );
            button.Name = name;
            button.Disabled = !trailer.Owned;
            button.CustomMinimumSize = new(205, 48);
            button.AddThemeFontSizeOverride("font_size", 17);
            if (!trailer.Owned)
            {
                button.AddThemeStyleboxOverride("disabled", Style("d5d5d5", 16));
                button.AddThemeColorOverride("font_disabled_color", new Color("777777"));
                button.TooltipText = "Postav s bobrem Bóďou most a vozík je tvůj.";
            }
            return button;
        }
        var toggle = Option(
            "GarageTrailer",
            !trailer.Owned ? "Vozík · zatím nezískán"
                : trailer.Attached ? "✓ Vozík připojený"
                : "Připojit vozík",
            () => TrailerToggleRequested?.Invoke()
        );
        if (trailer.Owned && garageFocus == toggle.Name)
            focus = toggle;
        foreach (var (id, name, _) in TrailerModel.Paints)
        {
            var paint = Option(
                "GarageTrailerPaint_" + id,
                (trailer.Owned && trailer.Paint == id ? "✓ " : "") + name,
                () => TrailerPaintSelected?.Invoke(id)
            );
            if (trailer.Owned && garageFocus == paint.Name)
                focus = paint;
        }
    }
}
