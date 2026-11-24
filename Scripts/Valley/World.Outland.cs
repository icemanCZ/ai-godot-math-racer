using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Graphics;

namespace MathRacer.Valley;

/// <summary>
/// The roads of the land between the old valley and its borders (<c>docs/MAP_EXPANSION_PLAN.md</c>, phase 4): the ring
/// round the old valley (<see cref="OutlandLayout"/>) with its two bridges, the roads that join it to the old roads
/// through ground the old valley leaves free, and a road out to each of the four borders. They are laid once the old
/// valley stands, with random numbers of their own, and nothing of the old valley moves for them.
/// </summary>
public partial class World
{
    /// <summary>The roads beyond the old valley, a network of their own beside <see cref="Routes"/>.</summary>
    internal Roads OutlandRoutes { get; private set; } = new() { Name = "OutlandRoads" };

    /// <summary>The routes of <see cref="OutlandRoutes"/>: the ring first, then the joins, then the roads to the borders.</summary>
    internal Vector3[][] OutlandRoutePoints { get; private set; } = Array.Empty<Vector3[]>();

    /// <summary>How many of <see cref="OutlandRoutePoints"/> after the ring join it to the old roads.</summary>
    internal int OutlandJoinCount { get; private set; }

    /// <summary>
    /// The roads to the borders, after the joins in <see cref="OutlandRoutePoints"/>, with the border each leads to: the
    /// last point of each is where the road ends, 42 m inside the edge of the valley.
    /// </summary>
    internal (Border Border, Vector3[] Points)[] BorderRoads { get; private set; } = Array.Empty<(Border, Vector3[])>();

    /// <summary>A join leaves the old roads only where nothing stands within this distance of its middle.</summary>
    private const float JoinClearance = 7;

    /// <summary>A road to a border ends this far inside the edge of the valley.</summary>
    private const float BorderRoadEnd = 42;

    /// <summary>
    /// Lays out the roads beyond the old valley and grades them into the ground, which is worked out next; the bridges
    /// are built at once, the roads themselves only on the final ground (<see cref="BuildOutlandRoads"/>).
    /// </summary>
    private void PlanOutlandRoads()
    {
        var layout = WorldLayout.Plan.Outland;
        var random = new Random(Settings.Seed + 7741);
        var ring = layout.Ring();
        ring.RemoveAt(ring.Count - 1);
        var joins = PlanJoins(ring);
        var borderRoads = PlanBorderRoads(ring, random);
        // Each road that meets the ring ends on a point of it: a join with its last point, a road to a border with its first.
        foreach (var road in joins)
            InsertOnRing(ring, road[^1]);
        foreach (var (_, road) in borderRoads)
            InsertOnRing(ring, road[0]);
        // The ring starts at a meeting, so that its closing point is a junction that really is one.
        var start = joins.Select(road => road[^1]).Concat(borderRoads.Select(road => road.Points[0])).FirstOrDefault();
        int startIndex = ring.IndexOf(start);
        if (startIndex > 0)
            ring = ring.Skip(startIndex).Concat(ring.Take(startIndex)).ToList();
        ring.Add(ring[0]);
        OutlandJoinCount = joins.Count;
        BorderRoads = borderRoads.ToArray();
        OutlandRoutePoints = new[] { ring.ToArray() }
            .Concat(joins)
            .Concat(borderRoads.Select(road => road.Points))
            .ToArray();
        Ground.GradeOutlandRoads(new OutlandRoadbed(OutlandRoutePoints, Ground.Landscape, layout.Bridges));
        foreach (var bridge in layout.Bridges)
            BuildOutlandBridge(bridge);
        GD.Print($"OUTLAND_ROADS joins={joins.Count} borders={borderRoads.Count} ring={ring.Count}");
    }

    /// <summary>Whether a plant beyond the old valley would stand on a bridge of the ring or its ramps.</summary>
    private static bool OutlandBlocked(Vector3 p) => WorldLayout.Plan.Outland.BridgeAt(p.X, p.Z, 8) != null;

    /// <summary>Builds the roads beyond the old valley on the final ground, with their verges.</summary>
    private void BuildOutlandRoads()
    {
        AddChild(OutlandRoutes);
        OutlandRoutes.GenerateOutland(Ground, OutlandRoutePoints, WorldLayout.Plan.Outland.Bridges);
        FarParts.Add("outland verges", OutlandRoutes.BuildVerges(Ground, null));
    }

    /// <summary>
    /// Roads from the old roads out to the ring, straight out from the middle: at most four, well apart, each from a
    /// point of an old road far out, through ground where nothing of the old valley stands and over no water.
    /// </summary>
    private List<Vector3[]> PlanJoins(List<Vector3> ring)
    {
        var occupied = OccupiedSpots();
        var joins = new List<Vector3[]>();
        var angles = new List<float>();
        var candidates = new List<Vector3>();
        for (int i = 0; i < Routes.Samples.Count; i += 3)
        {
            var sample = Routes.Samples[i];
            float radius = FlatMath.Length(sample.X, sample.Z);
            if (radius > 260)
                candidates.Add(sample);
        }
        foreach (var start in candidates.OrderByDescending(p => FlatMath.Length(p.X, p.Z)))
        {
            if (joins.Count == 4)
                break;
            float angle = Mathf.Atan2(start.Z, start.X);
            if (angles.Any(other => Mathf.Abs(Mathf.AngleDifference(angle, other)) < Mathf.DegToRad(50)))
                continue;
            // Straight out from the middle if the way is free, else a little to either side.
            foreach (float turn in new[] { 0f, 15, -15, 30, -30 })
            {
                var direction = new Vector3(start.X, 0, start.Z).Normalized().Rotated(Vector3.Up, Mathf.DegToRad(turn));
                if (RingHit(ring, start, direction) is not Vector3 end)
                    continue;
                if (WorldLayout.Plan.Outland.Bridges.Any(b => FlatDistance(end, new Vector3(b.CenterX, 0, b.Z)) < 110))
                    continue;
                if (joins.Any(other => FlatDistance(other[^1], end) < 150) || !JoinIsClear(start, end, occupied))
                    continue;
                joins.Add(JoinPoints(start, end));
                angles.Add(angle);
                break;
            }
        }
        return joins;
    }

    /// <summary>The points of a join: straight within the old valley, then gently bending out to the ring.</summary>
    private static Vector3[] JoinPoints(Vector3 start, Vector3 end)
    {
        var flatStart = new Vector3(start.X, 0, start.Z);
        var points = new List<Vector3> { flatStart };
        var direction = (end - flatStart).Normalized();
        var side = direction.Cross(Vector3.Up);
        float length = FlatDistance(flatStart, end);
        float leave = Mathf.Max(0, WorldTuning.CoreRadius + 20 - FlatMath.Length(start.X, start.Z));
        for (float along = Mathf.Min(leave, length - 40); along < length - 30; along += 45)
        {
            if (along <= 0)
                continue;
            float bend = along <= leave ? 0 : 9 * Mathf.Sin((along - leave) * .02f);
            points.Add(flatStart + direction * along + side * bend);
        }
        points.Add(end);
        return points.ToArray();
    }

    /// <summary>Whether a join from an old road to the ring keeps clear of everything the old valley has and of water.</summary>
    private bool JoinIsClear(Vector3 start, Vector3 end, List<(Vector2 Low, Vector2 High)> occupied)
    {
        var direction = (end - start) with { Y = 0 };
        float length = direction.Length();
        direction /= length;
        int riverSide = Math.Sign(start.X - River.CenterX(start.Z));
        for (float along = 6; along < length; along += 3)
        {
            var p = start + direction * along;
            if (
                River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 16
                || Math.Sign(p.X - River.CenterX(p.Z)) != riverSide
                || (along > 16 && Routes.Distance(p) < 10)
            )
                return false;
            if (Mathf.Max(Mathf.Abs(p.X), Mathf.Abs(p.Z)) > WorldTuning.CoreRadius + 10)
                continue;
            var flat = new Vector2(p.X, p.Z);
            if (
                Plants.NearTrunk(p, JoinClearance)
                || Village.IsNearVillage(p)
                || Village.IsReserved(p)
                || Tracks?.Contains(p) == true
                || InGlassworksZone(p)
                || InFarmZone(p)
                || InBakeryZone(p)
                || InPlaygroundZone(p)
                || InZooZone(p)
                || InHunterZone(p)
                || occupied.Any(box =>
                    flat.X > box.Low.X - JoinClearance
                    && flat.X < box.High.X + JoinClearance
                    && flat.Y > box.Low.Y - JoinClearance
                    && flat.Y < box.High.Y + JoinClearance
                )
            )
                return false;
        }
        return true;
    }

    /// <summary>
    /// The ground every made thing of the old valley covers, far enough out to matter for a join: the obstacles the car
    /// hits, the characters, and the bounds of every piece of every building, sign, fence and toy. The ground, the
    /// roads, the water, the plants and other things that cover the whole valley are left out (the plants' trunks are
    /// asked of <see cref="Vegetation.NearTrunk"/>).
    /// </summary>
    private List<(Vector2 Low, Vector2 High)> OccupiedSpots()
    {
        var spots = new List<(Vector2, Vector2)>();
        void AddBox(Aabb box)
        {
            if (box.Size.X > 40 || box.Size.Z > 40)
                return;
            var low = new Vector2(box.Position.X, box.Position.Z);
            var high = new Vector2(box.End.X, box.End.Z);
            if (
                Mathf.Max(
                    Mathf.Max(Mathf.Abs(low.X), Mathf.Abs(high.X)),
                    Mathf.Max(Mathf.Abs(low.Y), Mathf.Abs(high.Y))
                ) < 250
            )
                return;
            spots.Add((low, high));
        }
        foreach (var child in GetChildren())
        {
            if (
                child
                is Terrain
                    or Roads
                    or Vegetation
                    or Valley.Grass
                    or Valley.Wildlife
                    or Valley.Atmosphere
                    or River
                    or Valley.Tracks
                    or Valley.BorderWater
                    or Valley.BorderForest
                    or FireflyField
                    or HuntField
            )
                continue;
            // A mesh by the bounds of its mesh, anything else (instanced batches, texts, particles) by where it stands:
            // their bounds come from the renderer, and a check run has none, so a valley would be planned otherwise there.
            if (child is Node3D node)
                foreach (var geometry in node.FindChildren("*", nameof(GeometryInstance3D), true, false).Prepend(node))
                    if (geometry is MeshInstance3D { Mesh: { } mesh } instance)
                        AddBox(instance.GlobalTransform * mesh.GetAabb());
                    else if (geometry is GeometryInstance3D other)
                        AddBox(new Aabb(other.GlobalPosition, Vector3.Zero));
        }
        foreach (var body in FindChildren("*", nameof(StaticBody3D), true, false).OfType<StaticBody3D>())
            if (body.IsInGroup("solid_obstacles") && body.GetParent() is not Vegetation)
                AddBox(new Aabb(body.GlobalPosition - new Vector3(1.5f, 0, 1.5f), new Vector3(3, 1, 3)));
        foreach (var actor in actors.Values)
            AddBox(new Aabb(actor.GlobalPosition - new Vector3(1.5f, 0, 1.5f), new Vector3(3, 2, 3)));
        return spots;
    }

    /// <summary>
    /// A road out to each border: to the foot of the mountains, down to the beach, to the bank of the border river and
    /// to the edge of the forest. Each leaves the ring where a straight line out meets it, away from the valley's river.
    /// </summary>
    private List<(Border Border, Vector3[] Points)> PlanBorderRoads(List<Vector3> ring, Random random)
    {
        var plan = WorldLayout.Plan;
        var borders = plan.Borders;
        float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
        float edge = ValleyBounds.HalfSize - BorderRoadEnd;
        // North and south they run on the side of the middle away from the river; east and west anywhere along.
        float across = -plan.Side * Range(180, 260);
        float southEnd = Mathf.Min(edge, borders.Coast(across) - 30);
        float along = Range(-260, 260);
        var targets = new[]
        {
            (Border.Mountains, From: new Vector3(across, 0, 0), To: new Vector3(across + Range(-25, 25), 0, -edge)),
            (Border.Sea, From: new Vector3(across, 0, 0), To: new Vector3(across + Range(-25, 25), 0, southEnd)),
            (
                Border.River,
                From: new Vector3(0, 0, along),
                To: new Vector3(plan.Side * edge, 0, along + Range(-25, 25))
            ),
            (
                Border.Forest,
                From: new Vector3(0, 0, -along),
                To: new Vector3(-plan.Side * edge, 0, -along + Range(-25, 25))
            ),
        };
        var roads = new List<(Border, Vector3[])>();
        foreach (var (border, from, to) in targets)
        {
            var direction = (to - from).Normalized();
            if (RingHit(ring, from, direction) is not Vector3 start)
                continue;
            var middle = start.Lerp(to, .5f) + direction.Cross(Vector3.Up) * Range(-18, 18);
            roads.Add((border, new[] { start, middle, to }));
        }
        return roads;
    }

    /// <summary>Where a ray from <paramref name="from"/> along <paramref name="direction"/> first meets the ring.</summary>
    private static Vector3? RingHit(List<Vector3> ring, Vector3 from, Vector3 direction)
    {
        Vector3? best = null;
        float nearest = float.MaxValue;
        var origin = new Vector2(from.X, from.Z);
        var ray = new Vector2(direction.X, direction.Z);
        for (int i = 0; i < ring.Count; i++)
        {
            var a = new Vector2(ring[i].X, ring[i].Z);
            var b = new Vector2(ring[(i + 1) % ring.Count].X, ring[(i + 1) % ring.Count].Z);
            var edge = b - a;
            float denominator = ray.Cross(edge);
            if (Mathf.Abs(denominator) < .0001f)
                continue;
            float distance = (a - origin).Cross(edge) / denominator;
            float t = (a - origin).Cross(ray) / denominator;
            if (distance <= 0 || t < 0 || t > 1 || distance >= nearest)
                continue;
            nearest = distance;
            var hit = a + edge * t;
            best = new Vector3(hit.X, 0, hit.Y);
        }
        return best;
    }

    /// <summary>The ring keeps no point of its own this near a point where a road meets it.</summary>
    private const float RingPointGap = 15;

    /// <summary>
    /// Puts <paramref name="point"/> into the ring between the two points it lies between, and leaves out the points of
    /// the ring near it: a curve through two points close together, among points far apart, would loop. The straight
    /// pieces at the bridges stay as they are.
    /// </summary>
    private static void InsertOnRing(List<Vector3> ring, Vector3 point)
    {
        if (ring.Contains(point))
            return;
        ring.RemoveAll(other =>
            FlatDistance(other, point) < RingPointGap && WorldLayout.Plan.Outland.BridgeAt(other.X, other.Z, 30) == null
        );
        int best = 0;
        float nearest = float.MaxValue;
        for (int i = 0; i < ring.Count; i++)
        {
            float distance = SegmentDistance(point, ring[i], ring[(i + 1) % ring.Count]);
            if (distance < nearest)
            {
                nearest = distance;
                best = i;
            }
        }
        ring.Insert(best + 1, point);
    }

    /// <summary>A wooden bridge of the ring over the river, like the old one, with short ramps down to the banks.</summary>
    private void BuildOutlandBridge(OutlandLayout.Bridge bridge)
    {
        var node = new Node3D
        {
            Name = "OutlandBridge",
            Position = new Vector3(bridge.CenterX, bridge.Top - .11f, bridge.Z),
        };
        AddChild(node);
        float length = bridge.HalfLength * 2,
            width = OutlandLayout.Bridge.HalfWidth * 2;
        int planks = Mathf.RoundToInt(length);
        for (int i = 0; i < planks; i++)
            Art.Block(
                node,
                new(-length / 2 + (i + .5f) * length / planks, 0, 0),
                new(length / planks - .03f, .22f, width),
                "c39770"
            );
        float drop = River.OutlandDeckRise - River.OutlandBankRise;
        foreach (float end in new[] { -1f, 1f })
        {
            float angle = -end * Mathf.Atan(drop / 3);
            float cosine = Mathf.Cos(angle);
            var ramp = new StaticBody3D
            {
                Position = new Vector3(end * (bridge.HalfLength + 1.5f), .11f - drop / 2 - .11f * cosine, 0),
                Rotation = new Vector3(0, 0, angle),
            };
            node.AddChild(ramp);
            ramp.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(3 / cosine, .22f, width) } });
            Art.Block(ramp, Vector3.Zero, new(3 / cosine, .22f, width), "b58b66");
        }
        foreach (float side in new[] { -OutlandLayout.Bridge.HalfWidth + .1f, OutlandLayout.Bridge.HalfWidth - .1f })
        {
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 4.5f) + 1);
            for (int i = 0; i < posts; i++)
                Art.Cylinder(
                    node,
                    new(-length / 2 + .5f + i * (length - 1) / (posts - 1), .65f, side),
                    .14f,
                    .12f,
                    1.5f,
                    "a77a59"
                );
            Art.Beam(node, new(-length / 2 + .5f, 1.2f, side), new(length / 2 - .5f, 1.2f, side), .16f, "e2bb8b");
            Art.Beam(node, new(-length / 2 + .5f, .55f, side), new(length / 2 - .5f, .55f, side), .12f, "e2bb8b");
        }
        var body = new StaticBody3D();
        node.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(length, .22f, width) } });
        foreach (float side in new[] { -OutlandLayout.Bridge.HalfWidth + .1f, OutlandLayout.Bridge.HalfWidth - .1f })
            body.AddChild(
                new CollisionShape3D
                {
                    Position = new(0, .65f, side),
                    Shape = new BoxShape3D { Size = new(length, 1.3f, .25f) },
                }
            );
    }
}
