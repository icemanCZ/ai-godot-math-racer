using System.Collections.Generic;

namespace MathRacer;

public interface IChallenge
{
    int Answer { get; }
    IReadOnlyList<int> Choices { get; }
    string Expression { get; }
}
