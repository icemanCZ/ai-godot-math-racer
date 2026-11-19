using System;
using System.Linq;
using System.Text.Json;
using Godot;
using MathRacer.App;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>One more run of a suite with an extra argument, such as one driving route of the road checks.</summary>
internal sealed record DiagnosticVariant(string Argument, string Suffix);

/// <summary>
/// A headless check that <c>./build.ps1 -Test</c> runs. This list is the only place that knows the suites: the game
/// starts them from it (<see cref="Game"/>.AttachDiagnostics) and <c>build.ps1</c> reads it back through
/// <c>--list-checks</c>, so a new suite is added here and nowhere else.
/// </summary>
/// <param name="Mode">The flag without the dashes, as in <c>--ford-checks</c>.</param>
/// <param name="Create">Makes the node that runs the suite; null for a suite that starts itself before the world exists.</param>
/// <param name="NeedsStart">Whether the game is switched to driving before the suite starts.</param>
/// <param name="ContinuesGame">Whether the game skips the title screen and continues at startup.</param>
/// <param name="Seeds">The world seeds the suite runs with in the test run; none means one run with the default seed.</param>
/// <param name="Variants">Extra runs with an argument each; none means one plain run per seed.</param>
/// <param name="FixedFps">Whether the run uses a fixed frame rate (the road audit and the smoke test do not).</param>
/// <param name="InSuite">False for tools that write files and are not part of the test run.</param>
internal sealed record DiagnosticSuite(
    string Mode,
    Func<Game, World, Vehicle, Node>? Create = null,
    bool NeedsStart = true,
    bool ContinuesGame = false,
    int[]? Seeds = null,
    DiagnosticVariant[]? Variants = null,
    bool FixedFps = true,
    bool InSuite = true
);

internal static class DiagnosticSuites
{
    private static readonly int[] Seed42 = { 42 };
    private static readonly int[] ThreeSeeds = { 1, 42, 7421 };

    private static DiagnosticVariant[] Routes(params string[] routes) =>
        routes
            .Select(route => new DiagnosticVariant(
                $"--road-driving-route={route}",
                $"route{route.Replace(',', '+').TrimEnd('-')}"
            ))
            .ToArray();

    /// <summary>
    /// In the order the test run starts them: roughly longest first, so that no long suite starts near the end.
    /// Road driving is the longest, so its routes are split over processes of similar length: the two long loops
    /// have one each, the short ones share one; "4-" also covers any route added later.
    /// </summary>
    internal static readonly DiagnosticSuite[] All =
    {
        new("road-driving-checks", Seeds: new[] { 1, 16 }, Variants: Routes("2", "3", "0,1,4-")),
        new("smoke-test", (g, w, v) => new SmokeTest(g, w, v), NeedsStart: false, ContinuesGame: true, FixedFps: false),
        new("new-game-checks", (g, w, v) => new NewGameChecks(g, w, v), NeedsStart: false, Seeds: Seed42),
        new(
            "track-checks",
            (g, w, v) => new WheelTrackChecks(v, w.Ground, w.Routes),
            NeedsStart: false,
            ContinuesGame: true,
            Seeds: ThreeSeeds
        ),
        new("bandit-checks", (g, w, v) => new BanditChecks(g, w, v), Seeds: Seed42),
        new("collision-checks", (g, w, v) => new CollisionChecks(v, w), NeedsStart: false, ContinuesGame: true),
        new("controller-checks", (g, w, v) => new ControllerChecks(g, w, v), NeedsStart: false),
        new("village-checks", (g, w, v) => new VillageChecks(g, w, v), Seeds: Seed42),
        new("golem-checks", (g, w, v) => new GolemChecks(g, w, v), Seeds: Seed42),
        new("ford-checks", (g, w, v) => new FordChecks(g, w, v), Seeds: ThreeSeeds),
        new("works-checks", (g, w, v) => new WorksChecks(g, w, v), Seeds: ThreeSeeds),
        new("bakery-checks", (g, w, v) => new BakeryChecks(g, w, v), Seeds: ThreeSeeds),
        new("hidden-checks", (g, w, v) => new HiddenChecks(g, w, v), Seeds: ThreeSeeds),
        new("build-checks", (g, w, v) => new BuildChecks(g, w, v), Seeds: ThreeSeeds),
        new("plot-checks", (g, w, v) => new PlotChecks(g, w, v), Seeds: ThreeSeeds),
        new("playground-checks", (g, w, v) => new PlaygroundChecks(g, w, v), Seeds: ThreeSeeds),
        new("railway-checks", (g, w, v) => new RailwayChecks(g, w, v), Seeds: ThreeSeeds),
        new("zoo-checks", (g, w, v) => new ZooChecks(g, w, v), Seeds: ThreeSeeds),
        new("treasure-checks", (g, w, v) => new TreasureChecks(g, w, v), Seeds: ThreeSeeds),
        new("mayor-checks", (g, w, v) => new MayorChecks(g, w, v), Seeds: ThreeSeeds),
        new("glass-checks", (g, w, v) => new GlassChecks(g, w, v), Seeds: ThreeSeeds),
        new(
            "weather-checks",
            (g, w, v) => new WeatherChecks(g, w, v),
            NeedsStart: false,
            ContinuesGame: true,
            Seeds: Seed42
        ),
        new("postcard-checks", (g, w, v) => new PostcardChecks(g, w, v), Seeds: Seed42),
        new("event-checks", (g, w, v) => new EventChecks(g, w, v), Seeds: ThreeSeeds),
        new("trade-checks", (g, w, v) => new TradeChecks(g, w, v), Seeds: ThreeSeeds),
        new("collection-checks", (g, w, v) => new CollectionChecks(g, w, v), Seeds: Seed42),
        new("collector-pet-checks", (g, w, v) => new CollectorPetChecks(g, w, v), Seeds: Seed42),
        new("world-pet-checks", (g, w, v) => new WorldPetChecks(g, w, v), Seeds: ThreeSeeds),
        new("garden-checks", (g, w, v) => new GardenChecks(g, w, v), Seeds: Seed42),
        new("farm-checks", (g, w, v) => new FarmChecks(g, w, v), Seeds: Seed42),
        new("save-checks", (g, w, v) => new SaveChecks(g, w, v), Seeds: Seed42),
        new("save-fixture-checks", (g, w, v) => new SaveFixtureChecks(g, w, v), Seeds: Seed42),
        new(
            "world-fingerprint-checks",
            (g, w, v) => new WorldFingerprintChecks(w),
            NeedsStart: false,
            Seeds: ThreeSeeds
        ),
        new("contract-checks", (g, w, v) => new ContractChecks(g, w), NeedsStart: false, Seeds: ThreeSeeds),
        new("garage-checks", (g, w, v) => new GarageChecks(g, w, v), Seeds: Seed42),
        new("delivery-checks", (g, w, v) => new DeliveryChecks(g, w, v), Seeds: ThreeSeeds),
        new("content-checks", (g, w, v) => new ContentChecks(w), NeedsStart: false, Seeds: new[] { 1, 14, 21, 24, 42 }),
        new("unit-checks", FixedFps: false),
        new("road-audit", Seeds: new[] { 1, 2, 42, 44, 53, 7421 }, FixedFps: false),
        // Tools that are not part of the test run: they write the files in Tests, or look at one save file.
        new("load-save-checks", (g, w, v) => new LoadSaveChecks(g, w, v), InSuite: false),
        new("save-fixtures", (g, w, v) => new SaveFixtures(g, w, v), InSuite: false),
        new("write-world-fingerprint", (g, w, v) => new WorldFingerprintWriter(w), NeedsStart: false, InSuite: false),
    };

    internal static bool Requested(DiagnosticSuite suite) => LaunchOptions.Has("--" + suite.Mode);

    /// <summary>
    /// The suites and the tools, one line of JSON each, which <c>build.ps1</c> and <c>test.ps1</c> read; a tool is
    /// marked by <c>inSuite: false</c>. One object per line, because Windows PowerShell 5.1 does not unroll a JSON array.
    /// </summary>
    internal static string[] ListForTestRun() =>
        All.Select(suite =>
                JsonSerializer.Serialize(
                    new
                    {
                        mode = suite.Mode,
                        seeds = suite.Seeds ?? Array.Empty<int>(),
                        variants = (suite.Variants ?? Array.Empty<DiagnosticVariant>())
                            .Select(variant => new { argument = variant.Argument, suffix = variant.Suffix })
                            .ToArray(),
                        fixedFps = suite.FixedFps,
                        inSuite = suite.InSuite,
                    }
                )
            )
            .ToArray();
}
