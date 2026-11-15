using Godot;
using System;
using System.Linq;

namespace MathRacer;

public partial class Game
{
    private bool persistenceReady;
    private double saveTimer;
    private string lastSave = "";
    private GameState lastSavedState = GameState.Title;
    private static (QuizType Types, bool ThreeTerms)? newGameQuizzes;

    private int ActorId(Animal? actor) => actor == null ? -1 : actor == bandits[0] ? BanditActorId : world.ActorId(actor);

    private Animal? ActorAt(int id) => id == BanditActorId ? bandits[0] : world.ActorAt(id);

    internal void NewGame()
    {
        if (newGameQuizzes.HasValue || State is not (GameState.Title or GameState.Paused)) return;
        int oldSeed = Settings.Seed;
        int seed;
        do { seed = Random.Shared.Next(1, int.MaxValue - 4096); } while (seed == oldSeed);
        // Stop the old scene's exit save before switching the shared generators.
        persistenceReady = false;
        newGameQuizzes = State == GameState.Title && SaveStore.Initial is { } saved
            ? (saved.QuizTypes, saved.ThreeTermMath) : (enabledQuizzes, threeTermMath);
        vehicle.Active = false;
        ui.LoadingWorld();
        Callable.From(() =>
        {
            Settings.SetSeed(seed);
            var error = GetTree().ReloadCurrentScene();
            if (error == Error.Ok) return;
            Settings.SetSeed(oldSeed); newGameQuizzes = null; persistenceReady = true;
            if (State == GameState.Title) ui.Title(SaveStore.Initial != null); else ui.Pause();
            GD.PushError($"Cannot start new game: {error}");
        }).CallDeferred();
    }

    internal SaveData CaptureProgress() => new()
    {
        GolemOwned = GolemOwned, GolemActive = GolemActive,
        QuizTypes = enabledQuizzes, NextQuiz = nextQuiz, ThreeTermMath = threeTermMath, HandoverQuiz = handoverQuiz,
        Reading = CurrentChallenge as ReadingChallenge,
        BanditSite = banditSite, BanditSolved = banditSolved, BanditCooldown = banditCooldown,
        Syllables = CurrentChallenge as SyllableChallenge,
        SyllableSelection = CurrentChallenge is SyllableChallenge ? syllableSelection : default,
        DisabledCollectorPets = world.Village.CollectorPets.Where(p => p.IsOwned && p.Disabled).Select(p => p.Kind.ToString()).ToArray(),
        Postcards = (PostcardProgress?[])postcards.Clone(), PostcardPage = postcardPage,
        EventActive = Events.Active, EventNext = Events.Next, EventWins = (int[])Events.Wins.Clone(),
        Seed = Settings.Seed, Position = new[] { vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z }, Heading = vehicle.Heading,
        State = State == GameState.Paused ? beforePause : State,
        Nearby = ActorId(nearby),
        TradeCounts = (int[])world.Village.TradeCounts.Clone(),
        HouseLevels = world.Village.Levels,
        FarmCount = world.Village.FarmCount,
        GardenCount = world.Village.GardenCount,
        GemCount = world.Village.GemCount, ToyCount = world.Village.ToyCount,
        Animals = world.Animals.Select(a => a.RewardAvailable).ToArray(),
        Unlocked = unlockedExpeditions.Select(a => Array.IndexOf(world.Animals, a)).ToArray(),
        Completed = completedDeliveries.Select(a => Array.IndexOf(world.Animals, a)).ToArray(),
        Owned = OwnedRewards.ToArray(), Rewards = CurrentRewards.ToArray(), PreviousRewards = previousRewards.ToArray(),
        Look = vehicle.Appearance.CurrentLookId, Model = vehicle.Appearance.CurrentModelId,
        Accessories = CarAccessory.Catalogue.Where(a => vehicle.Appearance.HasAccessory(a.Id)).Select(a => a.Id).ToArray(),
        Sender = ActorId(Delivery?.Sender), Collection = Delivery?.Collection ?? false,
        Picked = Delivery?.Picked ?? Array.Empty<bool>(), Arrived = Delivery?.Arrived ?? false, DeliveryReward = deliveryReward,
        Math = CurrentChallenge as MathChallenge, Letters = CurrentChallenge as LetterChallenge,
        DeliveryQuestion = CurrentChallenge as DeliveryChallenge, Rejected = rejectedAnswers.ToArray()
    };

    internal void RestoreProgress(SaveData data)
    {
        if (Delivery != null) { Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null; }
        CancelPostcardPhoto();
        enabledQuizzes = data.QuizTypes; nextQuiz = data.NextQuiz; threeTermMath = data.ThreeTermMath;
        handoverQuiz = data.HandoverQuiz;
        companionStopTime = 0;
        Array.Clear(companionWanders);
        syllableSelection = data.SyllableSelection;
        Array.Copy(data.Postcards, postcards, postcards.Length);
        postcardPage = data.PostcardPage; rejectedPostcardAnswers = 0; postcardFeedback = "";
        for (int i = 0; i < postcards.Length; i++) viewpoints[i].Sign.Visible = postcards[i] == null;
        ClearHighlight();
        celebrationFx?.QueueFree(); celebrationFx = null; celebrationTime = 0; celebrationPanel = null; ui.CelebrationMessage("");
        Events.Restore(data.EventActive, data.EventNext, data.EventWins);
        var restoredPosition = new Vector3(data.Position[0], data.Position[1], data.Position[2]);
        float surface = world.Ground.Height(restoredPosition.X, restoredPosition.Z);
        if (world.Village.TerrainArea.HasPoint(new(restoredPosition.X, restoredPosition.Z)) && restoredPosition.Y < surface - .2f)
            restoredPosition.Y = surface + .15f;
        vehicle.SetPose(restoredPosition, data.Heading);
        RestoreGolem(data.GolemOwned, data.GolemActive);
        vehicle.Appearance.ApplyModel(data.Model);
        vehicle.Appearance.Apply(CarLook.All.FirstOrDefault(l => l.Id == data.Look) ?? CarLook.All[0]);
        foreach (var accessory in CarAccessory.Catalogue)
            if (vehicle.Appearance.HasAccessory(accessory.Id) != data.Accessories.Contains(accessory.Id)) vehicle.Appearance.ToggleAccessory(accessory.Id);
        for (int i = 0; i < world.Animals.Length; i++) world.Animals[i].SetRewardAvailable(data.Animals[i]);
        world.Village.RestoreLevels(data.HouseLevels);
        world.Village.RestoreTrades(data.TradeCounts);
        world.Village.RestoreFarm(data.FarmCount);
        world.Village.RestoreGarden(data.GardenCount);
        world.Village.RestoreCollection(false, data.GemCount); world.Village.RestoreCollection(true, data.ToyCount);
        foreach (var pet in world.Village.CollectorPets)
        {
            pet.Disabled = pet.IsOwned && (data.DisabledCollectorPets?.Contains(pet.Kind.ToString()) ?? false);
            PlaceCollectorPet(pet);
        }
        unlockedExpeditions.Clear(); completedDeliveries.Clear();
        foreach (int i in data.Unlocked) unlockedExpeditions.Add(world.Animals[i]);
        foreach (int i in data.Completed) completedDeliveries.Add(world.Animals[i]);
        OwnedRewards.Clear(); OwnedRewards.UnionWith(data.Owned);
        CurrentRewards = data.Rewards; previousRewards = data.PreviousRewards;
        bool validEncounter = RestoreBandits(data);
        nearby = validEncounter ? ActorAt(data.Nearby) : null;
        CurrentChallenge = (IChallenge?)data.Math ?? (IChallenge?)data.Letters ?? (IChallenge?)data.Syllables ??
            (IChallenge?)data.Reading ?? data.DeliveryQuestion;
        if (!validEncounter) { CurrentChallenge = null; banditSolved = 0; }
        rejectedAnswers.Clear(); rejectedAnswers.UnionWith(data.Rejected);
        deliveryReward = data.DeliveryReward;
        if (data.Sender >= 0)
        {
            var sender = ActorAt(data.Sender)!;
            Delivery = new Expedition(world, vehicle, sender, data.Collection, data.Picked) { Arrived = data.Arrived };
            AddChild(Delivery);
            if (deliveryReward) Delivery.ReleaseCargo();
        }
        lastCollected = Delivery?.Collected ?? -1;
        ui.DeliveryHud(Delivery); ui.AnimalCount(RemainingAnimals, world.Animals.Length); UpdateDeliveryGuides();
        beforePause = validEncounter ? data.State : GameState.Exploring; Resume();
        foreach (var camera in GetChildren().OfType<FollowCamera>()) camera.Snap();
    }

    private void ContinueGame()
    {
        if (State == GameState.Title && SaveStore.Initial is { } data) RestoreProgress(data);
        else Start();
    }

    private void SaveProgress()
    {
        if (!persistenceReady || !SaveStore.Enabled || State == GameState.Title) return;
        var data = CaptureProgress();
        string json = System.Text.Json.JsonSerializer.Serialize(data);
        if (json != lastSave && SaveStore.Write(data, SaveStore.SavePath)) lastSave = json;
        lastSavedState = State;
        saveTimer = 0;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationApplicationFocusOut || what == NotificationApplicationPaused) SaveProgress();
    }
    public override void _ExitTree() => SaveProgress();
}
