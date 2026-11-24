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
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The dragon under the mountains (<c>docs/DRAGON_PLAN.md</c>): he lies on his ledge beside the homestead, inside the
/// valley and off the roads, with the mountain slope on the road to the mountains; six deliveries from the slope, where he
/// asks the loading question himself, raise his nest; his riddle of three questions, which survives a save between its
/// questions, makes him sneeze his fire back, lights the fire, hatches the egg, pays its coins and brings the little
/// dragon; the flight on his back takes off, stays high above the valley and lands on the ledge again, paying only the
/// first time; a save from before the dragon loads with an untouched nest; and the damaged saves are turned down.
/// </summary>
public partial class DragonChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public DragonChecks(Game game, World world, Vehicle car)
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

    private bool Has(string name) => game.FindChildren(name, "", true, false).Any(node => !node.IsQueuedForDeletion());

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

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
            string path = DiagnosticArtifacts.PathFor("dragon-checks/progress.json");
            var trailer = game.Trailer;
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
                if (before != after)
                {
                    int at = 0;
                    while (at < Math.Min(before.Length, after.Length) && before[at] == after[at])
                        at++;
                    int from = Math.Max(0, at - 120);
                    GD.Print($"SAVED:    {before.Substring(from, Math.Min(240, before.Length - from))}");
                    GD.Print($"RESTORED: {after.Substring(from, Math.Min(240, after.Length - from))}");
                }
                Check(before == after, $"the dragon restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"dragon-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Near(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            // ---- Where he lies ----
            var site = world.DragonPlace;
            var slope = world.SlopePlace;
            var lair = world.DragonLair;
            var dragon = world.Dragon;
            var body = world.DragonBody;
            var flight = world.DragonFlight;
            var home = world.BorderHomes[(int)Border.Mountains];
            Check(home != null, "the valley has a homestead under the mountains");
            Check(
                site != null && slope != null && lair != null && dragon != null && body != null && flight != null,
                "the dragon lies beside it, with his ledge, his nest, his flight and the mountain slope"
            );
            Check(world.ActorId(dragon!) == World.DragonId, $"the dragon has the ID {World.DragonId}");
            Check(dragon!.Role is DragonRole && lair!.Dragon == dragon, "the dragon sends the trailer for his nest");
            foreach (var p in new[] { site!.Lair, site.Nest, site.Drop, site.Talk, slope!.Position })
                Check(ValleyBounds.Contains(p.X, p.Z, 5), $"{p} lies inside the valley");
            Check(
                Flat(site.Drop, home!.Road) < 100,
                $"the drop ring is {Flat(site.Drop, home.Road):0} m from the end of the road"
            );
            Check(
                world.Routes.Distance(site.Lair) > 20 && world.OutlandRoutes.Distance(site.Lair) >= 9,
                "he lies off the roads"
            );
            Check(
                Flat(site.Drop, home.Road) < 45 || world.OutlandRoutes.Distance(site.Drop) < 4,
                "the drop ring is beside the homestead or on the road"
            );
            Check(
                Flat(site.Talk, site.Drop) < 6 && Flat(site.Talk, site.Lair) > 10,
                "the car meets him under his chin, by the drop ring"
            );
            int inZone = 0,
                bare = 0;
            for (float ahead = DragonSite.ZoneBack; ahead <= DragonSite.ZoneFront; ahead += 3)
            for (float across = -DragonSite.ZoneAcross; across <= DragonSite.ZoneAcross; across += 3)
            {
                var q = world.Ground.At(site.Local(across, ahead).X, site.Local(across, ahead).Z);
                if (world.InDragonZone(q))
                    inZone++;
                if (world.Plants.OutlandCleared?.Invoke(q) == true && !world.Grass.CanGrow(q))
                    bare++;
            }
            Check(inZone > 30 && bare == inZone, "no plant and no grass grows on his ledge");
            float clearance = flight!.LowestClearance(world.Ground);
            Check(clearance > 25, $"his flight stays high above the valley, {clearance:0} m at the lowest");
            // The way from the slope to the nest, by road, with the loaded trailer.
            var arrows = new Node3D { Name = "DragonArrows" };
            world.AddChild(arrows);
            var toward = site.Road - slope.Road;
            car.SetPose(world.Ground.At(slope.Road.X, slope.Road.Z, .15f), Mathf.Atan2(-toward.X, -toward.Z));
            var guide = new RouteGuide(world, car, arrows, () => site.Drop);
            guide.Retarget(site.Road);
            guide.CreateArrows();
            float length = 0;
            for (int i = 1; i < guide.RoutePoints.Count; i++)
                length += Flat(guide.RoutePoints[i - 1], guide.RoutePoints[i]);
            Check(length > 0 && length < 900, $"the loads are carried {length:0} m from the slope to the nest");
            arrows.QueueFree();
            var little = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Dragonling);
            Check(
                little != null && !little.IsOwned && little.Collector == dragon,
                "the little dragon waits in the egg by the dragon"
            );
            Check(!body!.Healthy && !lair.Burning && lair.Stage == 0, "at first he has a cold and the nest is bare");

            // ---- Saves that are turned down ----
            Rejected(data => data.Dragon = null!, "no dragon section");
            Rejected(data => data.Dragon.Stage = DragonPlans.Trips + 1, "a nest with seven deliveries");
            Rejected(data => data.Dragon.Stage = -1, "a nest with a negative stage");
            Rejected(
                data =>
                {
                    data.Dragon.Stage = 3;
                    data.Dragon.Riddle = 1;
                },
                "a riddle before the nest is finished"
            );
            Rejected(
                data =>
                {
                    data.Dragon.Stage = DragonPlans.Trips;
                    data.Dragon.Riddle = DragonPlans.Riddles + 1;
                },
                "a riddle with four answers"
            );
            Rejected(data => data.Dragon.Flown = true, "a flight before the egg has hatched");
            Rejected(
                data =>
                {
                    data.State = GameState.DragonStatus;
                    data.Nearby = World.HunterId;
                },
                "the dragon's panel at the hunter"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Dragon, Index = 1 },
                "a trip for a second dragon"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.TradeStatus;
                    data.Nearby = World.DragonId;
                },
                "a shop's panel at the dragon"
            );
            // A save from before the dragon: the section is not in the file, and the nest is bare.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Dragon");
                string file = DiagnosticArtifacts.PathFor("dragon-checks/before-the-dragon.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(read is { Dragon: { Stage: 0, Riddle: 0, Flown: false } }, "a save from before the dragon loads");
                game.RestoreProgress(read!);
                Check(lair.Stage == 0 && !lair.Hatched, "and his nest is bare");
            }

            // ---- Six deliveries ----
            Check(
                DragonPlans.Trips == 6 && DragonPlans.Parts.Length == DragonPlans.Trips,
                "six deliveries make the nest"
            );
            int coinsBefore = game.Coins;
            for (int step = 0; step < DragonPlans.Trips; step++)
            {
                bool save = step is 0 or 4;
                var cargo = DragonPlans.CargoOf(step);
                Near(dragon.Position);
                game.OpenDragon(dragon);
                Check(
                    game.State == GameState.DragonStatus && Has("StartDragonTrip"),
                    $"the dragon asks for delivery {step + 1} of his nest"
                );
                if (save)
                    RoundTrip("the dragon's panel");
                game.BeginDragonTrip();
                Check(game.State == GameState.Challenge, "the trip asks its quiz first");
                if (save)
                    RoundTrip("the quiz before a trip for the nest");
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.DragonLair == lair && trip.Step == step && trailer.Attached,
                    $"delivery {step + 1} sends the empty trailer"
                );
                Check(
                    trip!.Load == cargo && trip.Source.Position == slope.Position && trip.Asker == dragon,
                    $"the {cargo} is loaded at the mountain slope and the dragon asks the question"
                );
                if (save)
                    RoundTrip($"a trip for the nest ({step + 1})");
                Near(slope.Position);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the loading question");
                if (save)
                    RoundTrip("the dragon's loading question at the slope");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (save)
                    RoundTrip($"a full trailer for the nest ({step + 1})");
                Near(site.Drop);
                Frames(1);
                Check(trailer.Dumping, "the bed tips in front of the nest");
                Frames(40);
                Check(lair.Stage == step + 1, $"delivery {step + 1} brings {DragonPlans.Parts[step]}");
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
                Check(!little!.IsOwned && game.Coins == coinsBefore, "no coins and no little dragon yet");
            }
            Check(lair.Complete && !lair.Hatched, "the sixth delivery finishes the nest");
            RoundTrip("a finished nest");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Dragon, Index = 0 },
                "a trip to a nest that has no delivery left"
            );

            // ---- The riddle ----
            Near(dragon.Position);
            game.OpenDragon(dragon);
            Check(
                game.State == GameState.DragonStatus && !Has("StartDragonTrip") && Has("StartDragonRiddle"),
                "the finished nest asks for the riddle"
            );
            game.BeginDragonRiddle();
            Check(
                game.State == GameState.Challenge && game.CurrentChallenge != null,
                "the first question of the riddle"
            );
            RoundTrip("the first question of the riddle");
            ChallengeChecks.Solve(game);
            Check(
                lair.Riddle == 1 && body.Breath == 1 && game.State == GameState.Challenge,
                "he breathes in and asks the second"
            );
            RoundTrip("the second question of the riddle");
            Check(body.Breath == 1, "a save keeps his breath");
            ChallengeChecks.Solve(game);
            Check(lair.Riddle == 2 && game.State == GameState.Challenge, "and the third");
            Rejected(data => data.Dragon.Flown = true, "a flight during the riddle");
            ChallengeChecks.Solve(game);
            Check(lair.Hatched && game.State != GameState.Challenge, "the third answer ends the riddle");
            Check(game.Coins == coinsBefore + DragonPlans.Coins, "the hatched egg pays its coins");
            Check(little!.IsOwned, "the little dragon comes");
            bool flamed = false;
            for (int i = 0; i < 40; i++)
            {
                Frames(1);
                flamed |= body.Flaming;
            }
            Check(flamed && lair.Burning && body.Healthy, "his sneeze lights the fire, and his cold is gone");
            RoundTrip("a hatched egg");
            Check(lair.Burning && body.Healthy && !body.Flying, "the fire still burns after a load");

            // ---- The flight ----
            game.FinishInteraction();
            Near(dragon.Position);
            game.OpenDragon(dragon);
            Check(game.State == GameState.DragonStatus && Has("StartDragonFlight"), "he offers a flight");
            int beforeFlight = game.Coins;
            var rest = body.GlobalTransform;
            game.BeginDragonFlight();
            Check(game.DragonFlying && body.Flying, "he takes off with the player on his back");
            Check(game.Coins == beforeFlight + DragonPlans.FlightCoins, "the first flight pays");
            Frames(150);
            Check(
                game.DragonFlying
                    && body.GlobalPosition.Y - world.Ground.Height(body.GlobalPosition.X, body.GlobalPosition.Z) > 25,
                "he flies high over the valley"
            );
            RoundTrip("a save during the flight");
            Check(!game.DragonFlying && !body.Flying, "a load puts him back on his ledge");
            Check(body.GlobalTransform.Origin.DistanceTo(rest.Origin) < .01f, "where he lay before");
            game.FinishInteraction();
            Near(dragon.Position);
            game.OpenDragon(dragon);
            game.BeginDragonFlight();
            Check(
                game.DragonFlying && game.Coins == beforeFlight + DragonPlans.FlightCoins,
                "a second flight pays nothing"
            );
            Frames(Mathf.CeilToInt(DragonFlight.Duration * 10) + 5);
            Check(!game.DragonFlying && !body.Flying, "the flight ends by itself");
            Check(body.GlobalTransform.Origin.DistanceTo(rest.Origin) < .01f, "he lands on his ledge again");
            RoundTrip("after the flights");
            GD.Print("DRAGON_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
