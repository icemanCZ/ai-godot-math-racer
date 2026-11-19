using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Ui;

public partial class GameUi
{
    internal void SorcererHint()
    {
        talkButton.Visible = !modal.Visible;
        talkButton.Text = controller ? Texts.ACernokneznik : Texts.ECernokneznik;
    }

    internal void Sorcerer(Character sorcerer, Expedition? delivery, bool owned)
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
            var start = IconButton("check", () => Send(new UiIntent.GolemQuest()), "StartGolemQuest");
            start.AccessibilityName = Texts.VyrazitProDilky;
            start.GrabFocus();
        }
        BackButton();
    }
}
