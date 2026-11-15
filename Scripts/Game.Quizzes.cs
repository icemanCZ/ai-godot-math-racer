using System;

namespace MathRacer;

[Flags]
public enum QuizType
{
    None = 0,
    Math = 1,
    Letters = 2,
    Syllables = 4,
    Reading = 8,
    All = Math | Letters | Syllables | Reading
}

public partial class Game
{
    private readonly LetterChallengeGenerator letterChallenges = new(Settings.Seed + 205);
    private readonly SyllableChallengeGenerator syllableChallenges = new(Settings.Seed + 206);
    private readonly ReadingChallengeGenerator readingChallenges = new(Settings.Seed + 207);
    private static readonly QuizType[] WordQuizTypes = { QuizType.Syllables, QuizType.Letters, QuizType.Reading };
    private QuizType enabledQuizzes = QuizType.All;
    private bool threeTermMath;
    private int nextWordQuiz;
    private bool handoverQuiz;
    private SyllableSelection syllableSelection;

    private void ToggleQuizType(QuizType type)
    {
        if (State != GameState.Garage || enabledQuizzes == type) return;
        enabledQuizzes ^= type;
        ShowGarage();
        SaveProgress();
    }

    private void SetMathLevel(bool advanced)
    {
        if (State != GameState.Garage || threeTermMath == advanced) return;
        threeTermMath = advanced;
        ShowGarage();
        SaveProgress();
    }

    private QuizType SelectQuizType(bool preferMath, int start)
    {
        if (preferMath && enabledQuizzes.HasFlag(QuizType.Math)) return QuizType.Math;
        for (int i = 0; i < WordQuizTypes.Length; i++)
        {
            var type = WordQuizTypes[(start + i) % WordQuizTypes.Length];
            if (enabledQuizzes.HasFlag(type)) return type;
        }
        return QuizType.Math;
    }

    private void BeginQuiz(bool preferMath, bool handover = false)
    {
        var type = SelectQuizType(preferMath, nextWordQuiz);
        if (type != QuizType.Math) nextWordQuiz = (Array.IndexOf(WordQuizTypes, type) + 1) % WordQuizTypes.Length;
        CurrentChallenge = type switch
        {
            QuizType.Math => challenges.Next(threeTermMath),
            QuizType.Letters => letterChallenges.Next(),
            QuizType.Syllables => syllableChallenges.Next(),
            QuizType.Reading => readingChallenges.Next(),
            _ => throw new InvalidOperationException("At least one quiz type must be enabled.")
        };
        handoverQuiz = handover;
        syllableSelection = default;
        rejectedAnswers.Clear();
        State = GameState.Challenge;
        ShowChallenge();
    }
}
