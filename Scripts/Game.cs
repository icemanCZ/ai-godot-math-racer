using Godot;
using System;
using System.Linq;

namespace MathRacer;

public enum GameState
{
    // SaveData persists these numeric values.
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
    SorcererStatus = 16
}

public partial class Game : Node3D
{
    public GameState State { get; private set; } = GameState.Title;
    private readonly World world = new();
    private readonly Vehicle vehicle = new();
    private readonly GameUi ui = new();
    private Animal? nearby;
    internal Expedition? Delivery { get; private set; }
    private readonly System.Collections.Generic.HashSet<Animal> completedDeliveries = new();
    private readonly System.Collections.Generic.HashSet<Animal> unlockedExpeditions = new();
    private bool deliveryReward;
    private int lastCollected = -1;
    internal int RemainingDeliveries => world.Animals.Length - completedDeliveries.Count;
    internal int RemainingAnimals => world.Animals.Count(animal => animal.RewardAvailable);
    private GameState beforePause;
    private readonly ChallengeGenerator challenges = new(Settings.Seed + 204);
    private readonly System.Collections.Generic.HashSet<int> rejectedAnswers = new();
    internal IChallenge? CurrentChallenge { get; private set; }
    internal System.Collections.Generic.IReadOnlyList<CarReward> CurrentRewards { get; private set; } = Array.Empty<CarReward>();
    private System.Collections.Generic.IReadOnlyList<CarReward> previousRewards = Array.Empty<CarReward>();
    private readonly Random rewardRandom = new();
    internal System.Collections.Generic.HashSet<CarReward> OwnedRewards { get; } = new()
    {
        new(CarLook.All[0].Id, false), new("classic", false, true)
    };
    private bool AtGarage => vehicle.Position.DistanceTo(world.Village.GarageEntrance) < Settings.InteractionRange;
    public override void _Ready()
    {
        GameControls.Setup();
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--road-audit"))
        {
            RunRoadAudit();
            return;
        }

        AddChild(world);
        world.Generate();
        AddChild(vehicle);
        vehicle.Setup(world.Ground, world.Routes);
        if (args.Contains("--road-driving-checks"))
        {
            StartRoadDrivingChecks();
            return;
        }

        world.AddChild(Events);
        Events.Generate(world);
        SetupPostcards();
        SetupBandits();
        world.AddChild(world.Wildlife);
        world.Wildlife.Generate(world, vehicle);
        var camera = new FollowCamera { Target = vehicle, Ground = world.Ground, Fov = 62, Far = 1200, Current = true };
        AddChild(camera);
        followCamera = camera;
        camera.Snap();
        world.AddChild(Weather);
        Weather.Setup(world, vehicle, camera);
        world.AddChild(world.Grass);
        world.Grass.Setup(world, vehicle);
        SetupUi();
        SetupGolem();
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);

        persistenceReady = true;
        if (newGameQuizzes is { } quizSettings)
        {
            (enabledQuizzes, threeTermMath) = quizSettings;
            newGameQuizzes = null;
            Start();
            SaveProgress();
        }
        else if (ContinueOnStartup(args)) ContinueGame();

        AttachDiagnostics(args, camera);
        GD.Print($"WORLD_READY seed={Settings.Seed} animals={world.Animals.Length} road_samples={world.Routes.Samples.Count}");
    }

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
        ui.LookSelected += SelectLook;
        ui.AccessorySelected += SelectAccessory;
        ui.ModelSelected += SelectModel;
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
    private void Start() { State = GameState.Exploring; vehicle.Active = true; ui.HideModal(); }
    private void CloseInteraction()
    {
        StopSpeech();
        DismissBandits();
        if (State == GameState.PostcardAlbum) CancelPostcardPhoto();
        if (State == GameState.Reward && deliveryReward && Delivery != null)
        {
            if (Delivery.Sender.IsTrader) world.Village.TradeCounts[Delivery.Sender.TradeIndex]++;
            Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null; ui.DeliveryHud(null);
            UpdateDeliveryGuides();
        }
        deliveryReward = false;
        handoverQuiz = false;
        CurrentChallenge = null; rejectedAnswers.Clear(); Start();
    }
    private void Resume()
    {
        State = beforePause; vehicle.Active = State == GameState.Exploring;
        if (State == GameState.Challenge && nearby != null && CurrentChallenge != null) ShowChallenge();
        else if (State == GameState.Reward && nearby != null) ShowRewards();
        else if (State == GameState.ExpeditionChoice && nearby != null) ui.ExpeditionChoice(nearby, vehicle.Appearance);
        else if (State == GameState.Garage) ShowGarage();
        else if (State == GameState.HouseReward && nearby != null) ui.HouseReward(nearby, world.Village.Level(nearby.HouseIndex));
        else if (State == GameState.HouseCollection && Delivery != null) ui.HouseCollection(Delivery.Sender, Delivery.Collected);
        else if (State == GameState.HouseFruitOffer && nearby != null) ui.HouseFruitOffer(nearby, world.Village.Level(nearby.HouseIndex), Delivery == null);
        else if (State == GameState.FarmStatus) ui.FarmStatus(world.Village, Delivery);
        else if (State == GameState.GardenStatus) ui.GardenStatus(world.Village, Delivery);
        else if (State == GameState.CollectionStatus && nearby != null) ui.CollectionStatus(world.Village, nearby, Delivery);
        else if (State == GameState.TradeStatus && nearby != null) ui.TradeStatus(nearby, Delivery);
        else if (State == GameState.WeatherChoice) ShowWeatherChoice();
        else if (State == GameState.SorcererStatus) ShowSorcerer();
        else if (State == GameState.PostcardAlbum && pendingPhoto < 0) ShowPostcards();
        else ui.HideModal();
        if (celebrationPanel == State && celebrationTime > 0) ui.HideModal();
    }
    public override void _Input(InputEvent e)
    {
        if (e.IsEcho()) return;
        if (e.IsActionPressed("pause") && State != GameState.Title)
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed("back") && State != GameState.Title)
        {
            if (State == GameState.Paused) Resume();
            else if (State is GameState.Challenge or GameState.Reward or GameState.ExpeditionChoice or GameState.Garage or GameState.HouseReward or GameState.HouseCollection or GameState.HouseFruitOffer or GameState.FarmStatus or GameState.GardenStatus or GameState.CollectionStatus or GameState.TradeStatus or GameState.PostcardAlbum or GameState.WeatherChoice or GameState.SorcererStatus) CloseInteraction();
            else TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsEcho()) return;
        if (State != GameState.Exploring) return;
        if (e.IsActionPressed("reset"))
        {
            vehicle.Reset();
            foreach (var pet in world.Village.CollectorPets)
                if (pet.IsActive) PlaceCollectorPet(pet);
        }
        if (e.IsActionPressed("talk")) Interact();
        else if (e is InputEventKey { PhysicalKeycode: Key.Space, Pressed: true } &&
            !AtGarage && nearby == null && NearbyPostcard() >= 0)
            InteractWithPostcards();
    }
    private void Interact()
    {
        if (State != GameState.Exploring) return;
        if (AtGarage) { OpenGarage(); return; }
        if (nearby == null && InteractWithPostcards()) return;
        if (InteractWithEvent()) return;
        if (Delivery != null && Delivery.Arrived && vehicle.Position.DistanceTo(Delivery.Recipient.Position) < Settings.InteractionRange) BeginDeliveryQuestion();
        else if (nearby != null) OpenChallenge(nearby);
    }
    private void ShowRewards() => ui.Reward(nearby!, vehicle.Appearance, CurrentRewards);
    private void ShowGarage() => ui.Garage(vehicle.Appearance, OwnedRewards, GolemOwned, GolemActive, world.Village.CollectorPets, enabledQuizzes, threeTermMath);
    internal void OpenGarage()
    {
        if (State != GameState.Exploring || !AtGarage) return;
        State = GameState.Garage; vehicle.Active = false; ShowGarage();
    }
    internal void ConfigureCar(CarReward reward)
    {
        if (State != GameState.Garage || !OwnedRewards.Contains(reward)) return;
        if (reward.IsModel) vehicle.Appearance.ApplyModel(reward.Id);
        else if (reward.IsAccessory) vehicle.Appearance.ToggleAccessory(reward.Id);
        else vehicle.Appearance.Apply(CarLook.All.First(look => look.Id == reward.Id));
        ShowGarage();
    }
    internal void OfferDelivery(Animal sender)
    {
        if (State != GameState.Exploring || Delivery != null || !unlockedExpeditions.Contains(sender) || completedDeliveries.Contains(sender)) return;
        nearby = sender; CurrentChallenge = null;
        State = GameState.ExpeditionChoice; vehicle.Active = false;
        ui.ExpeditionChoice(sender, vehicle.Appearance);
        Speak("Teď si prosím vyber rozvoz nebo dovoz.");
    }
    internal void SelectExpedition(bool collection)
    {
        if (State != GameState.ExpeditionChoice || nearby == null || Delivery != null) return;
        StartExpedition(nearby, collection);
    }
    private void StartExpedition(Animal sender, bool collection)
    {
        Delivery = new Expedition(world, vehicle, sender, collection);
        AddChild(Delivery);
        CurrentChallenge = null; rejectedAnswers.Clear();
        lastCollected = Delivery.Collected;
        ui.DeliveryHud(Delivery); Start();
        UpdateDeliveryGuides();
        Speak(ExpeditionInstruction(Delivery));
    }
    private bool CanChooseExpedition(Animal animal) => unlockedExpeditions.Contains(animal) && !completedDeliveries.Contains(animal) && Delivery?.Sender != animal;
    private void UpdateDeliveryGuides()
    {
        world.Sorcerer.Guide(!GolemOwned || (Delivery?.Golem == true && Delivery.CanDeliver), Delivery?.Golem == true && Delivery.CanDeliver);
        foreach (var animal in world.InteractionActors)
        {
            if (animal.IsWitch || animal.IsSorcerer) continue;
            bool target = Delivery != null && Delivery.CanDeliver && animal == Delivery.Recipient;
            animal.Guide(animal.RewardAvailable || CanChooseExpedition(animal) || target, target);
        }
    }
    internal void BeginDeliveryQuestion()
    {
        if (Delivery == null || !Delivery.CanDeliver || State != GameState.Exploring || vehicle.Position.DistanceTo(Delivery.Recipient.Position) >= Settings.InteractionRange) return;
        Delivery.Arrived = true; nearby = Delivery.Recipient; vehicle.Active = false;
        if (Delivery.Golem) { CompleteGolemQuest(); return; }
        if (Delivery.Sender.IsTrader) { CompleteTrade(); return; }
        if (Delivery.SpecialCollection)
        {
            bool collection = Delivery.Gems || Delivery.Toys;
            bool toys = Delivery.Toys;
            bool garden = Delivery.Garden;
            var pet = world.Village.PetFor(nearby);
            int count = pet.Count + 1;
            string message = count == pet.Goal ? $"Nový mazlíček: {pet.DisplayName}!" : collection
                ? "Nový poklad ve sbírce! Děkujeme!"
                : garden ? "Nová květina na zahrádce!" : "Zvířátko je doma! Děkujeme!";
            CelebrateCompletion(nearby, message, true);
            Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null;
            if (collection) world.Village.RestoreCollection(toys, count);
            else if (garden) world.Village.RestoreGarden(count);
            else world.Village.RestoreFarm(count);
            CurrentChallenge = null; rejectedAnswers.Clear(); ui.DeliveryHud(null); UpdateDeliveryGuides();
            State = collection ? GameState.CollectionStatus : garden ? GameState.GardenStatus : GameState.FarmStatus;
            if (collection) ui.CollectionStatus(world.Village, nearby, null, true);
            else if (garden) ui.GardenStatus(world.Village, null, true);
            else ui.FarmStatus(world.Village, null, true);
            ShowCelebrationView();
            SaveProgress(); return;
        }
        if (!enabledQuizzes.HasFlag(QuizType.Math))
        {
            BeginQuiz(handover: true);
            return;
        }
        CurrentChallenge = new DeliveryChallenge(Delivery.Cargo, Delivery.Amount); rejectedAnswers.Clear();
        State = GameState.Challenge; ShowChallenge();
        Speak(QuizInstruction(nearby, CurrentChallenge));
    }
    internal void OpenChallenge(Animal animal)
    {
        if (State != GameState.Exploring) return;
        if (animal.IsSorcerer) { OpenSorcerer(); return; }
        if (animal.IsWitch) { OpenWeatherChallenge(); return; }
        if (animal.IsTrader) { OpenTrade(animal); return; }
        if (animal.IsFarmer) { OpenFarm(); return; }
        if (animal.IsGardener) { OpenGarden(); return; }
        if (animal.IsCollectionOwner) { OpenCollection(animal); return; }
        if (animal.HouseIndex >= 0)
        {
            if (Delivery?.Sender == animal)
            {
                if (Delivery.CanDeliver) { BeginDeliveryQuestion(); return; }
                nearby = animal; CurrentChallenge = null; rejectedAnswers.Clear();
                State = GameState.HouseCollection; vehicle.Active = false;
                ui.HouseCollection(animal, Delivery.Collected); return;
            }
            if (!animal.RewardAvailable)
            {
                nearby = animal; State = GameState.HouseReward; vehicle.Active = false;
                ui.HouseReward(animal, world.Village.Level(animal.HouseIndex)); return;
            }
            if (world.Village.TaskAt(animal.HouseIndex) == VillageTaskKind.Fruit)
            {
                nearby = animal; CurrentChallenge = null; rejectedAnswers.Clear();
                State = GameState.HouseFruitOffer; vehicle.Active = false;
                ui.HouseFruitOffer(animal, world.Village.Level(animal.HouseIndex), Delivery == null); return;
            }
            BeginChallenge(animal); return;
        }
        if (!animal.RewardAvailable) { OfferDelivery(animal); return; }
        animal.SetRewardAvailable(false);
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);
        BeginChallenge(animal);
    }
    private void BeginChallenge(Animal animal)
    {
        nearby = animal; vehicle.Active = false;
        BeginQuiz();
    }

    internal void SubmitAnswer(int answer)
    {
        if (State != GameState.Challenge || banditArrival > 0 || CurrentChallenge == null || nearby == null || !CurrentChallenge.Choices.Contains(answer)) return;
        if (CurrentChallenge is SyllableChallenge syllables)
        {
            var next = syllableSelection.Pick(syllables, answer);
            if (next == syllableSelection) return;
            syllableSelection = next;
            ShowChallenge();
            SaveProgress();
            return;
        }
        else if (!CurrentChallenge.Accepts(answer))
        {
            rejectedAnswers.Add(answer);
            ShowChallenge();
            return;
        }
        CompleteChallenge();
    }

    internal void ConfirmSyllables()
    {
        if (State == GameState.PostcardAlbum) { ConfirmPostcardSyllables(); return; }
        if (State == GameState.Challenge && banditArrival <= 0 && CurrentChallenge is SyllableChallenge syllables &&
            syllableSelection.IsComplete(syllables))
            CompleteChallenge();
    }

    private void CompleteChallenge()
    {
        if (nearby == null) return;
        if (InBanditQuiz) { AdvanceBanditQuiz(); return; }
        StopSpeech();
        bool handover = handoverQuiz || CurrentChallenge is DeliveryChallenge;
        if (handoverQuiz)
        {
            handoverQuiz = false;
            CurrentChallenge = null; rejectedAnswers.Clear();
        }
        if (nearby.IsWitch)
        {
            CurrentChallenge = null; rejectedAnswers.Clear();
            State = GameState.WeatherChoice; ShowWeatherChoice();
            Speak("Teď prosím vyber počasí.");
            return;
        }
        if (nearby.IsTrader) { StartTrade(); return; }
        if (nearby.IsFarmer || nearby.IsGardener || nearby.IsCollectionOwner)
        {
            int count = nearby.IsFarmer ? world.Village.FarmCount : nearby.IsGardener ? world.Village.GardenCount : world.Village.CollectionCount(nearby.IsToymaker);
            int total = nearby.IsFarmer ? Village.FarmKinds.Length : nearby.IsGardener ? Village.FlowerNames.Length : Village.CollectionNames(nearby.IsToymaker).Length;
            if (Delivery != null || count >= total) return;
            StartExpedition(nearby, true); SaveProgress(); return;
        }
        if (handover && Delivery?.Sender.HouseIndex >= 0)
        {
            var owner = Delivery.Sender;
            CompleteHouseTask(owner);
            Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null; ui.DeliveryHud(null); UpdateDeliveryGuides(); SaveProgress(); return;
        }
        if (nearby.HouseIndex >= 0 && !handover)
        {
            CompleteHouseTask(nearby); SaveProgress(); return;
        }
        deliveryReward = handover;
        if (!deliveryReward)
        {
            var sender = nearby;
            unlockedExpeditions.Add(sender); sender.Celebrate();
            CurrentRewards = Array.Empty<CarReward>();
            CloseInteraction(); UpdateDeliveryGuides(); OfferDelivery(sender);
            return;
        }
        if (deliveryReward && Delivery != null)
        {
            completedDeliveries.Add(Delivery.Sender);
            CelebrateCompletion(Delivery.Recipient, "Doručeno! Děkujeme za pomoc!", true);
        }
        CurrentRewards = CarReward.Draw(rewardRandom, previousRewards); previousRewards = CurrentRewards;
        State = GameState.Reward; ShowRewards(); ShowCelebrationView();
    }
    internal void SelectLook(string id)
    {
        if (State != GameState.Reward || !CurrentRewards.Contains(new CarReward(id, false))) return;
        var look = CarLook.All.FirstOrDefault(option => option.Id == id);
        if (look == null) return;
        OwnedRewards.Add(new(id, false)); vehicle.Appearance.Apply(look); CloseInteraction();
    }
    internal void SelectModel(string id)
    {
        if (State != GameState.Reward || !CurrentRewards.Contains(new CarReward(id, false, true))) return;
        if (!CarBody.All.Any(body => body.Id == id)) return;
        OwnedRewards.Add(new(id, false, true)); vehicle.Appearance.ApplyModel(id); CloseInteraction();
    }
    internal void TogglePause()
    {
        if (State == GameState.Paused) Resume();
        else { StopSpeech(); beforePause = State; State = GameState.Paused; vehicle.Active = false; ui.Pause(); }
    }
    internal void SelectAccessory(string id)
    {
        if (State != GameState.Reward || !CurrentRewards.Contains(new CarReward(id, true))) return;
        OwnedRewards.Add(new(id, true)); vehicle.Appearance.ToggleAccessory(id); CloseInteraction();
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
        {
            Weather.Tick((float)delta);
            nearby = world.InteractionActors.Where(a => a.Position.DistanceTo(vehicle.Position) < Settings.InteractionRange)
                .OrderByDescending(a => a.RewardAvailable || CanChooseExpedition(a)).ThenBy(a => a.Position.DistanceSquaredTo(vehicle.Position)).FirstOrDefault();
            if (Delivery != null)
            {
                Delivery.Tick(delta); ui.DeliveryDirection(Delivery.Waypoint, vehicle);
                if (lastCollected != Delivery.Collected)
                {
                    lastCollected = Delivery.Collected; ui.DeliveryHud(Delivery); UpdateDeliveryGuides();
                    if (Delivery.CanDeliver) Speak("Náklad je na autě. Teď ho dovez zpátky.");
                    SaveProgress();
                }
                bool atRecipient = Delivery.CanDeliver && vehicle.Position.DistanceTo(Delivery.Recipient.Position) < Settings.InteractionRange;
                ui.DeliveryHint((nearby?.RewardAvailable ?? false) || nearby?.HouseIndex >= 0 || atRecipient);
                if (atRecipient && !Delivery.Arrived) BeginDeliveryQuestion();
            }
            else ui.DeliveryHint(nearby != null && (nearby.IsFarmer || nearby.IsGardener || nearby.IsCollectionOwner || nearby.RewardAvailable || nearby.HouseIndex >= 0 || CanChooseExpedition(nearby)));
            if (nearby?.HouseIndex >= 0) ui.HouseHint();
            if (nearby?.IsFarmer == true) ui.FarmHint();
            if (nearby?.IsGardener == true) ui.GardenHint();
            if (nearby?.IsCollectionOwner == true) ui.CollectionHint(nearby.IsToymaker);
            if (nearby?.IsTrader == true) ui.TradeHint(nearby);
            if (nearby?.IsWitch == true) ui.WitchHint();
            if (nearby?.IsSorcerer == true) ui.SorcererHint();
            if (AtGarage) ui.GarageHint();
        }
        else ui.DeliveryHint(false);
        TickCompanionStop((float)delta);
        TickGolem((float)delta);
        TickCollectorPets((float)delta);
        TickEvents(delta);
        TickPostcards(delta);
        TickBandits((float)delta);
        saveTimer += delta;
        if (State != GameState.Title && (State != lastSavedState || saveTimer >= (State == GameState.Exploring ? 2 : .5))) SaveProgress();
    }
}


