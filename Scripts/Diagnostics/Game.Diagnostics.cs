using System;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class Game
{
    partial void RunDiagnosticsBeforeWorld(ref bool handled)
    {
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
            || args.Contains("--smoke-test")
            || args.Contains("--track-checks")
            || args.Contains("--collision-checks")
            || args.Contains("--weather-checks");
    }

    partial void AttachDiagnostics(FollowCamera camera)
    {
        var args = LaunchOptions.Args;
        if (args.Contains("--golem-checks"))
        {
            Start();
            AddChild(new GolemChecks(this, world, vehicle));
        }
        if (args.Contains("--weather-checks"))
            AddChild(new WeatherChecks(this, world, vehicle));
        if (args.Contains("--bandit-checks"))
        {
            Start();
            AddChild(new BanditChecks(this, world, vehicle));
        }
        if (args.Contains("--collision-checks"))
            AddChild(new CollisionChecks(vehicle, world));
        if (args.Contains("--track-checks"))
            AddChild(new WheelTrackChecks(vehicle, world.Ground, world.Routes));
        if (args.Contains("--smoke-test"))
            AddChild(new SmokeTest(this, world, vehicle));
        if (args.Contains("--controller-checks"))
            AddChild(new ControllerChecks(this, world, vehicle));
        if (args.Contains("--delivery-checks"))
        {
            Start();
            AddChild(new DeliveryChecks(this, world, vehicle));
        }
        if (args.Contains("--garage-checks"))
        {
            Start();
            AddChild(new GarageChecks(this, world, vehicle));
        }
        if (args.Contains("--village-checks"))
        {
            Start();
            AddChild(new VillageChecks(this, world, vehicle));
        }
        if (args.Contains("--farm-checks"))
        {
            Start();
            AddChild(new FarmChecks(this, world, vehicle));
        }
        if (args.Contains("--garden-checks"))
        {
            Start();
            AddChild(new GardenChecks(this, world, vehicle));
        }
        if (args.Contains("--trade-checks"))
        {
            Start();
            AddChild(new TradeChecks(this, world, vehicle));
        }
        if (args.Contains("--collection-checks"))
        {
            Start();
            AddChild(new CollectionChecks(this, world, vehicle));
        }
        if (args.Contains("--collector-pet-checks"))
        {
            Start();
            AddChild(new CollectorPetChecks(this, world, vehicle));
        }
        if (args.Contains("--save-checks"))
        {
            Start();
            AddChild(new SaveChecks(this, world, vehicle));
        }
        if (args.Contains("--postcard-checks"))
        {
            Start();
            AddChild(new PostcardChecks(this, world, vehicle));
        }
        if (args.Contains("--event-checks"))
        {
            Start();
            AddChild(new EventChecks(this, world, vehicle));
        }
        if (args.Contains("--new-game-checks"))
            AddChild(new NewGameChecks(this, world, vehicle));
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
        if (view == "garage")
        {
            vehicle.SetPose(world.Village.GarageEntrance, 0);
            OpenGarage();
        }
        if (view is "village" or "village-upgraded" or "village-question" or "village-fruit-offer" or "village-fruit")
        {
            if (view == "village-upgraded")
                world.Village.RestoreLevels(new[] { 10, 10, 10, 10 });
            if (view is "village-fruit-offer" or "village-fruit")
                world.Village.RestoreLevels(
                    new[] { world.Village.Tasks(0).ToList().IndexOf(VillageTaskKind.Fruit), 0, 0, 0 }
                );
            vehicle.SetPose(world.Village.Residents[0].Position + world.Village.Houses[0].Basis.Z * 3, 0);
            if (view is "village-question" or "village-fruit-offer" or "village-fruit")
                OpenChallenge(world.Village.Residents[0]);
            if (view == "village-fruit")
                BeginVillageCollection();
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
                    enabledQuizzes = QuizType.Letters;
                if (view == "syllables")
                    enabledQuizzes = QuizType.Syllables;
                if (view == "reading")
                    enabledQuizzes = QuizType.Reading;
                if (view == "challenge-2")
                    threeTermMath = true;
                OpenChallenge(world.Animals[view is "letters" or "syllables" or "reading" ? 1 : 0]);
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
