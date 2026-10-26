using System.Collections.Generic;

namespace MathRacer;

public enum MathOperation { Addition, Subtraction }

public sealed record MathChallenge(int Left, int Right, MathOperation Operation, IReadOnlyList<int> Choices) : IChallenge
{
    public int Answer => Operation == MathOperation.Addition ? Left + Right : Left - Right;
    public string Expression => $"{Left} {(Operation == MathOperation.Addition ? "+" : "−")} {Right} = ?";
}
