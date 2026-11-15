using Godot;
using System;

namespace MathRacer;

public partial class GameUi
{
    private void SyllableTiles(SyllableChallenge question, SyllableSelection selection, Action<int> choose)
    {
        var group = new VBoxContainer();
        group.AddThemeConstantOverride("separation", 6);
        content.AddChild(group);
        var instruction = new Label { Text = $"✓  {selection.Count} / {question.Syllables.Count}",
            HorizontalAlignment = HorizontalAlignment.Center };
        instruction.AddThemeFontSizeOverride("font_size", 22);
        group.AddChild(instruction);
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 8);
        group.AddChild(grid);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            bool selected = selection.IsSelected(choice);
            bool rejected = selection.IsRejected(choice);
            string tile = question.Tiles[choice];
            var button = Button(selected ? $"✓ {tile}" : rejected ? $"× {tile}" : tile, () => choose(choice), grid);
            button.Name = $"Answer{choice}";
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.CustomMinimumSize = new(124, 56);
            button.AddThemeFontSizeOverride("font_size", 30);
            button.Disabled = selected || rejected;
            if (selected) button.AddThemeStyleboxOverride("disabled", Style("cde6cb", 20));
            if (!button.Disabled) first ??= button;
        }
        first?.GrabFocus();
    }
}
