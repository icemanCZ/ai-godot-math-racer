using Godot;

namespace MathRacer.Vehicles;

/// <summary>
/// The smoke of the steam engine's chimney. Like a real locomotive it puffs in beats with the driving wheels, four
/// beats a turn, which run together into a streaming plume at speed. How hard the engine works decides the puffs: big,
/// fast and a little grey while the train gathers speed, small and pale while it holds its speed, none at all when the
/// driver lets go of the pedal, and a thin lazy wisp while it stands. The puffs live in the valley, not on the train,
/// so they stay behind where they were blown out and the plume trails after the engine.
/// </summary>
public partial class TrainSmoke : Node3D
{
    private const int Capacity = 72;

    /// <summary>How far the train goes between two beats: a quarter turn of the driving wheels (radius .42 m).</summary>
    private const float BeatLength = Mathf.Tau * .42f / 4;

    /// <summary>The most beats a second; faster, the puffs run together anyway.</summary>
    private const float MostBeats = 12;

    /// <summary>The share of the effort that is left while the train holds its speed with the pedal down.</summary>
    internal const float Holding = .3f;

    private readonly Vector3[] position = new Vector3[Capacity];
    private readonly Vector3[] velocity = new Vector3[Capacity];
    private readonly Vector3[] shape = new Vector3[Capacity];
    private readonly float[] age = new float[Capacity];
    private readonly float[] life = new float[Capacity];
    private readonly float[] start = new float[Capacity];
    private readonly float[] grow = new float[Capacity];
    private readonly float[] opacity = new float[Capacity];
    private readonly float[] grey = new float[Capacity];
    private readonly float[] seed = new float[Capacity];
    private readonly RandomNumberGenerator random = new();
    private static ShaderMaterial? material;

    /// <summary>
    /// A puff is a lumpy ball whose edge melts away and whose inside is torn into wisps, so it reads as smoke and not
    /// as a glass bead. Its colour is the instance colour (sRGB, alpha the puff's opacity); the custom data holds the
    /// puff's own seed and how far through its life it is, so the lumps of one puff stay put and slowly roll.
    /// </summary>
    private const string PuffShader = """
        shader_type spatial;
        render_mode blend_mix, depth_draw_never, cull_back, specular_disabled, diffuse_lambert_wrap;

        varying vec3 local;

        float hash(vec3 p) {
            p = fract(p * 0.3183099 + 0.1);
            p *= 17.0;
            return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float noise(vec3 x) {
            vec3 i = floor(x);
            vec3 f = fract(x);
            f = f * f * (3.0 - 2.0 * f);
            return mix(
                mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x),
                    mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
                mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x),
                    mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y),
                f.z);
        }

        void vertex() {
            vec3 p = VERTEX * 1.7 + vec3(INSTANCE_CUSTOM.x * 31.0, INSTANCE_CUSTOM.y * 1.2, 0.0);
            float lump = noise(p) * 0.65 + noise(p * 2.3) * 0.35;
            VERTEX *= 0.72 + 0.5 * lump;
            local = VERTEX * 2.2 + vec3(0.0, -INSTANCE_CUSTOM.y * 1.5, INSTANCE_CUSTOM.x * 17.0);
        }

        void fragment() {
            float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
            float wisps = noise(local) * 0.6 + noise(local * 2.4) * 0.4;
            // The top of a puff catches the sky, its belly is in its own shade: that makes it round like a cloud.
            float up = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).y;
            ALBEDO = pow(COLOR.rgb, vec3(2.2)) * mix(0.62, 1.0, smoothstep(-0.7, 0.6, up));
            ALPHA = COLOR.a * smoothstep(0.0, 0.7, facing) * smoothstep(0.08, 0.6, wisps + facing * 0.45);
            ROUGHNESS = 1.0;
            BACKLIGHT = vec3(0.35);
        }
        """;
    private MultiMesh puffs = null!;
    private Vehicle? vehicle;
    private Vector3 lastPlace;
    private bool placed;
    private float effort;
    private float beat;
    private float sinceBeat;
    private int oldest;
    private int count;

    /// <summary>The volume of smoke blown out so far, which the checks compare between ways of driving.</summary>
    internal float Blown { get; private set; }

    public override void _Ready()
    {
        random.Seed = 7;
        puffs = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            InstanceCount = Capacity,
            VisibleInstanceCount = 0,
            Mesh = new SphereMesh
            {
                Radius = 1,
                Height = 2,
                RadialSegments = 20,
                Rings = 10,
            },
        };
        AddChild(
            new MultiMeshInstance3D
            {
                Name = "Puffs",
                Multimesh = puffs,
                TopLevel = true,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = material ??= new ShaderMaterial { Shader = new Shader { Code = PuffShader } },
            }
        );
        for (Node? node = GetParent(); node != null; node = node.GetParent())
            if (node is Vehicle car)
            {
                vehicle = car;
                break;
            }
        // A train shown in the garage picture has been standing a while, so its wisp is already there.
        for (int i = 0; i < 90; i++)
            Step(1 / 30f, 0, Vector3.Zero);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
        {
            // Another model is on the car: the smoke goes with the train and starts afresh when it comes back.
            count = 0;
            placed = false;
            puffs.VisibleInstanceCount = 0;
            return;
        }
        float dt = Mathf.Min((float)delta, .1f);
        var place = GlobalPosition;
        var moved = placed && dt > 0 ? (place - lastPlace) / dt : Vector3.Zero;
        lastPlace = place;
        placed = true;
        if (moved.Length() > 40)
        {
            // The train was put somewhere else, not driven there: the smoke of the old place stays behind unseen.
            count = 0;
            moved = Vector3.Zero;
        }
        Step(dt, vehicle?.Effort ?? 0, moved);
    }

    /// <summary>
    /// Moves the smoke on by <paramref name="dt"/> seconds while the engine works with <paramref name="work"/> (0 to
    /// 1) and the chimney moves with <paramref name="motion"/>, and blows out the beats that fall due.
    /// </summary>
    internal void Step(float dt, float work, Vector3 motion)
    {
        // The engine eases into and out of its effort, so one frame of a let-go pedal does not cut the plume.
        effort = Mathf.MoveToward(effort, work, (work > effort ? 6f : 3.5f) * dt);
        float speed = new Vector2(motion.X, motion.Z).Length();
        sinceBeat += dt;
        if (effort > .02f)
        {
            // Pulling from a standstill the wheels turn before the train moves, so the beats start at once.
            beat += Mathf.Max(speed * dt / BeatLength, dt * 2.5f);
            if (beat >= 1 && sinceBeat >= 1 / MostBeats)
            {
                beat = Mathf.Min(beat - 1, 1);
                sinceBeat = 0;
                Chuff(effort, motion, speed);
            }
        }
        else
        {
            beat = 1;
            // Standing, the fire keeps going and a thin wisp curls up; rolling without steam, the chimney is clear.
            if (speed < .5f && sinceBeat >= .42f)
            {
                sinceBeat = 0;
                Wisp(motion);
            }
        }
        Drift(dt);
    }

    private void Chuff(float work, Vector3 motion, float speed)
    {
        // At speed the beats come too fast to see one by one, so each is a little smaller and they make one plume.
        float steady = 1 - Mathf.Clamp((speed - 4) / 14, 0, .2f);
        float size = (.15f + .16f * work) * steady;
        Puff(
            size,
            (.55f + 1.1f * work) * steady,
            Vector3.Up * (1.2f + 2.3f * work) + motion * .3f,
            1.8f + 2f * work,
            .6f + .38f * work,
            Mathf.Lerp(.98f, .8f, work)
        );
    }

    private void Wisp(Vector3 motion) => Puff(.08f, .36f, Vector3.Up * .5f + motion * .3f, 3f, .42f, .98f);

    private void Puff(float from, float to, Vector3 push, float lasts, float alpha, float shade)
    {
        int i = (oldest + count) % Capacity;
        if (count == Capacity)
            oldest = (oldest + 1) % Capacity;
        else
            count++;
        float spread = push.Length() * .08f;
        position[i] = GlobalPosition + new Vector3(random.RandfRange(-.03f, .03f), 0, random.RandfRange(-.03f, .03f));
        velocity[i] = push + new Vector3(random.RandfRange(-spread, spread), 0, random.RandfRange(-spread, spread));
        shape[i] = new(random.RandfRange(.85f, 1.15f), random.RandfRange(.8f, 1.05f), random.RandfRange(.85f, 1.15f));
        age[i] = 0;
        life[i] = lasts * random.RandfRange(.85f, 1.15f);
        start[i] = from;
        grow[i] = to * random.RandfRange(.85f, 1.2f) - from;
        opacity[i] = alpha;
        grey[i] = shade + random.RandfRange(-.02f, .02f);
        seed[i] = random.Randf();
        Blown += to * to * to * alpha;
    }

    private void Drift(float dt)
    {
        // The air slows a puff at once, the warm smoke keeps rising slowly, and a light breeze leans it aside.
        float drag = Mathf.Exp(-2.6f * dt);
        var rise = new Vector3(.25f, .55f, .1f);
        while (count > 0 && age[oldest] + dt >= life[oldest])
        {
            oldest = (oldest + 1) % Capacity;
            count--;
        }
        for (int n = 0; n < count; n++)
        {
            int i = (oldest + n) % Capacity;
            age[i] += dt;
            velocity[i] = rise + (velocity[i] - rise) * drag;
            position[i] += velocity[i] * dt;
            float t = age[i] / life[i];
            // A puff swells fast as it leaves the chimney, then slowly, and fades away as it spreads.
            float radius = start[i] + grow[i] * (1 - Mathf.Exp(-3.2f * t));
            float alpha = opacity[i] * Mathf.Min(1, t * 30) * (1 - t) * (1 - t);
            float shade = Mathf.Lerp(grey[i], .98f, t);
            // The oldest puffs are drawn first, as they are the farthest from the chimney.
            puffs.SetInstanceTransform(n, new Transform3D(Basis.FromScale(shape[i] * radius), position[i]));
            puffs.SetInstanceColor(n, new Color(shade, shade, shade * 1.01f, alpha));
            puffs.SetInstanceCustomData(n, new Color(seed[i], t, 0, 0));
        }
        puffs.VisibleInstanceCount = count;
    }
}
