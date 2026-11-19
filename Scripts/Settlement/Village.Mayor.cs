using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Graphics;

namespace MathRacer.Settlement;

/// <summary>
/// The mayor on the village square and the decorations she sells. Both are placed in the square's own frame inside the
/// ground that the square already reserves and draw no random numbers, so nothing else in the valley moves. A
/// decoration is built once, hidden and without a solid body, and appears when it is owned.
/// </summary>
public partial class Village
{
    /// <summary>Where the mayor stands on the square (x, z), clear of the album stop at the entrance and of the lantern strings.</summary>
    private static readonly Vector2 MayorSpot = new(-8.6f, -.7f);

    internal Character Mayor { get; private set; } = null!;

    private readonly Dictionary<string, List<Node3D>> decorationNodes = new();
    private readonly HashSet<string> ownedDecorations = new();

    private void GenerateMayor()
    {
        var spot = Square.Transform * new Vector3(MayorSpot.X, 0, MayorSpot.Y);
        var rug = SquareProp("MayorRug", MayorSpot.X, MayorSpot.Y);
        Art.Cylinder(rug, new(0, .03f, 0), 1.15f, 1.15f, .06f, "c0504d");
        Art.Cylinder(rug, new(0, .065f, 0), .95f, .95f, .05f, "f3cf70");
        Art.Cylinder(rug, new(0, .09f, 0), .85f, .85f, .05f, "c0504d");
        var spec = CharacterCatalog.Mayor;
        Mayor = Character.Create(spec);
        Mayor.Role = new MayorRole();
        Mayor.DressAsMayor();
        AddChild(Mayor);
        Mayor.Position = ground.At(spot.X, spot.Z);
        Mayor.Rotation = Square.Transform.Basis.GetEuler();

        foreach (var decoration in TownDecorations.All)
        {
            var nodes = new List<Node3D>();
            for (int copy = 0; copy < decoration.Slots.Length; copy++)
            {
                var node = PlaceDecoration($"Decoration_{decoration.Id}_{copy}", decoration.Slots[copy]);
                decoration.Build(node, true);
                nodes.Add(node);
            }
            decorationNodes[decoration.Id] = nodes;
            ShowDecoration(decoration.Id);
        }
    }

    /// <summary>The frame of a place of the village: its origin and its turn, with z towards the road.</summary>
    private Transform3D FrameOf(TownPlace place) =>
        place switch
        {
            TownPlace.Square => Square.Transform,
            TownPlace.Garage => GarageBuilding.Transform,
            >= TownPlace.House0 and <= TownPlace.House3 => Houses[place - TownPlace.House0].Transform,
            _ => Shops[place - TownPlace.Shop0].Transform,
        };

    /// <summary>Where a slot lies in the world, on the ground.</summary>
    internal Vector3 SlotPosition(TownSlot slot)
    {
        var p = FrameOf(slot.Place) * new Vector3(slot.X, 0, slot.Z);
        return ground.At(p.X, p.Z, .08f);
    }

    private Node3D PlaceDecoration(string name, TownSlot slot)
    {
        var node = new Node3D
        {
            Name = name,
            Position = SlotPosition(slot),
            Basis = FrameOf(slot.Place).Basis * Basis.FromEuler(new Vector3(0, slot.Yaw, 0)),
        };
        AddChild(node);
        return node;
    }

    /// <summary>The ids of the decorations that were bought, in the order of the shop.</summary>
    internal string[] OwnedDecorationIds =>
        TownDecorations.All.Where(decoration => ownedDecorations.Contains(decoration.Id)).Select(d => d.Id).ToArray();

    internal bool OwnsDecoration(string id) => ownedDecorations.Contains(id);

    /// <summary>Puts a bought decoration on the square; an unknown id is ignored.</summary>
    internal void AddDecoration(string id)
    {
        if (!decorationNodes.ContainsKey(id))
            return;
        ownedDecorations.Add(id);
        ShowDecoration(id);
    }

    /// <summary>Sets the decorations that stand on the square to exactly these; unknown ids are ignored.</summary>
    internal void RestoreDecorations(IEnumerable<string>? ids)
    {
        ownedDecorations.Clear();
        foreach (string id in ids ?? Enumerable.Empty<string>())
            if (decorationNodes.ContainsKey(id))
                ownedDecorations.Add(id);
        foreach (string id in decorationNodes.Keys)
            ShowDecoration(id);
    }

    /// <summary>The copies of a decoration on the square, one for each of its places; empty for an unknown id.</summary>
    internal IReadOnlyList<Node3D> DecorationNodes(string id) =>
        decorationNodes.TryGetValue(id, out var nodes) ? nodes : System.Array.Empty<Node3D>();

    /// <summary>A point above the first copy of a decoration, for a camera or a celebration.</summary>
    internal Vector3 DecorationFocus(string id) =>
        decorationNodes.TryGetValue(id, out var nodes) && nodes.Count > 0
            ? nodes[0].GlobalPosition + Vector3.Up * 2
            : Mayor.GlobalPosition;

    private void ShowDecoration(string id)
    {
        bool owned = ownedDecorations.Contains(id);
        foreach (var node in decorationNodes[id])
        {
            node.Visible = owned;
            foreach (var shape in node.FindChildren("*", "CollisionShape3D", true, false).Cast<CollisionShape3D>())
                shape.Disabled = !owned;
        }
    }
}
