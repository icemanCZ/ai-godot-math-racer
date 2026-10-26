using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

public sealed record PictureWord(string Id, string Word)
{
    public string Initial => Word[..1].ToUpperInvariant();
}

public sealed record LetterChallenge(string Letter, IReadOnlyList<PictureWord> Pictures) : IChallenge
{
    public int Answer => Pictures.ToList().FindIndex(picture => picture.Initial == Letter);
    public IReadOnlyList<int> Choices { get; } = Array.AsReadOnly(new[] { 0, 1, 2 });
    public string Expression => Letter;
}

public sealed class LetterChallengeGenerator
{
    public static IReadOnlyList<PictureWord> Words { get; } = Array.AsReadOnly(new[]
    {
        new PictureWord("car", "auto"), new PictureWord("banana", "banán"),
        new PictureWord("lemon", "citron"), new PictureWord("house", "dům"),
        new PictureWord("pear", "hruška"), new PictureWord("apple", "jablko"),
        new PictureWord("key", "klíč"), new PictureWord("boat", "loď"),
        new PictureWord("ball", "míč"), new PictureWord("fish", "ryba"),
        new PictureWord("sun", "slunce"), new PictureWord("snail", "šnek"),
        new PictureWord("butterfly", "motýl"), new PictureWord("cat", "kočka"),
        new PictureWord("dog", "pes"), new PictureWord("elephant", "slon"),
        new PictureWord("giraffe", "žirafa"), new PictureWord("zebra", "zebra"),
        new PictureWord("turtle", "želva"), new PictureWord("duck", "kachna"),
        new PictureWord("umbrella", "deštník"), new PictureWord("tree", "strom"),
        new PictureWord("flower", "květina"), new PictureWord("strawberry", "jahoda"),
        new PictureWord("carrot", "mrkev"), new PictureWord("icecream", "zmrzlina"),
        new PictureWord("clock", "hodiny"), new PictureWord("book", "kniha"),
        new PictureWord("pencil", "tužka"), new PictureWord("chocolate", "čokoláda"),
        new PictureWord("train", "vlak"), new PictureWord("rocket", "raketa")
    });
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
        var correct = remaining[index]; remaining.RemoveAt(index);
        var distractors = Words.Where(word => word.Initial != correct.Initial).ToArray();
        random.Shuffle(distractors);
        var pictures = new[] { correct, distractors[0], distractors[1] };
        random.Shuffle(pictures);
        return new LetterChallenge(correct.Initial, Array.AsReadOnly(pictures));
    }
}
