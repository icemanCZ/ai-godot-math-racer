namespace MathRacer;

public static class Settings
{
    // --- Session state. Starting a new game reloads the scene, so these are the only values
    // --- that outlive one Game instance. Everything else is rebuilt from them.

    /// <summary>The world seed: an explicit <c>--seed</c>, the saved world's seed, or a random one.</summary>
    public static int Seed { get; private set; } = LaunchOptions.Seed ?? SaveStore.Initial?.Seed ?? NewSeed();

    /// <summary>Quiz settings carried from the previous game into the world created by "New game".</summary>
    internal static (QuizType Types, bool ThreeTerms)? PendingNewGameQuizzes { get; set; }

    internal static void SetSeed(int seed) => Seed = seed;

    /// <summary>A random seed that leaves room for generator stream offsets.</summary>
    internal static int NewSeed() => System.Random.Shared.Next(1, int.MaxValue - 4096);

    // --- Tuning constants.

    public const float WorldRadius = 555f;
    public const float TerrainStep = 2.5f;
    public const float PlayableRadius = 525f;
    public const float MaxSpeed = 15f;
    public const float ReverseSpeed = 6f;
    public const float Acceleration = 7f;
    public const float Steering = 1.65f;
    public const float InteractionRange = 8f;

    /// <summary>Top speed and acceleration factor while towing a full trailer.</summary>
    public const float LoadedTrailerFactor = .85f;
}
