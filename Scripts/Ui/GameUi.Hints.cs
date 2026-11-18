using System;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.Ui;

/// <summary>The hints under the screen and the status lines of the collectors.</summary>
public partial class GameUi
{
    public void GarageHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Garáž" : "E · Garáž";
    }

    public void HouseHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Domeček" : "E · Domeček";
    }

    public void FarmHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Farma" : "E · Farma";
    }

    public void GardenHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Zahradník" : "E · Zahradník";
    }

    public void CollectionHint(bool toys)
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = (controller ? "A · " : "E · ") + (toys ? "Hračkář" : "Sběratel drahokamů");
    }

    public void CollectionStatus(Village village, Character owner, Expedition? expedition, bool returned = false)
    {
        bool toys = owner.Role is ExhibitCollectorRole { Toys: true };
        int count = village.CollectionCount(toys);
        Clear();
        Text(owner.DisplayName, 30);
        Text($"{(returned ? "✓  " : "")}{count} / {Village.CollectionNames(toys).Length}", 28);
        if (expedition?.Sender == owner)
            Text(expedition.CollectionName, 22);
        else if (expedition != null)
            Text("…", 26);
        CollectorPetStatus(village.PetFor(owner));
        BackButton().GrabFocus();
    }

    public void GardenStatus(Village village, Expedition? expedition, bool returned = false)
    {
        Clear();
        Text("Zahradník Vítek", 32);
        Text($"{(returned ? "✓  " : "")}{village.GardenCount} / {Village.FlowerNames.Length}", 28);
        if (expedition?.Garden == true)
            Text(expedition.FlowerName, 22);
        else if (expedition != null)
            Text("…", 26);
        CollectorPetStatus(village.PetFor(village.Gardener));
        BackButton().GrabFocus();
    }

    public void FarmStatus(Village village, Expedition? expedition, bool returned = false)
    {
        Clear();
        Text("Farmář Tonda", 32);
        Text($"{(returned ? "✓  " : "")}{village.FarmCount} / {Village.FarmKinds.Length}", 28);
        if (expedition?.Farm == true)
            Text(expedition.FarmName, 22);
        else if (expedition != null)
            Text("…", 26);
        CollectorPetStatus(village.PetFor(village.Farmer));
        BackButton().GrabFocus();
    }

    private void CollectorPetStatus(CollectorPet pet) =>
        Text(pet.IsOwned ? $"✓ {pet.DisplayName}" : $"{pet.DisplayName} · {pet.Goal}", 20);
}
