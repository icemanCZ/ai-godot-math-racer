using System.Collections.Generic;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Settlement;

/// <summary>
/// The glass lamp maker on the village square, opposite the mayor, with a worktable of his lamps. Like the mayor he is
/// placed in the square's own frame inside the ground that the square already reserves and draws no random numbers, so
/// nothing else in the valley moves. The lamps he hands out stand in <see cref="Lamps"/>; the glasses he blows them from
/// are tipped out at <see cref="GlassmakerDrop"/>.
/// </summary>
public partial class Village
{
    /// <summary>Where the lamp maker stands on the square (x, z): the mirror of the mayor.</summary>
    private static readonly Vector2 GlassmakerSpot = new(8.6f, -.7f);

    /// <summary>
    /// Where the trailer tips out the glasses for the lamp maker (x, z): on the paving between him and the fountain, clear
    /// of the bench, the stall and his worktable.
    /// </summary>
    private static readonly Vector2 GlassmakerDropSpot = new(4.6f, .4f);

    internal Character Glassmaker { get; private set; } = null!;

    /// <summary>The ring on the square where the glasses for the lamp maker are tipped out.</summary>
    internal Vector3 GlassmakerDrop { get; private set; }

    /// <summary>
    /// The lamps in the bag and on the ground; it hangs from the village, which stands at the origin, so a lamp's position
    /// is its world position. <see cref="World"/> makes it once the world is generated (<see cref="CreateLamps"/>): the
    /// road audit only generates the village, and a node of this class in that village made the process crash now and
    /// then when it quit.
    /// </summary>
    internal LampField Lamps { get; private set; } = null!;

    private readonly List<(Node3D Node, Node3D Glow)> tableLamps = new();

    private void GenerateGlassmaker()
    {
        var spot = Square.Transform * new Vector3(GlassmakerSpot.X, 0, GlassmakerSpot.Y);
        var rug = SquareProp("GlassmakerRug", GlassmakerSpot.X, GlassmakerSpot.Y);
        Art.Cylinder(rug, new(0, .03f, 0), 1.15f, 1.15f, .06f, "3f6f9a");
        Art.Cylinder(rug, new(0, .065f, 0), .95f, .95f, .05f, "bfe6f2");
        Art.Cylinder(rug, new(0, .09f, 0), .85f, .85f, .05f, "3f6f9a");
        Glassmaker = Character.Create(CharacterCatalog.Glassmaker);
        Glassmaker.Role = new GlassmakerRole();
        Glassmaker.DressAsGlassmaker();
        AddChild(Glassmaker);
        Glassmaker.Position = ground.At(spot.X, spot.Z);
        Glassmaker.Rotation = Square.Transform.Basis.GetEuler();
        var drop = Square.Transform * new Vector3(GlassmakerDropSpot.X, 0, GlassmakerDropSpot.Y);
        GlassmakerDrop = ground.At(drop.X, drop.Z);

        // A worktable beside him with a candle and a lamp that shine at night.
        var table = SquareProp("GlassmakerTable", 9.9f, -2.7f);
        Art.Block(table, new(0, .62f, 0), new(1.5f, .12f, .95f), "bc8b60");
        foreach (float x in new[] { -.6f, .6f })
        foreach (float z in new[] { -.35f, .35f })
            Art.Block(table, new(x, .3f, z), new(.1f, .6f, .1f), "926c51");
        Obstacles.Box(table, new(0, .35f, 0), new(1.5f, .7f, .95f));
        AddTableLamp("candle", table, new(-.35f, .68f, 0), 1);
        AddTableLamp("lamp", table, new(.4f, .68f, 0), .45f);
    }

    private void AddTableLamp(string kind, Node3D table, Vector3 position, float scale)
    {
        var node = new Node3D
        {
            Name = "DisplayLamp_" + kind,
            Position = position,
            Scale = Vector3.One * scale,
        };
        table.AddChild(node);
        var glow = GlassLamps.Find(kind)!.Build(node);
        glow.Visible = false;
        tableLamps.Add((node, glow));
    }

    /// <summary>Makes the field of lamps, with the lamps of the maker's table in it, once the world is generated.</summary>
    internal void CreateLamps()
    {
        if (Lamps != null)
            return;
        Lamps = new LampField { Name = "GlassLamps" };
        AddChild(Lamps);
        foreach (var (node, glow) in tableLamps)
            Lamps.AdoptDisplay(node, glow);
    }

    /// <summary>Lights the lamps of the village for the present darkness; <paramref name="focus"/> is where the car is.</summary>
    internal void SetLampGlow(float darkness, Vector3 focus) => Lamps.SetDarkness(darkness, focus);
}
