using System;
using System.Collections.Generic;
using Godot;
using MathRacer.Graphics;
using MathRacer.Valley;

namespace MathRacer.Buildings;

/// <summary>
/// The forge beside the mountain slope (<c>docs/MAINLINE_PLAN.md</c>): a stone hut with a furnace that glows and a chimney
/// that smokes while a load of stone is being forged, an anvil with a hammer that falls, a heap of stone outside and the
/// rails the forge has made lying in a stack, one bar for each rail it holds. It is a new link in the production chains:
/// stone in, rails out.
/// </summary>
public sealed partial class LineForge : Node3D
{
    private const float Hot = 5;

    private readonly Terrain ground;
    private readonly Node3D hammer = new() { Name = "Hammer" };
    private readonly Node3D stack = new() { Name = "Rails" };
    private readonly List<MeshInstance3D> bars = new();
    private readonly Node3D heap = new() { Name = "StoneHeap" };
    private readonly Node3D glow = new() { Name = "Furnace" };
    private CpuParticles3D smoke = null!;
    private float time;
    private float hot;

    internal LineForgeSite Site { get; }

    /// <summary>How many rails the forge holds, 0 to <see cref="MainLinePlans.RailCapacity"/>.</summary>
    internal int Rails { get; private set; }

    /// <summary>Whether the forge is forging now (furnace glowing, hammer falling, chimney smoking).</summary>
    internal bool Forging => hot > 0;

    /// <summary>Where the rails are loaded and the stone tipped out.</summary>
    internal Vector3 Drop => Site.Drop;

    internal LineForge(LineForgeSite site, Terrain ground)
    {
        Site = site;
        this.ground = ground;
        Name = "LineForge";
        Position = ground.At(site.Foot.X, site.Foot.Z);
        Rotation = new Vector3(0, site.Yaw, 0);
        // Local +Z looks from the forge towards the drop and the road.
        Art.Wall(this, new(0, 1.6f, -.5f), new(6, 3.2f, 4.6f), "a09a90", FacadeStyle.Plaster);
        Art.Block(this, new(0, -.5f, -.5f), new(6.4f, 1.6f, 5), "8a857e");
        var roof = Art.Block(this, new(0, 3.55f, -.5f), new(6.8f, .3f, 5.4f), "4a4540");
        roof.RotationDegrees = new Vector3(6, 0, 0);
        Art.Block(this, new(-1.9f, 4.6f, -1.8f), new(1.1f, 3, 1.1f), "6d625a");
        Art.Block(this, new(-1.9f, 6.2f, -1.8f), new(1.35f, .3f, 1.35f), "4a4540");
        smoke = Art.ChimneySmoke(this, new Vector3(-1.9f, 6.6f, -1.8f));
        smoke.Emitting = false;
        // The open front: the furnace with its glow, and a wooden door standing open.
        Art.Block(this, new(-1.3f, 1.1f, 1.85f), new(1.7f, 1.7f, .25f), "3a3530");
        AddChild(glow);
        Art.Lamp(glow, new(-1.3f, 1.05f, 1.99f), new(1.1f, .9f, .08f), "ff8a2a");
        Art.Lamp(glow, new(-1.3f, 1.05f, 2.02f), new(.6f, .5f, .08f), "ffe08a");
        glow.Visible = false;
        Art.Block(this, new(1.5f, 1.4f, 1.95f), new(1.1f, 2.6f, .12f), "6a4a30");
        // The anvil on a stump, and the hammer that falls on it.
        Art.Cylinder(this, new(1.3f, .45f, 3.3f), .5f, .55f, .9f, "6a4a30");
        Art.Block(this, new(1.3f, 1.05f, 3.3f), new(.9f, .35f, .45f), "30363a");
        Art.Block(this, new(1.3f, 1.3f, 3.3f), new(.35f, .22f, .35f), "30363a");
        AddChild(hammer);
        hammer.Position = new Vector3(1.3f, 1.4f, 3.9f);
        Art.Beam(hammer, new Vector3(0, 0, 0), new Vector3(0, .1f, -.9f), .09f, "8a6040");
        Art.Block(hammer, new(0, .12f, -.9f), new(.45f, .3f, .3f), "4a5258");
        // The stack of rails beside the forge, and the heap of stone that waits to be forged.
        AddChild(stack);
        stack.Position = new Vector3(-4.6f, 0, 1.2f);
        for (int i = 0; i < MainLinePlans.RailCapacity; i++)
        {
            var bar = Art.Block(stack, new(0, .25f + i * .2f, 0), new(.14f, .14f, 4.2f), "7d8890");
            Art.Block(bar, new(0, 0, 0), new(1, 1.3f, .98f), "6a757c");
            bar.Position = new Vector3((i % 2) * .22f - .11f, .22f + (i / 2) * .22f, 0);
            bars.Add(bar);
        }
        Art.Block(stack, new(0, .1f, -1.4f), new(.5f, .2f, .3f), "6a4a30");
        Art.Block(stack, new(0, .1f, 1.4f), new(.5f, .2f, .3f), "6a4a30");
        AddChild(heap);
        heap.Position = new Vector3(4.4f, 0, 1.6f);
        string[] greys = { "8f8f8a", "a7a6a0", "77756f" };
        for (int i = 0; i < 9; i++)
        {
            float size = .5f + (i * 7 % 5) * .1f;
            Art.Ball(
                heap,
                new((i % 3) * .8f - .8f, size * .45f + (i / 6) * .5f, (i / 3 % 2) * .8f),
                new(size, size * .7f, size),
                greys[i % 3]
            );
        }
        SetRails(0);
    }

    /// <summary>Sets how many rails lie in the stack.</summary>
    internal void SetRails(int rails)
    {
        Rails = Math.Clamp(rails, 0, MainLinePlans.RailCapacity);
        for (int i = 0; i < bars.Count; i++)
            bars[i].Visible = i < Rails;
        stack.Visible = Rails > 0;
    }

    /// <summary>A load of stone has been tipped out: the forge works for a while and makes rails of it.</summary>
    internal void Forge(float seconds = Hot) => hot = seconds;

    internal void Tick(float delta, float night)
    {
        time += delta;
        if (hot > 0)
            hot = Math.Max(0, hot - delta);
        bool working = hot > 0;
        glow.Visible = working;
        if (smoke.Emitting != working)
            smoke.Emitting = working;
        // The hammer falls twice a second while the forge works.
        float fall = working ? Mathf.Abs(Mathf.Sin(time * 6)) : 0;
        hammer.Rotation = new Vector3(Mathf.DegToRad(-50 * (1 - fall)), 0, 0);
    }
}
