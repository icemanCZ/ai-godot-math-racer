using System;

namespace MathRacer.Quizzes;

/// <summary>The kinds of question the game asks; a save and the garage switch them on and off.</summary>
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
