using Godot;
using System.Collections.Generic;

namespace MathRacer;

public partial class World : Node3D
{
    public const int QuestAnimalCount = 10;
    public Terrain Ground { get; } = new();
    public Roads Routes { get; private set; } = new();
    public Village Village { get; private set; } = new();
    public Vegetation Plants { get; } = new();
    public Grass Grass { get; } = new();
    public Wildlife Wildlife { get; } = new();
    internal Atmosphere Atmosphere { get; } = new();
    internal Animal Witch { get; private set; } = null!;
    internal Animal Sorcerer { get; private set; } = null!;
    internal Node3D Tower { get; private set; } = null!;
    public Animal[] Animals { get; private set; } = System.Array.Empty<Animal>();
    internal Vector3[] SpecialPickupSpots { get; private set; } = System.Array.Empty<Vector3>();
    public void Generate()
    {
        GenerateVillage();
        AddChild(Atmosphere); Atmosphere.Generate();
        var river = new River(); AddChild(river); river.Generate(Ground);
        foreach (float distance in new[] { 115f, 180f })
        {
            var frame = Routes.SampleMain(distance);
            var benchSpot = frame.Center + frame.Forward.Cross(Vector3.Up) * 9;
            Bench(Ground.At(benchSpot.X, benchSpot.Z));
        }
        foreach (float distance in new[] { 22f, 140f })
        {
            var frame = Routes.SampleMain(distance);
            var verge = frame.Center + frame.Forward.Cross(Vector3.Up) * 6;
            Sign(verge.X, verge.Z, "ZA KAMARÁDY");
        }
        PlaceWitch();
        PlaceSorcerer();
        Animals = new[] { Animal.Create("Králíček Matěj","Ahoj! Vítej v našem údolí.\nProjeď se a najdi moje kamarády!",0),
            Animal.Create("Liška Eliška","Jé, ty máš krásné autíčko!\nU řeky je dnes moc hezky.",1),
            Animal.Create("Medvěd Brumla","Ahoj, kamaráde! Tady to voní květinami.\nMám pro tebe malý početní úkol!",2) };
        var animals = new List<Animal>(Animals);
        for (int i = 0; i < Animals.Length; i++) { AddChild(Animals[i]); }
        string[] names = { "Králíček", "Liška", "Medvěd" };
        string[][] givenNames = {
            new[] { "Toník", "Pepík", "Kubík", "Vašík", "Honzík", "Vítek", "Jeník", "Štěpánek", "Filípek" },
            new[] { "Anička", "Bětka", "Klárka", "Terezka", "Barunka", "Maruška", "Zuzanka", "Kačenka", "Rozárka" },
            new[] { "Míša", "Bručoun", "Medoušek", "Bohouš", "Břéťa", "Dobromil", "Vojta", "Olda", "Radouš" }
        };
        string[][] colors = { new[] { "f3e8d5", "c5b4e3", "a9d8e8" }, new[] { "e9a16a", "e8bf73", "df92ac" }, new[] { "b98d71", "c5a9db", "97c9b0" } };
        for (int i = 0; i < QuestAnimalCount - Animals.Length; i++)
        {
            int kind = i % 3;
            var animal = Animal.Create($"{names[kind]} {givenNames[kind][i / 3]}", "Ahoj! Vyřeš příklad a vyber si odměnu.", kind, colors[kind][(i / 3 + 1) % 3]);
            AddChild(animal); animals.Add(animal);
        }
        Animals = animals.ToArray();
        PlaceAnimalsBesideRoads();
        SpecialPickupSpots = CreateSpecialPickupSpots();
        AddChild(Plants); Plants.Generate(Ground, Routes, Village, Animals,
            p => p.DistanceSquaredTo(Witch.Position) < 7 * 7 || p.DistanceSquaredTo(Tower.Position) < 18 * 18 || p.DistanceSquaredTo(Sorcerer.Position) < 8 * 8 || IsPickupApproachReserved(p));
    }
    internal void GenerateVillage()
    {
        AddChild(Ground); Ground.Generate();
        AddChild(Routes); Routes.Generate(Ground);
        AddChild(Village); Village.Generate(Ground, Routes);
        // Discover the seeded parcels first, then level the whole settlement together.
        // Rebuild before spawning animals or restoring saves, preserving all X/Z positions.
        var area = Village.TerrainArea;
        float height = Mathf.Max(1, Ground.Height(Village.Center.X, Village.Center.Z));
        RemoveChild(Village); Village.Free();
        RemoveChild(Routes); Routes.Free();
        Ground.LevelVillage(area, height);
        Routes = new Roads(); AddChild(Routes); Routes.Generate(Ground);
        Village = new Village(); AddChild(Village); Village.Generate(Ground, Routes);
    }
    private void PlaceAnimalsBesideRoads()
    {
        var random = new System.Random(Settings.Seed + 413);
        for (int i = 0; i < Animals.Length; i++)
        {
            bool placed = false;
            int startIndex = random.Next(Routes.Samples.Count - 1);
            for (int attempt = 0; attempt < Routes.Samples.Count && !placed; attempt++)
            {
                int index = (startIndex + attempt) % (Routes.Samples.Count - 1);
                var road = Routes.Samples[index];
                var tangent = Routes.Samples[index + 1] - road;
                if (tangent.Length() < .1f || tangent.Length() > 3) continue;
                var side = tangent.Normalized().Cross(Vector3.Up);
                foreach (int sign in new[] { i % 2 == 0 ? 1 : -1, i % 2 == 0 ? -1 : 1 })
                {
                    var verge = road + side * sign * 6.5f;
                    var position = Ground.At(verge.X, verge.Z);
                    if (new Vector2(position.X, position.Z).Length() > Settings.PlayableRadius - 10 ||
                        Village.IsNearVillage(position) || Village.IsReserved(position) || Routes.Distance(position) < 5.5f ||
                        position.DistanceSquaredTo(Witch.Position) < 20 * 20 || position.DistanceSquaredTo(Sorcerer.Position) < 25 * 25 ||
                        River.Distance(position.X, position.Z) < River.HalfWidth(position.Z) + 7) continue;
                    bool crowded = false;
                    for (int previous = 0; previous < i; previous++)
                        if (Animals[previous].Position.DistanceTo(position) < 22) { crowded = true; break; }
                    if (crowded) continue;
                    Animals[i].Position = position;
                    var facing = road - verge;
                    Animals[i].Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
                    placed = true;
                    break;
                }
            }
            if (!placed) throw new System.InvalidOperationException("No free roadside animal spot found.");
        }
    }
    private void PlaceWitch()
    {
        Witch = Animal.Create("Čarodějnice Bára", "Vyřeš příklad a vyber, jaké počasí mám přičarovat.", 2, "b6a2c8");
        Witch.DressAsWitch();
        var villageEdge = Village.TerrainArea.Grow(6);
        float best = float.PositiveInfinity;
        for (float distance = 192; distance <= 384; distance += 8)
        {
            var (road, forward) = Routes.SampleMain(distance);
            var verge = forward.Cross(Vector3.Up);
            foreach (int side in new[] { 1, -1 })
            {
                var spot = road + verge * (5.3f * side);
                spot.Y = Ground.Height(spot.X, spot.Z);
                float roadHeight = Routes.SurfaceHeight(road, Ground.Height(road.X, road.Z));
                float score = Mathf.Abs(spot.Y - roadHeight) + (distance - 192) * .07f +
                    (villageEdge.HasPoint(new Vector2(road.X, road.Z)) ? 20 : 0) +
                    (villageEdge.HasPoint(new Vector2(spot.X, spot.Z)) ? 20 : 0) +
                    (Village.IsReserved(spot) ? 20 : 0) +
                    (River.Distance(spot.X, spot.Z) < River.HalfWidth(spot.Z) + 8 ? 100 : 0);
                if (score >= best) continue;
                best = score;
                Witch.Position = spot;
                var facing = -verge * side;
                Witch.Rotation = new(0, Mathf.Atan2(facing.X, facing.Z), 0);
            }
        }
        AddChild(Witch);
        var cauldron = Ground.At(Witch.Position.X + Witch.Basis.X.X * 1.7f, Witch.Position.Z + Witch.Basis.X.Z * 1.7f);
        Art.Ball(this, cauldron + Vector3.Up * .42f, new(.63f, .5f, .63f), "434050");
        Art.Cylinder(this, cauldron + Vector3.Up * .75f, .55f, .62f, .15f, "554b62");
        Art.Cylinder(this, cauldron + Vector3.Up * .83f, .51f, .51f, .025f, "76d7aa");
        Obstacles.Box(this, cauldron + Vector3.Up * .4f, new(1.1f, .8f, 1.1f));
    }
    private void Bench(Vector3 p)
    {
        Art.Block(this, p + new Vector3(0, .65f, 0), new(2.6f, .18f, .8f), "c39770");
        Art.Block(this, p + new Vector3(0, 1.25f, .4f), new(2.6f, .7f, .12f), "d5ac7c");
        foreach (float x in new[] { -.9f, .9f }) Art.Block(this, p + new Vector3(x, .3f, 0), new(.18f, .6f, .6f), "a77a59");
        Obstacles.Box(this, p + new Vector3(0, .4f, 0), new(2.6f, .8f, .8f));
        Obstacles.Box(this, p + new Vector3(0, 1.25f, .4f), new(2.6f, .7f, .12f));
    }
    private void Sign(float x, float z, string text)
    {
        var p = Ground.At(x, z); Art.Cylinder(this, p + Vector3.Up * 1.5f, .13f, .13f, 3, "a77a59");
        Art.Sign(this, p + Vector3.Up * 3, text, 32);
        Obstacles.Trunk(this, p, .13f, 3);
    }
}





