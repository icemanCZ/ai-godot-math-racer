using System;
using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Diagnostics;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.App;

public partial class Game
{
    partial void RunDiagnosticsBeforeWorld(ref bool handled)
    {
        if (LaunchOptions.Has("--list-checks"))
        {
            // build.ps1 reads the suites of the test run from here.
            handled = true;
            SetProcess(false);
            foreach (string suite in DiagnosticSuites.ListForTestRun())
                GD.Print("DIAGNOSTIC_SUITE " + suite);
            GetTree().Quit();
            return;
        }
        if (!LaunchOptions.Has("--road-audit"))
            return;
        handled = true;
        RunRoadAudit();
    }

    partial void RunDiagnosticsWithVehicle(ref bool handled)
    {
        if (!LaunchOptions.Has("--road-driving-checks"))
            return;
        handled = true;
        StartRoadDrivingChecks();
    }

    partial void DiagnosticsSkipTitle(ref bool skipTitle) => skipTitle = ContinueOnStartup();

    private void RunRoadAudit()
    {
        SetProcess(false);
        try
        {
            AddChild(world);
            world.GenerateVillage();
            WorldLayoutChecks.ValidatePlan(WorldLayout.Plan, Settings.Seed);
            RoadPassabilityChecks.Run(world);
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private void StartRoadDrivingChecks()
    {
        SetProcess(false);
        vehicle.Active = true;
        RoadPassabilityChecks.Run(world);
        AddChild(new RoadDrivingChecks(world, vehicle));
    }

    private static bool ContinueOnStartup()
    {
        var args = LaunchOptions.Args;
        return (args.Contains("--play") && !args.Contains("--view=title"))
            || DiagnosticSuites.All.Any(suite => suite.ContinuesGame && DiagnosticSuites.Requested(suite));
    }

    partial void AttachDiagnostics(FollowCamera camera)
    {
        var args = LaunchOptions.Args;
        // Checks expect exact coin totals, so the scattered coins only pay out where a check turns them on.
        if (
            args.Any(arg =>
                arg.EndsWith("-checks") || arg is "--smoke-test" or "--save-fixtures" or "--write-world-fingerprint"
            )
        )
            WorldCoins.PickupEnabled = false;
        foreach (var suite in DiagnosticSuites.All)
        {
            if (suite.Create == null || !DiagnosticSuites.Requested(suite))
                continue;
            if (suite.NeedsStart)
                Start();
            AddChild(suite.Create(this, world, vehicle));
        }
        if (args.Contains("--capture"))
        {
            string view = args.FirstOrDefault(arg => arg.StartsWith("--view="))?[7..] ?? "gameplay";
            PrepareCapture(view);
            AddChild(new VisualCapture(camera, world, vehicle, view));
        }
    }

    private void PrepareCapture(string view)
    {
        if (view.StartsWith("postcard-"))
        {
            int index = Math.Max(0, PostcardIndex(view));
            vehicle.SetPose(PostcardStop(index) + Vector3.Up * .25f, 0);
            Interact();
        }
        if (view.StartsWith("baker") || view.StartsWith("merchant") || view.StartsWith("confectioner"))
        {
            int trade =
                view.StartsWith("baker") ? 0
                : view.StartsWith("merchant") ? 1
                : 2;
            var owner = world.Village.Traders[trade];
            vehicle.SetPose(owner.Position + Vector3.Right * 3, 0);
            if (view.EndsWith("question") || view.EndsWith("delivery") || view.EndsWith("reward"))
                OpenChallenge(owner);
            if (view.EndsWith("delivery") || view.EndsWith("reward"))
                ChallengeChecks.Solve(this);
            if (view.EndsWith("reward"))
            {
                vehicle.SetPose(Delivery!.Recipient.Position + Vector3.Right * 2, 0);
                BeginDeliveryQuestion();
            }
        }
        if (view.StartsWith("museum") || view.StartsWith("toys"))
        {
            bool toys = view.StartsWith("toys");
            var owner = toys ? world.Village.Toymaker : world.Village.GemCollector;
            if (view.EndsWith("full"))
                world.Village.RestoreCollection(toys, Village.CollectionNames(toys).Length);
            vehicle.SetPose(owner.Position + Vector3.Right * 3, 0);
            if (view.EndsWith("question") || view.EndsWith("expedition"))
                OpenChallenge(owner);
            if (view.EndsWith("expedition"))
                ChallengeChecks.Solve(this);
        }
        if (view is "garden" or "garden-full" or "garden-question" or "garden-expedition")
        {
            if (view == "garden-full")
                world.Village.RestoreGarden(Village.FlowerNames.Length);
            vehicle.SetPose(world.Village.Gardener.Position + Vector3.Right * 3, 0);
            if (view is "garden-question" or "garden-expedition")
                OpenChallenge(world.Village.Gardener);
            if (view == "garden-expedition")
                ChallengeChecks.Solve(this);
        }
        if (view is "farm" or "farm-full" or "farm-question" or "farm-expedition")
        {
            if (view == "farm-full")
                world.Village.RestoreFarm(Village.FarmKinds.Length);
            vehicle.SetPose(world.Village.Farmer.Position + Vector3.Right * 3, 0);
            if (view is "farm-question" or "farm-expedition")
                OpenChallenge(world.Village.Farmer);
            if (view == "farm-expedition")
                ChallengeChecks.Solve(this);
        }
        if (view == "house-celebration")
        {
            var owner = world.Village.Residents[0];
            vehicle.SetPose(owner.Position + Vector3.Right * 3, 0);
            CompleteHouseTask(owner);
        }
        if (view is "garage" or "garage-shop")
        {
            vehicle.SetPose(world.Village.GarageEntrance, 0);
            // Enough coins for a paint or an accessory, but not a model, shows both price states.
            AddCoins(23);
            OpenGarage();
            if (view == "garage-shop" && FindChild("GarageScroll", true, false) is ScrollContainer scroll)
                scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, 290);
        }
        if (view == "coins")
        {
            // Park just short of the nearest coin so that it spins in front of the car.
            var coin = WorldCoins.ActivePositions.MinBy(p => p.DistanceSquaredTo(vehicle.Position));
            var toward = (coin - vehicle.Position) with { Y = 0 };
            var spot = coin - toward.Normalized() * 7;
            vehicle.SetPose(world.Ground.At(spot.X, spot.Z, .15f), Mathf.Atan2(-toward.X, -toward.Z));
            WorldCoins.PickupEnabled = false;
        }
        if (
            view
            is "village"
                or "village-upgraded"
                or "village-question"
                or "village-fruit-offer"
                or "village-fruit"
                or "village-material-offer"
                or "village-material-missing"
        )
        {
            if (view == "village-upgraded")
                world.Village.RestoreLevels(new[] { 10, 10, 10, 10 });
            if (view is "village-material-offer" or "village-material-missing")
            {
                // The first house asks for goods; the works has some in stock, or none so that the raw material is shown.
                world.Village.RestoreLevels(
                    new[] { world.Village.Tasks(0).ToList().FindIndex(k => Village.MaterialOf(k) != null), 0, 0, 0 }
                );
                foreach (var works in world.Industry)
                    works.SetStock(view == "village-material-offer" ? 6 : 0);
            }
            if (view is "village-fruit-offer" or "village-fruit")
                world.Village.RestoreLevels(
                    new[] { world.Village.Tasks(0).ToList().IndexOf(VillageTaskKind.Fruit), 0, 0, 0 }
                );
            vehicle.SetPose(world.Village.Residents[0].Position + world.Village.Houses[0].Basis.Z * 3, 0);
            if (
                view
                is "village-question"
                    or "village-fruit-offer"
                    or "village-fruit"
                    or "village-material-offer"
                    or "village-material-missing"
            )
                OpenChallenge(world.Village.Residents[0]);
            if (view == "village-fruit")
            {
                BeginVillageCollection();
                ChallengeChecks.Solve(this);
            }
        }
        if (view == "task-choice")
        {
            OpenChallenge(world.Animals[0]);
            ChallengeChecks.Solve(this);
        }
        void PrepareExpedition(bool collection, bool start = true)
        {
            var sender = world.Animals[collection ? 1 : 0];
            OpenChallenge(sender);
            ChallengeChecks.Solve(this);
            if (start)
                SelectExpedition(collection);
        }
        void CompleteExpedition()
        {
            if (Delivery!.Collection)
                foreach (var point in Delivery.PickupPositions)
                {
                    vehicle.SetPose(point + Vector3.Up * .15f, 0);
                    Delivery.Tick(.25);
                }
            vehicle.SetPose(Delivery.Recipient.Position + Vector3.Right * 2, 0);
            BeginDeliveryQuestion();
        }
        if (
            view
            is "collection-offer"
                or "collection"
                or "collection-question"
                or "collection-reward"
                or "delivery-offer"
                or "delivery"
                or "delivery-question"
                or "delivery-reward"
        )
        {
            PrepareExpedition(view.StartsWith("collection"), !view.EndsWith("offer"));
            if (view.EndsWith("question") || view.EndsWith("reward"))
                CompleteExpedition();
            if (view.EndsWith("reward"))
                ChallengeChecks.Solve(this);
        }
        if (view is "challenge" or "challenge-2" or "letters" or "syllables" or "reading" or "reward" or "accessories")
        {
            if (view is "reward" or "accessories")
            {
                PrepareExpedition(false);
                CompleteExpedition();
                ChallengeChecks.Solve(this);
            }
            else
            {
                if (view == "letters")
                    quiz.Enabled = QuizType.Letters;
                if (view == "syllables")
                    quiz.Enabled = QuizType.Syllables;
                if (view == "reading")
                    quiz.Enabled = QuizType.Reading;
                if (view == "challenge-2")
                    quiz.ThreeTermMath = true;
                OpenChallenge(world.Animals[view is "letters" or "syllables" or "reading" ? 1 : 0]);
            }
        }
        if (view.StartsWith("ford-") && int.TryParse(view["ford-".Length..], out int stage) && world.Ford is { } ford)
        {
            ford.SetStage(stage);
            var site = ford.Site;
            vehicle.SetPose(
                world.Ground.At(site.NearDump.X, site.Z, .15f),
                site.Side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2
            );
            trailerParade = true;
            trailer.Attach(true);
            trailer.SetLoad(stage < 3 ? HaulLoad.Gravel : HaulLoad.Bricks);
        }
        if (view == "trailer")
        {
            trailerParade = true;
            trailer.Attach(true);
            trailer.SetLoad(HaulLoad.Stones);
        }
        if (view == "bridge-roads")
            world.Ford?.SetStage(Ford.Trips);
        if (
            view is "sawmill" or "brickworks" or "gravel-works" or "works-full" or "works-panel"
            && world.Industry.Length > 0
        )
        {
            world.Ford?.SetStage(Ford.Trips);
            TrailerOwned = true;
            var works = world.Industry[
                view switch
                {
                    "brickworks" => 1,
                    "gravel-works" => 2,
                    _ => 0,
                }
            ];
            foreach (var other in world.Industry)
                other.SetStock(view == "works-full" ? Works.Capacity : 7);
            var ring = works.Site.Position;
            vehicle.SetPose(
                world.Ground.At(ring.X, ring.Z, .15f),
                Mathf.Atan2(-works.Site.Inward.X, -works.Site.Inward.Z)
            );
            trailerParade = true;
            trailer.Attach(true);
            trailer.SetLoad(works.Spec.Input);
            if (view == "works-panel")
            {
                vehicle.SetPose(works.Worker.Position + Vector3.Right * 3, 0);
                OpenWorks(works.Worker);
            }
        }
        if (ParseBuildView(view) is var (viewProject, viewStage) && viewProject < world.Projects.Length)
        {
            var project = world.Projects[viewProject];
            project.SetStage(viewStage);
            var drop = project.DropFor(0);
            vehicle.SetPose(world.Ground.At(drop.X, drop.Z, .15f), 0);
        }
        if (ParsePlotView(view) is var (plotIndex, plotStage) && plotIndex < world.Village.BuildPlots.Count)
        {
            var plot = world.Village.BuildPlots[plotIndex];
            plot.SetStage(plotStage);
            vehicle.SetPose(world.Ground.At(plot.Drop.X, plot.Drop.Z, .15f), 0);
        }
        if (view == "log-yard" && world.LogYard is { } logYard)
            vehicle.SetPose(world.Ground.At(logYard.Position.X, logYard.Position.Z, .15f), 0);
        if (view is "beaver" or "load-question" && world.Beaver != null)
        {
            vehicle.SetPose(world.Beaver.Position + Vector3.Right * 3, 0);
            OpenBuilder(world.Beaver);
            if (view == "load-question")
            {
                world.Industry[(int)Product.Gravel].SetStock(5);
                BeginBuildTrip();
                ChallengeChecks.Solve(this);
                quiz.Enabled = QuizType.Math;
                vehicle.SetPose(world.Ground.At(Haul!.Source.Position.X, Haul.Source.Position.Z, .15f), 0);
                BeginLoading();
            }
        }
        if (view == "decorated")
            foreach (var accessory in CarAccessory.All)
                vehicle.Appearance.ToggleAccessory(accessory.Id);
        if (CarBody.All.Any(body => body.Id == view))
        {
            vehicle.Appearance.ApplyModel(view);
            vehicle.Appearance.Apply(CarLook.All.First(look => look.Id == "ocean"));
            foreach (
                var id in view == "buggy" ? new[] { "surfboard", "lights", "bumper" }
                : view == "pickup" ? new[] { "spoiler", "exhaust", "antenna" }
                : new[] { "rack", "suitcase", "spare" }
            )
                vehicle.Appearance.ToggleAccessory(id);
        }
    }
}

public partial class Game
{
    /// <summary>A view such as <c>mill-5</c> or <c>tower-10</c>: the building and how many deliveries are shown.</summary>
    /// <summary>"plot-N" shows plot N with all its stages delivered, "plot-N-S" with S of them.</summary>
    internal static (int Plot, int Stage)? ParsePlotView(string view)
    {
        if (!view.StartsWith("plot-"))
            return null;
        var parts = view["plot-".Length..].Split('-');
        if (parts.Length is < 1 or > 2 || !int.TryParse(parts[0], out int plot) || plot is < 0 or >= PlotPlans.Count)
            return null;
        int stage = PlotPlans.Trips;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out stage) || stage is < 0 or > PlotPlans.Trips))
            return null;
        return (plot, stage);
    }

    internal static (int Project, int Stage)? ParseBuildView(string view)
    {
        foreach (
            var (prefix, project) in new[]
            {
                ("mill-", BuildPlans.Mill),
                ("tower-", BuildPlans.Tower),
                ("jetty-", BuildPlans.Jetty),
            }
        )
            if (
                view.StartsWith(prefix)
                && int.TryParse(view[prefix.Length..], out int stage)
                && stage is >= 0 and <= BuildPlans.Trips
            )
                return (project, stage);
        return null;
    }
}
