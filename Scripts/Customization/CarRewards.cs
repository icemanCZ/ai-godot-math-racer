using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer;

/// <summary>The garage's earned car items and the choice of rewards currently on offer.</summary>
internal sealed class CarRewards
{
    private readonly Random random = new();

    /// <summary>Earned items; the starting paint and the classic car are always owned.</summary>
    public HashSet<CarReward> Owned { get; } = new() { new(CarLook.All[0].Id, false), new("classic", false, true) };

    /// <summary>The rewards the player may pick from right now, empty outside a reward panel.</summary>
    public IReadOnlyList<CarReward> Offered { get; private set; } = Array.Empty<CarReward>();

    /// <summary>The last offer, so that a new random offer never repeats it exactly.</summary>
    public IReadOnlyList<CarReward> Previous { get; private set; } = Array.Empty<CarReward>();

    public bool IsOffered(CarReward reward) => Offered.Contains(reward);

    public void ClearOffer() => Offered = Array.Empty<CarReward>();

    /// <summary>A paint, a vehicle and two distinct accessories for a finished roadside delivery.</summary>
    public void OfferRandom() => Offer(CarReward.Draw(random, Previous));

    /// <summary>One accessory, paint and vehicle for a shop order, preferring items not yet owned.</summary>
    public void OfferOnePerCategory() =>
        Offer(
            new[]
            {
                Choose(CarAccessory.All.Select(a => new CarReward(a.Id, true))),
                Choose(CarLook.All.Select(l => new CarReward(l.Id, false))),
                Choose(CarBody.All.Select(b => new CarReward(b.Id, false, true))),
            }
        );

    /// <summary>Takes an offered reward into the garage. Returns false when it was not on offer.</summary>
    public bool Claim(CarReward reward)
    {
        if (!IsOffered(reward))
            return false;
        Owned.Add(reward);
        return true;
    }

    public void Restore(
        IEnumerable<CarReward> owned,
        IReadOnlyList<CarReward> offered,
        IReadOnlyList<CarReward> previous
    )
    {
        Owned.Clear();
        Owned.UnionWith(owned);
        Offered = offered;
        Previous = previous;
    }

    private void Offer(IReadOnlyList<CarReward> rewards)
    {
        Offered = rewards;
        Previous = rewards;
    }

    private CarReward Choose(IEnumerable<CarReward> options)
    {
        var all = options.ToArray();
        var missing = all.Where(r => !Owned.Contains(r)).ToArray();
        var pool = missing.Length > 0 ? missing : all;
        return pool[random.Next(pool.Length)];
    }
}
