using Godot;
using System;
using System.Linq;

namespace MathRacer;

// Real fixed-tick impacts using shapes copied from the generated scenery.
public partial class CollisionChecks : Node
{
    private readonly Vehicle car;
    private readonly World world;
    private StaticBody3D obstacle = null!;
    private readonly Vector3 origin = new(0, 80, 0);
    private Shape3D[] shapes = Array.Empty<Shape3D>();
    private int frame, scenario;
    private float contactZ;
    private Vector3 treeBodyPosition;
    private bool treeImpactSeen;
    public CollisionChecks(Vehicle car, World world) { this.car = car; this.world = world; }

    public override void _Ready()
    {
        var trunks = GetTree().GetNodesInGroup("obstacle_trunks");
        var rocks = GetTree().GetNodesInGroup("obstacle_rocks");
        Check(trunks.Count >= world.Plants.TreeCount + 26, "every tree, fence post and sign has a solid trunk");
        Check(rocks.Count > 100, "generated meadow and river rocks are solid");
        var boxes = GetTree().GetNodesInGroup("obstacle_boxes");
        int garageWalls = world.Village.GarageBuilding.GetChildren().OfType<StaticBody3D>().Count();
        Check(garageWalls == 3 && boxes.Count == 20 + garageWalls, "all bench parts, fence rails and garage walls are solid, gates stay open");
        shapes = new[] {
            trunks.Cast<StaticBody3D>().Select(body => body.GetChild<CollisionShape3D>(0).Shape)
                .OfType<CapsuleShape3D>().OrderByDescending(shape => shape.Height).First(),
            ((StaticBody3D)rocks.First()).GetChild<CollisionShape3D>(0).Shape,
            ((StaticBody3D)boxes.First()).GetChild<CollisionShape3D>(0).Shape,
            // An upright fence rail, a house wall and a bridge rail.
            new BoxShape3D { Size = new(3.6f, .13f, .13f) },
            new BoxShape3D { Size = new(7, 4, 6) },
            new BoxShape3D { Size = new(38, 1.3f, .25f) }
        };
        Obstacles.Box(world, origin - Vector3.Up * .5f, new(60, 1, 60));
        obstacle = Obstacles.Add(world, origin, shapes[0]);
    }

    public override void _PhysicsProcess(double delta)
    {
        frame++;
        if (scenario == shapes.Length)
        {
            if (frame == 1)
            {
                obstacle.QueueFree();
                obstacle = GetTree().GetNodesInGroup("obstacle_trunks").Cast<StaticBody3D>()
                    .First(body => body.GetParent() == world.Plants);
                treeBodyPosition = obstacle.Position;
                obstacle.Position = origin + new Vector3(0, 1.5f, -5);
                car.SetPose(origin + new Vector3(0, .15f, 5), 0);
                Input.ActionPress("accelerate");
            }
            treeImpactSeen |= world.Plants.ShakingTreeCount > 0 && world.Plants.FallingLeafCount >= 5;
            if (frame == 150)
            {
                Check(treeImpactSeen, "real vehicle impact shakes the generated tree and releases leaves");
                Check(car.Position.Z > -5 && Mathf.Abs(car.Speed) < 1, "animated tree trunk remains solid");
            }
            if (frame < 480) return;
            Check(world.Plants.ShakingTreeCount == 0 && world.Plants.FallingLeafCount == 0,
                "tree settles and leaves are removed; holding throttle does not repeat the effect");
            Input.ActionRelease("accelerate");
            obstacle.Position = treeBodyPosition;
            world.Plants.HitTree(obstacle, Vector3.Forward * 10, Vector3.Back);
            Check(world.Plants.ShakingTreeCount == 1 && world.Plants.FallingLeafCount >= 5, "a later impact can shake the same tree again");
            GD.Print("COLLISION_CHECKS_PASSED");
            GetTree().Quit();
            return;
        }
        if (frame == 1)
        {
            obstacle.GetChild<CollisionShape3D>(0).Shape = shapes[scenario];
            // Bury the lower half of rocks, as in the actual scenery.
            float lift = scenario == 1 ? 0 : scenario == 3 ? .45f : .65f;
            obstacle.Position = origin + new Vector3(0, lift, -5);
            car.SetPose(origin + new Vector3(0, .15f, 5), 0);
            Input.ActionPress("accelerate");
        }
        if (frame == 150)
        {
            Check(car.Position.Y > 79.8f && car.Position.Z > -5,
                $"scenario {scenario}: fast forward impact cannot pass through obstacle");
            Check(Mathf.Abs(car.Speed) < 1, $"scenario {scenario}: impact sheds stored speed");
            contactZ = car.Position.Z;
            Input.ActionRelease("accelerate"); Input.ActionPress("reverse");
        }
        if (frame == 210)
        {
            Check(car.Position.Z > contactZ + 2 && car.Speed < -3,
                $"scenario {scenario}: car can reverse away after impact");
            // Reverse into the same obstacle from its other side.
            car.SetPose(origin + new Vector3(0, .15f, -15), 0);
        }
        if (frame < 390) return;
        Check(car.Position.Z < -5 && Mathf.Abs(car.Speed) < 1,
            $"scenario {scenario}: reverse impact is blocked and loses speed");
        Input.ActionRelease("reverse");
        scenario++;
        frame = 0;
    }

    private void Check(bool condition, string message)
    {
        if (condition) { GD.Print("PASS " + message); return; }
        GD.PrintErr("COLLISION_CHECK_FAILED " + message + $" position={car.Position} speed={car.Speed}");
        GetTree().Quit(1);
        throw new InvalidOperationException(message);
    }
}
