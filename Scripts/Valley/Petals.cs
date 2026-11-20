using Godot;

namespace MathRacer.Valley;

/// <summary>
/// The petals, dandelion seeds and leaves that the car knocks off the flowers and bushes (<see cref="Trampling"/>). They
/// fly up round the car, flutter down on the wind, lie on the ground a moment and shrink away; seeds float longer. All
/// of them are one <see cref="MultiMesh"/>, drawn in one call, and its whole buffer goes to the engine in one call a
/// frame. Scenery only: nothing is saved and nothing is solid.
/// </summary>
internal sealed partial class Petals : Node3D
{
    internal const int Capacity = 320;

    /// <summary>Floats a petal takes in the buffer: a transform (12) and a colour (4).</summary>
    private const int Stride = 16;

    private const float LifeTime = 8;

    private struct Petal
    {
        public Vector3 Position,
            Velocity,
            Turn,
            Spin,
            Shape;
        public Color Color;
        public float Age,
            Phase,
            Landed,
            Rest;
        public bool Fluff;
    }

    private readonly Petal[] petals = new Petal[Capacity];
    private readonly float[] buffer = new float[Capacity * Stride];
    private readonly MultiMesh multimesh;
    private System.Func<float, float, float> floor = (_, _) => 0;
    private FlatRandom random = new(7341);

    /// <summary>A headless run draws nothing, so the buffer is not filled there.</summary>
    private static readonly bool Rendered = DisplayServer.GetName() != "headless";

    internal int Count { get; private set; }

    internal Petals()
    {
        Name = "Petals";
        TopLevel = true;
        multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new SphereMesh
            {
                Radius = 1,
                Height = 2,
                RadialSegments = 8,
                Rings = 4,
            },
            InstanceCount = Capacity,
            VisibleInstanceCount = 0,
        };
        AddChild(
            new MultiMeshInstance3D
            {
                Multimesh = multimesh,
                MaterialOverride = new StandardMaterial3D
                {
                    VertexColorUseAsAlbedo = true,
                    VertexColorIsSrgb = true,
                    Roughness = .85f,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            }
        );
    }

    /// <summary>The height a petal lands on at a point of the valley.</summary>
    internal void Setup(System.Func<float, float, float> ground) => floor = ground;

    /// <summary>
    /// One petal at <paramref name="at"/>, thrown with <paramref name="velocity"/>. <paramref name="shape"/> is the size
    /// of its three half axes; a <paramref name="fluff"/> (a dandelion seed) falls slowly and drifts far on the wind.
    /// A petal is left out when the air is already full of them.
    /// </summary>
    internal void Throw(Vector3 at, Vector3 velocity, Color color, Vector3 shape, bool fluff = false)
    {
        if (Count == Capacity)
            return;
        petals[Count++] = new Petal
        {
            Position = at,
            Velocity = velocity,
            Turn = new Vector3(random.RandfRange(-1, 1), random.RandfRange(0, Mathf.Tau), random.RandfRange(-1, 1)),
            Spin = new Vector3(random.RandfRange(-7, 7), random.RandfRange(-4, 4), random.RandfRange(-7, 7)),
            Shape = shape,
            Color = color,
            Phase = random.RandfRange(0, Mathf.Tau),
            Landed = -1,
            Rest = random.RandfRange(2.2f, 3.6f),
            Fluff = fluff,
        };
    }

    /// <summary>A random number between <paramref name="from"/> and <paramref name="to"/>, for the throws.</summary>
    internal float Random(float from, float to) => random.RandfRange(from, to);

    /// <summary>Moves every petal one frame on; <paramref name="wind"/> is the wind on the ground in metres a second.</summary>
    internal void Tick(float dt, Vector2 wind)
    {
        for (int i = Count - 1; i >= 0; i--)
        {
            ref var petal = ref petals[i];
            petal.Age += dt;
            float ground = floor(petal.Position.X, petal.Position.Z) + .02f;
            if (petal.Landed < 0)
            {
                // The air slows a petal down to the wind: a petal falls at about two metres a second, a seed at half a metre.
                float gravity = petal.Fluff ? 1.1f : 3.6f,
                    drag = petal.Fluff ? 2.4f : 1.6f,
                    carried = petal.Fluff ? 1 : .5f;
                petal.Velocity.Y -= gravity * dt;
                var air = new Vector3(wind.X * carried, 0, wind.Y * carried);
                petal.Velocity += (air - petal.Velocity) * (1 - Mathf.Exp(-drag * dt));
                // Petals see-saw as they fall.
                float swing = petal.Fluff ? .25f : .8f;
                var flutter = new Vector3(
                    Mathf.Sin(petal.Age * 6.5f + petal.Phase),
                    0,
                    Mathf.Cos(petal.Age * 5.1f + petal.Phase * 1.3f)
                );
                petal.Position += (petal.Velocity + flutter * swing) * dt;
                petal.Turn += petal.Spin * dt;
                if (petal.Position.Y <= ground && petal.Velocity.Y < 0)
                {
                    petal.Position.Y = ground;
                    petal.Landed = 0;
                }
            }
            else
            {
                // On the ground a petal lies flat and stays where it fell.
                petal.Landed += dt;
                petal.Position.Y = ground;
                float settle = 1 - Mathf.Exp(-10 * dt);
                petal.Turn.X -= petal.Turn.X * settle;
                petal.Turn.Z -= petal.Turn.Z * settle;
            }
            if (Scale(petal) > 0)
                continue;
            petals[i] = petals[--Count];
        }
        Draw();
    }

    /// <summary>How large a petal is drawn: it shrinks away after it has lain a while, or when it has flown too long.</summary>
    private static float Scale(in Petal petal)
    {
        float landed = petal.Landed < 0 ? 1 : 1 - (petal.Landed - petal.Rest) / .7f;
        float old = 1 - (petal.Age - (LifeTime - .8f)) / .8f;
        return Mathf.Clamp(Mathf.Min(landed, old), 0, 1);
    }

    private void Draw()
    {
        Visible = Count > 0;
        if (!Visible || !Rendered)
            return;
        for (int i = 0; i < Count; i++)
        {
            ref var petal = ref petals[i];
            var basis = Basis.FromEuler(petal.Turn).ScaledLocal(petal.Shape * Scale(petal));
            var p = petal.Position;
            int o = i * Stride;
            buffer[o] = basis.Column0.X;
            buffer[o + 1] = basis.Column1.X;
            buffer[o + 2] = basis.Column2.X;
            buffer[o + 3] = p.X;
            buffer[o + 4] = basis.Column0.Y;
            buffer[o + 5] = basis.Column1.Y;
            buffer[o + 6] = basis.Column2.Y;
            buffer[o + 7] = p.Y;
            buffer[o + 8] = basis.Column0.Z;
            buffer[o + 9] = basis.Column1.Z;
            buffer[o + 10] = basis.Column2.Z;
            buffer[o + 11] = p.Z;
            buffer[o + 12] = petal.Color.R;
            buffer[o + 13] = petal.Color.G;
            buffer[o + 14] = petal.Color.B;
            buffer[o + 15] = 1;
        }
        multimesh.Buffer = buffer;
        multimesh.VisibleInstanceCount = Count;
    }
}
