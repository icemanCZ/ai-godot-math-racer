using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace MathRacer.Valley;

/// <summary>
/// Meshes of the valley that only draw something and are far from where the game starts, or small beside the road:
/// the ground beyond the old valley and the verges of the roads. Where everything stands, the heights and the
/// collision are worked out while the valley is generated; these are built after it, a slice at a time, so that the
/// game shows sooner (the system "far parts" in <c>Game.Systems.cs</c>). A run without a window draws nothing and
/// drops them (<see cref="Drop"/>); a picture or a benchmark builds them at once (<see cref="Finish"/>).
/// </summary>
public sealed class DeferredBuild
{
    private readonly Queue<(string Name, IEnumerator<bool> Work)> queue = new();
    private readonly List<Action> afterwards = new();
    private readonly Stopwatch busy = new();
    private int frames;

    internal bool Done => queue.Count == 0;

    /// <summary>Work that yields after each slice of it, such as one mesh.</summary>
    internal void Add(string name, IEnumerable<bool> work) => queue.Enqueue((name, work.GetEnumerator()));

    /// <summary>Work done in one slice, after everything added before it.</summary>
    internal void Add(string name, Action work) => Add(name, Once(work));

    /// <summary>Clean-up once the work is built or dropped, such as freeing what it was built from.</summary>
    internal void Afterwards(Action cleanUp) => afterwards.Add(cleanUp);

    private static IEnumerable<bool> Once(Action work)
    {
        work();
        yield break;
    }

    /// <summary>Runs slices until about <paramref name="budgetMs"/> milliseconds are used, at least one.</summary>
    internal void Run(double budgetMs)
    {
        if (queue.Count == 0)
            return;
        frames++;
        busy.Start();
        var watch = Stopwatch.StartNew();
        do
        {
            var work = queue.Peek().Work;
            if (work.MoveNext())
                continue;
            work.Dispose();
            queue.Dequeue();
        } while (queue.Count > 0 && watch.Elapsed.TotalMilliseconds < budgetMs);
        busy.Stop();
        if (queue.Count > 0)
            return;
        CleanUp();
        GD.Print($"FAR_PARTS_READY ms={busy.ElapsedMilliseconds} frames={frames}");
    }

    /// <summary>Builds everything that is left now.</summary>
    internal void Finish() => Run(double.PositiveInfinity);

    /// <summary>Leaves all of it out, for a run without a window, which draws nothing.</summary>
    internal void Drop()
    {
        while (queue.Count > 0)
            queue.Dequeue().Work.Dispose();
        CleanUp();
    }

    private void CleanUp()
    {
        foreach (var cleanUp in afterwards)
            cleanUp();
        afterwards.Clear();
    }
}
