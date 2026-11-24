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
/// The balloon station (<c>docs/BALLOON_PLAN.md</c>): it stands beside a road of the land beyond the old valley, far from
/// the village and off the roads, with a balloon tall enough to be seen from afar and the balloonist where the car meets
/// her from the road; her question lets the balloon go; the wind carries it high above the valley along its way, the
/// burner makes it climb and the valve sink; a star the basket reaches is caught and pays, a star it misses fades; the
/// rooster comes with the twentieth star; a flight that catches every star pays a bonus when it lands; the talk button
/// and a load put the balloon back on its field; a save from before the station loads with no flight; and the damaged
/// saves are turned down.
/// </summary>
public partial class BalloonChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public BalloonChecks(Game game, World world, Vehicle car)
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
            string path = DiagnosticArtifacts.PathFor("balloon-checks/progress.json");
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
                Check(before == after, $"the balloon station restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"balloon-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Load(Action<SaveData> change)
            {
                var data = game.CaptureProgress();
                change(data);
                string file = DiagnosticArtifacts.PathFor($"balloon-checks/changed-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file), "changed save written");
                var read = SaveStore.Read(file);
                Check(read != null, "changed save read back");
                game.RestoreProgress(read!);
            }
            void Near(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            // ---- Where it stands ----
            var site = world.BalloonPlace;
            var station = world.BalloonStation;
            var balloon = world.Balloon;
            var stars = world.BalloonStars;
            var way = world.BalloonFlight;
            var balloonist = world.Balloonist;
            Check(
                site != null
                    && station != null
                    && balloon != null
                    && stars != null
                    && way != null
                    && balloonist != null,
                "the valley has a balloon station with its balloon, its stars, its way and the balloonist"
            );
            Check(world.ActorId(balloonist!) == World.BalloonistId, $"the balloonist has the ID {World.BalloonistId}");
            Check(balloonist!.Role is BalloonistRole && station!.Balloonist == balloonist, "she flies the balloon");
            var pad = site!.Pad;
            Check(ValleyBounds.Contains(pad.X, pad.Z, 20), $"{pad} lies inside the valley");
            Check(
                Mathf.Max(Mathf.Abs(pad.X), Mathf.Abs(pad.Z)) > WorldTuning.CoreRadius,
                "the station is beyond the old valley"
            );
            float fromVillage = Flat(pad, world.Village.Center);
            Check(fromVillage > 480, $"the station is {fromVillage:0} m from the village");
            Check(
                world.Routes.Distance(pad) > 20 && world.OutlandRoutes.Distance(pad) > 12,
                "its field lies off the roads"
            );
            Check(
                world.OutlandRoutes.Distance(site.Talk) < Settings.InteractionRange + 2,
                "the car meets the balloonist from the road"
            );
            float top = balloon!.GlobalPosition.Y + HotAirBalloon.MouthHeight + HotAirBalloon.EnvelopeHeight;
            float aboveGround = top - world.Ground.Height(pad.X, pad.Z);
            Check(aboveGround > 30, $"the balloon rises {aboveGround:0} m above its field, seen from afar");
            Check(
                Flat(balloon.GlobalPosition, pad) < .01f
                    && station.Tethered
                    && Mathf.Abs(station.Lift - BalloonStation.Hover) < .01f,
                "the balloon floats on its ropes above the field"
            );
            int inZone = 0,
                bare = 0;
            for (float along = BalloonSite.ZoneAlongMin; along <= BalloonSite.ZoneAlongMax; along += 3)
            for (float across = -BalloonSite.ZoneAcross; across <= BalloonSite.ZoneAcross; across += 3)
            {
                var q = world.Ground.At(site.Local(across, along).X, site.Local(across, along).Z);
                if (world.InBalloonZone(q))
                    inZone++;
                if (world.Plants.OutlandCleared?.Invoke(q) == true && !world.Grass.CanGrow(q))
                    bare++;
            }
            Check(inZone > 60 && bare == inZone, "no plant and no grass grows on the field");
            float clearance = way!.LowestClearance(world.Ground);
            Check(clearance > 10, $"the way stays above the hills, {clearance:0} m at the lowest");
            Check(
                way.Length is > 500 and < 1200 && way.Duration < 115,
                $"a flight is {way.Length:0} m long and takes {way.Duration:0} s"
            );
            for (int star = 1; star < BalloonFlight.Stars; star++)
                Check(
                    way.StarDistance(star) - way.StarDistance(star - 1) > 4 * BalloonRide.CatchAlong,
                    $"star {star + 1} hangs well apart from star {star}"
                );
            var rooster = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Rooster);
            Check(
                rooster != null && !rooster.IsOwned && rooster.Collector == balloonist,
                "the rooster waits by the balloonist"
            );

            // ---- Saves that are turned down ----
            Rejected(data => data.Balloon = null!, "no balloon section");
            Rejected(data => data.Balloon.Flights = -1, "a negative count of flights");
            Rejected(
                data =>
                {
                    data.Balloon.Flights = 2;
                    data.Balloon.Stars = 2 * BalloonFlight.Stars + 1;
                },
                "more stars than the flights had"
            );
            Rejected(data => data.Balloon.Stars = -1, "a negative count of stars");
            Rejected(
                data =>
                {
                    data.State = GameState.BalloonStatus;
                    data.Nearby = World.HunterId;
                },
                "the balloonist's panel at the hunter"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.TradeStatus;
                    data.Nearby = World.BalloonistId;
                },
                "a shop's panel at the balloonist"
            );
            // A save from before the station: the section is not in the file, and nobody has flown.
            {
                var old = JsonNode.Parse(JsonSerializer.Serialize(game.CaptureProgress()))!.AsObject();
                old.Remove("Balloon");
                string file = DiagnosticArtifacts.PathFor("balloon-checks/before-the-balloon.json");
                File.WriteAllText(file, old.ToJsonString());
                var read = SaveStore.Read(file);
                Check(read is { Balloon: { Flights: 0, Stars: 0 } }, "a save from before the balloon station loads");
                game.RestoreProgress(read!);
                Check(
                    game.BalloonFlights == 0 && game.BalloonStars == 0 && !game.BalloonFlying,
                    "and no one has flown"
                );
            }

            // ---- The first flight ----
            int coins = game.Coins;
            Near(balloonist.Position);
            game.OpenBalloonist(balloonist);
            Check(game.State == GameState.BalloonStatus && Has("StartBalloonFlight"), "the balloonist offers a flight");
            RoundTrip("the balloonist's panel");
            game.BeginBalloonFlight();
            Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "a question heats the air first");
            RoundTrip("the question before a flight");
            Check(!game.BalloonFlying, "the balloon waits for the answer");
            ChallengeChecks.Solve(game);
            Check(game.BalloonFlying && balloon.Flying && !station.Tethered, "the right answer lets the balloon go");
            Check(game.State == GameState.Exploring && !car.Active, "the car waits on the road");
            Check(
                game.Coins == coins + BalloonPlans.FirstFlightCoins && game.BalloonFlights == 1,
                "the first flight pays"
            );
            var ride = game.BalloonRide!;
            Frames(80);
            float rise =
                balloon.GlobalPosition.Y - world.Ground.Height(balloon.GlobalPosition.X, balloon.GlobalPosition.Z);
            Check(ride.Distance > 20 && rise > 4, $"the wind carries it off the field, {rise:0} m above the ground");
            // The burner makes it climb, the valve sink, before the wind reaches the first star.
            ride.Place(10, 20);
            Input.ActionPress("accelerate");
            Frames(30);
            Input.ActionRelease("accelerate");
            float climbed = ride.Height;
            Check(climbed > 25 && balloon.Flaming, $"the burner lifts it to {climbed:0} m");
            Input.ActionPress("reverse");
            Frames(50);
            Input.ActionRelease("reverse");
            Check(ride.Height < climbed - 6, $"the valve lets it sink to {ride.Height:0} m");
            Check(ride.Distance < way.StarDistance(0) - BalloonRide.CatchAlong, "still before the first star");
            // A star the basket reaches is caught; one it passes above or below fades.
            ride.Place(way.StarDistance(0) - 3, ride.StarHeight(0));
            int before = game.Coins;
            Frames(2);
            Check(ride.IsCaught(0) && ride.Caught == 1 && game.BalloonStars == 1, "the basket catches the first star");
            Check(game.Coins == before + BalloonPlans.CoinsPerStar, "a star pays a coin");
            ride.Place(way.StarDistance(1) - 3, ride.StarHeight(1) > 30 ? 4 : 52);
            Frames(2);
            Check(!ride.IsCaught(1), "a star far above or below is not caught");
            ride.Place(way.StarDistance(1) + BalloonRide.CatchAlong + 4, ride.Height);
            Frames(15);
            Check(!stars!.Shows(1) && !stars.Shows(0), "the caught star bursts and the missed one fades");
            Check(stars.Shows(2), "the next star still hangs ahead");
            RoundTrip("a save during a flight");
            Check(
                !game.BalloonFlying && !balloon.Flying && station.Tethered,
                "a load puts the balloon back on its field"
            );
            Check(
                Flat(balloon.GlobalPosition, pad) < .01f && Mathf.Abs(station.Lift - BalloonStation.Hover) < .01f,
                "on its ropes, where it floated before"
            );
            Check(game.BalloonStars == 1 && game.BalloonFlights == 1, "the star caught before the load is kept");
            Check(car.Active, "and the car drives on");

            // ---- The talk button lands it early ----
            Near(balloonist.Position);
            game.OpenBalloonist(balloonist);
            game.BeginBalloonFlight();
            ChallengeChecks.Solve(game);
            Check(
                game.BalloonFlying && game.Coins == before + BalloonPlans.CoinsPerStar,
                "a second flight pays no more"
            );
            Check(game.BalloonRide!.StarHeight(0) != BalloonFlight.StarHeight(0, 0), "its stars hang at other heights");
            game.Interact();
            Check(game.BalloonFlying, "the press that started it does not end it");
            Frames(20);
            game.Interact();
            Check(!game.BalloonFlying && station.Tethered, "the talk button lands it at once");

            // ---- The rooster and every star ----
            Load(data =>
            {
                data.Balloon.Flights = 4;
                data.Balloon.Stars = BalloonPlans.RoosterStars - 1;
            });
            Check(
                game.BalloonStars == BalloonPlans.RoosterStars - 1 && !rooster!.IsOwned,
                "nineteen stars, no rooster yet"
            );
            Near(balloonist.Position);
            game.OpenBalloonist(balloonist);
            game.BeginBalloonFlight();
            ChallengeChecks.Solve(game);
            ride = game.BalloonRide!;
            int startCoins = game.Coins;
            for (int star = 0; star < BalloonFlight.Stars; star++)
            {
                ride.Place(way.StarDistance(star) - 2, ride.StarHeight(star));
                Frames(1);
                Check(ride.IsCaught(star), $"star {star + 1} is caught");
                if (star == 0)
                    Check(rooster!.IsOwned, "the twentieth star brings the rooster");
            }
            RoundTrip("the rooster in a save");
            Check(rooster!.IsOwned, "the rooster stays after a load");
            game.OpenBalloonist(balloonist);
            game.BeginBalloonFlight();
            ChallengeChecks.Solve(game);
            ride = game.BalloonRide!;
            startCoins = game.Coins;
            for (int star = 0; star < BalloonFlight.Stars; star++)
            {
                ride.Place(way.StarDistance(star) - 2, ride.StarHeight(star));
                Frames(1);
            }
            Check(ride.Caught == BalloonFlight.Stars, "a flight catches every star");
            ride.Place(way.Length - 60, 30);
            Frames(150);
            Check(!game.BalloonFlying && station.Tethered, "the balloon comes down onto its field by itself");
            Check(Flat(balloon.GlobalPosition, pad) < .01f, "and lands on the target");
            Check(station.Lift < BalloonStation.Hover, "it stands on the field for a while");
            Check(
                game.Coins == startCoins + BalloonFlight.Stars * BalloonPlans.CoinsPerStar + BalloonPlans.AllStarsCoins,
                "every star pays, and so does the landing with all of them"
            );
            Check(game.BalloonFlights == 6, "six flights are counted");
            Frames(120);
            Check(
                Mathf.Abs(station.Lift - BalloonStation.Hover) < .01f
                    && Mathf.Abs(balloon.GlobalPosition.Y - station.BalloonAnchor.Y) < .01f,
                "then it rises on its ropes above the trees again"
            );
            RoundTrip("after the flights");
            GD.Print("BALLOON_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
