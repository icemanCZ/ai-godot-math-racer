using System.Collections.Generic;

namespace MathRacer;

public interface IChallenge
{
    bool Accepts(int choice);
    IReadOnlyList<int> Choices { get; }
    string Expression { get; }
}
