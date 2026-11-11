using Godot;
using System.Collections.Generic;

namespace MathRacer;

internal enum CollectorPetKind { Farm, Garden, Gems, Toys }

internal sealed partial class CollectorPet : Node3D
{
    private readonly Village village;
    private readonly Node3D model = new();
    private readonly List<(Node3D Foot, int Swing)> feet = new();
    private Vector3 home;
    private float homeYaw;
    private float phase;

    internal CollectorPetKind Kind { get; }
    internal Animal Collector { get; }
    internal string DisplayName { get; }
    internal string RewardHint => $"Odměna za všech {Goal}: {DisplayName}.";
    internal int Goal { get; }
    internal int Count => Kind switch
    {
        CollectorPetKind.Farm => village.FarmCount,
        CollectorPetKind.Garden => village.GardenCount,
        CollectorPetKind.Gems => village.GemCount,
        _ => village.ToyCount
    };
    internal bool IsOwned => Count >= Goal;
    internal bool IsActive => IsOwned && !Disabled;
    internal bool Disabled { get; set; }
    internal Vector3 TrailOffset { get; }

    internal CollectorPet(CollectorPetKind kind, Village village)
    {
        this.village = village;
        Kind = kind;
        Name = $"CollectorPet_{kind}";
        (Collector, DisplayName, Goal, TrailOffset) = kind switch
        {
            CollectorPetKind.Farm => (village.Farmer, "Vlněné jehňátko", Village.FarmKinds.Length, new Vector3(-1.8f, 0, 4.2f)),
            CollectorPetKind.Garden => (village.Gardener, "Květinový skřítek", Village.FlowerNames.Length, new Vector3(1.8f, 0, 4.2f)),
            CollectorPetKind.Gems => (village.GemCollector, "Krystalová želvička", Village.GemNames.Length, new Vector3(-1.8f, 0, 7.3f)),
            _ => (village.Toymaker, "Plyšový medvídek", Village.ToyNames.Length, new Vector3(1.8f, 0, 7.3f))
        };
        AddChild(model);
        switch (kind)
        {
            case CollectorPetKind.Farm: BuildLamb(); break;
            case CollectorPetKind.Garden: BuildSprout(); break;
            case CollectorPetKind.Gems: BuildTurtle(); break;
            case CollectorPetKind.Toys: BuildTeddy(); break;
        }
    }

    internal void SetHome(Vector3 position, float yaw)
    {
        home = position;
        homeYaw = yaw;
        ReturnHome();
    }

    internal void ReturnHome() => Place(home, homeYaw);

    internal Node3D CreatePreviewModel()
    {
        var preview = (Node3D)model.Duplicate(0);
        preview.Position = Vector3.Zero;
        return preview;
    }

    internal void Place(Vector3 position, float yaw)
    {
        Position = position;
        Rotation = new(0, yaw, 0);
        Animate(0, 0);
    }

    internal void Animate(float delta, float speed)
    {
        phase += delta * (2.5f + speed * 1.8f);
        float stride = Mathf.Min(speed / 4, 1);
        model.Position = Vector3.Up * (.025f * (1 + Mathf.Sin(phase)) + .14f * stride * Mathf.Abs(Mathf.Sin(phase)));
        foreach (var (foot, swing) in feet)
            foot.Rotation = new(Mathf.Sin(phase) * stride * swing * .65f, 0, 0);
    }

    private void Foot(Vector3 joint, float length, float width, string color, int swing)
    {
        var foot = new Node3D { Position = joint };
        model.AddChild(foot);
        feet.Add((foot, swing));
        Art.Ball(foot, new(0, -.48f * length, .035f), new(width, .52f * length, width * 1.25f), color);
    }

    private void Eyes(float x, float y, float z)
    {
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(model, new(side * x, y, z), new(.052f, .063f, .039f), "34404a");
            Art.Ball(model, new(side * x - .014f, y + .02f, z + .028f), Vector3.One * .016f, "fff5e1");
        }
    }

    private void BuildLamb()
    {
        Art.Ball(model, new(0, .66f, -.12f), new(.43f, .37f, .58f), "fff0d4");
        Art.Ball(model, new(0, .7f, -.66f), new(.14f, .15f, .14f), "fff0d4");
        Art.Ball(model, new(0, .87f, .39f), new(.29f, .29f, .26f), "dbc19d");
        Art.Ball(model, new(0, .75f, .59f), new(.18f, .11f, .11f), "f4dac1");
        Art.Ball(model, new(0, .79f, .688f), new(.05f, .03f, .02f), "775857");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(model, new(side * .3f, .96f, .37f), new(.17f, .085f, .09f), "dbc19d").RotateZ(side * -.3f);
            Art.Ball(model, new(side * .15f, 1.08f, .43f), Vector3.One * .155f, "fff0d4");
            foreach (int end in new[] { -1, 1 })
            {
                Art.Ball(model, new(side * .29f, .78f, end * .22f - .12f), new(.18f, .23f, .26f), "fff7e6");
                Foot(new(side * .24f, .42f, end * .32f - .1f), .42f, .105f, "765e58", side * end);
            }
        }
        Eyes(.126f, .91f, .602f);
    }

    private void BuildSprout()
    {
        Art.Ball(model, new(0, .48f, 0), new(.29f, .32f, .24f), "6eaa62");
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.Tau / 8;
            Art.Ball(model, new(Mathf.Sin(angle) * .33f, .98f + Mathf.Cos(angle) * .33f, 0),
                new(.135f, .23f, .1f), i % 2 == 0 ? "f7a9c4" : "ee87ae").RotateZ(-angle);
        }
        Art.Ball(model, new(0, .98f, .08f), new(.3f, .29f, .19f), "f6cc73");
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(model, new(side * .3f, .51f, .02f), new(.26f, .088f, .11f), "95c86f").RotateZ(side * .5f);
            Foot(new(side * .14f, .25f, 0), .25f, .12f, "4d8554", side);
        }
        Art.Ball(model, new(0, .91f, .267f), new(.05f, .022f, .018f), "865243");
        Eyes(.105f, 1.02f, .245f);
    }

    private void BuildTurtle()
    {
        Art.Ball(model, new(0, .42f, -.09f), new(.45f, .28f, .56f), "5a867d");
        Art.Ball(model, new(0, .44f, .47f), new(.235f, .21f, .29f), "8cc6a7");
        Art.Ball(model, new(0, .29f, -.65f), new(.09f, .1f, .18f), "8cc6a7");
        Village.CreateGem(model, new(0, .59f, -.12f), 3).Scale = new(.71f, .58f, .9f);
        foreach (int side in new[] { -1, 1 })
        {
            var crystal = Village.CreateGem(model, new(side * .26f, .5f, -.23f), 1);
            crystal.Scale = Vector3.One * .38f;
            crystal.RotateZ(side * -.26f);
            foreach (int end in new[] { -1, 1 })
                Foot(new(side * .34f, .26f, end * .28f - .1f), .26f, .13f, "8cc6a7", side * end);
        }
        Eyes(.113f, .49f, .705f);
    }

    private void BuildTeddy()
    {
        const string fur = "c58a59";
        Art.Ball(model, new(0, .61f, 0), new(.32f, .39f, .25f), fur);
        Art.Ball(model, new(0, 1.08f, .04f), new(.35f, .32f, .3f), fur);
        foreach (int side in new[] { -1, 1 })
        {
            Art.Ball(model, new(side * .27f, 1.33f, .03f), Vector3.One * .145f, fur);
            Art.Ball(model, new(side * .27f, 1.34f, .145f), new(.085f, .08f, .04f), "eec99a");
            Art.Ball(model, new(side * .33f, .63f, .035f), new(.12f, .25f, .13f), fur).RotateZ(side * .38f);
            Art.Ball(model, new(side * .1f, .83f, .276f), new(.11f, .07f, .04f), "d76667").RotateZ(side * .32f);
            Foot(new(side * .17f, .39f, .025f), .39f, .13f, fur, side);
        }
        Art.Ball(model, new(0, .99f, .309f), new(.153f, .11f, .068f), "f0cfa4");
        Art.Ball(model, new(0, 1.02f, .368f), new(.057f, .033f, .025f), "61463c");
        Art.Ball(model, new(0, .83f, .318f), Vector3.One * .049f, "f5a275");
        Eyes(.115f, 1.12f, .315f);
    }
}
