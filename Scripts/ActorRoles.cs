namespace MathRacer;

/// <summary>
/// What a character does when the player meets them. <see cref="Game"/> owns the activity flows;
/// a role picks the flow for its character and describes how the character is presented.
/// </summary>
internal abstract class ActorRole
{
    /// <summary>Icon drawn on the task marker, or "" for the plain question mark.</summary>
    public virtual string Icon => "";

    /// <summary>False when the character manages its own task marker instead of the delivery guides.</summary>
    public virtual bool UsesDeliveryGuide => true;

    /// <summary>Whether the talk prompt appears even without a pending quiz or expedition choice.</summary>
    public virtual bool InvitesTalk(bool expeditionActive) => false;

    public virtual void ShowHint(GameUi ui, Animal actor) { }

    public abstract void Open(Game game, Animal actor);

    /// <summary>Continues after the character's quiz is solved. Returns false to use the roadside reward flow.</summary>
    public virtual bool CompleteQuiz(Game game, Animal actor, bool handover) => false;
}

/// <summary>A roadside friend: an opening quiz, then a delivery or apple collection with a car reward.</summary>
internal sealed class RoadsideFriendRole : ActorRole
{
    public static RoadsideFriendRole Instance { get; } = new();

    private RoadsideFriendRole() { }

    public override void Open(Game game, Animal actor) => game.OpenRoadsideFriend(actor);
}

/// <summary>A village resident whose house improves with each finished task.</summary>
internal sealed class HouseResidentRole : ActorRole
{
    public int House { get; }

    public HouseResidentRole(int house) => House = house;

    public override string Icon => "person";

    public override bool InvitesTalk(bool expeditionActive) => true;

    public override void ShowHint(GameUi ui, Animal actor) => ui.HouseHint();

    public override void Open(Game game, Animal actor) => game.OpenHouse(actor);

    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        // A handover belongs to the expedition; Game finishes it.
        if (handover)
            return false;
        game.CompleteHouseQuiz(actor);
        return true;
    }
}

/// <summary>A shop owner with repeatable road deliveries.</summary>
internal sealed class TraderRole : ActorRole
{
    public int Index { get; }

    public TraderRole(int index) => Index = index;

    public override string Icon => "hand";

    public override void ShowHint(GameUi ui, Animal actor) => ui.TradeHint(actor);

    public override void Open(Game game, Animal actor) => game.OpenTrade(actor);

    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        game.StartTrade();
        return true;
    }
}

internal sealed class WitchRole : ActorRole
{
    public override string Icon => "cloud";
    public override bool UsesDeliveryGuide => false;

    public override void ShowHint(GameUi ui, Animal actor) => ui.WitchHint();

    public override void Open(Game game, Animal actor) => game.OpenWeatherChallenge();

    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        game.OfferWeatherChoice();
        return true;
    }
}

internal sealed class SorcererRole : ActorRole
{
    public override string Icon => "sorcerer";
    public override bool UsesDeliveryGuide => false;

    public override void ShowHint(GameUi ui, Animal actor) => ui.SorcererHint();

    public override void Open(Game game, Animal actor) => game.OpenSorcerer();
}

/// <summary>
/// The character who gives the work for one building: Bóďa the beaver for the bridge, later the miller, the
/// stargazer and the fisherman. They send the trailer with the next delivery and run their own task marker.
/// </summary>
internal sealed class BuilderRole : ActorRole
{
    public int Project { get; }

    public BuilderRole(int project) => Project = project;

    public override string Icon =>
        Project switch
        {
            BuildPlans.Mill => "mill",
            BuildPlans.Tower => "tower",
            BuildPlans.Jetty => "jetty",
            _ => "bridge",
        };
    public override bool UsesDeliveryGuide => false;

    public override void ShowHint(GameUi ui, Animal actor) => ui.BuilderHint(Project);

    public override void Open(Game game, Animal actor) => game.OpenBuilder(actor);

    /// <summary>A builder's only quiz is the loading question for a raw load that has no worker of its own.</summary>
    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        game.CompleteHaulLoading();
        return true;
    }
}

/// <summary>
/// The newcomer of a building plot. Talking to them offers the next delivery of material for their house; once the
/// house is finished they live in it. They send the trailer and run their own task marker, like a builder.
/// </summary>
internal sealed class PlotResidentRole : ActorRole
{
    public HousePlot Plot { get; }

    public PlotResidentRole(HousePlot plot) => Plot = plot;

    public override string Icon => "plot";
    public override bool UsesDeliveryGuide => false;

    public override bool InvitesTalk(bool expeditionActive) => true;

    public override void ShowHint(GameUi ui, Animal actor) => ui.PlotHint(Plot.Complete);

    public override void Open(Game game, Animal actor) => game.OpenPlot(actor);

    /// <summary>A newcomer asks no quiz of their own: the loading questions come from the works' workers.</summary>
    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        game.CompleteHaulLoading();
        return true;
    }
}

/// <summary>
/// The worker at the sawmill, brickworks or gravel works. They show the stock and, for building trips, load the
/// goods and ask the loading question from afar. Raw-material trips are started by the worker at the source.
/// </summary>
internal sealed class WorksRole : ActorRole
{
    public int Index { get; }

    public WorksRole(int index) => Index = index;

    public override string Icon => WorksCatalog.All[Index].Icon;
    public override bool UsesDeliveryGuide => false;

    public override void ShowHint(GameUi ui, Animal actor) => ui.WorksHint(Index);

    public override void Open(Game game, Animal actor) => game.OpenWorks(actor);

    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        game.CompleteHaulLoading();
        return true;
    }
}

/// <summary>
/// The worker at the quarry, the clay pit or the log yard. Talking to them offers the trip that takes the raw
/// material to its works; the loading question follows at once, asked by the worker.
/// </summary>
internal sealed class SourceRole : ActorRole
{
    public HaulLoad Load { get; }

    /// <summary>Position in <see cref="SourceCatalog.All"/>; -1 for the sand pit's worker, who has no works.</summary>
    public int Index => SourceCatalog.IndexOf(Load);

    public SourceRole(HaulLoad load) => Load = load;

    public override string Icon => SourceCatalog.IconOf(Load);
    public override bool UsesDeliveryGuide => false;

    public override void ShowHint(GameUi ui, Animal actor) => ui.SourceHint(Load);

    public override void Open(Game game, Animal actor) => game.OpenSource(actor);

    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        game.CompleteHaulLoading();
        return true;
    }
}

/// <summary>A resident who fills a village display, one off-road expedition per item.</summary>
internal abstract class CollectorRole : ActorRole
{
    public override string Icon => "basket";

    public override bool InvitesTalk(bool expeditionActive) => !expeditionActive;

    public override void Open(Game game, Animal actor) => game.OpenCollector(actor, this);

    public override bool CompleteQuiz(Game game, Animal actor, bool handover)
    {
        game.StartCollectorExpedition(actor, this);
        return true;
    }

    public abstract int Count(Village village);
    public abstract int Total { get; }

    /// <summary>
    /// First of this collector's slots in <see cref="World.SpecialPickupSpots"/>, which holds the farm,
    /// garden, gem and toy pickups in that order; item n uses slot offset + n.
    /// </summary>
    public abstract int PickupSlotOffset { get; }
    public abstract GameState StatusState { get; }

    /// <summary>Celebration text for a delivered item that does not complete the companion.</summary>
    public abstract string DeliveredMessage { get; }
    public abstract void Restore(Village village, int count);
    public abstract void ShowStatus(
        GameUi ui,
        Village village,
        Animal owner,
        Expedition? expedition,
        bool returned = false
    );
}

internal sealed class FarmerRole : CollectorRole
{
    public override void ShowHint(GameUi ui, Animal actor) => ui.FarmHint();

    public override int Count(Village village) => village.FarmCount;

    public override int Total => Village.FarmKinds.Length;
    public override int PickupSlotOffset => 0;
    public override GameState StatusState => GameState.FarmStatus;
    public override string DeliveredMessage => "Zvířátko je doma! Děkujeme!";

    public override void Restore(Village village, int count) => village.RestoreFarm(count);

    public override void ShowStatus(
        GameUi ui,
        Village village,
        Animal owner,
        Expedition? expedition,
        bool returned = false
    ) => ui.FarmStatus(village, expedition, returned);
}

internal sealed class GardenerRole : CollectorRole
{
    public override void ShowHint(GameUi ui, Animal actor) => ui.GardenHint();

    public override int Count(Village village) => village.GardenCount;

    public override int Total => Village.FlowerNames.Length;
    public override int PickupSlotOffset => Village.FarmKinds.Length;
    public override GameState StatusState => GameState.GardenStatus;
    public override string DeliveredMessage => "Nová květina na zahrádce!";

    public override void Restore(Village village, int count) => village.RestoreGarden(count);

    public override void ShowStatus(
        GameUi ui,
        Village village,
        Animal owner,
        Expedition? expedition,
        bool returned = false
    ) => ui.GardenStatus(village, expedition, returned);
}

/// <summary>The gem collector's museum or, with <see cref="Toys"/>, the toymaker's playground.</summary>
internal sealed class ExhibitCollectorRole : CollectorRole
{
    public bool Toys { get; }

    public ExhibitCollectorRole(bool toys) => Toys = toys;

    public override void ShowHint(GameUi ui, Animal actor) => ui.CollectionHint(Toys);

    public override int Count(Village village) => village.CollectionCount(Toys);

    public override int Total => Village.CollectionNames(Toys).Length;
    public override int PickupSlotOffset =>
        Village.FarmKinds.Length + Village.FlowerNames.Length + (Toys ? Village.GemNames.Length : 0);
    public override GameState StatusState => GameState.CollectionStatus;
    public override string DeliveredMessage => "Nový poklad ve sbírce! Děkujeme!";

    public override void Restore(Village village, int count) => village.RestoreCollection(Toys, count);

    public override void ShowStatus(
        GameUi ui,
        Village village,
        Animal owner,
        Expedition? expedition,
        bool returned = false
    ) => ui.CollectionStatus(village, owner, expedition, returned);
}
