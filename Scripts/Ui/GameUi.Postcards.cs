using System;
using Godot;
using MathRacer.App;
using MathRacer.Quizzes;
using MathRacer.Saves;

namespace MathRacer.Ui;

public partial class GameUi
{
    internal void PostcardAlbum(
        int stamps,
        (string Name, string Clue, string Icon, bool Found, bool Stamped)[] cards,
        Action<int> open
    )
    {
        Clear();
        Text(Texts.PohledniceZKouzelnehoUdoli, 32);
        Text($"✓  {stamps} / {cards.Length}", 22);
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
                var label = new Label
                {
                    Text = card.Clue,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CustomMinimumSize = new(0, 40),
                };
                label.AddThemeFontSizeOverride("font_size", 18);
                tile.AddChild(label);
            }
        }

        BackButton();
    }

    internal void PostcardDetail(
        string title,
        IChallenge? question,
        PostcardProgress progress,
        string feedback,
        Action<int> answer,
        Action album
    )
    {
        Clear();
        if (progress.Stamped)
            Text(title, 32);
        if (progress.Stamped && progress.Jpeg.Length > 0)
        {
            using var image = new Image();
            if (image.LoadJpgFromBuffer(progress.Jpeg) == Error.Ok)
                content.AddChild(
                    new TextureRect
                    {
                        Texture = ImageTexture.CreateFromImage(image),
                        CustomMinimumSize = new(344, 194),
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    }
                );
        }
        if (progress.Stamped)
            Text(feedback == "" ? "✓" : $"✓  {feedback}", 22);
        if (question is SyllableChallenge syllables)
        {
            content.AddChild(new DeliveryPicture { Icon = syllables.PictureId, CustomMinimumSize = new(0, 162) });
            SyllableTiles(syllables, progress.Selection, answer);
        }
        else if (question != null)
        {
            Text(question.Expression, 38);
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 10);
            content.AddChild(row);
            Button? firstChoice = null;
            foreach (int choice in question.Choices)
            {
                var button = Button(
                    question switch
                    {
                        IPictureChallenge => "",
                        ISignChallenge signs => signs.Label(choice),
                        _ => choice.ToString(),
                    },
                    () => answer(choice),
                    row,
                    narrate: false
                );
                button.Name = $"Answer{choice}";
                button.CustomMinimumSize = new(112, 76);
                if (question is IPictureChallenge pictures)
                {
                    string icon = pictures.Pictures[choice].Id;
                    Control picture =
                        question is ReadingChallenge
                            ? new WordPicture { PictureId = icon }
                            : new DeliveryPicture { Icon = icon };
                    picture.MouseFilter = Control.MouseFilterEnum.Ignore;
                    button.CustomMinimumSize = new(150, 122);
                    button.AddChild(picture);
                    picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                }
                firstChoice ??= button;
            }
            firstChoice?.GrabFocus();
            NarrateAnswerButtons(row, question);
        }
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        content.AddChild(actions);
        IconButton("album", album, "PostcardBack", actions).AccessibilityName = Texts.AlbumPohlednic;
        BackButton("LeavePostcard", actions);
    }
}
