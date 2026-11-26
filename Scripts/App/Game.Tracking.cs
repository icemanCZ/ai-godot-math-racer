using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The tracks of the forest keeper (<c>docs/TRACKING_PLAN.md</c>). Medvědice hajná Božena keeps a book of the tracks of
/// six animals; once the post has brought her a letter, her panel (state <see cref="GameState.TrackingStatus"/>) shows
/// it and, with a green arrow, sends the player out after the first. A trail of footprints leads from beside her homestead over the land beyond the old valley
/// to the den of the animal; only a stretch of it shows in front of the car, the next prints glowing, and the arrow
/// points back to it when the car has left it. At the den a question coaxes the animal out: it pays coins, goes into the
/// book and stays sitting in front of its den, and the trail of the next animal begins right there. The sixth fills the
/// book, pays a bonus and brings the squirrel Oříška. Whether the player was sent out and how many animals are found are
/// saved as <c>SaveData.Tracking</c>; how far along a trail the car is, is found again from where it stands.
/// </summary>
public partial class Game
{
    private TrackingHunt? trackingHunt;

    /// <summary>The hunt after the tracks of this valley, made anew when the valley is.</summary>
    internal TrackingHunt Tracking
    {
        get
        {
            if (trackingHunt == null || !trackingHunt.Follows(world.TrackingTrails))
                trackingHunt = new TrackingHunt(world.TrackingTrails);
            return trackingHunt;
        }
    }

    /// <summary>The valley has the trails and the post has brought the forest keeper her first letter.</summary>
    private bool TrackingWanted =>
        world.TrackingDens != null && (Tracking.Started || Post.Delivered[(int)Border.Forest] > 0);

    /// <summary>The book of tracks is wanted and the forest keeper has not sent the player out yet: she shows her mark.</summary>
    private bool TrackingAwaitsStart => TrackingWanted && !Tracking.Started;

    /// <summary>The forest keeper shows her book of tracks; false before her first letter or without trails.</summary>
    private bool OpenTracking(Character keeper)
    {
        if (!TrackingWanted)
            return false;
        EnterInteraction(GameState.TrackingStatus, keeper);
        ShowTracking();
        var hunt = Tracking;
        Speak(
            !hunt.Started ? keeper.Greeting + "\n" + Texts.TrackingWanted
                : hunt.Current is { } trail ? Texts.TrackingFollow(trail.Animal)
                : Texts.TrackingBookFull,
            keeper
        );
        return true;
    }

    private void ShowTracking()
    {
        if (nearby is not { Role: BorderKeeperRole { Border: Border.Forest } } keeper || world.TrackingDens == null)
            return;
        var hunt = Tracking;
        ui.TrackingPanel(keeper, hunt.Started, hunt.Found, hunt.Current?.Animal, Post.Stamps((int)Border.Forest));
    }

    /// <summary>The green arrow of her panel: the first trail begins beside her homestead.</summary>
    internal void BeginTracking()
    {
        if (
            State != GameState.TrackingStatus
            || nearby is not { Role: BorderKeeperRole { Border: Border.Forest } } keeper
            || !TrackingWanted
            || Tracking.Started
        )
            return;
        Tracking.Start(vehicle.Position);
        keeper.Celebrate();
        CloseInteraction();
        UpdateTracking();
        Speak(Texts.TrackingFirst(Tracking.Current!.Animal), keeper);
        SaveProgress();
    }

    /// <summary>At the den of the trail followed now: the question that coaxes the animal out.</summary>
    internal void OpenTrackingAnimal(Character animal)
    {
        if (animal.Role is not TrackingAnimalRole || Tracking.Current is not { } trail || animal.Away)
            return;
        nearby = animal;
        vehicle.Active = false;
        BeginQuiz();
        Speak(Texts.TrackingCoax(trail.Animal), animal);
    }

    /// <summary>
    /// The animal is coaxed out: coins, its page in the book, and the next trail from its den; the last one fills the
    /// book, pays a bonus and brings the squirrel.
    /// </summary>
    internal void CompleteTrackingQuiz(Character animal)
    {
        if (Tracking.Current is not { } trail || world.TrackingDens is not { } dens)
        {
            FinishInteraction();
            return;
        }
        ClearChallenge();
        int den = Tracking.Found;
        Tracking.FindAnimal(vehicle.Position);
        UpdateTracking();
        bool full = Tracking.Complete;
        int coins = TrackingPlans.CoinsPerAnimal + (full ? TrackingPlans.BookCoins : 0);
        string message = Texts.TrackingFound(trail.Animal, Tracking.Found, Tracking.Trails);
        if (full)
            message += "\n" + Texts.TrackingBookDone(TrackingPlans.BookCoins);
        CelebrateAt(dens.Focus(den), WithPets(message, EarnPets()));
        Speak(
            Tracking.Current is { } next ? Texts.TrackingNext(trail.Animal, next.Animal) : Texts.TrackingLast,
            animal
        );
        PayReward(coins);
    }

    /// <summary>
    /// Puts the dens, the prints and the animal to coax out in line with the hunt: the animal moves on to the den of the
    /// trail followed now, with its name, or goes away when no trail is followed.
    /// </summary>
    private void UpdateTracking()
    {
        if (world.TrackingDens is not { } dens)
            return;
        var hunt = Tracking;
        dens.SetState(hunt.Started, hunt.Found);
        world.TrackingPrints?.Show(hunt.Current, hunt.Progress);
        if (world.TrackingAnimal is { } animal)
        {
            if (hunt.Current is { } trail)
            {
                animal.Position = trail.Talk;
                animal.Rename(TrackingPlans.Name(trail.Animal));
            }
            animal.Away = hunt.Current == null;
            animal.SetRewardAvailable(false);
        }
        UpdatePostGuides();
    }

    /// <summary>
    /// The car follows the trail: the prints it reaches move the stretch shown on, the glowing prints pulse, the corner
    /// panel shows how far it is, and when the car has left the trail the arrow points back to it.
    /// </summary>
    private void TickTracking(double delta)
    {
        if (State is GameState.Title || world.TrackingDens is not { } dens || world.TrackingPrints is not { } prints)
            return;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        dens.Tick(dt);
        prints.Tick(dt);
        var hunt = Tracking;
        bool exploring = State == GameState.Exploring;
        if (hunt.Current is not { } trail)
        {
            ui.TrackingHud(false, null, 0, false);
            return;
        }
        if (exploring && hunt.Step(vehicle.Position))
        {
            prints.Show(trail, hunt.Progress);
            SaveProgress();
        }
        bool free = Delivery == null && Haul == null && letterTrip == null;
        bool lost = free && hunt.Lost(vehicle.Position);
        ui.TrackingHud(exploring, trail.Animal, hunt.Fraction, lost);
        if (exploring && lost)
            ui.TrackingDirection(hunt.Lead, vehicle);
    }

    /// <summary>Whether the corner panel of a trail shows, and its arrow back to the trail, for the checks.</summary>
    internal bool TrackingHudShown => ui.TrackingHudShown;

    internal bool TrackingArrowShown => ui.TrackingArrowShown;

    /// <summary>Puts the hunt back from a save, before the pets are, so that the squirrel knows whether it is owned.</summary>
    private void RestoreTracking(bool started, int found)
    {
        Tracking.Restore(started, found, vehicle.Position);
        UpdateTracking();
    }
}
