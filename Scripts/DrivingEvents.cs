using Godot;
using System;
using System.Linq;

namespace MathRacer;

// Decorations have no collision: the road stays usable during deliveries.
public partial class DrivingEvents : Node3D
{
    internal const int GateCount = 6;
    internal const float VillageClearance = 160;
    private readonly Curve3D[] courses = new Curve3D[2];
    internal readonly Label3D[] StartMarkers = new Label3D[2];
    internal (Vector3 Center, Vector3 Forward) Sample(int course, float distance)
    {
        var curve = courses[course];
        float length = curve.GetBakedLength();
        float offset = Mathf.Clamp(distance, 0, length);
        return (curve.SampleBaked(offset), (curve.SampleBaked(Mathf.Min(offset + .2f, length)) - curve.SampleBaked(Mathf.Max(offset - .2f, 0))).Normalized());
    }
    internal readonly float[] StartDistances = new float[2];
    internal readonly Vector3[,] Points = new Vector3[2, GateCount];
    private readonly Node3D[,] gates = new Node3D[2, GateCount];
    private Label3D nextMarker = null!;
    private readonly Label3D[] signs = new Label3D[2];
    internal int Active { get; private set; } = -1;
    internal int Next { get; private set; }
    internal int[] Wins { get; private set; } = new int[2];
    internal static string Title(int course) => course == 0 ? "Duhové brány" : "Senný slalom";

    internal void Generate(World world)
    {
        for (int course = 0; course < 2; course++)
        {
            var actors = world.Animals.Concat(world.Village.Residents).Concat(world.Village.Traders)
                .Append(world.Village.Farmer).Append(world.Village.Gardener).Append(world.Village.GemCollector).Append(world.Village.Toymaker).ToArray();
            // Use the outer countryside loops, with the entire course clear of the village.
            courses[course] = WorldPlan.CreateCurve(WorldLayout.Plan.Routes[course + 2]);
            float DistanceFromVillage(Vector3 p) => world.Village.BuildingFoundations
                .Min(b => new Vector2(p.X - b.Building.Position.X, p.Z - b.Building.Position.Z).Length());
            float Clearance(float start) => Enumerable.Range(0, 24)
                .Min(i => DistanceFromVillage(Sample(course, start + i * 4).Center));
            var candidates = Enumerable.Range(0, Math.Max(1, (int)((courses[course].GetBakedLength() - 120) / 8)))
                .Select(i => 16f + i * 8)
                .Where(d => Clearance(d) >= VillageClearance && actors.All(a => a.Position.DistanceTo(Sample(course, d).Center) >= 16))
                .OrderByDescending(Clearance).ToArray();
            if (candidates.Length == 0) throw new InvalidOperationException("No countryside event course outside village clearance.");
            StartDistances[course] = candidates[0];
            for (int i = 0; i < GateCount; i++)
            {
                var frame = Sample(course, StartDistances[course] + i * 17);
                var side = frame.Forward.Cross(Vector3.Up).Normalized();
                var p = frame.Center + side * (course == 1 && i > 0 ? (i % 2 == 0 ? -1 : 1) * .95f : 0);
                p.Y = world.Routes.SurfaceHeight(p, world.Ground.Height(p.X, p.Z));
                if (River.OnBridge(p.X, p.Z)) p.Y = Mathf.Max(p.Y, River.BridgeSurfaceHeight(p.X));
                Points[course, i] = p;
                var gate = new Node3D { Position = p, Rotation = new(0, Mathf.Atan2(side.Z, -side.X), 0) };
                AddChild(gate); gates[course, i] = gate;
                if (course == 0)
                {
                    string color = new[] { "ed776b", "f5b45b", "ffe58a", "70b765", "68cfee", "b69adb" }[i];
                    foreach (float x in new[] { -2.9f, 2.9f }) Art.Cylinder(gate, new(x, 2.5f, 0), .11f, .11f, 5, color);
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
                Art.Sign(gate, Vector3.Up * (course == 0 ? 5.7f : 3.2f), (i + 1).ToString(), 38);
            }
            StartMarkers[course] = Art.TaskMarker(this, Points[course, 0] + Vector3.Up * 7, "☺");
            signs[course] = Art.Sign(this, Points[course, 0] + Vector3.Up * 7,
                Title(course) + "\nZastav a stiskni tlačítko úkolu", 36);
        }
        nextMarker = Art.TaskMarker(this, Vector3.Zero, "!", true);
        Refresh();
    }

    internal int Nearby(Vector3 position)
    {
        for (int i = 0; i < 2; i++) if (position.DistanceTo(Points[i, 0]) < 6) return i;
        return -1;
    }
    internal void Begin(int course) { Active = course; Next = 0; Refresh(); }
    internal void Cancel() { Active = -1; Next = 0; Refresh(); }
    internal int Tick(Vector3 position)
    {
        if (Active < 0 || position.DistanceTo(Points[Active, Next]) > (Active == 0 ? 2.7f : 1.65f)) return -1;
        Next++;
        if (Next < GateCount) { Refresh(); return -1; }
        int finished = Active; Wins[finished] = Math.Min(Wins[finished] + 1, 9999); Cancel(); return finished;
    }
    internal void Restore(int active, int next, int[] wins)
    {
        Active = active; Next = next; Wins = (int[])wins.Clone(); Refresh();
    }
    private void Refresh()
    {
        nextMarker.Visible = Active >= 0 && Next > 0;
        if (Active >= 0) nextMarker.Position = Points[Active, Next] + Vector3.Up * 7;
        for (int course = 0; course < 2; course++)
        {
            signs[course].Text = Title(course) + (Wins[course] > 0 ? $" · Hotovo {Wins[course]}×" : "") + (Active == course ? "\nProjížděj brány v pořadí" : "\nZastav a stiskni tlačítko úkolu");
            for (int i = 0; i < GateCount; i++)
            {
                gates[course, i].Visible = Active < 0 || Active == course;
                gates[course, i].Scale = Vector3.One * (Active == course && i == Next ? 1.08f : 1);
                foreach (var child in gates[course, i].GetChildren())
                    if (child is Label3D label) label.Modulate = new Color(Active == course && i == Next ? "63e7e0" : "fff5d9");
            }
        }
    }
}
