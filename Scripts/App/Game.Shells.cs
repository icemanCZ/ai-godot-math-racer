using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The shells of the beach (<c>docs/SHELLS_PLAN.md</c>). The sea falls and rises twice a day with the clock
/// (<see cref="Tide"/>); every high tide leaves new shells on the wet sand of the cove below the homestead of the keeper of
/// the sea, which only the low tide uncovers. The car may drive down the beach past the edge of the valley as far as the
/// water lets it and picks up a shell by driving to it: a coin for each, more for the first of a kind, and once all eight
/// kinds are found a bonus and the crab Klepetko. The corner panel shows by the beach how many shells of each kind the
/// player has and points to the nearest shell. The tide, its shells picked up and the shells of each kind are saved as
/// <c>SaveData.Shells</c>.
/// </summary>
public partial class Game
{
    /// <summary>The corner panel of the shells shows while the car is this near the cove.</summary>
    private const float ShellHudReach = 150;

    private ShellsLog? shellsLog;
    private BeachShells? shellsLaidFor;
    private int shellsLaidTide = -1;
    private int lastHalfOfDay = -1;
    private bool shellsAnnounced;

    /// <summary>The shells of this valley, made anew when the valley is.</summary>
    internal ShellsLog Shells
    {
        get
        {
            if (shellsLog == null || !ReferenceEquals(shellsLaidFor, world.BeachShells))
            {
                shellsLog = new ShellsLog();
                shellsLaidFor = world.BeachShells;
                shellsLaidTide = -1;
                lastHalfOfDay = -1;
            }
            return shellsLog;
        }
    }

    /// <summary>The shells of the tide on the sand now, laid out again when the tide is a new one.</summary>
    private BeachShell[] ShellsNow { get; set; } = System.Array.Empty<BeachShell>();

    private void LayShells()
    {
        var log = Shells;
        if (world.BeachShells is not { } models || shellsLaidTide == log.Tide)
            return;
        shellsLaidTide = log.Tide;
        ShellsNow = world.ShellsOfTide(log.Tide);
        models.Lay(ShellsNow);
        models.KindsFound = log.KindsFound;
    }

    /// <summary>
    /// The tide follows the clock; a high tide brings the next shells; the car picks up a shell it drives to; and by the
    /// cove the corner panel shows the shells of each kind and the arrow to the nearest one uncovered.
    /// </summary>
    private void TickShells(double delta)
    {
        world.SetTide(Weather.Clock);
        if (State is GameState.Title || world.BeachShells is not { } models)
        {
            ui.ShellsHud(false, vehicle, Vector3.Zero, Shells.Kinds, "");
            return;
        }
        var log = Shells;
        int half = Tide.HalfOfDay(Weather.Clock);
        if (lastHalfOfDay >= 0 && half != lastHalfOfDay)
        {
            log.NextTide();
            shellsAnnounced = false;
            SaveProgress();
        }
        lastHalfOfDay = half;
        LayShells();
        float sea = world.SeaSurface;
        models.Refresh(log.IsPicked, sea, delta);
        bool exploring = State == GameState.Exploring;
        var car = vehicle.Position;
        if (exploring)
            foreach (var shell in ShellsNow)
            {
                if (log.IsPicked(shell.Number) || !models.Shown(shell.Number))
                    continue;
                if (FlatMath.Length(shell.Position.X - car.X, shell.Position.Z - car.Z) > ShellPlans.PickReach)
                    continue;
                PickShell(shell);
                models.Refresh(log.IsPicked, sea, 0);
                break;
            }
        BeachShell? nearest = null;
        float distance = float.MaxValue;
        foreach (var shell in ShellsNow)
        {
            if (log.IsPicked(shell.Number) || !models.Shown(shell.Number))
                continue;
            float d = FlatMath.Length(shell.Position.X - car.X, shell.Position.Z - car.Z);
            if (d < distance)
            {
                distance = d;
                nearest = shell;
            }
        }
        bool near =
            world.ShellCove is { } cove
            && Mathf.Abs(car.X - cove.X) < World.CoveHalfWidth + ShellHudReach
            && car.Z > cove.Z - ShellHudReach;
        bool shown = exploring && near && Delivery == null && Haul == null;
        if (shown && nearest != null && !shellsAnnounced)
        {
            shellsAnnounced = true;
            Speak(OutlandTexts.ShellsLowTide);
        }
        ui.ShellsHud(
            shown,
            vehicle,
            nearest?.Position ?? Vector3.Zero,
            log.Kinds,
            nearest != null ? $"{distance:0} m" : OutlandTexts.ShellsWait
        );
    }

    /// <summary>A shell is picked up: a coin, more for the first of its kind, and for the last kind the crab.</summary>
    private void PickShell(BeachShell shell)
    {
        var log = Shells;
        bool newKind = log.Pick(shell.Number, shell.Kind);
        if (world.BeachShells is { } models)
            models.KindsFound = log.KindsFound;
        bool full = newKind && log.Complete;
        int coins =
            ShellPlans.CoinsPerShell + (newKind ? ShellPlans.NewKindCoins : 0) + (full ? ShellPlans.AllKindsCoins : 0);
        AddCoins(coins);
        string name = ShellPlans.Name(shell.Kind);
        string message = newKind
            ? OutlandTexts.ShellNewKind(name, log.KindsFound, ShellPlans.Kinds, coins)
            : OutlandTexts.ShellPicked(name, log.CountOf(shell.Kind));
        if (full)
            message += "\n" + OutlandTexts.ShellsAllKinds(ShellPlans.AllKindsCoins);
        string? pets = newKind ? EarnPets() : null;
        CelebrateAt(shell.Position + Vector3.Up, WithPets(message, pets));
        Speak(
            full ? OutlandTexts.ShellsCrab
            : newKind ? OutlandTexts.ShellNewKindSpoken(name)
            : name
        );
        SaveProgress();
    }

    /// <summary>Whether the corner panel of the shells shows, for the checks.</summary>
    internal bool ShellsHudShown => ui.ShellsHudShown;

    /// <summary>The shells of the tide on the sand now, for the checks.</summary>
    internal BeachShell[] ShellsOnSand => ShellsNow;

    /// <summary>Puts the shells back from a save, before the pets are, so that the crab knows whether he is owned.</summary>
    private void RestoreShells(int tide, int[] picked, int[] kinds)
    {
        Shells.Restore(tide, picked, kinds);
        shellsLaidTide = -1;
        lastHalfOfDay = -1;
        LayShells();
        if (world.BeachShells is { } models)
            models.KindsFound = Shells.KindsFound;
    }
}
