using System;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

public partial class GameUi
{
    internal void SorcererHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? "A · Černokněžník" : "E · Černokněžník";
    }

    internal void Sorcerer(Animal sorcerer, Expedition? delivery, bool owned)
    {
        Clear();
        Text(sorcerer.DisplayName, 30);
        if (owned)
            Text("✓  6 / 6", 32);
        else if (delivery?.Golem == true)
            Text($"{delivery.Collected} / 6", 32);
        else
        {
            Text("0 / 6", 32);
            var start = IconButton("play", () => GolemQuestRequested?.Invoke(), "StartGolemQuest");
            start.AccessibilityName = "Vyrazit pro dílky";
            start.GrabFocus();
        }
        BackButton();
    }
}
