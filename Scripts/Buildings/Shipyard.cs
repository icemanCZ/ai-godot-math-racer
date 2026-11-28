using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The shipyard on the coast (<c>docs/SHIP_PLAN.md</c>): the shipwright's shed, a stack of planks and barrels and the
/// sign on the edge of the valley, the slipway down the beach into the water with the cradle the ship stands on and the
/// scaffolding round it while it is built, and the pier on piles out to where the launched ship lies at anchor. The
/// hedgehog carpenter Hoblík hammers on the scaffolding while there is something to build and fishes from the pier once
/// the ship floats. The buoys that show the way to the turtle island ride the waves out at sea.
/// </summary>
public sealed partial class Shipyard : Node3D
{
    private readonly ShipSite site;
    private readonly Terrain ground;
    private readonly Node3D cradle = new() { Name = "Cradle" };
    private readonly Node3D scaffold = new() { Name = "Scaffold" };
    private readonly Node3D hedgehog = new() { Name = "Hedgehog" };
    private readonly Node3D hammer = new() { Name = "HedgehogHammer" };
    private readonly Node3D rod = new() { Name = "FishingRod" };
    private readonly List<Node3D> buoys = new();
    private readonly List<float> rung = new();
    private float time;
    private float work;

    internal Shipyard(ShipSite site, Terrain ground)
    {
        this.site = site;
        this.ground = ground;
        Name = "Shipyard";
        BuildShed();
        BuildSlipway();
        BuildPier();
        BuildHedgehog();
        foreach (var buoy in site.Buoys)
            BuildBuoy(buoy);
    }

    /// <summary>Whether the cradle and the scaffolding still stand, for the checks.</summary>
    internal bool ScaffoldShown => scaffold.Visible;

    /// <summary>Where the ship stands on the slipway: its middle over the cradle, and its pitch down the beach.</summary>
    internal (Vector3 Position, float Yaw, float Pitch) SlipPose
    {
        get
        {
            var middle = site.Local(0, -9);
            var bow = site.Local(0, -9 + Ship.HalfLength);
            var stern = site.Local(0, -9 - Ship.HalfLength);
            float hBow = ground.Height(bow.X, bow.Z),
                hStern = ground.Height(stern.X, stern.Z);
            float y = Mathf.Max(ground.Height(middle.X, middle.Z), (hBow + hStern) / 2) + 1;
            return (middle with { Y = y }, site.Yaw, Mathf.Atan2(hStern - hBow, 2 * Ship.HalfLength));
        }
    }

    private Vector3 OnGround(Vector3 p) => ground.At(p.X, p.Z);

    private void BuildShed()
    {
        var shed = new Node3D { Name = "Shed", Position = OnGround(site.OnEdge(-6, -6)) };
        shed.Rotation = new Vector3(0, site.Yaw, 0);
        AddChild(shed);
        Art.Wall(shed, new(0, 1.4f, 0), new(5, 2.8f, 4), "b8865a", FacadeStyle.Plaster);
        var roof = Art.Block(shed, new(0, 3.05f, 0), new(5.6f, .25f, 4.6f), "6b4022");
        roof.RotationDegrees = new Vector3(-8, 0, 0);
        Art.Block(shed, new(0, 1.1f, 2.02f), new(1.6f, 2.2f, .1f), "6a4a30");
        Art.Window(shed, new(1.7f, 1.6f, 2.02f), new(.8f, .6f, .06f), "9ed0e6");
        var sign = Art.Sign(shed, new(0, 3.8f, 1.5f), "LODĚNICE", 40);
        sign.Modulate = new Color("fff5d9");
        // Planks stacked beside the shed and two barrels.
        for (int i = 0; i < 5; i++)
            Art.Block(shed, new(3.6f, .15f + i * .2f, .4f), new(1.2f, .16f, 3.6f), i % 2 == 0 ? "c49a66" : "b88c5a");
        foreach (float x in new[] { -3.2f, -3.9f })
            Art.Cylinder(shed, new(x, .5f, 1.6f), .38f, .38f, 1, "8a5a30");
        Obstacles.Box(shed, new(0, 1.4f, 0), new(5, 2.8f, 4));
    }

    private void BuildSlipway()
    {
        AddChild(cradle);
        AddChild(scaffold);
        // Two greased rails from the top of the beach into the water.
        foreach (float along in new[] { -1.3f, 1.3f })
        {
            var from = OnGround(site.Local(along, -26)) + Vector3.Up * .15f;
            var to = site.Local(along, 8) with { Y = ValleyBorders.SeaLevel - 1.2f };
            Art.Beam(this, from, to, .35f, "6b4a2e");
            for (float k = 0; k <= 1.001f; k += 1 / 8f)
            {
                var p = from.Lerp(to, k);
                Art.Block(this, p - Vector3.Up * .2f, new(.3f, .35f, .3f), "5e3c22");
            }
        }
        // The cradle under the keel, and the scaffolding of posts and boards round the hull.
        for (int k = -4; k <= 4; k++)
        {
            var p = OnGround(site.Local(0, -9 + k * 2.8f));
            Art.Block(cradle, p + Vector3.Up * .5f, new(2.4f, 1, .4f), "7a5434");
        }
        foreach (int side in new[] { -1, 1 })
            for (int k = -4; k <= 4; k += 2)
            {
                float seaward = -9 + k * 2.8f;
                var foot = OnGround(site.Local(side * (Ship.Beam(k * 2.8f) + 1.4f), seaward));
                Art.Block(scaffold, foot + Vector3.Up * 2.6f, new(.2f, 5.2f, .2f), "a07a4c");
                Art.Block(
                    scaffold,
                    foot + Vector3.Up * 3.1f + site.Along * side * -.5f,
                    new(1.2f, .1f, 2.8f),
                    "c49a66"
                ).Rotation = new Vector3(0, site.Yaw, 0);
            }
        // A pile of logs on the beach.
        var pile = OnGround(site.Local(9, -18));
        for (int i = 0; i < 4; i++)
            Art.Log(
                this,
                pile + new Vector3(0, .35f + (i / 3) * .6f, 0) + site.Along * (i % 3 - 1) * .7f,
                site.Seaward,
                .35f,
                5,
                "8a6040"
            );
    }

    private void BuildPier()
    {
        var pier = new Node3D { Name = "ShipPier" };
        AddChild(pier);
        float deck = ValleyBorders.SeaLevel + 1.1f;
        var foot = OnGround(site.PierFoot);
        var head = site.PierHead with { Y = deck };
        var start = foot with { Y = Mathf.Max(foot.Y + .3f, deck) };
        float length = (head - start).Length();
        var board = Art.Block(pier, (start + head) / 2, new(2.6f, .14f, length), "c49a66");
        board.Rotation = new Vector3(0, site.Yaw, 0);
        for (float k = 0; k <= length; k += 3.5f)
        {
            var p = start + (head - start).Normalized() * k;
            foreach (int side in new[] { -1, 1 })
                Art.Cylinder(pier, p + site.Along * side * 1.2f - Vector3.Up * 1.8f, .14f, .14f, 3.8f, "6b4a2e");
        }
        foreach (int side in new[] { -1, 1 })
            Art.Cylinder(pier, head + site.Along * side * 1.1f + Vector3.Up * .4f, .17f, .19f, 1, "6b4a2e");
    }

    /// <summary>Hoblík: a round brown body with spines, a pale snout and a hammer, or later a fishing rod.</summary>
    private void BuildHedgehog()
    {
        AddChild(hedgehog);
        Art.Ball(hedgehog, new(0, .45f, 0), new(.75f, .7f, .85f), "5e4a3a");
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10;
            var spine = Art.Block(
                hedgehog,
                new(Mathf.Cos(a) * .32f, .62f, Mathf.Sin(a) * .3f - .08f),
                new(.06f, .3f, .06f),
                "3a2e24"
            );
            spine.RotationDegrees = new Vector3(Mathf.Sin(a) * -40 - 15, 0, Mathf.Cos(a) * 40);
        }
        Art.Ball(hedgehog, new(0, .48f, .4f), new(.36f, .32f, .3f), "e8c9a0");
        Art.Ball(hedgehog, new(0, .48f, .58f), new(.1f, .08f, .08f), "2b2b2e");
        foreach (int side in new[] { -1, 1 })
            Art.Ball(hedgehog, new(side * .12f, .6f, .48f), new(.06f, .07f, .04f), "2b2b2e");
        hedgehog.AddChild(hammer);
        hammer.Position = new Vector3(.36f, .5f, .2f);
        Art.Beam(hammer, Vector3.Zero, new(0, 0, .5f), .05f, "8a6040");
        Art.Block(hammer, new(0, 0, .5f), new(.2f, .12f, .12f), "4a5258");
        hedgehog.AddChild(rod);
        rod.Position = new Vector3(.3f, .5f, .2f);
        Art.Beam(rod, Vector3.Zero, new(0, 1.6f, 1.8f), .04f, "8a6040");
        Art.Beam(rod, new(0, 1.6f, 1.8f), new(0, -1.2f, 2.1f), .01f, "f4f1ea");
        rod.Visible = false;
    }

    private void BuildBuoy(Vector3 at)
    {
        var buoy = new Node3D { Name = $"Buoy{buoys.Count}", Position = at with { Y = ValleyBorders.SeaLevel } };
        AddChild(buoy);
        Art.Cylinder(buoy, new(0, .2f, 0), .9f, .7f, 1.1f, "d23b2f");
        Art.Cylinder(buoy, new(0, .85f, 0), .5f, .5f, .3f, "f4f1ea");
        Art.Beam(buoy, new(0, .9f, 0), new(0, 2.6f, 0), .12f, "30363a");
        Art.Ball(buoy, new(0, 2.5f, 0), new(.45f, .45f, .45f), "e0b04a");
        Art.Lamp(buoy, new(0, 2.95f, 0), new(.22f, .22f, .22f), "ffe27a");
        buoys.Add(buoy);
        rung.Add(-1);
    }

    /// <summary>The cradle and the scaffolding stand until the ship is launched; Hoblík hammers on it, then fishes.</summary>
    internal void SetLaunched(bool launched, int stage)
    {
        cradle.Visible = !launched;
        scaffold.Visible = !launched;
        hammer.Visible = !launched;
        rod.Visible = launched;
        if (launched)
        {
            var seat = site.PierHead with { Y = ValleyBorders.SeaLevel + 1.2f } - site.Seaward * 1.2f;
            hedgehog.Position = seat;
            hedgehog.Rotation = new Vector3(0, site.Yaw, 0);
        }
        else
        {
            // He climbs the scaffolding as the hull grows, and stands by the cradle before it.
            int side = stage % 2 == 0 ? 1 : -1;
            float seaward = -9 + (stage % 5 - 2) * 2.8f;
            var foot = OnGround(site.Local(side * (Ship.HalfBeam + .9f), seaward));
            hedgehog.Position = foot + Vector3.Up * (stage >= 3 ? 3.2f : 0);
            hedgehog.Rotation = new Vector3(0, Mathf.Atan2(-site.Along.X * side, -site.Along.Z * side), 0);
        }
    }

    /// <summary>A delivery came: Hoblík hammers for a while.</summary>
    internal void Hammer() => work = 4;

    /// <summary>A buoy the ship passes rings: it rocks and its lamp flashes.</summary>
    internal void Ring(int buoy)
    {
        if (buoy >= 0 && buoy < rung.Count)
            rung[buoy] = 0;
    }

    internal int Buoys => buoys.Count;

    internal void Tick(float delta, float sea)
    {
        time += delta;
        if (work > 0)
            work = Mathf.Max(0, work - delta);
        hammer.Rotation = new Vector3(work > 0 ? -Mathf.Abs(Mathf.Sin(time * 7)) * 1.1f : -.3f, 0, 0);
        for (int i = 0; i < buoys.Count; i++)
        {
            var buoy = buoys[i];
            float swing = 0;
            if (rung[i] >= 0)
            {
                rung[i] += delta;
                swing = Mathf.Sin(rung[i] * 9) * .35f * Mathf.Max(0, 1 - rung[i] / 2.5f);
                if (rung[i] > 2.5f)
                    rung[i] = -1;
            }
            buoy.Position = buoy.Position with { Y = sea + Mathf.Sin(time * 1.3f + i) * .12f };
            buoy.Rotation = new Vector3(Mathf.Sin(time * .9f + i) * .05f + swing, 0, Mathf.Cos(time * .8f + i) * .05f);
        }
    }
}
