using MathRacer.Quizzes;

namespace MathRacer.Saves;

/// <summary>A postcard that was taken: the picture, whether it has its stamp, and the syllables picked so far.</summary>
public sealed record PostcardProgress(byte[] Jpeg, bool Stamped, SyllableSelection Selection = default);
