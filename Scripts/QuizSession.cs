using System;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>
/// The questions the game asks: which kinds are switched on, which kind comes next, the generators (each with its
/// own seeded stream, so a valley asks the same questions every time) and the question that is open with the
/// wrong answers tried so far. It holds no scene nodes: <see cref="Game"/> shows the question and speaks.
/// </summary>
internal sealed class QuizSession
{
    private static readonly QuizType[] Rotation =
    {
        QuizType.Math,
        QuizType.Syllables,
        QuizType.Letters,
        QuizType.Reading,
    };

    public static int RotationLength => Rotation.Length;

    private readonly ChallengeGenerator math = new(Settings.Seed + 204);
    private readonly LetterChallengeGenerator letters = new(Settings.Seed + 205);
    private readonly SyllableChallengeGenerator syllables = new(Settings.Seed + 206);
    private readonly ReadingChallengeGenerator reading = new(Settings.Seed + 207);
    private readonly Random loadRandom = new(Settings.Seed + 2313);

    public QuizType Enabled { get; set; } = QuizType.All;
    public bool ThreeTermMath { get; set; }

    /// <summary>Position in the rotation of the kind that comes next.</summary>
    public int NextQuiz { get; set; }

    /// <summary>The open question, if any.</summary>
    public IChallenge? Current { get; set; }

    /// <summary>The wrong answers already tried for the open question.</summary>
    public HashSet<int> Rejected { get; } = new();

    public SyllableSelection Selection { get; set; }

    /// <summary>The open question is the word quiz that replaces cargo counting at a handover.</summary>
    public bool Handover { get; set; }

    /// <summary>The trip that starts once the open question is solved.</summary>
    public Game.Departure Departure { get; set; }

    /// <summary>The panel that offered the trip, for the kinds of trip that return to one.</summary>
    public GameState DepartureState { get; set; }

    /// <summary>
    /// The next question of the next enabled kind. With <paramref name="load"/>, an arithmetic turn becomes the
    /// trailer loading question for that material.
    /// </summary>
    public IChallenge Next(HaulLoad? load)
    {
        QuizType type;
        do
        {
            type = Rotation[NextQuiz];
            NextQuiz = (NextQuiz + 1) % Rotation.Length;
        } while (!Enabled.HasFlag(type));
        return type switch
        {
            QuizType.Math when load is HaulLoad cargo => LoadChallenge.Create(loadRandom, cargo),
            QuizType.Math => math.Next(ThreeTermMath),
            QuizType.Letters => letters.Next(),
            QuizType.Syllables => syllables.Next(),
            QuizType.Reading => reading.Next(),
            _ => throw new InvalidOperationException("At least one quiz type must be enabled."),
        };
    }

    /// <summary>Forgets the open question together with the wrong answers already tried and the trip that waited for it.</summary>
    public void Clear()
    {
        Departure = Game.Departure.None;
        Current = null;
        Rejected.Clear();
    }
}
