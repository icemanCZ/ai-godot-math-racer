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
    private static bool newGamePending;

    internal void NewGame()
    {
        if (newGamePending || State is not (GameState.Title or GameState.Paused)) return;
        int oldSeed = Settings.Seed;
        int seed;
        do { seed = Random.Shared.Next(1, int.MaxValue - 4096); } while (seed == oldSeed);
        // Stop the old scene's exit save before switching the shared generators.
        persistenceReady = false; newGamePending = true; vehicle.Active = false;
        ui.LoadingWorld();
        Callable.From(() =>
        {
            Settings.SetSeed(seed);
            var error = GetTree().ReloadCurrentScene();
            if (error == Error.Ok) return;
            Settings.SetSeed(oldSeed); newGamePending = false; persistenceReady = true;
            if (State == GameState.Title) ui.Title(SaveStore.Initial != null); else ui.Pause();
            GD.PushError($"Cannot start new game: {error}");
        }).CallDeferred();
    }

    internal SaveData CaptureProgress() => new()
    {
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
        Accessories = CarAccessory.All.Where(a => vehicle.Appearance.HasAccessory(a.Id)).Select(a => a.Id).ToArray(),
        Sender = ActorId(Delivery?.Sender), Collection = Delivery?.Collection ?? false,
        Picked = Delivery?.Picked ?? Array.Empty<bool>(), Arrived = Delivery?.Arrived ?? false, DeliveryReward = deliveryReward,
        Math = CurrentChallenge as MathChallenge, Letters = CurrentChallenge as LetterChallenge,
        DeliveryQuestion = CurrentChallenge as DeliveryChallenge, Rejected = rejectedAnswers.ToArray(), Feedback = feedback
    };

    internal void RestoreProgress(SaveData data)
    {
        if (Delivery != null) { Delivery.ReleaseCargo(); RemoveChild(Delivery); Delivery.QueueFree(); Delivery = null; }
        ClearHighlight();
        celebrationFx?.QueueFree(); celebrationFx = null; celebrationTime = 0; celebrationPanel = null; ui.CelebrationMessage("");
        Events.Restore(data.EventActive, data.EventNext, data.EventWins);
        var restoredPosition = new Vector3(data.Position[0], data.Position[1], data.Position[2]);
        float surface = world.Ground.Height(restoredPosition.X, restoredPosition.Z);
        if (world.Village.TerrainArea.HasPoint(new(restoredPosition.X, restoredPosition.Z)) && restoredPosition.Y < surface - .2f)
            restoredPosition.Y = surface + .15f;
        vehicle.SetPose(restoredPosition, data.Heading);
        vehicle.Appearance.ApplyModel(data.Model);
        vehicle.Appearance.Apply(CarLook.All.FirstOrDefault(l => l.Id == data.Look) ?? CarLook.All[0]);
        foreach (var accessory in CarAccessory.All)
            if (vehicle.Appearance.HasAccessory(accessory.Id) != data.Accessories.Contains(accessory.Id)) vehicle.Appearance.ToggleAccessory(accessory.Id);
        for (int i = 0; i < world.Animals.Length; i++) world.Animals[i].SetRewardAvailable(data.Animals[i]);
        world.Village.RestoreLevels(data.HouseLevels);
        world.Village.RestoreTrades(data.TradeCounts);
        world.Village.RestoreFarm(data.FarmCount);
        world.Village.RestoreGarden(data.GardenCount);
        world.Village.RestoreCollection(false, data.GemCount); world.Village.RestoreCollection(true, data.ToyCount);
        unlockedExpeditions.Clear(); completedDeliveries.Clear();
        foreach (int i in data.Unlocked) unlockedExpeditions.Add(world.Animals[i]);
        foreach (int i in data.Completed) completedDeliveries.Add(world.Animals[i]);
        OwnedRewards.Clear(); OwnedRewards.UnionWith(data.Owned);
        CurrentRewards = data.Rewards; previousRewards = data.PreviousRewards;
        nearby = ActorAt(data.Nearby);
        CurrentChallenge = (IChallenge?)data.Math ?? (IChallenge?)data.Letters ?? data.DeliveryQuestion;
        rejectedAnswers.Clear(); rejectedAnswers.UnionWith(data.Rejected); feedback = data.Feedback;
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
        beforePause = data.State; Resume();
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
