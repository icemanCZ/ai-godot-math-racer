using System;

namespace MathRacer.Valley;

/// <summary>How warm the detector feels: the farther the treasure, the colder.</summary>
internal enum TreasureWarmth
{
    Freezing,
    Cold,
    Warm,
    Hot,
    Burning,
    Here,
}

/// <summary>
/// The treasure hunt of the pirate captain, without any scene node: how many treasures were found and whether one is being
/// looked for. A hunt begins only when the player asks the captain for it, and ends with the chest, so after every find the
/// player has to drive back to her for the next one. Where the next treasure lies follows from the valley's seed and the
/// number found (<c>World.TreasureSpot</c>), so only these two numbers are saved.
/// </summary>
internal sealed class TreasureHunt
{
    /// <summary>The most treasures that a save may claim; beyond it the file is damaged.</summary>
    public const int Limit = 9999;

    /// <summary>How close the car must be to dig.</summary>
    public const float DigRadius = 4.5f;

    /// <summary>Every this many treasures, the chest holds twice as many coins.</summary>
    public const int BigEvery = 5;

    public int Found { get; private set; }

    /// <summary>A treasure has been given out and is not found yet.</summary>
    public bool Active { get; private set; }

    public void Restore(int found, bool active)
    {
        Found = Math.Clamp(found, 0, Limit);
        Active = active;
    }

    /// <summary>Starts looking for the next treasure; false while one is already being looked for.</summary>
    public bool Begin()
    {
        if (Active)
            return false;
        Active = true;
        return true;
    }

    /// <summary>The treasure is dug up: the hunt ends and the next one waits for the captain.</summary>
    public void Finish()
    {
        if (!Active)
            return;
        Active = false;
        Found = Math.Min(Found + 1, Limit);
    }

    /// <summary>The coins in the chest of the treasure with this number (0 is the first).</summary>
    public static int Coins(int index) => (index + 1) % BigEvery == 0 ? 40 : 20;

    /// <summary>How many treasures are left until the next big chest.</summary>
    public static int UntilBig(int found) => BigEvery - found % BigEvery;

    public static TreasureWarmth WarmthAt(float distance) =>
        distance <= DigRadius ? TreasureWarmth.Here
        : distance <= 14 ? TreasureWarmth.Burning
        : distance <= 35 ? TreasureWarmth.Hot
        : distance <= 80 ? TreasureWarmth.Warm
        : distance <= 150 ? TreasureWarmth.Cold
        : TreasureWarmth.Freezing;

    /// <summary>0 far away, 1 on the spot.</summary>
    public static float Closeness(float distance) => Math.Clamp(1 - (distance - DigRadius) / 200f, 0, 1);

    /// <summary>Seconds between two beeps of the detector; the closer, the faster.</summary>
    public static float BeepInterval(float distance) => Math.Clamp(.22f + (distance - DigRadius) / 90f, .22f, 2.6f);
}
