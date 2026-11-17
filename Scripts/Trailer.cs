using System;
using System.Collections.Generic;
using Godot;

namespace MathRacer;

/// <summary>Geometry shared by the towed trailer and its garage preview. Local -Z points to the hitch.</summary>
internal static class TrailerModel
{
    internal static readonly (string Id, string Name, string Color)[] Paints =
    {
        ("red", "Červený vozík", "c0573e"),
        ("blue", "Modrý vozík", "4f8fc4"),
    };

    internal static string PaintColor(string id) =>
        Array.Find(Paints, paint => paint.Id == id).Color ?? Paints[0].Color;

    internal sealed class Parts
    {
        internal Node3D Bed = null!;
        internal Node3D Load = null!;
        internal readonly Node3D[] Piles = new Node3D[3];
        internal readonly List<Node3D> Wheels = new();
        internal readonly List<MeshInstance3D> Painted = new();
    }

    internal static Parts Build(Node3D root, string color)
    {
        var parts = new Parts();
        foreach (float x in new[] { -.98f, .98f })
        {
            var wheel = new Node3D { Name = x < 0 ? "LeftWheel" : "RightWheel", Position = new(x, .45f, 0) };
            root.AddChild(wheel);
            Art.Cylinder(wheel, Vector3.Zero, .45f, .45f, .26f, "2f3236").RotationDegrees = new(0, 0, 90);
            Art.Cylinder(wheel, Vector3.Zero, .22f, .22f, .28f, "d8d2c4").RotationDegrees = new(0, 0, 90);
            Art.Block(wheel, Vector3.Zero, new(.3f, .08f, .7f), "8d8a83");
            parts.Wheels.Add(wheel);
        }
        Art.Block(root, new(0, .52f, 0), new(1.9f, .1f, .14f), "5a5650");
        Art.Beam(root, new(0, .55f, -.9f), new(0, .5f, -2.55f), .12f, "5a5650");
        Art.Ball(root, new(0, .5f, -2.6f), Vector3.One * .13f, "35332f");
        // The bed tips about its rear edge to pour out the load.
        parts.Bed = new Node3D { Name = "Bed", Position = new(0, .72f, 1.05f) };
        root.AddChild(parts.Bed);
        parts.Painted.Add(Art.Block(parts.Bed, new(0, 0, -1.05f), new(1.75f, .12f, 2.15f), color));
        foreach (float x in new[] { -.85f, .85f })
            parts.Painted.Add(Art.Block(parts.Bed, new(x, .3f, -1.05f), new(.08f, .55f, 2.15f), color));
        parts.Painted.Add(Art.Block(parts.Bed, new(0, .3f, -2.1f), new(1.75f, .55f, .08f), color));
        Art.Block(parts.Bed, new(0, .3f, -.02f), new(1.75f, .5f, .07f), "e2bb8b");
        foreach (float x in new[] { -.86f, .86f })
            Art.Block(parts.Bed, new(x, .6f, -1.05f), new(.1f, .06f, 2.2f), "e2bb8b");
        parts.Load = new Node3D { Name = "TrailerLoad", Position = new(0, .1f, -1.05f) };
        parts.Bed.AddChild(parts.Load);
        var stones = new Node3D { Name = "Stones" };
        string[] greys = { "8f8f8a", "a7a6a0", "77756f" };
        for (int i = 0; i < 10; i++)
        {
            float x = -.55f + (i % 4) * .37f,
                z = -.7f + (i / 4) * .65f,
                size = .26f + (i * 7 % 5) * .03f;
            Art.Ball(stones, new(x, .18f + (i % 3) * .06f, z), new(size, size * .8f, size), greys[i % 3]);
        }
        Art.Ball(stones, new(0, .42f, 0), new(.34f, .26f, .32f), greys[1]);
        parts.Piles[(int)HaulLoad.Stones] = stones;
        parts.Piles[(int)HaulLoad.Soil] = Mound("Soil", "8a6142", "6f4b33");
        parts.Piles[(int)HaulLoad.Sand] = Mound("Sand", "e3c98a", "d6b873");
        foreach (var pile in parts.Piles)
        {
            pile.Visible = false;
            parts.Load.AddChild(pile);
        }
        return parts;
    }

    private static Node3D Mound(string name, string color, string shade)
    {
        var mound = new Node3D { Name = name };
        Art.Block(mound, new(0, .14f, 0), new(1.6f, .26f, 2), color);
        Art.Ball(mound, new(0, .32f, 0), new(.75f, .3f, .9f), color);
        Art.Ball(mound, new(-.35f, .3f, .45f), new(.4f, .18f, .4f), shade);
        Art.Ball(mound, new(.4f, .28f, -.5f), new(.38f, .16f, .38f), shade);
        return mound;
    }

    internal static void Paint(Parts parts, string color)
    {
        foreach (var mesh in parts.Painted)
            mesh.MaterialOverride = Art.Mat(color);
    }
}

/// <summary>
/// A trailer that follows the car's tow hitch along its path. It has no collision of its own; the
/// axle trails the hitch like a real drawbar and keeps to the shared driving surface.
/// </summary>
public partial class Trailer : Node3D
{
    /// <summary>Distance from the car's centre to the drawbar ball; the car's rear bumper ends near 1.65.</summary>
    private const float HitchOffset = 1.7f;
    private const float DrawbarLength = 2.6f;
    private const float MaxHitchAngle = 75 * Mathf.Pi / 180;
    private const float FillTime = .8f;
    private const float RaiseTime = .9f;
    private const float PourTime = .7f;
    private const float LowerTime = .7f;
    private readonly World world;
    private readonly Vehicle car;
    private TrailerModel.Parts parts = null!;
    private CpuParticles3D dust = null!;
    private float fill = 1;
    private float dumpTime = -1;
    private Action? dumped;
    private float bounce;
    private float bounceVelocity;
    private float lastPitch;
    private float shake;
    private string paint = TrailerModel.Paints[0].Id;

    internal HaulLoad? Load { get; private set; }
    internal bool Attached => Visible;
    internal bool Dumping => dumpTime >= 0;
    internal string PaintId => paint;

    internal Trailer(World world, Vehicle car)
    {
        this.world = world;
        this.car = car;
        Name = "Trailer";
        Visible = false;
    }

    public override void _Ready()
    {
        parts = TrailerModel.Build(this, TrailerModel.PaintColor(paint));
        dust = new CpuParticles3D
        {
            Name = "SoilDust",
            Emitting = false,
            Amount = 28,
            Lifetime = 1.3f,
            Position = new(0, .9f, .6f),
            Mesh = new SphereMesh
            {
                Radius = .18f,
                Height = .36f,
                RadialSegments = 6,
                Rings = 3,
            },
            Direction = new(0, .4f, 1),
            Spread = 35,
            InitialVelocityMin = .6f,
            InitialVelocityMax = 1.6f,
            Gravity = new(0, .25f, 0),
            ScaleAmountMin = .6f,
            ScaleAmountMax = 1.4f,
            LocalCoords = false,
        };
        dust.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(.62f, .47f, .33f, .45f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        AddChild(dust);
        car.PoseReset += Snap;
        ShowLoad();
    }

    public override void _ExitTree() => car.PoseReset -= Snap;

    internal void Attach(bool attached)
    {
        Visible = attached;
        if (attached)
            Snap();
        else
            dust.Emitting = false;
        UpdateLoadFactor();
    }

    internal void SetPaint(string id)
    {
        paint = Array.Exists(TrailerModel.Paints, p => p.Id == id) ? id : TrailerModel.Paints[0].Id;
        if (parts != null)
            TrailerModel.Paint(parts, TrailerModel.PaintColor(paint));
    }

    /// <summary>Changes the load at once, as when restoring a save.</summary>
    internal void SetLoad(HaulLoad? load)
    {
        Load = load;
        fill = 1;
        dumpTime = -1;
        dumped = null;
        if (parts != null)
        {
            parts.Bed.Rotation = Vector3.Zero;
            parts.Load.Position = new(0, .1f, -1.05f);
        }
        ShowLoad();
        UpdateLoadFactor();
    }

    /// <summary>Pours a load into the empty bed with a short growing pile.</summary>
    internal void Fill(HaulLoad load)
    {
        SetLoad(load);
        fill = 0;
        ShowLoad();
    }

    /// <summary>Tips the bed and calls <paramref name="onDumped"/> once the load has poured out.</summary>
    internal void Dump(Action onDumped)
    {
        if (Load == null || Dumping)
            return;
        dumpTime = 0;
        dumped = onDumped;
    }

    /// <summary>Places the axle straight behind the hitch, as after a reset or a loaded save.</summary>
    internal void Snap()
    {
        var back = Flat(car.Transform.Basis.Z);
        if (back.LengthSquared() < .001f)
            back = Vector3.Back;
        var hitch = car.Position + back.Normalized() * HitchOffset;
        Place(hitch + back.Normalized() * DrawbarLength, hitch, 0);
    }

    internal void Tick(float delta)
    {
        if (!Visible || parts == null)
            return;
        var back = Flat(car.Transform.Basis.Z).Normalized();
        var hitch = car.Position + back * HitchOffset;
        var trail = Flat(Position - hitch);
        if (trail.LengthSquared() > (DrawbarLength + 4) * (DrawbarLength + 4) || trail.LengthSquared() < .0001f)
        {
            Snap();
            return;
        }
        var direction = trail.Normalized();
        // Limit the hitch angle so reversing cannot fold the trailer into the car.
        float angle = back.SignedAngleTo(direction, Vector3.Up);
        if (Mathf.Abs(angle) > MaxHitchAngle)
            direction = back.Rotated(Vector3.Up, Mathf.Sign(angle) * MaxHitchAngle);
        var previous = Position;
        Place(hitch + direction * DrawbarLength, hitch, delta);
        float rolled = Flat(Position - previous).Length();
        float sign = Flat(Position - previous).Dot(direction) > 0 ? 1 : -1;
        foreach (var wheel in parts.Wheels)
            wheel.RotateX(sign * rolled / .45f);
        AnimateLoad(delta, rolled / Mathf.Max(delta, .0001f));
    }

    private void Place(Vector3 axle, Vector3 hitch, float delta)
    {
        var forward = Flat(hitch - axle);
        if (forward.LengthSquared() < .0001f)
            forward = Vector3.Forward;
        forward = forward.Normalized();
        var right = forward.Cross(Vector3.Up);
        float Height(Vector3 p) => world.DriveSurface(p.X, p.Z);
        axle.Y = Height(axle);
        float pitch = Mathf.Atan((Height(axle + forward * 1.2f) - Height(axle - forward * 1.2f)) / 2.4f);
        float roll = Mathf.Atan((Height(axle + right * .95f) - Height(axle - right * .95f)) / 1.9f);
        Position = axle;
        Rotation = new Vector3(pitch, Mathf.Atan2(-forward.X, -forward.Z), roll);
        if (delta <= 0)
        {
            lastPitch = pitch;
            bounce = bounceVelocity = 0;
            return;
        }
        // Sudden changes of slope set the load bouncing on a soft spring.
        bounceVelocity += (pitch - lastPitch) * 9 - bounce * 140 * delta - bounceVelocity * 9 * delta;
        bounce = Mathf.Clamp(bounce + bounceVelocity * delta, -.09f, .09f);
        lastPitch = pitch;
    }

    private void AnimateLoad(float delta, float speed)
    {
        shake += delta * (14 + speed * 1.5f);
        float rattle = Mathf.Sin(shake) * .012f * Mathf.Min(speed / 8, 1);
        if (fill < 1)
            fill = Mathf.Min(1, fill + delta / FillTime);
        float grown = Mathf.SmoothStep(0, 1, fill);
        parts.Load.Scale = new Vector3(1, Mathf.Max(.05f, grown), 1);
        if (Dumping)
        {
            dumpTime += delta;
            float raise = Mathf.Clamp(dumpTime / RaiseTime, 0, 1);
            float lower = Mathf.Clamp((dumpTime - RaiseTime - PourTime) / LowerTime, 0, 1);
            parts.Bed.Rotation = new Vector3(Mathf.SmoothStep(0, 1, raise - lower) * .85f, 0, 0);
            float pour = Mathf.Clamp((dumpTime - RaiseTime * .6f) / PourTime, 0, 1);
            parts.Load.Position = new Vector3(0, .1f, -1.05f + pour * 1.6f);
            parts.Load.Scale = new Vector3(1, Mathf.Max(.05f, 1 - pour), 1);
            if (pour >= 1 && Load != null)
            {
                Load = null;
                ShowLoad();
                UpdateLoadFactor();
                var callback = dumped;
                dumped = null;
                callback?.Invoke();
            }
            if (dumpTime >= RaiseTime + PourTime + LowerTime)
            {
                dumpTime = -1;
                parts.Bed.Rotation = Vector3.Zero;
                parts.Load.Position = new(0, .1f, -1.05f);
                parts.Load.Scale = Vector3.One;
            }
        }
        else
            parts.Load.Position = new(0, .1f + bounce + rattle, -1.05f);
        dust.Emitting = Load == HaulLoad.Soil && speed > 3 && !Dumping;
    }

    private void ShowLoad()
    {
        if (parts == null)
            return;
        for (int i = 0; i < parts.Piles.Length; i++)
            parts.Piles[i].Visible = Load == (HaulLoad)i;
    }

    private void UpdateLoadFactor() => car.LoadFactor = Visible && Load != null ? Settings.LoadedTrailerFactor : 1;

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
