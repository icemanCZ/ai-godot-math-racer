using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MathRacer.App;

namespace MathRacer.Quizzes;

/// <summary>A question whose answers are signs, not numbers: a button shows <see cref="Label"/> and reads <see cref="Spoken"/>.</summary>
public interface ISignChallenge : IChallenge
{
    string Label(int choice);
    string Spoken(int choice);
}

/// <summary>
/// Comparing two numbers from 0 to 10, as children do in the first class: is the left one smaller, the same or
/// larger? The choices are the signs &lt;, = and &gt;.
/// </summary>
public sealed record CompareChallenge(int Left, int Right) : ISignChallenge
{
    public const int Largest = 10;
    public const int Smaller = 0;
    public const int Same = 1;
    public const int Larger = 2;

    private static readonly IReadOnlyList<int> SignIndices = Array.AsReadOnly(new[] { Smaller, Same, Larger });
    private static readonly string[] Signs = { "<", "=", ">" };
    private static readonly string[] SpokenSigns = { Texts.JeMensiNez, Texts.SeRovna, Texts.JeVetsiNez };

    [JsonIgnore]
    public IReadOnlyList<int> Choices => SignIndices;

    [JsonIgnore]
    public string Expression => $"{Left}   ?   {Right}";

    [JsonIgnore]
    public int Answer => Math.Sign(Left - Right) + 1;

    public bool Accepts(int choice) => choice == Answer;

    public string Label(int choice) => Signs[choice];

    /// <summary>The whole claim of a sign, such as "4 je menší než 7", which says nothing of whether it is true.</summary>
    public string Spoken(int choice) => $"{Left} {SpokenSigns[choice]} {Right}";

    internal bool IsValid() => Left is >= 0 and <= Largest && Right is >= 0 and <= Largest;
}

internal sealed class CompareChallengeGenerator(int seed)
{
    private readonly Random random = new(seed);

    internal CompareChallenge Next()
    {
        int left = random.Next(0, CompareChallenge.Largest + 1);
        // One question in five has two equal numbers, so the sign "=" is learnt too.
        if (random.Next(5) == 0)
            return new(left, left);
        int right = random.Next(0, CompareChallenge.Largest);
        if (right >= left)
            right++;
        return new(left, right);
    }
}
