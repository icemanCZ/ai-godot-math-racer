using System;
using System.Linq;
using System.Text.Json;

namespace MathRacer;

/// <summary>
/// Moves a save of an older game forward. A file of version 1 to 4 is read into the flat <see cref="SaveDataV4"/>,
/// goes through the steps that bring it to version 4, and is then moved into the sections of
/// <see cref="SaveData"/>; <see cref="Repair"/> then fixes what the player's game left behind in any version. A
/// migration is never deleted and never made stricter: an old save must keep loading.
/// </summary>
internal static class SaveMigrations
{
    /// <summary>Reads a save of version 1 to 4 and brings it to version 4.</summary>
    internal static SaveDataV4 ReadLegacy(string json)
    {
        var data = JsonSerializer.Deserialize<SaveDataV4>(json) ?? throw new JsonException("Empty save data.");
        MigrateLegacyQuestion(data);
        MigrateRewardOffer(data);
        MigrateBuilds(data);
        MigrateTrips(data);
        return data;
    }

    /// <summary>The save of version 4 in the sections of version 5. Every field has exactly one place.</summary>
    internal static SaveData ToCurrent(SaveDataV4 old) =>
        new()
        {
            Version = SaveData.CurrentVersion,
            Seed = old.Seed,
            State = old.State,
            Nearby = old.Nearby,
            Vehicle = new VehicleSave
            {
                Position = old.Position,
                Heading = old.Heading,
                Look = old.Look,
                Model = old.Model,
                Accessories = old.Accessories,
            },
            Economy = new EconomySave
            {
                Coins = old.Coins,
                RewardCoins = old.RewardCoins,
                Owned = old.Owned,
            },
            Quiz = new QuizSave
            {
                Types = old.QuizTypes,
                ThreeTermMath = old.ThreeTermMath,
                Next = old.NextQuiz,
                Handover = old.HandoverQuiz,
                Challenge = old.Challenge,
                Selection = old.SyllableSelection,
                Rejected = old.Rejected,
                Departure = old.Departure,
                DepartureState = old.DepartureState,
            },
            Task = new TaskSave
            {
                Sender = old.Sender,
                Collection = old.Collection,
                Picked = old.Picked,
                Arrived = old.Arrived,
                DeliveryReward = old.DeliveryReward,
                Trip = old.Trip,
            },
            Roadside = new RoadsideSave
            {
                Animals = old.Animals,
                Unlocked = old.Unlocked,
                Completed = old.Completed,
            },
            Village = new VillageSave
            {
                HouseLevels = old.HouseLevels,
                FarmCount = old.FarmCount,
                GardenCount = old.GardenCount,
                GemCount = old.GemCount,
                ToyCount = old.ToyCount,
                TradeCounts = old.TradeCounts,
            },
            Buildings = new BuildingsSave
            {
                Stages = old.BuildStages!,
                PlotStages = old.PlotStages,
                WorksStock = old.WorksStock,
                TrailerOwned = old.TrailerOwned,
                TrailerAttached = old.TrailerAttached,
                TrailerPaint = old.TrailerPaint,
            },
            Companions = new CompanionsSave
            {
                GolemOwned = old.GolemOwned,
                GolemActive = old.GolemActive,
                DisabledPets = old.DisabledCollectorPets,
            },
            Postcards = new PostcardsSave { Cards = old.Postcards, Page = old.PostcardPage },
            Events = new EventsSave
            {
                Active = old.EventActive,
                Next = old.EventNext,
                Wins = old.EventWins,
                Best = old.EventBest,
                Time = old.EventTime,
                Splits = old.EventSplits,
            },
            Bandits = new BanditsSave
            {
                Site = old.BanditSite,
                Solved = old.BanditSolved,
                Cooldown = old.BanditCooldown,
            },
        };

    /// <summary>What the game of any version left behind that a save can be repaired from, before it is validated.</summary>
    internal static void Repair(SaveData data)
    {
        MigrateEventRun(data);
        MigrateHouseTasks(data);
    }

    /// <summary>Repairs that follow validation: the postcard album grew and the roadside friends became fewer.</summary>
    internal static void AfterValidation(SaveData data)
    {
        if (data.Postcards.Cards.Length == Game.OriginalPostcardCount)
        {
            var expanded = new PostcardProgress?[Game.PostcardCount];
            data.Postcards.Cards.CopyTo(expanded, 0);
            data.Postcards.Cards = expanded;
        }
        MigrateAnimalCount(data);
    }

    /// <summary>Moves a version 1 question from its per-kind field into <see cref="SaveDataV4.Challenge"/>.</summary>
    private static void MigrateLegacyQuestion(SaveDataV4 data)
    {
        data.Challenge ??= (IChallenge?)data.Math
            ?? (IChallenge?)data.Letters
            ?? (IChallenge?)data.Syllables
            ?? (IChallenge?)data.Reading
            ?? data.DeliveryQuestion;
        data.Math = null;
        data.Letters = null;
        data.Syllables = null;
        data.Reading = null;
        data.DeliveryQuestion = null;
        if (data.Version == 1)
            data.Version = 2;
    }

    /// <summary>
    /// Version 2 offered a choice of car items on the reward panel. A save made there pays the coins of the
    /// new reward instead, so the finished task is not lost.
    /// </summary>
    private static void MigrateRewardOffer(SaveDataV4 data)
    {
        if (data.Version != 2)
            return;
        data.Version = 3;
        if (data.State != GameState.Reward || !data.DeliveryReward)
            return;
        bool trade = data.Sender >= World.FirstTraderId && data.Sender < World.WitchId;
        data.RewardCoins = trade ? Game.TradeCoins : Game.RoadsideDeliveryCoins;
        data.Coins += data.RewardCoins;
    }

    /// <summary>
    /// Older saves have only the bridge, with six deliveries. A finished bridge stays finished, an unfinished one
    /// keeps its share of the work and a trip under way is dropped, since the loads have changed.
    /// </summary>
    private static void MigrateBuilds(SaveDataV4 data)
    {
        if (data.BuildStages != null)
            return;
        int old = Math.Clamp(data.FordStage, 0, 6);
        int stage = old >= 6 ? BuildPlans.Trips : old * BuildPlans.Trips / 6;
        data.BuildStages = new int[BuildPlans.Count];
        data.BuildStages[BuildPlans.Bridge] = stage;
        if (data.FordTrip >= 0 && data.Challenge is LoadChallenge)
        {
            ClearQuestion(data);
            data.State = GameState.Exploring;
            data.Nearby = -1;
        }
        data.FordStage = 0;
        data.FordTrip = -1;
        data.FordTripLoaded = false;
    }

    /// <summary>
    /// Before version 4 each kind of trailer trip had a pair of fields. A save with one trip gets it as
    /// <see cref="SaveDataV4.Trip"/>. A save with two trips, a loaded flag without a trip or a number that is not a
    /// trip stays damaged, so that validation refuses it as it always did.
    /// </summary>
    private static void MigrateTrips(SaveDataV4 data)
    {
        var trips = new System.Collections.Generic.List<SavedTrip>();
        bool damaged = false;
        void Take(string kind, int? index, bool? loaded)
        {
            if (index is null or -1)
                damaged |= loaded == true;
            else if (index < -1)
                damaged = true;
            else
                trips.Add(
                    new SavedTrip
                    {
                        Kind = kind,
                        Index = index.Value,
                        Loaded = loaded == true,
                    }
                );
        }
        Take(TripKinds.Build, data.BuildTrip, data.BuildTripLoaded);
        Take(TripKinds.Supply, data.SupplyTrip, data.SupplyTripLoaded);
        Take(TripKinds.House, data.HouseTrip, data.HouseTripLoaded);
        Take(TripKinds.Plot, data.PlotTrip, data.PlotTripLoaded);
        data.BuildTrip = data.SupplyTrip = data.HouseTrip = data.PlotTrip = null;
        data.BuildTripLoaded = data.SupplyTripLoaded = data.HouseTripLoaded = data.PlotTripLoaded = null;
        if (damaged || trips.Count > 1 || (trips.Count == 1 && data.Trip != null))
            data.Trip = new SavedTrip { Kind = "damaged", Index = -1 };
        else if (trips.Count == 1)
            data.Trip = trips[0];
        if (data.Version == 3)
            data.Version = 4;
    }

    private static void ClearQuestion(SaveDataV4 data)
    {
        data.Challenge = null;
        data.SyllableSelection = default;
        data.Rejected = Array.Empty<int>();
        data.HandoverQuiz = false;
    }

    /// <summary>
    /// Houses used to ask only quizzes and apples. A save that waits at a house whose next task is now different
    /// (an apple offer, or a quiz where the house now wants goods) goes back to driving, so the rest of it is kept.
    /// </summary>
    private static void MigrateHouseTasks(SaveData data)
    {
        if (data.Nearby is < World.FirstResidentId or >= World.FarmerId || data.Task.Sender >= 0)
            return;
        var task = SaveRules.SavedTask(data, data.Nearby - World.FirstResidentId);
        // The quiz before a goods trip is meant: the house asks for goods and the trip follows the quiz.
        bool stale =
            data.State == GameState.HouseFruitOffer && task != VillageTaskKind.Fruit
            || data.State == GameState.Challenge
                && data.Quiz.Departure == 0
                && data.Quiz.Challenge is not null and not LoadChallenge
                && Village.MaterialOf(task ?? VillageTaskKind.Math) != null;
        if (!stale)
            return;
        data.State = GameState.Exploring;
        data.Nearby = -1;
        ClearQuestion(data);
    }

    private static void MigrateAnimalCount(SaveData data)
    {
        if (data.Roadside.Animals.Length == World.QuestAnimalCount)
            return;
        data.Roadside.Animals = data.Roadside.Animals.Take(World.QuestAnimalCount).ToArray();
        data.Roadside.Unlocked = data.Roadside.Unlocked.Where(i => i < World.QuestAnimalCount).ToArray();
        data.Roadside.Completed = data.Roadside.Completed.Where(i => i < World.QuestAnimalCount).ToArray();
        bool Removed(int id) => id >= World.QuestAnimalCount && id < World.FirstResidentId;
        // Village actor IDs stay at 30–40 so existing house and trade progress keeps its meaning.
        if (Removed(data.Task.Sender))
        {
            data.Task.Sender = -1;
            data.Task.Collection = false;
            data.Task.Picked = Array.Empty<bool>();
            data.Task.Arrived = false;
            data.Task.DeliveryReward = false;
            data.State = GameState.Exploring;
            ClearQuestion(data);
        }
        if (Removed(data.Nearby))
        {
            data.Nearby = -1;
            data.State = GameState.Exploring;
            ClearQuestion(data);
            data.Task.DeliveryReward = false;
            // A retained delivery may have been at a customer removed from this smaller world.
            data.Task.Arrived = false;
        }
    }

    /// <summary>
    /// Saves from before the ghost race have no clock or gate times. A run that was under way is dropped, and a
    /// damaged record falls back to the pace ghost, so neither costs the player the rest of the save.
    /// </summary>
    private static void MigrateEventRun(SaveData data)
    {
        if (data.Events.Best is not { Length: 2 })
            data.Events.Best = new[] { Array.Empty<float>(), Array.Empty<float>() };
        for (int i = 0; i < 2; i++)
            if (data.Events.Best[i] == null || !DrivingEvents.ValidSplits(data.Events.Best[i]))
                data.Events.Best[i] = Array.Empty<float>();
        bool consistent =
            data.Events.Splits != null
            && data.Events.Splits.Length == data.Events.Next
            && data.Events.Active >= 0
            && float.IsFinite(data.Events.Time)
            && data.Events.Time >= 0;
        if (consistent)
            return;
        data.Events.Splits = Array.Empty<float>();
        data.Events.Time = 0;
        if (data.Events.Active >= 0 || data.Events.Next != 0)
        {
            data.Events.Active = -1;
            data.Events.Next = 0;
        }
    }

    private static void ClearQuestion(SaveData data)
    {
        data.Quiz.Challenge = null;
        data.Quiz.Selection = default;
        data.Quiz.Rejected = Array.Empty<int>();
        data.Quiz.Handover = false;
    }
}
