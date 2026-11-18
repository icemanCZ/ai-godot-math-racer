using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;

namespace MathRacer.Quizzes;

public sealed record SyllableChallenge(string PictureId, IReadOnlyList<string> Syllables, IReadOnlyList<string> Tiles)
    : IChallenge
{
    internal const int TileCount = 6;
    private static readonly IReadOnlyList<int> TileIndices = Array.AsReadOnly(Enumerable.Range(0, TileCount).ToArray());

    [JsonIgnore]
    public IReadOnlyList<int> Choices => TileIndices;

    [JsonIgnore]
    public string Expression => string.Concat(Syllables);

    public bool Accepts(int choice) => Syllables.Contains(Tiles[choice]);

    internal int CorrectMask => Choices.Where(Accepts).Aggregate(0, (mask, choice) => mask | (1 << choice));

    internal bool IsValid() =>
        Syllables is { Count: > 0 and < TileCount }
        && Tiles?.Count == TileCount
        && Tiles.All(tile => !string.IsNullOrWhiteSpace(tile))
        && Tiles.Where(Syllables.Contains).Order().SequenceEqual(Syllables.Order());
}

public readonly record struct SyllableSelection(int Selected, int Rejected)
{
    [JsonIgnore]
    internal int Count => BitOperations.PopCount((uint)Selected);

    internal bool IsSelected(int choice) => (Selected & (1 << choice)) != 0;

    internal bool IsRejected(int choice) => (Rejected & (1 << choice)) != 0;

    internal SyllableSelection Pick(SyllableChallenge question, int choice)
    {
        if (!question.Choices.Contains(choice) || IsSelected(choice) || IsRejected(choice))
            return this;
        return question.Accepts(choice)
            ? this with
            {
                Selected = Selected | (1 << choice),
            }
            : this with
            {
                Rejected = Rejected | (1 << choice),
            };
    }

    internal bool IsComplete(SyllableChallenge question) => Selected == question.CorrectMask;

    internal bool IsValid(SyllableChallenge question) =>
        (Selected & ~question.CorrectMask) == 0
        && (Rejected & ~((1 << SyllableChallenge.TileCount) - 1 & ~question.CorrectMask)) == 0;
}

internal sealed record SyllableWord(string Id, string Segmented)
{
    internal string Word => Segmented.Replace("-", "");
}

internal sealed class SyllableChallengeGenerator(int seed)
{
    internal static IReadOnlyList<SyllableWord> Words { get; } =
        Array.AsReadOnly(
            new SyllableWord[]
            {
                new("car", "au-to"),
                new("banana", "ba-nán"),
                new("lemon", "ci-tron"),
                new("house", "dům"),
                new("pear", "hruš-ka"),
                new("apple", "ja-bl-ko"),
                new("key", "klíč"),
                new("boat", "loď"),
                new("ball", "míč"),
                new("fish", "ry-ba"),
                new("sun", "slun-ce"),
                new("snail", "šnek"),
                new("butterfly", "mo-týl"),
                new("cat", "koč-ka"),
                new("dog", "pes"),
                new("elephant", "slon"),
                new("giraffe", "ži-ra-fa"),
                new("zebra", "ze-bra"),
                new("turtle", "žel-va"),
                new("duck", "kach-na"),
                new("umbrella", "dešt-ník"),
                new("tree", "strom"),
                new("flower", "kvě-ti-na"),
                new("strawberry", "ja-ho-da"),
                new("carrot", "mr-kev"),
                new("icecream", "zmrz-li-na"),
                new("clock", "ho-di-ny"),
                new("book", "kni-ha"),
                new("pencil", "tuž-ka"),
                new("chocolate", "čo-ko-lá-da"),
                new("train", "vlak"),
                new("rocket", "ra-ke-ta"),
            }
        );
    private static readonly string[] TilePool = Words
        .SelectMany(word => word.Segmented.ToUpperInvariant().Split('-'))
        .Distinct()
        .ToArray();
    private readonly Random random = new(seed);
    private readonly List<SyllableWord> remaining = new();

    internal SyllableChallenge Next()
    {
        if (remaining.Count == 0)
            remaining.AddRange(Words);
        int index = random.Next(remaining.Count);
        var word = remaining[index];
        remaining.RemoveAt(index);
        return Create(word.Id, word.Segmented, random);
    }

    internal static SyllableChallenge Create(string pictureId, string segmented, Random random)
    {
        var syllables = segmented.ToUpperInvariant().Split('-');
        var distractors = TilePool.Where(tile => !syllables.Contains(tile)).ToArray();
        random.Shuffle(distractors);
        var tiles = syllables.Concat(distractors.Take(SyllableChallenge.TileCount - syllables.Length)).ToArray();
        random.Shuffle(tiles);
        return new(pictureId, Array.AsReadOnly(syllables), Array.AsReadOnly(tiles));
    }
}
