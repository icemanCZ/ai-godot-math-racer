using System;
using MathRacer.Buildings;
using MathRacer.Quizzes;

namespace MathRacer.App;

public partial class Game
{
    internal static int QuizRotationLength => QuizSession.RotationLength;

    /// <summary>
    /// The trip that starts once the quiz now on screen is solved. SaveData persists these numbers, so a value is
    /// never renumbered or reused.
    /// </summary>
    internal enum Departure
    {
        None,
        Golem,
        Fruit,
        Build,
        Plot,
        House,
        Missing,
        Playground,
        Railway,
        Zoo,

        /// <summary>Glasses from the glassworks for the lamp maker on the square.</summary>
        Glassmaker,

        /// <summary>Goods from the works for the hunter's hut.</summary>
        Hunter,

        /// <summary>Stone from the coast quarry and goods from the works for the lighthouse.</summary>
        Lighthouse,
    }

    /// <summary>Asks the quiz that must be solved before a trip starts; the trip continues in StartDeparture.</summary>
    private void AskBeforeDeparture(Departure trip)
    {
        quiz.DepartureState = State;
        quiz.Departure = trip;
        BeginQuiz();
    }

    private void ToggleQuizType(QuizType type)
    {
        if (State != GameState.Garage || quiz.Enabled == type)
            return;
        quiz.Enabled ^= type;
        ShowGarage();
        SaveProgress();
    }

    private void SetMathLevel(bool advanced)
    {
        if (State != GameState.Garage || quiz.ThreeTermMath == advanced)
            return;
        quiz.ThreeTermMath = advanced;
        ShowGarage();
        SaveProgress();
    }

    /// <summary>
    /// Opens the next enabled quiz type. With <paramref name="load"/>, an arithmetic turn becomes the
    /// trailer loading question for that material.
    /// </summary>
    private void BeginQuiz(bool handover = false, HaulLoad? load = null)
    {
        CurrentChallenge = quiz.Next(load);
        quiz.Handover = handover;
        quiz.Selection = default;
        quiz.Rejected.Clear();
        State = GameState.Challenge;
        ShowChallenge();
        if (nearby != null && !InBanditQuiz)
            Speak(QuizInstruction(nearby, CurrentChallenge), nearby);
    }
}
