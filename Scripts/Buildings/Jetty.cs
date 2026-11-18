using System;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The jetty: stone footings on the bank, log piles driven into the riverbed, a plank deck laid section by section,
/// a railing and finally a flag at the end. The ring lies on the bank and the deck leads out over the water.
/// </summary>
public partial class Jetty : SitedProject
{
    private float deckStart;
    private float deckEnd;
    private Boat? boat;

    /// <summary>The boat that sails the river once the jetty is finished.</summary>
    internal Boat? Boat => boat;

    /// <summary>Distance from the ring to where the deck ends over the water.</summary>
    internal float Length => deckEnd;

    internal override Vector3 Focus => Site.Position + Site.Inward * ((deckStart + deckEnd) / 2) + Vector3.Up * 3;

    internal Jetty(BuildSite site, Terrain ground)
        : base(site, ground)
    {
        Name = "Jetty";
        // The deck begins a little before the water and reaches out over about nine metres of the river.
        float z = site.Position.Z;
        float shore = 0;
        while (
            shore < 40
            && River.Distance(site.Position.X + site.Inward.X * shore, site.Position.Z + site.Inward.Z * shore)
                >= River.HalfWidth(z)
        )
            shore += .5f;
        deckStart = Mathf.Max(2.5f, shore - 4);
        deckEnd = shore + Mathf.Clamp(River.HalfWidth(z) * .9f, 6, 11);
    }

    private float Bed(float t) =>
        Ground.Height(Position.X + Site.Inward.X * t, Position.Z + Site.Inward.Z * t) - Position.Y;

    private protected override void Build()
    {
        float level = River.Level(Position.Z) - Position.Y;
        float deck = Mathf.Max(level + .9f, -.1f);
        // 0 gravel: a stone footing where the deck leaves the bank.
        float footing = Pad(Stages[0], 0, deckStart, 5, 3, "8f8f8a");
        for (int i = 0; i < 4; i++)
            Art.Ball(
                Stages[0],
                new(-1.7f + i * 1.15f, footing + .2f, deckStart + 1.9f),
                new(.7f, .4f, .6f),
                i % 2 == 0 ? "b9b4a5" : "8f8a7d"
            );
        // 1 gravel: rubble along the bank on both sides of the deck.
        for (int i = 0; i < 8; i++)
        {
            float t = deckStart + 1 + i * .9f;
            foreach (float x in new[] { -2.4f, 2.4f })
                Art.Ball(
                    Stages[1],
                    new(x, Mathf.Max(Bed(t), level - .1f), t),
                    new(.7f, .4f, .6f),
                    (i + (x > 0 ? 1 : 0)) % 2 == 0 ? "a7a6a0" : "8f8f8a"
                );
        }
        // 2-3 logs: pairs of piles every 2.6 m, the shore half first.
        int pairs = Mathf.Max(2, Mathf.CeilToInt((deckEnd - deckStart) / 2.6f) + 1);
        for (int pair = 0; pair < pairs; pair++)
        {
            float t = deckStart + pair * (deckEnd - deckStart) / (pairs - 1);
            var stage = Stages[pair < pairs / 2 ? 2 : 3];
            foreach (float x in new[] { -1.4f, 1.4f })
            {
                float bottom = Mathf.Min(Bed(t), level - .3f) - .3f;
                float height = deck - .1f - bottom;
                Art.Cylinder(
                    stage,
                    new(x, bottom + height / 2, t),
                    .2f,
                    .22f,
                    height,
                    pair % 2 == 0 ? "7a4f33" : "8a5f3f"
                );
            }
        }
        // 4-8 planks: the deck grows in five sections from the shore, with the stringers under the first one
        // and the railing on the last.
        int planks = Mathf.CeilToInt((deckEnd - deckStart) / .8f);
        for (int plank = 0; plank < planks; plank++)
        {
            float t = deckStart + .4f + plank * .8f;
            int section = Mathf.Min(4, plank * 5 / planks);
            Art.Block(
                Stages[4 + section],
                new(0, deck, t),
                new(3.4f, .14f, .74f),
                plank % 2 == 0 ? "c39770" : "b8895f"
            );
        }
        foreach (float x in new[] { -1.2f, 1.2f })
            Art.Beam(Stages[4], new(x, deck - .25f, deckStart), new(x, deck - .25f, deckEnd), .2f, "8a6a4a");
        for (float t = deckStart + .2f; t <= deckEnd; t += 2.2f)
            foreach (float x in new[] { -1.65f, 1.65f })
                Art.Cylinder(Stages[8], new(x, deck + .6f, t), .07f, .07f, 1.2f, "a77a59");
        foreach (float x in new[] { -1.65f, 1.65f })
            Art.Beam(Stages[8], new(x, deck + 1.1f, deckStart + .2f), new(x, deck + 1.1f, deckEnd), .1f, "e2bb8b");
        // 9 flag: a flagpole at the end of the jetty and a bench halfway along.
        Art.Cylinder(Stages[9], new(0, deck + 1.9f, deckEnd - .3f), .06f, .06f, 3.6f, "a77a59");
        Art.Block(Stages[9], new(.55f, deck + 3.2f, deckEnd - .3f), new(1.1f, .6f, .04f), "e2563b");
        Art.Block(Stages[9], new(.55f, deck + 3.2f, deckEnd - .3f), new(1.1f, .2f, .045f), "fff1b8");
        float middle = (deckStart + deckEnd) / 2;
        Art.Block(Stages[9], new(1.2f, deck + .45f, middle), new(.5f, .12f, 1.8f), "c39770");
        foreach (float t in new[] { middle - .7f, middle + .7f })
            Art.Block(Stages[9], new(1.2f, deck + .2f, t), new(.4f, .4f, .12f), "a77a59");
        // The finished jetty brings a boat that sails the river past it, up and down a stretch of about 26 m.
        boat = new Boat(Position.Z, 26) { Visible = false };
        AddChild(boat);
    }

    private protected override void Refresh()
    {
        base.Refresh();
        if (boat != null)
            boat.Visible = Complete;
    }
}
