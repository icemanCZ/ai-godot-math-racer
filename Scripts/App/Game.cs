using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Ui;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.App;

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
    BuildStatus = 17,
    WorksStatus = 18,
    SourceStatus = 19,
    HouseMaterialOffer = 20,
    PlotStatus = 21,
    PlaygroundStatus = 22,
    MayorShop = 23,
    GlassmakerShop = 24,
    RailwayStatus = 25,
}

public partial class Game : Node3D
{
    private readonly Interaction interaction = new();

    public GameState State
    {
        get => interaction.State;
        private set => interaction.State = value;
    }

    private readonly World world = new();
    private readonly Vehicle vehicle = new();
    private readonly GameUi ui = new();
    private Character? nearby
    {
        get => interaction.Nearby;
        set => interaction.Nearby = value;
    }

    private readonly RoadsideProgress roadside = new();
    private readonly Economy economy = new();
    private bool deliveryReward;
    private int lastCollected = -1;
    internal int RemainingDeliveries => world.Animals.Length - roadside.CompletedCount;
    internal int RemainingAnimals => world.Animals.Count(animal => animal.RewardAvailable);
    private readonly QuizSession quiz = new();

    internal IChallenge? CurrentChallenge
    {
        get => quiz.Current;
        private set => quiz.Current = value;
    }
    internal HashSet<CarReward> OwnedRewards => economy.Owned;
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

        persistenceReady = true;
        bool skipTitle = false;
        DiagnosticsSkipTitle(ref skipTitle);
        if (Settings.PendingNewGameQuizzes is { } quizSettings)
        {
            (quiz.Enabled, quiz.ThreeTermMath) = quizSettings;
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
        ui.Intended += HandleIntent;
        ui.Title(SaveStore.Initial != null);
    }

    private void Start()
    {
        State = GameState.Exploring;
        vehicle.Active = true;
        ui.HideModal();
    }

    /// <summary>Stops the car and switches to an interaction state.</summary>
    private void EnterInteraction(GameState state, Character? actor = null)
    {
        interaction.Enter(state, actor);
        vehicle.Active = false;
    }

    /// <summary>Forgets the open question together with the wrong answers already tried.</summary>
    private void ClearChallenge()
    {
        quiz.Clear();
    }

    private void CloseInteraction()
    {
        StopSpeech();
        DismissBandits();
        if (State == GameState.PostcardAlbum)
            CancelPostcardPhoto();
        if (State == GameState.Reward && deliveryReward && Delivery != null)
        {
            if (Delivery.Sender.Role is TraderRole shop)
                world.Village.TradeCounts[shop.Index]++;
            EndExpedition();
        }
        deliveryReward = false;
        economy.RewardCoins = 0;
        quiz.Handover = false;
        ClearChallenge();
        Start();
    }

    private void Resume()
    {
        interaction.Unpause();
        vehicle.Active = State == GameState.Exploring;
        if (!ShowPanel(State))
            ui.HideModal();
        if (celebrationPanel == State && celebrationTime > 0)
            ui.HideModal();
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
            else if (Interaction.IsInteraction(State))
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
            foreach (var pet in world.AllPets)
                if (pet.IsActive)
                    PlaceCollectorPet(pet);
        }
        if (e.IsActionPressed("talk"))
            Interact();
    }

    internal void Interact()
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
        if (LampPickupAvailable && TakeUpLamp())
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
        else if (Lamps.Carried.Count > 0)
            PutDownLamp();
    }

    private void ShowRewards() => ui.CoinReward(economy.RewardCoins);

    private void ShowGarage() =>
        ui.Garage(
            vehicle.Appearance,
            OwnedRewards,
            GolemOwned,
            GolemActive,
            world.AllPets,
            quiz.Enabled,
            quiz.ThreeTermMath,
            Coins,
            world.Ford == null ? null : new GarageTrailer(TrailerOwned, trailerParade, trailer.PaintId)
        );

    internal void OpenGarage()
    {
        if (State != GameState.Exploring || !AtGarage)
            return;
        EnterInteraction(GameState.Garage);
        ShowGarage();
        Speak(Texts.GarazVyberSiAutoDruhyUkolu);
    }

    internal void ConfigureCar(CarReward reward)
    {
        if (State != GameState.Garage)
            return;
        // An item not owned yet is bought first and then equipped like an owned one; the plain tyre marks are free.
        if (reward.IsTrail && reward.Id == CarTrail.Plain.Id)
        {
            vehicle.Appearance.ApplyTrail(reward.Id);
            ShowGarage();
            return;
        }
        if (!OwnedRewards.Contains(reward) && !BuyCarItem(reward))
            return;
        if (reward.IsTrail)
        {
            vehicle.Appearance.ApplyTrail(reward.Id);
            ShowGarage();
            return;
        }
        if (reward.IsModel)
            vehicle.Appearance.ApplyModel(reward.Id);
        else if (reward.IsAccessory)
            vehicle.Appearance.ToggleAccessory(reward.Id);
        else
            vehicle.Appearance.Apply(CarLook.Catalogue.First(look => look.Id == reward.Id));
        ShowGarage();
    }

    internal void OfferDelivery(Character sender)
    {
        if (State != GameState.Exploring || !roadside.CanStart(sender, Delivery))
            return;
        CurrentChallenge = null;
        EnterInteraction(GameState.ExpeditionChoice, sender);
        ui.ExpeditionChoice(sender, vehicle.Appearance);
        Speak(Texts.TedSiProsimVyberRozvozNebo, sender);
    }

    internal void SelectExpedition(bool collection)
    {
        if (State != GameState.ExpeditionChoice || nearby == null || !roadside.CanStart(nearby, Delivery))
            return;
        StartExpedition(nearby, collection);
    }

    private void StartExpedition(Character sender, bool collection)
    {
        CancelActiveTask();
        tasks.Begin(new Expedition(world, vehicle, sender, collection));
        ClearChallenge();
        lastCollected = Delivery.Collected;
        ui.DeliveryHud(Delivery);
        Start();
        UpdateDeliveryGuides();
        Speak(ExpeditionInstruction(Delivery), sender);
    }

    private bool CanChooseExpedition(Character animal) => roadside.CanStart(animal, Delivery);

    private void UpdateDeliveryGuides()
    {
        bool golemReturning = Delivery?.Golem == true && Delivery.CanDeliver;
        world.Sorcerer.Guide(!GolemOwned || golemReturning, golemReturning);
        UpdateHaulGuides();
        foreach (var animal in world.InteractionActors)
        {
            if (!animal.Role.UsesDeliveryGuide)
                continue;
            bool target =
                Delivery != null && Delivery.CanDeliver && animal == Delivery.Recipient
                || Haul is { House: >= 0, Loaded: true } houseTrip && animal == houseTrip.Contractor;
            bool glassOrder =
                Haul == null
                && animal.Role is HouseResidentRole { House: var house }
                && HouseMaterial(house) is HaulLoad.Glass or HaulLoad.Bread;
            animal.Guide(animal.RewardAvailable || CanChooseExpedition(animal) || target || glassOrder, target);
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
        if (Delivery.Sender.Role is TraderRole)
        {
            CompleteTrade();
            return;
        }
        if (Delivery.Sender.Role is CollectorRole collector)
        {
            CompleteCollectorDelivery(collector);
            return;
        }
        if (!quiz.Enabled.HasFlag(QuizType.Math))
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

    internal void OpenChallenge(Character animal)
    {
        if (State != GameState.Exploring)
            return;
        animal.Role.Open(this, animal);
    }

    internal void OpenRoadsideFriend(Character animal)
    {
        if (!animal.RewardAvailable)
        {
            OfferDelivery(animal);
            return;
        }
        animal.SetRewardAvailable(false);
        BeginChallenge(animal);
    }

    private void BeginChallenge(Character animal)
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
            var next = quiz.Selection.Pick(syllables, answer);
            if (next == quiz.Selection)
                return;
            quiz.Selection = next;
            ShowChallenge();
            SaveProgress();
            return;
        }
        if (!CurrentChallenge.Accepts(answer))
        {
            quiz.Rejected.Add(answer);
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
            && quiz.Selection.IsComplete(syllables)
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
        bool handover = quiz.Handover || CurrentChallenge is DeliveryChallenge;
        if (quiz.Handover)
        {
            quiz.Handover = false;
            ClearChallenge();
        }
        // A solved quiz that was asked before a trip lets the trip start.
        if (quiz.Departure != Departure.None)
        {
            var trip = quiz.Departure;
            quiz.Departure = Departure.None;
            StartDeparture(trip);
            return;
        }
        // A solved quiz with the collector who holds the special load of the trip under way hands the load over.
        if (Haul is { Loaded: false } pickup && HaulLoads.IsSpecial(pickup.Load) && pickup.Asker == nearby)
        {
            CompleteHaulLoading();
            return;
        }
        if (nearby.Role.CompleteQuiz(this, nearby, handover))
            return;
        if (handover && Delivery?.Sender.Role is HouseResidentRole)
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
            CelebrateCompletion(Delivery.Recipient, Texts.DorucenoDekujemeZaPomoc, true);
        }
        PayReward(RoadsideDeliveryCoins);
    }

    /// <summary>Pays a finished delivery's coins and opens the reward panel that shows them.</summary>
    private void PayReward(int coins)
    {
        economy.RewardCoins = coins;
        AddCoins(coins);
        Speak(Texts.CoinsGained(coins), null, interrupt: false);
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
        interaction.Pause();
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
        UnhitchIdleTrailer();
        foreach (var system in systems)
            system.Tick(delta);
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
                    Speak(Texts.NakladJeNaAuteTedHo);
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
        else if (LampPickupAvailable && LampNearby() is { } lamp)
            ui.LampPickupHint(GlassLamps.Find(Lamps.KindAt(lamp))?.Name ?? "");
        else if (LampPickupAvailable && Lamps.Carried.Count > 0)
            ui.LampPutHint();
    }
}
