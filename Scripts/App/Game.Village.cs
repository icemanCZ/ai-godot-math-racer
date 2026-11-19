using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.App;

public partial class Game
{
    private void ShowChallenge()
    {
        if (InBanditQuiz && banditArrival > 0)
        {
            ui.HideModal();
            return;
        }
        ui.Challenge(CurrentChallenge!, quiz.Rejected, quiz.Selection, InBanditQuiz ? banditSolved + 1 : 0);
    }

    internal void OpenHouse(Character resident)
    {
        int house = ((HouseResidentRole)resident.Role).House;
        if (Delivery?.Sender == resident)
        {
            if (Delivery.CanDeliver)
            {
                BeginDeliveryQuestion();
                return;
            }
            ClearChallenge();
            EnterInteraction(GameState.HouseCollection, resident);
            ui.HouseCollection(resident, Delivery.Collected);
            Speak(Texts.CollectedProgress(Delivery.Collected, 3), resident);
            return;
        }
        if (HouseMaterial(house) is HaulLoad.Glass or HaulLoad.Bread)
        {
            // The tenth improvement is done, and the resident asks for glasses for a housewarming, then for bread.
            ClearChallenge();
            EnterInteraction(GameState.HouseMaterialOffer, resident);
            ShowHouseMaterial();
            SpeakHouseMaterial(resident);
            return;
        }
        if (!resident.RewardAvailable)
        {
            EnterInteraction(GameState.HouseReward, resident);
            ui.HouseReward(resident, world.Village.Level(house));
            Speak(Texts.HouseProgress(world.Village.Level(house), Village.Upgrades.Length), resident);
            return;
        }
        if (world.Village.TaskAt(house) == VillageTaskKind.Fruit)
        {
            ClearChallenge();
            EnterInteraction(GameState.HouseFruitOffer, resident);
            ui.HouseFruitOffer(resident, world.Village.Level(house), true);
            Speak(Texts.OvoceNabidka, resident);
            return;
        }
        if (HouseMaterial(house) != null)
        {
            ClearChallenge();
            EnterInteraction(GameState.HouseMaterialOffer, resident);
            ShowHouseMaterial();
            SpeakHouseMaterial(resident);
            return;
        }
        BeginChallenge(resident);
    }

    /// <summary>
    /// The goods the resident of a house wants for the next improvement, or null for a quiz or an apple task.
    /// A valley without the works has no goods to haul, so its houses ask a quiz instead. A finished house asks for glasses
    /// once, if the valley has a glassworks, and once it has them for bread, if the valley has a bakery.
    /// </summary>
    private HaulLoad? HouseMaterial(int house) =>
        world.Industry.Length == 0 ? null
        : world.Village.Level(house) >= Village.Upgrades.Length
            ? world.Glassworks != null && !world.Village.IsGlazed(house) ? HaulLoad.Glass
                : world.Bakery != null && world.Village.IsGlazed(house) && !world.Village.IsBaked(house)
                    ? HaulLoad.Bread
                : null
        : Village.MaterialOf(world.Village.TaskAt(house));

    private HaulTrip? TripOf(Character resident) =>
        Haul is { House: >= 0 } trip && trip.Contractor == resident ? trip : null;

    /// <summary>Shows what the resident wants and the way to fetch it: the trip, the play button or the missing raw material.</summary>
    private bool ShowHouseMaterial()
    {
        if (
            nearby is not { Role: HouseResidentRole { House: var house } } resident
            || HouseMaterial(house) is not { } load
        )
            return false;
        var works = GoodsWorks(load)!;
        var trip = TripOf(resident);
        bool ready = works.Stock >= Works.PerTrip;
        // While a supply trip for the missing goods is under way the trailer is shown instead of a second button.
        bool fetching = Haul is { Works: { } supply } && supply == works;
        ui.HouseMaterialOffer(
            resident,
            load,
            trip ?? (fetching ? Haul : null),
            trip == null && ready,
            trip == null && !ready && !fetching && !works.Full ? works : null
        );
        return true;
    }

    private void SpeakHouseMaterial(Character resident)
    {
        var load = HouseMaterial(((HouseResidentRole)resident.Role).House)!.Value;
        var works = GoodsWorks(load)!;
        string wanted = load == HaulLoad.Planks ? Texts.Prkna : Texts.Cihly;
        bool glass = load == HaulLoad.Glass;
        bool bread = load == HaulLoad.Bread;
        Speak(
            TripOf(resident) != null ? Texts.PrivedMiTenNakladKDomecku
                : works.Stock >= Works.PerTrip
                    ? (
                        glass ? Texts.HouseNeedsGlasses
                        : bread ? Texts.HouseNeedsBread
                        : Texts.HouseNeeds(wanted)
                    )
                : glass ? Texts.HouseNeedsGlassesButNone
                : bread ? Texts.HouseNeedsBreadButNone
                : Texts.HouseNeedsButNone(wanted, works.Spec.Name.ToLower()),
            resident
        );
    }

    /// <summary>The resident's trip: take goods from the works' stock on the trailer and tip them out in front of the house.</summary>
    internal void BeginHouseTrip() => BeginHouseTrip(false);

    private void BeginHouseTrip(bool quizzed)
    {
        if (
            State != GameState.HouseMaterialOffer
            || nearby is not { Role: HouseResidentRole { House: var house } } resident
            || HouseMaterial(house) is not { } load
            || TripOf(resident) != null
            || GoodsWorks(load) is not { } works
            || works.Stock < Works.PerTrip
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.House);
            return;
        }
        StartTrip(new HouseTripTarget(world, house, load));
    }

    /// <summary>The goods are tipped out in front of the house: the house is improved.</summary>
    internal void FinishHouseDump(HaulTrip trip)
    {
        var owner = trip.Contractor;
        EndHaul();
        if (trip.Load == HaulLoad.Glass)
            CompleteGlassOrder(owner);
        else if (trip.Load == HaulLoad.Bread)
            CompleteBreadOrder(owner);
        else
            CompleteHouseTask(owner);
        SaveProgress();
    }

    /// <summary>The bread stands in front of the finished house: the resident pays and sits down to breakfast.</summary>
    private void CompleteBreadOrder(Character owner)
    {
        int house = ((HouseResidentRole)owner.Role).House;
        world.Village.Bake(house);
        AddCoins(HouseTaskCoins);
        nearby = owner;
        CelebrateCompletion(owner, Texts.BreadParty(HouseTaskCoins));
        ClearChallenge();
        deliveryReward = false;
        EnterInteraction(GameState.HouseReward);
        ui.HouseReward(owner, world.Village.Level(house));
        ShowCelebrationView();
        UpdateDeliveryGuides();
    }

    /// <summary>The glasses stand in front of the finished house: the resident pays and holds a housewarming.</summary>
    private void CompleteGlassOrder(Character owner)
    {
        int house = ((HouseResidentRole)owner.Role).House;
        world.Village.Glaze(house);
        AddCoins(HouseTaskCoins);
        nearby = owner;
        CelebrateCompletion(owner, Texts.GlassesParty(HouseTaskCoins));
        ClearChallenge();
        deliveryReward = false;
        EnterInteraction(GameState.HouseReward);
        ui.HouseReward(owner, world.Village.Level(house));
        ShowCelebrationView();
        UpdateDeliveryGuides();
    }

    internal void CompleteHouseQuiz(Character resident)
    {
        CompleteHouseTask(resident);
        SaveProgress();
    }

    internal void BeginVillageCollection()
    {
        if (
            State != GameState.HouseFruitOffer
            || nearby is not { Role: HouseResidentRole { House: var house }, RewardAvailable: true }
            || world.Village.TaskAt(house) != VillageTaskKind.Fruit
        )
            return;
        // The apple trip starts only after a solved quiz.
        quiz.Departure = Departure.Fruit;
        BeginQuiz();
    }

    private void CompleteHouseTask(Character owner)
    {
        int house = ((HouseResidentRole)owner.Role).House;
        world.Village.Upgrade(house);
        AddCoins(HouseTaskCoins);
        nearby = owner;
        string upgrade = Village.Upgrades[world.Village.Level(house) - 1];
        CelebrateCompletion(
            owner,
            Texts.NewUpgrade(upgrade),
            unload: Delivery?.Sender == owner,
            building: world.Village.Houses[house]
        );
        ClearChallenge();
        deliveryReward = false;
        EnterInteraction(GameState.HouseReward);
        ui.HouseReward(owner, world.Village.Level(house));
        ShowCelebrationView();
    }
}
