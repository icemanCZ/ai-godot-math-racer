using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer;

/// <summary>
/// Road navigation toward one target: a graph of connected road samples (including the bridge),
/// arrows anchored on the road ahead of the car and a final arrow toward an off-road target.
/// The arrows are added to the owner node so activities keep them with their other scenery.
/// </summary>
internal sealed class RouteGuide
{
    private readonly World world;
    private readonly Vehicle car;
    private readonly Node3D owner;
    private readonly Func<Vector3> destination;
    private readonly List<int>[] edges;
    private readonly int[] next;
    private readonly Dictionary<(int Anchor, bool Reverse), MeshInstance3D> routeArrows = new();
    private readonly List<MeshInstance3D> guidanceArrows = new();
    private Mesh arrowMesh = null!;
    private ShaderMaterial arrowMaterial = null!;
    private MeshInstance3D destinationArrow = null!;
    private float arrowPhase;
    private float refresh;
    private float destinationRoadDistance;
    private Vector3 roadExit;
    private Vector3 roadWaypoint;
    private bool offRoad;
    private bool showRoadExit;
    private bool created;

    public Vector3 Destination => destination();
    public Vector3 Waypoint => offRoad ? Destination : roadWaypoint;
    public int RouteLength { get; private set; }

    public RouteGuide(World world, Vehicle car, Node3D owner, Func<Vector3> destination)
    {
        this.world = world;
        this.car = car;
        this.owner = owner;
        this.destination = destination;
        var samples = world.Routes.Samples;
        edges = new List<int>[samples.Count];
        for (int i = 0; i < edges.Length; i++)
            edges[i] = new List<int>();
        next = new int[samples.Count];
        Array.Fill(next, -1);
        var buckets = new Dictionary<Vector2I, List<int>>();
        for (int i = 0; i < samples.Count; i++)
        {
            var p = samples[i];
            var key = new Vector2I(Mathf.FloorToInt(p.X / 4), Mathf.FloorToInt(p.Z / 4));
            for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
                if (buckets.TryGetValue(key + new Vector2I(x, z), out var neighbors))
                    foreach (int j in neighbors)
                        if (p.DistanceSquaredTo(samples[j]) < 10.24f)
                        {
                            edges[i].Add(j);
                            edges[j].Add(i);
                        }
            if (!buckets.TryGetValue(key, out var bucket))
                buckets[key] = bucket = new();
            bucket.Add(i);
        }
    }

    /// <summary>
    /// Plans the road part of the route. <paramref name="roadApproach"/> is the road end of a cleared
    /// off-road approach; without it the route ends at the road sample closest to the destination.
    /// </summary>
    public void Retarget(Vector3? roadApproach = null)
    {
        var samples = world.Routes.Samples;
        var target = Destination;
        destinationRoadDistance = world.Routes.Distance(target);
        Array.Fill(next, -1);
        int routeTarget = Nearest(roadApproach ?? target);
        roadExit = samples[routeTarget];
        roadExit.Y = ArrowRoadHeight(roadExit);
        var costs = new float[samples.Count];
        Array.Fill(costs, float.PositiveInfinity);
        var queue = new PriorityQueue<int, float>();
        costs[routeTarget] = 0;
        queue.Enqueue(routeTarget, 0);
        while (queue.TryDequeue(out int i, out float cost))
        {
            if (cost > costs[i])
                continue;
            foreach (int j in edges[i])
            {
                float candidate = cost + samples[i].DistanceTo(samples[j]);
                if (candidate >= costs[j])
                    continue;
                costs[j] = candidate;
                next[j] = i;
                queue.Enqueue(j, candidate);
            }
        }
        foreach (float cost in costs)
            if (float.IsPositiveInfinity(cost))
                throw new InvalidOperationException("Delivery road navigation is disconnected.");
        refresh = 0;
    }

    /// <summary>Creates the arrow meshes. Call once the owner is in the scene tree.</summary>
    public void CreateArrows()
    {
        if (created)
            return;
        created = true;
        var arrowBuilder = new SurfaceTool();
        arrowBuilder.Begin(Mesh.PrimitiveType.Triangles);
        // A solid arrow points along local -Z, with lit sides that make its depth visible.
        var outline = new Vector3[]
        {
            new(0, 0, -1.2f),
            new(.72f, 0, -.35f),
            new(.28f, 0, -.35f),
            new(.28f, 0, .85f),
            new(-.28f, 0, .85f),
            new(-.28f, 0, -.35f),
            new(-.72f, 0, -.35f),
        };
        const float halfThickness = .18f;
        var cap = new[] { 0, 1, 2, 0, 2, 5, 0, 5, 6, 2, 3, 4, 2, 4, 5 };
        arrowBuilder.SetNormal(Vector3.Up);
        foreach (int vertex in cap)
            arrowBuilder.AddVertex(outline[vertex] + Vector3.Up * halfThickness);
        arrowBuilder.SetNormal(Vector3.Down);
        for (int i = cap.Length - 1; i >= 0; i--)
            arrowBuilder.AddVertex(outline[cap[i]] + Vector3.Down * halfThickness);
        for (int i = 0; i < outline.Length; i++)
        {
            var a = outline[i];
            var b = outline[(i + 1) % outline.Length];
            var edge = b - a;
            arrowBuilder.SetNormal(new Vector3(edge.Z, 0, -edge.X).Normalized());
            var topA = a + Vector3.Up * halfThickness;
            var bottomA = a + Vector3.Down * halfThickness;
            var topB = b + Vector3.Up * halfThickness;
            var bottomB = b + Vector3.Down * halfThickness;
            foreach (var vertex in new[] { topA, bottomA, bottomB, topA, bottomB, topB })
                arrowBuilder.AddVertex(vertex);
        }
        arrowMesh = arrowBuilder.Commit();
        arrowMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/RouteArrow.gdshader") };
        destinationArrow = new MeshInstance3D
        {
            Name = "DestinationArrow",
            Mesh = arrowMesh,
            MaterialOverride = arrowMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
        };
        owner.AddChild(destinationArrow);
        UpdateGuidance();
        UpdateArrowVisibility();
    }

    public void Tick(double delta)
    {
        // Move the rendered mesh while keeping the road anchors fixed; pause freezes the phase.
        arrowPhase = (arrowPhase + (float)delta * 2.1f) % Mathf.Tau;
        arrowMaterial.SetShaderParameter("bob_phase", arrowPhase);
        refresh -= (float)delta;
        if (refresh <= 0)
        {
            refresh = .25f;
            UpdateGuidance();
        }
        UpdateArrowVisibility();
    }

    public void HideAll()
    {
        foreach (var arrow in guidanceArrows)
            arrow.Hide();
        if (created)
            destinationArrow.Hide();
    }

    private int Nearest(Vector3 p) => NearestSample(world, p);

    /// <summary>Index of the road sample closest to <paramref name="p"/>.</summary>
    internal static int NearestSample(World world, Vector3 p)
    {
        int best = 0;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < world.Routes.Samples.Count; i++)
        {
            float d = world.Routes.Samples[i].DistanceSquaredTo(p);
            if (d < distance)
            {
                best = i;
                distance = d;
            }
        }
        return best;
    }

    private void UpdateArrowVisibility()
    {
        offRoad = !OnRoad(car.Position);
        var forward = -car.Transform.Basis.Z;
        foreach (var arrow in guidanceArrows)
        {
            var offset = arrow.Position - car.Position;
            offset.Y = 0;
            arrow.Visible = !offRoad && offset.Dot(forward) > 0 && offset.LengthSquared() >= 16f;
        }
        var anchor = offRoad ? car.Position : roadExit;
        var direction = Destination - anchor;
        direction.Y = 0;
        destinationArrow.Visible =
            direction.LengthSquared() > .5f && (offRoad ? direction.Length() <= destinationRoadDistance : showRoadExit);
        if (!destinationArrow.Visible)
            return;
        destinationArrow.Position = anchor + Vector3.Up * (offRoad ? 4.2f : 1.25f);
        destinationArrow.Rotation = new Vector3(0, Mathf.Atan2(-direction.X, -direction.Z), 0);
    }

    private bool OnRoad(Vector3 point) => world.Routes.Contains(point) || River.OnBridge(point.X, point.Z);

    private float ArrowRoadHeight(Vector3 point)
    {
        float surface = world.Routes.SurfaceHeight(point, world.Ground.Height(point.X, point.Z));
        return River.OnBridge(point.X, point.Z) ? Mathf.Max(surface, River.BridgeSurfaceHeight(point.X)) : surface;
    }

    private void UpdateGuidance()
    {
        var points = new List<Vector3>();
        var nodes = new List<int>();
        int node = Nearest(car.Position);
        RouteLength = 0;
        while (node >= 0 && RouteLength++ < next.Length)
        {
            nodes.Add(node);
            points.Add(world.Routes.Samples[node]);
            node = next[node];
        }
        var target = Destination;
        points.Add(target);
        // Aim ahead along the road instead of across the river or through trees.
        roadWaypoint = points[Math.Min(8, points.Count - 1)];
        showRoadExit = nodes.Count <= 8 && !OnRoad(target);
        foreach (var arrow in guidanceArrows)
            arrow.Hide();
        guidanceArrows.Clear();
        var shownAnchors = new HashSet<int>();
        for (int sample = 0; sample < Math.Min(54, nodes.Count - 1); sample++)
        {
            int anchorIndex = world.Routes.SampleAnchors[nodes[sample]];
            if (!shownAnchors.Add(anchorIndex))
                continue;
            var anchor = world.Routes.NavigationAnchors[anchorIndex];
            bool reverse = anchor.Forward.Dot(points[sample + 1] - points[sample]) < 0;
            var key = (anchorIndex, reverse);
            if (!routeArrows.TryGetValue(key, out var arrow))
            {
                var p = anchor.Position;
                var forward = reverse ? -anchor.Forward : anchor.Forward;
                if (forward.LengthSquared() < .001f)
                    continue;
                arrow = new MeshInstance3D
                {
                    Name = "RouteArrow" + routeArrows.Count,
                    Mesh = arrowMesh,
                    MaterialOverride = arrowMaterial,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(p.X, ArrowRoadHeight(p) + 1.25f, p.Z),
                    Rotation = new Vector3(0, Mathf.Atan2(-forward.X, -forward.Z), 0),
                };
                routeArrows.Add(key, arrow);
                owner.AddChild(arrow);
            }
            guidanceArrows.Add(arrow);
        }
    }
}
