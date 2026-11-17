using System;
using Godot;

namespace MathRacer;

/// <summary>What the garage shows about the trailer won at the ford.</summary>
internal readonly record struct GarageTrailer(bool Owned, bool Attached, string Paint);

public partial class GameUi
{
    public event Action? FordQuestRequested;
    public event Action? TrailerToggleRequested;
    public event Action<string>? TrailerPaintSelected;

    internal static string LoadIcon(HaulLoad load) =>
        load switch
        {
            HaulLoad.Stones => "stones",
            HaulLoad.Soil => "soil",
            _ => "sand",
        };

    internal void BeaverHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Bobr" : "E · Bobr";
    }

    internal void Beaver(Animal beaver, int stage, FordTrip? trip, bool canStart)
    {
        Clear();
        Text(beaver.DisplayName, 30);
        // Six small pictures show which loads are already in the ford.
        var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        loads.AddThemeConstantOverride("separation", 4);
        content.AddChild(loads);
        for (int i = 0; i < Ford.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(Ford.TripLoads[i]))!;
            picture.CustomMinimumSize = new(78, 66);
            if (i >= stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text(stage >= Ford.Trips ? $"✓  {stage} / {Ford.Trips}" : $"{stage} / {Ford.Trips}", 30);
        if (stage >= Ford.Trips)
            Text("Most je hotový!", 24);
        else if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("play", () => FordQuestRequested?.Invoke(), "StartFordTrip");
            start.AccessibilityName = "Zapřáhnout vozík";
            start.GrabFocus();
        }
        else
            Text("…", 30);
        var back = BackButton();
        if (!canStart)
            back.GrabFocus();
    }

    internal void HaulHud(FordTrip? trip, Animal? beaver)
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
            trip.Loaded && !trip.FarBank && beaver != null
                ? new DeliveryPicture { Animal = beaver }
                : new DeliveryPicture { Icon = LoadIcon(trip.Load) };
        destination.CustomMinimumSize = new(80, 80);
        deliveryHud.AddChild(destination);
        deliveryHud.AddChild(
            new Label
            {
                Name = "NavigationText",
                Text = $"{trip.Trip + 1} / {Ford.Trips}",
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
