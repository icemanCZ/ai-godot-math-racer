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
/// The hunter at the edge of the village: that every valley has him and his pen on dry, empty ground off the roads, the
/// hut that four deliveries of goods raise before he sends anyone hunting, the hunt itself (the animals in a meadow far
/// away, the net thrown from the car that wraps an animal, a missed net picked up from the grass, a net lost in the river
/// and the new one the hunter gives), the animal loaded on the car and driven into the pen, the fawn after eight animals,
/// and the saves.
/// </summary>
public partial class HuntChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public HuntChecks(Game game, World world, Vehicle car)
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

    /// <summary>The heading under which the car looks along <paramref name="direction"/> (its forward is -Z).</summary>
    private static float Facing(Vector3 direction) => Mathf.Atan2(-direction.X, -direction.Z);

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
            string path = DiagnosticArtifacts.PathFor("hunt-checks/progress.json");
            var trailer = game.Trailer;
            var field = world.HuntGrounds;
            var hunt = game.Hunt;
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save accepted: {what}");
                game.RestoreProgress(loaded!);
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"hunt progress restores: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"hunt-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- The hunter, his pen and the ground they stand on ----
            var hunter = world.Hunter;
            var pen = world.Pen;
            var site = world.HunterPlace;
            Check(hunter != null && pen != null && site != null, "every valley has the hunter and his pen");
            Check(
                hunter!.Role is HunterRole
                    && world.ActorId(hunter) == World.HunterId
                    && world.ActorAt(World.HunterId) == hunter
                    && ActorIds.Hunter == 73
                    && pen!.Hunter == hunter,
                "the hunter is registered under the next free actor ID"
            );
            Check(hunter.DisplayName == CharacterCatalog.Hunter.Name, "the hunter has his name");
            GD.Print($"HUNTER_SITE village={world.Village.PlotDistance(site!.Gate):0}");
            int wet = 0,
                onRoad = 0,
                trees = 0,
                elsewhere = 0,
                total = 0;
            for (float along = 0; along <= HunterSite.Depth; along += 2)
            for (float across = -HunterSite.HalfWidth; across <= HunterSite.HalfWidth; across += 2)
            {
                var p = site.Local(across, along);
                p = world.Ground.At(p.X, p.Z);
                total++;
                if (River.Distance(p.X, p.Z) < River.HalfWidth(p.Z) + 1)
                    wet++;
                if (world.Routes.Distance(p) < 4)
                    onRoad++;
                if (world.Plants.NearTrunk(p, 1.0f))
                    trees++;
                if (world.InPlaygroundZone(p) || world.InZooZone(p) || world.Village.IsReserved(p))
                    elsewhere++;
                if (!world.InHunterZone(p))
                    elsewhere++;
            }
            Check(total > 100 && wet == 0, "no water inside the pen");
            Check(onRoad == 0, "no road crosses the pen");
            Check(trees == 0, "no tree stands in the pen");
            Check(elsewhere == 0, "the pen lies in its own cleared ground, clear of the village and the other sites");
            Check(
                world.Hidden.Friends.All(friend => !world.InHunterZone(friend.GlobalPosition)),
                "no small friend hides in the pen"
            );
            Check(
                world.InteractionActors.All(actor => actor == hunter || !world.InHunterZone(actor.GlobalPosition)),
                "no other character stands at the pen"
            );
            Check(pen!.AnimalCount == 0 && hunt.Caught == 0 && !hunt.Active, "the pen starts empty");
            var fawn = world.WorldPets.SingleOrDefault(p => p.Kind == CollectorPetKind.Fawn);
            Check(fawn != null && !fawn.IsOwned && fawn.Collector == hunter, "the fawn waits beside the hunter");

            // ---- Saves, on the fresh valley ----
            string json = JsonSerializer.Serialize(game.CaptureProgress());
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
            node.Remove("Hunt");
            string oldFile = DiagnosticArtifacts.PathFor("hunt-checks/old.json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(oldFile)!);
            System.IO.File.WriteAllText(oldFile, node.ToJsonString());
            var old = SaveStore.Read(oldFile);
            Check(
                old is { Hunt: { Hut: 0, Active: false, Carrying: "", Penned.Length: 0 } },
                "a file written before the hunter loads with the hut staked out and an empty pen"
            );
            game.RestoreProgress(old!);
            Check(pen.Stage == 0 && pen.AnimalCount == 0 && !hunt.Active, "the old save restores the empty site");
            Rejected(data => data.Hunt.Hut = HutPlans.Trips + 1, "a hut with five deliveries");
            Rejected(data => data.Hunt.Hut = -1, "a hut with a negative stage");
            Rejected(data => data.Hunt.Active = true, "a hunt before the hut stands");
            Rejected(data => data.Hunt.Penned = new[] { "Rabbit" }, "an animal in the pen before the hut stands");
            Rejected(
                data =>
                {
                    data.Hunt.Hut = HutPlans.Trips;
                    data.Hunt.Penned = new[] { "Dragon" };
                },
                "an unknown animal in the pen"
            );
            Rejected(
                data =>
                {
                    data.Hunt.Hut = HutPlans.Trips;
                    data.Hunt.Penned = new[] { "rabbit" };
                },
                "an animal's name in the wrong case"
            );
            Rejected(
                data =>
                {
                    data.Hunt.Hut = HutPlans.Trips;
                    data.Hunt.Carrying = "Fox";
                },
                "an animal on the car without a hunt"
            );
            Rejected(
                data =>
                {
                    data.Hunt.Hut = HutPlans.Trips;
                    data.Hunt.Penned = Enumerable.Repeat("Deer", AnimalHunt.Limit + 1).ToArray();
                },
                "more animals than a save may claim"
            );
            Rejected(data => data.Hunt = null!, "no hunt section");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Hunter, Index = 1 },
                "a trip for a second hut"
            );
            Rejected(
                data =>
                {
                    data.Hunt.Hut = HutPlans.Trips;
                    data.Task.Trip = new SavedTrip { Kind = TripKinds.Hunter, Index = 0 };
                },
                "a trip to a hut that has no delivery left"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.HunterStatus;
                    data.Nearby = World.FarmerId;
                },
                "the hunter's panel at the farmer"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.ZooStatus;
                    data.Nearby = World.HunterId;
                },
                "the zoo panel at the hunter"
            );
            Rejected(data => data.Nearby = World.HunterId + 1, "a character beyond the hunter");
            var finished = game.CaptureProgress();
            finished.Hunt.Hut = HutPlans.Trips;
            finished.Hunt.Penned = new[] { "Fox" };
            string finishedFile = DiagnosticArtifacts.PathFor("hunt-checks/finished.json");
            Check(
                SaveStore.Write(finished, finishedFile) && SaveStore.Read(finishedFile) != null,
                "a finished hut with a fox in the pen is a valid save"
            );

            // ---- The hut comes first ----
            Check(HutPlans.Trips == 4 && HutPlans.Parts.Length == HutPlans.Trips, "four deliveries raise the hut");
            for (int i = 0; i < HutPlans.Trips; i++)
                Check(HaulLoads.IsGoods(HutPlans.CargoOf(i)), $"delivery {i + 1} of the hut is goods from a works");
            Check(world.Industry.Length > 0, "this valley has the works the hut is built from");
            Check(pen.Stage == 0 && !pen.Complete, "the hut is only staked out at first");
            void Near(Character who) => car.SetPose(world.Ground.At(who.Position.X + 2, who.Position.Z, .15f), 0);
            foreach (var works in world.Industry)
                works.SetStock(Works.Capacity);
            Near(hunter);
            game.OpenHunter(hunter);
            Check(
                game.State == GameState.HunterStatus && Has("StartHutTrip") && !Has("StartHunt"),
                "before the hut stands the hunter asks for material, not for a hunt"
            );
            game.BeginHunt();
            Check(!hunt.Active && field.HerdCount == 0, "no hunt begins before the hut stands");
            game.FinishInteraction();

            // A panel left open at the hunter and the quiz before a hut trip come back as they were.
            Near(hunter);
            game.OpenHunter(hunter);
            RoundTrip("the hunter's panel before the hut");
            Check(game.State == GameState.HunterStatus && Has("StartHutTrip"), "the panel comes back after loading");
            game.BeginHutTrip();
            Check(game.State == GameState.Challenge, "the hut trip asks its quiz first");
            RoundTrip("the quiz before a hut trip");
            ChallengeChecks.Solve(game);
            Check(game.Haul is { HunterHut: { } restored } && restored == pen, "the quiz before the trip comes back");
            var noTrip = game.CaptureProgress();
            noTrip.Task.Trip = null;
            game.RestoreProgress(noTrip);
            Check(game.Haul == null, "a save without the hut trip ends it");

            int partsBefore = pen.GetNode<Node3D>("Hut").GetChildCount();
            int coinsBefore = game.Coins;
            void DeliverHut(bool save = true)
            {
                int step = pen.Stage;
                var cargo = HutPlans.CargoOf(step);
                foreach (var works in world.Industry)
                    works.SetStock(Works.Capacity);
                Near(hunter);
                game.OpenHunter(hunter);
                Check(Has("StartHutTrip"), $"the hunter offers delivery {step + 1} of the hut");
                game.BeginHutTrip();
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.HunterHut == pen && trip.Step == step && trailer.Attached,
                    $"delivery {step + 1} sends the empty trailer"
                );
                Check(
                    trip!.Load == cargo
                        && trip.Contractor == hunter
                        && trip.Asker == world.PickupActor(cargo, trip.Source),
                    "the works' worker loads it for the hunter"
                );
                if (save)
                    RoundTrip($"trip to the works for the hut ({step + 1})");
                var source = world.SourceFor(cargo);
                car.SetPose(world.Ground.At(source.Position.X, source.Position.Z, .15f), 0);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the works asks a question");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (save)
                    RoundTrip($"full trailer for the hut ({step + 1})");
                car.SetPose(world.Ground.At(site.Drop.X, site.Drop.Z, .15f), 0);
                Frames(1);
                Check(trailer.Dumping, "the bed tips at the gate");
                Frames(40);
                Check(pen.Stage == step + 1 && trailer.Load == null, $"delivery {step + 1} raises the hut");
                int parts = pen.GetNode<Node3D>("Hut").GetChildCount();
                Check(parts > partsBefore, $"more of the hut stands after delivery {step + 1}");
                partsBefore = parts;
                Check(game.Haul == null && !game.TaskActive, "the next delivery waits for the player");
            }

            DeliverHut();
            Check(game.Coins == coinsBefore, "no coins before the hut is finished");
            RoundTrip("a hut with its foundation");

            // Without the goods the hunter shows what the works needs and sends the player for the raw material.
            foreach (var works in world.Industry)
                works.SetStock(0);
            Near(hunter);
            game.OpenHunter(hunter);
            Check(
                !Has("StartHutTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                "the hunter shows what is missing when there are no planks"
            );
            game.BeginHutTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "no delivery starts without its goods");
            var needed = world.Industry[(int)HaulLoads.ProductOf(HutPlans.CargoOf(pen.Stage))];
            game.BeginMissingSupply();
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Loaded: false } fetch && fetch.Works == needed,
                "the hunter sends the empty trailer for the raw material"
            );
            var withoutTrip = game.CaptureProgress();
            withoutTrip.Task.Trip = null;
            game.RestoreProgress(withoutTrip);
            Check(game.Haul == null, "a save without the trip ends it");

            DeliverHut();
            DeliverHut(save: false);
            Check(!pen.Complete && pen.Stage == 3, "three deliveries leave the hut without its chimney");
            DeliverHut();
            Frames(5);
            Check(pen.Complete && pen.Stage == HutPlans.Trips, "four deliveries finish the hut");
            Check(game.Coins == coinsBefore + HutPlans.Coins, "the finished hut paid its coins");
            RoundTrip("a finished hut");
            Near(hunter);
            game.OpenHunter(hunter);
            Check(Has("StartHunt") && !Has("StartHutTrip"), "the finished hut lets the hunter send the player hunting");
            game.BeginHutTrip();
            ChallengeChecks.Solve(game);
            Check(game.Haul == null, "a finished hut asks for no more material");
            game.FinishInteraction();

            // ---- A hunt: the meadow, a caught animal, the way back to the pen ----
            void Begin()
            {
                Near(hunter);
                game.OpenHunter(hunter);
                Check(game.State == GameState.HunterStatus && Has("StartHunt"), "the hunter offers a hunt");
                game.BeginHunt();
                Check(
                    hunt.Active
                        && game.State == GameState.Exploring
                        && field.Net == NetState.InHand
                        && field.HerdCount == HuntField.HerdSize
                        && field.FreeCount == HuntField.HerdSize,
                    "the hunt begins with the net on the car and three animals running free"
                );
            }
            // Puts every animal but the first far aside and the car <paramref name="distance"/> metres from it, facing it.
            void Aim(float distance)
            {
                var target = field.PositionOf(0);
                for (int i = 1; i < field.HerdCount; i++)
                    if (!field.IsWrapped(i))
                        field.Place(i, world.Ground.At(target.X + 40 + i * 6, target.Z + 40));
                field.Place(0, target);
                var direction = new Vector3(1, 0, .3f).Normalized();
                var from = world.Ground.At(target.X - direction.X * distance, target.Z - direction.Z * distance, .15f);
                car.SetPose(from, Facing(direction));
            }
            WildKind CatchFirst()
            {
                Aim(14);
                Check(game.NetThrowAvailable, "the net can be thrown");
                Check(game.ThrowNet() && field.Net == NetState.Flying, "the space bar throws the net");
                Check(!game.NetThrowAvailable, "a flying net cannot be thrown again");
                Frames(12);
                Check(field.Net == NetState.Wrapped && field.WrappedKind != null, "the net comes down on the animal");
                Check(field.FreeCount == HuntField.HerdSize - 1, "the caught animal no longer runs");
                var kind = field.WrappedKind!.Value;
                var spot = field.NetSpot!.Value;
                car.SetPose(world.Ground.At(spot.X + 2, spot.Z, .15f), 0);
                Check(game.CatchLoadAvailable, "the talk button offers to load the animal");
                Check(game.LoadCatch(), "the animal is loaded");
                Check(
                    hunt.Carrying == kind
                        && field.CarriedKind == kind
                        && field.Net == NetState.Wrapped
                        && field.NetSpot == null,
                    "the animal rides on the car in its net"
                );
                return kind;
            }
            void DeliverAnimal(WildKind kind)
            {
                int caught = hunt.Caught;
                car.SetPose(world.Ground.At(site.Drop.X, site.Drop.Z, .15f), 0);
                Frames(1);
                Check(
                    hunt.Caught == caught + 1
                        && hunt.Penned[^1] == kind
                        && !hunt.Active
                        && hunt.Carrying == null
                        && field.CarriedKind == null
                        && field.HerdCount == 0,
                    $"driving to the gate takes the {kind} into the pen and ends the hunt"
                );
                Check(pen.AnimalCount == Math.Min(hunt.Caught, HunterPen.Shown), "the animal lives in the pen");
            }

            Begin();
            Check(
                Flat(field.Meadow, site.Gate) > 80,
                $"the animals run far from the pen ({Flat(field.Meadow, site.Gate):0} m)"
            );
            Check(
                Enumerable.Range(0, field.HerdCount).All(i => world.IsMeadow(field.PositionOf(i), 2)),
                "the animals stand on open meadow"
            );
            Check(
                field.NearestFree(car.Position) is { } nearest && AnimalHunt.IsPrey(nearest.Kind),
                "the corner of the screen points to the nearest animal"
            );
            RoundTrip("a hunt under way");
            Check(hunt.Active && field.HerdCount == HuntField.HerdSize, "a hunt under way comes back with its animals");

            // The animals run from a car that comes close.
            var still = field.PositionOf(0);
            for (int i = 1; i < field.HerdCount; i++)
                field.Place(i, world.Ground.At(still.X + 40 + i * 6, still.Z + 40));
            field.Place(0, still);
            var towards = new Vector3(0, 0, 1);
            car.SetPose(world.Ground.At(still.X, still.Z - 6, .15f), Facing(towards));
            Frames(6);
            Check(Flat(still, field.PositionOf(0)) > 1.5f, "an animal runs from the car that comes close");

            // A miss: the net lies in the grass until the car comes back for it.
            var meadow = field.PositionOf(0);
            for (int i = 0; i < field.HerdCount; i++)
                field.Place(i, world.Ground.At(meadow.X + i * 2, meadow.Z));
            Vector3? throwFrom = null;
            float throwHeading = 0;
            for (int turn = 0; turn < 16 && throwFrom == null; turn++)
            {
                var away = new Vector3(Mathf.Sin(turn * Mathf.Tau / 16), 0, Mathf.Cos(turn * Mathf.Tau / 16));
                var from = meadow + away * 45;
                var landing = from + away * HuntField.BlindThrow;
                if (
                    River.Distance(landing.X, landing.Z) < River.HalfWidth(landing.Z) + 4
                    || FlatMath.Length(landing.X, landing.Z) > Settings.PlayableRadius - 10
                    || River.Distance(from.X, from.Z) < River.HalfWidth(from.Z) + 2
                )
                    continue;
                throwFrom = world.Ground.At(from.X, from.Z, .15f);
                throwHeading = Facing(away);
            }
            Check(throwFrom != null, "a dry place to throw from was found");
            void ThrowAside()
            {
                car.SetPose(throwFrom!.Value, throwHeading);
                Check(game.ThrowNet(), "the net is thrown where no animal is");
                Frames(12);
                Check(field.Net == NetState.Lying && field.NetSpot != null, "a missed net lies in the grass");
            }
            ThrowAside();
            Check(!game.NetThrowAvailable, "the net in the grass cannot be thrown");
            var lying = field.NetSpot!.Value;
            Check(Flat(lying, car.Position) > 8, "the net flew ahead of the car");
            Check(!game.NetPickupAvailable, "the net is too far to pick up from where it was thrown");
            car.SetPose(world.Ground.At(lying.X + 2, lying.Z, .15f), 0);
            Check(game.NetPickupAvailable && game.PickUpNet(), "driving to the net picks it up");
            Check(field.Net == NetState.InHand && game.NetThrowAvailable, "the net is back on the car");

            // A net left lying is replaced by the hunter; the hunt goes on.
            ThrowAside();
            Near(hunter);
            game.OpenHunter(hunter);
            Check(
                field.Net == NetState.InHand && game.State == GameState.HunterStatus && !Has("StartHunt"),
                "the hunter gives a new net for one left lying and the hunt goes on"
            );
            game.FinishInteraction();

            // The river takes the net, and the hunter gives a new one.
            bool atRiver = false;
            for (float z = -400; z <= 400 && !atRiver; z += 7)
            {
                float x = River.CenterX(z);
                if (world.OnCrossing(x, z) || world.OnCrossing(x + 2, z))
                    continue;
                var from = new Vector3(x + HuntField.BlindThrow + 2, 0, z);
                if (
                    River.Distance(from.X, from.Z) < River.HalfWidth(z) + 3
                    || FlatMath.Length(from.X, from.Z) > Settings.PlayableRadius - 20
                    || Enumerable.Range(0, field.HerdCount).Any(i => Flat(field.PositionOf(i), from) < 60)
                )
                    continue;
                car.SetPose(world.Ground.At(from.X, from.Z, .15f), Facing(Vector3.Left));
                atRiver = true;
            }
            Check(atRiver, "a bank to throw from was found");
            Check(game.ThrowNet(), "the net is thrown towards the river");
            Frames(12);
            Check(field.Net == NetState.Lost && field.NetSpot == null, "a net that falls into the river is lost");
            Check(
                !game.NetThrowAvailable && !game.NetPickupAvailable,
                "a lost net can be neither thrown nor picked up"
            );
            Near(hunter);
            game.OpenHunter(hunter);
            Check(field.Net == NetState.InHand && hunt.Active, "the hunter gives a new net for the lost one");
            game.FinishInteraction();
            ThrowAside();
            RoundTrip("a net in the grass");
            Check(
                field.Net == NetState.InHand && hunt.Active && field.HerdCount == HuntField.HerdSize,
                "after a load the hunt goes on with the net on the car"
            );

            var first = CatchFirst();
            RoundTrip("an animal on the car");
            Check(hunt.Carrying == first && field.CarriedKind == first, "the animal on the car comes back");
            Check(!game.NetThrowAvailable, "no net is thrown while an animal rides on the car");
            Near(hunter);
            game.OpenHunter(hunter);
            Check(hunt.Caught == 1 && !hunt.Active, "talking to the hunter hands the animal over too");
            Check(pen.AnimalCount == 1, "the animal lives in the pen");
            game.FinishInteraction();
            RoundTrip("one animal in the pen");
            Check(pen.AnimalCount == 1 && hunt.Penned[0] == first, "the pen comes back with its animal");

            // The animals of the pen stroll about inside the fence.
            Frames(1);
            var strolling = pen.AnimalKinds.ToArray();
            Check(strolling.Length == 1 && strolling[0] == first, "the pen shows the animal that was brought");

            // ---- Eight animals bring the fawn ----
            while (hunt.Caught < AnimalHunt.PetGoal)
            {
                Check(!fawn!.IsOwned, $"no fawn with {hunt.Caught} animals in the pen");
                Begin();
                var kind = CatchFirst();
                DeliverAnimal(kind);
            }
            Check(fawn!.IsOwned && fawn.IsActive, "eight animals in the pen bring the fawn as a pet");
            Check(fawn.DisplayName == WorldPetCatalog.Fawn.Name, "the fawn has its name");
            Check(
                AnimalHunt.Prey.All(kind => hunt.Penned.Contains(kind)),
                "rabbits, foxes and deer have all been caught"
            );
            RoundTrip("eight animals and the fawn");
            Check(fawn.IsOwned && pen.AnimalCount == AnimalHunt.PetGoal, "the fawn and the pen come back");
            Near(hunter);
            game.OpenHunter(hunter);
            Check(Has("StartHunt"), "the hunter still sends the player hunting after the fawn");
            game.FinishInteraction();

            GD.Print("HUNT_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }
}
