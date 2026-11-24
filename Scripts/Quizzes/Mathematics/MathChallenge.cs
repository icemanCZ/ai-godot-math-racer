using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MathRacer.Quizzes;

public enum MathOperation
{
    Addition,
    Subtraction,
}

/// <summary>
/// An arithmetic question: <see cref="Left"/> and <see cref="Right"/> joined by <see cref="Operation"/>, and with
/// <see cref="Third"/> one more number joined by <see cref="SecondOperation"/>. Saves of older versions have no second
/// operation, which then is an addition, as every three-number question of theirs was.
/// </summary>
public sealed record MathChallenge(
    int Left,
    int Right,
    MathOperation Operation,
    IReadOnlyList<int> Choices,
    int? Third = null,
    // Left out of a save when it is an addition, so a question of an older kind is written as it always was.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] MathOperation SecondOperation =
        MathOperation.Addition
) : IChallenge
{
    public int Answer => Evaluate(Left, Operation, Right, Third, SecondOperation);

    public bool Accepts(int choice) => choice == Answer;

    public string Expression =>
        Third is int third
            ? $"{Left} {Sign(Operation)} {Right} {Sign(SecondOperation)} {third} = ?"
            : $"{Left} {Sign(Operation)} {Right} = ?";

    internal static int Apply(int left, MathOperation operation, int right) =>
        operation == MathOperation.Addition ? left + right : left - right;

    internal static int Evaluate(
        int left,
        MathOperation operation,
        int right,
        int? third,
        MathOperation secondOperation
    )
    {
        int result = Apply(left, operation, right);
        return third is int value ? Apply(result, secondOperation, value) : result;
    }

    private static string Sign(MathOperation operation) => operation == MathOperation.Addition ? "+" : "−";
}
