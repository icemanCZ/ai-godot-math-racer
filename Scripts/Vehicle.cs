using Godot;

namespace MathRacer;

public partial class Vehicle : CharacterBody3D
{
    public bool Active;
    public float Speed { get; private set; }
    public float Heading { get; private set; }
    public VehicleAppearance Appearance { get; } = new();
    private Terrain terrain = null!;
    private Roads roads = null!;
    internal WheelTracks Tracks { get; } = new();
    private readonly Node3D model = new();
    private CollisionShape3D chassis = null!;
    private readonly System.Collections.Generic.List<Node3D> wheels = new();
    private float stuckTime;
    private Vector3 lastPosition;
    public void Setup(Terrain ground, Roads routes)
    {
        roads = routes;
        AddChild(Tracks);
        terrain = ground; FloorSnapLength = 1.2f; FloorMaxAngle = Mathf.DegToRad(48);
        AddChild(new CollisionShape3D { Position = new Vector3(0, .65f, 0), Shape = new CapsuleShape3D { Radius = .72f, Height = 1.4f } });
        // The rounded ground contact supports slopes; the chassis catches obstacles
        // at the bumpers and sides instead of letting them reach the car's centre.
        chassis = new CollisionShape3D { Position = new Vector3(0, .8f, 0), Shape = new BoxShape3D { Size = new(1.85f, 1.2f, 3.1f) } };
        AddChild(chassis);
        AddChild(model);
        // Shared axles preserve wheel rotation when swapping coachwork.
        VehicleModel.BuildDisplay(model, Appearance, wheels);
        Reset();
    }
    public void Reset()
    {
        SetPose(terrain.At(0, 8, .15f), 0);
    }
    internal void SetPose(Vector3 position, float heading)
    {
        Position = position; Heading = heading; Rotation = new Vector3(0, heading, 0);
        Speed = 0; Velocity = Vector3.Zero; stuckTime = 0; lastPosition = position;
        Tracks.Break();
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (!Active) { Speed = 0; Velocity = Vector3.Zero; Tracks.Break(); return; }
        float throttle = Mathf.Max(Input.GetActionStrength("accelerate"), Input.GetActionStrength("stick_forward"))
            - Mathf.Max(Input.GetActionStrength("reverse"), Input.GetActionStrength("stick_reverse"));
        float steer = Input.GetAxis("left", "right");
        float target = throttle >= 0 ? throttle * Settings.MaxSpeed : throttle * Settings.ReverseSpeed;
        bool braking = Input.IsActionPressed("brake");
        if (braking) target = 0;
        Speed = Mathf.MoveToward(Speed, target, (braking ? 25 : throttle == 0 ? 4 : Settings.Acceleration) * dt);
        Heading -= steer * Settings.Steering * (braking ? 1.7f : 1) * Mathf.Clamp(Speed / 5, -1, 1) / (1 + Mathf.Abs(Speed) * .035f) * dt;
        Rotation = new Vector3(0, Heading, 0);
        var forward = -Transform.Basis.Z;
        float Surface(float x, float z) => River.OnBridge(x, z) ? Mathf.Max(terrain.Height(x, z), River.BridgeSurfaceHeight(x)) : terrain.Height(x, z);
        float Slope(Vector3 direction) => (Surface(Position.X + direction.X * 1.6f, Position.Z + direction.Z * 1.6f) -
            Surface(Position.X - direction.X * 1.6f, Position.Z - direction.Z * 1.6f)) / 3.2f;
        // Let the chassis follow the road plane; an upright long box can catch on an uphill face.
        float pitch = Mathf.Atan(Slope(forward));
        float bank = Mathf.Atan(Slope(Transform.Basis.X));
        if (Mathf.Abs(Position.Y - Surface(Position.X, Position.Z)) > 1.5f)
        {
            // Elevated platforms and obstacles use their actual support plane, not terrain below them.
            var normal = IsOnFloor() ? GetFloorNormal() : Vector3.Up;
            pitch = Mathf.Atan2(-normal.Dot(forward), normal.Y);
            bank = Mathf.Atan2(-normal.Dot(Transform.Basis.X), normal.Y);
        }
        chassis.Rotation = new Vector3(pitch, 0, bank);
        // The handbrake loosens lateral grip: the body turns while momentum
        // keeps carrying the car along its previous direction. Grip returns smoothly.
        var movement = new Vector3(Velocity.X, 0, Velocity.Z);
        movement = movement.Lerp(forward * Speed, 1 - Mathf.Exp(-(braking ? 1.5f : 18f) * dt));
        movement = movement.LimitLength(Mathf.Abs(Speed));
        Velocity = new Vector3(movement.X, Velocity.Y - 24 * dt, movement.Z);
        var previousPosition = Position;
        var intendedPosition = Position + movement * dt;
        MoveAndSlide();
        bool movingAwayFromWater = River.Distance(intendedPosition.X, intendedPosition.Z) - River.HalfWidth(intendedPosition.Z) >
            River.Distance(previousPosition.X, previousPosition.Z) - River.HalfWidth(previousPosition.Z) + .00001f;
        bool blockedByWater = !movingAwayFromWater && !River.OnBridge(Position.X, Position.Z) &&
            River.Distance(Position.X, Position.Z) < River.HalfWidth(Position.Z) + 1 &&
            Position.Y < River.Level(Position.Z) - .35f;
        if (blockedByWater)
        {
            // Keep the last safe position so the driver can reverse away from the stream.
            Position = previousPosition;
            Speed = 0;
            Velocity = Vector3.Zero;
        }
        // MoveAndSlide removes blocked velocity; also shed the arcade speed so
        // the car cannot store full throttle against a wall and launch away.
        float impact = 0;
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            var normal = collision.GetNormal();
            if (Mathf.Abs(normal.Y) > .7f) continue; // Keep normal hill driving unchanged.
            var horizontal = new Vector3(normal.X, 0, normal.Z).Normalized();
            if (collision.GetCollider() is StaticBody3D body && body.GetParent() is Vegetation plants)
                plants.HitTree(body, movement, horizontal);
            impact = Mathf.Max(impact, -movement.Normalized().Dot(horizontal));
        }
        if (impact > 0)
            Speed *= Mathf.Sqrt(Mathf.Max(0, 1 - impact * impact));
        stuckTime = !blockedByWater && Mathf.Abs(throttle) > .5f && !braking && Position.DistanceTo(lastPosition) < .005f ? stuckTime + dt : 0;
        lastPosition = Position;
        if (stuckTime > 5) Reset();
        if (new Vector2(Position.X, Position.Z).Length() > Settings.PlayableRadius || Position.Y < terrain.Height(Position.X, Position.Z) - 25) Reset();
        float slope = (Surface(Position.X + forward.X, Position.Z + forward.Z) - Surface(Position.X - forward.X, Position.Z - forward.Z)) / 2;
        model.Rotation = model.Rotation.Lerp(new Vector3(Mathf.Atan(slope), 0, bank - steer * Speed * .003f), dt * 7);
        foreach (var wheel in wheels) wheel.RotateX(-Speed * dt * 2);
        Tracks.Update(dt, this, terrain, roads);
    }
}

