using System;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Buildings;

/// <summary>
/// The finished windmill as the second link of the bread chain: the farmer's wheat is tipped on the mill's ring, the
/// mill grinds it into sacks of flour that stand beside the ring, and the miller loads the flour for the bakery. Holds
/// the stock (saved as <c>BuildingsSave.FlourStock</c>) and is the target of the wheat trip (<see cref="TripKinds.Mill"/>);
/// the sacks themselves are drawn by the <see cref="Buildings.Mill"/>. A valley has one only where it has both the
/// mill and the bakery.
/// </summary>
internal sealed class FlourMill : ITripTarget
{
    public const int Capacity = Works.Capacity;

    /// <summary>Sacks of flour one load of wheat makes.</summary>
    public const int PerLoad = Works.PerLoad;

    /// <summary>Sacks of flour one trip to the bakery takes.</summary>
    public const int PerTrip = Works.PerTrip;

    internal Mill Mill { get; }

    public int Stock { get; private set; }

    public bool Full => Stock >= Capacity;

    /// <summary>Only a finished mill grinds: before that the farmer has nowhere to send his wheat.</summary>
    public bool Grinding => Mill.Complete;

    /// <summary>The miller, who takes the wheat in and loads the flour.</summary>
    internal Character Miller => Mill.Builder;

    internal FlourMill(Mill mill) => Mill = mill;

    /// <summary>One load of wheat is tipped out: two more sacks of flour stand beside the ring.</summary>
    internal void Deliver() => SetStock(Stock + PerLoad);

    /// <summary>Takes sacks of flour for the bakery; false when there are not enough.</summary>
    internal bool Take(int amount)
    {
        if (amount <= 0 || amount > Stock)
            return false;
        SetStock(Stock - amount);
        return true;
    }

    internal void SetStock(int stock)
    {
        Stock = Math.Clamp(stock, 0, Capacity);
        Mill.ShowFlour(Stock);
    }

    string ITripTarget.Kind => TripKinds.Mill;
    int ITripTarget.Index => 0;
    Character ITripTarget.Contractor => Miller;
    int ITripTarget.NextStep => 0;

    HaulLoad ITripTarget.LoadFor(int step) => HaulLoad.Wheat;

    Vector3 ITripTarget.DropFor(int step) => Mill.Site.Position;

    Vector3 ITripTarget.DropRoadFor(int step) => Mill.Site.Road;

    string ITripTarget.CounterFor(int step) => "";

    bool ITripTarget.DropsAtContractor(int step) => true;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishMillDump(trip);
}
