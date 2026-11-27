using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Quizzes;

namespace MathRacer.Ui;

/// <summary>
/// What the player asked for with a button or a tap on a panel. <see cref="GameUi"/> raises one of these through its
/// single <c>Intended</c> event and <c>Game.HandleIntent</c> answers it, so a new button is a new case here and a new
/// line in that switch, not a new event wired up in two places. The set is closed: only the types below derive from it.
/// </summary>
internal abstract record UiIntent
{
    private UiIntent() { }

    // The title screen and the pause menu.
    internal sealed record Play : UiIntent;

    internal sealed record NewGame : UiIntent;

    internal sealed record Resume : UiIntent;

    // Leaving a panel, talking to the nearest character, the narrator.
    internal sealed record CloseInteraction : UiIntent;

    internal sealed record Talk : UiIntent;

    internal sealed record Speak(string Text) : UiIntent;

    // Questions.
    internal sealed record Answer(int Choice) : UiIntent;

    internal sealed record ConfirmSyllables : UiIntent;

    // The garage.
    internal sealed record GarageReward(CarReward Reward) : UiIntent;

    internal sealed record ToggleQuizType(QuizType Type) : UiIntent;

    internal sealed record MathLevel(bool Advanced) : UiIntent;

    internal sealed record ToggleGolem : UiIntent;

    internal sealed record ToggleCollectorPet(CollectorPetKind Kind) : UiIntent;

    // Trips of the village and the buildings.
    internal sealed record VillageFruit : UiIntent;

    internal sealed record Expedition(bool Collection) : UiIntent;

    internal sealed record GolemQuest : UiIntent;

    internal sealed record BuildQuest : UiIntent;

    internal sealed record SupplyQuest : UiIntent;

    internal sealed record MissingSupply : UiIntent;

    internal sealed record HouseTrip : UiIntent;

    internal sealed record PlotTrip : UiIntent;

    internal sealed record PlaygroundTrip : UiIntent;

    internal sealed record RailwayTrip : UiIntent;

    internal sealed record ZooTrip : UiIntent;

    /// <summary>The lamp maker's shop: send the trailer for glasses from the glassworks.</summary>
    internal sealed record GlassmakerTrip : UiIntent;

    internal sealed record Lookout : UiIntent;

    // The pirate captain's treasure hunt.
    internal sealed record StartTreasureHunt : UiIntent;

    // The hunter at his pen.
    internal sealed record StartHunt : UiIntent;

    internal sealed record HutTrip : UiIntent;

    // The post to the four corners of the valley and the lighthouse on the shore.
    internal sealed record TakeLetter : UiIntent;

    internal sealed record LighthouseTrip : UiIntent;

    // The dragon under the mountains: a trip for his nest, the riddle that makes him sneeze and the flight on his back.
    internal sealed record DragonTrip : UiIntent;

    internal sealed record DragonRiddle : UiIntent;

    internal sealed record DragonFlight : UiIntent;

    // The balloon station: a flight in the hot-air balloon, after the question that heats its air.
    internal sealed record BalloonFlight : UiIntent;

    // The forest keeper's book of tracks: out after the first animal's trail.
    internal sealed record TrackingStart : UiIntent;

    // The shepherd's fold: out after his lost sheep, and the sheep that follow the car into the fold.
    internal sealed record SheepStart : UiIntent;

    internal sealed record SheepPen : UiIntent;

    // The owl's map: the wonders seen go into it, and the road arrows lead to the nearest wonder not yet seen.
    internal sealed record ExplorerRecord : UiIntent;

    internal sealed record ExplorerGuide : UiIntent;

    /// <summary>The heron's green check mark at the landing: the next load of the landing, or the crossing.</summary>
    internal sealed record FerryStart : UiIntent;

    /// <summary>The heron's green check mark on the island: the telescope of the hide.</summary>
    internal sealed record FerryTelescope : UiIntent;

    /// <summary>The heron's punt on the island: the crossing home.</summary>
    internal sealed record FerryHome : UiIntent;

    // The ferrywoman's rally: a stage waits for the car at its start arch.
    internal sealed record RallyStart(int Stage) : UiIntent;

    // The witch's night panel: the firefly hunt, or the weather question.
    internal sealed record StartFireflyHunt : UiIntent;

    internal sealed record ChangeWeather : UiIntent;

    // The mayor's shop.
    internal sealed record BuyDecoration(string Id) : UiIntent;

    // The glass lamp maker's shop.
    internal sealed record TakeLamp(string Id) : UiIntent;

    // The trailer.
    internal sealed record ToggleTrailer : UiIntent;

    internal sealed record TrailerPaint(string Id) : UiIntent;
}
