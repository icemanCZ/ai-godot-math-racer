using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The dragon's ledge (<c>docs/DRAGON_PLAN.md</c>): slabs of grey rock he lies on, his cave in a rocky mound behind him
/// with a little heap of gold in its mouth, and in front of his head the nest, which six deliveries raise
/// (<see cref="SetStage"/>): a bed of sand in two loads, a ring of stones in two and, beside the nest on the side away from
/// the homestead, a fire laid in two loads of logs. His cold egg lies in the nest and warms up as it grows. After the
/// riddle (<see cref="Riddle"/>) the fire burns (<see cref="Hatched"/>), the egg has hatched and only its shells are left.
/// The node stands at <see cref="DragonSite.Lair"/>, turned by <see cref="DragonSite.Yaw"/>, so its frame is the lair's.
/// </summary>
public partial class DragonLair : Node3D
{
    private const string Rock = "8d8a85",
        RockDark = "77736d",
        Sand = "e8cf8f";

    private readonly Terrain ground;
    private readonly Basis basis;
    private readonly Node3D nest = new() { Name = "Nest" };
    private readonly Node3D fire = new() { Name = "FirePit" };
    private readonly Node3D[] parts = new Node3D[DragonPlans.Trips + 1];
    private readonly List<CollisionShape3D> woodpile = new();
    private readonly Node3D egg = new() { Name = "Egg" };
    private readonly Node3D shells = new() { Name = "Shells", Visible = false };
    private readonly Node3D flames = new() { Name = "Flames", Visible = false };
    private readonly List<MeshInstance3D> tongues = new();
    private readonly OmniLight3D light = new()
    {
        Name = "FireLight",
        LightColor = new Color("ff9a3c"),
        OmniRange = 20,
        LightEnergy = 0,
        ShadowEnabled = false,
        Visible = false,
    };
    private StandardMaterial3D eggShell = null!;
    private float time;
    private bool hatchShown = true;

    internal DragonSite Site { get; }

    /// <summary>The deliveries the nest has had, 0 to <see cref="DragonPlans.Trips"/>.</summary>
    internal int Stage { get; private set; }

    internal bool Complete => Stage >= DragonPlans.Trips;

    /// <summary>The answers of the riddle given so far, 0 to <see cref="DragonPlans.Riddles"/>.</summary>
    internal int Riddle { get; private set; }

    /// <summary>The riddle is solved: the dragon has sneezed his fire back, the fire burns and the egg has hatched.</summary>
    internal bool Hatched => Riddle >= DragonPlans.Riddles;

    /// <summary>Whether the fire is burning at the moment, for the checks.</summary>
    internal bool Burning => flames.Visible;

    /// <summary>Where the dragon lies: on the ledge, along its slope, his head towards the nest.</summary>
    internal Transform3D DragonRest { get; }

    /// <summary>How high above the talking place under his chin his head is.</summary>
    internal float HeadHeight { get; }

    /// <summary>Where the little dragon waits beside the nest until it hatches.</summary>
    internal Vector3 LittleHome { get; }

    /// <summary>The middle of the nest a little above it, for the camera and the celebration.</summary>
    internal Vector3 Focus => Position + basis * (nest.Position + Vector3.Up * 1.4f);

    internal DragonLair(DragonSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "DragonLair";
        Position = site.Lair;
        Rotation = new Vector3(0, site.Yaw, 0);
        basis = new Basis(Vector3.Up, site.Yaw);
        // The dragon lies along the slope of his ledge, his head a little lower than his tail where it rises.
        float front = Floor(0, 4),
            back = Floor(0, -7);
        float pitch = Mathf.Clamp(Mathf.Atan2(back - front, 11), -.22f, .22f);
        float baseY = Mathf.Max(Floor(0, 0), (front + back) / 2) + .1f;
        DragonRest = new Transform3D(basis * new Basis(Vector3.Right, pitch), Position + Vector3.Up * baseY);
        var head = DragonRest * (new Vector3(0, 5.4f, 6.2f) * Characters.Dragon.Size);
        HeadHeight = Mathf.Clamp(head.Y - ground.Height(site.Talk.X, site.Talk.Z), 3, 12);
        var home = site.Local(-site.FireSide * 3.4f, DragonSite.NestAhead + .4f);
        LittleHome = ground.At(home.X, home.Z);
        for (int i = 0; i <= DragonPlans.Trips; i++)
        {
            parts[i] = new Node3D { Name = "NestStage" + i };
            AddChild(parts[i]);
        }
        BuildLedge(baseY, pitch);
        BuildCave();
        nest.Position = At(0, DragonSite.NestAhead);
        AddChild(nest);
        fire.Position = At(site.FireSide * 4.4f, DragonSite.NestAhead - .8f);
        AddChild(fire);
        BuildNest();
        BuildFire();
        SetStage(0);
    }

    /// <summary>Height above the node of the ground under a point of the lair's frame.</summary>
    private float Floor(float x, float z)
    {
        var p = Position + basis * new Vector3(x, 0, z);
        return ground.Height(p.X, p.Z) - Position.Y;
    }

    /// <summary>A point of the lair's frame on the ground, lifted by <paramref name="lift"/>.</summary>
    private Vector3 At(float x, float z, float lift = 0) => new(x, Floor(x, z) + lift, z);

    /// <summary>
    /// The slabs of rock he lies on, their tops along his body and their feet sunk into the ground, and a few boulders round
    /// them; one solid box keeps the car off him and his tail.
    /// </summary>
    private void BuildLedge(float baseY, float pitch)
    {
        float Top(float z) => baseY + .35f - z * Mathf.Tan(pitch);
        foreach (
            var (x, z, rx, rz, color) in new[]
            {
                (0f, 0f, 3.8f, 5.4f, Rock),
                (-.6f, -6.5f, 3.6f, 3.2f, RockDark),
                (1.2f, 3.6f, 2.6f, 2.4f, Rock),
                (-2.6f, -9.5f, 2.6f, 2.6f, Rock),
            }
        )
        {
            float top = Top(z);
            float low = Mathf.Min(Floor(x, z), Mathf.Min(Floor(x - rx, z), Floor(x + rx, z)));
            low = Mathf.Min(low, Mathf.Min(Floor(x, z - rz), Floor(x, z + rz)));
            float ry = Mathf.Max(1, (top - low) + .6f);
            var slab = Art.Ball(this, new(x, top - ry, z), new(rx, ry, rz), color);
            slab.Rotation = new Vector3(pitch, 0, 0);
        }
        foreach (
            var (x, z, size) in new[] { (5.3f, -3f, 1.1f), (-5.3f, 1.5f, .9f), (4.7f, 4.8f, .7f), (-5.8f, -5.5f, 1.3f) }
        )
            Art.Ball(this, At(x, z, size * .3f), new(size * 1.2f, size, size), RockDark);
        float height = 6f;
        Obstacles.Box(this, new(0, baseY + height / 2, -1.2f), new(7.4f, height, 12));
        // The tail curls round towards his left.
        Obstacles.Box(this, new(-2.8f, Top(-7) + 1, -8.4f), new(6, 2.2f, 7.5f));
    }

    /// <summary>The rocky mound behind him with the dark mouth of his cave and a heap of gold coins glinting in it.</summary>
    private void BuildCave()
    {
        float floor = Floor(0, -12.5f);
        Art.Ball(this, new(0, floor + 1.2f, -13.6f), new(6.8f, 5.4f, 4.4f), RockDark);
        Art.Ball(this, new(-4.6f, floor + .6f, -12.4f), new(3.2f, 3.6f, 3.0f), Rock);
        Art.Ball(this, new(4.8f, floor + .4f, -12.8f), new(3.0f, 3.0f, 2.8f), Rock);
        Art.Ball(this, new(0, floor + 2.0f, -9.45f), new(2.5f, 2.3f, .45f), "2b2622");
        foreach (
            var (x, y, z) in new[]
            {
                (-.7f, .3f, -9.1f),
                (.5f, .25f, -9.0f),
                (0f, .55f, -9.2f),
                (.9f, .2f, -8.9f),
                (-.2f, .2f, -8.8f),
            }
        )
            Art.Ball(this, new(x, floor + y, z), new(.42f, .22f, .42f), "f2c14e");
        Art.Ball(this, new(.2f, floor + .95f, -9.2f), new(.22f, .26f, .16f), "e65c8a");
        Obstacles.Box(this, new(0, floor + 2.5f, -13.6f), new(13, 5, 7));
    }

    /// <summary>The hollow in the rock, the egg, and the sand and the stones that the deliveries bring.</summary>
    private void BuildNest()
    {
        // The bare hollow: a few pebbles round where the nest will be.
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10 + .2f;
            Art.Ball(
                parts[0],
                nest.Position + new Vector3(Mathf.Sin(a) * 1.7f, .1f, Mathf.Cos(a) * 1.7f),
                new(.22f, .14f, .2f),
                RockDark
            );
        }
        // One: a heap of sand; two: the nest itself, a soft rim round a hollow.
        Art.Ball(parts[1], nest.Position + Vector3.Up * .02f, new(2.1f, .32f, 2.1f), Sand);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            Art.Ball(
                parts[2],
                nest.Position + new Vector3(Mathf.Sin(a) * 1.55f, .32f, Mathf.Cos(a) * 1.55f),
                new(.72f, .42f, .72f),
                "e2c27e"
            );
        }
        Art.Ball(parts[2], nest.Position + Vector3.Up * .3f, new(1.2f, .18f, 1.2f), "d9b46c");
        // Three and four: a ring of stones round the nest that keeps the warmth in.
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.Tau / 12;
            float size = .42f + .08f * Mathf.Sin(i * 2.3f);
            Art.Ball(
                parts[i < 6 ? 3 : 4],
                At(Mathf.Sin(a) * 2.7f, DragonSite.NestAhead + Mathf.Cos(a) * 2.7f, size * .5f),
                new(size * 1.15f, size, size),
                i % 3 == 0 ? RockDark : Rock
            );
        }
        // The egg: cream with green and orange spots, cold and pale blue at first.
        nest.AddChild(egg);
        eggShell = new StandardMaterial3D { AlbedoColor = new Color("cfdbe8").SrgbToLinear(), Roughness = .7f };
        var shell = Art.Ball(egg, new(0, .78f, 0), new(.6f, .78f, .6f), "f6e7c8");
        shell.MaterialOverride = eggShell;
        foreach (
            var (x, y, z, color) in new[]
            {
                (.42f, .95f, .38f, "7fbf6a"),
                (-.5f, .6f, .25f, "f2a14e"),
                (.1f, 1.25f, -.52f, "7fbf6a"),
                (-.2f, .4f, -.55f, "f2a14e"),
                (.55f, .45f, -.15f, "7fbf6a"),
            }
        )
            Art.Ball(egg, new Vector3(x * .96f, y, z * .96f), new(.12f, .12f, .12f), color);
        // The shells that are left when the little one has hatched.
        nest.AddChild(shells);
        foreach (var (x, z, tilt) in new[] { (-.45f, .1f, .9f), (.5f, -.2f, -1.1f) })
        {
            var half = Art.Ball(shells, new(x, .45f, z), new(.55f, .32f, .55f), "f6e7c8");
            half.Rotation = new Vector3(0, 0, tilt);
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.Tau / 5;
                Art.Cylinder(half, new(Mathf.Sin(a) * .8f, .9f, Mathf.Cos(a) * .8f), .2f, .01f, .5f, "f6e7c8");
            }
        }
    }

    /// <summary>The fire pit beside the nest: a ring of stones, logs laid across it and then stood up round it, and the flames.</summary>
    private void BuildFire()
    {
        for (int i = 0; i < 9; i++)
        {
            float a = i * Mathf.Tau / 9;
            Art.Ball(
                parts[5],
                fire.Position + new Vector3(Mathf.Sin(a) * 1.3f, .15f, Mathf.Cos(a) * 1.3f),
                new(.3f, .22f, .28f),
                RockDark
            );
        }
        foreach (float a in new[] { .3f, 1.35f, 2.4f })
            Art.Log(
                parts[5],
                fire.Position + Vector3.Up * .25f,
                new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)),
                .16f,
                2.1f,
                "8b5a35"
            );
        // Six logs stood up into a tepee.
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.Tau / 6 + .25f;
            var foot = new Vector3(Mathf.Sin(a) * .95f, .1f, Mathf.Cos(a) * .95f);
            var top = new Vector3(0, 1.75f, 0);
            Art.Log(
                parts[6],
                fire.Position + (foot + top) / 2,
                top - foot,
                .13f,
                foot.DistanceTo(top),
                i % 2 == 0 ? "7d4f2e" : "8b5a35"
            );
        }
        var body = Obstacles.Add(
            parts[5],
            fire.Position + Vector3.Up * .7f,
            new BoxShape3D { Size = new(2.8f, 1.4f, 2.8f) }
        );
        woodpile.Add((CollisionShape3D)body.GetChild(0));
        // The flames: three cones one inside the other and little tongues round them, and the glow of the embers.
        fire.AddChild(flames);
        foreach (
            var (radius, height, color) in new[]
            {
                (.75f, 2.4f, "ff6a1a"),
                (.5f, 1.9f, "ffb02e"),
                (.28f, 1.3f, "fff2a0"),
            }
        )
        {
            var cone = Art.Cylinder(flames, new(0, .2f + height / 2, 0), radius, .03f, height, color);
            cone.MaterialOverride = Glow(color, .9f);
            cone.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            tongues.Add(cone);
        }
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5;
            // A cylinder, not a ball: the flicker sets its scale round one.
            var tongue = Art.Cylinder(
                flames,
                new(Mathf.Sin(a) * .55f, .75f, Mathf.Cos(a) * .55f),
                .2f,
                .02f,
                1.3f,
                "ff8a2a"
            );
            tongue.Rotation = new Vector3(Mathf.Cos(a) * .25f, 0, -Mathf.Sin(a) * .25f);
            tongue.MaterialOverride = Glow("ff8a2a", .9f);
            tongue.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            tongues.Add(tongue);
        }
        var embers = Art.Ball(flames, new(0, .15f, 0), new(.85f, .1f, .85f), "ff4a1a");
        embers.MaterialOverride = Glow("ff4a1a");
        flames.AddChild(light);
        light.Position = Vector3.Up * 1.6f;
    }

    private static StandardMaterial3D Glow(string color, float alpha = 1) =>
        new()
        {
            AlbedoColor = new Color(color).SrgbToLinear() with { A = alpha },
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = alpha < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        };

    /// <summary>Raises the nest to a number of deliveries: at once from a save, or after a load is tipped out.</summary>
    internal void SetStage(int stage)
    {
        Stage = Math.Clamp(stage, 0, DragonPlans.Trips);
        parts[0].Visible = Stage < 2;
        for (int i = 1; i <= DragonPlans.Trips; i++)
            parts[i].Visible = i <= Stage;
        // The heap of sand gives way to the nest itself.
        parts[1].Visible = Stage == 1;
        foreach (var shape in woodpile)
            shape.Disabled = Stage < 5;
        ShowEgg();
    }

    /// <summary>
    /// Puts the answers of the riddle given so far; after the third the fire burns and the egg has hatched. With
    /// <paramref name="shown"/> false the egg stays as it was until <see cref="ShowHatched"/>, when the dragon's sneeze
    /// lights the fire.
    /// </summary>
    internal void SetRiddle(int riddle, bool shown = true)
    {
        Riddle = Complete ? Math.Clamp(riddle, 0, DragonPlans.Riddles) : 0;
        hatchShown = shown;
        ShowEgg();
    }

    /// <summary>The fire catches and the little dragon comes out of its egg.</summary>
    internal void ShowHatched()
    {
        hatchShown = true;
        ShowEgg();
    }

    /// <summary>The egg lies on its side on the bare rock, sits up in the sand and warms up, and is shells once it has hatched.</summary>
    private void ShowEgg()
    {
        bool hatched = Hatched && hatchShown;
        egg.Visible = !hatched;
        shells.Visible = hatched;
        flames.Visible = hatched;
        light.Visible = hatched;
        bool sitting = Stage >= 1;
        egg.Position = sitting ? new Vector3(0, Stage >= 2 ? .15f : 0, 0) : new Vector3(0, -.15f, 0);
        egg.Rotation = sitting ? Vector3.Zero : new Vector3(0, 0, 1.25f);
        float warm = Stage / (float)DragonPlans.Trips;
        eggShell.AlbedoColor = new Color("cfdbe8").Lerp(new Color("fbe3b8"), warm).SrgbToLinear();
    }

    /// <summary>The flames flicker, the light glows by night and a warm egg rocks, the more answers of the riddle the more.</summary>
    internal void Tick(float delta, float darkness)
    {
        time += delta;
        if (flames.Visible)
        {
            for (int i = 0; i < tongues.Count; i++)
            {
                float flicker =
                    1 + .14f * Mathf.Sin(time * (9 + i * 2.3f) + i) + .07f * Mathf.Sin(time * 23 + i * 1.7f);
                tongues[i].Scale = new Vector3(1, flicker, 1) * (i < 3 ? 1 : .8f + .3f * Mathf.Sin(time * 5 + i));
            }
            light.LightEnergy = .4f + darkness * 2.6f + .25f * Mathf.Sin(time * 13);
        }
        if (egg.Visible && Stage >= 2)
        {
            float rock = (Complete ? .06f + Riddle * .07f : .02f) * Mathf.Sin(time * (3 + Riddle * 2));
            egg.Rotation = new Vector3(rock * .6f, 0, rock);
        }
    }
}
