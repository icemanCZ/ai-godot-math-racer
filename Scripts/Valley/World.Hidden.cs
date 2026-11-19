using System;
using System.Collections.Generic;
using Godot;
using MathRacer.App;

namespace MathRacer.Valley;

/// <summary>Where the small friends hide: beside the roads, away from everything that was built.</summary>
public partial class World
{
    /// <summary>The ladybugs, hedgehogs and frogs who hide in the valley.</summary>
    internal HiddenFriends Hidden { get; } = new();

    /// <summary>
    /// Places the friends last, after the plants, with a random generator of their own, so that nothing placed before
    /// them moves. They are spread out (the spacing shrinks only when a valley is short of room) and take the kinds in
    /// turn, which gives every kind the same number.
    /// </summary>
    private void CreateHiddenFriends()
    {
        AddChild(Hidden);
        var random = new Random(Settings.Seed + 5151);
        int count = Routes.Samples.Count - 1;
        var order = new int[count];
        for (int i = 0; i < count; i++)
            order[i] = i;
        for (int i = count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
        var spots = new List<(Vector3 Position, float Yaw)>();
        foreach (float spacing in new[] { 80f, 55f, 35f, 20f, 10f })
        {
            foreach (int index in order)
            {
                if (spots.Count == HiddenFriends.Total)
                    break;
                var road = Routes.Samples[index];
                var tangent = Routes.Samples[index + 1] - road;
                if (
                    tangent.Length() < WorldTuning.FriendMinRoadStep
                    || tangent.Length() > WorldTuning.FriendMaxRoadStep
                )
                    continue;
                var side = tangent.Normalized().Cross(Vector3.Up);
                float roadHeight = Routes.SurfaceHeight(road, Ground.Height(road.X, road.Z));
                int first = random.Next(2) == 0 ? 1 : -1;
                float offset = 6.3f + (float)random.NextDouble() * 1.4f;
                foreach (int sign in new[] { first, -first })
                {
                    var verge = road + side * sign * offset;
                    var position = Ground.At(verge.X, verge.Z);
                    if (Mathf.Abs(position.Y - roadHeight) > 2.2f || !IsHiddenSpotFree(position, spots, spacing))
                        continue;
                    var facing = road - verge;
                    spots.Add((position, Mathf.Atan2(facing.X, facing.Z)));
                    break;
                }
            }
            if (spots.Count == HiddenFriends.Total)
                break;
        }
        if (spots.Count < HiddenFriends.Total)
            throw new InvalidOperationException($"Only {spots.Count} hiding places were found for the small friends.");
        for (int i = 0; i < spots.Count; i++)
            Hidden.Add((HiddenKind)(i % HiddenFriends.KindCount), spots[i].Position, spots[i].Yaw);
    }

    private bool IsHiddenSpotFree(Vector3 p, List<(Vector3 Position, float Yaw)> others, float spacing)
    {
        if (
            new Vector2(p.X, p.Z).Length() > Settings.PlayableRadius - 14
            || Village.IsNearVillage(p)
            || Village.IsReserved(p)
            || Routes.Distance(p) < 5.2f
            || Plants.NearTrunk(p, 1.8f)
            || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 6
            || IsPickupApproachReserved(p)
            || IsHaulReserved(p)
            || IsHaulApproachReserved(p)
            || InGlassworksZone(p)
            || InFarmZone(p)
            || InBakeryZone(p)
            || InPlaygroundZone(p)
            || FlatDistance(p, Tower.Position) < 18
        )
            return false;
        foreach (var actor in actors.Values)
            if (FlatDistance(p, actor.Position) < 12)
                return false;
        foreach (var project in Projects)
            if (FlatDistance(p, project.Focus) < 22)
                return false;
        foreach (var (other, _) in others)
            if (FlatDistance(p, other) < spacing)
                return false;
        return true;
    }
}
