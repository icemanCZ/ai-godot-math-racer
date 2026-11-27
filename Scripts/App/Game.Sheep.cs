using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The shepherd's lost sheep (<c>docs/SHEEP_PLAN.md</c>). A storm scattered eight of Zajíc pastýř Bohouš's sheep over the
/// land beyond the old valley; his panel (state
/// <see cref="GameState.ShepherdStatus"/>) shows his fold and, with a green arrow, sends the player out after them. A
/// sheep the car comes near bleats and trots after the car along its way, as many as are found; a car that drives too
/// fast leaves them behind, and one that falls far behind stops and waits. Back at the shepherd, a question sends the
/// sheep that follow into the fold, where they jump over the rail one after another: coins for each, a bonus for the
/// whole flock and the sheepdog puppy Alík. Whether the player was sent out, the sheep at home and the sheep found are
/// saved as <c>SaveData.Sheep</c>; a found sheep is put right behind the car after a load.
/// </summary>
public partial class Game
{
    private SheepHerd? sheepHerd;

    /// <summary>The sheep of this valley, made anew when the valley is.</summary>
    internal SheepHerd Sheep
    {
        get
        {
            if (sheepHerd == null || !sheepHerd.Grazes(world.SheepSpots))
                sheepHerd = new SheepHerd(world.SheepSpots);
            return sheepHerd;
        }
    }

    /// <summary>How many sheep waited for the car at the last tick, so that a sheep left behind is told once.</summary>
    private int sheepWaiting;

    /// <summary>The valley has the sheep, so the shepherd shows his fold from the start; no letter is needed.</summary>
    private bool SheepWanted => world.SheepFlock != null;

    /// <summary>The shepherd waits for the player to set out, or for the sheep that follow the car: he shows his mark.</summary>
    private bool SheepAwaitShepherd => SheepWanted && (!Sheep.Started || Sheep.FollowingCount > 0);

    /// <summary>The shepherd shows his fold; false in a valley without sheep.</summary>
    private bool OpenShepherd(Character keeper)
    {
        if (!SheepWanted)
            return false;
        EnterInteraction(GameState.ShepherdStatus, keeper);
        ShowShepherd();
        var herd = Sheep;
        Speak(
            !herd.Started ? keeper.Greeting + "\n" + Texts.SheepWanted
                : herd.Complete ? Texts.SheepAllHome
                : herd.FollowingCount > 0 ? Texts.SheepBrought(herd.FollowingCount)
                : Texts.SheepStillLost(herd.LostCount + herd.WaitingCount),
            keeper
        );
        return true;
    }

    private void ShowShepherd()
    {
        if (nearby is not { Role: BorderKeeperRole { Border: Border.Mountains } } keeper || world.SheepFlock == null)
            return;
        var herd = Sheep;
        var status = Enumerable.Range(0, herd.Count).Select(herd.StatusOf).ToArray();
        var where = Enumerable.Range(0, herd.Count).Select(i => SheepWhere(herd.Spot(i))).ToArray();
        ui.ShepherdPanel(
            keeper,
            herd.Started,
            status,
            where,
            herd.FollowingCount,
            world.AllPets.FirstOrDefault(pet => pet.Kind == CollectorPetKind.Sheepdog),
            Post.Stamps((int)Border.Mountains)
        );
    }

    /// <summary>
    /// The borders a meadow lies by, shown as their pictures under a lost sheep: one border (by the sea, under the
    /// mountains…) or the two of a corner (between the river and the sea…).
    /// </summary>
    internal static Border[] SheepWhere(Vector3 spot)
    {
        int side = WorldLayout.Plan.Side;
        var across = side * spot.X > 0 ? Border.River : Border.Forest;
        var along = spot.Z > 0 ? Border.Sea : Border.Mountains;
        float x = Mathf.Abs(spot.X),
            z = Mathf.Abs(spot.Z);
        if (x > z * 1.8f)
            return new[] { across };
        if (z > x * 1.8f)
            return new[] { along };
        return new[] { along, across };
    }

    /// <summary>The green arrow of his panel: the sheep appear on their meadows.</summary>
    internal void BeginSheepHunt()
    {
        if (
            State != GameState.ShepherdStatus
            || nearby is not { Role: BorderKeeperRole { Border: Border.Mountains } } keeper
            || !SheepWanted
            || Sheep.Started
        )
            return;
        Sheep.Start();
        keeper.Celebrate();
        CloseInteraction();
        UpdateSheep();
        Speak(Texts.SheepGoLook, keeper);
        SaveProgress();
    }

    internal void BeginSheepPen() => BeginSheepPen(false);

    /// <summary>
    /// The shepherd's second button: a question first, then the sheep that follow the car run into the fold one after
    /// another; coins for each, and for the last of the flock a bonus and the sheepdog puppy.
    /// </summary>
    private void BeginSheepPen(bool quizzed)
    {
        if (
            State != GameState.ShepherdStatus
            || nearby is not { Role: BorderKeeperRole { Border: Border.Mountains } } keeper
            || world.SheepFlock is not { } flock
            || Sheep.FollowingCount == 0
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Sheep);
            Speak(Texts.SheepPenQuestion, keeper);
            return;
        }
        ClearChallenge();
        var herd = Sheep;
        int before = herd.HomeCount;
        var penned = herd.Pen();
        flock.Pen(penned, before);
        UpdateSheep();
        keeper.Celebrate();
        bool full = herd.Complete;
        int coins = penned.Count * SheepPlans.CoinsPerSheep + (full ? SheepPlans.FlockCoins : 0);
        string message = Texts.SheepPenned(penned.Count, herd.HomeCount, herd.Count);
        if (full)
            message += "\n" + Texts.SheepFlockDone(SheepPlans.FlockCoins);
        CelebrateAt(world.FoldMiddle + Vector3.Up * 2, WithPets(message, EarnPets()));
        Speak(full ? Texts.SheepAllHome : Texts.SheepThanks(herd.LostCount + herd.WaitingCount), keeper);
        PayReward(coins);
    }

    /// <summary>Puts the sheep and the shepherd's mark in line with the herd.</summary>
    private void UpdateSheep()
    {
        if (world.SheepFlock is not { } flock)
            return;
        sheepWaiting = Sheep.WaitingCount;
        if (!flock.Penning)
            flock.Restore(Sheep, vehicle.Position, vehicle.Basis.Z);
        UpdatePostGuides();
    }

    /// <summary>
    /// The sheep move on: the found ones trot after the car, a sheep the car comes near joins it, a sheep left behind
    /// says so, and the corner panel points to the next sheep to find, the one left behind, or home.
    /// </summary>
    private void TickSheep(double delta)
    {
        if (State is GameState.Title || world.SheepFlock is not { } flock)
            return;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        var herd = Sheep;
        var car = vehicle.Position;
        var back = vehicle.Basis.Z;
        if (dt > 0 && herd.Active)
        {
            var joined = herd.Tick(car, back, dt);
            foreach (int sheep in joined)
                flock.Joined(sheep);
            if (joined.Count > 0)
            {
                int last = joined[^1];
                string message = Texts.SheepJoined(SheepPlans.Name(last), herd.FollowingCount);
                CelebrateAt(flock.PositionOf(last) + Vector3.Up * 1.6f, message);
                Speak(message);
                UpdatePostGuides();
                SaveProgress();
            }
            int waiting = herd.WaitingCount;
            if (waiting > sheepWaiting)
            {
                Speak(Texts.SheepLeftBehind);
                UpdatePostGuides();
            }
            sheepWaiting = waiting;
        }
        flock.Tick(dt, herd, car, back);
        bool exploring = State == GameState.Exploring;
        if (!herd.Active)
        {
            ui.SheepHud(false, vehicle, Vector3.Zero, null, 0, 0, 0, "", "");
            return;
        }
        // Where the arrow points: a sheep left behind first, then home with a few sheep or none left to find, else the
        // nearest lost sheep.
        Vector3 goal;
        Character? who = null;
        string note;
        int waitingSheep = herd.Nearest(SheepStatus.Waiting, car);
        int lostSheep = herd.Nearest(SheepStatus.Lost, car);
        if (waitingSheep >= 0)
        {
            goal = herd.WhereIs(waitingSheep);
            note = Texts.SheepGoBack(SheepPlans.Name(waitingSheep));
        }
        else if (herd.FollowingCount > 0 && (lostSheep < 0 || herd.FollowingCount >= 3))
        {
            goal = world.Shepherd?.Position ?? world.FoldMiddle;
            who = world.Shepherd;
            note = herd.Lag > SheepHerd.LagWarning ? Texts.SheepSlowDown : Texts.SheepTakeHome;
        }
        else if (lostSheep >= 0)
        {
            goal = herd.WhereIs(lostSheep);
            note =
                herd.Lag > SheepHerd.LagWarning ? Texts.SheepSlowDown
                : herd.FollowingCount > 0 ? Texts.SheepFindMore
                : Texts.SheepListen;
        }
        else
        {
            goal = world.FoldMiddle;
            note = "";
        }
        float distance = new Vector2(goal.X - car.X, goal.Z - car.Z).Length();
        ui.SheepHud(
            exploring,
            vehicle,
            goal,
            who,
            herd.FollowingCount,
            herd.HomeCount,
            herd.Count,
            distance < 15 ? "" : $"{distance:0} m",
            note
        );
    }

    /// <summary>Whether the corner panel of the sheep shows, for the checks.</summary>
    internal bool SheepHudShown => ui.SheepHudShown;

    /// <summary>The note the corner panel of the sheep shows, for the checks.</summary>
    internal string SheepHudNote => ui.SheepHudNote;

    /// <summary>Puts the flock back from a save, before the pets are, so that the puppy knows whether he is owned.</summary>
    private void RestoreSheep(bool started, int[] home, int[] following)
    {
        Sheep.Restore(started, home, following);
        sheepWaiting = 0;
        world.SheepFlock?.Restore(Sheep, vehicle.Position, vehicle.Basis.Z);
        UpdatePostGuides();
    }
}
