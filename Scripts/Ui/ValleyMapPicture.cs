using System.Linq;
using Godot;

namespace MathRacer.Ui;

/// <summary>What the owl's map shows of a valley (<c>docs/EXPLORER_PLAN.md</c>), on the ground as x and z.</summary>
internal sealed record ValleyMapData(
    Vector2[][] Roads,
    Vector2[][] OutlandRoads,
    Vector2[] River,
    Vector2[] BorderRiver,
    Vector2[] Coast,
    int Side,
    Vector2 Village,
    Vector2 Owl,
    Vector2[] Landmarks
);

/// <summary>How a wonder shows on the owl's map.</summary>
internal enum LandmarkStatus
{
    Hidden,
    Seen,
    Recorded,
}

/// <summary>
/// The owl's map of the whole valley, drawn as a child's map on old paper: the mountains along the top, the sea along
/// the bottom, the border river on its side and the forest on the other, the roads of the old valley and those beyond it,
/// the village, the owl's stand and the eight wonders: a question mark where one is still hidden, its picture with an
/// exclamation mark once the car has seen it, and with the owl's blue flag once it is written in. The wonder the road
/// arrows lead to has a golden ring, and the car is a red dot.
/// </summary>
internal sealed partial class ValleyMapPicture : Control
{
    private const float Extent = 1400;

    public ValleyMapData Map { get; init; } = null!;

    public LandmarkStatus[] Status { get; init; } = [];

    public Vector2 Car { get; init; }

    public int Guided { get; init; } = -1;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Resized += QueueRedraw;
    }

    private float MapScale => Mathf.Min(Size.X, Size.Y) / (2 * Extent);

    private Vector2 At(Vector2 ground) => Size / 2 + ground * MapScale;

    public override void _Draw()
    {
        float s = MapScale;
        var frame = new Rect2(At(new Vector2(-Extent, -Extent)), Vector2.One * 2 * Extent * s);
        DrawRect(frame, new Color("e9dcb4"));
        DrawRect(frame.Grow(-6), new Color("cfe0a2"));
        // The sea along the bottom, below its coast.
        var coast = Map.Coast.Select(At).ToList();
        if (coast.Count > 1)
        {
            var sea = coast.ToList();
            sea.Add(new Vector2(coast[^1].X, frame.End.Y - 6));
            sea.Add(new Vector2(coast[0].X, frame.End.Y - 6));
            DrawColoredPolygon(sea.ToArray(), new Color("6fb2d6"));
            DrawPolyline(
                coast.Select(p => p + new Vector2(0, -5 * s * 10)).ToArray(),
                new Color("f0dea0"),
                14 * s * 10
            );
            for (int i = 0; i < 6; i++)
            {
                var wave = new Vector2(frame.Position.X + frame.Size.X * (i + .5f) / 6, coast[0].Y);
                wave.Y = coast.OrderBy(p => Mathf.Abs(p.X - wave.X)).First().Y + 50 * s;
                DrawArc(wave, 14 * s * 10, Mathf.Pi * 1.15f, Mathf.Pi * 1.85f, 8, new Color("eaf6fb"), 2);
            }
        }
        // The forest on the side away from the border river.
        for (int i = 0; i < 11; i++)
        {
            float z = -Extent + 180 + i * 210;
            float x = -Map.Side * (1250 + (i % 2) * 70);
            Tree(At(new Vector2(x, z)), 36 * s * 10);
        }
        // The border river and the valley's own river.
        if (Map.BorderRiver.Length > 1)
            DrawPolyline(Map.BorderRiver.Select(At).ToArray(), new Color("5aa6cf"), 48 * s, true);
        if (Map.River.Length > 1)
            DrawPolyline(Map.River.Select(At).ToArray(), new Color("5aa6cf"), 22 * s, true);
        // The mountains along the top.
        for (int i = 0; i < 9; i++)
        {
            float x = -Extent + 120 + i * 300;
            var foot = At(new Vector2(x, -Extent + 260 + (i % 2) * 40));
            float w = 200 * s,
                h = 230 * s;
            DrawColoredPolygon(
                new[] { foot + new Vector2(-w, 0), foot + new Vector2(0, -h), foot + new Vector2(w, 0) },
                new Color("8c8a86")
            );
            DrawColoredPolygon(
                new[]
                {
                    foot + new Vector2(-w * .3f, -h * .7f),
                    foot + new Vector2(0, -h),
                    foot + new Vector2(w * .3f, -h * .7f),
                },
                new Color("f7f7f2")
            );
        }
        // The roads: the old valley's darker, those beyond it lighter.
        foreach (var road in Map.OutlandRoads.Where(road => road.Length > 1))
            DrawPolyline(road.Select(At).ToArray(), new Color("b38a5a"), 16 * s, true);
        foreach (var road in Map.Roads.Where(road => road.Length > 1))
            DrawPolyline(road.Select(At).ToArray(), new Color("7a5a3a"), 20 * s, true);
        // The village: three little houses.
        var village = At(Map.Village);
        foreach (var offset in new[] { new Vector2(-14, 4), new Vector2(0, -6), new Vector2(14, 4) })
            House(village + offset * s * 8, 10 * s * 8);
        // The owl's stand: a little brown owl.
        Owl(At(Map.Owl), 60 * s);
        var font = ThemeDB.FallbackFont;
        for (int i = 0; i < Map.Landmarks.Length && i < Status.Length; i++)
        {
            var p = At(Map.Landmarks[i]);
            float size = 150 * s;
            if (i == Guided)
                DrawArc(p, size * .75f, 0, Mathf.Tau, 32, new Color("f2c53d"), 5, true);
            if (Status[i] == LandmarkStatus.Hidden)
            {
                DrawCircle(p, size * .42f, new Color("fff7e0"));
                DrawArc(p, size * .42f, 0, Mathf.Tau, 24, new Color("7a5a3a"), 3, true);
                int fontSize = (int)(size * .6f);
                DrawString(
                    font,
                    p + new Vector2(-size * .42f, fontSize * .36f),
                    "?",
                    HorizontalAlignment.Center,
                    size * .84f,
                    fontSize,
                    new Color("7a5a3a")
                );
                continue;
            }
            DrawCircle(p, size * .55f, new Color("fff7e0"));
            LandmarkPicture.Paint(this, i, p, size * .9f);
            if (Status[i] == LandmarkStatus.Recorded)
            {
                var pole = p + new Vector2(size * .45f, -size * .1f);
                DrawLine(pole, pole + new Vector2(0, -size * .6f), new Color("5b4636"), 3);
                DrawColoredPolygon(
                    new[]
                    {
                        pole + new Vector2(0, -size * .6f),
                        pole + new Vector2(size * .32f, -size * .48f),
                        pole + new Vector2(0, -size * .36f),
                    },
                    new Color("3f7fd0")
                );
            }
            else
            {
                int fontSize = (int)(size * .5f);
                DrawString(
                    font,
                    p + new Vector2(size * .3f, -size * .15f),
                    "!",
                    HorizontalAlignment.Left,
                    -1,
                    fontSize,
                    new Color("d0402f")
                );
            }
        }
        // The car: a red dot with a white rim.
        var car = At(Car);
        DrawCircle(car, 30 * s + 3, Colors.White);
        DrawCircle(car, 30 * s, new Color("d23b2f"));
        DrawRect(frame, new Color("8a6a45"), false, 4);
    }

    private void Tree(Vector2 foot, float size)
    {
        DrawRect(
            new Rect2(foot + new Vector2(-size * .08f, -size * .3f), new Vector2(size * .16f, size * .3f)),
            new Color("6b4a2e")
        );
        DrawColoredPolygon(
            new[]
            {
                foot + new Vector2(-size * .4f, -size * .2f),
                foot + new Vector2(0, -size),
                foot + new Vector2(size * .4f, -size * .2f),
            },
            new Color("3d7a3a")
        );
    }

    private void House(Vector2 foot, float size)
    {
        DrawRect(
            new Rect2(foot + new Vector2(-size * .5f, -size * .7f), new Vector2(size, size * .7f)),
            new Color("f4e7cf")
        );
        DrawColoredPolygon(
            new[]
            {
                foot + new Vector2(-size * .65f, -size * .65f),
                foot + new Vector2(0, -size * 1.25f),
                foot + new Vector2(size * .65f, -size * .65f),
            },
            new Color("c0504d")
        );
    }

    private void Owl(Vector2 p, float size)
    {
        DrawCircle(p, size * .5f, new Color("9a7652"));
        DrawCircle(p + new Vector2(0, -size * .1f), size * .36f, new Color("eedcbc"));
        foreach (int side in new[] { -1, 1 })
        {
            DrawCircle(p + new Vector2(side * size * .16f, -size * .14f), size * .12f, Colors.White);
            DrawCircle(p + new Vector2(side * size * .16f, -size * .14f), size * .06f, new Color("2a2522"));
            DrawColoredPolygon(
                new[]
                {
                    p + new Vector2(side * size * .22f, -size * .4f),
                    p + new Vector2(side * size * .42f, -size * .62f),
                    p + new Vector2(side * size * .4f, -size * .3f),
                },
                new Color("7d5c3e")
            );
        }
        DrawColoredPolygon(
            new[] { p + new Vector2(-size * .05f, 0), p + new Vector2(size * .05f, 0), p + new Vector2(0, size * .1f) },
            new Color("e8a33a")
        );
    }
}
