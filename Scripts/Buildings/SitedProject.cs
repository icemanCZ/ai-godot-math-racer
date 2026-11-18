using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// Where a building (the mill, the lookout tower or the jetty) stands: <see cref="Position"/> is the unloading
/// ring, <see cref="Road"/> the road point its way starts from, <see cref="Inward"/> the direction in which the
/// building lies behind the ring (towards the water for the jetty) and <see cref="BuilderSpot"/> where its builder stands.
/// </summary>
internal sealed record BuildSite(int Project, Vector3 Position, Vector3 Road, Vector3 Inward, Vector3 BuilderSpot);

/// <summary>
/// A building built beside a road or field path, in a frame whose +Z points along <see cref="BuildSite.Inward"/>.
/// Subclasses create the work of each of the ten stages; solid parts only block the car once their stage is built.
/// </summary>
public abstract partial class SitedProject : BuildProject
{
    internal BuildSite Site { get; }

    private protected readonly Terrain Ground;
    private protected readonly Node3D[] Stages = new Node3D[Trips];
    private readonly Basis basis;
    private readonly List<CollisionShape3D>[] solids = new List<CollisionShape3D>[Trips];

    private protected SitedProject(BuildSite site, Terrain ground)
        : base(site.Project)
    {
        Site = site;
        Ground = ground;
        Position = site.Position;
        float yaw = Mathf.Atan2(site.Inward.X, site.Inward.Z);
        Rotation = new Vector3(0, yaw, 0);
        basis = new Basis(Vector3.Up, yaw);
        for (int i = 0; i < Trips; i++)
            solids[i] = new List<CollisionShape3D>();
    }

    internal override Vector3 DropFor(int step) => Site.Position;

    internal override Vector3 DropRoadFor(int step) => Site.Road;

    /// <summary>Height above the ring of the ground below a point given in the building's own frame.</summary>
    private protected float Floor(float x, float z)
    {
        var global = Position + basis * new Vector3(x, 0, z);
        return Ground.Height(global.X, global.Z) - Position.Y;
    }

    /// <summary>A thick flat slab under a part of the building; returns the height of its top.</summary>
    private protected float Pad(Node3D parent, float x, float z, float width, float depth, string color)
    {
        float top = float.MinValue;
        foreach (float dx in new[] { -.5f, 0, .5f })
        foreach (float dz in new[] { -.5f, 0, .5f })
            top = Mathf.Max(top, Floor(x + dx * width, z + dz * depth));
        top += .05f;
        Art.Block(parent, new(x, top - 2.5f, z), new(width, 5, depth), color);
        return top;
    }

    /// <summary>A solid box that blocks the car once <paramref name="stage"/> (0-9) has been built.</summary>
    private protected void Solid(int stage, Vector3 center, Vector3 size, Basis? rotation = null)
    {
        var body = Obstacles.Add(Stages[stage], center, new BoxShape3D { Size = size }, rotation);
        var shape = (CollisionShape3D)body.GetChild(0);
        shape.Disabled = stage >= Stage;
        solids[stage].Add(shape);
    }

    public override void _Ready()
    {
        for (int i = 0; i < Trips; i++)
        {
            Stages[i] = new Node3D { Name = GetType().Name + "Stage" + i, Visible = false };
            AddChild(Stages[i]);
        }
        Build();
        Refresh();
    }

    /// <summary>Creates the models of all ten stages in <see cref="Stages"/>.</summary>
    private protected abstract void Build();

    private protected override void Refresh()
    {
        for (int i = 0; i < Trips; i++)
        {
            Stages[i].Visible = i < Stage;
            foreach (var shape in solids[i])
                shape.Disabled = i >= Stage;
        }
    }
}
