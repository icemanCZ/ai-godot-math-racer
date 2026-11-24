using System;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Saves;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Vehicles;

namespace MathRacer.App;

public partial class Game
{
    private bool persistenceReady;
    private double saveTimer;
    private string lastSave = "";
    private GameState lastSavedState = GameState.Title;

    private int ActorId(Character? actor) =>
        actor == null ? -1
        : actor == bandits[0] ? BanditActorId
        : world.ActorId(actor);

    private Character? ActorAt(int id) => id == BanditActorId ? bandits[0] : world.ActorAt(id);

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
                ? (saved.Quiz.EnabledTypes(), saved.Quiz.ThreeTermMath)
                : (quiz.Enabled, quiz.ThreeTermMath);
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
            Seed = Settings.Seed,
            State = interaction.Resting,
            Nearby = ActorId(nearby),
            Vehicle = new VehicleSave
            {
                Position = new[] { vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z },
                Heading = vehicle.Heading,
                Look = vehicle.Appearance.CurrentLookId,
                Model = vehicle.Appearance.CurrentModelId,
                Trail = vehicle.Appearance.CurrentTrailId,
                Accessories = CarAccessory
                    .Catalogue.Where(a => vehicle.Appearance.HasAccessory(a.Id))
                    .Select(a => a.Id)
                    .ToArray(),
            },
            Economy = new EconomySave
            {
                Coins = Coins,
                RewardCoins = interaction.Resting == GameState.Reward ? economy.RewardCoins : 0,
                Owned = economy.Owned.ToArray(),
            },
            Quiz = new QuizSave
            {
                Types = quiz.Enabled,
                Offered = QuizType.All,
                ThreeTermMath = quiz.ThreeTermMath,
                Next = quiz.NextQuiz,
                Handover = quiz.Handover,
                Challenge = CurrentChallenge,
                Selection = CurrentChallenge is SyllableChallenge ? quiz.Selection : default,
                Rejected = quiz.Rejected.ToArray(),
                Departure = (int)quiz.Departure,
                DepartureState = quiz.Departure
                    is Departure.Build
                        or Departure.Plot
                        or Departure.Playground
                        or Departure.Railway
                        or Departure.Zoo
                        or Departure.House
                        or Departure.Missing
                        or Departure.Glassmaker
                        or Departure.Hunter
                        or Departure.Lighthouse
                    ? quiz.DepartureState
                    : GameState.Title,
            },
            Task = new TaskSave
            {
                Sender = ActorId(Delivery?.Sender),
                Collection = Delivery?.Collection ?? false,
                Picked = Delivery?.Picked ?? Array.Empty<bool>(),
                Arrived = Delivery?.Arrived ?? false,
                DeliveryReward = deliveryReward,
                Trip = Haul is { } haul
                    ? new SavedTrip
                    {
                        Kind = haul.Target.Kind,
                        Index = haul.Target.Index,
                        Loaded = haul.Loaded,
                    }
                    : null,
            },
            Roadside = new RoadsideSave
            {
                Animals = world.Animals.Select(a => a.RewardAvailable).ToArray(),
                Unlocked = roadside.UnlockedIndices(world.Animals),
                Completed = roadside.CompletedIndices(world.Animals),
            },
            Village = new VillageSave
            {
                HouseLevels = world.Village.Levels,
                FarmCount = world.Village.FarmCount,
                GardenCount = world.Village.GardenCount,
                GemCount = world.Village.GemCount,
                ToyCount = world.Village.ToyCount,
                TradeCounts = (int[])world.Village.TradeCounts.Clone(),
                Glazed = world.Village.GlazedFlags,
                Baked = world.Village.BakedFlags,
            },
            Buildings = new BuildingsSave
            {
                Stages = BuildStages(),
                PlotStages = PlotStages(),
                WorksStock = world.WorksStock(),
                GlassStock = world.Glassworks?.Stock ?? 0,
                BreadStock = world.Bakery?.Stock ?? 0,
                FlourStock = world.FlourMill?.Stock ?? 0,
                TrailerOwned = TrailerOwned,
                TrailerAttached = trailerParade,
                TrailerPaint = trailer.PaintId,
            },
            Companions = new CompanionsSave
            {
                GolemOwned = GolemOwned,
                GolemActive = GolemActive,
                DisabledPets = world
                    .AllPets.Where(p => p.IsOwned && p.Disabled)
                    .Select(p => p.Kind.ToString())
                    .ToArray(),
            },
            Playground = new PlaygroundSave { Stage = world.Playground?.Stage ?? 0 },
            Railway = new RailwaySave { Stage = world.Railway?.Stage ?? 0 },
            Zoo = new ZooSave { Stage = world.Zoo?.Stage ?? 0 },
            Treasure = new TreasureSave { Found = treasure.Found, Active = treasure.Active },
            Post = new PostSave
            {
                Delivered = Post.Delivered,
                Carrying = Post.Carrying,
                Reply = Post.Reply,
            },
            // A lighthouse that stands from the start, in a valley without the works for it, has had no deliveries.
            Lighthouse = new LighthouseSave { Stage = world.LighthouseBuildable ? Lighthouse?.Stage ?? 0 : 0 },
            Fireflies = new FirefliesSave { Caught = fireflies.Caught },
            Hunt = new HuntSave
            {
                Penned = Hunt.PennedNames(),
                Active = Hunt.Active,
                Carrying = Hunt.CarryingName,
                Hut = world.Pen?.Stage ?? 0,
            },
            Day = new DaySave { Clock = Weather.Clock },
            Decorations = new DecorationsSave { Owned = world.Village.OwnedDecorationIds },
            Lamps = new LampsSave
            {
                Carried = Lamps.Carried.ToArray(),
                Placed = Lamps
                    .Spots.Select(spot => new PlacedLampSave
                    {
                        Kind = spot.Kind,
                        X = spot.X,
                        Z = spot.Z,
                        Yaw = spot.Yaw,
                    })
                    .ToArray(),
            },
            Postcards = new PostcardsSave { Cards = (PostcardProgress?[])postcards.Clone(), Page = postcardPage },
            Events = new EventsSave
            {
                Active = Events.Active,
                Next = Events.Next,
                Wins = (int[])Events.Wins.Clone(),
                Best = Events.BestSplits.Select(b => (float[])b.Clone()).ToArray(),
                Time = (float)Events.Elapsed,
                Splits = Events.RunSplits.ToArray(),
            },
            Bandits = new BanditsSave
            {
                Site = banditSite,
                Solved = banditSolved,
                Cooldown = banditCooldown,
            },
            Hauls = new HaulsSave
            {
                Raw = world.Tally.RawCounts(),
                Works = world.Tally.WorksCounts(),
                Wheat = world.Tally.Wheat,
                Baked = world.Tally.Baked,
            },
            Hidden = new HiddenSave { Found = world.Hidden.FoundIndices() },
        };

    internal void RestoreProgress(SaveData data)
    {
        EndExpedition(refreshUi: false);
        CancelPostcardPhoto();
        quiz.Enabled = data.Quiz.EnabledTypes();
        quiz.NextQuiz = data.Quiz.Next;
        quiz.ThreeTermMath = data.Quiz.ThreeTermMath;
        quiz.Handover = data.Quiz.Handover;
        companionStopTime = 0;
        Array.Clear(companionWanders);
        quiz.Selection = data.Quiz.Selection;
        RestorePostcards(data);
        ResetCelebration();
        Events.Restore(
            data.Events.Active,
            data.Events.Next,
            data.Events.Wins,
            data.Events.Best,
            data.Events.Time,
            data.Events.Splits
        );
        RestoreCarPose(data);
        RestoreGolem(data.Companions.GolemOwned, data.Companions.GolemActive);
        RestoreCarLook(data);
        RestoreVillage(data);
        RestoreTreasure(data.Treasure.Found, data.Treasure.Active);
        RestorePost(data.Post.Delivered, data.Post.Carrying, data.Post.Reply);
        RestoreFireflies(data.Fireflies.Caught);
        Weather.SetClock(data.Day.Clock, instant: true);
        roadside.Restore(world.Animals, data.Roadside.Unlocked, data.Roadside.Completed);
        economy.Restore(data.Economy.Owned, data.Economy.Coins, data.Economy.RewardCoins);
        ui.CoinCount(Coins, 0);
        bool validEncounter = RestoreBandits(data);
        nearby = validEncounter ? ActorAt(data.Nearby) : null;
        CurrentChallenge = data.Quiz.Challenge;
        if (!validEncounter)
        {
            CurrentChallenge = null;
            banditSolved = 0;
        }
        quiz.Rejected.Clear();
        quiz.Rejected.UnionWith(data.Quiz.Rejected);
        // The trip that waits for the open quiz to be solved; without a question there is none.
        quiz.Departure = CurrentChallenge != null ? (Departure)data.Quiz.Departure : Departure.None;
        quiz.DepartureState = data.Quiz.DepartureState;
        deliveryReward = data.Task.DeliveryReward;
        if (data.Task.Sender >= 0)
        {
            var sender = ActorAt(data.Task.Sender)!;
            var expedition = new Expedition(world, vehicle, sender, data.Task.Collection, data.Task.Picked)
            {
                Arrived = data.Task.Arrived,
            };
            tasks.Begin(expedition);
            if (deliveryReward)
                expedition.ReleaseCargo();
        }
        lastCollected = Delivery?.Collected ?? -1;
        ui.DeliveryHud(Delivery);
        // The pen is counted before the pets are, so the hunter's fawn knows whether it is owned.
        RestoreHunt(data.Hunt.Penned, data.Hunt.Active, data.Hunt.Carrying, data.Hunt.Hut);
        // The lighthouse is raised before the trips and the pets are restored, so its trip and the gull know its stage.
        RestoreLighthouse(data.Lighthouse.Stage);
        RestoreBuilds(data);
        UpdateDeliveryGuides();
        interaction.BeforePause = validEncounter ? data.State : GameState.Exploring;
        Resume();
        foreach (var camera in GetChildren().OfType<FollowCamera>())
            camera.Snap();
    }

    private void RestorePostcards(SaveData data)
    {
        Array.Copy(data.Postcards.Cards, postcards, postcards.Length);
        postcardPage = data.Postcards.Page;
        postcardAttempt = 0;
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
        var position = new Vector3(data.Vehicle.Position[0], data.Vehicle.Position[1], data.Vehicle.Position[2]);
        // The village terrain is levelled after generation; lift a car saved below the new surface.
        float surface = world.Ground.Height(position.X, position.Z);
        if (world.Village.TerrainArea.HasPoint(new(position.X, position.Z)) && position.Y < surface - .2f)
            position.Y = surface + .15f;
        vehicle.SetPose(position, data.Vehicle.Heading);
    }

    private void RestoreCarLook(SaveData data)
    {
        vehicle.Appearance.ApplyModel(data.Vehicle.Model);
        vehicle.Appearance.ApplyTrail(data.Vehicle.Trail);
        vehicle.Appearance.Apply(CarLook.Catalogue.FirstOrDefault(l => l.Id == data.Vehicle.Look) ?? CarLook.All[0]);
        foreach (var accessory in CarAccessory.Catalogue)
            if (vehicle.Appearance.HasAccessory(accessory.Id) != data.Vehicle.Accessories.Contains(accessory.Id))
                vehicle.Appearance.ToggleAccessory(accessory.Id);
    }

    private void RestoreVillage(SaveData data)
    {
        for (int i = 0; i < world.Animals.Length; i++)
            world.Animals[i].SetRewardAvailable(data.Roadside.Animals[i]);
        world.Village.RestoreLevels(data.Village.HouseLevels);
        world.Village.RestoreGlazed(data.Village.Glazed);
        world.Village.RestoreBaked(data.Village.Baked);
        world.Village.RestoreTrades(data.Village.TradeCounts);
        world.Village.RestoreDecorations(data.Decorations.Owned);
        Lamps.Restore(
            data.Lamps.Carried,
            data.Lamps.Placed.Select(lamp => new LampSpot(lamp.Kind, lamp.X, lamp.Z, lamp.Yaw))
        );
        RefreshLampHud();
        world.Village.RestoreFarm(data.Village.FarmCount);
        world.Village.RestoreGarden(data.Village.GardenCount);
        world.Village.RestoreCollection(false, data.Village.GemCount);
        world.Village.RestoreCollection(true, data.Village.ToyCount);
        foreach (var pet in world.AllPets)
        {
            pet.Disabled = pet.IsOwned && (data.Companions.DisabledPets?.Contains(pet.Kind.ToString()) ?? false);
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
