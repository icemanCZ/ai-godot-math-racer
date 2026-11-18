using System;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.Ui;

/// <summary>What a house or a village resident offers: the reward, the collection, the expedition choice.</summary>
public partial class GameUi
{
    public void HouseReward(Character animal, int level)
    {
        Clear();
        Text($"✓  {level} / {Village.Upgrades.Length}", 34);
        BackButton().GrabFocus();
    }

    public void HouseCollection(Character animal, int collected)
    {
        Clear();
        Picture(content, "apple", count: 3);
        Text($"{collected} / 3", 30);
        BackButton().GrabFocus();
    }

    public void HouseFruitOffer(Character animal, int level, bool canStart)
    {
        Clear();
        Picture(content, "apple", count: 3);
        if (canStart)
        {
            var start = IconButton("play", () => Send(new UiIntent.VillageFruit()), "VillageFruitTask");
            start.AccessibilityName = "Vyrazit pro ovoce";
        }
        else
            Text("…", 34);
        BackButton();
    }

    public void ExpeditionChoice(Character animal, VehicleAppearance appearance)
    {
        Clear();
        talkButton.Hide();
        Text("✓", 38);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 24);
        content.AddChild(row);
        var look = System.Linq.Enumerable.First(CarLook.Catalogue, option => option.Id == appearance.CurrentLookId);
        Button? first = null;
        foreach (bool collection in new[] { false, true })
        {
            var card = new VBoxContainer();
            card.AddThemeConstantOverride("separation", 12);
            row.AddChild(card);
            card.AddChild(
                new CarLookPreview
                {
                    Look = look,
                    ModelId = appearance.CurrentModelId,
                    ExpeditionCargo =
                        collection || animal.Kind == 1 ? "apple"
                        : animal.Kind == 0 ? "carrot"
                        : "honey",
                    CargoAmount = collection ? 0 : 1,
                }
            );
            var select = Button(collection ? "Dovoz" : "Rozvoz", () => Send(new UiIntent.Expedition(collection)), card);
            select.Name = collection ? "ChooseCollection" : "ChooseDelivery";
            first ??= select;
        }
        BackButton("LeaveExpeditionChoice");
        first?.GrabFocus();
    }
}
