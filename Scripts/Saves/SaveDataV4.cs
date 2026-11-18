using System;
using System.Text.Json.Serialization;
using MathRacer.App;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Tasks;

namespace MathRacer.Saves;

/// <summary>
/// The save of versions 1 to 4: one flat class. It is only read, so that a save of an older game can be migrated step by
/// step (<see cref="SaveMigrations"/>) and then moved into the sections of <see cref="SaveData"/>. Nothing in the
/// game writes it, and it is never changed, because an old save must keep loading.
/// </summary>
public sealed class SaveDataV4
{
    /// <summary>The last version that had this flat shape.</summary>
    public const int LastVersion = 4;

    public PostcardProgress?[] Postcards { get; set; } = new PostcardProgress?[Game.PostcardCount];
    public int PostcardPage { get; set; } = -1;
    public int EventActive { get; set; } = -1;
    public int EventNext { get; set; }
    public int[] EventWins { get; set; } = new int[2];

    /// <summary>Gate times of the player's record per course; empty while the pace ghost is still unbeaten.</summary>
    public float[][] EventBest { get; set; } = new[] { Array.Empty<float>(), Array.Empty<float>() };

    /// <summary>Seconds on the clock of the active run, which has one gate time in <see cref="EventSplits"/> per gate passed.</summary>
    public float EventTime { get; set; }
    public float[] EventSplits { get; set; } = Array.Empty<float>();
    public bool GolemOwned { get; set; }
    public bool GolemActive { get; set; }

    // Before the buildings had ten deliveries each, only the bridge existed with six. These fields are read only
    // to migrate older saves into BuildStages.
    public int FordStage { get; set; }
    public int FordTrip { get; set; } = -1;
    public bool FordTripLoaded { get; set; }

    /// <summary>Deliveries made to the bridge, the mill, the tower and the jetty, 0–10 each; null in older saves.</summary>
    public int[]? BuildStages { get; set; }

    /// <summary>
    /// The trailer trip under way, or null: what it is for (<see cref="TripKinds"/>), which one of its kind, and
    /// whether the load is aboard. Never set together with an expedition.
    /// </summary>
    public SavedTrip? Trip { get; set; }

    // Before version 4 each kind of trip had a pair of fields. They are read only to migrate older saves into Trip
    // (MigrateTrips), which empties them, so a save of the current version never writes them.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? BuildTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? BuildTripLoaded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SupplyTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? SupplyTripLoaded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? HouseTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? HouseTripLoaded { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PlotTrip { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PlotTripLoaded { get; set; }

    /// <summary>Goods stored at the sawmill, brickworks and gravel works (sawmill, brickworks, gravel works order), 0–12 each.</summary>
    public int[] WorksStock { get; set; } = new int[3];

    /// <summary>Deliveries made to each of the six building plots, 0–6; zeros in saves from before the plots.</summary>
    public int[] PlotStages { get; set; } = new int[PlotPlans.Count];

    public bool TrailerOwned { get; set; }
    public bool TrailerAttached { get; set; }
    public string TrailerPaint { get; set; } = "red";
    public string[] DisabledCollectorPets { get; set; } = Array.Empty<string>();
    public int Version { get; set; } = LastVersion;
    public QuizType QuizTypes { get; set; } = QuizType.All;
    public bool ThreeTermMath { get; set; }
    public int NextQuiz { get; set; }
    public bool HandoverQuiz { get; set; }

    /// <summary>
    /// The trip that starts when the open quiz is solved (<see cref="Game.Departure"/>), or 0. Saves from before
    /// this field have none, so a quiz asked before a trip lost its trip when the game was closed.
    /// </summary>
    public int Departure { get; set; }

    /// <summary>The panel that offered the trip, for the kinds of trip that return to one; otherwise Title.</summary>
    public GameState DepartureState { get; set; }
    public int BanditSite { get; set; } = -1;
    public int BanditSolved { get; set; }
    public float BanditCooldown { get; set; }
    public int Seed { get; set; }
    public float[] Position { get; set; } = new float[3];
    public float Heading { get; set; }
    public GameState State { get; set; }
    public int Nearby { get; set; } = -1;
    public bool[] Animals { get; set; } = Array.Empty<bool>();
    public int[] HouseLevels { get; set; } = new int[4];
    public int FarmCount { get; set; }
    public int GardenCount { get; set; }
    public int GemCount { get; set; }
    public int[] TradeCounts { get; set; } = new int[3];
    public int ToyCount { get; set; }
    public int[] Unlocked { get; set; } = Array.Empty<int>();
    public int[] Completed { get; set; } = Array.Empty<int>();
    public CarReward[] Owned { get; set; } = Array.Empty<CarReward>();
    public int Coins { get; set; }

    /// <summary>The coins shown on the open reward panel; they are already counted in <see cref="Coins"/>.</summary>
    public int RewardCoins { get; set; }
    public string Look { get; set; } = "";
    public string Model { get; set; } = "classic";
    public string[] Accessories { get; set; } = Array.Empty<string>();
    public int Sender { get; set; } = -1;
    public bool Collection { get; set; }
    public bool[] Picked { get; set; } = Array.Empty<bool>();
    public bool Arrived { get; set; }
    public bool DeliveryReward { get; set; }

    /// <summary>The open question, if any.</summary>
    public IChallenge? Challenge { get; set; }
    public SyllableSelection SyllableSelection { get; set; }
    public int[] Rejected { get; set; } = Array.Empty<int>();

    // Version 1 fields, read only to migrate older saves into Challenge.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MathChallenge? Math { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public LetterChallenge? Letters { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SyllableChallenge? Syllables { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ReadingChallenge? Reading { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeliveryChallenge? DeliveryQuestion { get; set; }
}
