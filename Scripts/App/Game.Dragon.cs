using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The dragon under the mountains (<c>docs/DRAGON_PLAN.md</c>). Drak Kvído lies on his ledge beside the homestead at the
/// end of the road to the mountains; he has caught a cold, sneezes rings of smoke instead of fire, and his egg is getting
/// cold. His panel (state <see cref="GameState.DragonStatus"/>) shows the nest and, with a green check mark, sends the
/// trailer for its next load from the mountain slope: two of sand, two of stones and two of logs. When the nest is done
/// comes his riddle, three quizzes in a row; with every answer he breathes in deeper, and after the third he sneezes his
/// fire back: the fire by the nest catches, the egg hatches into the little dragon Jiskřička and coins are paid. From then
/// on he takes the player for a flight round the valley on his back, which lights the coins, the animals and the hidden
/// friends below; the first flight pays a few coins. Saved as <c>SaveData.Dragon</c>.
/// </summary>
public partial class Game
{
    private DragonLair? DragonLair => world.DragonLair;

    /// <summary>Seconds into the flight on the dragon's back, or less than 0 while he lies on his ledge.</summary>
    private float dragonFlight = -1;

    /// <summary>Whether the player has flown on the dragon before: only the first flight pays.</summary>
    private bool dragonFlown;

    /// <summary>How long into a flight the pedals do not end it yet, so that the press that started it does not.</summary>
    private const float DragonFlightGrace = 1.5f;

    internal bool DragonFlying => dragonFlight >= 0;

    private HaulTrip? TripOf(DragonLair lair) => Haul != null && Haul.DragonLair == lair ? Haul : null;

    /// <summary>Talking to the dragon shows his nest, his riddle or his flight, and he says what he wants.</summary>
    internal void OpenDragon(Character dragon)
    {
        if (DragonLair is not { } lair)
            return;
        EnterInteraction(GameState.DragonStatus, dragon);
        ShowDragon();
        if (Haul != null && TripOf(lair) != null)
            return;
        Speak(
            lair.Hatched ? (dragonFlown ? OutlandTexts.DragonFlightAgain : OutlandTexts.DragonFlightWanted)
                : lair.Complete ? OutlandTexts.DragonRiddleWanted
                : lair.Stage == 0 ? dragon.Greeting + "\n" + OutlandTexts.DragonNestWanted
                : OutlandTexts.DragonNext,
            dragon
        );
    }

    private void ShowDragon()
    {
        if (nearby is not { Role: DragonRole } dragon || DragonLair is not { } lair)
            return;
        ui.DragonPanel(dragon, lair, TripOf(lair), Haul == null && !lair.Complete, !DragonFlying);
    }

    internal void BeginDragonTrip() => BeginDragonTrip(false);

    private void BeginDragonTrip(bool quizzed)
    {
        if (
            State != GameState.DragonStatus
            || nearby is not { Role: DragonRole }
            || DragonLair is not { Complete: false } lair
            || Haul != null
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Dragon);
            return;
        }
        StartTrip(lair);
    }

    /// <summary>The load is tipped out in front of the nest: the nest grows and the egg gets warmer.</summary>
    internal void FinishDragonDump(HaulTrip trip)
    {
        var lair = trip.DragonLair!;
        lair.SetStage(trip.Step + 1);
        world.DragonBody?.Celebrate();
        CelebrateAt(lair.Focus, OutlandTexts.DragonPart(DragonPlans.Parts[trip.Step], lair.Stage, DragonPlans.Trips));
        EndHaul();
        Speak(lair.Complete ? OutlandTexts.DragonRiddleWanted : OutlandTexts.DragonThanks, lair.Dragon);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    /// <summary>The first question of the riddle; the nest must be finished and the egg not yet hatched.</summary>
    internal void BeginDragonRiddle()
    {
        if (
            State != GameState.DragonStatus
            || nearby is not { Role: DragonRole } dragon
            || DragonLair is not { Complete: true, Hatched: false } lair
        )
            return;
        BeginQuiz();
        Speak(OutlandTexts.DragonRiddle(lair.Riddle, DragonPlans.Riddles), dragon);
    }

    /// <summary>
    /// A quiz with the dragon is solved: at the mountain slope it was the loading question, by the nest an answer of the
    /// riddle. He breathes in deeper with each; the last makes him sneeze his fire back.
    /// </summary>
    internal void CompleteDragonQuiz(Character dragon)
    {
        // At the mountain slope the dragon asks the loading question from afar.
        if (Haul is { Loaded: false } trip && trip.Asker == dragon && trip.Reached(vehicle.Position))
        {
            CompleteHaulLoading();
            return;
        }
        if (DragonLair is not { Complete: true, Hatched: false } lair)
        {
            FinishInteraction();
            return;
        }
        ClearChallenge();
        lair.SetRiddle(lair.Riddle + 1, shown: false);
        world.DragonBody?.Inhale(lair.Riddle);
        if (!lair.Hatched)
        {
            SaveProgress();
            BeginQuiz();
            Speak(OutlandTexts.DragonRiddle(lair.Riddle, DragonPlans.Riddles), dragon);
            return;
        }
        Hatch(lair, dragon);
    }

    /// <summary>The third answer: the big sneeze lights the fire, the egg hatches, coins are paid and the little one comes.</summary>
    private void Hatch(DragonLair lair, Character dragon)
    {
        CloseInteraction();
        if (world.DragonBody is { } body)
            body.Sneeze(() =>
            {
                lair.ShowHatched();
                body.Healthy = true;
                body.Celebrate();
            });
        else
            lair.ShowHatched();
        AddCoins(DragonPlans.Coins);
        CelebrateAt(lair.Focus, WithPets(OutlandTexts.DragonHatched(DragonPlans.Coins), EarnPets()));
        Speak(OutlandTexts.DragonHatchedSpeech, dragon);
        var outward = followCamera.GlobalPosition - lair.Focus;
        outward.Y = 0;
        followCamera.ShowMoment(lair.Focus, outward.LengthSquared() > .01f ? outward.Normalized() : lair.Site.Facing);
        SaveProgress();
    }

    /// <summary>The player climbs onto the dragon's back and he takes off for a flight round the valley.</summary>
    internal void BeginDragonFlight()
    {
        if (
            State != GameState.DragonStatus
            || nearby is not { Role: DragonRole } dragon
            || DragonLair is not { Hatched: true }
            || world.DragonBody is not { } body
            || world.DragonFlight == null
            || DragonFlying
        )
            return;
        CloseInteraction();
        vehicle.Active = false;
        dragonFlight = 0;
        body.Flying = true;
        followCamera.Ride(body.Seat);
        float seconds = DragonFlight.Duration;
        WorldCoins.Flash(seconds);
        foreach (var animal in world.Animals)
            if (animal.RewardAvailable)
                animal.Ping(seconds);
        world.Hidden.Ping(seconds);
        Speak(OutlandTexts.DragonFlightSpeech, dragon);
        if (!dragonFlown)
        {
            dragonFlown = true;
            AddCoins(DragonPlans.FlightCoins);
            CelebrateAt(body.GlobalPosition + Vector3.Up * 6, OutlandTexts.DragonFirstFlight(DragonPlans.FlightCoins));
            SaveProgress();
        }
    }

    /// <summary>He lands on his ledge again and the camera goes back behind the car.</summary>
    private void EndDragonFlight()
    {
        if (!DragonFlying)
            return;
        dragonFlight = -1;
        if (DragonLair is { } lair && world.DragonBody is { } body)
            body.Rest(lair.DragonRest);
        followCamera.EndRide();
        if (State == GameState.Exploring)
            vehicle.Active = true;
        Speak(OutlandTexts.DragonFlightEnd, world.Dragon, interrupt: false);
    }

    /// <summary>Puts the nest, the riddle and the flights back from a save; the egg is hatched at once.</summary>
    private void RestoreDragon(int stage, int riddle, bool flown)
    {
        dragonFlight = -1;
        dragonFlown = flown;
        if (DragonLair is not { } lair)
            return;
        lair.SetStage(stage);
        lair.SetRiddle(riddle);
        if (world.DragonBody is { } body)
        {
            body.Rest(lair.DragonRest);
            body.Healthy = lair.Hatched;
            body.Inhale(lair.Hatched ? 0 : lair.Riddle);
        }
    }

    /// <summary>The dragon breathes and looks round, the fire flickers, and the flight goes on until he has landed.</summary>
    private void TickDragon(double delta)
    {
        if (State is GameState.Title || DragonLair is not { } lair)
            return;
        float dt = State == GameState.Paused ? 0 : (float)delta;
        lair.Tick(dt, Weather.Darkness);
        if (world.DragonBody is not { } body)
            return;
        body.Tick(dt, vehicle.Position);
        if (!DragonFlying || State == GameState.Paused)
            return;
        if (State == GameState.Exploring)
            vehicle.Active = false;
        dragonFlight += dt;
        if (
            dragonFlight >= DragonFlight.Duration
            || world.DragonFlight == null
            || dragonFlight > DragonFlightGrace && State == GameState.Exploring && DrivesOn()
        )
        {
            EndDragonFlight();
            return;
        }
        body.Transform = world.DragonFlight.Pose(dragonFlight);
    }
}
