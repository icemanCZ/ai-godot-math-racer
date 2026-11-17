using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public sealed record PictureWord(string Id, string Word)
{
    public string Initial => Word[..1].ToUpperInvariant();
}

public interface IPictureChallenge : IChallenge
{
    IReadOnlyList<PictureWord> Pictures { get; }
}

public sealed record LetterChallenge(string Letter, IReadOnlyList<PictureWord> Pictures) : IPictureChallenge
{
    public int Answer => Pictures.ToList().FindIndex(picture => picture.Initial == Letter);
    public IReadOnlyList<int> Choices { get; } = Array.AsReadOnly(new[] { 0, 1, 2 });
    public string Expression => Letter;

    public bool Accepts(int choice) => choice == Answer;
}

public sealed class LetterChallengeGenerator
{
    public static IReadOnlyList<PictureWord> Words { get; } =
        Array.AsReadOnly(
            SyllableChallengeGenerator.Words.Select(word => new PictureWord(word.Id, word.Word)).ToArray()
        );
    private readonly Random random;
    private readonly List<PictureWord> remaining = new();

    public LetterChallengeGenerator(int seed) => random = new Random(seed);

    public LetterChallenge Next()
    {
        if (remaining.Count == 0)
        {
            remaining.AddRange(Words);
        }
        int index = random.Next(remaining.Count);
        var correct = remaining[index];
        remaining.RemoveAt(index);
        var distractors = Words.Where(word => word.Initial != correct.Initial).ToArray();
        random.Shuffle(distractors);
        var pictures = new[] { correct, distractors[0], distractors[1] };
        random.Shuffle(pictures);
        return new LetterChallenge(correct.Initial, Array.AsReadOnly(pictures));
    }
}
