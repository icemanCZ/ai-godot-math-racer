using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Quizzes;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The arches and the gates of the ferrywoman's rally (<c>docs/RALLY_PLAN.md</c>). An arch with a chequered banner,
/// "START · CÍL", stands beyond each road to a border, where one stage ends and the next begins. A gate has four striped
/// posts across the road and its verges, a bar over them and three boards, one over each lane, each framed in a colour
/// of its own (red, yellow and blue from the left, as the corner panel shows them). The boards of the next gate show the
/// three answers of its question, a number, a sign or a picture, drawn by small viewports of their own; the boards of
/// the others show a question mark, and a gate that was driven through shows the right board green and a wrong one red.
/// Nothing here has collision, so the car drives through the posts as through the banners.
/// </summary>
internal sealed partial class RallyGates : Node3D
{
    /// <summary>The colours of the frames of the lanes, from the left.</summary>
    internal static readonly string[] LaneColors = { "e2533f", "f0c437", "4a86d4" };

    /// <summary>The boards hang this high over the road, under the bar, so that the car drives under them.</summary>
    private const float BoardHeight = 3.7f,
        BarHeight = 4.75f;

    private readonly RallyCourse course;
    private readonly Func<float, float, float> surface;
    private readonly List<Gate>[] gates;
    private readonly List<Node3D> arches = new();
    private readonly List<Node3D> pennants = new();
    private readonly Sign[] signs = new Sign[3];
    private (int Stage, int Gate) asked = (-1, -1);
    private float clock;

    /// <summary>One gate: its node and its three boards with the question mark of each.</summary>
    private sealed record Gate(Node3D Node, MeshInstance3D[] Boards, Label3D[] Marks, StandardMaterial3D[] Faces);

    /// <summary>The viewport that draws the answer of one board of the gate asked next, and its texture.</summary>
    private sealed record Sign(SubViewport Viewport, ColorRect Paper, Label Text, CenterContainer Picture);

    /// <summary>How many stages were driven for gold, which the ferrywoman's otter counts.</summary>
    internal int Golds { get; set; }

    internal RallyGates(RallyCourse course, Func<float, float, float> surface)
    {
        this.course = course;
        this.surface = surface;
        gates = new List<Gate>[course.Stages.Count];
    }

    public override void _Ready()
    {
        for (int i = 0; i < signs.Length; i++)
            signs[i] = CreateSign(i);
        foreach (var stage in course.Stages)
            arches.Add(BuildArch(stage.Start));
        for (int s = 0; s < course.Stages.Count; s++)
        {
            gates[s] = new List<Gate>();
            foreach (float along in course.Stages[s].Gates)
                gates[s].Add(BuildGate(along, $"RallyGate{s}_{gates[s].Count}"));
        }
    }

    /// <summary>A node standing on the ring at a distance along it, facing the way the stages run (its -Z).</summary>
    private Node3D Place(float along, string name)
    {
        var middle = course.At(along);
        var forward = course.Forward(along);
        var right = course.Right(along);
        var node = new Node3D
        {
            Name = name,
            Transform = new Transform3D(new Basis(right, Vector3.Up, -forward), middle),
        };
        AddChild(node);
        return node;
    }

    /// <summary>The height of the ground under a point of a node, in the node's own frame.</summary>
    private float GroundUnder(Node3D node, float across, float on = 0)
    {
        var p = node.Transform * new Vector3(across, 0, on);
        return surface(p.X, p.Z) - node.Position.Y;
    }

    /// <summary>A post with red and white stripes from the ground under it up to <paramref name="top"/>.</summary>
    private static void Post(Node3D parent, float across, float ground, float top)
    {
        float height = top - ground;
        int stripes = Mathf.Max(3, Mathf.RoundToInt(height / .55f));
        for (int i = 0; i < stripes; i++)
        {
            float step = height / stripes;
            Art.Block(
                parent,
                new(across, ground + step * (i + .5f), 0),
                new(.22f, step, .22f),
                i % 2 == 0 ? "e2533f" : "fbf6ee"
            );
        }
        Art.Ball(parent, new(across, top + .1f, 0), new(.17f, .17f, .17f), "f0c437");
    }

    /// <summary>The arch at the start of a stage: two posts, a chequered banner and "START · CÍL" on both faces.</summary>
    private Node3D BuildArch(float along)
    {
        var arch = Place(along, $"RallyArch{arches.Count}");
        float half = RallyCourse.GateHalfWidth + .6f;
        foreach (int side in new[] { -1, 1 })
        {
            Post(arch, side * half, GroundUnder(arch, side * half) - .1f, BarHeight + .6f);
            // A pennant on top of each post.
            var flag = new Node3D { Position = new(side * half, BarHeight + 1.05f, 0) };
            arch.AddChild(flag);
            Art.Block(flag, new(side * .35f, 0, 0), new(.6f, .34f, .03f), "4a86d4");
            pennants.Add(flag);
        }
        int squares = 14;
        float width = half * 2 / squares;
        for (int row = 0; row < 2; row++)
        for (int i = 0; i < squares; i++)
            Art.Block(
                arch,
                new(-half + width * (i + .5f), BarHeight + .2f - row * .4f, 0),
                new(width, .4f, .08f),
                (i + row) % 2 == 0 ? "1f1f24" : "fbf6ee"
            );
        foreach (int face in new[] { 1, -1 })
        {
            var label = new Label3D
            {
                Name = "ArchText",
                Text = "START  ·  CÍL",
                FontSize = 96,
                PixelSize = .02f,
                OutlineSize = 14,
                Modulate = new Color("fff5d9"),
                OutlineModulate = new Color("243018"),
                Position = new(0, BarHeight + 1.05f, face * .06f),
                RotationDegrees = new(0, face > 0 ? 0 : 180, 0),
                // One-sided, or each face would show through the other in a mirror.
                DoubleSided = false,
                Layers = Art.SignLayer,
            };
            arch.AddChild(label);
        }
        return arch;
    }

    /// <summary>A gate with its four posts, its bar, the white lines between its lanes and its three boards.</summary>
    private Gate BuildGate(float along, string name)
    {
        var node = Place(along, name);
        float lane = RallyCourse.LaneHalfWidth * 2;
        for (int post = 0; post < 4; post++)
        {
            float across = -RallyCourse.GateHalfWidth + post * lane;
            Post(node, across, GroundUnder(node, across) - .1f, BarHeight);
        }
        Art.Block(node, new(0, BarHeight, 0), new(RallyCourse.GateHalfWidth * 2 + .3f, .2f, .2f), "fbf6ee");
        // White lines on the ground between the lanes, from some way before the gate up to it.
        foreach (int side in new[] { -1, 1 })
            for (float on = 2; on <= 22; on += 3)
            {
                float x = side * RallyCourse.LaneHalfWidth;
                Art.Block(node, new(x, GroundUnder(node, x, on) + .17f, on), new(.16f, .04f, 1.6f), "f6f2e8");
            }
        var boards = new MeshInstance3D[3];
        var marks = new Label3D[3];
        var faces = new StandardMaterial3D[3];
        for (int i = 0; i < 3; i++)
        {
            float across = (i - 1) * lane;
            Art.Block(node, new(across, BoardHeight, -.05f), new(lane * .88f, 1.62f, .08f), LaneColors[i]);
            var face = new StandardMaterial3D
            {
                AlbedoColor = Colors.White,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            };
            var board = new MeshInstance3D
            {
                Name = $"Board{i}",
                Mesh = new QuadMesh { Size = new Vector2(lane * .76f, 1.38f) },
                Position = new(across, BoardHeight, 0),
                MaterialOverride = face,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            node.AddChild(board);
            var mark = new Label3D
            {
                Name = "Mark",
                Text = "?",
                FontSize = 150,
                PixelSize = .008f,
                OutlineSize = 0,
                Modulate = new Color(LaneColors[i]),
                Position = new(across, BoardHeight, .02f),
                Layers = Art.SignLayer,
            };
            node.AddChild(mark);
            boards[i] = board;
            marks[i] = mark;
            faces[i] = face;
        }
        return new Gate(node, boards, marks, faces);
    }

    /// <summary>A small viewport that draws one answer on white paper, a number or a sign as text, or a picture.</summary>
    private Sign CreateSign(int index)
    {
        var viewport = new SubViewport
        {
            Name = $"RallySign{index}",
            Size = new(256, 232),
            TransparentBg = false,
            Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        AddChild(viewport);
        var paper = new ColorRect { Color = Colors.White };
        viewport.AddChild(paper);
        paper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var text = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.AddThemeFontSizeOverride("font_size", 150);
        text.AddThemeColorOverride("font_color", new Color("2b3a40"));
        paper.AddChild(text);
        text.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var picture = new CenterContainer();
        paper.AddChild(picture);
        picture.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return new Sign(viewport, paper, text, picture);
    }

    /// <summary>What a board of a question shows: the number, the sign or the word of its picture, for the checks.</summary>
    internal static string ChoiceText(IChallenge question, int choice) =>
        question switch
        {
            IPictureChallenge pictures => pictures.Pictures[choice].Word,
            ISignChallenge signs => signs.Label(choice),
            _ => choice.ToString(),
        };

    /// <summary>
    /// The boards of one gate show the answers of its question, from the left in the order of its choices; every other
    /// gate shows its question marks again, or the colours of how it was answered.
    /// </summary>
    internal void Ask(int stage, int gate, IChallenge question)
    {
        Unask();
        asked = (stage, gate);
        var g = gates[stage][gate];
        for (int i = 0; i < 3; i++)
        {
            var sign = signs[i];
            int choice = question.Choices[i];
            foreach (var child in sign.Picture.GetChildren())
            {
                sign.Picture.RemoveChild(child);
                child.QueueFree();
            }
            if (question is IPictureChallenge pictures)
            {
                sign.Text.Text = "";
                sign.Picture.AddChild(
                    new WordPicture { PictureId = pictures.Pictures[choice].Id, CustomMinimumSize = new(240, 200) }
                );
            }
            else
                sign.Text.Text = ChoiceText(question, choice);
            sign.Paper.Color = Colors.White;
            sign.Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
            g.Faces[i].AlbedoTexture = sign.Viewport.GetTexture();
            g.Faces[i].AlbedoColor = Colors.White;
            g.Marks[i].Visible = false;
        }
    }

    /// <summary>The gate asked last shows its question marks again.</summary>
    private void Unask()
    {
        if (asked.Stage < 0)
            return;
        var g = gates[asked.Stage][asked.Gate];
        for (int i = 0; i < 3; i++)
        {
            g.Faces[i].AlbedoTexture = null;
            g.Marks[i].Visible = true;
            g.Boards[i].Scale = Vector3.One;
        }
        asked = (-1, -1);
    }

    /// <summary>
    /// A gate was driven through: the board of the right answer turns green and, when another lane was taken, that
    /// board turns red; the question marks stay hidden until the next drive.
    /// </summary>
    internal void Answered(int stage, int gate, int? lane, int right)
    {
        if (asked == (stage, gate))
        {
            var kept = gates[stage][gate];
            for (int i = 0; i < 3; i++)
                kept.Boards[i].Scale = Vector3.One;
            asked = (-1, -1);
        }
        var g = gates[stage][gate];
        for (int i = 0; i < 3; i++)
        {
            g.Marks[i].Visible = false;
            g.Faces[i].AlbedoColor =
                i == right ? new Color("7fd36b")
                : i == lane ? new Color("ef7a6a")
                : new Color(1, 1, 1, 1) * .8f;
        }
    }

    /// <summary>Every gate shows its question marks again, for a new drive.</summary>
    internal void Reset()
    {
        Unask();
        foreach (var stage in gates)
        foreach (var g in stage)
            for (int i = 0; i < 3; i++)
            {
                g.Faces[i].AlbedoTexture = null;
                g.Faces[i].AlbedoColor = Colors.White;
                g.Marks[i].Visible = true;
            }
    }

    /// <summary>The boards of the gate asked next swell a little in turn, and the pennants of the arches flutter.</summary>
    internal void Tick(float delta)
    {
        clock += delta;
        if (asked.Stage >= 0)
        {
            var g = gates[asked.Stage][asked.Gate];
            for (int i = 0; i < 3; i++)
                g.Boards[i].Scale = Vector3.One * (1 + .04f * Mathf.Sin(clock * 4 + i * 1.4f));
        }
        for (int i = 0; i < pennants.Count; i++)
            pennants[i].Rotation = new(0, .25f * Mathf.Sin(clock * 5 + i), 0);
    }

    /// <summary>Where a gate stands, for the checks and the views.</summary>
    internal Vector3 GatePosition(int stage, int gate) => gates[stage][gate].Node.Position;

    /// <summary>Whether a gate shows the answers of a question now, for the checks.</summary>
    internal bool Asks(int stage, int gate) => asked == (stage, gate);

    /// <summary>The text a board of the gate asked next shows, for the checks.</summary>
    internal string BoardText(int lane) => signs[lane].Text.Text;

    /// <summary>The colour of a board, for the checks: green for the right answer of a gate driven through.</summary>
    internal Color BoardColor(int stage, int gate, int lane) => gates[stage][gate].Faces[lane].AlbedoColor;

    /// <summary>How many arches stand, for the checks.</summary>
    internal int ArchCount => arches.Count;
}
