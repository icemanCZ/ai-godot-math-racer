using Godot;

namespace MathRacer;

public partial class Animal : Node3D
{
    public string DisplayName { get; private set; } = "";
    public string Greeting { get; private set; } = "";
    private Node3D model = null!;
    private float time;
    private float celebration;
    public void Celebrate() => celebration = 2.5f;
    private Label3D taskMarker = null!;
    public bool RewardAvailable { get; private set; } = true;
    public int Kind { get; private set; }
    public string FurColor { get; private set; } = "";
    public void Guide(bool visible, bool destination = false)
    {
        taskMarker.Visible = visible;
        taskMarker.Text = destination ? "!" : "?";
        taskMarker.Modulate = new Color(destination ? "63e7e0" : "ffeb3b");
    }
    public void SetRewardAvailable(bool available)
    {
        RewardAvailable = available;
        taskMarker.Visible = available;
    }
    public static Animal Create(string name, string greeting, int kind, string? furColor = null)
    {
        var a = new Animal { DisplayName = name, Greeting = greeting }; a.model = new Node3D(); a.AddChild(a.model);
        string color = furColor ?? (kind == 0 ? "f3e8d5" : kind == 1 ? "e9a16a" : "b98d71");
        a.Kind = kind; a.FurColor = color;
        Art.Ball(a.model, new(0, .8f, 0), new(.65f, .85f, .55f), color);
        Art.Ball(a.model, new(0, 1.8f, 0), new(.82f, .75f, .65f), color);
        Art.Ball(a.model, new(0, 1.5f, .5f), new(.5f, .33f, .26f), "fff0d9");
        foreach (float x in new[] { -.36f, .36f })
        {
            var ear = Art.Ball(a.model, new(x, kind == 0 ? 2.9f : 2.4f, 0), kind == 0 ? new(.23f, .8f, .22f) : new Vector3(.29f, kind == 1 ? .5f : .3f, .25f), color);
            ear.RotationDegrees = new Vector3(0, 0, -x * 22);
            Art.Ball(a.model, new(x, kind == 0 ? 2.93f : 2.45f, .19f), kind == 0 ? new(.1f, .55f, .06f) : new Vector3(.14f, .2f, .06f), "eab4ad");
            Art.Ball(a.model, new(x, 1.93f, .56f), new(.19f, .23f, .12f), "fffaf0");
            Art.Ball(a.model, new(x, 1.92f, .66f), new(.09f, .12f, .05f), "384d51");
            Art.Ball(a.model, new(x - .025f, 1.98f, .71f), Vector3.One * .035f, "ffffff");
            Art.Ball(a.model, new(x, .24f, .26f), new(.32f, .22f, .4f), color);
        }
        Art.Ball(a.model, new(0, 1.58f, .76f), new(.13f, .1f, .09f), "6c5557");
        if (kind == 1) { var tail = Art.Ball(a.model, new(.5f, .7f, -.65f), new(.3f, .5f, .8f), color); tail.RotationDegrees = new(25, 30, 0); Art.Ball(a.model, new(.8f, 1, -1.1f), new(.25f, .3f, .32f), "fff0d9"); }
        a.taskMarker = Art.TaskMarker(a, new(0, 7.5f, 0), "?");
        Art.Sign(a, new(0, 3.65f, 0), name, 30);
        return a;
    }
    public override void _Process(double delta)
    {
        time += (float)delta;
        taskMarker.Position = Vector3.Up * (7.5f + Mathf.Sin(time * 2.4f) * .6f);
        model.Position = Vector3.Up * Mathf.Sin(time * 1.8f) * .045f;
        if (celebration > 0)
        {
            celebration = Mathf.Max(0, celebration - (float)delta);
            model.Position += Vector3.Up * Mathf.Abs(Mathf.Sin(celebration * 7)) * .65f;
        }
    }
}

