using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The shells of the beach (<c>docs/SHELLS_PLAN.md</c>): a cove below the homestead of the keeper of the sea with a sign
/// above it; the sea falls and rises with the clock twice a day; every high tide leaves new shells on the wet sand beyond
/// the edge of the valley, which only the low tide uncovers and where the car may drive; the car picks a shell up by
/// driving to it, which pays more for the first of a kind; the eighth kind brings the crab; a save keeps the tide and the
/// shells, a save from before the shells loads with none of them, and the damaged saves are turned down.
/// </summary>
public partial class ShellChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public ShellChecks(Game game, World world, Vehicle car)
    {
        this.game = game;
        this.world = world;
        this.car = car;
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        GD.Print("PASS: " + message);
    }

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("shell-checks/progress.json");
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                foreach (var rule in SaveRules.All.Where(rule => rule.Invalid(saved)))
                    GD.Print($"BROKEN RULE: {rule.Name}");
                Check(SaveRules.All.All(rule => !rule.Invalid(saved)), $"save accepted: {what}");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save read back: {what}");
                game.RestoreProgress(loaded!);
                string before = JsonSerializer.Serialize(saved),
                    after = JsonSerializer.Serialize(game.CaptureProgress());
                Check(before == after, $"the shells restore: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"shell-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Load(Action<SaveData> change)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"shell-checks/changed-{files++}.json");
                Check(SaveStore.Write(data, file), "changed save written");
                var read = SaveStore.Read(file);
                Check(read != null, "changed save read back");
                game.RestoreProgress(read!);
            }
            void At(float clock)
            {
                game.Weather.SetClock(clock, true);
                Frames(1);
            }
            void Pose(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            // ---- The cove, its sign and the shells of a tide ----
            var cove = world.ShellCove;
            var shells = world.BeachShells;
            var borders = WorldLayout.Plan.Borders;
            Check(cove != null && shells != null && world.ShellSign != null, "the valley has the cove and its sign");
            var sign = world.ShellSign!.Value;
            Check(
                ValleyBounds.Contains(sign.X, sign.Z) && sign.Z > ValleyBounds.HalfSize - 10,
                $"the sign {sign} stands at the edge of the valley above the beach"
            );
            if (world.BorderHomes[(int)Border.Sea] is { } home)
                Check(
                    Mathf.Abs(cove!.Value.X - home.Road.X) < 1,
                    "the cove lies below the homestead of the keeper of the sea"
                );
            var first = world.ShellsOfTide(0);
            var second = world.ShellsOfTide(1);
            Check(first.Length >= 6 && second.Length >= 6, $"a tide leaves {first.Length} shells");
            Check(
                first.Select(s => s.Position).SequenceEqual(world.ShellsOfTide(0).Select(s => s.Position))
                    && !first.Select(s => s.Position).SequenceEqual(second.Select(s => s.Position)),
                "every tide leaves its own shells, the same in every game"
            );
            foreach (var shell in first.Concat(second))
            {
                var p = shell.Position;
                string name = $"{ShellPlans.Name(shell.Kind)} {p}";
                Check(
                    Mathf.Abs(p.X - cove!.Value.X) <= World.CoveHalfWidth && p.Z > ValleyBounds.HalfSize,
                    $"{name} lies in the cove, beyond the edge of the valley"
                );
                Check(
                    p.Y < ValleyBorders.SeaLevel && p.Y > ValleyBorders.SeaLevel - Tide.Range + .2f,
                    $"{name} lies on the sand that only the low tide uncovers"
                );
                Check(River.Distance(p.X, p.Z) > River.HalfWidth(p.Z) + 5, $"{name} lies off the river's mouth");
                Check(borders.Sand(p.X, p.Z) > .9f, $"{name} lies on sand");
            }
            var crab = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Crab);
            Check(
                crab != null && !crab.IsOwned && crab.Collector == world.LighthouseKeeper,
                "the crab waits by the keeper of the sea"
            );

            // ---- The tide ----
            At(0);
            Check(
                Mathf.Abs(world.TideOffset) < .01f,
                $"the sea is high at the start of the day ({world.TideOffset:0.00})"
            );
            Check(game.ShellsOnSand.All(shell => !shells!.Shown(shell.Number)), "the high tide covers the shells");
            Check(
                game.ShellsOnSand.All(shell => world.SeaDepth(shell.Position) > 0),
                "the car cannot drive to them through the sea"
            );
            At(Tide.Period / 2);
            Check(
                Mathf.Abs(world.TideOffset + Tide.Range) < .01f,
                $"a quarter of a day later the sea is low ({world.TideOffset:0.00})"
            );
            Check(game.ShellsOnSand.All(shell => shells!.Shown(shell.Number)), "the low tide uncovers every shell");
            Check(
                game.ShellsOnSand.All(shell => world.SeaDepth(shell.Position) < 0),
                "and the car may drive to them on the wet sand"
            );
            Check(
                world.SeaDepth(new Vector3(0, 0, -ValleyBounds.HalfSize - 5)) == float.PositiveInfinity
                    && world.SeaDepth(new Vector3(ValleyBounds.HalfSize + 5, 0, 1180)) == float.PositiveInfinity,
                "past the other edges the car may still not drive"
            );

            // ---- Picking up shells ----
            Pose(new Vector3(cove!.Value.X, 0, ValleyBounds.HalfSize - 20));
            Frames(2);
            Check(game.ShellsHudShown, "by the cove the corner panel shows the shells");
            int coins = game.Coins;
            var shell1 = game.ShellsOnSand[0];
            var probe = shell1.Position + new Vector3(ShellPlans.PickReach + 3, 0, 0);
            Pose(probe);
            Frames(1);
            Check(!game.Shells.IsPicked(shell1.Number), "a shell a little way off stays on the sand");
            Pose(shell1.Position);
            Frames(1);
            Check(
                game.Shells.IsPicked(shell1.Number) && !shells!.Shown(shell1.Number),
                "the car picks up a shell it drives to"
            );
            Check(
                game.Coins == coins + ShellPlans.CoinsPerShell + ShellPlans.NewKindCoins
                    && game.Shells.CountOf(shell1.Kind) == 1,
                "the first of its kind pays more"
            );
            var same = game.ShellsOnSand.FirstOrDefault(shell =>
                shell.Number != shell1.Number && shell.Kind == shell1.Kind
            );
            if (same != null)
            {
                coins = game.Coins;
                Pose(same.Position);
                Frames(1);
                Check(
                    game.Coins == coins + ShellPlans.CoinsPerShell && game.Shells.CountOf(shell1.Kind) == 2,
                    "another of the same kind pays a coin"
                );
            }
            RoundTrip("shells picked up");
            Check(game.Shells.IsPicked(shell1.Number), "after a load the shell is still picked up");

            // ---- The next high tide brings new shells ----
            int tide = game.Shells.Tide;
            At(Tide.Period - .5f);
            Check(game.Shells.Tide == tide, "until the high tide the shells stay");
            At(Tide.Period + .5f);
            Check(
                game.Shells.Tide == tide + 1
                    && game.Shells.Picked.Length == 0
                    && game.ShellsOnSand.Select(s => s.Position)
                        .SequenceEqual(world.ShellsOfTide(tide + 1).Select(s => s.Position)),
                "the high tide brings the next shells"
            );
            RoundTrip("the next tide");

            // ---- Saves that are turned down ----
            Rejected(data => data.Shells = null!, "no section of the shells");
            Rejected(data => data.Shells.Picked = null!, "no list of the shells picked up");
            Rejected(data => data.Shells.Tide = -1, "a tide before the first");
            Rejected(data => data.Shells.Picked = new[] { ShellPlans.PerTide }, "a shell the tide did not bring");
            Rejected(data => data.Shells.Picked = new[] { 1, 1 }, "a shell picked up twice");
            Rejected(data => data.Shells.Kinds = new int[3], "the wrong number of kinds");
            Rejected(data => data.Shells.Kinds = new[] { 1, -1, 0, 0, 0, 0, 0, 0 }, "fewer than no shells of a kind");
            // A save from before the shells: the section is not in the file, and none is picked up.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Shells");
                string file = DiagnosticArtifacts.PathFor("shell-checks/before-the-shells.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read is { Shells: { Tide: 0, Picked.Length: 0, Kinds.Length: ShellPlans.Kinds } },
                    "a save from before the shells loads"
                );
                game.RestoreProgress(read!);
                Check(game.Shells.KindsFound == 0 && game.Shells.Tide == 0, "and no shell is picked up");
            }

            // ---- The last kind and the crab ----
            int missing = ShellPlans.Pearl;
            int found = Enumerable.Range(0, 400).First(t => world.ShellsOfTide(t).Any(s => s.Kind == missing));
            Load(data =>
            {
                data.Shells.Tide = found;
                data.Shells.Picked = Array.Empty<int>();
                data.Shells.Kinds = Enumerable.Range(0, ShellPlans.Kinds).Select(k => k == missing ? 0 : 2).ToArray();
                data.Day.Clock = Tide.Period / 2;
            });
            Frames(1);
            Check(
                game.Shells.Tide == found && game.Shells.KindsFound == 7 && !crab!.IsOwned,
                "seven kinds, no crab yet"
            );
            var pearl = game.ShellsOnSand.First(s => s.Kind == missing);
            coins = game.Coins;
            Pose(pearl.Position);
            Frames(1);
            Check(
                game.Coins == coins + ShellPlans.CoinsPerShell + ShellPlans.NewKindCoins + ShellPlans.AllKindsCoins,
                "the last kind pays a bonus"
            );
            Check(game.Shells.Complete && crab!.IsOwned, "every kind brings the crab");
            RoundTrip("every kind in a save");
            GD.Print("SHELL_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
