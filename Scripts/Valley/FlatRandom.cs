using System.Numerics;

namespace MathRacer.Valley;

/// <summary>
/// The numbers of Godot's <c>RandomNumberGenerator</c> (PCG32 with Godot's default stream) for a given seed, in plain
/// C#. Godot's generator is one call into the engine per number, and a grass tile asks for thousands of them, which made
/// every tile cost most of a millisecond and the car stutter when a ring of tiles came in. Each method gives exactly
/// what Godot gives, bit for bit, so the grass comes out the same; <c>unit-checks</c> compares them on random seeds.
/// </summary>
internal struct FlatRandom
{
    private const ulong Multiplier = 6364136223846793005UL;

    /// <summary>Godot's RandomPCG::DEFAULT_INC, the stream every RandomNumberGenerator uses.</summary>
    private const ulong Increment = (1442695040888963407UL << 1) | 1;

    private ulong state;

    /// <summary>What <c>new RandomNumberGenerator { Seed = seed }</c> starts from (pcg32_srandom_r).</summary>
    public FlatRandom(ulong seed)
    {
        state = 0;
        Next();
        state += seed;
        Next();
    }

    /// <summary><c>Randi()</c>: pcg32_random_r.</summary>
    public uint Next()
    {
        ulong old = state;
        state = old * Multiplier + Increment;
        uint shifted = (uint)(((old >> 18) ^ old) >> 27);
        int rotation = (int)(old >> 59);
        return BitOperations.RotateRight(shifted, rotation);
    }

    /// <summary><c>Randf()</c>: RandomPCG::randf, a binary fraction with its exponent counted from a second number.</summary>
    public float Randf()
    {
        uint exponentOffset = Next();
        if (exponentOffset == 0)
            return 0;
        return float.ScaleB(Next() | 0x80000001u, -32 - BitOperations.LeadingZeroCount(exponentOffset));
    }

    /// <summary><c>RandfRange(from, to)</c>: RandomPCG::random(float, float).</summary>
    public float RandfRange(float from, float to) => Randf() * (to - from) + from;
}
