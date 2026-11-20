using System.Collections.Generic;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// What the car does to the flowers and the small bushes it drives over. A flower is knocked flat in the direction the
/// car goes and its petals fly up round the car (<see cref="Petals"/>); a while later the stem stands up again, swings
/// a little past upright, and the blossom grows back. A bush is pressed down and springs back with a wobble, throwing a
/// few leaves and the petals of its blossoms. A plant stays down for as long as a wheel is on it.
/// <para>
/// The plants are parts of the <see cref="MultiMesh"/> batches of <see cref="Vegetation"/> and <see cref="Wayside"/>,
/// which register them here while they are generated: a plant is its foot, its reach and the instances it is made of,
/// the "heads" among them being the petals that fly off. Only the plants that are moving keep their rest transforms
/// (read back from the batch when they are first touched), so the registry costs a few bytes a part. Scenery only:
/// nothing is saved, no random draw of the valley is taken, and a plant bent while the game is saved simply stands
/// up again in the next game.
/// </para>
/// </summary>
internal sealed partial class Trampling : Node3D
{
    internal enum Kind : byte
    {
        Flower,
        Bush,
    }

    /// <summary>A batch of instances: where its instances are measured from and what its heads throw.</summary>
    /// <param name="Origin">The position of the batch's node; its instance transforms are relative to it.</param>
    /// <param name="Color">The colour of the petals its heads throw.</param>
    /// <param name="PerHead">Petals thrown by one head; zero takes one petal of the head's own size.</param>
    /// <param name="PetalSize">The half length of those petals when <paramref name="PerHead"/> is not zero.</param>
    /// <param name="Fluff">Whether they are dandelion seeds, which float.</param>
    /// <param name="Lift">How high above an instance's own origin its head is, in the instance's units.</param>
    private sealed record Batch(Vector3 Origin, Color Color, int PerHead, float PetalSize, bool Fluff, float Lift)
    {
        internal MultiMesh? Mesh;
    }

    private readonly record struct Part(int Batch, int Index, bool Head);

    private readonly record struct Plant(Vector3 Foot, float Reach, Kind Kind, int First, int Count);

    /// <summary>A plant that is down or on its way up again.</summary>
    private sealed class Bent(int plant, Transform3D[] rest, Vector3 axis, float lie)
    {
        internal readonly int Plant = plant;
        internal readonly Transform3D[] Rest = rest;
        internal readonly Vector3 Axis = axis;
        internal float Age,
            From,
            Lie = lie,
            Lean,
            Bloom,
            DrawnLean = float.NaN,
            DrawnBloom = float.NaN;
    }

    private const float Cell = 4;

    /// <summary>The largest reach of a plant, so that a search round the car finds every plant it can touch.</summary>
    private const float MostReach = 1.8f;

    /// <summary>How far a flower is knocked over (radians) and how long it takes.</summary>
    private const float FlowerLean = 1.3f;
    private const float KnockTime = .14f;
    private const float FlowerRise = 2.2f;
    private const float BloomTime = 1.1f;

    private const float BushLean = .38f;
    private const float BushSquash = .4f;
    private const float BushRise = 1.5f;

    private static readonly Color[] BushLeaves = { new("89bc76"), new("b0d780"), new("559b71") };

    private readonly List<Batch> batches = new();
    private readonly List<Part> parts = new();
    private readonly List<Plant> plants = new();
    private readonly Dictionary<Vector2I, List<int>> grid = new();
    private readonly Dictionary<int, Bent> bent = new();
    private readonly List<Bent> moving = new();
    private readonly Petals petals = new();
    private int building = -1;
    private Vector3 buildingFoot;
    private float buildingReach;
    private Kind buildingKind;

    /// <summary>A headless run keeps no instance data (see Vegetation), so there only the state is followed.</summary>
    private static readonly bool Rendered = DisplayServer.GetName() != "headless";

    internal int FlowerCount { get; private set; }
    internal int BushCount { get; private set; }

    /// <summary>The plants that are down or standing up again.</summary>
    internal int BentCount => moving.Count;

    internal int PetalCount => petals.Count;

    internal Trampling()
    {
        Name = "Trampling";
        AddChild(petals);
    }

    /// <summary>The height of the ground, on which the petals land.</summary>
    internal void Setup(System.Func<float, float, float> height) => petals.Setup(height);

    // ---- Registration, while the plants are generated ----

    /// <summary>A new batch; its mesh is bound with <see cref="Bind"/> once it is made.</summary>
    internal int AddBatch(
        Color color,
        Vector3 origin = default,
        int perHead = 0,
        float petalSize = 0,
        bool fluff = false,
        float lift = 0
    )
    {
        batches.Add(new Batch(origin, color, perHead, petalSize, fluff, lift));
        return batches.Count - 1;
    }

    internal void Bind(int batch, MultiMesh mesh) => batches[batch].Mesh = mesh;

    /// <summary>Starts a plant standing at <paramref name="foot"/>, which the car touches within <paramref name="reach"/>.</summary>
    internal void Begin(Vector3 foot, float reach, Kind kind)
    {
        building = parts.Count;
        buildingFoot = foot;
        buildingReach = Mathf.Min(reach, MostReach);
        buildingKind = kind;
    }

    /// <summary>An instance of the plant begun last; a head is hidden while the plant is down and grows back.</summary>
    internal void Add(int batch, int index, bool head)
    {
        if (building >= 0)
            parts.Add(new Part(batch, index, head));
    }

    internal bool Building => building >= 0;

    internal void End()
    {
        if (building < 0)
            return;
        int count = parts.Count - building;
        if (count > 0)
        {
            var cell = CellOf(buildingFoot);
            if (!grid.TryGetValue(cell, out var list))
                grid[cell] = list = new();
            list.Add(plants.Count);
            plants.Add(new Plant(buildingFoot, buildingReach, buildingKind, building, count));
            if (buildingKind == Kind.Flower)
                FlowerCount++;
            else
                BushCount++;
        }
        building = -1;
    }

    /// <summary>Forgets the plants that were taken out of an area that is built on (Vegetation.ClearArea).</summary>
    internal void Forget(System.Func<Vector3, bool> cleared)
    {
        foreach (var list in grid.Values)
            list.RemoveAll(plant =>
            {
                if (!cleared(plants[plant].Foot))
                    return false;
                if (plants[plant].Kind == Kind.Flower)
                    FlowerCount--;
                else
                    BushCount--;
                return true;
            });
    }

    private static Vector2I CellOf(Vector3 p) => new(Mathf.FloorToInt(p.X / Cell), Mathf.FloorToInt(p.Z / Cell));

    /// <summary>The feet of the plants of a kind, for the checks.</summary>
    internal IEnumerable<Vector3> Feet(Kind kind)
    {
        foreach (var list in grid.Values)
        foreach (int plant in list)
            if (plants[plant].Kind == kind)
                yield return plants[plant].Foot;
    }

    // ---- The car ----

    /// <summary>
    /// A box on the ground, <paramref name="halfWidth"/> by <paramref name="halfLength"/> round <paramref name="center"/>
    /// and turned to <paramref name="forward"/>, that moves with <paramref name="velocity"/> (the car or the trailer):
    /// every plant under it goes down, or stays down. Nothing happens to a plant under a car that stands still.
    /// </summary>
    internal void Touch(Vector3 center, Vector3 forward, float halfWidth, float halfLength, Vector3 velocity)
    {
        var flatVelocity = new Vector3(velocity.X, 0, velocity.Z);
        if (flatVelocity.LengthSquared() < .5f * .5f)
            return;
        forward.Y = 0;
        if (forward.LengthSquared() < .0001f)
            return;
        forward = forward.Normalized();
        var right = forward.Cross(Vector3.Up);
        float reach = halfLength + halfWidth + MostReach;
        var low = CellOf(center - new Vector3(reach, 0, reach));
        var high = CellOf(center + new Vector3(reach, 0, reach));
        for (int x = low.X; x <= high.X; x++)
        for (int z = low.Y; z <= high.Y; z++)
        {
            if (!grid.TryGetValue(new Vector2I(x, z), out var list))
                continue;
            foreach (int index in list)
            {
                var plant = plants[index];
                var offset = plant.Foot - center;
                // A car that jumps over a flower leaves it standing.
                if (Mathf.Abs(offset.Y) > 1.6f)
                    continue;
                offset.Y = 0;
                if (
                    Mathf.Abs(offset.Dot(right)) > halfWidth + plant.Reach
                    || Mathf.Abs(offset.Dot(forward)) > halfLength + plant.Reach
                )
                    continue;
                Knock(index, center, flatVelocity);
            }
        }
    }

    private void Knock(int index, Vector3 car, Vector3 velocity)
    {
        var plant = plants[index];
        if (bent.TryGetValue(index, out var down))
        {
            // Still down: it stays down while the car is on it.
            if (down.Age < down.Lie)
            {
                down.Lie = Mathf.Max(down.Lie, down.Age + LieTime(plant.Kind));
                return;
            }
            // On its way up: knocked again, from where it is.
            if (down.Bloom > .5f)
                Throw(plant, down, car, velocity);
            down.From = down.Lean;
            down.Age = 0;
            down.Lie = LieTime(plant.Kind);
            return;
        }
        var rest = new Transform3D[Rendered ? plant.Count : 0];
        for (int i = 0; i < rest.Length; i++)
        {
            var part = parts[plant.First + i];
            rest[i] = batches[part.Batch].Mesh?.GetInstanceTransform(part.Index) ?? Transform3D.Identity;
        }
        var push = velocity.Normalized();
        var plantBent = new Bent(index, rest, Vector3.Up.Cross(push).Normalized(), LieTime(plant.Kind));
        Throw(plant, plantBent, car, velocity);
        bent[index] = plantBent;
        moving.Add(plantBent);
    }

    /// <summary>How long a plant stays down after the car was last on it.</summary>
    private float LieTime(Kind kind) => kind == Kind.Flower ? petals.Random(4.5f, 7.5f) : petals.Random(.35f, .6f);

    /// <summary>The petals of the plant's heads fly up round the car; a bush also loses a few leaves.</summary>
    private void Throw(Plant plant, Bent down, Vector3 car, Vector3 velocity)
    {
        var away = plant.Foot - car;
        away.Y = 0;
        away = away.LengthSquared() > .01f ? away.Normalized() : velocity.Normalized();
        void One(Vector3 at, Color color, Vector3 shape, bool fluff)
        {
            float turn = petals.Random(-1.4f, 1.4f);
            var outward = away.Rotated(Vector3.Up, turn);
            var throwVelocity =
                velocity * petals.Random(.35f, .6f)
                + outward * petals.Random(1.2f, 2.8f)
                + Vector3.Up * (fluff ? petals.Random(1.2f, 2.4f) : petals.Random(2.4f, 4.4f));
            var jitter = new Vector3(petals.Random(-.08f, .08f), petals.Random(0, .08f), petals.Random(-.08f, .08f));
            petals.Throw(at + jitter, throwVelocity, color, shape, fluff);
        }
        int heads = 0;
        for (int i = 0; i < plant.Count; i++)
            heads += parts[plant.First + i].Head ? 1 : 0;
        // A lupin has fifteen florets: some of them are enough.
        int every = Mathf.Max(1, (heads + 7) / 8),
            head = 0;
        for (int i = 0; i < plant.Count; i++)
        {
            var part = parts[plant.First + i];
            if (!part.Head || head++ % every != 0)
                continue;
            var batch = batches[part.Batch];
            var at = plant.Foot + Vector3.Up * .4f;
            if (down.Rest.Length > 0)
            {
                var transform = Pose(plant, down, i, 1);
                at = batch.Origin + transform.Origin + transform.Basis * new Vector3(0, batch.Lift, 0);
            }
            if (batch.PerHead == 0)
            {
                float size = down.Rest.Length > 0 ? Extent(down.Rest[i].Basis) : .1f;
                size = Mathf.Clamp(size * .8f, .04f, .16f);
                One(at, batch.Color, new Vector3(size, size * .14f, size * .6f), false);
                continue;
            }
            float scale = down.Rest.Length > 0 ? down.Rest[i].Basis.Column1.Length() : 1;
            float petal = batch.PetalSize * scale;
            for (int n = 0; n < batch.PerHead; n++)
                One(
                    at,
                    batch.Color,
                    batch.Fluff ? new Vector3(petal, petal * .6f, petal) : new Vector3(petal, petal * .2f, petal * .5f),
                    batch.Fluff
                );
        }
        if (plant.Kind != Kind.Bush)
            return;
        int leaves = Mathf.Clamp(Mathf.RoundToInt(plant.Reach * 5), 3, 7);
        for (int n = 0; n < leaves; n++)
        {
            var at =
                plant.Foot + new Vector3(petals.Random(-.4f, .4f), petals.Random(.4f, .9f), petals.Random(-.4f, .4f));
            float size = petals.Random(.09f, .15f);
            One(at, BushLeaves[n % BushLeaves.Length], new Vector3(size, size * .2f, size * .5f), false);
        }
    }

    private static float Extent(Basis basis) =>
        Mathf.Max(basis.Column0.Length(), Mathf.Max(basis.Column1.Length(), basis.Column2.Length()));

    // ---- Every frame ----

    internal void Tick(float dt, Vector2 wind)
    {
        for (int i = moving.Count - 1; i >= 0; i--)
        {
            var down = moving[i];
            var plant = plants[down.Plant];
            down.Age += dt;
            bool done = Animate(plant.Kind, down);
            if (done)
            {
                down.Lean = 0;
                down.Bloom = 1;
            }
            Draw(plant, down);
            if (!done)
                continue;
            bent.Remove(down.Plant);
            moving.RemoveAt(i);
        }
        petals.Tick(dt, wind);
    }

    /// <summary>Sets the plant's lean (1 is fully down) and its blossom; true once it is as it was.</summary>
    private static bool Animate(Kind kind, Bent down)
    {
        float age = down.Age;
        float rise = kind == Kind.Flower ? FlowerRise : BushRise;
        // The bush's blossoms grow back after it has stood up; a flower's while it straightens.
        float bloomStart = down.Lie + (kind == Kind.Flower ? rise * .55f : rise + 1.5f);
        if (age < KnockTime)
            down.Lean = Mathf.Lerp(down.From, 1, Ease(age / KnockTime));
        else if (age < down.Lie)
            // A small bounce as it hits the ground.
            down.Lean = 1 + Mathf.Sin((age - KnockTime) * 18) * Mathf.Exp(-(age - KnockTime) * 9) * .08f;
        else if (age < down.Lie + rise)
        {
            float u = (age - down.Lie) / rise;
            down.Lean =
                kind == Kind.Flower
                    ? 1 - BackOut(u)
                    // A bush springs back past upright and wobbles to rest.
                    : (1 - u) * (1 - u) * Mathf.Cos(u * Mathf.Pi * 2.5f);
        }
        else
            down.Lean = 0;
        down.Bloom = age < bloomStart ? 0 : BackOut(Mathf.Min(1, (age - bloomStart) / BloomTime));
        return age >= down.Lie + rise && age >= bloomStart + BloomTime;
    }

    private static float Ease(float t) => t * t * (3 - 2 * t);

    /// <summary>Eases out past 1 and back, by about a tenth.</summary>
    private static float BackOut(float t)
    {
        const float c = 1.4f;
        float u = t - 1;
        return 1 + (c + 1) * u * u * u + c * u * u;
    }

    private void Draw(Plant plant, Bent down)
    {
        if (down.Rest.Length == 0 || (down.Lean == down.DrawnLean && down.Bloom == down.DrawnBloom))
            return;
        bool bodyMoved = down.Lean != down.DrawnLean;
        down.DrawnLean = down.Lean;
        down.DrawnBloom = down.Bloom;
        for (int i = 0; i < plant.Count; i++)
        {
            var part = parts[plant.First + i];
            if (!bodyMoved && !part.Head)
                continue;
            batches[part.Batch]
                .Mesh?.SetInstanceTransform(part.Index, Pose(plant, down, i, part.Head ? down.Bloom : 1));
        }
    }

    /// <summary>Where the plant's <paramref name="i"/>th part is drawn now, in its batch's space.</summary>
    private Transform3D Pose(Plant plant, Bent down, int i, float head)
    {
        var part = parts[plant.First + i];
        var rest = down.Rest[i];
        var pivot = plant.Foot - batches[part.Batch].Origin;
        Basis turn;
        if (plant.Kind == Kind.Flower)
            turn = new Basis(down.Axis, down.Lean * FlowerLean);
        else
        {
            // Pressed down and spread out, then stretched up as it springs back.
            float squash = 1 - down.Lean * BushSquash,
                spread = 1 + Mathf.Max(0, down.Lean) * BushSquash * .35f;
            turn = new Basis(down.Axis, down.Lean * BushLean) * Basis.FromScale(new Vector3(spread, squash, spread));
        }
        var basis = turn * rest.Basis;
        if (head != 1)
            basis = basis.ScaledLocal(Vector3.One * Mathf.Max(head, 0));
        return new Transform3D(basis, pivot + turn * (rest.Origin - pivot));
    }
}
