using System;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Valley;

namespace MathRacer.Saves;

/// <summary>
/// The save of the current version: the world seed, where the player is, and one section for each part of the game
/// that has progress to keep. Plain data only: scene nodes are regenerated from the saved world seed. Every section has
/// defaults for everything it holds, so a save made before a field existed still loads. A new part of the game adds its
/// section here, its rule to <see cref="SaveRules.All"/> and its capture and restore to <c>Game.Persistence</c>.
/// Version 5 introduced the sections; the flat save of versions 1 to 4 is <see cref="SaveDataV4"/>.
/// </summary>
public sealed class SaveData
{
    public const int CurrentVersion = 5;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>The seed of the valley; the world is generated from it again when the save is loaded.</summary>
    public int Seed { get; set; }

    /// <summary>The interaction the player was in, never Title or Paused.</summary>
    public GameState State { get; set; }

    /// <summary>The character the player was talking to (an actor ID, see <see cref="ActorIds"/>), or -1.</summary>
    public int Nearby { get; set; } = -1;

    public VehicleSave Vehicle { get; set; } = new();
    public EconomySave Economy { get; set; } = new();
    public QuizSave Quiz { get; set; } = new();

    /// <summary>The single task slot: an expedition or a trailer trip, never both.</summary>
    public TaskSave Task { get; set; } = new();

    public RoadsideSave Roadside { get; set; } = new();
    public VillageSave Village { get; set; } = new();
    public BuildingsSave Buildings { get; set; } = new();
    public CompanionsSave Companions { get; set; } = new();
    public PostcardsSave Postcards { get; set; } = new();
    public EventsSave Events { get; set; } = new();
    public BanditsSave Bandits { get; set; } = new();

    /// <summary>The loads hauled so far, which earn the pets of the sources and the works. Added after version 5 began.</summary>
    public HaulsSave Hauls { get; set; } = new();

    /// <summary>The small friends found so far, which earn their pets. Added after version 5 began.</summary>
    public HiddenSave Hidden { get; set; } = new();

    /// <summary>The playground's progress. Added after version 5 began.</summary>
    public PlaygroundSave Playground { get; set; } = new();

    /// <summary>The decorations of the town bought from the mayor. Added after version 5 began.</summary>
    public DecorationsSave Decorations { get; set; } = new();

    /// <summary>The glass lamps in the bag and on the ground. Added after version 5 began.</summary>
    public LampsSave Lamps { get; set; } = new();

    /// <summary>The little railway's progress. Added after version 5 began.</summary>
    public RailwaySave Railway { get; set; } = new();

    /// <summary>The zoo's progress. Added after version 5 began.</summary>
    public ZooSave Zoo { get; set; } = new();

    /// <summary>The pirate captain's treasure hunt. Added after version 5 began.</summary>
    public TreasureSave Treasure { get; set; } = new();

    /// <summary>The fireflies caught for the witch's collection of stars. Added after version 5 began.</summary>
    public FirefliesSave Fireflies { get; set; } = new();

    /// <summary>The hour of the valley's day, so a game goes on at the same time of day. Added after version 5 began.</summary>
    public DaySave Day { get; set; } = new();

    /// <summary>The hunter's pen and the hunt under way. Added after version 5 began.</summary>
    public HuntSave Hunt { get; set; } = new();

    /// <summary>The post to the keepers of the borders. Added after version 5 began.</summary>
    public PostSave Post { get; set; } = new();

    /// <summary>The lighthouse on the shore. Added after version 5 began.</summary>
    public LighthouseSave Lighthouse { get; set; } = new();

    /// <summary>The dragon under the mountains. Added after version 5 began.</summary>
    public DragonSave Dragon { get; set; } = new();

    /// <summary>The balloon station far beyond the village. Added after version 5 began.</summary>
    public BalloonSave Balloon { get; set; } = new();

    /// <summary>The forest keeper's book of tracks. Added after version 5 began.</summary>
    public TrackingSave Tracking { get; set; } = new();

    /// <summary>The shepherd's lost sheep. Added after version 5 began.</summary>
    public SheepSave Sheep { get; set; } = new();

    /// <summary>The ferrywoman's rally. Added after version 5 began.</summary>
    public RallySave Rally { get; set; } = new();

    /// <summary>The owl's map of the wonders beyond the old valley. Added after version 5 began.</summary>
    public ExplorerSave Explorer { get; set; } = new();

    /// <summary>The shells on the beach of the sea. Added after version 5 began.</summary>
    public ShellsSave Shells { get; set; } = new();
}

/// <summary>
/// The owl's map (<c>docs/EXPLORER_PLAN.md</c>): whether she has shown it to the player, the wonders the car has seen and
/// those written into her map (0 to <see cref="Buildings.ExplorerPlans.Landmarks"/> - 1, in the order it happened), and
/// the wonder her road arrows lead to, or -1. A save from before the map has seen nothing.
/// </summary>
public sealed class ExplorerSave
{
    public bool Started { get; set; }
    public int[] Spotted { get; set; } = System.Array.Empty<int>();
    public int[] Recorded { get; set; } = System.Array.Empty<int>();
    public int Guided { get; set; } = -1;
}

/// <summary>
/// The shells of the beach (<c>docs/SHELLS_PLAN.md</c>): the number of the tide whose shells lie on the beach now (each
/// high tide brings the next), the numbers of its shells already picked up, and how many shells of each kind the player
/// has. Where the shells of a tide lie follows from the valley and the tide's number. A save from before the shells has
/// picked none.
/// </summary>
public sealed class ShellsSave
{
    public int Tide { get; set; }
    public int[] Picked { get; set; } = System.Array.Empty<int>();
    public int[] Kinds { get; set; } = new int[Buildings.ShellPlans.Kinds];
}

/// <summary>
/// The ferrywoman's rally: the best time of each of its stages (in the order they are driven round the ring) in tenths
/// of a second, 0 for a stage not yet driven; the medals follow from them. A drive under way is not saved; a save from
/// before the rally has driven no stage.
/// </summary>
public sealed class RallySave
{
    public int[] Best { get; set; } = new int[Buildings.RallyPlans.Stages];
}

/// <summary>
/// The shepherd's lost sheep: whether he has sent the player out after them, the numbers of the sheep at home in his
/// fold and of those found that walk behind the car (0 to <see cref="Buildings.SheepPlans.Sheep"/> - 1, in the order
/// they were found). Where a found sheep is, is not saved; a save from before the sheep has none of it.
/// </summary>
public sealed class SheepSave
{
    public bool Started { get; set; }
    public int[] Home { get; set; } = System.Array.Empty<int>();
    public int[] Following { get; set; } = System.Array.Empty<int>();
}

/// <summary>
/// The forest keeper's book of tracks: whether she has sent the player out after the first trail, and how many of its
/// animals are found (0 to <see cref="Buildings.TrackingPlans.Animals"/>), which is also the trail followed now. How far
/// along it the car is, is not saved; a save from before the trails has none of it.
/// </summary>
public sealed class TrackingSave
{
    public bool Started { get; set; }
    public int Found { get; set; }
}

/// <summary>
/// The letters handed to each keeper of a border (by <c>Border</c>; the stamps of the album follow from them), the keeper
/// the letter being carried is for (−1 for none) and the house of the village the reply being carried is for (−1 for
/// none). A save made before the post has handed over nothing and carries nothing.
/// </summary>
public sealed class PostSave
{
    public int[] Delivered { get; set; } = new int[Tasks.Post.Keepers];
    public int Carrying { get; set; } = -1;
    public int Reply { get; set; } = -1;
}

/// <summary>The deliveries made to the lighthouse, 0 to <see cref="Buildings.LighthousePlans.Trips"/>.</summary>
public sealed class LighthouseSave
{
    public int Stage { get; set; }
}

/// <summary>
/// The dragon under the mountains: the deliveries made to his nest (0 to <see cref="Buildings.DragonPlans.Trips"/>), the
/// answers of his riddle (0 to <see cref="Buildings.DragonPlans.Riddles"/>, only once the nest is finished; the egg has
/// hatched after the last) and whether the player has flown on his back. A save from before the dragon has none of it.
/// </summary>
public sealed class DragonSave
{
    public int Stage { get; set; }
    public int Riddle { get; set; }
    public bool Flown { get; set; }
}

/// <summary>
/// The balloon station: the flights the player has taken off for (which also picks the heights of the next flight's
/// stars) and the stars caught on them in all, at most <see cref="Valley.BalloonFlight.Stars"/> a flight. Where a flight
/// is is not saved; a save from before the station has none of it.
/// </summary>
public sealed class BalloonSave
{
    public int Flights { get; set; }
    public int Stars { get; set; }
}

/// <summary>
/// The animals brought to the hunter's pen, by kind (<see cref="AnimalHunt.Prey"/>, saved by name), whether he has sent the
/// player out with a net, and the animal caught and loaded on the car ("" for none). Where the net and the free animals are
/// is not saved: after a load the net is on the car and the animals run in a new meadow.
/// </summary>
public sealed class HuntSave
{
    public string[] Penned { get; set; } = Array.Empty<string>();
    public bool Active { get; set; }
    public string Carrying { get; set; } = "";

    /// <summary>The deliveries the hunter's hut has had, 0 to <see cref="HutPlans.Trips"/>; he sends no one hunting before.</summary>
    public int Hut { get; set; }
}

/// <summary>
/// The clock of the day: the seconds since the morning, 0 to <see cref="Weather.DayLength"/>. A save made before it existed
/// begins in the morning. The weather of the sky is not saved; it starts clear.
/// </summary>
public sealed class DaySave
{
    public float Clock { get; set; }
}

/// <summary>
/// The fireflies caught in the lantern so far. The stars of the collection that shine (one for every
/// <see cref="FireflyHunt.PerStar"/>) and the brightness of the lantern follow from this number, and a hunt lasts as long as
/// the night that was asked for, so nothing else is saved.
/// </summary>
public sealed class FirefliesSave
{
    public int Caught { get; set; }
}

/// <summary>
/// The treasures dug up so far and whether one is being looked for. Where the next one lies follows from the valley's seed and
/// <see cref="Found"/>, so nothing else is saved; a hunt is over once its chest is open, and a new one begins at the captain.
/// </summary>
public sealed class TreasureSave
{
    public int Found { get; set; }
    public bool Active { get; set; }
}

/// <summary>The deliveries made to the zoo at the edge of the village, 0 to <see cref="ZooPlans.Trips"/>.</summary>
public sealed class ZooSave
{
    public int Stage { get; set; }
}

/// <summary>The deliveries made to the railway round the playground, 0 to <see cref="RailwayPlans.Trips"/>.</summary>
public sealed class RailwaySave
{
    public int Stage { get; set; }
}

/// <summary>
/// The lamps from the glass lamp maker: the kinds in the bag (<see cref="GlassLamps"/>; an id is never renamed) and
/// the lamps that stand on the ground. A lamp's height is not saved, the ground gives it again.
/// </summary>
public sealed class LampsSave
{
    public string[] Carried { get; set; } = Array.Empty<string>();
    public PlacedLampSave[] Placed { get; set; } = Array.Empty<PlacedLampSave>();
}

/// <summary>One lamp on the ground: its kind, where it stands on the map (x, z) and the way it faces.</summary>
public sealed class PlacedLampSave
{
    public string Kind { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Yaw { get; set; }
}

/// <summary>The decorations bought from the mayor, by id (<see cref="TownDecorations"/>); an id is never renamed.</summary>
public sealed class DecorationsSave
{
    public string[] Owned { get; set; } = Array.Empty<string>();
}

/// <summary>The deliveries made to the playground at the edge of the village, 0 to <see cref="PlaygroundPlans.Trips"/>.</summary>
public sealed class PlaygroundSave
{
    public int Stage { get; set; }
}

/// <summary>The ladybugs, hedgehogs and frogs found beside the roads, by their numbers (the order they were placed in).</summary>
public sealed class HiddenSave
{
    public int[] Found { get; set; } = Array.Empty<int>();
}

/// <summary>The car: where it stands and how it looks.</summary>
public sealed class VehicleSave
{
    public float[] Position { get; set; } = new float[3];
    public float Heading { get; set; }
    public string Look { get; set; } = "";
    public string Model { get; set; } = "classic";
    public string[] Accessories { get; set; } = Array.Empty<string>();

    /// <summary>The stamps the wheels leave (<see cref="CarTrail"/>); the plain tyre marks in a save from before trails.</summary>
    public string Trail { get; set; } = CarTrail.Plain.Id;
}

/// <summary>The coins and the garage items that were bought.</summary>
public sealed class EconomySave
{
    public int Coins { get; set; }

    /// <summary>The coins shown on the open reward panel; they are already counted in <see cref="Coins"/>.</summary>
    public int RewardCoins { get; set; }

    public CarReward[] Owned { get; set; } = Array.Empty<CarReward>();
}

/// <summary>The quiz settings, the question that is open and the trip that waits for it.</summary>
public sealed class QuizSave
{
    public QuizType Types { get; set; } = QuizType.All;

    /// <summary>
    /// The kinds of quiz the game knew when it wrote the save. A kind that came later is not in <see cref="Types"/>
    /// only because the player never saw it, so it is switched on when the save is loaded (<see cref="EnabledTypes"/>);
    /// one the player switched off stays off. A save without this field knew the first four kinds.
    /// </summary>
    public QuizType Offered { get; set; } = FirstTypes;

    internal const QuizType FirstTypes = QuizType.Math | QuizType.Letters | QuizType.Syllables | QuizType.Reading;

    /// <summary>The kinds of quiz that are on after loading: the saved ones and every kind the save did not know yet.</summary>
    internal QuizType EnabledTypes() => Types | (QuizType.All & ~Offered);

    public bool ThreeTermMath { get; set; }

    /// <summary>Position in the rotation of the kind of quiz that comes next.</summary>
    public int Next { get; set; }

    /// <summary>The open question is the word quiz that replaces cargo counting at a handover.</summary>
    public bool Handover { get; set; }

    /// <summary>The open question, if any.</summary>
    public IChallenge? Challenge { get; set; }

    public SyllableSelection Selection { get; set; }
    public int[] Rejected { get; set; } = Array.Empty<int>();

    /// <summary>The trip that starts when the open quiz is solved (<see cref="Game.Departure"/>), or 0.</summary>
    public int Departure { get; set; }

    /// <summary>The panel that offered the trip, for the kinds of trip that return to one; otherwise Title.</summary>
    public GameState DepartureState { get; set; }
}

/// <summary>The delivery or collection under way, or the trailer trip, whichever there is.</summary>
public sealed class TaskSave
{
    /// <summary>The character who sent the expedition (an actor ID), or -1 without one.</summary>
    public int Sender { get; set; } = -1;

    public bool Collection { get; set; }
    public bool[] Picked { get; set; } = Array.Empty<bool>();
    public bool Arrived { get; set; }
    public bool DeliveryReward { get; set; }

    /// <summary>
    /// The trailer trip under way, or null: what it is for (<see cref="TripKinds"/>), which one of its kind, and
    /// whether the load is aboard. Never set together with an expedition.
    /// </summary>
    public SavedTrip? Trip { get; set; }
}

/// <summary>Which roadside friends wait for a quiz and which have unlocked or finished their expedition.</summary>
public sealed class RoadsideSave
{
    public bool[] Animals { get; set; } = Array.Empty<bool>();
    public int[] Unlocked { get; set; } = Array.Empty<int>();
    public int[] Completed { get; set; } = Array.Empty<int>();
}

/// <summary>The village: the houses, the collections of the four collectors and the shops.</summary>
public sealed class VillageSave
{
    public int[] HouseLevels { get; set; } = new int[4];
    public int FarmCount { get; set; }
    public int GardenCount { get; set; }
    public int GemCount { get; set; }
    public int ToyCount { get; set; }
    public int[] TradeCounts { get; set; } = new int[3];

    /// <summary>Houses whose resident has been given glasses after the tenth improvement.</summary>
    public bool[] Glazed { get; set; } = new bool[4];

    /// <summary>Whether the resident of a finished house has been given bread from the bakery, house by house.</summary>
    public bool[] Baked { get; set; } = new bool[4];
}

/// <summary>The four buildings, the six building plots, the works and the trailer.</summary>
public sealed class BuildingsSave
{
    /// <summary>Deliveries made to the bridge, the mill, the tower and the jetty, 0–10 each.</summary>
    public int[] Stages { get; set; } = new int[BuildPlans.Count];

    /// <summary>Deliveries made to each of the six building plots, 0–6.</summary>
    public int[] PlotStages { get; set; } = new int[PlotPlans.Count];

    /// <summary>Goods stored at the sawmill, brickworks and gravel works, 0–12 each.</summary>
    public int[] WorksStock { get; set; } = new int[3];

    /// <summary>Glasses stored at the glassworks, 0–12. A save from before the glassworks has none.</summary>
    public int GlassStock { get; set; }

    /// <summary>Loaves stored at the bakery, 0–12. A save from before the bakery has none.</summary>
    public int BreadStock { get; set; }

    /// <summary>
    /// Sacks of flour standing at the finished mill, 0–12, ground from the farmer's wheat for the bakery. A save from
    /// before the mill ground flour has none.
    /// </summary>
    public int FlourStock { get; set; }

    public bool TrailerOwned { get; set; }
    public bool TrailerAttached { get; set; }
    public string TrailerPaint { get; set; } = "red";
}

/// <summary>
/// The loads that were hauled: each raw material that was tipped out (stones, soil, sand, logs) and each load a works
/// took in (sawmill, brickworks, gravel works, glassworks). A save from before the counts has zeros.
/// </summary>
public sealed class HaulsSave
{
    public int[] Raw { get; set; } = new int[4];
    public int[] Works { get; set; } = new int[4];

    /// <summary>
    /// Loads of wheat tipped out at the mill (at the bakery in a save from before the mill ground flour, and in a valley
    /// without the mill). A save from before the bakery has none.
    /// </summary>
    public int Wheat { get; set; }

    /// <summary>Loads of flour (or, before the mill, of wheat) the bakery has taken in. A save from before the bakery has none.</summary>
    public int Baked { get; set; }
}

/// <summary>The golem and the companions that follow the car.</summary>
public sealed class CompanionsSave
{
    public bool GolemOwned { get; set; }
    public bool GolemActive { get; set; }

    /// <summary>The companions that are owned but switched off, by kind.</summary>
    public string[] DisabledPets { get; set; } = Array.Empty<string>();
}

/// <summary>The postcards that were taken.</summary>
public sealed class PostcardsSave
{
    public PostcardProgress?[] Cards { get; set; } = new PostcardProgress?[Game.PostcardCount];

    /// <summary>The card whose question is open, or -1.</summary>
    public int Page { get; set; } = -1;
}

/// <summary>The driving courses and the ghost races.</summary>
public sealed class EventsSave
{
    /// <summary>The course of the run under way, or -1.</summary>
    public int Active { get; set; } = -1;

    /// <summary>The gate the run passes next.</summary>
    public int Next { get; set; }

    public int[] Wins { get; set; } = new int[2];

    /// <summary>Gate times of the player's record per course; empty while the pace ghost is still unbeaten.</summary>
    public float[][] Best { get; set; } = new[] { Array.Empty<float>(), Array.Empty<float>() };

    /// <summary>Seconds on the clock of the active run, which has one gate time in <see cref="Splits"/> per gate passed.</summary>
    public float Time { get; set; }

    public float[] Splits { get; set; } = Array.Empty<float>();
}

/// <summary>The forest bandits.</summary>
public sealed class BanditsSave
{
    public int Site { get; set; } = -1;
    public int Solved { get; set; }
    public float Cooldown { get; set; }
}

/// <summary>A trailer trip under way: the delivery it fetches is the next one of its target.</summary>
public sealed class SavedTrip
{
    /// <summary>One of <see cref="TripKinds"/>.</summary>
    public string Kind { get; set; } = "";

    /// <summary>The project, works, house or plot, by its index among those of its kind; 0 for a target that is alone.</summary>
    public int Index { get; set; }

    /// <summary>Whether the load is already on the trailer.</summary>
    public bool Loaded { get; set; }
}
