namespace MathRacer;

public static class Settings
{
    // Restore the saved world's seed; leave room for generator stream offsets.
    public static int Seed { get; private set; } = ChooseSeed();
    internal static void SetSeed(int seed) => Seed = seed;
    private static int ChooseSeed()
    {
        foreach (string argument in Godot.OS.GetCmdlineUserArgs())
            if (argument.StartsWith("--seed="))
            {
                if (int.TryParse(argument[7..], out int seed) && seed > 0 && seed < int.MaxValue - 4096) return seed;
                throw new System.ArgumentException("Seed must be between 1 and 2147479550.");
            }
        return SaveStore.Initial?.Seed ?? System.Random.Shared.Next(1, int.MaxValue - 4096);
    }
    public const float WorldRadius = 555f;
    public const float TerrainStep = 2.5f;
    public const float PlayableRadius = 525f;
    public const float MaxSpeed = 15f;
    public const float ReverseSpeed = 6f;
    public const float Acceleration = 7f;
    public const float Steering = 1.65f;
    public const float InteractionRange = 8f;
}

