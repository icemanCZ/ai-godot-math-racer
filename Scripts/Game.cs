using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace MathRacer;

public enum GameState
{
    // SaveData persists these numeric values. A new interaction state needs a panel in Game.ShowPanel.
    Title = 0,
    Exploring = 1,
    Challenge = 2,
    Reward = 3,
    Paused = 4,
    ExpeditionChoice = 5,
    Garage = 6,
    HouseReward = 7,
    HouseCollection = 8,
    HouseFruitOffer = 9,
    FarmStatus = 10,
    GardenStatus = 11,
    CollectionStatus = 12,
    TradeStatus = 13,
    PostcardAlbum = 14,
    WeatherChoice = 15,
    SorcererStatus = 16,
    FordStatus = 17,
}

public partial class Game : Node3D
{
    public GameState State { get; private set; } = GameState.Title;
    private readonly World world = new();
    private readonly Vehicle vehicle = new();
    private readonly GameUi ui = new();
    private Animal? nearby;
    internal Expedition? Delivery { get; private set; }
    private readonly RoadsideProgress roadside = new();
    private readonly CarRewards rewards = new();
    private bool deliveryReward;
    private int lastCollected = -1;
    internal int RemainingDeliveries => world.Animals.Length - roadside.CompletedCount;
    internal int RemainingAnimals => world.Animals.Count(animal => animal.RewardAvailable);
    private GameState beforePause;
    private readonly ChallengeGenerator challenges = new(Settings.Seed + 204);
    private readonly HashSet<int> rejectedAnswers = new();
    internal IChallenge? CurrentChallenge { get; private set; }
    internal HashSet<CarReward> OwnedRewards => rewards.Owned;
    private bool AtGarage => vehicle.Position.DistanceTo(world.Village.GarageEntrance) < Settings.InteractionRange;

    public override void _Ready()
    {
        GameControls.Setup();
        bool diagnosticOnly = false;
        RunDiagnosticsBeforeWorld(ref diagnosticOnly);
        if (diagnosticOnly)
            return;

        AddChild(world);
        world.Generate();
        AddChild(vehicle);
        vehicle.Setup(world.Ground, world.Routes, world.Ford);
        RunDiagnosticsWithVehicle(ref diagnosticOnly);
        if (diagnosticOnly)
            return;

        world.AddChild(Events);
        Events.Generate(world);
        SetupPostcards();
        SetupBandits();
        SetupCoins();
        world.AddChild(world.Wildlife);
        world.Wildlife.Generate(world, vehicle);
        var camera = new FollowCamera
        {
            Target = vehicle,
            Ground = world.Ground,
            Fov = 62,
            Far = 1200,
            Current = true,
        };
        AddChild(camera);
        followCamera = camera;
        camera.Snap();
        world.AddChild(Weather);
        Weather.Setup(world, vehicle, camera);
        world.AddChild(world.Grass);
        world.Grass.Setup(world, vehicle);
        SetupUi();
        ui.CoinCount(Coins, 0);
        SetupGolem();
        SetupTrailer();
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);

        persistenceReady = true;
        bool skipTitle = false;
        DiagnosticsSkipTitle(ref skipTitle);
        if (Settings.PendingNewGameQuizzes is { } quizSettings)
        {
            (enabledQuizzes, threeTermMath) = quizSettings;
            Settings.PendingNewGameQuizzes = null;
            Start();
            SaveProgress();
        }
        else if (skipTitle)
            ContinueGame();

        AttachDiagnostics(camera);
        GD.Print(
            $"WORLD_READY seed={Settings.Seed} animals={world.Animals.Length} road_samples={world.Routes.Samples.Count}"
        );
    }

    // Hooks implemented in Scripts/Diagnostics, which release exports leave out. Without an
    // implementation the compiler removes the calls, so a release build always starts normally.
    partial void RunDiagnosticsBeforeWorld(ref bool handled);

    partial void RunDiagnosticsWithVehicle(ref bool handled);

    partial void DiagnosticsSkipTitle(ref bool skipTitle);

    partial void AttachDiagnostics(FollowCamera camera);

    private void SetupUi()
    {
        AddChild(ui);
        ui.Setup();
        ui.PlayRequested += ContinueGame;
        ui.ResumeRequested += Resume;
        ui.CloseInteractionRequested += CloseInteraction;
        ui.Title(SaveStore.Initial != null);
        ui.NewGameRequested += NewGame;
        ui.AnswerSelected += SubmitAnswer;
        ui.SyllablesConfirmed += ConfirmSyllables;
        ui.SpeechRequested += Speak;
        ui.TalkRequested += Interact;
        ui.VillageFruitRequested += BeginVillageCollection;
        ui.ExpeditionSelected += SelectExpedition;
        ui.GarageRewardSelected += ConfigureCar;
        ui.QuizTypeSelected += ToggleQuizType;
        ui.MathLevelSelected += SetMathLevel;
        ui.GolemToggleRequested += ToggleGolem;
        ui.CollectorPetToggleRequested += ToggleCollectorPet;
        ui.GolemQuestRequested += BeginGolemQuest;
    }

    private void Start()
    {
        State = GameState.Exploring;
        vehicle.Active = true;
        ui.HideModal();
    }

    /// <summary>Stops the car and switches to an interaction state.</summary>
    private void EnterInteraction(GameState state, Animal? actor = null)
    {
        if (actor != null)
            nearby = actor;
        State = state;
        vehicle.Active = false;
    }

    /// <summary>Forgets the open question together with the wrong answers already tried.</summary>
    private void ClearChallenge()
    {
        CurrentChallenge = null;
        rejectedAnswers.Clear();
    }

    /// <summary>
    /// Removes the active expedition from the scene. With <paramref name="refreshUi"/> it also hides the
    /// delivery HUD and refreshes the task markers; callers that still change related state pass false.
    /// </summary>
    private void EndExpedition(bool refreshUi = true)
    {
        if (Delivery == null)
            return;
        Delivery.ReleaseCargo();
        RemoveChild(Delivery);
        Delivery.QueueFree();
        Delivery = null;
        if (!refreshUi)
            return;
        ui.DeliveryHud(null);
        UpdateDeliveryGuides();
    }

    private void CloseInteraction()
    {
        StopSpeech();
        DismissBandits();
        if (State == GameState.PostcardAlbum)
            CancelPostcardPhoto();
        if (State == GameState.Reward && deliveryReward && Delivery != null)
        {
            if (Delivery.Sender.IsTrader)
                world.Village.TradeCounts[Delivery.Sender.TradeIndex]++;
            EndExpedition();
        }
        deliveryReward = false;
        rewardCoins = 0;
        handoverQuiz = false;
        ClearChallenge();
        Start();
    }

    private void Resume()
    {
        State = beforePause;
        vehicle.Active = State == GameState.Exploring;
        if (!ShowPanel(State))
            ui.HideModal();
        if (celebrationPanel == State && celebrationTime > 0)
            ui.HideModal();
    }

    /// <summary>Title, driving and pause have no panel; every other state is an interaction that Back closes.</summary>
    private static bool IsInteraction(GameState state) =>
        state is not (GameState.Title or GameState.Exploring or GameState.Paused);

    /// <summary>
    /// Shows the panel of an interaction state from the current context, e.g. after pause, loading or a celebration.
    /// <paramref name="returned"/> marks a collection status shown right after a delivery.
    /// Returns false when the state has no panel or its context is missing.
    /// </summary>
    private bool ShowPanel(GameState state, bool returned = false)
    {
        switch (state)
        {
            case GameState.Challenge when nearby != null && CurrentChallenge != null:
                ShowChallenge();
                return true;
            case GameState.Reward when nearby != null:
                ShowRewards();
                return true;
            case GameState.ExpeditionChoice when nearby != null:
                ui.ExpeditionChoice(nearby, vehicle.Appearance);
                return true;
            case GameState.Garage:
                ShowGarage();
                return true;
            case GameState.HouseReward when nearby != null:
                ui.HouseReward(nearby, world.Village.Level(nearby.HouseIndex));
                return true;
            case GameState.HouseCollection when Delivery != null:
                ui.HouseCollection(Delivery.Sender, Delivery.Collected);
                return true;
            case GameState.HouseFruitOffer when nearby != null:
                ui.HouseFruitOffer(nearby, world.Village.Level(nearby.HouseIndex), !TaskActive);
                return true;
            case GameState.FarmStatus:
                ui.FarmStatus(world.Village, Delivery, returned);
                return true;
            case GameState.GardenStatus:
                ui.GardenStatus(world.Village, Delivery, returned);
                return true;
            case GameState.CollectionStatus when nearby != null:
                ui.CollectionStatus(world.Village, nearby, Delivery, returned);
                return true;
            case GameState.TradeStatus when nearby != null:
                ui.TradeStatus(nearby, Delivery);
                return true;
            case GameState.WeatherChoice:
                ShowWeatherChoice();
                return true;
            case GameState.SorcererStatus:
                ShowSorcerer();
                return true;
            case GameState.FordStatus when world.Beaver != null:
                ShowBeaver();
                return true;
            case GameState.PostcardAlbum when pendingPhoto < 0:
                ShowPostcards();
                return true;
            default:
                return false;
        }
    }

    public override void _Input(InputEvent e)
    {
        if (e.IsEcho())
            return;
        if (e.IsActionPressed("pause") && State != GameState.Title)
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("back") && State != GameState.Title)
        {
            if (State == GameState.Paused)
                Resume();
            else if (IsInteraction(State))
                CloseInteraction();
            else
                TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsEcho())
            return;
        if (State != GameState.Exploring)
            return;
        if (e.IsActionPressed("reset"))
        {
            vehicle.Reset();
            foreach (var pet in world.Village.CollectorPets)
                if (pet.IsActive)
                    PlaceCollectorPet(pet);
        }
        if (e.IsActionPressed("talk"))
            Interact();
        else if (
            e is InputEventKey { PhysicalKeycode: Key.Space, Pressed: true }
            && !AtGarage
            && nearby == null
            && NearbyPostcard() >= 0
        )
            InteractWithPostcards();
    }

    private void Interact()
    {
        if (State != GameState.Exploring)
            return;
        if (AtGarage)
        {
            OpenGarage();
            return;
        }
        if (nearby == null && InteractWithPostcards())
            return;
        if (InteractWithEvent())
            return;
        if (Haul is { Loaded: false, Arrived: true } && Haul.Reached(vehicle.Position))
            BeginLoading();
        else if (
            Delivery != null
            && Delivery.Arrived
            && vehicle.Position.DistanceTo(Delivery.Recipient.Position) < Settings.InteractionRange
        )
            BeginDeliveryQuestion();
        else if (nearby != null)
            OpenChallenge(nearby);
    }

    private void ShowRewards() => ui.CoinReward(rewardCoins);

    private void ShowGarage() =>
        ui.Garage(
            vehicle.Appearance,
            OwnedRewards,
            GolemOwned,
            GolemActive,
            world.Village.CollectorPets,
            enabledQuizzes,
            threeTermMath,
            Coins,
            world.Ford == null ? null : new GarageTrailer(TrailerOwned, trailerParade, trailer.PaintId)
        );

    internal void OpenGarage()
    {
        if (State != GameState.Exploring || !AtGarage)
            return;
        EnterInteraction(GameState.Garage);
        ShowGarage();
    }

    internal void ConfigureCar(CarReward reward)
    {
        if (State != GameState.Garage)
            return;
        // An item not owned yet is bought first and then equipped like an owned one.
        if (!OwnedRewards.Contains(reward) && !BuyCarItem(reward))
            return;
        if (reward.IsModel)
            vehicle.Appearance.ApplyModel(reward.Id);
        else if (reward.IsAccessory)
            vehicle.Appearance.ToggleAccessory(reward.Id);
        else
            vehicle.Appearance.Apply(CarLook.All.First(look => look.Id == reward.Id));
        ShowGarage();
    }

    internal void OfferDelivery(Animal sender)
    {
        if (State != GameState.Exploring || TaskActive || !roadside.CanStart(sender, Delivery))
            return;
        CurrentChallenge = null;
        EnterInteraction(GameState.ExpeditionChoice, sender);
        ui.ExpeditionChoice(sender, vehicle.Appearance);
        Speak("Teď si prosím vyber rozvoz nebo dovoz.", sender);
    }

    internal void SelectExpedition(bool collection)
    {
        if (State != GameState.ExpeditionChoice || nearby == null || TaskActive)
            return;
        StartExpedition(nearby, collection);
    }

    private void StartExpedition(Animal sender, bool collection)
    {
        Delivery = new Expedition(world, vehicle, sender, collection);
        AddChild(Delivery);
        ClearChallenge();
        lastCollected = Delivery.Collected;
        ui.DeliveryHud(Delivery);
        Start();
        UpdateDeliveryGuides();
        Speak(ExpeditionInstruction(Delivery), sender);
    }

    private bool CanChooseExpedition(Animal animal) => Haul == null && roadside.CanStart(animal, Delivery);

    private void UpdateDeliveryGuides()
    {
        bool golemReturning = Delivery?.Golem == true && Delivery.CanDeliver;
        world.Sorcerer.Guide(!GolemOwned || golemReturning, golemReturning);
        UpdateBeaverGuide();
        foreach (var animal in world.InteractionActors)
        {
            if (!animal.Role.UsesDeliveryGuide)
                continue;
            bool target = Delivery != null && Delivery.CanDeliver && animal == Delivery.Recipient;
            animal.Guide(animal.RewardAvailable || CanChooseExpedition(animal) || target, target);
        }
    }

    private bool AtRecipient(Expedition delivery) =>
        vehicle.Position.DistanceTo(delivery.Recipient.Position) < Settings.InteractionRange;

    internal void BeginDeliveryQuestion()
    {
        if (Delivery == null || !Delivery.CanDeliver || State != GameState.Exploring || !AtRecipient(Delivery))
            return;
        Delivery.Arrived = true;
        nearby = Delivery.Recipient;
        vehicle.Active = false;
        if (Delivery.Golem)
        {
            CompleteGolemQuest();
            return;
        }
        if (Delivery.Sender.IsTrader)
        {
            CompleteTrade();
            return;
        }
        if (Delivery.Sender.Role is CollectorRole collector)
        {
            CompleteCollectorDelivery(collector);
            return;
        }
        if (!enabledQuizzes.HasFlag(QuizType.Math))
        {
            BeginQuiz(handover: true);
            return;
        }
        ClearChallenge();
        CurrentChallenge = new DeliveryChallenge(Delivery.Cargo, Delivery.Amount);
        State = GameState.Challenge;
        ShowChallenge();
        Speak(QuizInstruction(nearby, CurrentChallenge), nearby);
    }

    internal void OpenChallenge(Animal animal)
    {
        if (State != GameState.Exploring)
            return;
        animal.Role.Open(this, animal);
    }

    internal void OpenRoadsideFriend(Animal animal)
    {
        if (!animal.RewardAvailable)
        {
            OfferDelivery(animal);
            return;
        }
        animal.SetRewardAvailable(false);
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);
        BeginChallenge(animal);
    }

    private void BeginChallenge(Animal animal)
    {
        nearby = animal;
        vehicle.Active = false;
        BeginQuiz();
    }

    internal void SubmitAnswer(int answer)
    {
        if (
            State != GameState.Challenge
            || banditArrival > 0
            || CurrentChallenge == null
            || nearby == null
            || !CurrentChallenge.Choices.Contains(answer)
        )
            return;
        if (CurrentChallenge is SyllableChallenge syllables)
        {
            var next = syllableSelection.Pick(syllables, answer);
            if (next == syllableSelection)
                return;
            syllableSelection = next;
            ShowChallenge();
            SaveProgress();
            return;
        }
        if (!CurrentChallenge.Accepts(answer))
        {
            rejectedAnswers.Add(answer);
            ShowChallenge();
            return;
        }
        CompleteChallenge();
    }

    internal void ConfirmSyllables()
    {
        if (State == GameState.PostcardAlbum)
        {
            ConfirmPostcardSyllables();
            return;
        }
        if (
            State == GameState.Challenge
            && banditArrival <= 0
            && CurrentChallenge is SyllableChallenge syllables
            && syllableSelection.IsComplete(syllables)
        )
            CompleteChallenge();
    }

    private void CompleteChallenge()
    {
        if (nearby == null)
            return;
        if (InBanditQuiz)
        {
            AdvanceBanditQuiz();
            return;
        }
        StopSpeech();
        bool handover = handoverQuiz || CurrentChallenge is DeliveryChallenge;
        if (handoverQuiz)
        {
            handoverQuiz = false;
            ClearChallenge();
        }
        if (nearby.Role.CompleteQuiz(this, nearby, handover))
            return;
        if (handover && Delivery?.Sender.HouseIndex >= 0)
        {
            CompleteHouseTask(Delivery.Sender);
            EndExpedition();
            SaveProgress();
            return;
        }
        deliveryReward = handover;
        if (!deliveryReward)
        {
            // A solved opening quiz unlocks the friend's expedition choice.
            var sender = nearby;
            roadside.Unlock(sender);
            sender.Celebrate();
            CloseInteraction();
            UpdateDeliveryGuides();
            OfferDelivery(sender);
            return;
        }
        if (Delivery != null)
        {
            roadside.Complete(Delivery.Sender);
            CelebrateCompletion(Delivery.Recipient, "Doručeno! Děkujeme za pomoc!", true);
        }
        PayReward(RoadsideDeliveryCoins);
    }

    /// <summary>Pays a finished delivery's coins and opens the reward panel that shows them.</summary>
    private void PayReward(int coins)
    {
        rewardCoins = coins;
        AddCoins(coins);
        State = GameState.Reward;
        ShowRewards();
        ShowCelebrationView();
        SaveProgress();
    }

    internal void TogglePause()
    {
        if (State == GameState.Paused)
        {
            Resume();
            return;
        }
        StopSpeech();
        beforePause = State;
        State = GameState.Paused;
        vehicle.Active = false;
        ui.Pause();
    }

    internal void FinishInteraction() => CloseInteraction();

    public override void _Process(double delta)
    {
        foreach (var animal in world.InteractionActors)
        {
            animal.AnimationPaused = State is GameState.Paused or GameState.Title;
            animal.GreetPlayer(vehicle.Position, State == GameState.Exploring);
        }
        if (State == GameState.Exploring)
            UpdateExploring(delta);
        else
            ui.DeliveryHint(false);
        if (State is not (GameState.Paused or GameState.Title))
            trailer.Tick((float)delta);
        TickCompanionStop((float)delta);
        TickGolem((float)delta);
        TickCollectorPets((float)delta);
        TickEvents(delta);
        TickPostcards(delta);
        TickBandits((float)delta);
        TickCoins(delta);
        saveTimer += delta;
        double saveInterval = State == GameState.Exploring ? 2 : .5;
        if (State != GameState.Title && (State != lastSavedState || saveTimer >= saveInterval))
            SaveProgress();
    }

    private void UpdateExploring(double delta)
    {
        Weather.Tick((float)delta);
        // Prefer a character with a pending task, then the closest one.
        nearby = world
            .InteractionActors.Where(a => a.Position.DistanceTo(vehicle.Position) < Settings.InteractionRange)
            .OrderByDescending(a => a.RewardAvailable || CanChooseExpedition(a))
            .ThenBy(a => a.Position.DistanceSquaredTo(vehicle.Position))
            .FirstOrDefault();
        if (Delivery != null)
        {
            Delivery.Tick(delta);
            ui.DeliveryDirection(Delivery.Waypoint, vehicle);
            if (lastCollected != Delivery.Collected)
            {
                lastCollected = Delivery.Collected;
                ui.DeliveryHud(Delivery);
                UpdateDeliveryGuides();
                if (Delivery.CanDeliver)
                    Speak("Náklad je na autě. Teď ho dovez zpátky.");
                SaveProgress();
            }
            bool atRecipient = Delivery.CanDeliver && AtRecipient(Delivery);
            ui.DeliveryHint(
                nearby != null && (nearby.RewardAvailable || nearby.Role.InvitesTalk(expeditionActive: true))
                    || atRecipient
            );
            if (atRecipient && !Delivery.Arrived)
                BeginDeliveryQuestion();
        }
        else
        {
            TickHaul(delta);
            ui.DeliveryHint(
                nearby != null
                    && (
                        nearby.RewardAvailable
                        || nearby.Role.InvitesTalk(expeditionActive: Haul != null)
                        || CanChooseExpedition(nearby)
                    )
                    || Haul is { Loaded: false, Arrived: true }
            );
        }
        if (State != GameState.Exploring)
            return;
        nearby?.Role.ShowHint(ui, nearby);
        if (AtGarage)
            ui.GarageHint();
    }
}
