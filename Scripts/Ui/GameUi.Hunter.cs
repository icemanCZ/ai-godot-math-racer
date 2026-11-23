using System.Collections.Generic;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Ui;

/// <summary>
/// The hunter's hunt: his panel (the animals in his pen, eight places for the pet, a green check mark that sends the player out
/// with a net), the talk-button hint, and the corner of the screen while a hunt is on: an arrow to where the hunt goes on, a
/// picture of it (the nearest animal, the net in the grass or the hunter), the net on the car, the distance and a few words.
/// </summary>
public partial class GameUi
{
    private PanelContainer huntHud = null!;
    private DeliveryPicture huntArrow = null!;
    private HBoxContainer huntGoal = null!;
    private DeliveryPicture huntNet = null!;
    private Label huntDistance = null!;
    private Label huntNote = null!;
    private string huntGoalKey = "";

    private void SetupHunt()
    {
        huntHud = new PanelContainer
        {
            Name = "HuntHud",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        var style = CoinPanelStyle();
        style.BgColor = new Color("3e5a33", .9f);
        huntHud.AddThemeStyleboxOverride("panel", style);
        root.AddChild(huntHud);
        huntHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        huntHud.OffsetLeft = 32;
        huntHud.OffsetRight = 412;
        huntHud.OffsetTop = 80;
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        huntHud.AddChild(column);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        column.AddChild(row);
        huntArrow = new DeliveryPicture
        {
            Name = "HuntArrow",
            Icon = "arrow",
            CustomMinimumSize = new(72, 72),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(huntArrow);
        huntGoal = new HBoxContainer { Name = "HuntGoal", MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddChild(huntGoal);
        huntNet = new DeliveryPicture
        {
            Name = "HuntNet",
            Icon = "net",
            CustomMinimumSize = new(64, 64),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.AddChild(huntNet);
        huntDistance = new Label
        {
            Name = "HuntDistance",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        huntDistance.AddThemeFontSizeOverride("font_size", 30);
        huntDistance.AddThemeColorOverride("font_color", new Color("fff3c4"));
        row.AddChild(huntDistance);
        huntNote = new Label
        {
            Name = "HuntNote",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new(340, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        huntNote.AddThemeFontSizeOverride("font_size", 20);
        huntNote.AddThemeColorOverride("font_color", new Color("fff3c4"));
        column.AddChild(huntNote);
    }

    /// <summary>
    /// The corner of the screen while a hunt is on: the arrow turned to <paramref name="target"/> as seen from the camera, a
    /// picture of it (<paramref name="icon"/>, or <paramref name="who"/> for a character), the net bright while it is on the
    /// car, the <paramref name="distance"/> and a <paramref name="note"/> ("" for none). It sits under the treasure detector
    /// and the lantern when they show.
    /// </summary>
    internal void HuntHud(
        bool visible,
        Vehicle car,
        Vector3 target,
        string icon,
        Character? who,
        bool netOnCar,
        string distance,
        string note
    )
    {
        huntHud.Visible = visible && !modal.Visible;
        if (!huntHud.Visible)
            return;
        float top = 80;
        if (treasureHud.Visible)
            top = treasureHud.OffsetBottom + 12;
        if (fireflyHud.Visible)
            top = fireflyHud.OffsetBottom + 12;
        huntHud.OffsetTop = top;
        huntHud.OffsetBottom = top + (note == "" ? 96 : 150);
        string key = who != null ? $"actor:{who.DisplayName}" : icon;
        if (key != huntGoalKey)
        {
            huntGoalKey = key;
            foreach (var child in huntGoal.GetChildren())
            {
                huntGoal.RemoveChild(child);
                child.QueueFree();
            }
            huntGoal.AddChild(
                new DeliveryPicture
                {
                    Name = "HuntGoalPicture",
                    Icon = icon,
                    Character = who,
                    CustomMinimumSize = new(72, 72),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
        }
        huntNet.Modulate = new Color(1, 1, 1, netOnCar ? 1 : .3f);
        huntDistance.Text = distance;
        huntNote.Visible = note != "";
        huntNote.Text = note;
        huntHud.AccessibilityName = note != "" ? note : distance;
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
        huntArrow.Direction = Mathf.Atan2(delta.Dot(right), delta.Dot(forward));
        huntArrow.QueueRedraw();
    }

    internal void HunterHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + Texts.HunterHint;
    }

    /// <summary>
    /// The hunter's panel: the animals in his pen, eight places that fill with the animals towards his pet, and then the green
    /// check mark that sends the player out with a net, or, while a hunt is on, only the way back.
    /// </summary>
    internal void HunterPanel(Character hunter, IReadOnlyList<WildKind> penned, bool active, CollectorPet? pet)
    {
        Clear();
        Text(hunter.DisplayName, 30);
        Text(Texts.HuntPenned(penned.Count), 26);
        var places = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "PenPlaces" };
        places.AddThemeConstantOverride("separation", 4);
        content.AddChild(places);
        for (int i = 0; i < AnimalHunt.PetGoal; i++)
        {
            bool filled = i < penned.Count;
            var picture = new DeliveryPicture
            {
                Icon = filled ? AnimalHunt.Icon(penned[i]) : "wild-rabbit",
                CustomMinimumSize = new(58, 52),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, filled ? 1 : .25f),
            };
            places.AddChild(picture);
        }
        int left = AnimalHunt.PetGoal - penned.Count;
        Text(left > 0 ? Texts.HuntUntilPet(left) : Texts.HuntPetOwned, 18);
        if (pet != null && left > 0)
            Text(pet.RewardHint, 16);
        if (active)
        {
            Text(Texts.HunterStillHunting, 22);
            BackButton().GrabFocus();
            return;
        }
        var start = AcceptButton(() => Send(new UiIntent.StartHunt()), "StartHunt", Texts.HuntStart);
        start.GrabFocus();
        BackButton();
    }

    /// <summary>
    /// The hunter's panel before his hut stands: four pictures show the loads of the hut (the delivered ones bright), then the
    /// green check mark that sends the trailer, the trip under way, or, when the goods for the next delivery are not made yet,
    /// the raw material that the works needs.
    /// </summary>
    internal void HunterHutPanel(Character hunter, int stage, HaulTrip? trip, bool canStart, Works? missing)
    {
        Clear();
        Text(hunter.DisplayName, 30);
        Text(Texts.HutUnfinished, 20);
        var loads = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "HutLoads" };
        loads.AddThemeConstantOverride("separation", 8);
        content.AddChild(loads);
        for (int i = 0; i < HutPlans.Trips; i++)
        {
            var picture = Picture(loads, LoadIcon(HutPlans.CargoOf(i)))!;
            picture.CustomMinimumSize = new(64, 58);
            if (i >= stage)
                picture.Modulate = new Color(1, 1, 1, .32f);
        }
        Text($"{stage} / {HutPlans.Trips}", 30);
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            var start = IconButton("check", () => Send(new UiIntent.HutTrip()), "StartHutTrip");
            start.AccessibilityName = Texts.ZaprahnoutVozik;
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
            var fetch = IconButton("check", () => Send(new UiIntent.MissingSupply()), "StartMissingSupply");
            fetch.AccessibilityName = Texts.ZaprahnoutVozikProSurovinu;
            fetch.GrabFocus();
        }
        else
            Text("…", 30);
        var back = BackButton();
        if (!canStart && missing == null)
            back.GrabFocus();
    }
}
