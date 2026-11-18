using System;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class Game
{
    private bool persistenceReady;
    private double saveTimer;
    private string lastSave = "";
    private GameState lastSavedState = GameState.Title;

    private int ActorId(Animal? actor) =>
        actor == null ? -1
        : actor == bandits[0] ? BanditActorId
        : world.ActorId(actor);

    private Animal? ActorAt(int id) => id == BanditActorId ? bandits[0] : world.ActorAt(id);

    internal void NewGame()
    {
        if (Settings.PendingNewGameQuizzes.HasValue || State is not (GameState.Title or GameState.Paused))
            return;
        int oldSeed = Settings.Seed;
        int seed;
        do
        {
            seed = Settings.NewSeed();
        } while (seed == oldSeed);
        // Stop the old scene's exit save before switching the shared generators.
        persistenceReady = false;
        Settings.PendingNewGameQuizzes =
            State == GameState.Title && SaveStore.Initial is { } saved
                ? (saved.QuizTypes, saved.ThreeTermMath)
                : (enabledQuizzes, threeTermMath);
        vehicle.Active = false;
        ui.LoadingWorld();
        Callable
            .From(() =>
            {
                Settings.SetSeed(seed);
                var error = GetTree().ReloadCurrentScene();
                if (error == Error.Ok)
                    return;
                Settings.SetSeed(oldSeed);
                Settings.PendingNewGameQuizzes = null;
                persistenceReady = true;
                if (State == GameState.Title)
                    ui.Title(SaveStore.Initial != null);
                else
                    ui.Pause();
                GD.PushError($"Cannot start new game: {error}");
            })
            .CallDeferred();
    }

    internal SaveData CaptureProgress() =>
        new()
        {
            GolemOwned = GolemOwned,
            GolemActive = GolemActive,
            BuildStages = BuildStages(),
            Trip = Haul is { } haul
                ? new SavedTrip
                {
                    Kind = haul.Target.Kind,
                    Index = haul.Target.Index,
                    Loaded = haul.Loaded,
                }
                : null,
            PlotStages = PlotStages(),
            WorksStock = world.WorksStock(),
            TrailerOwned = TrailerOwned,
            TrailerAttached = trailerParade,
            TrailerPaint = trailer.PaintId,
            QuizTypes = enabledQuizzes,
            NextQuiz = nextQuiz,
            ThreeTermMath = threeTermMath,
            HandoverQuiz = handoverQuiz,
            Departure = (int)departure,
            DepartureState = departure is Departure.Build or Departure.Plot or Departure.House or Departure.Missing
                ? departureState
                : GameState.Title,
            BanditSite = banditSite,
            BanditSolved = banditSolved,
            BanditCooldown = banditCooldown,
            DisabledCollectorPets = world
                .AllPets.Where(p => p.IsOwned && p.Disabled)
                .Select(p => p.Kind.ToString())
                .ToArray(),
            Postcards = (PostcardProgress?[])postcards.Clone(),
            PostcardPage = postcardPage,
            EventActive = Events.Active,
            EventNext = Events.Next,
            EventWins = (int[])Events.Wins.Clone(),
            EventBest = Events.BestSplits.Select(b => (float[])b.Clone()).ToArray(),
            EventTime = (float)Events.Elapsed,
            EventSplits = Events.RunSplits.ToArray(),
            Seed = Settings.Seed,
            Position = new[] { vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z },
            Heading = vehicle.Heading,
            State = State == GameState.Paused ? beforePause : State,
            Nearby = ActorId(nearby),
            TradeCounts = (int[])world.Village.TradeCounts.Clone(),
            HouseLevels = world.Village.Levels,
            FarmCount = world.Village.FarmCount,
            GardenCount = world.Village.GardenCount,
            GemCount = world.Village.GemCount,
            ToyCount = world.Village.ToyCount,
            Animals = world.Animals.Select(a => a.RewardAvailable).ToArray(),
            Unlocked = roadside.UnlockedIndices(world.Animals),
            Completed = roadside.CompletedIndices(world.Animals),
            Owned = rewards.Owned.ToArray(),
            Coins = Coins,
            RewardCoins =
                State is GameState.Reward || (State == GameState.Paused && beforePause == GameState.Reward)
                    ? rewardCoins
                    : 0,
            Look = vehicle.Appearance.CurrentLookId,
            Model = vehicle.Appearance.CurrentModelId,
            Accessories = CarAccessory
                .Catalogue.Where(a => vehicle.Appearance.HasAccessory(a.Id))
                .Select(a => a.Id)
                .ToArray(),
            Sender = ActorId(Delivery?.Sender),
            Collection = Delivery?.Collection ?? false,
            Picked = Delivery?.Picked ?? Array.Empty<bool>(),
            Arrived = Delivery?.Arrived ?? false,
            DeliveryReward = deliveryReward,
            Challenge = CurrentChallenge,
            SyllableSelection = CurrentChallenge is SyllableChallenge ? syllableSelection : default,
            Rejected = rejectedAnswers.ToArray(),
        };

    internal void RestoreProgress(SaveData data)
    {
        EndExpedition(refreshUi: false);
        CancelPostcardPhoto();
        enabledQuizzes = data.QuizTypes;
        nextQuiz = data.NextQuiz;
        threeTermMath = data.ThreeTermMath;
        handoverQuiz = data.HandoverQuiz;
        companionStopTime = 0;
        Array.Clear(companionWanders);
        syllableSelection = data.SyllableSelection;
        RestorePostcards(data);
        ResetCelebration();
        Events.Restore(
            data.EventActive,
            data.EventNext,
            data.EventWins,
            data.EventBest,
            data.EventTime,
            data.EventSplits
        );
        RestoreCarPose(data);
        RestoreGolem(data.GolemOwned, data.GolemActive);
        RestoreCarLook(data);
        RestoreVillage(data);
        roadside.Restore(world.Animals, data.Unlocked, data.Completed);
        rewards.Restore(data.Owned, data.Coins);
        rewardCoins = data.RewardCoins;
        ui.CoinCount(Coins, 0);
        bool validEncounter = RestoreBandits(data);
        nearby = validEncounter ? ActorAt(data.Nearby) : null;
        CurrentChallenge = data.Challenge;
        if (!validEncounter)
        {
            CurrentChallenge = null;
            banditSolved = 0;
        }
        rejectedAnswers.Clear();
        rejectedAnswers.UnionWith(data.Rejected);
        // The trip that waits for the open quiz to be solved; without a question there is none.
        departure = CurrentChallenge != null ? (Departure)data.Departure : Departure.None;
        departureState = data.DepartureState;
        deliveryReward = data.DeliveryReward;
        if (data.Sender >= 0)
        {
            var sender = ActorAt(data.Sender)!;
            Delivery = new Expedition(world, vehicle, sender, data.Collection, data.Picked) { Arrived = data.Arrived };
            AddChild(Delivery);
            if (deliveryReward)
                Delivery.ReleaseCargo();
        }
        lastCollected = Delivery?.Collected ?? -1;
        ui.DeliveryHud(Delivery);
        RestoreBuilds(data);
        ui.AnimalCount(RemainingAnimals, world.Animals.Length);
        UpdateDeliveryGuides();
        beforePause = validEncounter ? data.State : GameState.Exploring;
        Resume();
        foreach (var camera in GetChildren().OfType<FollowCamera>())
            camera.Snap();
    }

    private void RestorePostcards(SaveData data)
    {
        Array.Copy(data.Postcards, postcards, postcards.Length);
        postcardPage = data.PostcardPage;
        rejectedPostcardAnswers = 0;
        postcardFeedback = "";
        for (int i = 0; i < postcards.Length; i++)
            viewpoints[i].Sign.Visible = postcards[i] == null;
    }

    private void ResetCelebration()
    {
        ClearHighlight();
        celebrationFx?.QueueFree();
        celebrationFx = null;
        celebrationTime = 0;
        celebrationPanel = null;
        ui.CelebrationMessage("");
    }

    private void RestoreCarPose(SaveData data)
    {
        var position = new Vector3(data.Position[0], data.Position[1], data.Position[2]);
        // The village terrain is levelled after generation; lift a car saved below the new surface.
        float surface = world.Ground.Height(position.X, position.Z);
        if (world.Village.TerrainArea.HasPoint(new(position.X, position.Z)) && position.Y < surface - .2f)
            position.Y = surface + .15f;
        vehicle.SetPose(position, data.Heading);
    }

    private void RestoreCarLook(SaveData data)
    {
        vehicle.Appearance.ApplyModel(data.Model);
        vehicle.Appearance.Apply(CarLook.Catalogue.FirstOrDefault(l => l.Id == data.Look) ?? CarLook.All[0]);
        foreach (var accessory in CarAccessory.Catalogue)
            if (vehicle.Appearance.HasAccessory(accessory.Id) != data.Accessories.Contains(accessory.Id))
                vehicle.Appearance.ToggleAccessory(accessory.Id);
    }

    private void RestoreVillage(SaveData data)
    {
        for (int i = 0; i < world.Animals.Length; i++)
            world.Animals[i].SetRewardAvailable(data.Animals[i]);
        world.Village.RestoreLevels(data.HouseLevels);
        world.Village.RestoreTrades(data.TradeCounts);
        world.Village.RestoreFarm(data.FarmCount);
        world.Village.RestoreGarden(data.GardenCount);
        world.Village.RestoreCollection(false, data.GemCount);
        world.Village.RestoreCollection(true, data.ToyCount);
        foreach (var pet in world.AllPets)
        {
            pet.Disabled = pet.IsOwned && (data.DisabledCollectorPets?.Contains(pet.Kind.ToString()) ?? false);
            PlaceCollectorPet(pet);
        }
    }

    private void ContinueGame()
    {
        if (State == GameState.Title && SaveStore.Initial is { } data)
            RestoreProgress(data);
        else
            Start();
    }

    private void SaveProgress()
    {
        if (!persistenceReady || !SaveStore.Enabled || State == GameState.Title)
            return;
        var data = CaptureProgress();
        string json = System.Text.Json.JsonSerializer.Serialize(data);
        if (json != lastSave && SaveStore.Write(data, SaveStore.SavePath))
            lastSave = json;
        lastSavedState = State;
        saveTimer = 0;
    }

    public override void _Notification(int what)
    {
        if (
            what == NotificationWMCloseRequest
            || what == NotificationApplicationFocusOut
            || what == NotificationApplicationPaused
        )
            SaveProgress();
    }

    public override void _ExitTree() => SaveProgress();
}
