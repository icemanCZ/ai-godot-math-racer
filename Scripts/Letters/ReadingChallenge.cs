using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MathRacer;

public sealed record ReadingChallenge(string Word, IReadOnlyList<PictureWord> Pictures) : IPictureChallenge
{
    private static readonly IReadOnlyList<int> PictureIndices = Array.AsReadOnly(new[] { 0, 1, 2 });
    [JsonIgnore] public IReadOnlyList<int> Choices => PictureIndices;
    [JsonIgnore] public string Expression => Word;
    public bool Accepts(int choice) => Pictures[choice].Word.ToUpperInvariant() == Word;

    internal bool IsValid() => Word is { Length: 3 } && Word == Word.ToUpperInvariant() &&
        Pictures?.Count == 3 && Pictures.All(ReadingChallengeGenerator.Words.Contains) &&
        Pictures.Distinct().Count() == 3 && Choices.Count(Accepts) == 1;
}

internal sealed class ReadingChallengeGenerator(int seed)
{
    internal static IReadOnlyList<PictureWord> Words { get; } = Array.AsReadOnly(new PictureWord[]
    {
        new("forest", "les"), new("dog", "pes"), new("house", "dům"), new("ball", "míč"),
        new("boat", "loď"), new("cheese", "sýr"), new("nose", "nos"), new("mouse", "myš"),
        new("honeyJar", "med"), new("tea", "čaj")
    });
    private readonly Random random = new(seed);
    private readonly List<PictureWord> remaining = new();

    internal ReadingChallenge Next()
    {
        if (remaining.Count == 0) remaining.AddRange(Words);
        int index = random.Next(remaining.Count);
        var word = remaining[index];
        remaining.RemoveAt(index);
        var distractors = Words.Where(picture => picture != word).ToArray();
        random.Shuffle(distractors);
        var choices = new[] { word, distractors[0], distractors[1] };
        random.Shuffle(choices);
        return new(word.Word.ToUpperInvariant(), Array.AsReadOnly(choices));
    }
}
