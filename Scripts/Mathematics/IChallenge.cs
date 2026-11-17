using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MathRacer;

/// <summary>An open question. Saves store it with a "$type" tag naming the concrete question kind.</summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(MathChallenge), "math")]
[JsonDerivedType(typeof(LetterChallenge), "letters")]
[JsonDerivedType(typeof(SyllableChallenge), "syllables")]
[JsonDerivedType(typeof(ReadingChallenge), "reading")]
[JsonDerivedType(typeof(DeliveryChallenge), "delivery")]
[JsonDerivedType(typeof(LoadChallenge), "load")]
public interface IChallenge
{
    bool Accepts(int choice);
    IReadOnlyList<int> Choices { get; }
    string Expression { get; }
}
