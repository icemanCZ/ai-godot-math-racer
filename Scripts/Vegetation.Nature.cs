using Godot;
using System;

namespace MathRacer;

public partial class Vegetation
{
    public Vector3 MeadowPreview { get; private set; }
    public Vector3 BankPreview { get; private set; }

    private static ArrayMesh ConiferMesh()
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(int ring, int side)
        {
            float t = ring / 4f, angle = side * Mathf.Tau / 16;
            float radius = (1 - t) * (1 + .07f * Mathf.Sin(angle * 5 + ring));
            return new(Mathf.Cos(angle) * radius, -1 + t * 2 + Mathf.Sin(angle * 7 + ring) * .09f * (1 - t), Mathf.Sin(angle) * radius);
        }
        for (int ring = 0; ring < 4; ring++)
            for (int side = 0; side < 16; side++)
            {
                var a = Point(ring, side); var b = Point(ring, side + 1);
                var c = Point(ring + 1, side); var d = Point(ring + 1, side + 1);
                foreach (var p in new[] { a, b, c, b, d, c }) surface.AddVertex(p);
            }
        surface.GenerateNormals(); return surface.Commit();
    }

    private static ArrayMesh LilyPadMesh()
    {
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        // Leave an open V in the circular pad, rather than a solid green disk.
        for (int i = 1; i < 15; i++)
        {
            float a = i * Mathf.Tau / 16, b = (i + 1) * Mathf.Tau / 16;
            surface.AddVertex(Vector3.Zero);
            surface.AddVertex(new(Mathf.Cos(b), 0, Mathf.Sin(b)));
            surface.AddVertex(new(Mathf.Cos(a), 0, Mathf.Sin(a)));
        }
        surface.GenerateNormals(); return surface.Commit();
    }

    private static ArrayMesh PointedLeafMesh()
    {
        // A folded, tapered leaf has a readable edge even when seen from below.
        var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] vertices = { new(0, 0, -.9f), new(-.42f, 0, 0), new(0, .13f, .12f), new(.42f, 0, 0), new(0, .05f, 1) };
        int[] triangles = { 0, 1, 2, 0, 2, 3, 1, 4, 2, 2, 4, 3 };
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < triangles.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    surface.SetColor(Colors.White);
                    surface.AddVertex(vertices[triangles[i + (side == 0 ? j : 2 - j)]]);
                }
        surface.GenerateNormals(); return surface.Commit();
    }

    private void GenerateNatureDetails(Terrain terrain, Roads roads, Func<Vector3, bool> reserved)
    {
        // This stream does not affect tree positions, solid obstacles or saved pickups.
        var random = new RandomNumberGenerator { Seed = (ulong)Settings.Seed + 1710 };
        bool Clear(Vector3 p) => new Vector2(p.X, p.Z).Length() < Settings.PlayableRadius - 10 &&
            !reserved(p) && roads.Distance(p) > 5.2f && !NearTrunk(p, 1.1f) &&
            River.Distance(p.X, p.Z) > River.HalfWidth(p.Z) + 7;

        // Irregular groups of taller daisies, buttercups and lupins, instead of a uniform scatter.
        for (int group = 0; group < 650; group++)
        {
            Vector3 center;
            if (group < roads.Samples.Count / 9)
            {
                int index = group * 9;
                var road = roads.Samples[index];
                var tangent = roads.Samples[Math.Min(index + 1, roads.Samples.Count - 1)] - road;
                var side = tangent.Normalized().Cross(Vector3.Up);
                center = terrain.At(road.X + side.X * (group % 2 == 0 ? 8 : -8), road.Z + side.Z * (group % 2 == 0 ? 8 : -8));
            }
            else center = terrain.At(random.RandfRange(-480, 480), random.RandfRange(-480, 480));
            if (!Clear(center)) continue;
            if (MeadowPreview == Vector3.Zero) MeadowPreview = center;
            int kind = group % 3;
            for (int flower = 0; flower < 9; flower++)
            {
                float angle = random.RandfRange(0, Mathf.Tau), radius = random.RandfRange(.2f, 2.2f);
                var p = terrain.At(center.X + Mathf.Cos(angle) * radius, center.Z + Mathf.Sin(angle) * radius);
                if (!Clear(p)) continue;
                float height = random.RandfRange(.55f, 1.05f);
                Part(p + Vector3.Up * height / 2, new(.025f, height / 2, .025f), "648752", "detail");
                for (int leaf = 0; leaf < 2; leaf++)
                {
                    float a = angle + leaf * 2.7f;
                    var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    Part(p + Vector3.Up * height * (.3f + leaf * .22f) + direction * .13f,
                        new(.18f, .15f, .28f), "86aa61", "sprig", new Basis(Vector3.Up, -a + Mathf.Pi / 2));
                }
                if (kind == 2)
                {
                    for (int tier = 0; tier < 5; tier++)
                        for (int petal = 0; petal < 3; petal++)
                        {
                            float a = petal * Mathf.Tau / 3 + tier * .7f;
                            Part(p + new Vector3(Mathf.Cos(a) * (.09f - tier * .012f), height + tier * .09f, Mathf.Sin(a) * (.09f - tier * .012f)),
                                new(.09f - tier * .01f, .075f, .09f - tier * .01f), tier % 2 == 0 ? "a397ce" : "c6b3df", "detail");
                        }
                }
                else Blossom(p + Vector3.Up * height, Vector3.Up, kind == 0 ? .27f : .2f, kind == 0 ? "fff0df" : "edcd72");
            }
        }

        // Fern groups use the same safe clearings as the flowers.
        for (int i = 0; i < 700; i++)
        {
            var p = terrain.At(random.RandfRange(-480, 480), random.RandfRange(-480, 480));
            if (!Clear(p)) continue;
            if (i % 3 == 0) Fern(p, random.RandfRange(.7f, 1.2f), random.RandfRange(0, Mathf.Tau));
        }
        // Reuse the exact existing boulder centers so moss never creates new obstacles.
        var moss = new System.Collections.Generic.List<(Vector3 Position, Vector3 Scale)>();
        foreach (var batch in batches)
            if (batch.Key.Shape == "leaf" && batch.Key.Color == "adaebf")
                foreach (var transform in batch.Value) moss.Add((transform.Origin, transform.Basis.Scale));
        foreach (var rock in moss)
        {
            Part(rock.Position + Vector3.Up * rock.Scale.Y * .78f, new Vector3(rock.Scale.X * .7f, .065f, rock.Scale.Z * .65f), "899b69", "detail");
            for (int pebble = 0; pebble < 3; pebble++)
            {
                var p = terrain.At(rock.Position.X + .7f + pebble * .2f, rock.Position.Z + .4f - pebble * .18f);
                if (Clear(p)) Part(p + Vector3.Up * .05f, new(.14f, .09f, .11f), pebble % 2 == 0 ? "c3beaa" : "9caa9e", "detail");
            }
        }

        // Both banks get reeds and broad leaves; protect bridges, roads and all interaction areas.
        for (int group = 0; group < 175; group++)
        {
            float z = -490 + group * 5.6f;
            if (Mathf.Abs(z - River.BridgeZ) < 14) continue;
            int side = group % 2 == 0 ? -1 : 1;
            float x = River.CenterX(z) + side * (River.HalfWidth(z) + random.RandfRange(2.8f, 4.5f));
            var center = terrain.At(x, z);
            if (reserved(center) || roads.Distance(center) < 6 || center.Y < River.Level(z) - .08f) continue;
            if (BankPreview == Vector3.Zero || Mathf.Abs(z - River.BridgeZ) < Mathf.Abs(BankPreview.Z - River.BridgeZ)) BankPreview = center;
            for (int reed = 0; reed < 7; reed++)
            {
                var p = terrain.At(x + random.RandfRange(-.6f, .6f), z + random.RandfRange(-.7f, .7f));
                if (reserved(p) || roads.Distance(p) < 6 || p.Y < River.Level(p.Z) - .08f) continue;
                float height = random.RandfRange(.9f, 1.65f);
                Part(p + Vector3.Up * height / 2, new(.025f, height / 2, .025f), "82995d", "detail");
                Part(p + Vector3.Up * height, new(.085f, .23f, .085f), "96704c", "detail");
                for (int leaf = 0; leaf < 3; leaf++)
                {
                    float a = reed + leaf * 2.1f;
                    var direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var basis = new Basis(Vector3.Up, -a + Mathf.Pi / 2).Rotated(direction.Cross(Vector3.Up).Normalized(), -.65f);
                    Part(p + Vector3.Up * height * .35f + direction * .22f, new(.13f, .2f, .65f), leaf % 2 == 0 ? "a7ba73" : "82995d", "sprig", basis);
                }
            }
        }

        for (int group = 0; group < 75; group++)
        {
            float z = -450 + group * 12.1f;
            if (Mathf.Abs(z - River.BridgeZ) < 17) continue;
            float x = River.CenterX(z) + (group % 2 == 0 ? -1 : 1) * (River.HalfWidth(z) - 1.6f);
            for (int leaf = 0; leaf < 4; leaf++)
            {
                var p = new Vector3(x + random.RandfRange(-.55f, .55f), 0, z + leaf * .7f);
                float depth = River.Level(p.Z) - terrain.Height(p.X, p.Z);
                if (depth < .15f || depth > 1.3f || reserved(p) || roads.Distance(p) < 6) continue;
                p.Y = River.Level(p.Z) + .065f;
                float size = random.RandfRange(.26f, .46f);
                Part(p, new(size, 1, size), leaf % 2 == 0 ? "729b72" : "91b482", "pad", new Basis(Vector3.Up, random.RandfRange(0, Mathf.Tau)));
                if (leaf == 0) Blossom(p + Vector3.Up * .07f, Vector3.Up, .2f, "ffe0e5");
            }
        }
    }

    private void Fern(Vector3 p, float size, float turn)
    {
        for (int frond = 0; frond < 6; frond++)
        {
            float angle = turn + frond * Mathf.Tau / 6;
            var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var side = direction.Cross(Vector3.Up);
            for (int pair = 0; pair < 5; pair++)
            {
                float t = (pair + 1) / 6f;
                var center = p + (direction * t * .85f + Vector3.Up * (.1f + Mathf.Sin(t * Mathf.Pi) * .5f)) * size;
                foreach (int sign in new[] { -1, 1 })
                    Part(center + side * sign * .08f * size, new Vector3(.16f * (1 - t * .6f), .18f, .28f * (1 - t * .65f)) * size,
                        pair % 2 == 0 ? "86aa61" : "acc77b", "sprig", new Basis(Vector3.Up, -angle + sign * .9f));
            }
        }
    }
}
