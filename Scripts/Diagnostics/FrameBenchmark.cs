using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// Measures how hard the game is on the machine: the car drives the roads by itself through a day, a storm and a
/// night, and parks on the finished village square, while every frame's time, the GPU and CPU render time and the
/// number of draw calls are recorded. Started with <c>--benchmark</c> in a real window (not headless), with vsync off and
/// no frame cap, so the frame rate is what the machine can do. Prints one <c>BENCHMARK</c> line per stage.
/// </summary>
public partial class FrameBenchmark : Node
{
    private sealed record Stage(string Name, float Seconds, bool Drives, Action? Enter = null);

    private sealed class Samples
    {
        public readonly List<double> Frame = new(),
            Gpu = new(),
            Cpu = new(),
            Draws = new(),
            Primitives = new(),
            Objects = new();
    }

    private const float Warmup = 2f;
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private readonly List<Stage> stages = new();
    private readonly List<Vector3> path = new();

    /// <summary>The roads the car drives in turn: the old valley's, then those beyond it.</summary>
    private Vector3[][] routes = WorldLayout.Plan.Routes;
    private Samples samples = new();
    private int stageIndex = -1,
        route,
        progress,
        stalled;
    private double stageTime;
    private ulong startedMsec;

    public FrameBenchmark(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    public override void _Ready()
    {
        startedMsec = Time.GetTicksMsec();
        // The engine's clock starts with the process, so this is how long the valley took to be ready.
        GD.Print($"BENCHMARK_START startup_seconds={startedMsec / 1000.0:0.0}");
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;
        RenderingServer.ViewportSetMeasureRenderTime(GetViewport().GetViewportRid(), true);
        float scale = float.TryParse(LaunchOptions.Value("--benchmark-scale"), out float s) ? s : 1;
        if (scale != 1)
            GetViewport().Scaling3DScale = scale;
        game.DistantDetails.Complete();
        ReportBatches();
        ReportSingles();
        TurnOff(LaunchOptions.Value("--benchmark-off")?.Split(',') ?? Array.Empty<string>());
        float seconds = float.TryParse(LaunchOptions.Value("--benchmark-seconds"), out float t) ? t : 15;
        var village = world.Village;
        stages.Add(new("jizda-den", seconds, true));
        stages.Add(new("jizda-bourka", seconds, true, () => game.Weather.Begin(WeatherKind.Storm, instant: true)));
        stages.Add(new("naves-den-postaveno", seconds * .6f, false, () =>
            {
                game.Weather.Begin(WeatherKind.Clear, instant: true);
                village.RestoreLevels(new[] { 10, 10, 10, 10 });
                village.RestoreGlazed(new[] { true, true, true, true });
                Park();
            }));
        stages.Add(new("jizda-noc", seconds, true, () => game.Weather.Begin(WeatherKind.Night, instant: true)));
        stages.Add(new("naves-noc-postaveno", seconds * .6f, false, Park));
        // Beyond the old valley: round the ring, then out to the borders with the mountains and the sea in view.
        stages.Add(new("jizda-pas", seconds, true, () => DriveOutland(world.OutlandRoutePoints.Take(1))));
        stages.Add(
            new("jizda-k-hranicim", seconds, true, () =>
                DriveOutland(world.OutlandRoutePoints.Skip(1 + world.OutlandJoinCount))
            )
        );
        StartRoute();
        Next();
    }

    /// <summary>
    /// The single meshes of the valley (not the instanced batches), counted by the part of the world they belong to and by
    /// their size: thousands of small ones each cost a draw call wherever they are on screen.
    /// </summary>
    private void ReportSingles()
    {
        var singles = GetTree()
            .Root.FindChildren("*", nameof(GeometryInstance3D), true, false)
            .Cast<GeometryInstance3D>()
            .Where(node => node is not MultiMeshInstance3D)
            .Select(node =>
            {
                Node top = node;
                while (top.GetParent() is { } parent && parent is not World && parent is not Game)
                    top = parent;
                var size = node is VisualInstance3D visual
                    ? (visual.GlobalTransform * visual.GetAabb()).Size
                    : Vector3.Zero;
                float extent = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
                return (
                    Node: node,
                    Group: $"{top.GetType().Name}:{top.Name}",
                    Extent: extent,
                    Ranged: node.VisibilityRangeEnd > 0
                );
            })
            .ToArray();
        GD.Print($"BENCHMARK_SINGLES count={singles.Length} ranged={singles.Count(s => s.Ranged)}");
        foreach (var group in singles.GroupBy(s => s.Group).OrderByDescending(g => g.Count()).Take(20))
            GD.Print(
                $"BENCHMARK_SINGLE group={group.Key} count={group.Count()} ranged={group.Count(s => s.Ranged)} "
                    + $"under_0.3m={group.Count(s => s.Extent < .3f)} under_1m={group.Count(s => s.Extent < 1)} "
                    + $"under_3m={group.Count(s => s.Extent < 3)} types={string.Join(",", group.GroupBy(s => s.Node.GetType().Name).Select(t => $"{t.Key}:{t.Count()}"))}"
            );
    }

    /// <summary>The biggest instanced batches of the valley: how many triangles each one sends to the GPU.</summary>
    private void ReportBatches()
    {
        var batches = GetTree()
            .Root.FindChildren("*", nameof(MultiMeshInstance3D), true, false)
            .Cast<MultiMeshInstance3D>()
            .Where(node => node.Multimesh?.Mesh != null)
            .Select(node =>
            {
                var mesh = node.Multimesh.Mesh;
                long triangles = 0;
                for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
                {
                    var arrays = mesh.SurfaceGetArrays(surface);
                    var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                    triangles +=
                        indices.Length > 0
                            ? indices.Length / 3
                            : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length / 3;
                }
                int count =
                    node.Multimesh.VisibleInstanceCount < 0
                        ? node.Multimesh.InstanceCount
                        : node.Multimesh.VisibleInstanceCount;
                return (
                    Node: node,
                    Instances: count,
                    MeshTriangles: triangles,
                    Total: triangles * count * (node.MaterialOverlay != null ? 2 : 1),
                    Size: node.GetAabb().Size
                );
            })
            .OrderByDescending(batch => batch.Total)
            .ToArray();
        GD.Print(
            $"BENCHMARK_BATCHES count={batches.Length} triangles_all={batches.Sum(b => b.Total)} "
                + $"instances_all={batches.Sum(b => (long)b.Instances)}"
        );
        // Grouped by the mesh they share, which is one shape of the vegetation (a canopy, a flower leaf, a trunk).
        foreach (
            var group in batches
                .GroupBy(batch => batch.Node.Multimesh.Mesh)
                .OrderByDescending(group => group.Sum(batch => batch.Total))
                .Take(15)
        )
        {
            var first = group.First();
            GD.Print(
                $"BENCHMARK_MESH mesh={group.Key.GetType().Name}#{group.Key.GetInstanceId() % 10000} parent={first.Node.GetParent().Name} "
                    + $"batches={group.Count()} instances={group.Sum(b => (long)b.Instances)} mesh_triangles={first.MeshTriangles} "
                    + $"triangles_with_overlay={group.Sum(b => b.Total)} overlay={first.Node.MaterialOverlay != null} "
                    + $"range_end={first.Node.VisibilityRangeEnd} shadow={first.Node.CastShadow}"
            );
        }
    }

    /// <summary>
    /// Turns parts of the picture off to see what they cost, as in <c>--benchmark-off=shadows,grass</c>: shadows,
    /// msaa, glow, fog, grass, plants (trees, bushes, flowers), details (their small parts), village, wildlife, forest
    /// (the trees of the borders), sea (the sea and the border river), outland (the ground beyond the old valley), lighthouse
    /// (the tower, its beam and the boat by night).
    /// </summary>
    private void TurnOff(string[] parts)
    {
        foreach (string part in parts)
        {
            switch (part)
            {
                case "shadows":
                    foreach (var light in GetTree().Root.FindChildren("*", nameof(DirectionalLight3D), true, false))
                        ((DirectionalLight3D)light).ShadowEnabled = false;
                    break;
                case "msaa":
                    GetViewport().Msaa3D = Viewport.Msaa.Disabled;
                    break;
                case "glow" or "fog":
                    foreach (var node in GetTree().Root.FindChildren("*", nameof(WorldEnvironment), true, false))
                    {
                        var environment = ((WorldEnvironment)node).Environment;
                        if (part == "glow")
                            environment.GlowEnabled = false;
                        else
                            environment.FogEnabled = false;
                    }
                    break;
                case "grass":
                    // Hidden and no longer streamed in round the car.
                    world.Grass.Visible = false;
                    world.Grass.SetProcess(false);
                    break;
                case "plants":
                    world.Plants.Visible = false;
                    break;
                case "details":
                    // The small parts drawn only near the camera: bark marks, blossoms, sprigs, lily pads.
                    foreach (var node in world.Plants.GetChildren().OfType<MultiMeshInstance3D>())
                        if (node.VisibilityRangeEnd is > 0 and < 100)
                            node.Visible = false;
                    break;
                case "village":
                    world.Village.Visible = false;
                    break;
                case "wildlife":
                    world.Wildlife.Visible = false;
                    break;
                case "forest":
                    world.BorderTrees.Visible = false;
                    break;
                case "sea":
                    world.BorderWater.Visible = false;
                    break;
                case "outland":
                    foreach (var node in world.Ground.GetChildren().OfType<MeshInstance3D>())
                        if (node.Name.ToString().Contains("Outland"))
                            node.Visible = false;
                    break;
                case "lighthouse":
                    // The lantern's beam, its light and the boat by night, and the tower itself.
                    game.LighthouseShown = false;
                    if (world.Lighthouse != null)
                        world.Lighthouse.Visible = false;
                    if (world.NightBoat != null)
                        world.NightBoat.Visible = false;
                    break;
                case "outland-plants":
                    world.Plants.OutlandPlants.Visible = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown part to turn off: {part}");
            }
            GD.Print($"BENCHMARK_OFF {part}");
        }
    }

    private void DriveOutland(IEnumerable<Vector3[]> roads)
    {
        game.Weather.Begin(WeatherKind.Clear, instant: true);
        routes = roads.ToArray();
        route = 0;
        StartRoute();
    }

    private void Park()
    {
        var square = world.Village.Square;
        var spot = square.GlobalPosition + square.GlobalBasis * new Vector3(0, 0, 16);
        var toward = (square.GlobalPosition - spot) with { Y = 0 };
        ReleaseInput();
        car.SetPose(world.Ground.At(spot.X, spot.Z, .2f), Mathf.Atan2(-toward.X, -toward.Z));
    }

    private void Next()
    {
        if (stageIndex >= 0)
            Report(stages[stageIndex]);
        stageIndex++;
        if (stageIndex == stages.Count)
        {
            ReleaseInput();
            GD.Print(
                $"BENCHMARK_DONE seconds={(Time.GetTicksMsec() - startedMsec) / 1000.0:0.0} "
                    + $"adapter=\"{RenderingServer.GetVideoAdapterName()}\" api=\"{RenderingServer.GetVideoAdapterApiVersion()}\" "
                    + $"window={DisplayServer.WindowGetSize()} scale3d={GetViewport().Scaling3DScale} "
                    + $"distant_details={game.DistantDetails.Ranged}"
            );
            GetTree().Quit();
            SetProcess(false);
            SetPhysicsProcess(false);
            return;
        }
        var stage = stages[stageIndex];
        stage.Enter?.Invoke();
        if (stage.Drives && stageIndex > 0 && !stages[stageIndex - 1].Drives)
            StartRoute();
        stageTime = 0;
        samples = new Samples();
    }

    public override void _Process(double delta)
    {
        stageTime += delta;
        var stage = stages[stageIndex];
        if (stageTime > Warmup)
        {
            var viewport = GetViewport().GetViewportRid();
            samples.Frame.Add(delta * 1000);
            samples.Gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport));
            samples.Cpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport));
            samples.Draws.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame));
            samples.Primitives.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame));
            samples.Objects.Add(Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame));
            ReportHitch(delta, viewport);
        }
        if (stageTime > Warmup + stage.Seconds)
            Next();
    }

    private ulong lastPhysicsFrames;
    private TimeSpan lastGcPause;
    private readonly int[] lastGcCounts = new int[3];
    private int hitches;

    /// <summary>
    /// A frame that took more than twice the stage's usual time is printed with what happened in it: the garbage
    /// collector's pause and collections, the physics steps, the render times and where the car was.
    /// </summary>
    private void ReportHitch(double delta, Rid viewport)
    {
        ulong physics = Engine.GetPhysicsFrames();
        var pause = GC.GetTotalPauseDuration();
        var counts = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        double usual =
            samples.Frame.Count > 30 ? samples.Frame.Skip(samples.Frame.Count - 30).Order().ElementAt(15) : 0;
        if (usual > 0 && delta * 1000 > Math.Max(2 * usual, usual + 8) && hitches++ < 400)
            GD.Print(
                $"BENCHMARK_HITCH stage={stages[stageIndex].Name} t={stageTime:0.00} frame_ms={delta * 1000:0.0} usual_ms={usual:0.0} "
                    + $"gc_pause_ms={(pause - lastGcPause).TotalMilliseconds:0.0} gc={counts[0] - lastGcCounts[0]}/{counts[1] - lastGcCounts[1]}/{counts[2] - lastGcCounts[2]} "
                    + $"physics_steps={physics - lastPhysicsFrames} "
                    + $"process_ms={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} "
                    + $"physics_ms={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0} "
                    + $"gpu_ms={RenderingServer.ViewportGetMeasuredRenderTimeGpu(viewport):0.0} "
                    + $"cpu_render_ms={RenderingServer.ViewportGetMeasuredRenderTimeCpu(viewport):0.0} "
                    + $"objects={Performance.GetMonitor(Performance.Monitor.ObjectCount)} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)} "
                    + $"car={car.Position.X:0},{car.Position.Z:0} speed={car.Speed:0.0}"
            );
        lastPhysicsFrames = physics;
        lastGcPause = pause;
        counts.CopyTo(lastGcCounts, 0);
    }

    private void Report(Stage stage)
    {
        var frames = samples.Frame.OrderBy(x => x).ToArray();
        if (frames.Length == 0)
            return;
        double avg = frames.Average();
        double p99 = frames[(int)Math.Min(frames.Length - 1, frames.Length * .99)];
        double p50 = frames[frames.Length / 2];
        GD.Print(
            $"BENCHMARK stage={stage.Name} frames={frames.Length} fps_avg={1000 / avg:0.0} fps_median={1000 / p50:0.0} "
                + $"fps_1pct_low={1000 / p99:0.0} frame_ms_avg={avg:0.00} frame_ms_p99={p99:0.00} frame_ms_max={frames[^1]:0.00} "
                + $"gpu_ms_avg={samples.Gpu.Average():0.00} gpu_ms_max={samples.Gpu.Max():0.00} "
                + $"cpu_render_ms_avg={samples.Cpu.Average():0.00} "
                + $"process_ms={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.00} "
                + $"physics_ms={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.00} "
                + $"draws_avg={samples.Draws.Average():0} draws_max={samples.Draws.Max():0} "
                + $"primitives_avg={samples.Primitives.Average():0} primitives_max={samples.Primitives.Max():0} "
                + $"objects_avg={samples.Objects.Average():0} "
                + $"vram_mb={Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:0} "
                + $"texture_mb={Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed) / 1048576:0} "
                + $"buffer_mb={Performance.GetMonitor(Performance.Monitor.RenderBufferMemUsed) / 1048576:0} "
                + $"static_mem_mb={OS.GetStaticMemoryUsage() / 1048576.0:0} "
                + $"process_ram_mb={System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576:0} "
                + $"car_speed={car.Speed:0.0}"
        );
    }

    private void StartRoute()
    {
        using var curve = WorldPlan.CreateCurve(routes[route % routes.Length]);
        float length = curve.GetBakedLength();
        path.Clear();
        for (float offset = 1; offset < length - 1; offset += 1)
            path.Add(curve.SampleBaked(offset));
        var forward = (path[5] - path[0]).Normalized();
        var p = path[0];
        float height = world.Ground.Height(p.X, p.Z);
        if (River.OnBridge(p.X, p.Z))
            height = Mathf.Max(height, River.BridgeSurfaceHeight(p.X));
        car.SetPose(new Vector3(p.X, height + .15f, p.Z), Mathf.Atan2(-forward.X, -forward.Z));
        progress = stalled = 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count || !stages[stageIndex].Drives)
            return;
        var flatCar = new Vector2(car.Position.X, car.Position.Z);
        float best = float.MaxValue;
        int before = progress;
        for (int i = Mathf.Max(0, progress - 2); i < Mathf.Min(path.Count, progress + 24); i++)
        {
            float distance = flatCar.DistanceSquaredTo(new Vector2(path[i].X, path[i].Z));
            if (distance < best)
            {
                best = distance;
                progress = Math.Max(progress, i);
            }
        }
        stalled = progress > before ? 0 : stalled + 1;
        // A car that has left the road or is held up (by bandits, say) starts the next route.
        if (progress >= path.Count - 3 || best > 64 || stalled > 300)
        {
            route++;
            StartRoute();
            return;
        }
        // Drive as the road checks do: full throttle on a straight, slower in bends.
        int lookAhead = 6 + (int)(car.Speed * .5f);
        var target = path[Mathf.Min(path.Count - 1, progress + lookAhead)] - car.Position;
        float error = Mathf.AngleDifference(car.Heading, Mathf.Atan2(-target.X, -target.Z));
        float steer = Mathf.Clamp(-error * 2.5f, -1, 1);
        Input.ActionRelease("left");
        Input.ActionRelease("right");
        Input.ActionPress(steer < 0 ? "left" : "right", Mathf.Abs(steer));
        float bend = Mathf.Abs(error);
        Input.ActionPress(
            "accelerate",
            bend > .45f ? .25f
                : bend > .2f ? .55f
                : 1f
        );
    }

    private static void ReleaseInput()
    {
        foreach (string action in new[] { "left", "right", "accelerate" })
            Input.ActionRelease(action);
    }
}
