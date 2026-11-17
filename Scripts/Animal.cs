using Godot;

namespace MathRacer;

public partial class Animal : Node3D
{
    /// <summary>Assigned before the character enters the scene; roadside friends keep the default.</summary>
    internal ActorRole Role { get; set; } = RoadsideFriendRole.Instance;
    public bool IsFarmer => Role is FarmerRole;
    public bool IsWitch => Role is WitchRole;
    public bool IsSorcerer => Role is SorcererRole;
    public bool IsGardener => Role is GardenerRole;
    public bool IsGemCollector => Role is ExhibitCollectorRole { Toys: false };
    public bool IsToymaker => Role is ExhibitCollectorRole { Toys: true };
    public bool IsCollectionOwner => Role is ExhibitCollectorRole;
    public int TradeIndex => Role is TraderRole trader ? trader.Index : -1;
    public bool IsTrader => Role is TraderRole;
    public int HouseIndex => Role is HouseResidentRole resident ? resident.House : -1;
    private Node3D wavingArm = null!;
    internal bool IsWaving { get; private set; }

    public void GreetPlayer(Vector3 player, bool active)
    {
        var markerDistance = Position - player;
        markerDistance.Y = 0;
        markerDistanceBlend = Mathf.SmoothStep(8, 32, markerDistance.Length());
        IsWaving = active && Position.DistanceSquaredTo(player) < 24 * 24;
        if (IsWaving)
        {
            var direction = player - Position;
            if (direction.LengthSquared() > .1f)
                Rotation = new(0, Mathf.Atan2(direction.X, direction.Z), 0);
        }
    }

    public string DisplayName { get; private set; } = "";
    public string Greeting { get; private set; } = "";
    private Node3D model = null!;
    private float time;
    private float celebration;
    internal bool AnimationPaused;
    internal bool Running;

    public void Celebrate() => celebration = 2.5f;

    private Node3D? beacon;
    private float pingTime;

    /// <summary>Lights a tall beam over the character for a while, as the lookout tower does for waiting friends.</summary>
    internal void Ping(float seconds)
    {
        beacon ??= Art.Beacon(this, "7fe8ff");
        beacon.Visible = true;
        pingTime = seconds;
    }

    internal bool Pinged => pingTime > 0;

    private TaskMarker taskMarker = null!;
    private Label3D nameLabel = null!;
    private Sprite3D? roleIcon;
    private float markerDistanceBlend = 1;
    private float markerBlend = 1;
    public bool RewardAvailable { get; private set; } = true;
    public int Kind { get; private set; }
    public string FurColor { get; private set; } = "";

    public override void _Ready()
    {
        if (Role.Icon != "")
            roleIcon = Art.RoleTaskIcon(taskMarker, Role.Icon);
        Guide(taskMarker.Visible);
    }

    public void Guide(bool visible, bool destination = false)
    {
        taskMarker.Visible = visible;
        taskMarker.Text =
            destination ? "!"
            : roleIcon != null ? ""
            : "?";
        if (roleIcon != null)
            roleIcon.Visible = !destination;
        taskMarker.Modulate = new Color(destination ? "63e7e0" : "ffeb3b");
    }

    public void SetRewardAvailable(bool available)
    {
        RewardAvailable = available;
        taskMarker.Visible = available;
    }

    internal static Animal Create(CharacterSpec spec) => Create(spec.Name, spec.Greeting, spec.Kind, spec.Fur);

    public static Animal Create(string name, string greeting, int kind, string? furColor = null)
    {
        var a = new Animal { DisplayName = name, Greeting = greeting };
        a.model = new Node3D();
        a.AddChild(a.model);
        string color = furColor
            ?? (
                kind == 0 ? "f3e8d5"
                : kind == 1 ? "e9a16a"
                : "b98d71"
            );
        a.Kind = kind;
        a.FurColor = color;
        Art.Ball(a.model, new(0, .8f, 0), new(.65f, .85f, .55f), color);
        Art.Ball(a.model, new(0, 1.8f, 0), new(.82f, .75f, .65f), color);
        Art.Ball(a.model, new(0, 1.5f, .5f), new(.5f, .33f, .26f), "fff0d9");
        foreach (float x in new[] { -.36f, .36f })
        {
            var ear = Art.Ball(
                a.model,
                new(x, kind == 0 ? 2.9f : 2.4f, 0),
                kind == 0 ? new(.23f, .8f, .22f) : new Vector3(.29f, kind == 1 ? .5f : .3f, .25f),
                color
            );
            ear.RotationDegrees = new Vector3(0, 0, -x * 22);
            Art.Ball(
                a.model,
                new(x, kind == 0 ? 2.93f : 2.45f, .19f),
                kind == 0 ? new(.1f, .55f, .06f) : new Vector3(.14f, .2f, .06f),
                "eab4ad"
            );
            Art.Ball(a.model, new(x, 1.93f, .56f), new(.19f, .23f, .12f), "fffaf0");
            Art.Ball(a.model, new(x, 1.92f, .66f), new(.09f, .12f, .05f), "384d51");
            Art.Ball(a.model, new(x - .025f, 1.98f, .71f), Vector3.One * .035f, "ffffff");
            Art.Ball(a.model, new(x, .24f, .26f), new(.32f, .22f, .4f), color);
        }
        Art.Ball(a.model, new(0, 1.58f, .76f), new(.13f, .1f, .09f), "6c5557");
        if (kind == 1)
        {
            var tail = Art.Ball(a.model, new(.5f, .7f, -.65f), new(.3f, .5f, .8f), color);
            tail.RotationDegrees = new(25, 30, 0);
            Art.Ball(a.model, new(.8f, 1, -1.1f), new(.25f, .3f, .32f), "fff0d9");
        }
        a.wavingArm = new Node3D { Position = new(.58f, 1.2f, 0) };
        a.model.AddChild(a.wavingArm);
        Art.Ball(a.wavingArm, new(.32f, -.15f, 0), new(.4f, .23f, .24f), color);
        Art.Ball(a.wavingArm, new(.6f, -.22f, 0), new(.25f, .26f, .25f), color);
        Art.Ball(a.model, new(-.67f, 1.05f, 0), new(.25f, .4f, .24f), color);
        a.taskMarker = Art.TaskMarker(a, new(0, 7.5f, 0), "?");
        a.nameLabel = Art.Sign(a, new(0, 3.65f, 0), name, 30);
        return a;
    }

    internal void DressAsSorcerer() => DressAsWitch();

    /// <summary>A flat paddle tail, two front teeth and a builder's cap.</summary>
    internal void DressAsBeaver()
    {
        var tail = Art.Ball(model, new(0, .25f, -.95f), new(.42f, .1f, .75f), "5b4636");
        tail.RotationDegrees = new(-12, 0, 0);
        for (int i = 0; i < 3; i++)
            Art.Block(model, new(0, .27f, -.7f - i * .3f), new(.6f, .02f, .03f), "47362a");
        foreach (float x in new[] { -.06f, .06f })
            Art.Block(model, new(x, 1.39f, .76f), new(.1f, .16f, .04f), "fffaf0");
        Art.Cylinder(model, new(0, 2.38f, 0), .62f, .5f, .3f, "f2b33d");
        Art.Block(model, new(0, 2.27f, .55f), new(.7f, .06f, .35f), "e09a2c");
    }

    /// <summary>A hard hat with a brim and a work apron for the people at the sawmill, brickworks and gravel works.</summary>
    internal void DressAsWorker(string hat)
    {
        Art.Cylinder(model, new(0, 2.42f, 0), .6f, .48f, .28f, hat);
        Art.Block(model, new(0, 2.3f, .55f), new(.74f, .06f, .36f), hat);
        Art.Block(model, new(0, .95f, .46f), new(.78f, .8f, .12f), "56707a");
    }

    internal void DressAsBandit(string scarf)
    {
        nameLabel.Hide();
        Art.Cylinder(model, new(0, 1.27f, 0), .45f, .47f, .18f, scarf);
        Art.Ball(model, new(.38f, 1.13f, .12f), new(.16f, .25f, .12f), scarf).RotateZ(.42f);
        Art.Block(model, new(0, 2.19f, .47f), new(.99f, .13f, .26f), "5a4f65");
    }

    internal void DressAsWitch()
    {
        nameLabel.Position += Vector3.Up * .45f;
        Art.Cylinder(model, new(0, 2.46f, 0), .96f, .92f, .13f, "453758");
        Art.Cylinder(model, new(0, 2.95f, 0), .63f, .025f, 1.05f, "534165").RotationDegrees = new Vector3(0, 0, -10);
        Art.Cylinder(model, new(0, 2.56f, 0), .64f, .56f, .16f, "9d63a8");
        Art.Block(model, new(0, 2.57f, .61f), new(.21f, .15f, .065f), "ffd376");
    }

    public override void _Process(double delta)
    {
        if (AnimationPaused)
            return;
        time += (float)delta;
        if (pingTime > 0 && (pingTime -= (float)delta) <= 0 && beacon != null)
            beacon.Visible = false;
        wavingArm.Rotation = Running
            ? new(Mathf.Sin(time * 17) * .7f, 0, -.35f)
            : new(0, 0, (IsWaving || celebration > 0) ? .95f + Mathf.Sin(time * 9) * .48f : -.35f);
        markerBlend = Mathf.Lerp(markerBlend, markerDistanceBlend, 1 - Mathf.Exp(-(float)delta * 8));
        float markerSize = Mathf.Lerp(.5f, 1, markerBlend);
        taskMarker.PixelSize = .0011f * markerSize;
        if (roleIcon != null)
            roleIcon.PixelSize = .00055f * markerSize;
        taskMarker.AnchorPosition =
            Vector3.Up
            * (Mathf.Lerp(4.7f, 7.5f, markerBlend) + Mathf.Sin(time * 2.4f) * Mathf.Lerp(.15f, .6f, markerBlend));
        model.Position = Vector3.Up * Mathf.Sin(time * 1.8f) * .045f;
        if (Running)
            model.Position += Vector3.Up * Mathf.Abs(Mathf.Sin(time * 12)) * .24f;
        if (celebration > 0)
        {
            celebration = Mathf.Max(0, celebration - (float)delta);
            model.Position += Vector3.Up * Mathf.Abs(Mathf.Sin(celebration * 7)) * .65f;
        }
    }
}
