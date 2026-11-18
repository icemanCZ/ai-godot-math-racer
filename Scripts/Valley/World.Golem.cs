using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Valley;

public partial class World
{
    private void PlaceSorcerer()
    {
        // Pick a high road verge with enough room for a tower and a reachable visitor.
        float best = float.NegativeInfinity;
        Vector3 chosen = Vector3.Zero,
            facing = Vector3.Forward,
            tower = Vector3.Zero;
        for (int i = 0; i < Routes.Samples.Count - 1; i += 3)
        {
            var road = Routes.Samples[i];
            var tangent = Routes.Samples[i + 1] - road;
            tangent.Y = 0;
            if (tangent.LengthSquared() < .1f || tangent.LengthSquared() > 9)
                continue;
            var side = tangent.Normalized().Cross(Vector3.Up);
            foreach (int sign in new[] { -1, 1 })
            {
                var visitor = road + side * (6 * sign);
                var building = road + side * (17 * sign);
                visitor.Y = Ground.Height(visitor.X, visitor.Z);
                building.Y = Ground.Height(building.X, building.Z);
                if (
                    new Vector2(building.X, building.Z).Length() > Settings.PlayableRadius - 22
                    || Village.IsNearVillage(building)
                    || Village.IsReserved(building)
                    || FlatDistance(building, Witch.Position) < 35
                    || Routes.Distance(building) < 12
                    || River.Distance(building.X, building.Z) < River.HalfWidth(building.Z) + 16
                    || Mathf.Abs(visitor.Y - road.Y) > 2.5f
                )
                    continue;
                float score = building.Y - Mathf.Abs(building.Y - visitor.Y) * 2;
                if (score <= best)
                    continue;
                best = score;
                chosen = visitor;
                tower = building;
                facing = -side * sign;
            }
        }
        if (float.IsNegativeInfinity(best))
            throw new InvalidOperationException("No place for the sorcerer's tower.");
        Sorcerer = Character.Create(
            "Černokněžník Baltazar",
            "Přivez šest dílků golema a získáš malého kamaráda.",
            2,
            "9f93ad"
        );
        Sorcerer.Role = new SorcererRole();
        Sorcerer.DressAsSorcerer();
        Sorcerer.Position = chosen;
        Sorcerer.Rotation = new(0, Mathf.Atan2(facing.X, facing.Z), 0);
        AddChild(Sorcerer);
        Tower = new Node3D { Name = "SorcererTower", Position = tower };
        AddChild(Tower);
        float baseY = 0;
        for (int x = -4; x <= 4; x += 2)
        for (int z = -4; z <= 4; z += 2)
            baseY = Mathf.Min(baseY, Ground.Height(tower.X + x, tower.Z + z) - tower.Y);
        Art.Cylinder(Tower, new(0, (baseY + 7) / 2, 0), 4.4f, 3.7f, 7 - baseY, "696578");
        Art.Cylinder(Tower, new(0, 7.2f, 0), 4.4f, 4.4f, .5f, "4e495f");
        Art.Cylinder(Tower, new(0, 10.5f, 0), 4.6f, .1f, 6.4f, "514269");
        Art.Ball(Tower, new(0, 13.8f, 0), Vector3.One * .28f, "f3cf70");
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.Tau / 12;
            Art.Block(Tower, new(Mathf.Sin(a) * 4.15f, 7.65f, Mathf.Cos(a) * 4.15f), new(.7f, .75f, .7f), "777185");
        }
        // Windows face out in every direction so the tower reads from the road.
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi / 2;
            var window = Art.Block(
                Tower,
                new(Mathf.Sin(a) * 3.9f, 4.7f, Mathf.Cos(a) * 3.9f),
                new(.85f, 1.5f, .12f),
                "f4d989"
            );
            window.Rotation = new(0, a, 0);
        }
        Obstacles.Box(Tower, new(0, 3, 0), new(7, 6, 7));
    }
}
