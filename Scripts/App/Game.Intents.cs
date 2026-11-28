using Godot;
using MathRacer.Ui;

namespace MathRacer.App;

public partial class Game
{
    /// <summary>
    /// The one place where what the player asked for in a panel (<see cref="UiIntent"/>) becomes something the game does.
    /// </summary>
    private void HandleIntent(UiIntent intent)
    {
        switch (intent)
        {
            case UiIntent.Play:
                ContinueGame();
                break;
            case UiIntent.NewGame:
                NewGame();
                break;
            case UiIntent.Resume:
                Resume();
                break;
            case UiIntent.CloseInteraction:
                CloseInteraction();
                break;
            case UiIntent.Talk:
                Interact();
                break;
            case UiIntent.Speak speak:
                Speak(speak.Text);
                break;
            case UiIntent.Answer answer:
                SubmitAnswer(answer.Choice);
                break;
            case UiIntent.ConfirmSyllables:
                ConfirmSyllables();
                break;
            case UiIntent.GarageReward garage:
                ConfigureCar(garage.Reward);
                break;
            case UiIntent.ToggleQuizType quiz:
                ToggleQuizType(quiz.Type);
                break;
            case UiIntent.MathLevel level:
                SetMathLevel(level.Advanced);
                break;
            case UiIntent.ToggleGolem:
                ToggleGolem();
                break;
            case UiIntent.ToggleCollectorPet pet:
                ToggleCollectorPet(pet.Kind);
                break;
            case UiIntent.VillageFruit:
                BeginVillageCollection();
                break;
            case UiIntent.Expedition expedition:
                SelectExpedition(expedition.Collection);
                break;
            case UiIntent.GolemQuest:
                BeginGolemQuest();
                break;
            case UiIntent.BuildQuest:
                BeginBuildTrip();
                break;
            case UiIntent.SupplyQuest:
                BeginSupplyTrip();
                break;
            case UiIntent.MissingSupply:
                BeginMissingSupply();
                break;
            case UiIntent.HouseTrip:
                BeginHouseTrip();
                break;
            case UiIntent.PlotTrip:
                BeginPlotTrip();
                break;
            case UiIntent.PlaygroundTrip:
                BeginPlaygroundTrip();
                break;
            case UiIntent.RailwayTrip:
                BeginRailwayTrip();
                break;
            case UiIntent.ZooTrip:
                BeginZooTrip();
                break;
            case UiIntent.GlassmakerTrip:
                BeginGlassmakerTrip();
                break;
            case UiIntent.StartTreasureHunt:
                BeginTreasureHunt();
                break;
            case UiIntent.StartHunt:
                BeginHunt();
                break;
            case UiIntent.HutTrip:
                BeginHutTrip();
                break;
            case UiIntent.TakeLetter:
                TakeLetter();
                break;
            case UiIntent.LighthouseTrip:
                BeginLighthouseTrip();
                break;
            case UiIntent.DragonTrip:
                BeginDragonTrip();
                break;
            case UiIntent.DragonRiddle:
                BeginDragonRiddle();
                break;
            case UiIntent.DragonFlight:
                BeginDragonFlight();
                break;
            case UiIntent.BalloonFlight:
                BeginBalloonFlight();
                break;
            case UiIntent.TrackingStart:
                BeginTracking();
                break;
            case UiIntent.SheepStart:
                BeginSheepHunt();
                break;
            case UiIntent.SheepPen:
                BeginSheepPen();
                break;
            case UiIntent.ExplorerRecord:
                BeginExplorerRecord();
                break;
            case UiIntent.ExplorerGuide:
                BeginExplorerGuide();
                break;
            case UiIntent.FerryStart:
                BeginFerryStart();
                break;
            case UiIntent.FerryTelescope:
                BeginTelescope();
                break;
            case UiIntent.FerryHome:
                BeginFerryHome();
                break;
            case UiIntent.CastleStart:
                BeginCastleStart();
                break;
            case UiIntent.MainLineStart:
                BeginMainLineStart();
                break;
            case UiIntent.MainLineRide ride:
                BeginMainLineRide(ride.Arm);
                break;
            case UiIntent.MainLineHome:
                BeginMainLineHome();
                break;
            case UiIntent.ShipStart:
                BeginShipStart();
                break;
            case UiIntent.ShipSail:
                BeginShipSail();
                break;
            case UiIntent.ShipHome:
                BeginShipHome();
                break;
            case UiIntent.RallyStart start:
                BeginRallyStage(start.Stage);
                break;
            case UiIntent.StartFireflyHunt:
                BeginFireflyHunt();
                break;
            case UiIntent.ChangeWeather:
                BeginWeatherQuiz();
                break;
            case UiIntent.BuyDecoration buy:
                BuyDecoration(buy.Id);
                break;
            case UiIntent.TakeLamp take:
                TakeLamp(take.Id);
                break;
            case UiIntent.Lookout:
                BeginLookout();
                break;
            case UiIntent.ToggleTrailer:
                ToggleTrailer();
                break;
            case UiIntent.TrailerPaint paint:
                SelectTrailerPaint(paint.Id);
                break;
            default:
                GD.PushError($"Nothing handles {intent}.");
                break;
        }
    }
}
