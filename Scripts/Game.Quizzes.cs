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
    All = Math | Letters | Syllables | Reading,
}

public partial class Game
{
    private readonly LetterChallengeGenerator letterChallenges = new(Settings.Seed + 205);
    private readonly SyllableChallengeGenerator syllableChallenges = new(Settings.Seed + 206);
    private readonly ReadingChallengeGenerator readingChallenges = new(Settings.Seed + 207);
    private static readonly QuizType[] QuizRotation =
    {
        QuizType.Math,
        QuizType.Syllables,
        QuizType.Letters,
        QuizType.Reading,
    };
    internal static int QuizRotationLength => QuizRotation.Length;
    private QuizType enabledQuizzes = QuizType.All;
    private bool threeTermMath;
    private int nextQuiz;
    private bool handoverQuiz;
    private SyllableSelection syllableSelection;

    private void ToggleQuizType(QuizType type)
    {
        if (State != GameState.Garage || enabledQuizzes == type)
            return;
        enabledQuizzes ^= type;
        ShowGarage();
        SaveProgress();
    }

    private void SetMathLevel(bool advanced)
    {
        if (State != GameState.Garage || threeTermMath == advanced)
            return;
        threeTermMath = advanced;
        ShowGarage();
        SaveProgress();
    }

    /// <summary>
    /// Opens the next enabled quiz type. With <paramref name="load"/>, an arithmetic turn becomes the
    /// trailer loading question for that material.
    /// </summary>
    private void BeginQuiz(bool handover = false, HaulLoad? load = null)
    {
        QuizType type;
        do
        {
            type = QuizRotation[nextQuiz];
            nextQuiz = (nextQuiz + 1) % QuizRotation.Length;
        } while (!enabledQuizzes.HasFlag(type));
        CurrentChallenge = type switch
        {
            QuizType.Math when load is HaulLoad cargo => LoadChallenge.Create(loadRandom, cargo),
            QuizType.Math => challenges.Next(threeTermMath),
            QuizType.Letters => letterChallenges.Next(),
            QuizType.Syllables => syllableChallenges.Next(),
            QuizType.Reading => readingChallenges.Next(),
            _ => throw new InvalidOperationException("At least one quiz type must be enabled."),
        };
        handoverQuiz = handover;
        syllableSelection = default;
        rejectedAnswers.Clear();
        State = GameState.Challenge;
        ShowChallenge();
        if (nearby != null && !InBanditQuiz)
            Speak(QuizInstruction(nearby, CurrentChallenge), nearby);
    }
}
