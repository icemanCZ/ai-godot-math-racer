using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Garage;
using MathRacer.Graphics;
using MathRacer.Ui;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Tasks;

// Decorations have no collision: the road stays usable during deliveries.
// Every run is a race against a translucent ghost car. It drives at a gentle fixed pace until the player
// beats it; after that it replays the player's record as a list of gate times.
public partial class DrivingEvents : Node3D
{
    internal const int GateCount = 14;
    internal const float VillageClearance = 160;

    // Gate spacing in metres. A course shrinks only when a seed has no countryside stretch long enough.
    private static readonly float[] BaseSpacing = { 30, 22 };
    private static readonly float[] ShrinkSteps = { 1, .8f, .65f, .5f, .4f };

    // Metres per second of the pace ghost. The car tops out at 15, so a child can beat it with a clean run.
    private static readonly float[] PaceSpeed = { 10.5f, 9 };
    private readonly Curve3D[] courses = new Curve3D[2];
    internal readonly Label3D[] StartMarkers = new Label3D[2];
    private World world = null!;

    internal (Vector3 Center, Vector3 Forward) Sample(int course, float distance)
    {
        var curve = courses[course];
        float length = curve.GetBakedLength();
        float offset = Mathf.Clamp(distance, 0, length);
        return (
            curve.SampleBaked(offset),
            (
                curve.SampleBaked(Mathf.Min(offset + .2f, length)) - curve.SampleBaked(Mathf.Max(offset - .2f, 0))
            ).Normalized()
        );
    }

    internal readonly float[] StartDistances = new float[2];
    internal readonly float[] GateSpacing = new float[2];
    internal readonly Vector3[,] Points = new Vector3[2, GateCount];
    private readonly Node3D[,] gates = new Node3D[2, GateCount];
    private TaskMarker nextMarker = null!;
    private readonly Label3D[] signs = new Label3D[2];
    internal int Active { get; private set; } = -1;
    internal int Next { get; private set; }
    internal int[] Wins { get; private set; } = new int[2];

    internal static string Title(int course) => course == 0 ? "Duhové brány" : "Senný slalom";

    internal float CourseLength(int course) => (GateCount - 1) * GateSpacing[course];

    /// <summary>Seconds with one decimal and a decimal comma, for example "41,2".</summary>
    internal static string FormatTime(double seconds) => seconds.ToString("0.0").Replace('.', ',');

    internal void Generate(World world)
    {
        this.world = world;
        var actors = world
            .InteractionActors.Where(actor =>
                actor.Role is not (WitchRole or SorcererRole or MayorRole or GlassmakerRole)
            )
            .ToArray();
        for (int course = 0; course < 2; course++)
        {
            // Use the outer countryside loops, with the entire course clear of the village.
            courses[course] = WorldPlan.CreateCurve(WorldLayout.Plan.Routes[course + 2]);
            float routeLength = courses[course].GetBakedLength();
            float DistanceFromVillage(Vector3 p) =>
                world.Village.BuildingFoundations.Min(b =>
                    new Vector2(p.X - b.Building.Position.X, p.Z - b.Building.Position.Z).Length()
                );
            float Clearance(float start, float length) =>
                Enumerable
                    .Range(0, (int)(length / 4) + 1)
                    .Min(i => DistanceFromVillage(Sample(course, start + Math.Min(i * 4, length)).Center));
            bool ClearOfActors(float start, float length) =>
                Enumerable
                    .Range(0, (int)(length / 8) + 1)
                    .All(i =>
                    {
                        var p = Sample(course, start + Math.Min(i * 8, length)).Center;
                        return actors.All(a => a.Position.DistanceTo(p) >= 16);
                    });

            float[] candidates = Array.Empty<float>();
            foreach (float shrink in ShrinkSteps)
            {
                GateSpacing[course] = BaseSpacing[course] * shrink;
                float length = CourseLength(course);
                int count = (int)((routeLength - 24 - length) / 8) + 1;
                candidates = Enumerable
                    .Range(0, Math.Max(0, count))
                    .Select(i => 16f + i * 8)
                    .Where(d => Clearance(d, length) >= VillageClearance && ClearOfActors(d, length))
                    .OrderByDescending(d => Clearance(d, length))
                    .ToArray();
                if (candidates.Length > 0)
                    break;
            }
            if (candidates.Length == 0)
                throw new InvalidOperationException("No countryside event course outside village clearance.");
            StartDistances[course] = candidates[0];
            for (int i = 0; i < GateCount; i++)
            {
                var frame = Sample(course, StartDistances[course] + i * GateSpacing[course]);
                var side = frame.Forward.Cross(Vector3.Up).Normalized();
                var p = frame.Center + side * (course == 1 && i > 0 ? (i % 2 == 0 ? -1 : 1) * .95f : 0);
                p.Y = GroundHeight(p);
                Points[course, i] = p;
                var gate = new Node3D { Position = p, Rotation = new(0, Mathf.Atan2(side.Z, -side.X), 0) };
                AddChild(gate);
                gates[course, i] = gate;
                if (course == 0)
                {
                    string[] colors = { "ed776b", "f5b45b", "ffe58a", "70b765", "68cfee", "b69adb" };
                    string color = i == 0 || i == GateCount - 1 ? "ffffff" : colors[i % colors.Length];
                    foreach (float x in new[] { -2.9f, 2.9f })
                        Art.Cylinder(gate, new(x, 2.5f, 0), .11f, .11f, 5, color);
                    Art.Block(gate, new(0, 5, 0), new(6, .32f, .32f), color);
                }
                else
                {
                    // Bales sit outside the car's path, alternating the gap along the road.
                    foreach (float x in new[] { -2.25f, 2.25f })
                    {
                        Art.Block(gate, new(x, .45f, 0), new(.7f, .9f, 1.1f), "dbb869");
                        Art.Block(gate, new(x, .45f, .56f), new(.12f, .93f, .03f), "a7834f");
                    }
                }
                string label =
                    i == 0 ? "START"
                    : i == GateCount - 1 ? "CÍL"
                    : (i + 1).ToString();
                Art.Sign(gate, Vector3.Up * (course == 0 ? 5.7f : 3.2f), label, 38);
            }
            StartMarkers[course] = Art.TaskMarker(this, Points[course, 0] + Vector3.Up * 7, "");
            Art.RoleTaskIcon(StartMarkers[course], course == 0 ? "gates" : "slalom");
            signs[course] = Art.Sign(
                this,
                Points[course, 0] + Vector3.Up * 5.4f,
                Title(course) + "\nZastav a stiskni tlačítko úkolu",
                36
            );
            BuildGhostCourse(course);
        }
        nextMarker = Art.TaskMarker(this, Vector3.Zero, "!", true);
        BuildGhost();
        Refresh();
    }

    private float GroundHeight(Vector3 p)
    {
        float height = world.Routes.SurfaceHeight(p, world.Ground.Height(p.X, p.Z));
        return River.OnBridge(p.X, p.Z) ? Mathf.Max(height, River.BridgeSurfaceHeight(p.X)) : height;
    }

    // --- The ghost.

    private readonly Curve3D[] ghostPaths = new Curve3D[2];
    private readonly float[,] gateOffsets = new float[2, GateCount];
    private readonly float[][] paceSplits = new float[2][];
    private readonly List<float> splits = new();
    private Node3D ghost = null!;

    /// <summary>The gate times of the player's record for each course, or empty while the pace ghost leads.</summary>
    internal float[][] BestSplits { get; private set; } = EmptyRecords();

    /// <summary>Seconds since the car passed the start gate of the active run.</summary>
    internal double Elapsed { get; private set; }

    /// <summary>The time at which each gate of the active run was passed.</summary>
    internal IReadOnlyList<float> RunSplits => splits;

    /// <summary>Seconds ahead of (negative) or behind (positive) the ghost at the last gate, or NaN.</summary>
    internal float Gap { get; private set; } = float.NaN;

    /// <summary>The outcome of the run that <see cref="Tick"/> last finished.</summary>
    internal (float Time, float GhostTime, bool BeatGhost) Result { get; private set; }

    internal bool GhostVisible => ghost.Visible;

    private static float[][] EmptyRecords() => new[] { Array.Empty<float>(), Array.Empty<float>() };

    internal static bool ValidSplits(float[]? splits) =>
        splits is { Length: GateCount }
        && splits[0] >= 0
        && splits.All(t => float.IsFinite(t) && t < 3600)
        && Enumerable.Range(1, GateCount - 1).All(i => splits[i] >= splits[i - 1]);

    // The ghost follows a smooth line through the gates, so it weaves through the slalom like a driver would.
    private void BuildGhostCourse(int course)
    {
        var path = new Curve3D { BakeInterval = .25f };
        for (int i = 0; i < GateCount; i++)
        {
            var tangent = (Points[course, Math.Min(i + 1, GateCount - 1)] - Points[course, Math.Max(i - 1, 0)]) / 6;
            path.AddPoint(Points[course, i], -tangent, tangent);
        }
        ghostPaths[course] = path;
        for (int i = 0; i < GateCount; i++)
        {
            float offset = path.GetClosestOffset(Points[course, i]);
            gateOffsets[course, i] = i == 0 ? offset : Mathf.Max(offset, gateOffsets[course, i - 1] + .1f);
        }
        paceSplits[course] = Enumerable
            .Range(0, GateCount)
            .Select(i => (gateOffsets[course, i] - gateOffsets[course, 0]) / PaceSpeed[course])
            .ToArray();
    }

    private void BuildGhost()
    {
        ghost = new Node3D { Visible = false };
        AddChild(ghost);
        var model = new Node3D();
        ghost.AddChild(model);
        VehicleModel.BuildDisplay(model, new VehicleAppearance(), new List<Node3D>(), "classic");
        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(.55f, .85f, 1f, .5f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        foreach (var node in model.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var mesh = (MeshInstance3D)node;
            mesh.MaterialOverride = material;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        Art.Sign(ghost, Vector3.Up * 3.1f, "duch", 30);
    }

    /// <summary>The gate times the ghost on <paramref name="course"/> drives to.</summary>
    internal float[] GhostSplits(int course) =>
        ValidSplits(BestSplits[course]) ? BestSplits[course] : paceSplits[course];

    internal float GhostTime(int course) => GhostSplits(course)[GateCount - 1];

    /// <summary>The ghost's distance along its line after <paramref name="time"/> seconds of the run.</summary>
    private float GhostDistance(int course, double time)
    {
        var times = GhostSplits(course);
        if (time <= times[0])
            return gateOffsets[course, 0];
        for (int i = 1; i < GateCount; i++)
            if (time <= times[i])
            {
                float span = times[i] - times[i - 1];
                float fraction = span > 0 ? (float)((time - times[i - 1]) / span) : 1;
                return Mathf.Lerp(gateOffsets[course, i - 1], gateOffsets[course, i], fraction);
            }
        return gateOffsets[course, GateCount - 1];
    }

    internal (Vector3 Position, float Heading) GhostPose(int course, double time)
    {
        var path = ghostPaths[course];
        float length = path.GetBakedLength();
        float distance = Mathf.Clamp(GhostDistance(course, time), 0, length);
        var position = path.SampleBaked(distance);
        var direction =
            path.SampleBaked(Mathf.Min(distance + 1, length)) - path.SampleBaked(Mathf.Max(distance - 1, 0));
        position.Y = GroundHeight(position);
        return (position, Mathf.Atan2(-direction.X, -direction.Z));
    }

    private void UpdateGhost()
    {
        ghost.Visible = Active >= 0;
        if (Active < 0)
            return;
        var pose = GhostPose(Active, Elapsed);
        ghost.Position = pose.Position;
        ghost.Rotation = new(0, pose.Heading, 0);
    }

    // --- Running a course.

    internal int Nearby(Vector3 position)
    {
        for (int i = 0; i < 2; i++)
            if (position.DistanceTo(Points[i, 0]) < 6)
                return i;
        return -1;
    }

    internal void Begin(int course)
    {
        Active = course;
        Next = 0;
        ResetRun();
        Refresh();
    }

    internal void Cancel()
    {
        Active = -1;
        Next = 0;
        ResetRun();
        Refresh();
    }

    private void ResetRun()
    {
        Elapsed = 0;
        splits.Clear();
        Gap = float.NaN;
    }

    /// <summary>Runs the race clock, which starts when the car passes the start gate.</summary>
    internal void Advance(double delta)
    {
        if (Active < 0 || Next == 0)
            return;
        Elapsed += delta;
        UpdateGhost();
    }

    internal int Tick(Vector3 position)
    {
        if (Active < 0 || position.DistanceTo(Points[Active, Next]) > (Active == 0 ? 2.7f : 1.65f))
            return -1;
        float time = (float)Elapsed;
        splits.Add(time);
        Gap = time - GhostSplits(Active)[Next];
        Next++;
        if (Next < GateCount)
        {
            Refresh();
            return -1;
        }
        int finished = Active;
        Wins[finished] = Math.Min(Wins[finished] + 1, 9999);
        float ghostTime = GhostTime(finished);
        bool beat = time < ghostTime;
        Result = (time, ghostTime, beat);
        if (beat)
            BestSplits[finished] = splits.ToArray();
        Cancel();
        return finished;
    }

    internal void Restore(int active, int next, int[] wins, float[][] best, float elapsed, float[] runSplits)
    {
        Active = active;
        Next = next;
        Wins = (int[])wins.Clone();
        BestSplits = best.Select(b => (float[])b.Clone()).ToArray();
        ResetRun();
        Elapsed = elapsed;
        splits.AddRange(runSplits);
        if (active >= 0 && splits.Count > 0)
            Gap = splits[^1] - GhostSplits(active)[splits.Count - 1];
        Refresh();
    }

    private void Refresh()
    {
        nextMarker.Visible = Active >= 0 && Next > 0;
        if (Active >= 0)
            nextMarker.AnchorPosition = Points[Active, Next] + Vector3.Up * 7;
        for (int course = 0; course < 2; course++)
        {
            signs[course].Text =
                Title(course)
                + (Wins[course] > 0 ? $" · Hotovo {Wins[course]}×" : "")
                + (Active == course ? "\nProjížděj brány v pořadí" : "\nZastav a stiskni tlačítko úkolu")
                + $"\nDuch: {FormatTime(GhostTime(course))} s"
                + (ValidSplits(BestSplits[course]) ? " (tvůj rekord)" : "");
            for (int i = 0; i < GateCount; i++)
            {
                gates[course, i].Visible = Active < 0 || Active == course;
                gates[course, i].Scale = Vector3.One * (Active == course && i == Next ? 1.08f : 1);
                foreach (var child in gates[course, i].GetChildren())
                    if (child is Label3D label)
                        label.Modulate = new Color(Active == course && i == Next ? "63e7e0" : "fff5d9");
            }
        }
        UpdateGhost();
    }
}
