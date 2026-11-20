using System;
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
/// The small friends who hide beside the roads: eight ladybugs, hedgehogs and frogs, found by driving close and pressing
/// E or the space bar, saved by their numbers and earning a pet for every kind that is found completely.
/// </summary>
public partial class HiddenChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public HiddenChecks(Game game, World world, Vehicle car)
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

    private void Frames(int count)
    {
        for (int i = 0; i < count; i++)
            game._Process(.1);
    }

    private void Talk() => game._UnhandledInput(new InputEventAction { Action = "talk", Pressed = true });

    private void Space() =>
        game._UnhandledInput(
            new InputEventKey
            {
                Keycode = Key.Space,
                PhysicalKeycode = Key.Space,
                Pressed = true,
            }
        );

    private CollectorPet Pet(CollectorPetKind kind) => world.WorldPets.First(p => p.Kind == kind);

    private void FindAll(HiddenKind kind)
    {
        foreach (var friend in world.Hidden.Friends.Where(f => f.Kind == kind && !f.Found).ToList())
        {
            car.SetPose(friend.Position, 0);
            Frames(1);
            Talk();
        }
    }

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("hidden-checks/progress.json");
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
                string file = DiagnosticArtifacts.PathFor($"hidden-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- Placement ----
            var hidden = world.Hidden;
            var friends = hidden.Friends;
            Check(
                friends.Count == HiddenFriends.Total && HiddenFriends.Total == 24,
                "twenty-four friends hide in the valley"
            );
            foreach (var kind in Enum.GetValues<HiddenKind>())
                Check(friends.Count(f => f.Kind == kind) == HiddenFriends.PerKind, $"eight friends of the kind {kind}");
            Check(
                friends.All(f =>
                    world.Routes.Distance(f.Position) >= 5.2f
                    && world.Routes.Distance(f.Position) < 12
                    && !world.Plants.NearTrunk(f.Position, 1.5f)
                    && world.InteractionActors.All(a => Flat(a.Position, f.Position) >= 10)
                ),
                "every friend is beside a road, off it, clear of trunks and of the characters"
            );
            Check(
                friends.All(f => !world.InFarmZone(f.Position) && !world.InBakeryZone(f.Position))
                    && friends.All(f => !world.InGlassworksZone(f.Position)),
                "no friend hides on the farm, the bakery or the glassworks"
            );
            Check(
                friends
                    .SelectMany((a, i) => friends.Skip(i + 1).Select(b => Flat(a.Position, b.Position)))
                    .All(distance => distance >= 9),
                "no two friends hide next to each other"
            );

            // ---- Finding one ----
            Check(hidden.FoundCount == 0 && friends.All(f => f.Visible), "nothing is found at the start");
            car.SetPose(world.Ground.At(friends[0].Position.X + 40, friends[0].Position.Z, .15f), 0);
            Frames(2);
            Check(
                friends[0].HintShown || Flat(car.Position, friends[0].Position) > 34,
                "a question mark shows near a friend"
            );
            hidden.Ping(5);
            Check(friends.All(f => f.Pinged), "the lookout lights every friend who is still hiding");
            car.SetPose(friends[0].Position, 0);
            Frames(3);
            Check(
                !friends[0].Found && hidden.FoundCount == 0 && game.HiddenNearby == friends[0],
                "driving close alone does not find a friend, it only offers it"
            );
            Space();
            Check(friends[0].Found && hidden.FoundCount == 1, "the space bar finds the friend beside the car");
            Check(game.HiddenNearby != friends[0], "a friend who is found is not offered again");
            Space();
            Frames(1);
            Check(hidden.FoundCount == 1, "a friend who is found stays one");
            Frames(20);
            Check(!friends[0].Visible, "the friend who was found is gone after the hop");
            Check(!friends[0].Pinged, "a friend who was found has no beam");
            RoundTrip("one friend found");
            Check(
                hidden.FoundCount == 1 && friends[0].Found && !friends[0].Visible,
                "the found friend is still gone after a reload"
            );

            // ---- Eight of a kind earn the pet ----
            var ladybug = Pet(CollectorPetKind.Ladybug);
            var hedgehog = Pet(CollectorPetKind.Hedgehog);
            var frog = Pet(CollectorPetKind.Frog);
            Check(
                ladybug.Goal == 8 && hedgehog.Goal == 8 && frog.Goal == 8 && !ladybug.IsOwned,
                "three pets wait for eight friends each"
            );
            Check(ladybug.Collector == world.Witch, "the witch keeps them");
            FindAll(HiddenKind.Ladybug);
            Frames(2);
            Check(
                ladybug.IsOwned && ladybug.IsActive && !hedgehog.IsOwned && !frog.IsOwned,
                "all eight ladybugs earn Beruška Tečka, and only her"
            );
            Check(game.CelebrationShowing, "finding the last ladybug is celebrated");
            RoundTrip("ladybugs found");
            Check(
                ladybug.IsActive && hidden.FoundOf(HiddenKind.Ladybug) == 8,
                "the ladybug pet comes along after a reload"
            );

            FindAll(HiddenKind.Hedgehog);
            FindAll(HiddenKind.Frog);
            Frames(2);
            Check(
                hedgehog.IsOwned && frog.IsOwned && hidden.FoundCount == HiddenFriends.Total,
                "all friends found, all pets earned"
            );
            RoundTrip("all friends found");
            Check(hidden.FoundCount == HiddenFriends.Total && frog.IsActive, "everything is kept after a reload");

            // ---- A save from before the friends ----
            var current = game.CaptureProgress();
            var old = JsonSerializer.SerializeToNode(current)!.AsObject();
            old.Remove("Hidden");
            string oldPath = DiagnosticArtifacts.PathFor("hidden-checks/before-friends.json");
            System.IO.File.WriteAllText(oldPath, old.ToJsonString());
            var before = SaveStore.Read(oldPath) ?? throw new InvalidOperationException("The older save was refused.");
            game.RestoreProgress(before);
            Check(
                hidden.FoundCount == 0 && friends.All(f => f.Visible) && !ladybug.IsOwned,
                "a save from before the friends loads with every friend hiding and no pet"
            );

            // ---- Damaged saves ----
            Rejected(data => data.Hidden.Found = new[] { 3, 3 }, "a friend found twice");
            Rejected(data => data.Hidden.Found = new[] { HiddenFriends.Total }, "a friend who does not exist");
            Rejected(data => data.Hidden.Found = new[] { -1 }, "a negative friend");
            Rejected(data => data.Hidden.Found = Enumerable.Range(0, 25).ToArray(), "more friends than hide");
            Rejected(data => data.Hidden.Found = null!, "no list of friends");

            GD.Print("HIDDEN_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
