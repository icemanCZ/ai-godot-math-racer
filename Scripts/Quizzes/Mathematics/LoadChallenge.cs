using System;
using System.Collections.Generic;
using System.Linq;
using MathRacer.Buildings;

namespace MathRacer.Quizzes;

/// <summary>
/// The loading question: the trailer holds <see cref="Capacity"/> pieces and already carries
/// <see cref="Present"/>. The answer is how many still fit.
/// </summary>
public sealed record LoadChallenge(HaulLoad Cargo, int Present, int[] Options) : IChallenge
{
    public const int Capacity = 10;

    public int Answer => Capacity - Present;
    public IReadOnlyList<int> Choices => Options;
    public string Expression => "";

    public bool Accepts(int choice) => choice == Answer;

    internal bool IsValid() =>
        Enum.IsDefined(Cargo)
        && Present is >= 1 and < Capacity
        && Options is { Length: 3 }
        && Options.Distinct().Count() == 3
        && Options.All(option => option is >= 0 and <= Capacity)
        && Options.Contains(Answer);

    /// <summary>Picks a seeded number of pieces already loaded and two nearby wrong answers.</summary>
    internal static LoadChallenge Create(Random random, HaulLoad cargo)
    {
        int present = random.Next(1, Capacity);
        int answer = Capacity - present;
        var wrong = new[] { answer - 2, answer - 1, answer + 1, answer + 2 }
            .Where(option => option is >= 0 and <= Capacity)
            .OrderBy(_ => random.Next())
            .Take(2);
        var options = wrong.Append(answer).OrderBy(_ => random.Next()).ToArray();
        return new LoadChallenge(cargo, present, options);
    }
}
