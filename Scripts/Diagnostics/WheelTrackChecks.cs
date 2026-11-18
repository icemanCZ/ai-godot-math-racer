using Godot;

namespace MathRacer;

// Reproduce driving right from spawn with several steering strengths. A hard turn can end against a tree beside the
// road, and after five seconds the car is put back on the road. That jump is not driving, so it is not a gap in the tracks.
public partial class WheelTrackChecks : Node
{
    private readonly Vehicle car;
    private readonly Terrain terrain;
    private readonly Roads roads;
    private readonly float[] steering = { .15f, .35f, .6f, 1f };
    private int frame,
        scenario,
        failures;
    private float previousRecorded;
    private bool movedByReset;
    private Vector3 previousPosition;

    public WheelTrackChecks(Vehicle car, Terrain terrain, Roads roads)
    {
        this.car = car;
        this.terrain = terrain;
        this.roads = roads;
        car.PoseReset += () => movedByReset = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        frame++;
        if (frame == 1)
        {
            car.Reset();
            Input.ActionPress("accelerate");
            Input.ActionPress("right", steering[scenario]);
            previousRecorded = car.Tracks.DistanceRecorded;
            previousPosition = car.Position;
            movedByReset = false;
        }
        if (frame == 90)
            Input.ActionRelease("right");
        if (frame % 30 == 0)
        {
            var displacement = car.Position.DistanceTo(previousPosition);
            bool dryLand = River.Distance(car.Position.X, car.Position.Z) > River.HalfWidth(car.Position.Z) + 4;
            if (
                !movedByReset
                && dryLand
                && displacement > .7f
                && displacement < 10
                && car.Tracks.DistanceRecorded <= previousRecorded
            )
            {
                failures++;
                var p = car.Position;
                GD.Print(
                    $"TRACK_GAP steering={steering[scenario]} frame={frame} position={p} floor={car.IsOnFloor()} ground={terrain.Height(p.X, p.Z)} water={River.Level(p.Z)} riverDistance={River.Distance(p.X, p.Z)}"
                );
            }
            previousRecorded = car.Tracks.DistanceRecorded;
            previousPosition = car.Position;
            movedByReset = false;
        }
        if (frame < 900)
            return;
        Input.ActionRelease("accelerate");
        Input.ActionRelease("right");
        int hidden = car.Tracks.HiddenSamples(terrain, roads);
        failures += hidden > 0 ? 1 : 0;
        GD.Print($"TRACK_SCENARIO_DONE steering={steering[scenario]} hidden={hidden}");
        if (++scenario < steering.Length)
        {
            frame = 0;
            return;
        }
        GD.Print(failures == 0 ? "WHEEL_TRACK_CHECKS_PASSED" : $"WHEEL_TRACK_CHECKS_FAILED gaps={failures}");
        GetTree().Quit(failures == 0 ? 0 : 1);
    }
}
