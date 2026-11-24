using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MathRacer.App;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The road guidance over both networks (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 0): every road of the old valley
/// and of the land round it can be reached from every other, a route from the village leads out to the end of each
/// road to a border with its arrows on the road, and a car that follows the guide from the ring with its own physics
/// gets there, as it does along every join from the old road it leaves out to the ring, gently graded and free of rocks. The ring meets the old roads at only two to four joins, so the far borders are 1.5 to 3.3 km away by road.
/// </summary>
public partial class RouteChecks : Node
{
    /// <summary>The longest road route from the village to the end of a road to a border, about four minutes' drive.</summary>
    internal const float LongestRoute = 3600;

    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private readonly Node3D arrows = new() { Name = "RouteArrows" };

    public RouteChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            world.AddChild(arrows);
            var whole = new RouteGuide(world, car, arrows, () => Vector3.Zero);
            Check(whole.IsConnected(), $"all {whole.Samples.Count} road samples of both networks are connected");
            Check(world.BorderRoads.Length == 4, "a road leads to each of the four borders");
            Check(
                world.BorderRoads.Select(road => road.Border).Distinct().Count() == 4,
                "the four roads lead to four different borders"
            );
            foreach (var (border, points) in world.BorderRoads)
                CheckRouteFromVillage(border, points[^1]);
            foreach (var (border, points) in world.BorderRoads)
                await CheckDriveFromRing(border, points[0], points[^1]);
            for (int join = 1; join <= world.OutlandJoinCount; join++)
                await CheckDriveJoin(join, world.OutlandRoutePoints[join]);
            GD.Print("ROUTE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            Release();
            GetTree().Quit(1);
        }
    }

    /// <summary>A guide that leads from <paramref name="from"/> to <paramref name="end"/>, its arrows made.</summary>
    private RouteGuide Guide(Vector3 from, Vector3 toward, Vector3 end)
    {
        foreach (var child in arrows.GetChildren())
            child.Free();
        var forward = toward - from;
        car.SetPose(world.Ground.At(from.X, from.Z, .15f), Mathf.Atan2(-forward.X, -forward.Z));
        var guide = new RouteGuide(world, car, arrows, () => end);
        guide.Retarget();
        guide.CreateArrows();
        return guide;
    }

    /// <summary>The route from the village to the end of one road to a border: its length and its arrows.</summary>
    private void CheckRouteFromVillage(Border border, Vector3 end)
    {
        int index = RouteGuide.NearestSample(world, world.Village.Center);
        var samples = world.Routes.Samples;
        var guide = Guide(samples[index], samples[Math.Min(samples.Count - 1, index + 2)], end);
        var route = guide.RoutePoints;
        float length = Length(route);
        Check(
            length < LongestRoute && Flat(route[^2], end) < 3,
            $"the end of the road to the {border} is {length:0} m from the village by road, less than {LongestRoute:0} m"
        );
        Check(
            route.Take(route.Count - 1).Any(p => !world.Routes.Contains(p)),
            $"the route to the {border} leaves the old roads for the roads beyond them"
        );
        CheckArrows(border);
    }

    /// <summary>Every road arrow stands on a road of either network, a little above its surface.</summary>
    private void CheckArrows(Border border)
    {
        int count = 0;
        foreach (var arrow in arrows.GetChildren().OfType<MeshInstance3D>())
        {
            if (!arrow.Name.ToString().StartsWith("RouteArrow"))
                continue;
            var p = arrow.Position;
            bool onRoad = world.Routes.Contains(p) || world.OutlandRoutes.Contains(p) || world.OnCrossing(p.X, p.Z);
            float surface = world.DriveSurface(p.X, p.Z);
            if (!onRoad || p.Y < surface + .3f || p.Y > surface + 2.6f)
                throw new InvalidOperationException(
                    $"a route arrow at {p} is not on the road (surface {surface:0.00})"
                );
            count++;
        }
        Check(count > 0, $"the {count} arrows on the way to the {border} stand on the road");
    }

    /// <summary>From where the road to a border leaves the ring, the car follows the guide to its end.</summary>
    private async Task CheckDriveFromRing(Border border, Vector3 start, Vector3 end)
    {
        var guide = Guide(start, start.Lerp(end, .02f), end);
        CheckArrows(border);
        var route = guide.RoutePoints.ToList();
        float best = await Follow(route, end, Length(route));
        guide.HideAll();
        Check(best < 8, $"the car follows the arrows from the ring to the {border} and arrives ({best:0.0} m)");
    }

    /// <summary>
    /// From the old road where a join leaves it, the car drives the join out to the ring. The part of it within the old
    /// valley lies on ground that the outland's grading does not change, so it climbs out of the old road's cut on a
    /// ramp of its own (<c>Terrain.GradeJoins</c>) and no flower rock stands in its way.
    /// </summary>
    private async Task CheckDriveJoin(int join, Vector3[] points)
    {
        var start = world.Ground.At(points[0].X, points[0].Z);
        var end = world.Ground.At(points[^1].X, points[^1].Z);
        var guide = Guide(start, start.Lerp(world.Ground.At(points[1].X, points[1].Z), .2f), end);
        var route = guide.RoutePoints.ToList();
        float steepest = 0;
        var steepestAt = Vector3.Zero;
        for (int i = 1; i < points.Length; i++)
        for (float t = 0; t < 1; t += 1f / 40)
        {
            var a = points[i - 1].Lerp(points[i], t);
            var b = points[i - 1].Lerp(points[i], t + 1f / 40);
            float run = Flat(a, b);
            float grade =
                Mathf.Abs(world.Ground.Height(b.X, b.Z) - world.Ground.Height(a.X, a.Z)) / Mathf.Max(run, .01f);
            if (run > .2f && grade > steepest)
            {
                steepest = grade;
                steepestAt = a;
            }
        }
        // The bank of an old road's cut rose .8 in one (seed 7421). The ground the joins cross by nature reaches .38,
        // and the ramp out of that cut on seed 7421 .42, because it must pass a point of the fingerprints' grid at the
        // point's own height (Terrain.Joins.cs).
        Check(
            steepest < .45f,
            $"join {join} climbs at most {steepest:0.00} (at {steepestAt.X:0} {steepestAt.Z:0}), no bank of an old road"
        );
        float best = await Follow(route, end, Length(route));
        guide.HideAll();
        Check(best < 8, $"the car drives join {join} from the old road out to the ring ({best:0.0} m)");
    }

    /// <summary>Drives along the route with the car's physics, steering a few points ahead; the nearest it came.</summary>
    private async Task<float> Follow(List<Vector3> route, Vector3 end, float length)
    {
        float best = float.PositiveInfinity;
        int progress = 0;
        int stuck = 0;
        var last = car.Position;
        var previous = car.Position;
        int frames = Mathf.CeilToInt(length / 8 * Engine.PhysicsTicksPerSecond) + 600;
        for (int frame = 0; frame < frames && best > 5; frame++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            // Bandits may stop the car on the way with their question; it is answered and the drive goes on.
            if (game.State == GameState.Challenge)
            {
                Release();
                ChallengeChecks.Solve(game);
                last = car.Position;
                continue;
            }
            if (game.State != GameState.Exploring)
                throw new InvalidOperationException($"the drive was stopped by {game.State} at {car.Position}");
            if (Flat(car.Position, previous) > 20)
            {
                GD.Print(
                    $"ROUTE_RESET from {previous} (ground {world.Ground.Height(previous.X, previous.Z):0.00}), route point {progress} of {route.Count}"
                );
                foreach (
                    var body in GetTree().Root.FindChildren("*", "StaticBody3D", true, false).OfType<StaticBody3D>()
                )
                foreach (var shape in body.GetChildren().OfType<CollisionShape3D>())
                    if (shape.GlobalPosition.DistanceTo(previous) < 6)
                        GD.Print(
                            $"ROUTE_NEAR {body.GetPath()} ({body.GetParent()?.GetType().Name} {body.GetParent()?.Name} in {body.GetParent()?.GetParent()?.GetType().Name} {body.GetParent()?.GetParent()?.Name}) shape at {shape.GlobalPosition} {shape.Shape?.GetType().Name}"
                        );
                break;
            }
            previous = car.Position;
            while (
                progress + 1 < route.Count
                && Flat(car.Position, route[progress + 1]) < Flat(car.Position, route[progress])
            )
                progress++;
            var target = route[Math.Min(route.Count - 1, progress + 6)] - car.Position;
            float error = Mathf.AngleDifference(car.Heading, Mathf.Atan2(-target.X, -target.Z));
            float steer = Mathf.Clamp(-error * 2.5f, -1, 1);
            Input.ActionRelease("left");
            Input.ActionRelease("right");
            Input.ActionPress(steer < 0 ? "left" : "right", Mathf.Abs(steer));
            Input.ActionPress("accelerate", Mathf.Abs(error) > .45f ? .3f : 1);
            best = Mathf.Min(best, Flat(car.Position, end));
            if (frame % 120 == 119)
            {
                stuck = Flat(car.Position, last) < 2 ? stuck + 1 : 0;
                last = car.Position;
                if (stuck >= 3)
                {
                    GD.Print($"ROUTE_STUCK at {car.Position}, route point {progress} of {route.Count}");
                    break;
                }
            }
        }
        if (best > 5)
            GD.Print($"ROUTE_END at {car.Position}, route point {progress} of {route.Count}, speed {car.Speed:0.0}");
        Release();
        return best;
    }

    private static float Length(IReadOnlyList<Vector3> route)
    {
        float length = 0;
        for (int i = 1; i < route.Count; i++)
            length += Flat(route[i - 1], route[i]);
        return length;
    }

    private static void Release()
    {
        Input.ActionRelease("accelerate");
        Input.ActionRelease("left");
        Input.ActionRelease("right");
    }

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();
}
