using System.Collections.Generic;

namespace MathRacer;

public enum MathOperation
{
    Addition,
    Subtraction,
}

public sealed record MathChallenge(
    int Left,
    int Right,
    MathOperation Operation,
    IReadOnlyList<int> Choices,
    int? Third = null
) : IChallenge
{
    public int Answer => Operation == MathOperation.Addition ? Left + Right + (Third ?? 0) : Left - Right;

    public bool Accepts(int choice) => choice == Answer;

    public string Expression =>
        Third is int third
            ? $"{Left} + {Right} + {third} = ?"
            : $"{Left} {(Operation == MathOperation.Addition ? "+" : "−")} {Right} = ?";
}
