namespace MathRacer;

public static class Settings
{
    // One seed per run, shared by all generators; leave room for their stream offsets.
    public static readonly int Seed = System.Random.Shared.Next(1, int.MaxValue - 4096);
    public const float WorldRadius = 555f;
    public const float TerrainStep = 2.5f;
    public const float PlayableRadius = 525f;
    public const float MaxSpeed = 15f;
    public const float ReverseSpeed = 6f;
    public const float Acceleration = 7f;
    public const float Steering = 1.65f;
    public const float InteractionRange = 8f;
}

