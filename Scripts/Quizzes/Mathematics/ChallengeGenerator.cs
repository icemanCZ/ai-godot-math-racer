using System;
using System.Collections.Generic;

namespace MathRacer.Quizzes;

/// <summary>
/// Positive operands, results and partial results in 0..10 and exactly three distinct answers in 0..10. Three-number
/// questions mix addition and subtraction: one in four only adds.
/// </summary>
public sealed class ChallengeGenerator
{
    private const int Largest = 10;
    private readonly Random random;

    public ChallengeGenerator(int seed) => random = new Random(seed);

    public MathChallenge Next(bool threeTerms = false)
    {
        if (threeTerms)
        {
            var firstOperation = RandomOperation();
            var secondOperation = RandomOperation();
            if (firstOperation == MathOperation.Addition && secondOperation == MathOperation.Addition)
                return ThreeAdditions();
            while (true)
            {
                int answer = random.Next(0, Largest + 1);
                int first = random.Next(1, Largest + 1);
                int second = random.Next(1, Largest + 1);
                int middle = MathChallenge.Apply(first, firstOperation, second);
                if (middle is < 0 or > Largest)
                    continue;
                int third = secondOperation == MathOperation.Addition ? answer - middle : middle - answer;
                if (third is >= 1 and <= Largest)
                    return Create(first, second, firstOperation, third, secondOperation);
            }
        }
        var operation = RandomOperation();
        int left = operation == MathOperation.Addition ? random.Next(1, 10) : random.Next(2, 11);
        int right = operation == MathOperation.Addition ? random.Next(1, 11 - left) : random.Next(1, left + 1);
        return Create(left, right, operation);
    }

    private MathOperation RandomOperation() => random.Next(2) == 0 ? MathOperation.Addition : MathOperation.Subtraction;

    /// <summary>Three positive numbers whose sum is spread evenly from 3 to 10.</summary>
    private MathChallenge ThreeAdditions()
    {
        int sum = random.Next(3, Largest + 1);
        int firstCut = random.Next(1, sum);
        int secondCut = random.Next(1, sum - 1);
        if (secondCut >= firstCut)
            secondCut++;
        int first = Math.Min(firstCut, secondCut);
        int second = Math.Abs(firstCut - secondCut);
        int third = sum - first - second;
        return Create(first, second, MathOperation.Addition, third, MathOperation.Addition);
    }

    private MathChallenge Create(
        int left,
        int right,
        MathOperation operation,
        int? third = null,
        MathOperation secondOperation = MathOperation.Addition
    )
    {
        int answer = MathChallenge.Evaluate(left, operation, right, third, secondOperation);
        var choices = new List<int> { answer };
        // Nearby distractors are plausible without introducing negative or out-of-range choices.
        int[] offsets = { -2, -1, 1, 2 };
        Shuffle(offsets);
        foreach (int offset in offsets)
        {
            int candidate = answer + offset;
            if (candidate >= 0 && candidate <= Largest && !choices.Contains(candidate))
                choices.Add(candidate);
            if (choices.Count == 3)
                break;
        }
        var answers = choices.ToArray();
        Shuffle(answers);
        return new MathChallenge(left, right, operation, Array.AsReadOnly(answers), third, secondOperation);
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
