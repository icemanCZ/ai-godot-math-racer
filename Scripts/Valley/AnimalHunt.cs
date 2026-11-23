using System;
using System.Collections.Generic;
using System.Linq;

namespace MathRacer.Valley;

/// <summary>
/// The hunter's hunt, without any scene node. The hunter sends the player out with a net (<see cref="Begin"/>); an animal
/// caught in it is loaded on the car (<see cref="Load"/>) and taken back to his pen (<see cref="Deliver"/>), which ends the
/// hunt, and a new one begins only when the player asks him again. The animals in the pen are kept in the order they were
/// brought, by kind; <see cref="PetGoal"/> of them earn the hunter's pet. Where the net is (in the hand, flying, lying in the
/// grass) belongs to the scene and is not saved: after a load the player has the net in the hand again.
/// </summary>
internal sealed class AnimalHunt
{
    /// <summary>The animals that run free in the meadows during a hunt; their names are saved, so they never change.</summary>
    public static readonly WildKind[] Prey = { WildKind.Rabbit, WildKind.Fox, WildKind.Deer };

    /// <summary>The animals brought to the pen that earn the hunter's pet.</summary>
    public const int PetGoal = 8;

    /// <summary>The most animals a save may claim.</summary>
    public const int Limit = 999;

    private readonly List<WildKind> penned = new();

    /// <summary>The animals in the pen, the first one brought first.</summary>
    public IReadOnlyList<WildKind> Penned => penned;

    public int Caught => penned.Count;

    /// <summary>The hunter has sent the player out; ends when an animal is brought to the pen.</summary>
    public bool Active { get; private set; }

    /// <summary>The animal caught in the net and loaded on the car, or null.</summary>
    public WildKind? Carrying { get; private set; }

    /// <summary>The picture of an animal of the hunt in the corner of the screen and in the hunter's panel.</summary>
    public static string Icon(WildKind kind) => $"wild-{kind.ToString().ToLowerInvariant()}";

    public static bool IsPrey(WildKind kind) => Array.IndexOf(Prey, kind) >= 0;

    /// <summary>The kind of a saved name, or null when it names no animal of the hunt.</summary>
    public static WildKind? KindOf(string name) =>
        Enum.TryParse<WildKind>(name, false, out var kind) && IsPrey(kind) && kind.ToString() == name ? kind : null;

    /// <summary>Animals still to bring for the pet, or 0 once it is earned.</summary>
    public int UntilPet => Math.Max(0, PetGoal - Caught);

    /// <summary>Sends the player out; false while a hunt is on.</summary>
    public bool Begin()
    {
        if (Active)
            return false;
        Active = true;
        Carrying = null;
        return true;
    }

    /// <summary>Puts the caught animal on the car; false when no hunt is on or an animal is loaded already.</summary>
    public bool Load(WildKind kind)
    {
        if (!Active || Carrying != null || !IsPrey(kind))
            return false;
        Carrying = kind;
        return true;
    }

    /// <summary>Takes the animal on the car into the pen and ends the hunt; null when there is none.</summary>
    public WildKind? Deliver()
    {
        if (!Active || Carrying is not { } kind)
            return null;
        if (penned.Count < Limit)
            penned.Add(kind);
        Carrying = null;
        Active = false;
        return kind;
    }

    /// <summary>Takes the hunt from a save; unknown names are left out and an animal on the car needs a hunt.</summary>
    public void Restore(IEnumerable<string> pennedNames, bool active, string carrying)
    {
        penned.Clear();
        penned.AddRange(pennedNames.Select(KindOf).OfType<WildKind>().Take(Limit));
        Active = active;
        Carrying = active ? KindOf(carrying) : null;
    }

    public string[] PennedNames() => penned.Select(kind => kind.ToString()).ToArray();

    public string CarryingName => Carrying?.ToString() ?? "";
}
