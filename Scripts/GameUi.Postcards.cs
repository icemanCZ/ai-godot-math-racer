using Godot;
using System;

namespace MathRacer;

public partial class GameUi
{
    internal void PostcardAlbum(int stamps, (string Name, string Clue, string Icon, bool Found, bool Stamped)[] cards, Action<int> open)
    {
        Clear();
        Text("Pohlednice z Kouzelného údolí", 32);
        Text($"Razítka {stamps} / {cards.Length} · Mapka údolí za {Game.PostcardRewardStamps}", 20);
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 10);
        content.AddChild(grid);
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;
            var card = cards[i];
            var tile = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddChild(tile);
            tile.AddChild(new DeliveryPicture { Icon = card.Icon, CustomMinimumSize = new(0, 55) });
            if (card.Found)
            {
                var button = Button((card.Stamped ? "✓  " : "") + card.Name, () => open(index), tile);
                button.Name = $"Postcard{index}";
                button.CustomMinimumSize = new(0, 40);
                button.AddThemeFontSizeOverride("font_size", 19);
            }
            else
            {
                var label = new Label { Text = card.Clue, HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new(0, 40) };
                label.AddThemeFontSizeOverride("font_size", 18);
                tile.AddChild(label);
            }
        }

        Button("Pokračovat v jízdě", () => CloseInteractionRequested?.Invoke());
    }

    internal void PostcardDetail(string title, string letter, string[] pictures, PostcardProgress progress,
        int rejected, string feedback, Action<int> answer, Action album)
    {
        Clear();
        Text(title, 32);
        if (progress.Jpeg.Length > 0)
        {
            using var image = new Image();
            if (image.LoadJpgFromBuffer(progress.Jpeg) == Error.Ok)
                content.AddChild(new TextureRect
                {
                    Texture = ImageTexture.CreateFromImage(image), CustomMinimumSize = new(344, 194),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                });
        }
        if (progress.Stamped) Text(feedback == "" ? "✓" : $"✓  {feedback}", 22);
        if (!progress.Stamped)
        {
            Text(letter, 38);
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            content.AddChild(row);
            Button? firstChoice = null;
            for (int i = 0; i < pictures.Length; i++)
            {
                int index = i;
                var button = Button("", () => answer(index), row);
                button.Name = $"PostcardAnswer{index}";
                button.Disabled = (rejected & (1 << index)) != 0;
                button.CustomMinimumSize = new(102, 76);
                var picture = new DeliveryPicture { Icon = pictures[i] };
                button.AddChild(picture);
                picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                if (button.Disabled) picture.Modulate = new Color(1, 1, 1, .35f);
                else firstChoice ??= button;
            }
            firstChoice?.GrabFocus();
        }
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        content.AddChild(actions);
        Button("Album", album, actions).Name = "PostcardBack";
        Button(progress.Stamped ? "Pokračovat v jízdě" : "Odpovědět později",
            () => CloseInteractionRequested?.Invoke(), actions);
    }
}
