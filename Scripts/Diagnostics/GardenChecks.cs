using System;
using System.Linq;
using Godot;

namespace MathRacer;

public partial class GardenChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;

    public GardenChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("garden-checks/progress.json");
            void RoundTrip()
            {
                var expected = game.CaptureProgress();
                Check(SaveStore.Write(expected, path), "garden progress saved");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, "garden progress passes validation");
                game.RestoreProgress(loaded!);
                Check(
                    System.Text.Json.JsonSerializer.Serialize(expected)
                        == System.Text.Json.JsonSerializer.Serialize(game.CaptureProgress()),
                    "garden quiz, cargo and flower beds restore exactly"
                );
            }
            var village = world.Village;
            foreach (
                var bed in village.GetChildren().OfType<Node3D>().Where(n => n.Name.ToString().StartsWith("FlowerBed"))
            )
            foreach (float x in new[] { -1.5f, 0, 1.5f })
            foreach (float z in new[] { -1.5f, 0, 1.5f })
                Check(!world.Routes.Contains(bed.ToGlobal(new(x, 0, z))), "planted flower beds stay off every road");
            foreach (var spot in world.SpecialPickupSpots)
            {
                Check(
                    !world.Routes.Contains(spot) && world.Routes.Distance(spot) >= 7.5f,
                    "animal and flower pickup spots are beyond road surfaces"
                );
                Check(
                    Mathf.Abs(spot.Y - world.Ground.Height(spot.X, spot.Z)) < .01f
                        && !world.Plants.NearTrunk(spot, 2.8f),
                    "pickup is grounded and its approach is clear of trees"
                );
            }
            Check(
                world.PickupApproaches.Any(pair => pair.Road.DistanceTo(pair.Pickup) > 46),
                "some terrain targets now use longer approaches"
            );
            Check(
                village.Houses.Count == 4 && village.Residents.Count == 4 && village.Gardener.IsGardener,
                "garden adds a dedicated resident without replacing houses"
            );
            Check(
                world.Animals.All(a => !village.IsNearVillage(a.Position)),
                "task animals leave garden and village clear"
            );
            // Accepting the gardener's task switches off an active expedition; the quiz alone does not.
            game.OpenChallenge(world.Animals[0]);
            ChallengeChecks.Solve(game);
            game.SelectExpedition(false);
            var existing = game.Delivery;
            game.OpenChallenge(village.Gardener);
            Check(
                game.State == GameState.Challenge && game.Delivery == existing,
                "the gardener's quiz keeps the active expedition until the task is accepted"
            );
            ChallengeChecks.Solve(game);
            Check(
                game.State == GameState.Exploring && game.Delivery != existing && game.Delivery?.Garden == true,
                "accepting the gardener's task replaces the active expedition"
            );
            RoundTrip();
            game.OpenChallenge(world.Animals[0]);
            game.SelectExpedition(false);
            Check(
                game.Delivery?.Sender == world.Animals[0] && game.Delivery.Garden == false,
                "a roadside order replaces the garden task"
            );
            game.FinishInteraction();
            car.SetPose(game.Delivery!.Recipient.Position + Vector3.Right * 2, 0);
            game.BeginDeliveryQuestion();
            ChallengeChecks.Solve(game);
            game.FinishInteraction();
            int visits = game.RemainingAnimals,
                deliveries = game.RemainingDeliveries;
            for (int i = 0; i < Village.FlowerNames.Length; i++)
            {
                car.SetPose(village.Gardener.Position + Vector3.Right * 2, 0);
                game.OpenChallenge(village.Gardener);
                Check(
                    game.State == GameState.Challenge && game.CurrentChallenge != null,
                    "each departure requires a quiz"
                );
                game.SubmitAnswer(
                    game.CurrentChallenge!.Choices.First(choice => !game.CurrentChallenge.Accepts(choice))
                );
                Check(
                    game.Delivery == null && village.GardenCount == i,
                    "wrong answer cannot start a trip or award an flower"
                );
                RoundTrip();
                game.FinishInteraction();
                game.OpenChallenge(village.Gardener);
                Check(
                    game.Delivery == null && game.CurrentChallenge != null,
                    "postponing the quiz does not unlock departure"
                );
                ChallengeChecks.Solve(game);
                Check(
                    game.State == GameState.Exploring && game.Delivery is { Garden: true, Collected: 0, Amount: 1 },
                    "correct answer starts one flower expedition"
                );
                RoundTrip();
                game.OpenChallenge(village.Gardener);
                Check(
                    game.State == GameState.GardenStatus && village.GardenCount == i,
                    "empty return cannot add an flower"
                );
                game.FinishInteraction();
                var destination = game.Delivery!.PickupPositions[0];
                var closestRoad = world.Routes.Samples.OrderBy(p => p.DistanceSquaredTo(destination)).First();
                car.SetPose(closestRoad, 0);
                game.Delivery.Tick(.25);
                Check(game.Delivery.Collected == 0, "passing on the road does not collect an off-road flower");
                var approach = world.PickupApproaches.Single(pair =>
                    Mathf.IsEqualApprox(pair.Pickup.X, destination.X)
                    && Mathf.IsEqualApprox(pair.Pickup.Z, destination.Z)
                );
                var exitArrow = game.Delivery.GetNode<MeshInstance3D>("DestinationArrow");
                car.SetPose(approach.Road, 0);
                game.Delivery.Tick(.25);
                Check(
                    exitArrow.Visible
                        && Mathf.IsEqualApprox(exitArrow.Position.X, approach.Road.X)
                        && Mathf.IsEqualApprox(exitArrow.Position.Z, approach.Road.Z),
                    "final arrow marks the start of the clear approach"
                );
                var farOffRoad = world.SpecialPickupSpots.MaxBy(p => p.DistanceSquaredTo(destination));
                car.SetPose(farOffRoad, 0);
                game.Delivery.Tick(.01);
                Check(!exitArrow.Visible, "off-road arrow stays hidden far from the target");
                game.Delivery.Tick(.25);
                var roadArrows = game
                    .Delivery.GetChildren()
                    .OfType<MeshInstance3D>()
                    .Where(a => a != exitArrow && a.Name.ToString().StartsWith("RouteArrow"))
                    .ToArray();
                bool roadArrowsVisible = false;
                for (int turn = 0; turn < 8 && !roadArrowsVisible; turn++)
                {
                    car.SetPose(farOffRoad, turn * Mathf.Tau / 8);
                    game.Delivery.Tick(.01);
                    roadArrowsVisible = roadArrows.Any(a => a.Visible);
                }
                Check(roadArrowsVisible, "road arrows stay visible after leaving the road far from the target");
                var offRoad = approach.Road.Lerp(destination, .55f);
                car.SetPose(offRoad, Mathf.Pi);
                game.Delivery.Tick(.01);
                var toward = destination - car.Position;
                toward.Y = 0;
                Check(
                    exitArrow.Visible
                        && exitArrow.Position.IsEqualApprox(car.Position + Vector3.Up * 4.2f)
                        && (-exitArrow.Basis.Z).Dot(toward.Normalized()) > .999f
                        && game.Delivery.Waypoint == destination,
                    "off-road arrow follows the car and points at the current target regardless of heading"
                );
                game.OpenChallenge(village.Farmer);
                Check(
                    game.State == GameState.Challenge && game.Delivery.Garden,
                    "the farm quiz does not cancel the gardener expedition"
                );
                game.FinishInteraction();
                car.SetPose(game.Delivery!.PickupPositions[0], 0);
                game.Delivery.Tick(.25);
                game.Delivery.Tick(.25);
                Check(
                    game.Delivery.Collected == 1 && game.Delivery.CanDeliver && game.Delivery.CargoVisible,
                    "flower caught once and visible on car"
                );
                RoundTrip();
                car.Reset();
                Check(game.Delivery!.Collected == 1, "reset preserves caught flower");
                car.SetPose(village.Gardener.Position + Vector3.Right * 2, 0);
                game.BeginDeliveryQuestion();
                Check(
                    game.State == GameState.GardenStatus && village.GardenCount == i + 1 && game.Delivery == null,
                    "returning adds exactly one flower to flower beds and removes cargo"
                );
                game.BeginDeliveryQuestion();
                Check(village.GardenCount == i + 1, "handover cannot duplicate flowers");
                RoundTrip();
                game.FinishInteraction();
            }
            game.OpenChallenge(village.Gardener);
            Check(
                game.State == GameState.GardenStatus
                    && game.CurrentChallenge == null
                    && !village.Gardener.RewardAvailable,
                "full garden thanks player without extra trips"
            );
            Check(
                game.RemainingAnimals == visits && game.RemainingDeliveries == deliveries,
                "garden preserves original quest progress"
            );
            GD.Print("GARDEN_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
