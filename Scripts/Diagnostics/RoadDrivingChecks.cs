using Godot;
using System;
using System.Collections.Generic;

namespace MathRacer;

/// <summary>Drive every route in both directions using the normal vehicle physics and scene obstacles.</summary>
public partial class RoadDrivingChecks : Node
{
    private readonly World world;
    private readonly Vehicle car;
    private readonly List<Vector3> path = new();
    private int route, direction, progress, frame, lastProgressFrame;
    private bool started;
    public RoadDrivingChecks(World world, Vehicle car) { this.world = world; this.car = car; }

    private void Begin()
    {
        using var curve = WorldPlan.CreateCurve(WorldLayout.Plan.Routes[route]);
        float length = curve.GetBakedLength();
        path.Clear();
        for (float offset = 1; offset < length - 1; offset += 1)
            path.Add(curve.SampleBaked(direction == 0 ? offset : length - offset));
        var forward = (path[5] - path[0]).Normalized();
        var p = path[0];
        float height = world.Ground.Height(p.X, p.Z);
        if (River.OnBridge(p.X, p.Z)) height = Mathf.Max(height, River.BridgeSurfaceHeight(p.X));
        car.SetPose(new Vector3(p.X, height + .15f, p.Z), Mathf.Atan2(-forward.X, -forward.Z));
        progress = frame = lastProgressFrame = 0;
        started = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!started) Begin();
        frame++;
        var flatCar = new Vector2(car.Position.X, car.Position.Z);
        int nearest = progress;
        float best = float.MaxValue;
        for (int i = Mathf.Max(0, progress - 2); i < Mathf.Min(path.Count, progress + 16); i++)
        {
            float distance = flatCar.DistanceSquaredTo(new Vector2(path[i].X, path[i].Z));
            if (distance < best) { best = distance; nearest = i; }
        }
        if (nearest > progress) { progress = nearest; lastProgressFrame = frame; }
        if (best > 25 || frame - lastProgressFrame > 300)
        {
            for (int i = 0; i < car.GetSlideCollisionCount(); i++)
            {
                var collision = car.GetSlideCollision(i);
                var collider = collision.GetCollider() as Node3D;
                GD.Print($"ROAD_DRIVING_CONTACT collider={collider?.GetPath()} position={collider?.GlobalPosition} normal={collision.GetNormal()} floor={car.IsOnFloor()}");
            }
            GD.PushError($"ROAD_DRIVING_FAILED seed={Settings.Seed} route={route} direction={direction} progress={progress}/{path.Count} position={car.Position} distance={Mathf.Sqrt(best)} speed={car.Speed}");
            GetTree().Quit(1); SetPhysicsProcess(false); return;
        }
        if (progress >= path.Count - 3)
        {
            GD.Print($"ROAD_DRIVEN seed={Settings.Seed} route={route} direction={direction} metres={path.Count}");
            if (++direction == 2) { direction = 0; route++; }
            if (route == WorldLayout.Plan.Routes.Length)
            {
                GD.Print("ROAD_DRIVING_CHECKS_PASSED"); GetTree().Quit(); SetPhysicsProcess(false); return;
            }
            started = false; return;
        }
        var target = path[Mathf.Min(path.Count - 1, progress + 6)] - car.Position;
        float desired = Mathf.Atan2(-target.X, -target.Z);
        float error = Mathf.AngleDifference(car.Heading, desired);
        float steer = Mathf.Clamp(-error * 2.5f, -1, 1);
        Input.ActionRelease("left"); Input.ActionRelease("right");
        Input.ActionPress(steer < 0 ? "left" : "right", Mathf.Abs(steer));
        Input.ActionPress("accelerate", Mathf.Abs(error) > .45f ? .25f : .55f);
    }
}
