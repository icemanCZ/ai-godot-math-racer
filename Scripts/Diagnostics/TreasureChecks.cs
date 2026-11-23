using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;
using MathRacer.App;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The pirate captain's treasure hunt: that every valley has her on the square, that every treasure lies on open ground the
/// car can reach and is always buried in the same place, that a hunt begins only at the captain and ends with the chest (so the
/// next one needs a drive back to her), the detector's warmth, the digging, the locked chest, the coins, the saves of every
/// moment of the hunt and the damaged ones.
/// </summary>
public partial class TreasureChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public TreasureChecks(Game game, World world, Vehicle car)
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

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    private void CarAt(Vector3 spot) => car.SetPose(world.Ground.At(spot.X, spot.Z, .15f), 0);

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("treasure-checks/progress.json");
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), $"save written: {what}");
                var loaded = SaveStore.Read(path) ?? throw new InvalidOperationException($"save refused: {what}");
                game.RestoreProgress(loaded);
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"restoring and capturing again gives the same save: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"treasure-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            TreasureSave Saved() => game.CaptureProgress().Treasure;

            // ---- The captain ----
            var village = world.Village;
            var captain = village.Treasurer;
            Check(
                captain.Role is TreasureRole
                    && world.ActorId(captain) == World.TreasurerId
                    && world.ActorAt(World.TreasurerId) == captain
                    && World.IsTreasurer(World.TreasurerId)
                    && ActorIds.Treasurer == 72,
                "the captain is registered under the next free actor ID"
            );
            Check(captain.DisplayName == CharacterCatalog.Treasurer.Name, "the captain has her name");
            var local = village.Square.Transform.AffineInverse() * captain.Position;
            Check(Mathf.Abs(local.X) < 11 && Mathf.Abs(local.Z) < 6.5f, "the captain stands on the village square");
            Check(village.IsReserved(captain.Position), "the square, where the captain stands, is reserved ground");
            Check(
                Flat(captain.Position, village.Mayor.Position) > Settings.InteractionRange
                    && Flat(captain.Position, village.Glassmaker.Position) > Settings.InteractionRange,
                "the captain stands out of reach of the mayor and the lamp maker"
            );

            // ---- Where treasures lie ----
            var spots = Enumerable.Range(0, 12).Select(world.TreasureSpot).ToList();
            Check(
                spots.SequenceEqual(Enumerable.Range(0, 12).Select(world.TreasureSpot)),
                "the same valley buries the same treasures every time"
            );
            for (int i = 0; i < spots.Count; i++)
            {
                var p = spots[i];
                Check(
                    new Vector2(p.X, p.Z).Length() < WorldTuning.CorePlayableRadius - 20
                        && Flat(p, village.Center) >= 70
                        && world.Routes.Distance(p) >= 5.2f
                        && !world.Plants.NearTrunk(p, 3f)
                        && world.InteractionActors.All(actor => Flat(actor.Position, p) >= 12),
                    $"treasure {i + 1} lies on open ground, far from the village, off the road and clear of the characters"
                );
                Check(
                    !world.InZooZone(p)
                        && !world.InPlaygroundZone(p)
                        && !world.InFarmZone(p)
                        && !world.InGlassworksZone(p),
                    $"treasure {i + 1} is not buried in a zoo, a playground, a farm or a glassworks"
                );
            }
            Check(
                spots.SelectMany((a, i) => spots.Skip(i + 1).Select(b => Flat(a, b))).All(distance => distance >= 20),
                "no two treasures lie next to each other"
            );
            Check(spots[0] != spots[1] && Flat(spots[0], spots[1]) > 40, "the next treasure is somewhere else");

            // ---- The detector ----
            Check(
                TreasureHunt.WarmthAt(3) == TreasureWarmth.Here
                    && TreasureHunt.WarmthAt(10) == TreasureWarmth.Burning
                    && TreasureHunt.WarmthAt(30) == TreasureWarmth.Hot
                    && TreasureHunt.WarmthAt(60) == TreasureWarmth.Warm
                    && TreasureHunt.WarmthAt(120) == TreasureWarmth.Cold
                    && TreasureHunt.WarmthAt(400) == TreasureWarmth.Freezing,
                "the detector goes from freezing to here as the car comes closer"
            );
            Check(
                TreasureHunt.BeepInterval(5) < TreasureHunt.BeepInterval(40)
                    && TreasureHunt.BeepInterval(40) < TreasureHunt.BeepInterval(200)
                    && TreasureHunt.Closeness(5) > TreasureHunt.Closeness(100),
                "the closer the treasure, the faster the beep and the fuller the bar"
            );

            // ---- Nothing is hunted before the captain asks ----
            Check(
                Saved() is { Found: 0, Active: false } && !world.Treasure.Visible && !game.TreasureDigAvailable,
                "at the start there is no treasure in the ground"
            );
            CarAt(spots[0]);
            Frames(2);
            game.Interact();
            Check(game.State == GameState.Exploring, "digging where no treasure was given out does nothing");

            // ---- The captain gives out a treasure ----
            CarAt(captain.Position + new Vector3(2, 0, 2));
            Frames(2);
            game.OpenTreasurer(captain);
            Check(
                game.State == GameState.TreasureStatus && Has("StartTreasureHunt"),
                "talking to the captain offers a treasure"
            );
            RoundTrip("the captain's panel");
            Check(
                game.State == GameState.TreasureStatus && Has("StartTreasureHunt"),
                "the panel comes back after loading"
            );
            game.BeginTreasureHunt();
            Check(game.State == GameState.Exploring && car.Active, "the car drives off to look for the treasure");
            Check(
                Saved() is { Found: 0, Active: true } && world.Treasure.Visible && !world.Treasure.ChestShown,
                "a treasure is buried, with its chest still underground"
            );
            Check(
                Flat(world.Treasure.GlobalPosition, spots[0]) < .01f,
                "it is buried where the seed says, the first treasure of the valley"
            );
            RoundTrip("a hunt is on");
            Check(
                Saved().Active && world.Treasure.Visible && Flat(world.Treasure.GlobalPosition, spots[0]) < .01f,
                "the treasure is buried again in the same place after loading"
            );

            // ---- The cross shows only near ----
            var far = spots[0] + new Vector3(100, 0, 0);
            CarAt(far);
            Frames(2);
            Check(
                !world.Treasure.CrossShown && !game.TreasureDigAvailable,
                "far away, nothing shows and nothing can be dug"
            );
            CarAt(spots[0] + new Vector3(12, 0, 0));
            Frames(2);
            Check(world.Treasure.CrossShown && !game.TreasureDigAvailable, "a red cross shows when the car is near");
            game.Interact();
            Check(game.State == GameState.Exploring, "a few metres too far to dig");

            // ---- Digging, the lock and the chest ----
            int coins = game.Coins;
            CarAt(spots[0]);
            Frames(2);
            Check(game.TreasureDigAvailable, "on the spot the car can dig");
            // Digging is on the talk button, like everything else: the space bar presses it.
            game._UnhandledInput(new InputEventKey { PhysicalKeycode = Key.Space, Pressed = true });
            Check(
                game.State == GameState.Challenge
                    && game.CurrentChallenge != null
                    && !car.Active
                    && game.CaptureProgress().Nearby == World.TreasurerId,
                "digging uncovers a locked chest that asks the captain's question"
            );
            RoundTrip("the question of the chest");
            Check(
                game.State == GameState.Challenge && game.CurrentChallenge != null,
                "the question comes back after loading"
            );
            game.FinishInteraction();
            Check(
                game.State == GameState.Exploring && Saved().Active && game.TreasureDigAvailable,
                "leaving the question keeps the hunt on"
            );
            game.Interact();
            Check(game.State == GameState.Challenge, "the car can dig again");
            ChallengeChecks.Solve(game);
            Check(
                game.State == GameState.Reward
                    && game.Coins == coins + TreasureHunt.Coins(0)
                    && world.Treasure.ChestShown
                    && world.Treasure.IsOpen,
                "a right answer opens the chest and pays its coins"
            );
            Check(Saved() is { Found: 1, Active: false }, "the hunt is over once the chest is open");
            RoundTrip("the reward panel");
            Check(game.State == GameState.Reward, "the reward panel comes back after loading");
            Frames(40);
            game.FinishInteraction();
            Check(game.State == GameState.Exploring && car.Active, "the reward is taken and the car drives on");

            // ---- No new hunt without the captain ----
            Frames(5);
            Check(
                !Saved().Active && !game.TreasureDigAvailable && game.State == GameState.Exploring,
                "no new treasure is given out by itself"
            );
            game.Interact();
            Check(game.State == GameState.Exploring, "digging at the empty hole does nothing");
            Check(world.Treasure.ChestShown, "the open chest stays in the valley until the next hunt");
            RoundTrip("a treasure found");
            Check(
                !world.Treasure.Visible && Saved() is { Found: 1, Active: false },
                "after loading, the hole is gone and one treasure counts"
            );

            // ---- The next hunt starts at the captain, somewhere else ----
            CarAt(captain.Position + new Vector3(2, 0, 2));
            Frames(2);
            game.OpenTreasurer(captain);
            Check(
                game.State == GameState.TreasureStatus && Has("StartTreasureHunt"),
                "the captain offers another treasure"
            );
            game.BeginTreasureHunt();
            Check(
                Saved() is { Found: 1, Active: true }
                    && Flat(world.Treasure.GlobalPosition, spots[1]) < .01f
                    && !world.Treasure.ChestShown,
                "the second treasure is buried where the seed says, with a closed chest"
            );
            CarAt(captain.Position + new Vector3(2, 0, 2));
            Frames(2);
            game.OpenTreasurer(captain);
            Check(
                game.State == GameState.TreasureStatus && !Has("StartTreasureHunt"),
                "while a treasure is being looked for, the captain only says to carry on"
            );
            game.BeginTreasureHunt();
            Check(
                Flat(world.Treasure.GlobalPosition, spots[1]) < .01f,
                "a second hunt cannot be started on top of the first"
            );
            game.FinishInteraction();

            // ---- Every fifth chest is big ----
            for (int found = 1; found < 5; found++)
            {
                CarAt(spots[found]);
                Frames(2);
                coins = game.Coins;
                game.Interact();
                Check(game.State == GameState.Challenge, $"treasure {found + 1} is dug up");
                ChallengeChecks.Solve(game);
                Check(
                    game.Coins == coins + TreasureHunt.Coins(found),
                    $"treasure {found + 1} holds {TreasureHunt.Coins(found)} coins"
                );
                Frames(40);
                game.FinishInteraction();
                if (found < 4)
                {
                    CarAt(captain.Position + new Vector3(2, 0, 2));
                    Frames(2);
                    game.OpenTreasurer(captain);
                    game.BeginTreasureHunt();
                    Check(Saved().Active, $"the captain gives out treasure {found + 2}");
                }
            }
            Check(
                TreasureHunt.Coins(4) == 40 && TreasureHunt.Coins(3) == 20 && TreasureHunt.UntilBig(5) == 5,
                "the fifth chest is big and the count starts again"
            );
            Check(Saved() is { Found: 5, Active: false }, "five treasures are found");

            // ---- The panel counts ----
            CarAt(captain.Position + new Vector3(2, 0, 2));
            Frames(2);
            game.OpenTreasurer(captain);
            Check(
                game.FindChild("TreasureChests", true, false) is Node chests
                    && chests.GetChildCount() == TreasureHunt.BigEvery,
                "the panel shows five coins towards the big chest"
            );
            game.FinishInteraction();

            // ---- A save from before the hunt ----
            var current = game.CaptureProgress();
            var old = JsonSerializer.SerializeToNode(current)!.AsObject();
            old.Remove("Treasure");
            string oldPath = DiagnosticArtifacts.PathFor("treasure-checks/before-treasure.json");
            System.IO.File.WriteAllText(oldPath, old.ToJsonString());
            var before = SaveStore.Read(oldPath) ?? throw new InvalidOperationException("The older save was refused.");
            game.RestoreProgress(before);
            Check(
                Saved() is { Found: 0, Active: false } && !world.Treasure.Visible,
                "a save from before the hunt loads with no treasure found and none in the ground"
            );

            // ---- Damaged saves ----
            Rejected(data => data.Treasure.Found = -1, "a negative number of treasures");
            Rejected(data => data.Treasure.Found = TreasureHunt.Limit + 1, "more treasures than the limit");
            Rejected(data => data.Treasure = null!, "no treasure section");
            Rejected(
                data =>
                {
                    data.State = GameState.TreasureStatus;
                    data.Nearby = -1;
                },
                "the captain's panel without the captain"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.TreasureStatus;
                    data.Nearby = World.MayorId;
                },
                "the captain's panel beside the mayor"
            );
            game.RestoreProgress(SaveStore.Read(path)!);
            CarAt(captain.Position + new Vector3(2, 0, 2));
            Frames(2);
            game.OpenTreasurer(captain);
            game.BeginTreasureHunt();
            CarAt(world.TreasureSpot(Saved().Found));
            Frames(2);
            game.Interact();
            Check(game.State == GameState.Challenge, "the chest of the next treasure asks its question");
            Rejected(data => data.Treasure.Active = false, "the chest's question with no treasure being looked for");
            Rejected(data => data.Quiz.Handover = true, "the chest's question as a handover");

            GD.Print("TREASURE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
