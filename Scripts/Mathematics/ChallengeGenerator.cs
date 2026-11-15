using System;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Positive operands, nonnegative results and exactly three distinct answers in 0..10.</summary>
public sealed class ChallengeGenerator
{
    private readonly Random random;
    public ChallengeGenerator(int seed) => random = new Random(seed);

    public MathChallenge Next(bool threeTerms = false)
    {
        if (threeTerms)
        {
            int first = random.Next(1, 9);
            int second = random.Next(1, 10 - first);
            int third = random.Next(1, 11 - first - second);
            return Create(first, second, MathOperation.Addition, third);
        }
        var operation = random.Next(2) == 0 ? MathOperation.Addition : MathOperation.Subtraction;
        int left = operation == MathOperation.Addition ? random.Next(1, 10) : random.Next(2, 11);
        int right = operation == MathOperation.Addition ? random.Next(1, 11 - left) : random.Next(1, left + 1);
        return Create(left, right, operation);
    }

    private MathChallenge Create(int left, int right, MathOperation operation, int? third = null)
    {
        int answer = operation == MathOperation.Addition ? left + right + (third ?? 0) : left - right;
        var choices = new List<int> { answer };
        // Nearby distractors are plausible without introducing negative or out-of-range choices.
        int[] offsets = { -2, -1, 1, 2 };
        Shuffle(offsets);
        foreach (int offset in offsets)
        {
            int candidate = answer + offset;
            if (candidate >= 0 && candidate <= 10 && !choices.Contains(candidate)) choices.Add(candidate);
            if (choices.Count == 3) break;
        }
        var answers = choices.ToArray();
        Shuffle(answers);
        return new MathChallenge(left, right, operation, Array.AsReadOnly(answers), third);
    }

    private void Shuffle(int[] values)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int other = random.Next(i + 1);
            (values[i], values[other]) = (values[other], values[i]);
        }
    }
}
