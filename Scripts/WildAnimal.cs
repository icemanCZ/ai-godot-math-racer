using Godot;
using System.Collections.Generic;

namespace MathRacer;

public enum WildKind { Butterfly, Bird, Rabbit, Fox, Deer }

public partial class WildAnimal : Node3D
{
    public WildKind Kind { get; }
    public bool Flying => Kind == WildKind.Butterfly || Kind == WildKind.Bird;
    public float EscapeSpeed => Kind switch { WildKind.Butterfly => 4.5f, WildKind.Bird => 12, WildKind.Rabbit => 8, WildKind.Fox => 9, _ => 11 };
    internal Vector3 Home, Direction;
    internal float Phase, Fear;
    private float altitude;
    private readonly Node3D model = new();
    private readonly List<Node3D> limbs = new();
    private readonly List<Node3D> wings = new();

    public WildAnimal(WildKind kind, float phase)
    {
        Kind = kind; Phase = phase;
        Name = $"Wild{kind}";
        AddChild(model);
        if (Flying) BuildFlier(); else BuildRunner();
    }

    private MeshInstance3D Ball(Node3D parent, Vector3 p, Vector3 size, string color)
    {
        var part = Art.Ball(parent, p, size, color);
        part.VisibilityRangeEnd = 140; part.VisibilityRangeEndMargin = 10;
        part.CastShadow = Flying ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On;
        return part;
    }

    private void BuildFlier()
    {
        bool butterfly = Kind == WildKind.Butterfly;
        string color = butterfly ? new[] { "f1a342", "a786e9", "5bcddd", "f184ac" }[(int)(Phase * 10) % 4] : "577da0";
        Ball(model, Vector3.Zero, butterfly ? new(.055f, .055f, .27f) : new(.19f, .22f, .4f), butterfly ? "49404d" : color);
        if (!butterfly)
        {
            Ball(model, new(0, .16f, .3f), new(.18f, .18f, .18f), "4a6178");
            Ball(model, new(0, .13f, .49f), new(.08f, .06f, .14f), "e7b559");
            Ball(model, new(0, -.03f, -.4f), new(.13f, .06f, .3f), color);
            Ball(model, new(0, -.1f, .12f), new(.15f, .14f, .25f), "efdcb6");
            foreach (int side in new[] { -1, 1 }) Ball(model, new(side * .14f, .21f, .4f), Vector3.One * .032f, "253441");
        }
        foreach (int side in new[] { -1, 1 })
        {
            var wing = new Node3D(); model.AddChild(wing); wings.Add(wing);
            Ball(wing, new(side * .34f, 0, .08f), butterfly ? new(.36f, .025f, .28f) : new(.55f, .035f, .22f), color);
            if (butterfly)
            {
                Ball(wing, new(side * .26f, 0, -.23f), new(.26f, .025f, .22f), color);
                Ball(wing, new(side * .4f, .025f, .1f), new(.12f, .012f, .12f), "ffe9b5");
                Ball(wing, new(side * .28f, .025f, -.23f), new(.08f, .012f, .08f), "51436d");
            }
        }
    }

    private void BuildRunner()
    {
        bool rabbit = Kind == WildKind.Rabbit, deer = Kind == WildKind.Deer;
        string fur = rabbit ? "c4af94" : deer ? "b68b60" : "d58c4b";
        float body = deer ? 1.15f : rabbit ? .36f : .55f;
        Ball(model, new(0, body, 0), deer ? new(.4f, .52f, .83f) : rabbit ? new(.29f, .33f, .43f) : new(.3f, .33f, .67f), fur);
        float headY = deer ? 1.85f : rabbit ? .68f : .8f;
        float headZ = deer ? .65f : rabbit ? .31f : .65f;
        if (deer) Ball(model, new(0, 1.55f, .52f), new(.23f, .5f, .27f), fur);
        Ball(model, new(0, headY, headZ), new(deer ? .25f : .23f, .25f, .29f), fur);
        Ball(model, new(0, headY - .1f, headZ + .24f), new(.17f, .12f, .22f), "efdfc2");
        Ball(model, new(0, headY - .07f, headZ + .43f), new(.065f, .05f, .05f), "453b38");
        foreach (int side in new[] { -1, 1 })
        {
            var ear = Ball(model, new(side * .18f, headY + (rabbit ? .38f : .25f), headZ - .04f), new(.09f, rabbit ? .37f : .22f, .09f), fur);
            ear.RotationDegrees = new(0, 0, side * (deer ? 38 : 12));
            Ball(model, ear.Position + Vector3.Back * .07f, new(.04f, rabbit ? .26f : .12f, .025f), "daac9e");
            Ball(model, new(side * .19f, headY + .04f, headZ + .19f), new(.045f, .055f, .04f), "2b3439");
            foreach (int end in new[] { -1, 1 })
            {
                var leg = new Node3D { Position = new(side * (deer ? .25f : .2f), body, end * (deer ? .52f : .28f)) };
                model.AddChild(leg); limbs.Add(leg);
                Ball(leg, new(0, -body * .48f, 0), new(.075f, body * .5f, .095f), fur);
                Ball(leg, new(0, -body + .07f, .06f), new(.09f, .08f, rabbit ? .19f : .12f), deer ? "51443d" : fur);
            }
        }
        Ball(model, new(0, body + .05f, deer ? -.8f : rabbit ? -.43f : -.84f), rabbit ? Vector3.One * .14f : deer ? new(.16f, .22f, .14f) : new(.21f, .2f, .48f), rabbit || deer ? "f4e7d1" : fur);
        if (Kind == WildKind.Fox) Ball(model, new(0, body + .05f, -1.19f), new(.17f, .17f, .19f), "f4e7d1");
        if (deer)
            foreach (float z in new[] { -.35f, 0, .3f })
                foreach (int side in new[] { -1, 1 }) Ball(model, new(side * .36f, 1.35f, z), new(.04f, .055f, .07f), "f4e7d1");
    }

    internal void Animate(float dt, bool moving)
    {
        bool fleeing = Fear > 0;
        float beat = Phase * (fleeing ? 23 : 12);
        if (Flying)
        {
            float target = Kind == WildKind.Butterfly ? (fleeing ? 2.8f : 1.1f) : (fleeing ? 4.5f : .45f);
            altitude = dt == 0 ? target : Mathf.MoveToward(altitude, target, dt * 3);
            model.Position = Vector3.Up * (altitude + Mathf.Sin(Phase * 3) * .12f);
            for (int i = 0; i < wings.Count; i++) wings[i].Rotation = new(0, 0, Mathf.Sin(beat) * .85f * (i == 0 ? -1 : 1));
        }
        else
        {
            model.Position = Vector3.Up * (moving ? Mathf.Abs(Mathf.Sin(beat * .5f)) * (Kind == WildKind.Rabbit ? .26f : .09f) : 0);
            for (int i = 0; i < limbs.Count; i++) limbs[i].Rotation = new(moving ? Mathf.Sin(beat + (i == 0 || i == 3 ? 0 : Mathf.Pi)) * .55f : 0, 0, 0);
        }
    }
}
