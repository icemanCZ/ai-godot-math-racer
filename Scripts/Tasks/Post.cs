using System;
using System.Linq;

namespace MathRacer.Tasks;

/// <summary>
/// The post to the four corners of the valley (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 2), without any scene node:
/// how many letters each keeper of a border has had, the letter on its way to a keeper and the reply on its way back to a
/// house of the village. The postman gives out one letter at a time and only when nothing is being carried, so the post
/// never runs by itself. Each keeper sends a stamp for every letter, three of them for the album; letters go on after
/// the album is full, for coins alone. Saved as <c>SaveData.Post</c>.
/// </summary>
internal sealed class Post
{
    /// <summary>The keepers, one for each border, in the order of <c>Border</c>.</summary>
    public const int Keepers = 4;

    /// <summary>The stamps each keeper sends for the album.</summary>
    public const int StampsPerKeeper = 3;

    /// <summary>The most letters a save may claim for one keeper; beyond it the file is damaged.</summary>
    public const int Limit = 9999;

    /// <summary>The coins for a letter handed to a keeper, for a reply brought home and for the full album.</summary>
    public const int LetterCoins = 6,
        ReplyCoins = 4,
        AlbumCoins = 30;

    private readonly int[] delivered = new int[Keepers];

    /// <summary>The letters handed to each keeper, by border.</summary>
    public int[] Delivered => (int[])delivered.Clone();

    /// <summary>The keeper (by border) the letter being carried is for, or −1.</summary>
    public int Carrying { get; private set; } = -1;

    /// <summary>The house of the village the reply being carried is for, or −1.</summary>
    public int Reply { get; private set; } = -1;

    /// <summary>A letter or a reply is on its way.</summary>
    public bool Busy => Carrying >= 0 || Reply >= 0;

    /// <summary>The letters handed over, to every keeper together.</summary>
    public int Total => delivered.Sum();

    /// <summary>The stamps in the album from one keeper.</summary>
    public int Stamps(int keeper) => Math.Min(delivered[keeper], StampsPerKeeper);

    /// <summary>The stamps in the album from all the keepers.</summary>
    public int AlbumStamps => Enumerable.Range(0, Keepers).Sum(Stamps);

    public bool AlbumFull => AlbumStamps == Keepers * StampsPerKeeper;

    /// <summary>How many keepers have sent at least one stamp; the pigeon comes when all four have.</summary>
    public int KeepersWithStamps => delivered.Count(count => count > 0);

    public void Restore(int[] counts, int carrying, int reply)
    {
        for (int i = 0; i < Keepers; i++)
            delivered[i] = Math.Clamp(i < counts.Length ? counts[i] : 0, 0, Limit);
        Carrying = carrying is >= 0 and < Keepers ? carrying : -1;
        Reply = Carrying < 0 && reply >= 0 ? reply : -1;
    }

    /// <summary>
    /// The keeper the next letter is for: among the keepers who live in the valley (<paramref name="present"/>), the one
    /// with the fewest letters, so every border is visited before any is visited again; equal ones take turns from a place
    /// that follows from the seed and the letters handed over. −1 when no keeper lives in the valley.
    /// </summary>
    public int NextAddressee(int seed, Func<int, bool> present)
    {
        int start = (int)((uint)(seed + Total) % Keepers);
        int best = -1;
        for (int i = 0; i < Keepers; i++)
        {
            int keeper = (start + i) % Keepers;
            if (present(keeper) && (best < 0 || delivered[keeper] < delivered[best]))
                best = keeper;
        }
        return best;
    }

    /// <summary>The postman hands over a letter for <paramref name="keeper"/>; false while something is carried.</summary>
    public bool Take(int keeper)
    {
        if (Busy || keeper is < 0 or >= Keepers)
            return false;
        Carrying = keeper;
        return true;
    }

    /// <summary>
    /// The letter is handed to its keeper, who gives a reply for <paramref name="house"/> (−1 for none). Returns whether
    /// the letter brought a new stamp into the album.
    /// </summary>
    public bool Deliver(int house)
    {
        if (Carrying < 0)
            return false;
        bool stamp = delivered[Carrying] < StampsPerKeeper;
        delivered[Carrying] = Math.Min(delivered[Carrying] + 1, Limit);
        Carrying = -1;
        Reply = house;
        return stamp;
    }

    /// <summary>The reply is brought to its house.</summary>
    public bool BringReply()
    {
        if (Reply < 0)
            return false;
        Reply = -1;
        return true;
    }
}
