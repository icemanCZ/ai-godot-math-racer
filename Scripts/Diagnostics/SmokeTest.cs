using Godot;
using System;
using System.Linq;

namespace MathRacer;

// Added only by the command-line validation mode; never runs during ordinary play.
public partial class SmokeTest : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int frame;
    public SmokeTest(Game game, World world, Vehicle car) { this.game = game; this.world = world; this.car = car; }
    public override void _PhysicsProcess(double delta)
    {
        frame++;
        if (frame == 5) { car.Reset(); Input.ActionPress("accelerate"); }
        if (frame == 95)
        {
            Check(car.Position.DistanceTo(world.Ground.At(0, 8)) > 3, "vehicle moves");
            Check(Mathf.Abs(car.Position.Y - world.Ground.Height(car.Position.X, car.Position.Z)) < 2, "terrain contact");
            Check(car.Tracks.SegmentCount > 10, "moving vehicle leaves tyre tracks");
            Check(car.Tracks.HiddenSamples(world.Ground, world.Routes) == 0, "first metres of tyre ribbons remain above the road, including triangle interiors");
            Input.ActionPress("brake");
        }
        if (frame == 135)
        {
            Check(Mathf.Abs(car.Speed) < .1f, "brake overrides accelerator");
            int stoppedTracks = car.Tracks.SegmentCount;
            for (int i = 0; i < 5; i++) car.Tracks.Update(1f / 60, car, world.Ground, world.Routes);
            Check(car.Tracks.SegmentCount == stoppedTracks, "stationary wheels do not add tyre tracks");
            Input.ActionRelease("brake"); Input.ActionRelease("accelerate"); Input.ActionPress("reverse"); Input.ActionPress("right");
        }
        if (frame == 195)
        {
            Check(car.Speed < -3, "reverse"); Check(Mathf.Abs(car.Heading) > .15f, "steering");
            Input.ActionRelease("reverse"); Input.ActionRelease("right");
            car.Reset(); Check(car.Position.DistanceTo(world.Ground.At(0, 8)) < 1, "reset");
            CheckCamera();
            game.OpenChallenge(world.Animals[0]); Check(game.State == GameState.Challenge && !car.Active, "animal opens question directly");
            game.TogglePause(); game.TogglePause(); Check(game.State == GameState.Challenge, "resume question");
            game.FinishInteraction(); game.TogglePause(); Check(!car.Active, "pause freezes driving"); game.TogglePause();
            Check(game.State == GameState.Exploring && car.Active, "pause resume");
            Check(world.Animals.Length == 30 && world.Routes.Samples.Count > 100, "world content");
            Check(world.Grass.TileCount > 200 && world.Grass.TileCount <= 1024 &&
                world.Grass.TuftCount > 40000 && world.Grass.TuftCount < 260000,
                "dense grass streams within a bounded camera neighborhood");
            Check(world.Grass.LodTileCount(0) > 20 && world.Grass.LodTileCount(1) > 50 && world.Grass.LodTileCount(2) > 50,
                "near, middle and distant grass LODs are populated");
            var grassBefore = world.Grass.LayerIdentities();
            var grassCamera = GetViewport().GetCamera3D();
            var grassCameraPosition = grassCamera.GlobalPosition;
            grassCamera.GlobalPosition += Vector3.Right * 40;
            world.Grass._Process(.31);
            var grassAfter = world.Grass.LayerIdentities();
            var commonGrass = grassBefore.Keys.Where(grassAfter.ContainsKey).ToArray();
            Check(commonGrass.Length > 100 && commonGrass.All(key => grassBefore[key] == grassAfter[key]),
                "camera movement across LOD bands preserves resident grass geometry");
            grassCamera.GlobalPosition = grassCameraPosition;
            world.Grass._Process(0);
            Check(!world.Grass.CanGrow(world.Ground.At(0, 8)) &&
                !world.Grass.CanGrow(world.Ground.At(River.CenterX(30), 30)) &&
                !world.Grass.CanGrow(world.Animals[0].Position),
                "grass leaves roads, water and animal clearings open");
            WildlifeChecks.Run(world, car, Check);
            CheckRoadSurface();
            Check(world.Animals.All(a => world.Routes.Distance(a.Position) >= 5.5f && world.Routes.Distance(a.Position) <= 7 && !world.Village.IsReserved(a.Position) && River.Distance(a.Position.X, a.Position.Z) > River.HalfWidth(a.Position.Z) + 5), "all animals stand safely beside roads within interaction range");
            Check(world.Animals.Select(a => a.Kind).Distinct().Count() == 3 && world.Animals.GroupBy(a => a.Kind).All(g => g.Select(a => a.FurColor).Distinct().Count() == 3), "three animal kinds with three fur colors each");
            Check(Settings.PlayableRadius * Settings.PlayableRadius / (166 * 166f) > 9.99f, "world playable area increased tenfold");
            Check(world.Ground.Height(-350, -250) > 10, "outer hills have elevation");
            ChallengeChecks.Run(game, world, car, Check);
            car.SetPose(world.Ground.At(River.CenterX(River.BridgeZ) - 25, River.BridgeZ, .15f), -Mathf.Pi / 2);
            Input.ActionPress("accelerate");
        }
        if (frame == 350)
        {
            Check(River.OnBridge(car.Position.X, car.Position.Z) && car.Position.Y > River.Level(River.BridgeZ) + .6f, "bridge supports vehicle above water");
        }
        if (frame == 465)
        {
            Input.ActionRelease("accelerate");
            Check(car.Position.X > River.CenterX(River.BridgeZ) + 22, "vehicle crosses river on bridge");
            car.SetPose(world.Ground.At(-350, -250, .15f), 0);
        }
        if (frame == 525)
        {
            Check(new Vector2(car.Position.X, car.Position.Z).Length() > 350 && Mathf.Abs(car.Position.Y - world.Ground.Height(car.Position.X, car.Position.Z)) < 2, "expanded world terrain supports vehicle beyond old boundary");
            car.Reset();
            Input.ActionPress("accelerate");
        }
        if (frame == 1725)
        {
            Check(car.Tracks.OldestAge >= 18 && car.Tracks.OldestAge <= 25.1f, "sustained driving retains tracks through their gradual fade");
            Input.ActionRelease("accelerate");
            // Isolate grip checks from the now-solid signpost directly ahead of spawn.
            // Scenery impacts are exercised separately by CollisionChecks.
            Obstacles.Box(world, new Vector3(0, 79.5f, 0), new Vector3(60, 1, 60));
            car.SetPose(new Vector3(0, 80.15f, 8), 0);
            Input.ActionPress("accelerate");
        }
        if (frame == 1875)
        {
            Input.ActionPress("brake"); Input.ActionPress("right");
        }
        if (frame == 1887)
        {
            float sideways = Mathf.Abs(new Vector3(car.Velocity.X, 0, car.Velocity.Z).Dot(car.GlobalBasis.X));
            Check(sideways > 1.5f, "handbrake turn produces sideways slide");
            Check(car.Speed > 1 && car.Speed < Settings.MaxSpeed - 3, "handbrake slows car during slide");
            Input.ActionRelease("brake"); Input.ActionRelease("right");
        }
        if (frame == 1907)
        {
            float sideways = Mathf.Abs(new Vector3(car.Velocity.X, 0, car.Velocity.Z).Dot(car.GlobalBasis.X));
            Check(sideways < .15f, "releasing handbrake restores lateral grip");
            Input.ActionPress("brake");
        }
        if (frame == 1955)
        {
            Check(Mathf.Abs(car.Speed) < .01f && new Vector2(car.Velocity.X, car.Velocity.Z).Length() < .01f, "handbrake stops sideways motion completely");
            Input.ActionRelease("brake"); Input.ActionRelease("accelerate");
            GD.Print("SMOKE_TEST_PASSED"); GetTree().Quit();
        }
    }
    private void CheckRoadSurface()
    {
        int samples = 0;
        bool aboveRoad = true;
        foreach (var child in world.Routes.GetChildren())
        {
            if (child is not MeshInstance3D road) continue;
            var faces = road.Mesh.GetFaces();
            for (int i = 0; i + 2 < faces.Length; i += 411)
            {
                var center = (faces[i] + faces[i + 1] + faces[i + 2]) / 3;
                float height = world.Routes.SurfaceHeight(center, world.Ground.Height(center.X, center.Z));
                aboveRoad &= height >= center.Y - .001f;
                samples++;
            }
        }
        Check(samples > 100, "road projection checked across the world");
        Check(aboveRoad, "tyre projection stays above rendered road triangles");
    }

    private void CheckCamera()
    {
        var camera = (FollowCamera)GetViewport().GetCamera3D();
        var originalMouseMode = Input.MouseMode;
        camera.Snap();
        camera._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
        camera._Input(new InputEventMouseMotion { Relative = new Vector2(260, -10000) });
        for (int i = 0; i < 120; i++) camera._Process(1.0 / 60);
        var offset = camera.Position - car.Position;
        Check(Mathf.Abs(offset.X) > 8 && Mathf.Abs(offset.Z) < 1, "mouse orbits to side of car and holds before timeout");
        Check(offset.Y >= 3 && offset.Y < 5, "vertical mouse orbit clamps above ground");
        camera._Input(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false });
        Check(Input.MouseMode == originalMouseMode, "releasing orbit restores mouse");
        for (int i = 0; i < 480; i++) camera._Process(1.0 / 60);
        offset = camera.Position - car.Position;
        Check(Mathf.Abs(offset.X) < .05f && Mathf.Abs(offset.Z - 10.5f) < .05f && Mathf.Abs(offset.Y - 5.7f) < .05f, "idle camera returns behind car");
        camera._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
        camera._Input(new InputEventMouseMotion { Relative = new Vector2(524, 10000) });
        for (int i = 0; i < 120; i++) camera._Process(1.0 / 60);
        Check(camera.Position.Z < car.Position.Z - 3 && camera.Position.Y > car.Position.Y + 10, "orbit reaches front and roof view");
        game.TogglePause(); camera._Process(1.0 / 60);
        Check(Input.MouseMode == originalMouseMode, "pause releases orbit mouse");
        camera.Snap();
        camera._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
        camera._Input(new InputEventMouseMotion { Relative = new Vector2(260, 100) });
        camera._Process(.5);
        Check(Mathf.Abs(camera.Position.X - car.Position.X) < .01f && Input.MouseMode == originalMouseMode, "paused camera ignores mouse orbit");
        game.TogglePause(); camera.Snap();
        camera._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
        camera._Notification((int)NotificationApplicationFocusOut);
        Check(Input.MouseMode == originalMouseMode, "losing focus releases orbit mouse");
        camera.Snap();
    }

    private void Check(bool condition, string label)
    {
        if (!condition) { GD.PushError("TEST_FAILED: " + label); GetTree().Quit(1); throw new InvalidOperationException(label); }
        GD.Print("PASS: " + label);
    }
}

