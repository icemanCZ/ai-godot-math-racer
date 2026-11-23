using System;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Characters;

namespace MathRacer.Valley;

/// <summary>
/// Where the hunter's pen stands: beside a road at the edge of the village, its gate a few metres off the road and the pen
/// itself behind it. In the pen's frame +Z points away from the road and +X to the right of that (<see cref="HunterSite"/>).
/// </summary>
internal sealed record HunterSite(Vector3 Gate, Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>The inside of the pen: from the front fence to the back fence, and to each side.</summary>
    public const float Depth = 22,
        HalfWidth = 12;

    /// <summary>How far the gate stands from the middle of the road.</summary>
    public const float RoadOffset = 10;

    /// <summary>The ground taken by the pen, the hunter and his rack: plants there are taken out and no small friend hides.</summary>
    public const float AlongMin = -6.5f,
        AlongMax = Depth + 2.5f,
        AcrossMax = HalfWidth + 3;

    public Vector3 Right => Vector3.Up.Cross(Inward);

    /// <summary>A point of the pen's frame (across, along) on the flat; the height is the gate's.</summary>
    public Vector3 Local(float across, float along) => Gate + Right * across + Inward * along;

    /// <summary>Where the hunter waits, in front of the fence beside the gate, looking at the road.</summary>
    public Vector3 HunterSpot => Local(4.6f, -2.4f);

    /// <summary>Where the car stops to hand an animal over: on the verge in front of the gate.</summary>
    public Vector3 Drop => Local(0, -4.2f);
}

public partial class World
{
    internal const int HunterId = ActorIds.Hunter;

    internal static bool IsHunter(int id) => id == HunterId;

    /// <summary>The hunter at his pen, or null in a valley that has no room for it.</summary>
    internal Character? Hunter { get; private set; }

    /// <summary>The pen with the animals the player brought, or null without the hunter.</summary>
    internal HunterPen? Pen { get; private set; }

    /// <summary>The animals that run free during a hunt and the net; nothing is shown between hunts.</summary>
    internal HuntField HuntGrounds { get; private set; } = null!;

    /// <summary>The animals brought to the pen and the hunt under way, which the hunter's pet counts.</summary>
    internal AnimalHunt Hunt { get; } = new();

    private HunterSite? hunterSite;

    internal HunterSite? HunterPlace => hunterSite;

    /// <summary>The passes of the search: the edge of the village first, then wider, then anywhere on gentler rules.</summary>
    private static readonly (float Near, float Far, float Slope, int Attempts)[] HunterPasses =
    {
        (35, 150, 2.6f, 6000),
        (25, 240, 3.4f, 12000),
        (0, 2000, 4.5f, 40000),
    };

    private static readonly float[] PenAcross = { -HunterSite.HalfWidth - 1, -6, 0, 6, HunterSite.HalfWidth + 1 };
    private static readonly float[] PenAlong = { -4.5f, 0, 5.5f, 11, 16.5f, HunterSite.Depth + 1 };

    /// <summary>
    /// Plans the hunter's pen after everything else and before the plants, with a seeded search of its own and without
    /// reserving ground or drawing from any other generator, so no older site, actor or tree of a valley moves; the plants
    /// that stand on the pen are taken out afterwards (<see cref="InHunterZone"/>). The gate looks at a road at the edge of
    /// the village; where there is no room the search goes further out and allows steeper ground.
    /// </summary>
    private void PlanHunter()
    {
        var random = new Random(Settings.Seed + 2393);
        var friends = InteractionActors.ToArray();
        var friendSpots = ActorSpots(friends);
        var benches = new System.Collections.Generic.List<Vector3>();
        foreach (float distance in WorldTuning.BenchDistances)
        {
            var frame = Routes.SampleMain(distance);
            benches.Add(frame.Center + frame.Forward.Cross(Vector3.Up) * WorldTuning.BenchOffset);
        }
        foreach (float distance in WorldTuning.SignDistances)
        {
            var frame = Routes.SampleMain(distance);
            benches.Add(frame.Center + frame.Forward.Cross(Vector3.Up) * WorldTuning.SignOffset);
        }
        int count = Routes.Samples.Count;
        if (count < 2)
            return;
        for (int pass = 0; pass < HunterPasses.Length; pass++)
        {
            var (near, far, slope, attempts) = HunterPasses[pass];
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int index = random.Next(count - 1);
                var road = Routes.Samples[index];
                var forward = Routes.Samples[index + 1] - road;
                forward.Y = 0;
                bool left = random.Next(2) == 0;
                if (forward.LengthSquared() < .01f)
                    continue;
                var outward = forward.Normalized().Cross(Vector3.Up) * (left ? -1 : 1);
                var gate = Ground.At(
                    road.X + outward.X * HunterSite.RoadOffset,
                    road.Z + outward.Z * HunterSite.RoadOffset
                );
                float village = Village.PlotDistance(gate);
                if (village < near || village > far)
                    continue;
                var site = new HunterSite(gate, road, outward, Mathf.Atan2(outward.X, outward.Z));
                if (PenFits(site, friendSpots, benches, slope))
                {
                    hunterSite = site;
                    GD.Print($"HUNTER_READY village={village:0} pass={pass}");
                    return;
                }
            }
        }
        GD.Print($"HUNTER_MISSING seed={Settings.Seed}");
    }

    /// <summary>
    /// The whole pen and the verge in front of it stand on dry, fairly level, empty ground: off every road (only the front
    /// verge comes near the road it faces), clear of the houses, the sites and their ways, the field paths, the tower, the
    /// characters, the benches and the places where loads and fruit are picked up.
    /// </summary>
    private bool PenFits(
        HunterSite site,
        Vector2[] friendSpots,
        System.Collections.Generic.List<Vector3> benches,
        float slope
    )
    {
        var tower = TowerSpot;
        foreach (float along in PenAlong)
        foreach (float across in PenAcross)
        {
            var q = site.Local(across, along);
            q = Ground.At(q.X, q.Z);
            bool front = along < 0;
            if (
                FlatMath.Length(q.X, q.Z) > Settings.PlayableRadius - 30
                || (!front && Mathf.Abs(q.Y - site.Gate.Y) > slope)
                || River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 6
                || Routes.Distance(q) < (front ? 4.6f : 7.5f)
                || CloserThan(tower, q.X, q.Z, 20)
                || AnyCloserThan(friendSpots, q.X, q.Z, 10)
                || AnyCloserThan(SpecialPickupSpots, q.X, q.Z, 10)
                || Village.IsReserved(q)
                || Village.PlotDistance(q) < 10
                || IsPickupApproachReserved(q)
                || IsHaulReserved(q)
                || InGlassworksZone(q)
                || InFarmZone(q)
                || InBakeryZone(q)
                || InPlaygroundZone(q)
                || InZooZone(q)
                || (Tracks?.Contains(q) ?? false)
            )
                return false;
            foreach (var works in Industry)
                if (InYard(q, works.Site.Position, works.Site.Inward, -10, 25, 17))
                    return false;
            foreach (var project in Projects)
                if (FlatDistance(q, project.Focus) < 24)
                    return false;
            foreach (var bench in benches)
                if (FlatDistance(q, bench) < 6)
                    return false;
        }
        return true;
    }

    /// <summary>Raises the pen and the hunter, who has the next free actor ID, registered after the pirate captain.</summary>
    private void BuildHunter()
    {
        HuntGrounds = new HuntField(this) { Name = "HuntGrounds" };
        AddChild(HuntGrounds);
        if (hunterSite is not { } site)
            return;
        var pen = new HunterPen(site, Ground);
        // The player raises the hut with goods from the works; a valley without them has it standing already.
        pen.SetStage(Industry.Length > 0 ? 0 : HutPlans.Trips);
        AddChild(pen);
        var hunter = Character.Create(CharacterCatalog.Hunter);
        hunter.Role = new HunterRole();
        hunter.DressAsHunter();
        hunter.Position = Ground.At(site.HunterSpot.X, site.HunterSpot.Z);
        hunter.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
        AddChild(hunter);
        Register(HunterId, hunter);
        pen.Hunter = hunter;
        Hunter = hunter;
        Pen = pen;
        // The hunter's pet, a fawn, waits beside him once eight animals are in the pen.
        var pet = new CollectorPet(
            WorldPetCatalog.Fawn,
            hunter,
            () => Hunt.Caught,
            AnimalHunt.PetGoal,
            worldPets.Count
        );
        var seat = site.Local(6.4f, -2.2f);
        pet.SetHome(Ground.At(seat.X, seat.Z), hunter.Rotation.Y);
        AddChild(pet);
        AddWorldPet(pet);
    }

    /// <summary>Whether a point lies on the pen, the hunter's corner or the verge in front of the gate.</summary>
    internal bool InHunterZone(Vector3 p) =>
        hunterSite is { } site
        && InSiteRectangle(p, site.Gate, site.Inward, HunterSite.AlongMin, HunterSite.AlongMax, HunterSite.AcrossMax);

    /// <summary>
    /// A place far from the pen where the animals of a hunt run free: open, dry meadow inside the playable ground, away
    /// from the village, the sites and the roads' busiest parts, from <paramref name="near"/> to <paramref name="far"/>
    /// metres from <paramref name="from"/>. Null when none was found.
    /// </summary>
    internal Vector3? HerdSpot(Vector3 from, float near, float far, Random random)
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            float turn = random.NextSingle() * Mathf.Tau;
            float distance = near + random.NextSingle() * (far - near);
            float x = from.X + Mathf.Sin(turn) * distance;
            float z = from.Z + Mathf.Cos(turn) * distance;
            var p = Ground.At(x, z);
            if (IsMeadow(p, 9))
                return p;
        }
        return null;
    }

    /// <summary>Open ground where a wild animal of the hunt may stand or run.</summary>
    internal bool IsMeadow(Vector3 p, float margin = 3)
    {
        if (
            FlatMath.Length(p.X, p.Z) > Settings.PlayableRadius - 25
            || River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + margin
            || Village.IsNearVillage(p)
            || Village.IsReserved(p)
            || Plants.NearTrunk(p, 1.4f)
            || InGlassworksZone(p)
            || InFarmZone(p)
            || InBakeryZone(p)
            || InPlaygroundZone(p)
            || InZooZone(p)
            || InHunterZone(p)
            || FlatDistance(p, Tower.Position) < 16
        )
            return false;
        return true;
    }
}
