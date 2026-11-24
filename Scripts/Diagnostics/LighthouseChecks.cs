using System;
using System.Linq;
using System.Text.Json;
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
/// The lighthouse on the shore (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phases 3 and 4): it is wanted only after the first
/// letter to the keeper of the sea; then eight deliveries raise it, the five of stone loaded at the coast quarry where the
/// keeper asks the loading question, the planks, bricks and glasses fetched from the works; goods that are not made send
/// the player for the raw material; every step survives a save; the finished lighthouse pays its coins, brings the gull and
/// by night turns its beam while the boat sails past; and the damaged saves are turned down.
/// </summary>
public partial class LighthouseChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public LighthouseChecks(Game game, World world, Vehicle car)
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
            string path = DiagnosticArtifacts.PathFor("lighthouse-checks/progress.json");
            var trailer = game.Trailer;
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                foreach (var rule in SaveRules.All.Where(rule => rule.Invalid(saved)))
                    GD.Print($"BROKEN RULE: {rule.Name}");
                Check(SaveRules.All.All(rule => !rule.Invalid(saved)), $"save accepted: {what}");
                var loaded = SaveStore.Read(path);
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
                Check(before == after, $"the lighthouse restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"lighthouse-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Near(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            var lighthouse = world.Lighthouse;
            var keeper = world.LighthouseKeeper;
            var quarry = world.QuarryPlace;
            Check(
                lighthouse != null && keeper != null && quarry != null,
                "the lighthouse, its keeper and the coast quarry"
            );
            Check(world.LighthouseBuildable && lighthouse!.Stage == 0, "the lighthouse is only staked out at first");
            Check(lighthouse.Keeper == keeper && lighthouse.Quarry == quarry, "the keeper of the sea raises it");
            var gull = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Gull);
            Check(gull != null && !gull.IsOwned && gull.Collector == keeper, "the gull waits by the keeper of the sea");
            // The way from the quarry to the lighthouse, by road, with the loaded trailer.
            var arrows = new Node3D { Name = "LighthouseArrows" };
            world.AddChild(arrows);
            var toward = lighthouse.Site.Road - quarry!.Road;
            car.SetPose(world.Ground.At(quarry.Road.X, quarry.Road.Z, .15f), Mathf.Atan2(-toward.X, -toward.Z));
            var guide = new RouteGuide(world, car, arrows, () => lighthouse.Site.Drop);
            guide.Retarget(lighthouse.Site.Road);
            guide.CreateArrows();
            float length = 0;
            for (int i = 1; i < guide.RoutePoints.Count; i++)
                length += Flat(guide.RoutePoints[i - 1], guide.RoutePoints[i]);
            Check(
                length > 0 && length < 1200,
                $"the stone is carried {length:0} m from the coast quarry to the lighthouse"
            );
            arrows.QueueFree();

            // ---- Before the first letter ----
            Near(keeper!.Position);
            game.OpenBorderKeeper(keeper);
            Check(game.State == GameState.KeeperStatus, "before the first letter the keeper only talks about the sea");
            game.FinishInteraction();
            Rejected(data => data.Lighthouse.Stage = 1, "a lighthouse begun before the first letter");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Lighthouse, Index = 0 },
                "a lighthouse trip before the first letter"
            );
            Rejected(data => data.Lighthouse = null!, "no lighthouse section");
            var letter = game.CaptureProgress();
            letter.Post.Delivered[(int)Border.Sea] = 1;
            game.RestoreProgress(letter);
            Rejected(data => data.Lighthouse.Stage = LighthousePlans.Trips + 1, "a lighthouse with nine deliveries");
            Rejected(data => data.Lighthouse.Stage = -1, "a lighthouse with a negative stage");
            Rejected(
                data =>
                {
                    data.State = GameState.LighthouseStatus;
                    data.Nearby = World.HunterId;
                },
                "the lighthouse panel at the hunter"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Lighthouse, Index = 1 },
                "a trip for a second lighthouse"
            );

            // ---- Eight deliveries ----
            Check(
                LighthousePlans.Trips == 8 && LighthousePlans.Parts.Length == LighthousePlans.Trips,
                "eight deliveries raise the lighthouse"
            );
            int coinsBefore = game.Coins;
            for (int step = 0; step < LighthousePlans.Trips; step++)
            {
                bool save = step is 0 or 5;
                var cargo = LighthousePlans.CargoOf(step);
                foreach (var works in world.AllWorks)
                    works.SetStock(Works.Capacity);
                Near(keeper.Position);
                game.OpenBorderKeeper(keeper);
                Check(
                    game.State == GameState.LighthouseStatus && Has("StartLighthouseTrip"),
                    $"the keeper offers delivery {step + 1} of the lighthouse"
                );
                if (save)
                    RoundTrip("the lighthouse panel");
                game.BeginLighthouseTrip();
                Check(game.State == GameState.Challenge, "the trip asks its quiz first");
                if (save)
                    RoundTrip("the quiz before a lighthouse trip");
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Lighthouse == lighthouse && trip.Step == step && trailer.Attached,
                    $"delivery {step + 1} sends the empty trailer"
                );
                bool stone = step < LighthousePlans.StoneTrips;
                Check(
                    trip!.Load == cargo
                        && (
                            stone
                                ? trip.Source.Position == quarry.Position && trip.Asker == keeper
                                : trip.Asker == world.PickupActor(cargo, trip.Source)
                        ),
                    stone ? "the stone is loaded at the coast quarry" : "the works' worker loads the goods"
                );
                if (save)
                    RoundTrip($"a trip for the lighthouse ({step + 1})");
                var source = game.Haul!.Source;
                // On the second stone trip a letter for the keeper of the sea is carried too: the question at the quarry
                // still loads the stone, and the letter waits for the keeper at home.
                bool letterAboard = step == 1;
                if (letterAboard)
                {
                    var carrying = game.CaptureProgress();
                    carrying.Post.Carrying = (int)Border.Sea;
                    game.RestoreProgress(carrying);
                }
                Near(source.Position);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the loading question");
                if (stone && save)
                    RoundTrip("the keeper's loading question at the coast quarry");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (letterAboard)
                {
                    Check(
                        game.Post.Carrying == (int)Border.Sea && game.Post.Delivered[(int)Border.Sea] == 1,
                        "a letter carried for the keeper of the sea stays aboard while the stone is loaded"
                    );
                    RoundTrip("a letter and a full trailer of stone");
                    var noLetter = game.CaptureProgress();
                    noLetter.Post.Carrying = -1;
                    game.RestoreProgress(noLetter);
                }
                if (save)
                    RoundTrip($"a full trailer for the lighthouse ({step + 1})");
                Near(lighthouse.Site.Drop);
                Frames(1);
                Check(trailer.Dumping, "the bed tips by the keeper's gate");
                Frames(40);
                Check(lighthouse.Stage == step + 1, $"delivery {step + 1} raises the {LighthousePlans.Parts[step]}");
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
                if (step == 5)
                {
                    // Without the goods the keeper shows what the works needs and sends the player for the raw material.
                    foreach (var works in world.AllWorks)
                        works.SetStock(0);
                    Near(keeper.Position);
                    game.OpenBorderKeeper(keeper);
                    Check(
                        !Has("StartLighthouseTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                        "the keeper shows what is missing when there are no bricks"
                    );
                    game.BeginMissingSupply();
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Loaded: false, Works: not null },
                        "the keeper sends the trailer for the raw material"
                    );
                    var noTrip = game.CaptureProgress();
                    noTrip.Task.Trip = null;
                    game.RestoreProgress(noTrip);
                }
                if (step < LighthousePlans.Trips - 1)
                    Check(!gull!.IsOwned && game.Coins == coinsBefore, "no coins and no gull before the lantern is up");
            }
            Check(lighthouse.Complete, "the eighth delivery finishes the lighthouse");
            Check(game.Coins == coinsBefore + LighthousePlans.Coins, "the finished lighthouse pays its coins");
            Check(gull!.IsOwned, "the gull comes with the finished lighthouse");
            RoundTrip("a finished lighthouse");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Lighthouse, Index = 0 },
                "a trip to a lighthouse that has no delivery left"
            );

            // ---- By night ----
            game.Weather.SetTime(DayTime.Day, instant: true);
            Near(keeper.Position + Vector3.Back * 30);
            Frames(5);
            Check(!lighthouse.Shining && !world.NightBoat!.Visible, "by day the lantern rests and the boat is away");
            game.Weather.SetTime(DayTime.Night, instant: true);
            Frames(5);
            Check(lighthouse.Shining && world.NightBoat!.Visible, "by night the beam turns and the boat sails past");
            GD.Print("LIGHTHOUSE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
