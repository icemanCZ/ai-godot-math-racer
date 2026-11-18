using System;
using System.Collections.Generic;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.App;

/// <summary>
/// What every trailer trip has in common, whoever sends it: the quiz that comes first, the start of the trip and the
/// way back into the panel that offered it. What differs is in the target of the trip (<see cref="ITripTarget"/>)
/// and in the checks of the panel that offers it.
/// </summary>
public partial class Game
{
    /// <summary>
    /// The trips that come from a panel and return to it once the quiz is solved, so that the checks of the panel
    /// run once more. The golem's pieces and a resident's apples are expeditions and start differently.
    /// </summary>
    private Dictionary<Departure, Action<bool>> PanelTrips =>
        new()
        {
            [Departure.Build] = BeginBuildTrip,
            [Departure.Plot] = BeginPlotTrip,
            [Departure.House] = BeginHouseTrip,
            [Departure.Missing] = BeginMissingSupply,
        };

    /// <summary>Starts the trailer trip of a target after its quiz, the speaker being its contractor.</summary>
    private void StartTrip(ITripTarget target) => StartTrip(target, target.Contractor);

    /// <summary>
    /// Starts the trailer trip of a target: the task under way is replaced, the empty trailer is hitched and the
    /// speaker tells the player where to go.
    /// </summary>
    private void StartTrip(ITripTarget target, Animal speaker)
    {
        CancelActiveTask();
        StartHaul(new HaulTrip(world, vehicle, target, target.NextStep, loaded: false));
        Start();
        Speak(TripInstruction(Haul!), speaker);
        SaveProgress();
    }

    /// <summary>A trip whose quiz has just been solved starts: a trip from a panel, the golem's pieces or a resident's apples.</summary>
    private void StartDeparture(Departure trip)
    {
        if (nearby == null)
            return;
        if (PanelTrips.TryGetValue(trip, out var begin))
        {
            State = quiz.DepartureState;
            begin(true);
            // A refused trip must not leave the quiz state behind.
            if (State == quiz.DepartureState && State != GameState.Exploring)
                CloseInteraction();
            return;
        }
        if (trip == Departure.Golem)
        {
            if (Delivery?.Golem == true || GolemOwned)
                return;
            StartExpedition(world.Sorcerer, true);
        }
        else
        {
            deliveryReward = false;
            StartExpedition(nearby, true);
        }
        SaveProgress();
    }
}
