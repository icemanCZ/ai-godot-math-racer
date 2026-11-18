using System;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

public partial class GameUi
{
    /// <summary>
    /// What a house wants for its next improvement: a picture of the goods, then the trip under way, the play
    /// button that sends the trailer for them or, when the works has none, the raw material that it needs.
    /// </summary>
    internal void HouseMaterialOffer(Character resident, HaulLoad load, HaulTrip? trip, bool canStart, Works? missing)
    {
        Clear();
        Text(resident.DisplayName, 30);
        Picture(content, LoadIcon(load))!.CustomMinimumSize = new(170, 140);
        Button? action = null;
        if (trip != null)
            Picture(content, trip.Loaded ? LoadIcon(trip.Load) : "trailer");
        else if (canStart)
        {
            action = IconButton("play", () => Send(new UiIntent.HouseTrip()), "StartHouseTrip");
            action.AccessibilityName = Texts.ZaprahnoutVozikProMaterial;
        }
        else if (missing != null)
        {
            // The goods are not made yet: show what the works needs and send the player for it.
            var need = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Name = "MissingGoods" };
            need.AddThemeConstantOverride("separation", 10);
            content.AddChild(need);
            Picture(need, LoadIcon(missing.Spec.Input))!.CustomMinimumSize = new(100, 84);
            var arrow = Picture(need, "arrow")!;
            arrow.CustomMinimumSize = new(70, 64);
            arrow.Direction = Mathf.Pi / 2;
            Picture(need, WorksCatalog.ProductIcon(missing.Spec.Product))!.CustomMinimumSize = new(100, 84);
            action = IconButton("play", () => Send(new UiIntent.MissingSupply()), "StartMissingSupply");
            action.AccessibilityName = Texts.ZaprahnoutVozikProSurovinu;
        }
        else
            Text("…", 30);
        var back = BackButton();
        (action ?? back).GrabFocus();
    }
}
