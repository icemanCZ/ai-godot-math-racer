using System;
using System.Linq;
using System.Text.Json;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Saves;

/// <summary>
/// What a save must satisfy to be loaded. Each rule says that a part of the save is invalid, and may read the rest of
/// the save, because the pieces refer to one another (the character being talked to, the panel that is open, the trip
/// under way). A new section brings its rule to <see cref="All"/>; <c>SaveStore</c> does not change.
/// </summary>
internal static class SaveRules
{
    private const int LegacyAnimalCount = 30;

    /// <summary>The rules, by the part of the save they look after. The order is the order they are tried in.</summary>
    internal static readonly (string Name, Func<SaveData, bool> Invalid)[] All =
    {
        ("world progress: postcards, events, vehicle, state", HasInvalidWorldProgress),
        ("references: roadside friends, coins, characters", HasInvalidReferences),
        ("quiz and bandits", HasInvalidQuizState),
        ("shops", HasInvalidTradeProgress),
        ("village houses and collections", HasInvalidVillageProgress),
        ("golem, sorcerer and witch", HasInvalidSpecialActorState),
        ("building plots", HasInvalidPlotState),
        ("playground", HasInvalidPlaygroundState),
        ("railway", HasInvalidRailwayState),
        ("zoo", HasInvalidZooState),
        ("treasure hunt", HasInvalidTreasure),
        ("firefly hunt", HasInvalidFireflies),
        ("time of day", HasInvalidDay),
        ("decorations and the mayor's shop", HasInvalidDecorations),
        ("glass lamps and the lamp maker's shop", HasInvalidLamps),
        ("buildings and trailer", HasInvalidBuildState),
        ("works", HasInvalidWorksState),
        ("hauled loads", HasInvalidHauls),
        ("glassworks and glazed houses", HasInvalidGlass),
        ("bakery and houses given bread", HasInvalidBread),
        ("small friends found", HasInvalidHidden),
        ("wheel trail", HasInvalidTrail),
        ("trailer trip", HasInvalidTrip),
        ("expedition cargo", HasInvalidPickupCount),
    };

    /// <summary>A file can leave a section out or set it to null; the rules and the repairs need all of them.</summary>
    internal static bool MissesSection(SaveData data) =>
        data.Vehicle is null
        || data.Economy is null
        || data.Quiz is null
        || data.Task is null
        || data.Roadside is null
        || data.Village is null
        || data.Buildings is null
        || data.Companions is null
        || data.Postcards is null
        || data.Events is null
        || data.Bandits is null
        || data.Hauls is null
        || data.Hidden is null
        || data.Playground is null
        || data.Decorations is null
        || data.Lamps is null
        || data.Railway is null
        || data.Zoo is null
        || data.Treasure is null
        || data.Fireflies is null
        || data.Day is null;

    /// <summary>Throws when the save is not one the game can restore.</summary>
    internal static void Validate(SaveData data)
    {
        if (
            data.Version != SaveData.CurrentVersion
            || data.Seed <= 0
            || data.Seed >= int.MaxValue - 4096
            || All.Any(rule => rule.Invalid(data))
        )
            throw new JsonException("Invalid save data.");
    }

    private static bool HasInvalidWorldProgress(SaveData data) =>
        data.Postcards.Cards == null
        || (
            data.Postcards.Cards.Length != Game.PostcardCount
            && data.Postcards.Cards.Length != Game.OriginalPostcardCount
        )
        || data.Postcards.Cards.Where(
                (card, i) =>
                    card != null
                    && (
                        card.Jpeg == null
                        || card.Jpeg.Length > 300000
                        || !Game.ValidPostcardSelection(i, data.Seed, card)
                    )
            )
            .Any()
        || data.Postcards.Page < -1
        || data.Postcards.Page >= data.Postcards.Cards.Length
        || (data.Postcards.Page >= 0 && data.Postcards.Cards[data.Postcards.Page] == null)
        || data.Events.Active < -1
        || data.Events.Active > 1
        || data.Events.Next < 0
        || data.Events.Next >= DrivingEvents.GateCount
        || data.Events.Wins == null
        || data.Events.Wins.Length != 2
        || data.Events.Wins.Any(w => w < 0 || w > 9999)
        || data.Events.Best == null
        || data.Events.Best.Length != 2
        || data.Events.Best.Any(best => best.Length != 0 && !DrivingEvents.ValidSplits(best))
        || data.Events.Splits == null
        || data.Events.Splits.Length != data.Events.Next
        || (data.Events.Active < 0 && (data.Events.Next != 0 || data.Events.Time != 0))
        || !float.IsFinite(data.Events.Time)
        || data.Events.Time < 0
        || data.Events.Time > 3600
        || data.Events.Splits.Any(t => !float.IsFinite(t) || t < 0 || t > data.Events.Time)
        || data.Vehicle.Position == null
        || data.Vehicle.Position.Length != 3
        || data.Vehicle.Position.Any(p => !float.IsFinite(p))
        || !float.IsFinite(data.Vehicle.Heading)
        || !Enum.IsDefined(data.State)
        || data.State == GameState.Title
        || data.State == GameState.Paused;

    private static bool HasInvalidReferences(SaveData data) =>
        data.Roadside.Animals == null
        || (data.Roadside.Animals.Length != World.QuestAnimalCount && data.Roadside.Animals.Length != LegacyAnimalCount)
        || data.Roadside.Unlocked == null
        || data.Roadside.Completed == null
        || data.Economy.Owned == null
        || data.Economy.Coins < 0
        || data.Economy.RewardCoins < 0
        || (data.Economy.RewardCoins > 0 && data.State != GameState.Reward)
        || data.Vehicle.Accessories == null
        || data.Task.Picked == null
        || data.Quiz.Rejected == null
        || data.Nearby < -1
        || data.Nearby > Math.Max(Game.BanditActorId, World.TreasurerId)
        || data.Task.Sender < -1
        || data.Task.Sender > World.SorcererId
        || data.Task.Sender == World.WitchId
        || data.Roadside.Unlocked.Concat(data.Roadside.Completed).Any(i => i < 0 || i >= data.Roadside.Animals.Length)
        || (data.Nearby >= data.Roadside.Animals.Length && data.Nearby < World.FirstResidentId)
        || (data.Task.Sender >= data.Roadside.Animals.Length && data.Task.Sender < World.FirstResidentId);

    private static bool HasInvalidQuizState(SaveData data) =>
        data.Quiz.Types == QuizType.None
        || (data.Quiz.Types & ~QuizType.All) != 0
        || data.Quiz.Next < 0
        || data.Quiz.Next >= Game.QuizRotationLength
        || (data.Quiz.Challenge is ReadingChallenge reading && !reading.IsValid())
        || data.Bandits.Site is < -1 or >= Game.MaxBanditSites
        || data.Bandits.Solved is < 0 or >= Game.BanditQuestionCount
        || !float.IsFinite(data.Bandits.Cooldown)
        || data.Bandits.Cooldown is < 0 or > Game.BanditCooldownTime
        || (
            data.Nearby == Game.BanditActorId
                ? data.State != GameState.Challenge
                    || data.Bandits.Site < 0
                    || data.Bandits.Cooldown != 0
                    || data.Quiz.Handover
                    || data.Quiz.Challenge is DeliveryChallenge
                : data.Bandits.Solved != 0
        )
        || (
            data.Quiz.Handover
            && (
                data.State != GameState.Challenge
                || data.Task.Sender < 0
                || !data.Task.Arrived
                || data.Task.DeliveryReward
                // A handover question replaces cargo counting with a word quiz.
                || data.Quiz.Challenge is not (LetterChallenge or SyllableChallenge or ReadingChallenge)
            )
        )
        || (
            data.Quiz.Challenge is SyllableChallenge syllables
                ? !syllables.IsValid() || !data.Quiz.Selection.IsValid(syllables)
                : data.Quiz.Selection != default
        )
        || (data.State is GameState.Challenge or GameState.Reward or GameState.ExpeditionChoice && data.Nearby < 0)
        || (data.State == GameState.Challenge && data.Quiz.Challenge == null)
        || HasInvalidDeparture(data);

    /// <summary>
    /// A trip waits for the open quiz: an ordinary question asked by the character who offers the trip, never a
    /// loading question or a handover. Saves from before this field have none.
    /// </summary>
    private static bool HasInvalidDeparture(SaveData data)
    {
        if (data.Quiz.Departure == 0)
            return data.Quiz.DepartureState != GameState.Title;
        if (
            !Enum.IsDefined((Game.Departure)data.Quiz.Departure)
            || data.State != GameState.Challenge
            || data.Quiz.Challenge is null or LoadChallenge or DeliveryChallenge
            || data.Quiz.Handover
            || data.Nearby == Game.BanditActorId
        )
            return true;
        bool resident = data.Nearby is >= World.FirstResidentId and < World.FarmerId;
        return (Game.Departure)data.Quiz.Departure switch
        {
            Game.Departure.Golem => data.Nearby != World.SorcererId || data.Quiz.DepartureState != GameState.Title,
            Game.Departure.Fruit => !resident || data.Quiz.DepartureState != GameState.Title,
            Game.Departure.Build => !World.IsBuilder(data.Nearby) || data.Quiz.DepartureState != GameState.BuildStatus,
            Game.Departure.Plot => !World.IsPlotResident(data.Nearby)
                || data.Quiz.DepartureState != GameState.PlotStatus,
            Game.Departure.Playground => !World.IsPlaygroundHelper(data.Nearby)
                || data.Quiz.DepartureState != GameState.PlaygroundStatus,
            Game.Departure.Railway => !World.IsStationHelper(data.Nearby)
                || data.Quiz.DepartureState != GameState.RailwayStatus,
            Game.Departure.Zoo => !World.IsZooKeeper(data.Nearby) || data.Quiz.DepartureState != GameState.ZooStatus,
            Game.Departure.House => !resident || data.Quiz.DepartureState != GameState.HouseMaterialOffer,
            // The works' missing goods are asked for from a builder, a newcomer or a resident.
            Game.Departure.Missing => data.Quiz.DepartureState switch
            {
                GameState.BuildStatus => !World.IsBuilder(data.Nearby),
                GameState.PlotStatus => !World.IsPlotResident(data.Nearby),
                GameState.PlaygroundStatus => !World.IsPlaygroundHelper(data.Nearby),
                GameState.RailwayStatus => !World.IsStationHelper(data.Nearby),
                GameState.ZooStatus => !World.IsZooKeeper(data.Nearby),
                GameState.HouseMaterialOffer => !resident,
                _ => true,
            },
            _ => true,
        };
    }

    private static bool HasInvalidTradeProgress(SaveData data) =>
        data.Village.TradeCounts == null
        || data.Village.TradeCounts.Length != 3
        || data.Village.TradeCounts.Any(c => c < 0 || c > 1000000)
        || (data.Task.Sender >= World.FirstTraderId && data.Task.Sender < World.WitchId && data.Task.Collection)
        // A shop shows its status while another task is under way: an expedition or a trailer trip.
        || (
            data.State == GameState.TradeStatus
            && (data.Nearby < World.FirstTraderId || (data.Task.Sender < 0 && data.Task.Trip == null))
        )
        || (
            data.Nearby >= World.FirstTraderId
            && data.Nearby < World.WitchId
            && data.State == GameState.Challenge
            // The opening quiz may run while another task is active; a shop never quizzes about its own order.
            && (data.Task.Sender == data.Nearby || data.Quiz.Challenge is DeliveryChallenge)
        )
        || (
            data.Task.Sender >= World.FirstTraderId
            && data.Task.Sender < World.WitchId
            && data.Task.DeliveryReward
            && (data.State != GameState.Reward || !data.Task.Arrived)
        );

    private static bool HasInvalidVillageProgress(SaveData data) =>
        data.Village.GemCount < 0
        || data.Village.GemCount > Village.GemNames.Length
        || data.Village.ToyCount < 0
        || data.Village.ToyCount > Village.ToyNames.Length
        || (
            data.Task.Sender == World.GemCollectorId
            && (data.Village.GemCount == Village.GemNames.Length || !data.Task.Collection || data.Task.DeliveryReward)
        )
        || (
            data.Task.Sender == World.ToymakerId
            && (data.Village.ToyCount == Village.ToyNames.Length || !data.Task.Collection || data.Task.DeliveryReward)
        )
        || (data.State == GameState.CollectionStatus && data.Nearby is not (World.GemCollectorId or World.ToymakerId))
        || data.Village.GardenCount < 0
        || data.Village.GardenCount > Village.FlowerNames.Length
        || (
            data.Task.Sender == World.GardenerId
            && (
                data.Village.GardenCount == Village.FlowerNames.Length
                || !data.Task.Collection
                || data.Task.DeliveryReward
            )
        )
        || (data.State == GameState.GardenStatus && data.Nearby != World.GardenerId)
        || data.Village.FarmCount < 0
        || data.Village.FarmCount > Village.FarmKinds.Length
        || (
            data.Task.Sender == World.FarmerId
            && (data.Village.FarmCount == Village.FarmKinds.Length || !data.Task.Collection || data.Task.DeliveryReward)
        )
        || (data.State == GameState.FarmStatus && data.Nearby != World.FarmerId)
        || data.Village.HouseLevels == null
        || data.Village.HouseLevels.Length != 4
        || data.Village.HouseLevels.Any(level => level < 0 || level > Village.Upgrades.Length)
        || (
            data.State == GameState.HouseReward
            && (data.Nearby < World.FirstResidentId || data.Nearby >= World.FarmerId)
        )
        || (
            data.Task.Sender >= World.FirstResidentId
            && data.Task.Sender < World.FirstTraderId
            && (!data.Task.Collection || data.Task.DeliveryReward)
        )
        || (
            data.State == GameState.HouseCollection
            && (
                data.Task.Sender < World.FirstResidentId
                || data.Task.Sender >= World.FarmerId
                || data.Nearby != data.Task.Sender
            )
        )
        || (
            data.State == GameState.HouseFruitOffer
            && (
                data.Nearby < World.FirstResidentId
                || data.Nearby >= World.FarmerId
                || data.Village.HouseLevels[data.Nearby - World.FirstResidentId] >= Village.Upgrades.Length
            )
        )
        || (
            data.State == GameState.HouseMaterialOffer
            && (
                data.Nearby < World.FirstResidentId
                || data.Nearby >= World.FarmerId
                || (
                    SavedTask(data, data.Nearby - World.FirstResidentId)
                        is not (VillageTaskKind.Planks or VillageTaskKind.Bricks)
                    && !IsGlassOrder(data, data.Nearby - World.FirstResidentId)
                    && !IsBreadOrder(data, data.Nearby - World.FirstResidentId)
                )
            )
        )
        || (
            data.Nearby >= World.FirstResidentId
            && data.Nearby < World.FirstTraderId
            && data.State is GameState.Reward or GameState.ExpeditionChoice
        )
        || (
            data.Nearby == World.FarmerId
            && data.State == GameState.Challenge
            && (
                data.Task.Sender == World.FarmerId
                || data.Village.FarmCount == Village.FarmKinds.Length
                || data.Quiz.Challenge is DeliveryChallenge
            )
        )
        || (
            data.Nearby == World.GardenerId
            && data.State == GameState.Challenge
            && (
                data.Task.Sender == World.GardenerId
                || data.Village.GardenCount == Village.FlowerNames.Length
                || data.Quiz.Challenge is DeliveryChallenge
            )
        )
        || (
            data.Nearby >= World.GemCollectorId
            && data.Nearby < World.FirstTraderId
            && data.State == GameState.Challenge
            && (
                data.Task.Sender == data.Nearby
                || (data.Nearby == World.GemCollectorId ? data.Village.GemCount : data.Village.ToyCount)
                    == Village.CollectionNames(data.Nearby == World.ToymakerId).Length
                || data.Quiz.Challenge is DeliveryChallenge
            )
        );

    private static bool HasInvalidSpecialActorState(SaveData data) =>
        (data.Companions.GolemActive && !data.Companions.GolemOwned)
        || (
            data.Task.Sender == World.SorcererId
            && (!data.Task.Collection || data.Companions.GolemOwned || data.Task.DeliveryReward)
        )
        || (data.State == GameState.SorcererStatus && data.Nearby != World.SorcererId)
        || (data.State == GameState.WeatherChoice && data.Nearby != World.WitchId)
        || (
            data.Nearby == World.WitchId
            && (
                data.State
                    is not (
                        GameState.Exploring
                        or GameState.Challenge
                        or GameState.WeatherChoice
                        or GameState.FireflyStatus
                    )
                || (data.State == GameState.Challenge && data.Quiz.Challenge is DeliveryChallenge)
            )
        );

    /// <summary>
    /// The progress of the four buildings, the trailer trip for one of them and the character met at the moment.
    /// The trailer is the bridge's reward; a trip is never set together with an expedition or a supply trip.
    /// </summary>
    private static bool HasInvalidBuildState(SaveData data) =>
        data.Buildings.Stages == null
        || data.Buildings.Stages.Length != BuildPlans.Count
        || data.Buildings.Stages.Any(stage => stage < 0 || stage > BuildPlans.Trips)
        || (data.Buildings.TrailerOwned && data.Buildings.Stages[BuildPlans.Bridge] < BuildPlans.Trips)
        || (data.Buildings.TrailerAttached && !data.Buildings.TrailerOwned)
        || data.Buildings.TrailerPaint == null
        || !Array.Exists(TrailerModel.Paints, paint => paint.Id == data.Buildings.TrailerPaint)
        || HasInvalidAsker(data);

    /// <summary>
    /// The progress of the six building plots, each 0–6 deliveries.
    /// </summary>
    private static bool HasInvalidPlotState(SaveData data) =>
        data.Buildings.PlotStages == null
        || data.Buildings.PlotStages.Length != PlotPlans.Count
        || data.Buildings.PlotStages.Any(stage => stage < 0 || stage > PlotPlans.Trips);

    /// <summary>
    /// The number of treasures found is within the limit, the captain's panel is open only beside her, and her question (the
    /// lock of a chest) is open only while a treasure is being looked for, with none of the trips that other characters ask
    /// their questions for.
    /// </summary>
    private static bool HasInvalidTreasure(SaveData data) =>
        data.Treasure.Found is < 0 or > TreasureHunt.Limit
        || (data.State == GameState.TreasureStatus && !World.IsTreasurer(data.Nearby))
        || (data.State == GameState.Challenge && World.IsTreasurer(data.Nearby) && !data.Treasure.Active)
        || (World.IsTreasurer(data.Nearby) && data.Quiz.Handover);

    /// <summary>
    /// The number of fireflies caught is within the limit of the collection, and the witch's firefly panel is open only beside her.
    /// </summary>
    private static bool HasInvalidFireflies(SaveData data) =>
        data.Fireflies.Caught < 0
        || data.Fireflies.Caught > FireflyHunt.Limit
        || (data.State == GameState.FireflyStatus && data.Nearby != World.WitchId);

    /// <summary>The clock of the day is a number of seconds within one day.</summary>
    private static bool HasInvalidDay(SaveData data) =>
        !float.IsFinite(data.Day.Clock) || data.Day.Clock < 0 || data.Day.Clock >= Weather.DayLength;

    /// <summary>The progress of the playground, 0 to 6 deliveries.</summary>
    /// <summary>Only known decorations, each at most once, and the shop is open only beside the mayor.</summary>
    private static bool HasInvalidDecorations(SaveData data) =>
        data.Decorations.Owned == null
        || data.Decorations.Owned.Any(id => TownDecorations.Find(id) == null)
        || data.Decorations.Owned.Distinct().Count() != data.Decorations.Owned.Length
        || (data.State == GameState.MayorShop && !World.IsMayor(data.Nearby));

    /// <summary>
    /// Only known lamps, within the limits of the bag and of the valley, standing inside the playable ground with a
    /// finite turn, and the lamp maker's shop is open only beside him.
    /// </summary>
    private static bool HasInvalidLamps(SaveData data) =>
        data.Lamps.Carried == null
        || data.Lamps.Placed == null
        || data.Lamps.Carried.Length > LampField.MaxCarried
        || data.Lamps.Placed.Length > LampField.MaxPlaced
        || data.Lamps.Carried.Any(kind => GlassLamps.Find(kind) == null)
        || data.Lamps.Placed.Any(lamp =>
            lamp == null
            || GlassLamps.Find(lamp.Kind) == null
            || !float.IsFinite(lamp.X)
            || !float.IsFinite(lamp.Z)
            || !float.IsFinite(lamp.Yaw)
            || Math.Abs(lamp.Yaw) > 100
            || new System.Numerics.Vector2(lamp.X, lamp.Z).Length() > Settings.PlayableRadius
        )
        || (data.State == GameState.GlassmakerShop && !World.IsGlassmaker(data.Nearby));

    private static bool HasInvalidPlaygroundState(SaveData data) =>
        data.Playground.Stage is < 0 or > PlaygroundPlans.Trips;

    /// <summary>The progress of the railway, 0 to 6 deliveries.</summary>
    private static bool HasInvalidRailwayState(SaveData data) => data.Railway.Stage is < 0 or > RailwayPlans.Trips;

    /// <summary>The progress of the zoo, 0 to 6 deliveries.</summary>
    private static bool HasInvalidZooState(SaveData data) => data.Zoo.Stage is < 0 or > ZooPlans.Trips;

    /// <summary>The cargo and the characters who may ask about it for the active trailer trip, or none without one.</summary>
    private static (HaulLoad Cargo, int[] Askers, bool Loaded) ActiveTripAsker(SaveData data)
    {
        if (data.Task.Trip is not { } trip)
            return (default, Array.Empty<int>(), false);
        switch (trip.Kind)
        {
            case TripKinds.Build
                when trip.Index is >= 0 and < BuildPlans.Count
                    && data.Buildings.Stages is { Length: BuildPlans.Count } buildStages
                    && buildStages[trip.Index] is >= 0 and < BuildPlans.Trips:
            {
                var cargo = BuildPlans.For(trip.Index)[buildStages[trip.Index]].Cargo;
                return (cargo, World.AskerIds(cargo, trip.Index), trip.Loaded);
            }
            case TripKinds.Supply when trip.Index is >= 0 and < WorksCatalog.Total:
            {
                var cargo = WorksCatalog.All[trip.Index].Input;
                // A sand pit without a worker is loaded on the glassworks' behalf, so its worker asks the question.
                var askers = World.AskerIds(cargo, 0);
                return (
                    cargo,
                    trip.Index == WorksCatalog.GlassIndex ? askers.Append(World.GlassworksId).ToArray() : askers,
                    trip.Loaded
                );
            }
            case TripKinds.Plot
                when trip.Index is >= 0 and < PlotPlans.Count
                    && data.Buildings.PlotStages is { Length: PlotPlans.Count } plotStages
                    && plotStages[trip.Index] is >= 0 and < PlotPlans.Trips:
            {
                var cargo = PlotPlans.CargoOf(plotStages[trip.Index]);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Playground when trip.Index == 0 && data.Playground.Stage is >= 0 and < PlaygroundPlans.Trips:
            {
                var cargo = PlaygroundPlans.CargoOf(data.Playground.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Railway when trip.Index == 0 && data.Railway.Stage is >= 0 and < RailwayPlans.Trips:
            {
                var cargo = RailwayPlans.CargoOf(data.Railway.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Zoo when trip.Index == 0 && data.Zoo.Stage is >= 0 and < ZooPlans.Trips:
            {
                var cargo = ZooPlans.CargoOf(data.Zoo.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.House when SavedTask(data, trip.Index) is { } kind && Village.MaterialOf(kind) is { } goods:
                return (goods, World.AskerIds(goods, 0), trip.Loaded);
            case TripKinds.House when IsGlassOrder(data, trip.Index):
                return (HaulLoad.Glass, World.AskerIds(HaulLoad.Glass, 0), trip.Loaded);
            case TripKinds.House when IsBreadOrder(data, trip.Index):
                return (HaulLoad.Bread, World.AskerIds(HaulLoad.Bread, 0), trip.Loaded);
        }
        return (default, Array.Empty<int>(), false);
    }

    /// <summary>A finished house whose resident has not yet been given glasses: the order that follows the tenth task.</summary>
    internal static bool IsGlassOrder(SaveData data, int house) =>
        house is >= 0 and < 4
        && data.Village.HouseLevels is { Length: 4 } levels
        && levels[house] == 10
        && data.Village.Glazed is { Length: 4 } glazed
        && !glazed[house];

    /// <summary>A finished house whose resident has glasses but no bread yet: the order that follows the housewarming.</summary>
    internal static bool IsBreadOrder(SaveData data, int house) =>
        house is >= 0 and < 4
        && data.Village.HouseLevels is { Length: 4 } levels
        && levels[house] == 10
        && data.Village.Glazed is { Length: 4 } glazed
        && glazed[house]
        && data.Village.Baked is { Length: 4 } baked
        && !baked[house];

    /// <summary>The bread stock is within the yard, and only a house that has glasses can have been given bread.</summary>
    private static bool HasInvalidBread(SaveData data) =>
        data.Buildings.BreadStock is < 0 or > Works.Capacity
        || data.Village.Baked == null
        || data.Village.Baked.Length != 4
        || data.Village.Glazed == null
        || data.Village.Glazed.Length != 4
        || data.Village.HouseLevels == null
        || data.Village.HouseLevels.Length != 4
        || Enumerable
            .Range(0, 4)
            .Any(house =>
                data.Village.Baked[house] && (!data.Village.Glazed[house] || data.Village.HouseLevels[house] < 10)
            );

    /// <summary>The trail on the wheels is the plain one or a stamp that was bought.</summary>
    private static bool HasInvalidTrail(SaveData data) =>
        !CarTrail.IsKnown(data.Vehicle.Trail)
        || (
            data.Vehicle.Trail != CarTrail.Plain.Id
            && !data.Economy.Owned.Contains(new CarReward(data.Vehicle.Trail, false, false, true))
        );

    /// <summary>The found friends are numbers of the friends that exist, each listed once.</summary>
    private static bool HasInvalidHidden(SaveData data) =>
        data.Hidden.Found == null
        || data.Hidden.Found.Length > HiddenFriends.Total
        || data.Hidden.Found.Any(index => index < 0 || index >= HiddenFriends.Total)
        || data.Hidden.Found.Distinct().Count() != data.Hidden.Found.Length;

    /// <summary>The glass stock is within the yard, and only a finished house can have been given glasses.</summary>
    private static bool HasInvalidGlass(SaveData data) =>
        data.Buildings.GlassStock is < 0 or > Works.Capacity
        || data.Village.Glazed == null
        || data.Village.Glazed.Length != 4
        || data.Village.HouseLevels == null
        || data.Village.HouseLevels.Length != 4
        || Enumerable.Range(0, 4).Any(house => data.Village.Glazed[house] && data.Village.HouseLevels[house] < 10);

    /// <summary>The task of a house's next improvement in a saved valley; null for a finished house or damaged levels.</summary>
    internal static VillageTaskKind? SavedTask(SaveData data, int house) =>
        data.Seed > 0
        && data.Village.HouseLevels is { Length: 4 } levels
        && house is >= 0 and < 4
        && levels[house] is >= 0 and < 10
            ? Village.CreateTasks(data.Seed, house)[levels[house]]
            : null;

    /// <summary>
    /// A trailer trip fetches the next delivery of its target, so that target must exist and be unfinished: a
    /// building or plot with deliveries left, a works that is not full, a house that asks for planks or bricks. It
    /// never runs together with an expedition.
    /// </summary>
    private static bool HasInvalidTrip(SaveData data)
    {
        if (data.Task.Trip is not { } trip)
            return false;
        if (data.Task.Sender >= 0)
            return true;
        return trip.Kind switch
        {
            TripKinds.Build => trip.Index is < 0 or >= BuildPlans.Count
                || data.Buildings.Stages![trip.Index] >= BuildPlans.Trips,
            TripKinds.Supply => trip.Index is < 0 or >= WorksCatalog.Total
                || (
                    trip.Index == WorksCatalog.GlassIndex ? data.Buildings.GlassStock
                    : trip.Index == WorksCatalog.BreadIndex ? data.Buildings.BreadStock
                    : data.Buildings.WorksStock[trip.Index]
                ) >= Works.Capacity,
            TripKinds.House => trip.Index is < 0 or >= 4
                || (
                    SavedTask(data, trip.Index) is not (VillageTaskKind.Planks or VillageTaskKind.Bricks)
                    && !IsGlassOrder(data, trip.Index)
                    && !IsBreadOrder(data, trip.Index)
                ),
            TripKinds.Plot => trip.Index is < 0 or >= PlotPlans.Count
                || data.Buildings.PlotStages[trip.Index] >= PlotPlans.Trips,
            TripKinds.Playground => trip.Index != 0 || data.Playground.Stage >= PlaygroundPlans.Trips,
            TripKinds.Railway => trip.Index != 0 || data.Railway.Stage >= RailwayPlans.Trips,
            TripKinds.Zoo => trip.Index != 0 || data.Zoo.Stage >= ZooPlans.Trips,
            _ => true,
        };
    }

    /// <summary>
    /// Builders, works workers and source workers show a panel of their own and, during a trailer trip, ask the
    /// loading question. A question can only be open for the trip's own asker, and only before the load is aboard.
    /// </summary>
    private static bool HasInvalidAsker(SaveData data)
    {
        GameState? panel =
            World.IsBuilder(data.Nearby) ? GameState.BuildStatus
            : data.Nearby is >= World.FirstWorksId and <= World.LastWorksId or World.GlassworksId or World.BakeryId
                ? GameState.WorksStatus
            : data.Nearby is >= World.FirstSourceId and <= World.LastSourceId or World.FieldFarmerId
                ? GameState.SourceStatus
            : World.IsPlotResident(data.Nearby) ? GameState.PlotStatus
            : World.IsPlaygroundHelper(data.Nearby) ? GameState.PlaygroundStatus
            : World.IsStationHelper(data.Nearby) ? GameState.RailwayStatus
            : World.IsZooKeeper(data.Nearby) ? GameState.ZooStatus
            : null;
        if (panel == null)
        {
            // The panels belong to their characters, and a loading question needs one of them to ask it.
            return data.State
                    is GameState.BuildStatus
                        or GameState.WorksStatus
                        or GameState.SourceStatus
                        or GameState.PlotStatus
                        or GameState.PlaygroundStatus
                        or GameState.RailwayStatus
                        or GameState.ZooStatus
                || data.Quiz.Challenge is LoadChallenge;
        }
        if (data.State is not (GameState.Exploring or GameState.Challenge) && data.State != panel)
            return true;
        var (cargo, askers, loaded) = ActiveTripAsker(data);
        // A quiz before a trip is asked by the character who offers it, whatever trip is under way meanwhile.
        if (data.State == GameState.Challenge && data.Quiz.Departure == 0)
        {
            if (
                !askers.Contains(data.Nearby)
                || loaded
                || data.Quiz.Handover
                || data.Quiz.Challenge is DeliveryChallenge
            )
                return true;
        }
        return data.Quiz.Challenge is LoadChallenge load
            && (!load.IsValid() || askers.Length == 0 || load.Cargo != cargo);
    }

    /// <summary>
    /// The stock of the three works, which never exceeds the yard.
    /// </summary>
    private static bool HasInvalidWorksState(SaveData data) =>
        data.Buildings.WorksStock == null
        || data.Buildings.WorksStock.Length != WorksCatalog.Count
        || data.Buildings.WorksStock.Any(stock => stock < 0 || stock > Works.Capacity);

    /// <summary>The counts of hauled loads: one for each raw material and each works, none negative or beyond the limit.</summary>
    private static bool HasInvalidHauls(SaveData data) =>
        data.Hauls.Raw == null
        || data.Hauls.Raw.Length != HaulTally.RawKinds
        || data.Hauls.Works == null
        || data.Hauls.Works.Length != HaulTally.WorksKinds
        || data.Hauls.Raw.Concat(data.Hauls.Works)
            .Concat(new[] { data.Hauls.Wheat, data.Hauls.Baked })
            .Any(count => count < 0 || count > HaulTally.Limit);

    private static bool HasInvalidPickupCount(SaveData data) =>
        (
            data.Task.Sender >= 0
            && data.Task.Picked.Length
                != (
                    data.Task.Sender == World.SorcererId ? 6
                    : data.Task.Sender >= World.FarmerId && data.Task.Sender < World.FirstTraderId ? 1
                    : data.Task.Collection ? 3
                    : 0
                )
        );
}
