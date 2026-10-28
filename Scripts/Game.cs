using Godot;
using System;
using System.Linq;

namespace MathRacer;

public enum GameState { Title, Exploring, Challenge, Reward, Paused, ExpeditionChoice, Garage, HouseReward, HouseCollection, HouseFruitOffer, FarmStatus, GardenStatus, CollectionStatus }

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
    private readonly LetterChallengeGenerator letterChallenges = new(Settings.Seed + 205);
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
    private string feedback = "Nikam nespěcháme. Můžeš si to spočítat na prstech.";
    public override void _Ready()
    {
        GameControls.Setup();
        if (OS.GetCmdlineUserArgs().Contains("--road-audit"))
        {
            SetProcess(false);
            try
            {
                AddChild(world.Ground); world.Ground.Generate();
                AddChild(world.Routes); world.Routes.Generate(world.Ground);
                WorldLayoutChecks.ValidatePlan(WorldLayout.Plan, Settings.Seed);
                RoadPassabilityChecks.Run(world);
                GetTree().Quit();
            }
            catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
            return;
        }
        AddChild(world); world.Generate(); AddChild(vehicle); vehicle.Setup(world.Ground, world.Routes);
        if (OS.GetCmdlineUserArgs().Contains("--road-driving-checks"))
        {
            SetProcess(false);
            vehicle.Active = true;
            RoadPassabilityChecks.Run(world);
            AddChild(new RoadDrivingChecks(world, vehicle));
            return;
        }
        world.AddChild(world.Wildlife); world.Wildlife.Generate(world, vehicle);
        var camera = new FollowCamera { Target = vehicle, Ground = world.Ground, Fov = 62, Far = 1200, Current = true }; AddChild(camera); camera.Snap();
        world.AddChild(world.Grass); world.Grass.Setup(world, vehicle);
        AddChild(ui); ui.Setup(); ui.PlayRequested += ContinueGame; ui.ResumeRequested += Resume; ui.CloseInteractionRequested += CloseInteraction; ui.Title(SaveStore.Initial != null);
        ui.NewGameRequested += NewGame;
        ui.AnswerSelected += SubmitAnswer; ui.LookSelected += SelectLook;
        ui.AccessorySelected += SelectAccessory;
        ui.ModelSelected += SelectModel;
        ui.TalkRequested += Interact;
        ui.VillageFruitRequested += BeginVillageCollection;
        ui.ExpeditionSelected += SelectExpedition;
        ui.GarageRewardSelected += ConfigureCar;
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);
        var args = OS.GetCmdlineUserArgs();
        persistenceReady = true;
        if (newGamePending) { newGamePending = false; Start(); SaveProgress(); }
        else if ((args.Contains("--play") && !args.Contains("--view=title")) || args.Contains("--smoke-test") || args.Contains("--track-checks") || args.Contains("--collision-checks")) ContinueGame();
        if (args.Contains("--collision-checks")) AddChild(new CollisionChecks(vehicle, world));
        if (args.Contains("--track-checks")) AddChild(new WheelTrackChecks(vehicle, world.Ground, world.Routes));
        if (args.Contains("--smoke-test")) AddChild(new SmokeTest(this, world, vehicle));
        if (args.Contains("--controller-checks")) AddChild(new ControllerChecks(this, world, vehicle));
        if (args.Contains("--delivery-checks")) { Start(); AddChild(new DeliveryChecks(this, world, vehicle)); }
        if (args.Contains("--garage-checks")) { Start(); AddChild(new GarageChecks(this, world, vehicle)); }
        if (args.Contains("--village-checks")) { Start(); AddChild(new VillageChecks(this, world, vehicle)); }
        if (args.Contains("--farm-checks")) { Start(); AddChild(new FarmChecks(this, world, vehicle)); }
        if (args.Contains("--garden-checks")) { Start(); AddChild(new GardenChecks(this, world, vehicle)); }
        if (args.Contains("--collection-checks")) { Start(); AddChild(new CollectionChecks(this, world, vehicle)); }
        if (args.Contains("--save-checks")) { Start(); AddChild(new SaveChecks(this, world, vehicle)); }
        if (args.Contains("--new-game-checks")) AddChild(new NewGameChecks(this, world, vehicle));
        if (args.Contains("--capture"))
        {
            string view = args.FirstOrDefault(a => a.StartsWith("--view="))?.Substring(7) ?? "gameplay";
            if (view.StartsWith("museum") || view.StartsWith("toys"))
            {
                bool toys = view.StartsWith("toys");
                var owner = toys ? world.Village.Toymaker : world.Village.GemCollector;
                if (view.EndsWith("full")) world.Village.RestoreCollection(toys, 6);
                vehicle.SetPose(owner.Position + Vector3.Right * 3, 0);
                if (view.EndsWith("question") || view.EndsWith("expedition")) OpenChallenge(owner);
                if (view.EndsWith("expedition")) SubmitAnswer(CurrentChallenge!.Answer);
            }
            if (view is "garden" or "garden-full" or "garden-question" or "garden-expedition")
            {
                if (view == "garden-full") world.Village.RestoreGarden(Village.FlowerNames.Length);
                vehicle.SetPose(world.Village.Gardener.Position + Vector3.Right * 3, 0);
                if (view is "garden-question" or "garden-expedition") OpenChallenge(world.Village.Gardener);
                if (view == "garden-expedition") SubmitAnswer(CurrentChallenge!.Answer);
            }
            if (view is "farm" or "farm-full" or "farm-question" or "farm-expedition")
            {
                if (view == "farm-full") world.Village.RestoreFarm(Village.FarmKinds.Length);
                vehicle.SetPose(world.Village.Farmer.Position + Vector3.Right * 3, 0);
                if (view is "farm-question" or "farm-expedition") OpenChallenge(world.Village.Farmer);
                if (view == "farm-expedition") SubmitAnswer(CurrentChallenge!.Answer);
            }
            if (view == "garage") { vehicle.SetPose(world.Village.GarageEntrance, 0); OpenGarage(); }
            if (view is "village" or "village-upgraded" or "village-question" or "village-fruit-offer" or "village-fruit")
            {
                if (view == "village-upgraded") world.Village.RestoreLevels(new[] { 10, 10, 10, 10 });
                if (view is "village-fruit-offer" or "village-fruit")
                    world.Village.RestoreLevels(new[] { world.Village.Tasks(0).ToList().IndexOf(VillageTaskKind.Fruit), 0, 0, 0 });
                vehicle.SetPose(world.Village.Residents[0].Position + world.Village.Houses[0].Basis.Z * 3, 0);
                if (view is "village-question" or "village-fruit-offer" or "village-fruit") OpenChallenge(world.Village.Residents[0]);
                if (view == "village-fruit") BeginVillageCollection();
            }
            if (view == "task-choice") { OpenChallenge(world.Animals[0]); SubmitAnswer(CurrentChallenge!.Answer); }
            void PrepareExpedition(bool collection, bool start = true)
            {
                var sender = world.Animals[collection ? 1 : 0];
                OpenChallenge(sender); SubmitAnswer(CurrentChallenge!.Answer);
                if (start) SelectExpedition(collection);
            }
            void CompleteExpedition()
            {
                if (Delivery!.Collection)
                    foreach (var point in Delivery.PickupPositions) { vehicle.SetPose(point + Vector3.Up * .15f, 0); Delivery.Tick(.25); }
                vehicle.SetPose(Delivery.Recipient.Position + Vector3.Right * 2, 0); BeginDeliveryQuestion();
            }
            if (view is "collection-offer" or "collection" or "collection-question" or "collection-reward" or "delivery-offer" or "delivery" or "delivery-question" or "delivery-reward")
            {
                PrepareExpedition(view.StartsWith("collection"), !view.EndsWith("offer"));
                if (view.EndsWith("question") || view.EndsWith("reward")) CompleteExpedition();
                if (view.EndsWith("reward")) SubmitAnswer(CurrentChallenge!.Answer);
            }
            if (view is "challenge" or "letters" or "reward" or "accessories")
            {
                if (view is "reward" or "accessories")
                {
                    PrepareExpedition(false); CompleteExpedition(); SubmitAnswer(CurrentChallenge!.Answer);
                }
                else OpenChallenge(world.Animals[view == "letters" ? 1 : 0]);
            }
            if (view == "decorated") foreach (var accessory in CarAccessory.All) vehicle.Appearance.ToggleAccessory(accessory.Id);
            if (CarBody.All.Any(body => body.Id == view))
            {
                vehicle.Appearance.ApplyModel(view);
                vehicle.Appearance.Apply(CarLook.All.First(look => look.Id == "ocean"));
                foreach (var id in view == "buggy" ? new[] { "surfboard", "lights", "bumper" } :
                    view == "pickup" ? new[] { "spoiler", "exhaust", "antenna" } : new[] { "rack", "suitcase", "spare" })
                    vehicle.Appearance.ToggleAccessory(id);
            }
            AddChild(new VisualCapture(camera, world, vehicle, view));
        }
        GD.Print($"WORLD_READY seed={Settings.Seed} animals={world.Animals.Length} road_samples={world.Routes.Samples.Count}");
    }
    private void Start() { State = GameState.Exploring; vehicle.Active = true; ui.HideModal(); }
    private void CloseInteraction()
    {
        if (State == GameState.Reward && deliveryReward && Delivery != null)
        {
            Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null; ui.DeliveryHud(null);
            UpdateDeliveryGuides();
        }
        deliveryReward = false;
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
        else ui.HideModal();
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
            else if (State is GameState.Challenge or GameState.Reward or GameState.ExpeditionChoice or GameState.Garage or GameState.HouseReward or GameState.HouseCollection or GameState.HouseFruitOffer or GameState.FarmStatus or GameState.GardenStatus or GameState.CollectionStatus) CloseInteraction();
            else TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsEcho()) return;
        if (State != GameState.Exploring) return;
        if (e.IsActionPressed("reset")) vehicle.Reset();
        if (e.IsActionPressed("talk")) Interact();
    }
    private void Interact()
    {
        if (State != GameState.Exploring) return;
        if (AtGarage) { OpenGarage(); return; }
        if (Delivery != null && Delivery.Arrived && vehicle.Position.DistanceTo(Delivery.Recipient.Position) < Settings.InteractionRange) BeginDeliveryQuestion();
        else if (nearby != null) OpenChallenge(nearby);
    }
    private void ShowRewards() => ui.Reward(nearby!, vehicle.Appearance, CurrentRewards);
    private void ShowGarage() => ui.Garage(vehicle.Appearance, OwnedRewards);
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
    }
    internal void SelectExpedition(bool collection)
    {
        if (State != GameState.ExpeditionChoice || nearby == null || Delivery != null) return;
        Delivery = new Expedition(world, vehicle, nearby, collection);
        AddChild(Delivery); ui.DeliveryHud(Delivery); Start();
        lastCollected = Delivery.Collected;
        UpdateDeliveryGuides();
    }
    private bool CanChooseExpedition(Animal animal) => unlockedExpeditions.Contains(animal) && !completedDeliveries.Contains(animal) && Delivery?.Sender != animal;
    private void UpdateDeliveryGuides()
    {
        foreach (var animal in world.Animals.Concat(world.Village.Residents).Append(world.Village.Farmer).Append(world.Village.Gardener).Append(world.Village.GemCollector).Append(world.Village.Toymaker))
        {
            bool target = Delivery != null && Delivery.CanDeliver && animal == Delivery.Recipient;
            animal.Guide(animal.RewardAvailable || CanChooseExpedition(animal) || target, target);
        }
    }
    internal void BeginDeliveryQuestion()
    {
        if (Delivery == null || !Delivery.CanDeliver || State != GameState.Exploring || vehicle.Position.DistanceTo(Delivery.Recipient.Position) >= Settings.InteractionRange) return;
        Delivery.Arrived = true; nearby = Delivery.Recipient; vehicle.Active = false;
        if (Delivery.SpecialCollection)
        {
            if (Delivery.Gems || Delivery.Toys)
            {
                bool toys = Delivery.Toys;
                Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null;
                world.Village.RestoreCollection(toys, world.Village.CollectionCount(toys) + 1); nearby.Celebrate();
                CurrentChallenge = null; rejectedAnswers.Clear(); ui.DeliveryHud(null); UpdateDeliveryGuides();
                State = GameState.CollectionStatus; ui.CollectionStatus(world.Village, nearby, null, true); SaveProgress(); return;
            }
            bool garden = Delivery.Garden;
            Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null;
            if (garden) world.Village.RestoreGarden(world.Village.GardenCount + 1);
            else world.Village.RestoreFarm(world.Village.FarmCount + 1);
            nearby.Celebrate();
            CurrentChallenge = null; rejectedAnswers.Clear(); ui.DeliveryHud(null); UpdateDeliveryGuides();
            State = garden ? GameState.GardenStatus : GameState.FarmStatus;
            if (garden) ui.GardenStatus(world.Village, null, true); else ui.FarmStatus(world.Village, null, true);
            SaveProgress(); return;
        }
        CurrentChallenge = new DeliveryChallenge(Delivery.Cargo, Delivery.Amount); rejectedAnswers.Clear();
        State = GameState.Challenge; feedback = ""; ShowChallenge();
    }
    internal void OpenChallenge(Animal animal)
    {
        if (State != GameState.Exploring) return;
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
        int taskIndex = animal.HouseIndex >= 0 ? world.Village.Level(animal.HouseIndex) : Array.IndexOf(world.Animals, animal);
        bool math = animal.HouseIndex >= 0 ? world.Village.TaskAt(animal.HouseIndex) == VillageTaskKind.Math : taskIndex % 2 == 0;
        CurrentChallenge = math ? challenges.Next() : letterChallenges.Next();
        rejectedAnswers.Clear();
        feedback = CurrentChallenge is LetterChallenge
            ? "Pojmenuj obrázky. Které slovo začíná na toto písmenko?"
            : "Nikam nespěcháme. Můžeš si to spočítat na prstech.";
        if (animal.HouseIndex >= 0)
            feedback = $"Domeček {taskIndex} / 10 · Odměna: {Village.Upgrades[taskIndex]}";
        State = GameState.Challenge; ShowChallenge();
    }
    internal void SubmitAnswer(int answer)
    {
        if (State != GameState.Challenge || CurrentChallenge == null || nearby == null || !CurrentChallenge.Choices.Contains(answer)) return;
        if (answer == CurrentChallenge.Answer)
        {
            if (nearby.IsFarmer || nearby.IsGardener || nearby.IsCollectionOwner)
            {
                int count = nearby.IsFarmer ? world.Village.FarmCount : nearby.IsGardener ? world.Village.GardenCount : world.Village.CollectionCount(nearby.IsToymaker);
                if (Delivery != null || count >= 6) return;
                Delivery = new Expedition(world, vehicle, nearby, true); AddChild(Delivery);
                CurrentChallenge = null; rejectedAnswers.Clear(); lastCollected = Delivery.Collected;
                ui.DeliveryHud(Delivery); Start(); UpdateDeliveryGuides(); SaveProgress(); return;
            }
            if (CurrentChallenge is DeliveryChallenge && Delivery?.Sender.HouseIndex >= 0)
            {
                var owner = Delivery.Sender;
                Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null; ui.DeliveryHud(null);
                CompleteHouseTask(owner); UpdateDeliveryGuides(); SaveProgress(); return;
            }
            if (nearby.HouseIndex >= 0 && CurrentChallenge is not DeliveryChallenge)
            {
                CompleteHouseTask(nearby); SaveProgress(); return;
            }
            deliveryReward = CurrentChallenge is DeliveryChallenge;
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
                Delivery.Recipient.Celebrate();
            }
            CurrentRewards = CarReward.Draw(rewardRandom, previousRewards); previousRewards = CurrentRewards;
            State = GameState.Reward; ShowRewards();
        }
        else
        {
            rejectedAnswers.Add(answer);
            feedback = "Nevadí! Zkus jinou odpověď. Společně to zvládneme.";
            ShowChallenge();
        }
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
        else { beforePause = State; State = GameState.Paused; vehicle.Active = false; ui.Pause(); }
    }
    internal void SelectAccessory(string id)
    {
        if (State != GameState.Reward || !CurrentRewards.Contains(new CarReward(id, true))) return;
        OwnedRewards.Add(new(id, true)); vehicle.Appearance.ToggleAccessory(id); CloseInteraction();
    }
    internal void FinishInteraction() => CloseInteraction();
    public override void _Process(double delta)
    {
        foreach (var animal in world.Animals.Concat(world.Village.Residents).Append(world.Village.Farmer).Append(world.Village.Gardener).Append(world.Village.GemCollector).Append(world.Village.Toymaker))
            animal.GreetPlayer(vehicle.Position, State == GameState.Exploring);
        if (State == GameState.Exploring)
        {
            nearby = world.Animals.Concat(world.Village.Residents).Append(world.Village.Farmer).Append(world.Village.Gardener).Append(world.Village.GemCollector).Append(world.Village.Toymaker).Where(a => a.Position.DistanceTo(vehicle.Position) < Settings.InteractionRange)
                .OrderByDescending(a => a.RewardAvailable || CanChooseExpedition(a)).ThenBy(a => a.Position.DistanceSquaredTo(vehicle.Position)).FirstOrDefault();
            if (Delivery != null)
            {
                Delivery.Tick(delta); ui.DeliveryDirection(Delivery.Waypoint, vehicle);
                if (lastCollected != Delivery.Collected)
                {
                    lastCollected = Delivery.Collected; ui.DeliveryHud(Delivery); UpdateDeliveryGuides();
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
            if (AtGarage) ui.GarageHint();
        }
        else ui.DeliveryHint(false);
        saveTimer += delta;
        if (State != GameState.Title && (State != lastSavedState || saveTimer >= (State == GameState.Exploring ? 2 : .5))) SaveProgress();
    }
}


