using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.Valley;

/// <summary>
/// The post office at the edge of the village (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 2): a small yellow house
/// beside a village road with its door towards it, the postman and the letter box in front. In the site's frame +Z
/// (<see cref="Inward"/>) points away from the road and +X (<see cref="Right"/>) to the right of that.
/// </summary>
/// <param name="Front">The middle of the forecourt, <see cref="RoadOffset"/> from the middle of the road.</param>
/// <param name="Road">The point of the road the post office looks at.</param>
/// <param name="Inward">From the road towards the post office, on the flat.</param>
/// <param name="Yaw">The rotation about Y that turns local +Z into <see cref="Inward"/>.</param>
internal sealed record PostOfficeSite(Vector3 Front, Vector3 Road, Vector3 Inward, float Yaw)
{
    /// <summary>How far the forecourt stands from the middle of the road.</summary>
    public const float RoadOffset = 7;

    /// <summary>The house: its middle this far on from <see cref="Front"/>, its width and its depth.</summary>
    public const float HouseAlong = 4.6f,
        HouseWidth = 7,
        HouseDepth = 6;

    /// <summary>The ground taken by the house, the forecourt and the verge before it.</summary>
    public const float AlongMin = -3.6f,
        AlongMax = HouseAlong + HouseDepth / 2 + 1.5f,
        AcrossMax = HouseWidth / 2 + 1.5f;

    public Vector3 Right => Vector3.Up.Cross(Inward);

    /// <summary>A point of the site's frame (across, along) on the flat; its height is the forecourt's.</summary>
    public Vector3 Local(float across, float along) => Front + Right * across + Inward * along;

    /// <summary>Where the postman waits: at the front of the forecourt right of the door, looking at the road.</summary>
    public Vector3 PostmanSpot => Local(2.3f, -1.6f);

    /// <summary>Where the yellow letter box stands, left of the door.</summary>
    public Vector3 LetterBox => Local(-2.5f, -.6f);
}

public partial class World
{
    private PostOfficeSite? postOfficeSite;

    /// <summary>The letters of the post, which the postman's pigeon counts.</summary>
    internal Post Post { get; } = new();

    /// <summary>The post office's place, or null in a valley where the postman stands on the square.</summary>
    internal PostOfficeSite? PostOfficePlace => postOfficeSite;

    /// <summary>The post office's house, or null.</summary>
    internal PostOffice? PostOffice { get; private set; }

    /// <summary>The postman: in front of the post office, or on the square where it has no place.</summary>
    internal Character Postman { get; private set; } = null!;

    /// <summary>The passes of the search: level ground near the middle of the village first, then a little steeper.</summary>
    private static readonly float[] PostOfficeSlopes = { 1.2f, 2.2f };

    /// <summary>
    /// Every point of the post office stays this near a house of the village: hidden friends, treasures, fireflies and
    /// the herds keep <see cref="Village.TaskAnimalClearance"/> from every house, so none of them, placed later, moves.
    /// </summary>
    private const float PostOfficeKeep = Settlement.Village.TaskAnimalClearance - 10;

    private static readonly float[] PostAcross =
    {
        -PostOfficeSite.AcrossMax,
        -2.6f,
        0,
        2.6f,
        PostOfficeSite.AcrossMax,
    };

    private static readonly float[] PostAlong =
    {
        PostOfficeSite.AlongMin + .6f,
        -1.2f,
        1.5f,
        PostOfficeSite.HouseAlong,
        PostOfficeSite.HouseAlong + 2.5f,
        PostOfficeSite.AlongMax,
    };

    /// <summary>
    /// Plans the post office after the hunter's pen and before the plants, like the pen: without random numbers,
    /// without reserving ground and without drawing from any other generator, so no older site, actor or tree of a
    /// valley moves; the plants that stand on it are taken out afterwards (<see cref="InPostOfficeZone"/>). It takes the
    /// place nearest the middle of the village where the house fits beside a road. Everything placed later that keeps
    /// clear of what is built (the small friends, the treasures, the fireflies and the herds of the hunt) keeps clear of
    /// the whole village, so it does not move either.
    /// </summary>
    private void PlanPostOffice()
    {
        int count = Routes.Samples.Count;
        if (count < 2)
            return;
        var center = Village.Center;
        var friends = InteractionActors
            .Concat(new[] { Village.Mayor, Village.Glassmaker, Village.Treasurer })
            .Select(a => new Vector2(a.Position.X, a.Position.Z))
            .ToArray();
        var benches = new List<Vector3>();
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
        var candidates = new List<(float Distance, PostOfficeSite Site)>();
        for (int index = 0; index + 1 < count; index++)
        {
            var road = Routes.Samples[index];
            var forward = (Routes.Samples[index + 1] - road) with { Y = 0 };
            if (forward.LengthSquared() < .01f)
                continue;
            foreach (int side in new[] { -1, 1 })
            {
                var inward = forward.Normalized().Cross(Vector3.Up) * side;
                var front = Ground.At(
                    road.X + inward.X * PostOfficeSite.RoadOffset,
                    road.Z + inward.Z * PostOfficeSite.RoadOffset
                );
                float distance = FlatDistance(front, center);
                float plot = Village.PlotDistance(front);
                if (plot < 9 || plot > PostOfficeKeep - PostOfficeSite.AlongMax)
                    continue;
                candidates.Add((distance, new PostOfficeSite(front, road, inward, Mathf.Atan2(inward.X, inward.Z))));
            }
        }
        // Nearest the middle first; a stable sort keeps the order of the road for equal distances.
        var ordered = candidates.OrderBy(candidate => candidate.Distance).ToList();
        for (int pass = 0; pass < PostOfficeSlopes.Length; pass++)
            foreach (var (distance, site) in ordered)
                if (PostOfficeFits(site, friends, benches, PostOfficeSlopes[pass]))
                {
                    postOfficeSite = site;
                    GD.Print($"POST_OFFICE_READY center={distance:0} pass={pass}");
                    return;
                }
        GD.Print($"POST_OFFICE_MISSING seed={Settings.Seed}");
    }

    /// <summary>
    /// The house, the forecourt and the verge before it stand on dry, fairly level, empty ground: off every road (only the
    /// verge comes near the road it faces), off the village's houses, gardens and paths, clear of the sites, the field
    /// ways, the tower, the characters, the benches and the places where loads and fruit are picked up.
    /// </summary>
    private bool PostOfficeFits(PostOfficeSite site, Vector2[] friends, List<Vector3> benches, float slope)
    {
        var tower = TowerSpot;
        foreach (float along in PostAlong)
        foreach (float across in PostAcross)
        {
            var q = site.Local(across, along);
            q = Ground.At(q.X, q.Z);
            bool verge = along < 0;
            if (
                Village.PlotDistance(q) > PostOfficeKeep
                || (!verge && Mathf.Abs(q.Y - site.Front.Y) > slope)
                || River.Distance(q.X, q.Z) < River.HalfWidth(q.Z) + 6
                || River.OnBridge(q.X, q.Z)
                || Routes.Distance(q) < (verge ? 3 : 5)
                || CloserThan(tower, q.X, q.Z, 20)
                || AnyCloserThan(friends, q.X, q.Z, 4)
                || AnyCloserThan(SpecialPickupSpots, q.X, q.Z, 10)
                || Village.IsReserved(q)
                || IsPickupApproachReserved(q)
                || IsHaulReserved(q)
                || IsHaulApproachReserved(q)
                || InGlassworksZone(q)
                || InFarmZone(q)
                || InBakeryZone(q)
                || InPlaygroundZone(q)
                || InZooZone(q)
                || InHunterZone(q)
                || (Tracks?.Contains(q) ?? false)
                || (Ford?.Covers(q) ?? false)
            )
                return false;
            foreach (var works in Industry)
                if (InYard(q, works.Site.Position, works.Site.Inward, -10, 25, 17))
                    return false;
            foreach (var project in Projects)
                if (FlatDistance(q, project.Focus) < 22)
                    return false;
            foreach (var bench in benches)
                if (FlatDistance(q, bench) < 6)
                    return false;
        }
        // No other character within reach of the postman, and the postman within reach of a car on the road.
        return !AnyCloserThan(friends, site.PostmanSpot.X, site.PostmanSpot.Z, Settings.InteractionRange + 2)
            && Routes.Distance(site.PostmanSpot) < Settings.InteractionRange - 2;
    }

    /// <summary>
    /// Raises the post office and puts the postman in front of it; where it has no place he stands on the square. He is
    /// registered last of all (<see cref="Generate"/>), so that no search that keeps clear of the characters moves.
    /// </summary>
    private void BuildPostOffice()
    {
        var postman = Character.Create(CharacterCatalog.Postman);
        postman.Role = new PostmanRole();
        postman.DressAsPostman();
        Postman = postman;
        if (postOfficeSite is { } site)
        {
            var office = new PostOffice(site, Ground);
            AddChild(office);
            PostOffice = office;
            postman.Position = Ground.At(site.PostmanSpot.X, site.PostmanSpot.Z);
            postman.Rotation = new Vector3(0, site.Yaw + Mathf.Pi, 0);
            AddChild(postman);
        }
        else
            Village.PlacePostmanOnSquare(postman);
        // The postman's pigeon waits on the forecourt and comes once every keeper has sent a stamp.
        var pigeon = new CollectorPet(
            WorldPetCatalog.Pigeon,
            postman,
            () => Post.KeepersWithStamps,
            Post.Keepers,
            worldPets.Count
        );
        var stand = postOfficeSite != null ? postman.Transform : Village.Transform * postman.Transform;
        var seat = stand * new Vector3(1.4f, 0, .5f);
        pigeon.SetHome(Ground.At(seat.X, seat.Z), postman.Rotation.Y);
        AddChild(pigeon);
        AddWorldPet(pigeon);
    }

    /// <summary>Whether a point lies on the post office, its forecourt or the verge before it.</summary>
    internal bool InPostOfficeZone(Vector3 p) =>
        postOfficeSite is { } site
        && InSiteRectangle(
            p,
            site.Front,
            site.Inward,
            PostOfficeSite.AlongMin,
            PostOfficeSite.AlongMax,
            PostOfficeSite.AcrossMax
        );
}
