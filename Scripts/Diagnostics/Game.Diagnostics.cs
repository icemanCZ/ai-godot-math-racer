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
        if (LaunchOptions.Has("--unit-checks"))
        {
            // The checks that need no valley run before one is generated.
            handled = true;
            SetProcess(false);
            GetTree().Quit(UnitChecks.Run());
            return;
        }
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
        try
        {
            RoadPassabilityChecks.Run(world);
        }
        catch (Exception error)
        {
            // Without this the game would keep running and the case would wait for the runner's time limit.
            GD.PushError(error.ToString());
            GetTree().Quit(1);
            return;
        }
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
        // A benchmark or a picture shows the valley as it is once the meshes built after it stand.
        if (args.Contains("--benchmark") || args.Contains("--capture"))
            world.FarParts.Finish();
        if (args.Contains("--benchmark"))
        {
            Start();
            AddChild(new FrameBenchmark(this, world, vehicle));
        }
        if (args.Contains("--capture"))
        {
            string view = args.FirstOrDefault(arg => arg.StartsWith("--view="))?[7..] ?? "gameplay";
            PrepareCapture(view);
            // The picture shows the valley as it looks once every part has its visibility range.
            DistantDetails.Complete();
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
        if (view is "garden" or "garden-full" or "garden-question" or "garden-expedition" or "garden-status")
        {
            if (view == "garden-full")
                world.Village.RestoreGarden(Village.FlowerNames.Length);
            vehicle.SetPose(world.Village.Gardener.Position + Vector3.Right * 3, 0);
            if (view is "garden-question" or "garden-expedition" or "garden-status")
                OpenChallenge(world.Village.Gardener);
            if (view is "garden-expedition" or "garden-status")
                ChallengeChecks.Solve(this);
            if (view == "garden-status")
                OpenChallenge(world.Village.Gardener);
        }
        if (view is "farm" or "farm-full" or "farm-question" or "farm-expedition" or "farm-status")
        {
            if (view == "farm-full")
                world.Village.RestoreFarm(Village.FarmKinds.Length);
            vehicle.SetPose(world.Village.Farmer.Position + Vector3.Right * 3, 0);
            if (view is "farm-question" or "farm-expedition" or "farm-status")
                OpenChallenge(world.Village.Farmer);
            if (view is "farm-expedition" or "farm-status")
                ChallengeChecks.Solve(this);
            if (view == "farm-status")
                OpenChallenge(world.Village.Farmer);
        }
        if (view == "house-celebration")
        {
            var owner = world.Village.Residents[0];
            vehicle.SetPose(owner.Position + Vector3.Right * 3, 0);
            CompleteHouseTask(owner);
        }
        if (view is "garage" or "garage-shop" or "garage-trails")
        {
            vehicle.SetPose(world.Village.GarageEntrance, 0);
            // Enough coins for a paint or an accessory, but not a model, shows both price states.
            AddCoins(23);
            OpenGarage();
            if (view == "garage-shop" && FindChild("GarageScroll", true, false) is ScrollContainer scroll)
                scroll.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, 290);
            if (view == "garage-trails" && FindChild("GarageScroll", true, false) is ScrollContainer bottom)
                bottom.SetDeferred(ScrollContainer.PropertyName.ScrollVertical, 9999);
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
                or "village-evening"
                or "village-night"
                or "village-question"
                or "village-fruit-offer"
                or "village-fruit"
                or "village-material-offer"
                or "village-material-missing"
        )
        {
            if (view == "village-upgraded")
                world.Village.RestoreLevels(new[] { 10, 10, 10, 10 });
            if (view is "village-evening" or "village-night")
            {
                // Finished houses whose residents were given glasses, in the evening or at night.
                world.Village.RestoreLevels(new[] { 10, 10, 10, 10 });
                world.Village.RestoreGlazed(new[] { true, true, true, false });
                Weather.Begin(view == "village-night" ? WeatherKind.Night : WeatherKind.Evening, instant: true);
            }
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
        if (view == "weather-choice")
        {
            vehicle.SetPose(world.Witch.Position + world.Witch.Basis.Z * 3 + Vector3.Up * .2f, 0);
            OpenWeatherChallenge();
            ChallengeChecks.Solve(this);
        }
        if (view is "night-drive" or "evening-drive" or "constellation")
            Weather.Begin(view == "evening-drive" ? WeatherKind.Evening : WeatherKind.Night, instant: true);
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
        if (
            view
            is "challenge"
                or "challenge-2"
                or "letters"
                or "syllables"
                or "reading"
                or "compare"
                or "miss"
                or "reward"
                or "accessories"
        )
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
                if (view == "compare")
                    quiz.Enabled = QuizType.Compare;
                if (view is "challenge-2" or "miss")
                    quiz.ThreeTermMath = true;
                OpenChallenge(world.Animals[view is "letters" or "syllables" or "reading" or "compare" ? 1 : 0]);
                if (view == "miss" && CurrentChallenge is { } question)
                    SubmitAnswer(question.Choices.First(choice => !question.Accepts(choice)));
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
        if (view is "lookout-day" or "lookout-night" && world.Projects.Length > BuildPlans.Tower)
        {
            // The finished tower with the car beside the stargazer; the capture starts the look itself.
            var tower = world.Projects[BuildPlans.Tower];
            tower.SetStage(BuildPlans.Trips);
            var builder = tower.Builder.Position;
            vehicle.SetPose(world.Ground.At(builder.X + 2, builder.Z, .15f), 0);
            if (view == "lookout-night")
                Weather.Begin(WeatherKind.Night, instant: true);
        }
        if (ParseBuildView(view) is var (viewProject, viewStage) && viewProject < world.Projects.Length)
        {
            var project = world.Projects[viewProject];
            project.SetStage(viewStage);
            // The finished mill shows its sacks of flour, ground for the bakery.
            if (project.Complete && project is Mill)
                world.FlourMill?.SetStock(8);
            var drop = project.DropFor(0);
            vehicle.SetPose(world.Ground.At(drop.X, drop.Z, .15f), 0);
        }
        if (ParsePlotView(view) is var (plotIndex, plotStage) && plotIndex < world.Village.BuildPlots.Count)
        {
            var plot = world.Village.BuildPlots[plotIndex];
            plot.SetStage(plotStage);
            vehicle.SetPose(world.Ground.At(plot.Drop.X, plot.Drop.Z, .15f), 0);
        }
        if (ParsePlaygroundView(view) is { } playgroundStage && world.Playground is { } yard)
        {
            yard.SetStage(playgroundStage);
            // A few seconds pass so that the swings are swinging and the child is on the slide.
            for (int i = 0; i < 35; i++)
                yard._Process(.1);
            vehicle.SetPose(world.Ground.At(yard.Drop.X, yard.Drop.Z, .15f), 0);
        }
        if (ParseRailwayView(view) is { } railwayStage && world.Railway is { } railway)
        {
            railway.SetStage(railwayStage);
            // The train has gone a little way round the loop, with its steam and whistle.
            for (int i = 0; i < 160; i++)
                railway._Process(.1);
            var parked = railway.Drop - railway.Site.Inward * (view is "railway-close" or "railway-pet" ? 30 : 6);
            vehicle.SetPose(world.Ground.At(parked.X, parked.Z, .15f), 0);
        }
        if (ParseZooView(view) is { } zooStage && world.Zoo is { } zoo)
        {
            zoo.SetStage(zooStage);
            // A few seconds pass so that the animals are at their business and the visitors are on the path.
            for (int i = 0; i < 90; i++)
                zoo._Process(.1);
            var parked = zoo.Drop - zoo.Site.Inward * (view is "zoo-close" or "zoo-pet" ? 22 : 6);
            vehicle.SetPose(world.Ground.At(parked.X, parked.Z, .15f), 0);
        }
        if (view.StartsWith("hunter-") && world.Pen is { } pen && world.Hunter is { } hunter)
        {
            // The hut with S of its four deliveries, the finished pen full of animals, the hunter close up or his panel.
            var site = pen.Site;
            pen.SetStage(ParseHunterView(view) ?? (view == "hunter-panel" ? 1 : HutPlans.Trips));
            if (view is "hunter-pen" or "hunter-close")
            {
                Hunt.Restore(new[] { "Rabbit", "Fox", "Deer", "Rabbit", "Deer", "Fox", "Rabbit", "Deer" }, false, "");
                pen.SetAnimals(Hunt.Penned);
                for (int i = 0; i < 40; i++)
                    pen.Tick(.1f, site.Gate, true);
            }
            var inward = site.Inward;
            vehicle.SetPose(
                world.Ground.At(site.Drop.X, site.Drop.Z, .15f),
                Mathf.Atan2(-inward.X, -inward.Z) + (view == "hunter-panel" ? Mathf.Pi : 0)
            );
            if (view == "hunter-panel")
            {
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                OpenHunter(hunter);
            }
        }
        if (view is "hunt" or "hunt-net" or "hunt-carry" && world.Pen is { } huntPen && world.Hunter is { } huntHunter)
        {
            // A hunt under way: the animals ahead of the car, the net come down on one of them, or one on the car's roof.
            huntPen.SetStage(HutPlans.Trips);
            Hunt.Restore(new[] { "Rabbit", "Fox" }, true, view == "hunt-carry" ? "Fox" : "");
            huntPen.SetAnimals(Hunt.Penned);
            var ahead = new Vector3(1, 0, .3f).Normalized();
            var side = ahead.Cross(Vector3.Up);
            float heading = Mathf.Atan2(-ahead.X, -ahead.Z);
            if (view == "hunt-carry")
            {
                HuntGrounds.Clear();
                HuntGrounds.Carry(WildKind.Fox, false);
                var road = huntPen.Site.Road;
                var gate = huntPen.Site.Gate;
                var towards = gate - road;
                towards.Y = 0;
                towards = towards.Normalized();
                var from = road - towards * 6;
                vehicle.SetPose(world.Ground.At(from.X, from.Z, .15f), Mathf.Atan2(-towards.X, -towards.Z));
            }
            else
            {
                HuntGrounds.Begin(huntHunter.Position, Hunt.Caught);
                var target = HuntGrounds.PositionOf(0);
                HuntGrounds.Place(0, world.Ground.At(target.X, target.Z));
                if (view == "hunt")
                {
                    var second = target + side * 4 + ahead * 3;
                    var third = target - side * 5 + ahead * 6;
                    HuntGrounds.Place(1, world.Ground.At(second.X, second.Z));
                    HuntGrounds.Place(2, world.Ground.At(third.X, third.Z));
                    var from = target - ahead * 15;
                    vehicle.SetPose(world.Ground.At(from.X, from.Z, .15f), heading);
                }
                else
                {
                    for (int i = 1; i < HuntGrounds.HerdCount; i++)
                    {
                        var aside = target + side * (14 + i * 4) + ahead * 10;
                        HuntGrounds.Place(i, world.Ground.At(aside.X, aside.Z));
                    }
                    var from = target - ahead * 14;
                    vehicle.SetPose(world.Ground.At(from.X, from.Z, .15f), heading);
                    HuntGrounds.Throw();
                    for (int i = 0; i < 10; i++)
                        HuntGrounds.Tick(.1f, true);
                    // The car has driven up to its catch.
                    var caught = HuntGrounds.NetSpot ?? target;
                    var near = caught - ahead * 6 + side * 2;
                    vehicle.SetPose(world.Ground.At(near.X, near.Z, .15f), heading);
                }
            }
        }
        if (view is "mayor" or "mayor-shop" or "mayor-town" or "mayor-house" or "mayor-close")
        {
            world.Village.RestoreDecorations(Settlement.TownDecorations.All.Select(d => d.Id));
            var mayor = world.Village.Mayor;
            vehicle.SetPose(world.Ground.At(mayor.Position.X + 3, mayor.Position.Z + 4, .15f), 0);
            if (view == "mayor-close")
                vehicle.SetPose(world.Ground.At(mayor.Position.X, mayor.Position.Z + 40, .15f), 0);
            if (view == "mayor-shop")
            {
                AddCoins(40);
                world.Village.RestoreDecorations(new[] { "arch", "tree" });
                OpenMayor(mayor);
            }
        }
        if (view is "treasure-captain" or "treasure-hunt" or "treasure-chest")
        {
            // The pirate captain on the square, a treasure with its red cross in sight, or its open chest.
            var captain = world.Village.Treasurer;
            vehicle.SetPose(world.Ground.At(captain.Position.X + 3, captain.Position.Z + 5, .15f), 0);
            if (view != "treasure-captain")
            {
                treasure.Restore(0, true);
                BuryTreasure();
                var near = treasureSpot + new Vector3(9, 0, 6);
                vehicle.SetPose(world.Ground.At(near.X, near.Z, .15f), 0);
                if (view == "treasure-chest")
                    world.Treasure.OpenChest();
                for (int i = 0; i < 30; i++)
                    world.Treasure.Tick(.1f, vehicle.Position, true);
            }
        }
        if (view is "fireflies" or "fireflies-sky" or "fireflies-witch")
        {
            // A starry night, three stars and a few fireflies of the collection: the lantern and the fireflies beside the car,
            // the first picture of the sky, or the witch's panel.
            Weather.Begin(WeatherKind.Night, instant: true);
            var open = world.TreasureSpot(0);
            vehicle.SetPose(world.Ground.At(open.X, open.Z, .15f), 0);
            fireflies.Restore(FireflyHunt.PerStar * 3 + 6);
            DrawCollection();
            if (view == "fireflies-witch")
            {
                var witch = world.Witch;
                vehicle.SetPose(world.Ground.At(witch.Position.X, witch.Position.Z + 3, .15f), 0);
                OpenFireflies();
            }
            else
            {
                fireflies.Start();
                for (int i = 0; i < 20; i++)
                    world.Sparks.Tick(.1f, true, fireflies.Lantern, true);
                var offsets = new Vector2[]
                {
                    new(-5, -9),
                    new(4, -12),
                    new(8, -7),
                    new(-9, -16),
                    new(1, -19),
                    new(-2, -6),
                };
                for (int i = 0; i < offsets.Length; i++)
                    world.Sparks.Place(i, world.Ground.At(open.X + offsets[i].X, open.Z + offsets[i].Y));
                world.Sparks.Tick(.016f, true, fireflies.Lantern, true);
            }
        }
        if (view is "glass" or "glass-shop" or "glass-night" or "glass-close")
        {
            // A few lamps on the square, in the day or at night, and the lamp maker's shop.
            var maker = world.Village.Glassmaker;
            var square = world.Village.Square;
            var lamps = new (float X, float Z, string Kind)[]
            {
                (-2.4f, 4.4f, "candle"),
                (2.4f, 4.4f, "lamp"),
                (-5.2f, 1.2f, "lamp"),
                (5.4f, 1.4f, "candle"),
                (-1.5f, -4.4f, "candle"),
                (3.2f, -4.6f, "lamp"),
                (-8, 3.2f, "candle"),
                (8.2f, 3.4f, "candle"),
            };
            world.Village.Lamps.Restore(
                view == "glass-shop" ? new[] { "candle", "lamp" } : Array.Empty<string>(),
                lamps.Select(lamp =>
                {
                    var spot = square.Transform * new Vector3(lamp.X, 0, lamp.Z);
                    return new Settlement.LampSpot(lamp.Kind, spot.X, spot.Z, lamp.X);
                })
            );
            vehicle.SetPose(world.Ground.At(maker.Position.X - 3, maker.Position.Z + 5, .15f), 0);
            if (view == "glass-night")
                Weather.Begin(WeatherKind.Night, instant: true);
            if (view == "glass-shop")
                OpenGlassmaker(maker);
            RefreshLampHud();
        }
        if (view.StartsWith("balloon") && world.BalloonPlace is { } balloonSite && world.Balloonist is { } balloonist)
        {
            // The car on the road beside the station; at night, with the balloonist's panel or high in a flight.
            var road = balloonSite.Local(2, 3);
            vehicle.SetPose(world.Ground.At(road.X, road.Z, .15f), balloonSite.Yaw + Mathf.Pi);
            if (view is "balloon-night" or "balloon-far-night")
                Weather.Begin(WeatherKind.Night, instant: true);
            if (view == "balloon-panel")
            {
                nearby = balloonist;
                OpenBalloonist(balloonist);
            }
            if (view == "balloon-flight" && world.BalloonFlight is { } way)
            {
                TakeOff(balloonist);
                balloonRide!.Place(way.StarDistance(2) - 36, balloonRide.StarHeight(2) - 3);
            }
        }
        if (view.StartsWith("tracking") && world.Forester is { } forester && world.TrackingTrails.Count > 0)
        {
            // Beside the forest keeper with her book; on a trail, the car on its prints; at a den, with its animal.
            var trails = world.TrackingTrails;
            vehicle.SetPose(world.Ground.At(forester.Position.X + 4, forester.Position.Z + 4, .15f), 0);
            if (view is "tracking-night" or "tracking-dens-night")
                Weather.Begin(WeatherKind.Night, instant: true);
            if (view == "tracking-panel")
            {
                // Two animals found and the third followed.
                Tracking.Restore(true, 2, vehicle.Position);
                UpdateTracking();
                nearby = forester;
                OpenTracking(forester);
            }
            if (view is "tracking-trail" or "tracking-night" or "tracking-lost")
            {
                // On the second trail, a little way along it, facing on.
                Tracking.Restore(true, 1, Vector3.Zero);
                var trail = trails[1];
                int at = Mathf.Min(30, trail.Prints.Length - 1);
                var print = trail.Prints[at];
                var forward = print.Forward;
                var pose = print.Position - forward * 4;
                if (view == "tracking-lost")
                    pose += Vector3.Up.Cross(forward) * 40;
                vehicle.SetPose(
                    world.Ground.At(pose.X, pose.Z, .15f),
                    Mathf.Atan2(-forward.X, -forward.Z) + (view == "tracking-lost" ? 1.2f : 0)
                );
                Tracking.Step(vehicle.Position);
                UpdateTracking();
            }
            if (view is "tracking-den" or "tracking-dens" or "tracking-dens-night")
            {
                // Before the third den with its animal peeping out, or every animal found.
                Tracking.Restore(true, view == "tracking-den" ? 2 : trails.Count, Vector3.Zero);
                UpdateTracking();
                if (view == "tracking-den")
                {
                    // The car has just arrived, beside the den and out of the camera's way.
                    var den = trails[2];
                    var toward = new Vector3(Mathf.Sin(den.Yaw), 0, Mathf.Cos(den.Yaw));
                    var stand = den.Den + toward * 4 - Vector3.Up.Cross(toward) * 3.5f;
                    vehicle.SetPose(world.Ground.At(stand.X, stand.Z, .15f), Mathf.Atan2(toward.X, toward.Z));
                }
            }
        }
        if (
            view.StartsWith("rally")
            && world.Rally is { } course
            && world.Ferrywoman is { } ferrywoman
            && world.BorderHomes[(int)Border.River] is { } ferry
        )
        {
            // Her letter came; beside her with the card of two stages driven, or on the first stage: before its start
            // arch, or nearing its first gate with the question in the corner panel.
            var delivered = Post.Delivered.ToArray();
            delivered[(int)Border.River] = 1;
            Post.Restore(delivered, -1, -1);
            var first = course.Stages[0];
            void Pose(float along)
            {
                var forward = course.Forward(along);
                var p = course.At(along);
                vehicle.SetPose(world.Ground.At(p.X, p.Z, .15f), Mathf.Atan2(-forward.X, -forward.Z));
            }
            if (view == "rally-panel")
            {
                RestoreRally(
                    new[] { RallyPlans.Tenths(first.Length / 12), 0, RallyPlans.Tenths(course.Stages[2].Length / 9), 0 }
                );
                var gate = ferrywoman.Position - ferry.Inward * 4 + ferry.Right * 2;
                vehicle.SetPose(world.Ground.At(gate.X, gate.Z, .15f), ferry.Yaw + Mathf.Pi);
                nearby = ferrywoman;
                OpenRally(ferrywoman);
            }
            else
            {
                RestoreRally(new int[RallyPlans.Stages]);
                rallyRun = new RallyRun(course, 0);
                float end = view == "rally" ? first.Start - 16 : first.Gates[0] - 38;
                for (float d = first.Start - 24; d < end; d += 3)
                {
                    Pose(d);
                    TickRally(.22);
                }
                Pose(end);
                TickRally(.1);
            }
        }
        if (
            view.StartsWith("sheep")
            && world.Shepherd is { } shepherd
            && world.SheepFlock != null
            && world.BorderHomes[(int)Border.Mountains] is { } fold
        )
        {
            // Beside the shepherd with his fold; out on a road with five sheep behind the car; before a lost sheep on its
            // meadow; or the fold with seven sheep home.
            var gate = shepherd.Position - fold.Inward * 4 + fold.Right * 2;
            vehicle.SetPose(world.Ground.At(gate.X, gate.Z, .15f), fold.Yaw + Mathf.Pi);
            int[] none = System.Array.Empty<int>();
            Sheep.Restore(
                true,
                view switch
                {
                    "sheep-panel" => new[] { 0, 3, 6 },
                    "sheep-fold" => new[] { 0, 1, 2, 3, 4, 5, 6 },
                    _ => none,
                },
                view switch
                {
                    "sheep-panel" => new[] { 5 },
                    "sheep-herd" => new[] { 1, 2, 4, 5, 7 },
                    _ => none,
                }
            );
            UpdateSheep();
            if (view == "sheep-panel")
            {
                nearby = shepherd;
                OpenShepherd(shepherd);
            }
            if (view == "sheep-lost")
            {
                // On the way from the road, 14 m before the sheep, facing it: open ground, no tree in front of the camera.
                var spot = world.SheepSpots[0];
                var way = world.SheepWays[0];
                var stand = way[0];
                for (int i = way.Length - 1; i >= 0; i--)
                    if (new Vector2(way[i].X - spot.X, way[i].Z - spot.Z).Length() >= 14)
                    {
                        stand = way[i];
                        break;
                    }
                var toward = (spot - stand) with { Y = 0 };
                toward = toward.Normalized();
                vehicle.SetPose(world.Ground.At(stand.X, stand.Z, .15f), Mathf.Atan2(-toward.X, -toward.Z));
            }
            if (view == "sheep-herd")
            {
                // Along a road beyond the old valley, slowly, so that the sheep line up behind the car.
                var samples = world.OutlandRoutes.Samples;
                int start = samples.Count / 3;
                for (int i = start; i < start + 90 && i + 1 < samples.Count; i++)
                {
                    var forward = (samples[i + 1] - samples[i]) with { Y = 0 };
                    if (forward.LengthSquared() < .0001f || forward.LengthSquared() > 25)
                        continue;
                    var p = samples[i];
                    vehicle.SetPose(world.Ground.At(p.X, p.Z, .15f), Mathf.Atan2(-forward.X, -forward.Z));
                    TickSheep(.12);
                }
                for (int i = 0; i < 20; i++)
                    TickSheep(.1);
            }
        }
        if (view.StartsWith("explorer") && world.Cartographer is { } owl && world.CartographerPlace is { } owlSite)
        {
            // On the road beside the owl's stand; with her panel, the map has wonders hidden, seen and written in.
            var road = owlSite.Road - owlSite.Right * 6;
            vehicle.SetPose(world.Ground.At(road.X, road.Z, .15f), Mathf.Atan2(owlSite.Right.X, owlSite.Right.Z));
            if (view == "explorer-panel")
            {
                Explorer.Restore(true, new[] { 0, 2, 3, 5, 6 }, new[] { 0, 3, 6 }, 1);
                world.LandmarkModels?.SetRecorded(Explorer.RecordedSet);
                nearby = owl;
                OpenCartographer(owl);
            }
            if (view == "explorer-hud")
            {
                Explorer.Restore(
                    true,
                    System.Array.Empty<int>(),
                    System.Array.Empty<int>(),
                    NearestHiddenLandmark(road)
                );
                UpdateExplorerTrip();
                for (int i = 0; i < 5; i++)
                    TickExplorer(.1);
            }
        }
        if (view.StartsWith("landmark-") && int.TryParse(view["landmark-".Length..], out int wonder))
        {
            // On the road beside a wonder, once it is written into the map, so that the owl's flag shows.
            if (wonder < world.Landmarks.Count)
            {
                var landmark = world.Landmarks[wonder];
                vehicle.SetPose(world.Ground.At(landmark.Road.X, landmark.Road.Z, .15f), 0);
                world.LandmarkModels?.SetRecorded(new[] { wonder });
            }
        }
        if (view.StartsWith("shells") && world.BeachShells != null)
        {
            // On the beach at low tide (or at high tide), before the first shell of the tide, facing the sea.
            Weather.SetClock(view == "shells-high" ? 0 : Tide.Period / 2, true);
            TickShells(.1);
            var target = ShellsOnSand.Length > 0 ? ShellsOnSand[0].Position : world.ShellCove!.Value;
            var stand = target + new Vector3(0, 0, -14);
            vehicle.SetPose(world.Ground.At(stand.X, stand.Z, .15f), Mathf.Pi);
            for (int i = 0; i < 3; i++)
                TickShells(.1);
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
        if (ParseModelView(view) is { } modelView)
        {
            vehicle.Appearance.ApplyModel(modelView.Model);
            vehicle.Appearance.Apply(CarLook.All.First(look => look.Id == "ocean"));
            foreach (
                var id in modelView.Decorated ? CarAccessory.Catalogue.Select(a => a.Id).Where(id => id != "wheels")
                : modelView.Model == "buggy" ? new[] { "surfboard", "lights", "bumper" }
                : modelView.Model == "pickup" ? new[] { "spoiler", "exhaust", "antenna" }
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

    /// <summary>"railway-S" shows the railway with S of its six deliveries made, "railway-close" the finished station and "railway-pet" the station one delivery short, with the pet waiting.</summary>
    internal static int? ParseRailwayView(string view) =>
        view == "railway-close" ? RailwayPlans.Trips
        : view == "railway-pet" ? RailwayPlans.Trips - 1
        : view.StartsWith("railway-")
        && int.TryParse(view["railway-".Length..], out int stage)
        && stage is >= 0 and <= RailwayPlans.Trips
            ? stage
        : null;

    /// <summary>"zoo-S" shows the zoo with S of its six deliveries made, "zoo-close" the finished zoo from the gate and "zoo-pet" the zoo one delivery short, with the pet waiting.</summary>
    internal static int? ParseZooView(string view) =>
        view == "zoo-close" ? ZooPlans.Trips
        : view == "zoo-pet" ? ZooPlans.Trips - 1
        : view.StartsWith("zoo-")
        && int.TryParse(view["zoo-".Length..], out int stage)
        && stage is >= 0 and <= ZooPlans.Trips
            ? stage
        : null;

    /// <summary>"hunter-S" shows the hunter's hut with S of its four deliveries made.</summary>
    internal static int? ParseHunterView(string view) =>
        view.StartsWith("hunter-")
        && int.TryParse(view["hunter-".Length..], out int stage)
        && stage is >= 0 and <= HutPlans.Trips
            ? stage
            : null;

    /// <summary>"playground-S" shows the playground with S of its six deliveries made.</summary>
    internal static int? ParsePlaygroundView(string view) =>
        view.StartsWith("playground-")
        && int.TryParse(view["playground-".Length..], out int stage)
        && stage is >= 0 and <= PlaygroundPlans.Trips
            ? stage
            : null;

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

public partial class Game
{
    /// <summary>
    /// A view of one model, as <c>plane</c>, <c>plane-decorated</c> (every accessory on) or <c>plane-rear</c> (decorated,
    /// seen from behind); the model is the id of a <see cref="CarBody"/>.
    /// </summary>
    internal static (string Model, bool Decorated, bool Rear)? ParseModelView(string view)
    {
        bool rear = view.EndsWith("-rear"),
            decorated = rear || view.EndsWith("-decorated");
        string model =
            rear ? view[..^"-rear".Length]
            : decorated ? view[..^"-decorated".Length]
            : view;
        return CarBody.All.Any(body => body.Id == model) ? (model, decorated, rear) : null;
    }
}
