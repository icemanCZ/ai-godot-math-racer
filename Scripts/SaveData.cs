using System;

namespace MathRacer;

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
}

/// <summary>The car: where it stands and how it looks.</summary>
public sealed class VehicleSave
{
    public float[] Position { get; set; } = new float[3];
    public float Heading { get; set; }
    public string Look { get; set; } = "";
    public string Model { get; set; } = "classic";
    public string[] Accessories { get; set; } = Array.Empty<string>();
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

    public bool TrailerOwned { get; set; }
    public bool TrailerAttached { get; set; }
    public string TrailerPaint { get; set; } = "red";
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

    /// <summary>The project, works, house or plot, by its index among those of its kind.</summary>
    public int Index { get; set; }

    /// <summary>Whether the load is already on the trailer.</summary>
    public bool Loaded { get; set; }
}
