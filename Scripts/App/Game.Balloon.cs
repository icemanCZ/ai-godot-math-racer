using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The balloon station far beyond the village (<c>docs/BALLOON_PLAN.md</c>). Liška balonářka Amálka stands by her tall
/// rainbow balloon, which is seen from all over the valley. Her panel (state <see cref="GameState.BalloonStatus"/>) shows
/// the stars caught so far and, with a green arrow, asks a question that heats the air in the envelope; the right answer
/// lets the balloon go. The wind carries it on a fixed way out over the valley and back; the player only makes it climb
/// (the pedal: the burner) or sink (reverse: the valve) to catch the golden stars that hang along the way at different
/// heights. Every star pays a coin, a flight that catches all of them a bonus, the very first flight a few coins more,
/// and twenty stars in all bring the rooster Kokrháček. The flight also lights the coins, the animals and the hidden
/// friends below, as the dragon's does. The flights and the stars are saved as <c>SaveData.Balloon</c>; a flight is not.
/// </summary>
public partial class Game
{
    private BalloonStation? BalloonStation => world.BalloonStation;

    /// <summary>The flight under way, or null while the balloon stands on its field.</summary>
    private BalloonRide? balloonRide;

    /// <summary>The flights made and the stars caught in all, kept here too for a valley without the station.</summary>
    private int balloonFlights,
        balloonStars;

    /// <summary>How long into a flight the talk button does not end it yet, so that the press that started it does not.</summary>
    private const float BalloonGrace = 1.5f;

    internal bool BalloonFlying => balloonRide != null;

    /// <summary>The flight under way, for the checks.</summary>
    internal BalloonRide? BalloonRide => balloonRide;

    internal int BalloonFlights => balloonFlights;

    internal int BalloonStars => balloonStars;

    /// <summary>Talking to the balloonist shows the stars caught so far and the flight.</summary>
    internal void OpenBalloonist(Character balloonist)
    {
        if (BalloonStation == null)
            return;
        EnterInteraction(GameState.BalloonStatus, balloonist);
        ShowBalloon();
        Speak(
            balloonFlights == 0 ? balloonist.Greeting + "\n" + Texts.BalloonFirst
                : balloonStars >= BalloonPlans.RoosterStars ? Texts.BalloonAgain
                : Texts.BalloonMoreStars(BalloonPlans.RoosterStars - balloonStars),
            balloonist
        );
    }

    private void ShowBalloon()
    {
        if (nearby is not { Role: BalloonistRole } balloonist || BalloonStation == null)
            return;
        ui.BalloonPanel(balloonist, balloonFlights, balloonStars, !BalloonFlying);
    }

    /// <summary>The green arrow of her panel: first the question that heats the air in the envelope.</summary>
    internal void BeginBalloonFlight()
    {
        if (
            State != GameState.BalloonStatus
            || nearby is not { Role: BalloonistRole } balloonist
            || BalloonStation == null
            || BalloonFlying
        )
            return;
        BeginQuiz();
        Speak(Texts.BalloonQuiz, balloonist);
    }

    /// <summary>The question is solved: the air is hot, the ropes are let go and the balloon rises.</summary>
    internal void CompleteBalloonQuiz(Character balloonist)
    {
        ClearChallenge();
        CloseInteraction();
        TakeOff(balloonist);
    }

    private void TakeOff(Character balloonist)
    {
        if (
            BalloonStation is not { } station
            || world.Balloon is not { } balloon
            || world.BalloonFlight is not { } way
            || BalloonFlying
        )
            return;
        bool first = balloonFlights == 0;
        balloonRide = new BalloonRide(way, balloonFlights, station.Lift);
        balloonFlights++;
        station.Flights = balloonFlights;
        station.SetFlying(true);
        balloon.Flying = true;
        world.BalloonStars?.Show(way, balloonRide.Number);
        vehicle.Active = false;
        followCamera.Ride(balloon, new Vector3(-18, 11, -27), new Vector3(0, 8, 20));
        float seconds = way.Duration + 8;
        WorldCoins.Flash(seconds);
        foreach (var animal in world.Animals)
            if (animal.RewardAvailable)
                animal.Ping(seconds);
        world.Hidden.Ping(seconds);
        ui.BalloonHud(true, 0, BalloonFlight.Stars);
        Speak(Texts.BalloonTakeOff, balloonist);
        if (first)
        {
            AddCoins(BalloonPlans.FirstFlightCoins);
            CelebrateAt(balloon.Focus, Texts.BalloonFirstFlight(BalloonPlans.FirstFlightCoins));
        }
        SaveProgress();
    }

    /// <summary>A star is caught: a coin, and once enough stars are caught, the rooster.</summary>
    private void CatchBalloonStar(int star)
    {
        if (balloonRide is not { } ride)
            return;
        balloonStars++;
        if (BalloonStation is { } station)
            station.Stars = balloonStars;
        world.BalloonStars?.Catch(star);
        AddCoins(BalloonPlans.CoinsPerStar);
        ui.BalloonHud(true, ride.Caught, BalloonFlight.Stars);
        string? pets = EarnPets();
        var where = world.BalloonStars?.StarPosition(star) ?? vehicle.Position;
        CelebrateAt(where, WithPets(Texts.BalloonStar(ride.Caught, BalloonFlight.Stars), pets));
        if (pets != null)
            Speak(Texts.BalloonRooster, world.Balloonist);
        SaveProgress();
    }

    /// <summary>
    /// The flight ends: on the field after the whole way, when the bonus for every star is paid, or early with the talk
    /// button or a load, when the balloon is put back on its field at once.
    /// </summary>
    private void EndBalloonFlight(bool landed)
    {
        if (balloonRide is not { } ride)
            return;
        balloonRide = null;
        if (BalloonStation is { } station && world.Balloon is { } balloon)
        {
            balloon.Flying = false;
            station.SetFlying(false);
            // After the whole way it stands on the target and rises on its ropes; ended early, it is back on them at once.
            if (landed)
                station.TouchDown();
            else
                station.Moor();
        }
        world.BalloonStars?.TakeDown();
        followCamera.EndRide();
        ui.BalloonHud(false, 0, 0);
        if (State == GameState.Exploring)
            vehicle.Active = true;
        if (!landed)
            return;
        if (ride.Caught == BalloonFlight.Stars)
        {
            AddCoins(BalloonPlans.AllStarsCoins);
            CelebrateAt(
                BalloonStation?.Focus ?? vehicle.Position,
                Texts.BalloonAllStars(BalloonFlight.Stars, BalloonPlans.AllStarsCoins)
            );
        }
        Speak(Texts.BalloonLanded(ride.Caught, BalloonFlight.Stars), world.Balloonist, interrupt: false);
        SaveProgress();
    }

    /// <summary>The talk button during a flight lands the balloon at once.</summary>
    private bool LandBalloonEarly()
    {
        if (balloonRide is not { } ride)
            return false;
        if (ride.Seconds > BalloonGrace)
            EndBalloonFlight(false);
        return true;
    }

    /// <summary>Puts the flights and the stars back from a save; a flight under way ends, as the balloon was on its field.</summary>
    private void RestoreBalloon(int flights, int stars)
    {
        EndBalloonFlight(false);
        balloonFlights = flights;
        balloonStars = stars;
        if (BalloonStation is { } station)
        {
            station.Flights = flights;
            station.Stars = stars;
        }
    }

    /// <summary>
    /// The windsock swings and the burner roars now and then; in flight the wind carries the balloon on, the pedal heats
    /// the air and the brake lets it out, and the stars along the way are caught or passed.
    /// </summary>
    private void TickBalloon(double delta)
    {
        if (State is GameState.Title || BalloonStation is not { } station || world.Balloon is not { } balloon)
            return;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        station.Tick(dt);
        world.BalloonStars?.Tick(dt);
        if (balloonRide is not { } ride)
        {
            balloon.Tick(dt, Weather.Darkness, false);
            return;
        }
        float lift = 0;
        if (State == GameState.Exploring)
        {
            vehicle.Active = false;
            lift = BalloonLift();
            ui.HiddenFriendHint(Texts.BalloonLand);
        }
        int caught = ride.Step(dt, lift);
        balloon.Transform = ride.Pose();
        balloon.Tick(dt, Weather.Darkness, ride.Burning);
        if (caught >= 0)
            CatchBalloonStar(caught);
        if (world.BalloonStars is { } stars)
            for (int star = 0; star < BalloonFlight.Stars; star++)
                if (!ride.IsCaught(star) && ride.Distance > ride.Way.StarDistance(star) + BalloonRide.CatchAlong)
                    stars.Miss(star);
        if (ride.Landed)
            EndBalloonFlight(true);
    }

    /// <summary>
    /// The burner on the pedal (or the stick forward), the valve on reverse (or the stick back); the space bar is the
    /// talk button, which lands the balloon.
    /// </summary>
    private static float BalloonLift()
    {
        float up = Mathf.Max(Input.GetActionStrength("accelerate"), Input.GetActionStrength("stick_forward"));
        float down = Mathf.Max(Input.GetActionStrength("reverse"), Input.GetActionStrength("stick_reverse"));
        return Mathf.Clamp(up - down, -1, 1);
    }
}
