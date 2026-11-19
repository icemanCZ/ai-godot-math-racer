using System;
using System.Linq;

namespace MathRacer.Valley;

/// <summary>
/// The witch's firefly hunt on a starry night, without any scene node. The witch lends the child a lantern; every firefly
/// that is caught makes it shine brighter, and every <see cref="PerStar"/> of them light one more star of the collection, the
/// stars of the <see cref="Constellation"/> pictures in order, so that one picture after another is drawn in the sky. Only the
/// number caught is saved; the lantern and the stars follow from it, and a hunt (<see cref="Active"/>) lasts as long as the
/// night that was asked for.
/// </summary>
internal sealed class FireflyHunt
{
    /// <summary>Fireflies that light one star.</summary>
    public const int PerStar = 10;

    /// <summary>Every star of every picture of the collection.</summary>
    public static int TotalStars => Constellation.All.Sum(picture => picture.Stars.Length);

    /// <summary>The most fireflies that a save may claim, and the number at which the collection is complete.</summary>
    public static int Limit => TotalStars * PerStar;

    public int Caught { get; private set; }

    /// <summary>The lantern is lit and fireflies are about; ends with the night and is never saved.</summary>
    public bool Active { get; private set; }

    public int Stars => Caught / PerStar;

    public bool Complete => Caught >= Limit;

    /// <summary>Fireflies in the lantern for the next star, 0 to <see cref="PerStar"/> - 1 (the full ten once complete).</summary>
    public int TowardsStar => Complete ? PerStar : Caught % PerStar;

    /// <summary>How brightly the lantern shines: dim after a star was lit, full just before the next.</summary>
    public float Lantern => TowardsStar / (float)PerStar;

    /// <summary>Takes the number caught from a save; no hunt is on after a load.</summary>
    public void Restore(int caught)
    {
        Caught = Math.Clamp(caught, 0, Limit);
        Active = false;
    }

    /// <summary>Lights the lantern; false while it is lit already or when every star shines.</summary>
    public bool Start()
    {
        if (Active || Complete)
            return false;
        Active = true;
        return true;
    }

    public void Stop() => Active = false;

    /// <summary>
    /// Puts one firefly into the lantern. Returns true when that lights a new star, and false when it does not or when no
    /// hunt is on.
    /// </summary>
    public bool Catch()
    {
        if (!Active || Complete)
            return false;
        Caught++;
        return Caught % PerStar == 0;
    }

    /// <summary>The picture and the star of it that star number <paramref name="star"/> (0 is the first) lights.</summary>
    public static (int Picture, int Star) StarAt(int star)
    {
        for (int picture = 0; picture < Constellation.All.Count; picture++)
        {
            int size = Constellation.All[picture].Stars.Length;
            if (star < size)
                return (picture, star);
            star -= size;
        }
        throw new ArgumentOutOfRangeException(nameof(star));
    }

    /// <summary>How many stars of each picture shine now.</summary>
    public int[] LitStars()
    {
        int left = Stars;
        return Constellation
            .All.Select(picture =>
            {
                int lit = Math.Min(left, picture.Stars.Length);
                left -= lit;
                return lit;
            })
            .ToArray();
    }

    /// <summary>How many pictures are drawn to the end.</summary>
    public int Finished => LitStars().Where((lit, i) => lit == Constellation.All[i].Stars.Length).Count();
}
