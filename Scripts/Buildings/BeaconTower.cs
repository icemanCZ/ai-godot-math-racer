using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Graphics;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// A beacon of the castle (<c>docs/CASTLE_PLAN.md</c>, phase 2) beside the homestead of a border: until its logs come,
/// stakes and a heap of brushwood mark where it will stand (shown once the castle is finished); then a wooden lookout on
/// four legs with a ladder, a platform and an iron basket on top, whose fire burns day and night and lights the ground
/// round it in the dark. The legs block the car once it stands.
/// </summary>
public partial class BeaconTower : Node3D, ITripTarget
{
    private readonly Terrain ground;
    private readonly Node3D plan = new() { Name = "BeaconPlan", Visible = false };
    private readonly Node3D tower = new() { Name = "BeaconTower", Visible = false };
    private readonly Node3D flames = new() { Name = "BeaconFlames" };
    private readonly System.Collections.Generic.List<MeshInstance3D> tongues = new();
    private readonly System.Collections.Generic.List<CollisionShape3D> legs = new();
    private readonly OmniLight3D light = new()
    {
        Name = "BeaconLight",
        LightColor = new Color("ff9a3c"),
        OmniRange = 22,
        LightEnergy = 0,
        ShadowEnabled = false,
    };
    private float time;

    internal BeaconSite Site { get; }

    /// <summary>Whether its logs have come: the tower stands and its fire burns.</summary>
    internal bool Built { get; private set; }

    /// <summary>The princess, who sends the logs.</summary>
    internal Character Princess { get; set; } = null!;

    /// <summary>The basket of fire, for the camera and the sparks that fly to the castle.</summary>
    internal Vector3 Fire => Position + Vector3.Up * (BeaconSite.Height + 1);

    internal BeaconTower(BeaconSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "Beacon" + site.Border;
        Position = ground.At(site.Foot.X, site.Foot.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        AddChild(plan);
        AddChild(tower);
        BuildPlan();
        BuildTower();
    }

    private float Floor(float x, float z)
    {
        var p = Position + new Basis(Vector3.Up, Site.Yaw) * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    /// <summary>Four stakes with red tips round the place and a heap of brushwood in its middle.</summary>
    private void BuildPlan()
    {
        foreach (var (x, z) in new[] { (-1.6f, -1.6f), (1.6f, -1.6f), (1.6f, 1.6f), (-1.6f, 1.6f) })
        {
            float y = Floor(x, z);
            Art.Cylinder(plan, new(x, y + .4f, z), .06f, .05f, .9f, "a77a59");
            Art.Ball(plan, new(x, y + .86f, z), new(.1f, .06f, .1f), "e25b45");
        }
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5;
            var twig = Art.Cylinder(
                plan,
                new(Mathf.Sin(a) * .3f, Floor(0, 0) + .35f, Mathf.Cos(a) * .3f),
                .05f,
                .03f,
                1.1f,
                "7a5536"
            );
            twig.Rotation = new Vector3(Mathf.Cos(a) * .6f, 0, -Mathf.Sin(a) * .6f);
        }
    }

    /// <summary>Four legs leaning in, a ladder, the platform with its rail and the iron basket of fire on top.</summary>
    private void BuildTower()
    {
        float top = BeaconSite.Height;
        foreach (var (x, z) in new[] { (-1.6f, -1.6f), (1.6f, -1.6f), (1.6f, 1.6f), (-1.6f, 1.6f) })
        {
            float y = Floor(x, z) - .3f;
            var foot = new Vector3(x, y, z);
            var head = new Vector3(x * .55f, top - 1.2f, z * .55f);
            Art.Beam(tower, foot, head, .26f, "8a6040");
            var body = Obstacles.Add(tower, foot + Vector3.Up * 1.5f, new BoxShape3D { Size = new(.6f, 3, .6f) });
            legs.Add((CollisionShape3D)body.GetChild(0));
        }
        // Cross braces at two heights.
        foreach (float h in new[] { 2.6f, 5.4f })
        {
            float spread = Mathf.Lerp(1.6f, .88f, h / (top - 1.2f));
            for (int side = 0; side < 4; side++)
            {
                var a = Corner(side, spread, h);
                var b = Corner(side + 1, spread, h);
                Art.Beam(tower, a, b, .12f, "a07650");
            }
        }
        // The ladder up the side towards the road.
        for (int rung = 0; rung < 9; rung++)
        {
            float h = .6f + rung * .8f;
            float spread = Mathf.Lerp(1.6f, .88f, h / (top - 1.2f));
            Art.Block(tower, new(0, h, spread + .05f), new(.8f, .07f, .07f), "c49a64");
        }
        // The platform with its rail.
        Art.Block(tower, new(0, top - 1.1f, 0), new(2.4f, .16f, 2.4f), "9a6c45");
        for (int side = 0; side < 4; side++)
        {
            var a = Corner(side, 1.15f, top - .45f);
            var b = Corner(side + 1, 1.15f, top - .45f);
            Art.Beam(tower, a, b, .08f, "7a5536");
            Art.Cylinder(tower, Corner(side, 1.15f, top - .75f), .05f, .05f, .7f, "7a5536");
        }
        // The iron basket and its fire.
        Art.Cylinder(tower, new(0, top - .3f, 0), .55f, .9f, .8f, "3b3530");
        flames.Position = new Vector3(0, top - .1f, 0);
        tower.AddChild(flames);
        foreach (
            var (radius, height, color) in new[]
            {
                (.75f, 2.0f, "ff6a1a"),
                (.5f, 1.6f, "ffb02e"),
                (.28f, 1.1f, "fff2a0"),
            }
        )
        {
            var cone = Art.Cylinder(flames, Vector3.Up * (height / 2), radius, .03f, height, color);
            cone.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(color).SrgbToLinear() with { A = .9f },
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            cone.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            tongues.Add(cone);
        }
        flames.AddChild(light);
        light.Position = Vector3.Up * 1.5f;
        // A little red flag on a corner of the rail.
        Art.Cylinder(tower, new(1.15f, top + .5f, 1.15f), .04f, .04f, 1.4f, "5a4636");
        Art.Block(tower, new(1.15f, top + .95f, 1.55f), new(.04f, .4f, .8f), "e2563b");
        SetBuilt(false, false);
    }

    private static Vector3 Corner(int side, float spread, float height) =>
        (side % 4) switch
        {
            0 => new(-spread, height, -spread),
            1 => new(spread, height, -spread),
            2 => new(spread, height, spread),
            _ => new(-spread, height, spread),
        };

    /// <summary>
    /// Puts the beacon up with its fire, or shows only where it will stand (<paramref name="wanted"/>: the castle is
    /// finished and asks for it), or nothing at all.
    /// </summary>
    internal void SetBuilt(bool built, bool wanted)
    {
        Built = built;
        tower.Visible = built;
        plan.Visible = !built && wanted;
        foreach (var leg in legs)
            leg.Disabled = !built;
    }

    /// <summary>The fire flickers, and its light shows by night (<paramref name="night"/> 0 by day to 1 at full night).</summary>
    internal void Tick(float delta, float night)
    {
        if (!Built)
            return;
        time += delta;
        for (int i = 0; i < tongues.Count; i++)
            tongues[i].Scale = new Vector3(1, 1 + .16f * Mathf.Sin(time * (8 + i * 2.1f) + i), 1);
        light.Visible = night > .05f;
        light.LightEnergy = .3f + 2.4f * night + .2f * Mathf.Sin(time * 12);
    }

    string ITripTarget.Kind => TripKinds.CastleBeacon;
    int ITripTarget.Index => (int)Site.Border;
    Character ITripTarget.Contractor => Princess;
    int ITripTarget.NextStep => 0;

    HaulLoad ITripTarget.LoadFor(int step) => HaulLoad.Logs;

    Vector3 ITripTarget.DropFor(int step) => ground.At(Site.Drop.X, Site.Drop.Z);

    Vector3 ITripTarget.DropRoadFor(int step) => Site.Road;

    string ITripTarget.CounterFor(int step) => "";

    bool ITripTarget.DropsAtContractor(int step) => false;

    void ITripTarget.FinishDump(Game game, HaulTrip trip) => game.FinishBeaconDump(trip);
}
