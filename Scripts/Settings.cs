namespace MathRacer;

public static class Settings
{
    // One seed per run, shared by all generators; leave room for their stream offsets.
    public static readonly int Seed = ChooseSeed();
    private static int ChooseSeed()
    {
        foreach (string argument in Godot.OS.GetCmdlineUserArgs())
            if (argument.StartsWith("--seed="))
            {
                if (int.TryParse(argument[7..], out int seed) && seed > 0 && seed < int.MaxValue - 4096) return seed;
                throw new System.ArgumentException("Seed must be between 1 and 2147479550.");
            }
        return System.Random.Shared.Next(1, int.MaxValue - 4096);
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

