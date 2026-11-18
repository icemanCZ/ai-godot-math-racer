using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Quizzes;
using MathRacer.Tasks;

namespace MathRacer.Ui;

/// <summary>The question panels: an ordinary question, a picture question, a delivery question.</summary>
public partial class GameUi
{
    public void Challenge(
        IChallenge question,
        System.Collections.Generic.IReadOnlySet<int> rejected,
        SyllableSelection selection,
        int banditStage
    )
    {
        if (question is DeliveryChallenge delivery)
        {
            DeliveryQuestion(delivery, rejected);
            return;
        }
        if (question is LoadChallenge load)
        {
            LoadQuestion(load, rejected);
            return;
        }
        Clear();
        if (banditStage > 0)
            Text($"?  {banditStage} / {Game.BanditQuestionCount}", 27);
        if (question is SyllableChallenge syllables)
        {
            content.AddChild(
                new WordPicture
                {
                    PictureId = syllables.PictureId,
                    CustomMinimumSize = new(0, 155),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                }
            );
            SyllableTiles(syllables, selection, answer => Send(new UiIntent.Answer(answer)));
            BackButton("LeaveChallenge");
            return;
        }
        var pictures = question as IPictureChallenge;
        Text(question.Expression, pictures != null ? 78 : 54);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        content.AddChild(row);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            int answer = choice;
            bool wrong = rejected.Contains(answer);
            var button = Button(
                pictures != null ? ""
                    : wrong ? $"× {answer}"
                    : answer.ToString(),
                () => Send(new UiIntent.Answer(answer)),
                row,
                narrate: false
            );
            button.Name = $"Answer{answer}";
            button.CustomMinimumSize = pictures != null ? new Vector2(220, 180) : new Vector2(140, 84);
            button.AddThemeFontSizeOverride("font_size", 40);
            button.Disabled = rejected.Contains(answer);
            if (pictures != null)
            {
                button.AddThemeStyleboxOverride("normal", Style("ffffff", 20));
                button.AddThemeStyleboxOverride("hover", Style("e2f3e7", 20));
                button.AddThemeStyleboxOverride("pressed", Style("c4e4d3", 20));
                button.AddThemeStyleboxOverride(
                    "focus",
                    new StyleBoxFlat
                    {
                        BgColor = Colors.Transparent,
                        BorderColor = new Color("edb941"),
                        BorderWidthLeft = 4,
                        BorderWidthRight = 4,
                        BorderWidthTop = 4,
                        BorderWidthBottom = 4,
                        CornerRadiusTopLeft = 20,
                        CornerRadiusTopRight = 20,
                        CornerRadiusBottomLeft = 20,
                        CornerRadiusBottomRight = 20,
                    }
                );
                var word = pictures.Pictures[answer];
                button.AccessibilityName = word.Word;
                var picture = new WordPicture
                {
                    PictureId = word.Id,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Modulate = button.Disabled ? new Color(1, 1, 1, .3f) : Colors.White,
                };
                button.AddChild(picture);
                picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            }
            if (!button.Disabled)
                first ??= button;
        }
        BackButton("LeaveChallenge");
        first?.GrabFocus();
        NarrateAnswerButtons(row, question);
    }

    private void NarrateAnswerButtons(Container row, IChallenge question)
    {
        for (int i = 0; i < question.Choices.Count; i++)
        {
            var button = row.GetChild<Button>(i);
            if (button.Disabled)
                continue;
            int choice = question.Choices[i];
            string label = question is IPictureChallenge pictures ? pictures.Pictures[choice].Word : choice.ToString();
            NarrateHighlight(button, () => label);
        }
    }

    private void DeliveryQuestion(DeliveryChallenge question, System.Collections.Generic.IReadOnlySet<int> rejected)
    {
        Clear();
        Picture(content, question.Cargo, null, question.Answer);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        content.AddChild(row);
        Button? first = null;
        foreach (int choice in question.Choices)
        {
            var button = Button(
                rejected.Contains(choice) ? $"× {choice}" : choice.ToString(),
                () => Send(new UiIntent.Answer(choice)),
                row,
                narrate: false
            );
            button.Name = $"Answer{choice}";
            button.CustomMinimumSize = new(180, 130);
            button.Disabled = rejected.Contains(choice);
            button.AddThemeFontSizeOverride("font_size", 40);
            if (!button.Disabled)
                first ??= button;
        }
        BackButton("LeaveChallenge");
        first?.GrabFocus();
        NarrateAnswerButtons(row, question);
    }
}
