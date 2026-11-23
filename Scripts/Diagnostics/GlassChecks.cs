using System;
using System.Linq;
using System.Text.Json;
using Godot;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Saves;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Diagnostics;

/// <summary>
/// The glass lamp maker on the village square and his lamps: that every valley has him under the next actor ID, that he
/// has two lamps (a candle and a lamp) that cost nothing, that a lamp goes into the bag, is put down on dry ground beside
/// the car, can be taken up again and shines only at night, that he takes glasses from the glassworks again and again
/// (and sends the player for sand when there are none), and that the bag, the standing lamps and the glasses trip are
/// saved, validated and absent from a save that never had them.
/// </summary>
public partial class GlassChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int invalidFiles;

    public GlassChecks(Game game, World world, Vehicle car)
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

    public override async void _Ready()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string path = DiagnosticArtifacts.PathFor("glass-checks/progress.json");
            void RoundTrip(string what)
            {
                var saved = game.CaptureProgress();
                Check(SaveStore.Write(saved, path), "save written");
                var loaded = SaveStore.Read(path);
                Check(loaded != null, $"save accepted: {what}");
                game.RestoreProgress(loaded!);
                Check(
                    JsonSerializer.Serialize(saved) == JsonSerializer.Serialize(game.CaptureProgress()),
                    $"lamps restore: {what}"
                );
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"glass-checks/invalid-{invalidFiles++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }

            // ---- The lamp maker ----
            var village = world.Village;
            var maker = village.Glassmaker;
            var lamps = village.Lamps;
            Check(
                maker.Role is GlassmakerRole
                    && world.ActorId(maker) == World.GlassmakerId
                    && world.ActorAt(World.GlassmakerId) == maker
                    && World.IsGlassmaker(World.GlassmakerId)
                    && ActorIds.Glassmaker == 69,
                "the lamp maker is registered under the next free actor ID"
            );
            Check(maker.DisplayName == CharacterCatalog.Glassmaker.Name, "the lamp maker has his name");
            var local = village.Square.Transform.AffineInverse() * maker.Position;
            Check(Mathf.Abs(local.X) < 11 && Mathf.Abs(local.Z) < 6.5f, "the lamp maker stands on the village square");
            var hit = world
                .GetWorld3D()
                .DirectSpaceState.IntersectRay(
                    PhysicsRayQueryParameters3D.Create(
                        maker.Position + Vector3.Up * .25f,
                        maker.Position + Vector3.Down * .25f
                    )
                );
            Check(
                hit.Count > 0 && Mathf.Abs(hit["position"].AsVector3().Y - maker.Position.Y) < .1f,
                "the lamp maker stands on the paving"
            );
            Check(village.IsReserved(maker.Position), "the square, where the lamp maker stands, is reserved ground");
            Check(
                maker.Position.DistanceTo(village.Mayor.Position) > Settings.InteractionRange,
                "the lamp maker stands apart from the mayor, so neither is taken for the other"
            );
            Check(village.Mayor.Position.DistanceTo(maker.Position) > 2.2f, "no one stands on anyone");

            // ---- The lamps ----
            var all = GlassLamps.All;
            Check(
                all.Select(lamp => lamp.Id).SequenceEqual(new[] { "candle", "lamp" }),
                "two lamps, a candle and a lamp, with the ids saved games use"
            );
            foreach (var lamp in all)
            {
                var node = new Node3D();
                AddChild(node);
                var glow = lamp.Build(node);
                Check(
                    glow.GetNodeOrNull<OmniLight3D>("LampLight") is { Visible: false, LightEnergy: 0 }
                        && glow.GetNodeOrNull<MeshInstance3D>("Halo") != null,
                    $"{lamp.Id} brings a halo and a light that is off"
                );
                Check(
                    node.FindChildren("*", "CollisionShape3D", true, false).Count == 0,
                    $"{lamp.Id} is not solid: the car drives over it"
                );
                Check(
                    node.FindChildren("*", "MeshInstance3D", true, false).Count > glow.GetChildCount(),
                    $"{lamp.Id} has glass and a stand as well as its glow"
                );
                node.QueueFree();
            }
            Check(
                lamps.Carried.Count == 0 && lamps.PlacedCount == 0,
                "a new game has no lamp in the bag or on the ground"
            );
            Check(
                village.FindChildren("DisplayLamp_*", "", true, false).Count == 2,
                "two lamps stand on the lamp maker's table"
            );

            // ---- The shop ----
            car.SetPose(world.Ground.At(maker.Position.X - 2, maker.Position.Z + 2, .15f), 0);
            game.OpenGlassmaker(maker);
            Check(game.State == GameState.GlassmakerShop && !car.Active, "talking to the lamp maker opens his shop");
            Check(Has("Lamp_candle") && Has("Lamp_lamp"), "the shop shows the candle and the lamp");
            int coins = game.Coins;
            game.TakeLamp("candle");
            Check(
                lamps.Carried.SequenceEqual(new[] { "candle" }) && game.Coins == coins,
                "a lamp is taken for nothing and goes into the bag"
            );
            game.TakeLamp("no-such-lamp");
            Check(lamps.Carried.Count == 1, "an unknown lamp is not taken");
            Check(game.State == GameState.GlassmakerShop, "the shop stays open after a lamp is taken");
            RoundTrip("a candle in the bag, shop open");
            Check(
                game.State == GameState.GlassmakerShop && Has("Lamp_lamp"),
                "the shop panel is shown again after loading"
            );
            game.TakeLamp("lamp");
            for (int i = 0; i < 10; i++)
                game.TakeLamp("candle");
            Check(lamps.Carried.Count == LampField.MaxCarried, "the bag holds at most eight lamps");
            Check(lamps.Carried[0] == "candle" && lamps.Carried[1] == "lamp", "the bag keeps the order of taking");
            var back = game.FindChild("Back", true, false) as Button;
            Check(back != null, "the shop has a Back button");
            back!.EmitSignal(BaseButton.SignalName.Pressed);
            Check(game.State == GameState.Exploring, "Back leaves the shop");

            // ---- Glasses for the lamp maker ----
            void Frames(int count)
            {
                for (int i = 0; i < count; i++)
                    game._Process(.1);
            }
            void NearMaker()
            {
                car.SetPose(world.Ground.At(maker.Position.X - 2, maker.Position.Z + 2, .15f), 0);
                game.OpenGlassmaker(maker);
            }
            var glassworks = world.Glassworks ?? throw new InvalidOperationException("The valley has no glassworks.");
            var trailer = game.Trailer;
            var drop = village.GlassmakerDrop;
            var dropLocal = village.Square.Transform.AffineInverse() * drop;
            Check(
                Mathf.Abs(dropLocal.X) < 9 && Mathf.Abs(dropLocal.Z) < 5,
                "the glasses are tipped out on the village square"
            );
            Check(
                drop.DistanceTo(maker.Position) < 6 && drop.DistanceTo(village.Mayor.Position) > 8,
                "the ring for the glasses lies beside the lamp maker, away from the mayor"
            );
            // Benches, stalls, lanterns and the fountain are solid; the paving and the ground lie below the query.
            var clear = new PhysicsShapeQueryParameters3D
            {
                Shape = new CylinderShape3D { Radius = 2, Height = 1.4f },
                Transform = new Transform3D(Basis.Identity, drop + Vector3.Up * 1.5f),
            };
            var blocking = world
                .GetWorld3D()
                .DirectSpaceState.IntersectShape(clear)
                .Select(hit => hit["collider"].AsGodotObject() as Node)
                .Where(node => node != null && node != car && !trailer.IsAncestorOf(node))
                .ToArray();
            Check(
                blocking.Length == 0,
                "nothing on the square stands in the ring for the glasses: "
                    + string.Join(", ", blocking.Select(node => node.GetPath().ToString()))
            );

            glassworks.SetStock(0);
            NearMaker();
            Check(
                Has("GlassOrder") && Has("StartMissingSupply") && !Has("StartGlassmakerTrip"),
                "without glasses in the glassworks the lamp maker shows the sand they are made from"
            );
            game.BeginGlassmakerTrip();
            Check(
                game.State == GameState.GlassmakerShop && game.Haul == null,
                "no glasses trip starts without glasses"
            );
            game.BeginMissingSupply();
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Loaded: false } sand && sand.Works == glassworks && sand.Load == HaulLoad.Sand,
                "he sends the empty trailer for sand for the glassworks"
            );
            var pit = world.SourceFor(HaulLoad.Sand);
            car.SetPose(world.Ground.At(pit.Position.X, pit.Position.Z, .15f), 0);
            Frames(1);
            ChallengeChecks.Solve(game);
            Check(game.Haul is { Loaded: true } && trailer.Load == HaulLoad.Sand, "the sand is loaded");
            car.SetPose(world.Ground.At(glassworks.Site.Position.X, glassworks.Site.Position.Z, .15f), 0);
            Frames(41);
            Check(glassworks.Stock == Works.PerLoad && game.Haul == null, "the glassworks blows glasses from the sand");

            NearMaker();
            Check(
                Has("StartGlassmakerTrip") && !Has("StartMissingSupply"),
                "with glasses in the glassworks the lamp maker offers the trip"
            );
            game.BeginGlassmakerTrip();
            Check(game.State == GameState.Challenge && game.Haul == null, "a quiz comes before the trip");
            RoundTrip("the quiz before the glasses trip");
            ChallengeChecks.Solve(game);
            var glassTrip = game.Haul;
            Check(
                glassTrip is { Loaded: false, ForGlassmaker: true, Load: HaulLoad.Glass }
                    && glassTrip.Contractor == maker
                    && glassTrip.Asker == glassworks.Worker
                    && trailer.Attached,
                "the empty trailer goes to the glassworks, whose worker loads it for the lamp maker"
            );
            RoundTrip("the trailer on its way for the glasses");
            Check(game.Haul is { ForGlassmaker: true, Loaded: false }, "the glasses trip is restored");
            NearMaker();
            Check(
                !Has("StartGlassmakerTrip") && Has("GlassOrder"),
                "during the trip his shop shows it instead of a second one"
            );
            game.FinishInteraction();
            car.SetPose(world.Ground.At(glassworks.Site.Position.X, glassworks.Site.Position.Z, .15f), 0);
            Frames(1);
            Check(game.State == GameState.Challenge, "the glassworks asks the loading question");
            ChallengeChecks.Solve(game);
            Check(
                game.Haul is { Loaded: true, ForGlassmaker: true }
                    && trailer.Load == HaulLoad.Glass
                    && glassworks.Stock == Works.PerLoad - Works.PerTrip,
                "the glasses are taken out of the glassworks and onto the trailer"
            );
            RoundTrip("a trailer of glasses on its way to the square");
            int coinsBeforeGlass = game.Coins;
            car.SetPose(world.Ground.At(drop.X, drop.Z, .15f), 0);
            Frames(1);
            Check(trailer.Dumping && !car.Active, "the bed tips on the square while the car waits");
            Frames(40);
            Check(
                game.Haul == null && trailer.Load == null && game.Coins == coinsBeforeGlass + Game.GlassmakerCoins,
                "the lamp maker pays for the glasses"
            );
            NearMaker();
            Check(Has("StartGlassmakerTrip"), "and takes glasses again whenever the glassworks has some");
            game.FinishInteraction();

            var savedTrip = game.CaptureProgress();
            savedTrip.Task.Trip = new SavedTrip { Kind = TripKinds.Glassmaker, Index = 0 };
            string tripFile = DiagnosticArtifacts.PathFor("glass-checks/glassmaker-trip.json");
            Check(SaveStore.Write(savedTrip, tripFile) && SaveStore.Read(tripFile) != null, "a glasses trip is saved");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.Glassmaker, Index = 1 },
                "a glasses trip to a second lamp maker"
            );

            // ---- Putting a lamp down ----
            var meadow = FindMeadow();
            car.SetPose(world.Ground.At(meadow.X, meadow.Y, .15f), 0);
            car.Active = true;
            Check(game.PutDownLamp(), "a lamp is put down beside the car");
            Check(
                lamps.PlacedCount == 1 && lamps.Carried.Count == LampField.MaxCarried - 1,
                "it leaves the bag and stands on the ground"
            );
            var standing = lamps.PositionAt(0);
            Check(lamps.KindAt(0) == "candle", "the first lamp of the bag is the one that is put down");
            Check(
                new Vector2(standing.X - car.Position.X, standing.Z - car.Position.Z).Length() is > 2 and < 4,
                "the lamp stands a car's width away from the car"
            );
            Check(
                Mathf.Abs(standing.Y - (world.DriveSurface(standing.X, standing.Z) + .02f)) < .05f,
                "the lamp stands on the ground"
            );
            Check(!lamps.CanStandAt(standing.X + .3f, standing.Z), "two lamps cannot stand on top of each other");
            Check(game.PutDownLamp() && lamps.PlacedCount == 2, "the next lamp finds a free place of its own");
            Check(lamps.NodeAt(0).Visible && lamps.NodeAt(0).IsInsideTree(), "a lamp that stands is in the scene");

            // Water and the edge of the valley are no place for a lamp.
            float riverZ = Enumerable
                .Range(-20, 40)
                .Select(i => i * 20f)
                .First(z => !River.OnBridge(River.CenterX(z), z));
            Check(!lamps.CanStandAt(River.CenterX(riverZ), riverZ), "no lamp stands in the river");
            Check(!lamps.CanStandAt(Settings.PlayableRadius, 0), "no lamp stands outside the valley");

            // ---- Taking a lamp up ----
            int placed = lamps.PlacedCount,
                carriedBefore = lamps.Carried.Count;
            car.SetPose(new Vector3(standing.X + 1, car.Position.Y, standing.Z), 0);
            Check(game.TakeUpLamp(), "a lamp within reach is taken up");
            Check(
                lamps.PlacedCount == placed - 1 && lamps.Carried.Count == carriedBefore + 1,
                "it leaves the ground and goes back into the bag"
            );
            car.SetPose(new Vector3(standing.X + 60, car.Position.Y, standing.Z), 0);
            Check(!game.TakeUpLamp(), "a lamp that is far away is not taken");
            while (lamps.Carried.Count < LampField.MaxCarried)
                lamps.TryTake("lamp");
            car.SetPose(world.Ground.At(meadow.X, meadow.Y, .15f), 0);
            int beforeFull = lamps.PlacedCount;
            Check(!lamps.TryTake("candle") && lamps.Carried.Count == LampField.MaxCarried, "a full bag takes no more");
            if (lamps.NearestIndex(car.Position, Game.LampReach) >= 0)
                Check(game.TakeUpLamp() && lamps.PlacedCount == beforeFull, "a full bag leaves the lamp on the ground");

            // ---- The talk button does it all ----
            lamps.Restore(new[] { "candle", "lamp" }, null);
            car.SetPose(world.Ground.At(meadow.X, meadow.Y, .15f), 0);
            car.Active = true;
            game._Process(.1);
            game.Interact();
            Check(
                lamps.PlacedCount == 1 && lamps.Carried.Count == 1,
                "the talk button puts a lamp down where nothing else is to be done"
            );
            game.Interact();
            Check(
                lamps.PlacedCount == 2 && lamps.Carried.Count == 0,
                "and puts the next one down beside the car too, as the first is out of reach"
            );
            game.Interact();
            Check(lamps.PlacedCount == 2, "with the bag empty it does nothing");
            var first = lamps.PositionAt(0);
            car.SetPose(new Vector3(first.X + 1, car.Position.Y, first.Z), 0);
            game._Process(.1);
            game.Interact();
            Check(
                lamps.PlacedCount == 1 && lamps.Carried.Count == 1,
                "the talk button takes a lamp up when the car is beside it"
            );

            // ---- The night ----
            lamps.Restore(
                new[] { "candle" },
                Enumerable
                    .Range(0, 9)
                    .Select(i => new LampSpot(i % 2 == 0 ? "candle" : "lamp", meadow.X + 3 + i * 1.5f, meadow.Y + 5, i))
            );
            car.SetPose(world.Ground.At(meadow.X, meadow.Y, .15f), 0);
            village.SetLampGlow(0, car.Position);
            Check(
                !lamps.IsGlowing && Enumerable.Range(0, lamps.PlacedCount).All(i => !Glow(i).Visible),
                "by day the lamps do not shine"
            );
            Check(lamps.LitCount == 0, "by day no lamp lights the ground");
            village.SetLampGlow(1, car.Position);
            Check(
                lamps.IsGlowing && Enumerable.Range(0, lamps.PlacedCount).All(i => Glow(i).Visible),
                "at night every lamp shines"
            );
            Check(lamps.LitCount is > 0 and <= 5, $"only the nearest few lamps light the ground ({lamps.LitCount})");
            game.Weather.Begin(WeatherKind.Night, instant: true);
            game.Weather.Tick(.1f);
            Check(lamps.IsGlowing, "the night of the weather switches the lamps on");
            game.Weather.SetTime(DayTime.Day, instant: true);
            game.Weather.Tick(.1f);
            Check(!lamps.IsGlowing && lamps.LitCount == 0, "and the day switches them off again");
            lamps.Restore(null, Enumerable.Range(0, 40).Select(i => new LampSpot("candle", i * 2, 40, 0)));
            Check(lamps.PlacedCount == LampField.MaxPlaced, "a valley holds at most thirty lamps");
            lamps.Restore(new[] { "candle" }, null);
            Check(!game.PutDownLamp() || lamps.PlacedCount <= 1, "a lamp is put down at most where there is room");
            lamps.Restore(
                new[] { "candle", "lamp" },
                new[]
                {
                    new LampSpot("candle", meadow.X + 4, meadow.Y, .5f),
                    new LampSpot("lamp", meadow.X + 8, meadow.Y, 1),
                }
            );
            RoundTrip("a candle and a lamp on the ground and two in the bag");

            // ---- What a save may not say ----
            Rejected(data => data.Lamps.Carried = new[] { "no-such-lamp" }, "an unknown lamp in the bag");
            Rejected(
                data => data.Lamps.Carried = Enumerable.Repeat("candle", 9).ToArray(),
                "more lamps than the bag holds"
            );
            Rejected(data => data.Lamps.Carried = null!, "no bag");
            Rejected(data => data.Lamps.Placed = null!, "no list of standing lamps");
            Rejected(
                data =>
                    data.Lamps.Placed = new[]
                    {
                        new PlacedLampSave
                        {
                            Kind = "no-such-lamp",
                            X = 3,
                            Z = 3,
                        },
                    },
                "an unknown lamp on the ground"
            );
            Rejected(
                data => data.Lamps.Placed = Enumerable.Repeat(new PlacedLampSave { Kind = "lamp" }, 31).ToArray(),
                "more lamps than the valley holds"
            );
            Rejected(
                data =>
                    data.Lamps.Placed = new[]
                    {
                        new PlacedLampSave { Kind = "lamp", X = 5000 },
                    },
                "a lamp beyond the edge of the valley"
            );
            Rejected(
                data =>
                    data.Lamps.Placed = new[]
                    {
                        new PlacedLampSave { Kind = "lamp", Yaw = 1000 },
                    },
                "a lamp that is turned round a thousand times"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.GlassmakerShop;
                    data.Nearby = World.SorcererId;
                },
                "the lamp maker's shop beside someone else"
            );
            Rejected(data => data.Nearby = World.TreasurerId + 1, "a character beyond the lamp maker");

            // A file made before there were lamps loads with none.
            var plain = game.CaptureProgress();
            var json = JsonSerializer.SerializeToNode(plain)!.AsObject();
            json.Remove("Lamps");
            string oldFile = DiagnosticArtifacts.PathFor("glass-checks/before-lamps.json");
            System.IO.File.WriteAllText(oldFile, json.ToJsonString());
            var old = SaveStore.Read(oldFile);
            Check(
                old != null && old.Lamps.Carried.Length == 0 && old.Lamps.Placed.Length == 0,
                "a save from before the lamps loads without any"
            );
            game.RestoreProgress(old!);
            Check(
                lamps.PlacedCount == 0 && lamps.Carried.Count == 0,
                "loading such a save takes the lamps off the ground and out of the bag"
            );

            GD.Print("GLASS_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private Node3D Glow(int index) => world.Village.Lamps.NodeAt(index).GetNode<Node3D>("Glow");

    /// <summary>A dry spot on open ground, away from the village, for putting lamps down (x, z).</summary>
    private Vector2 FindMeadow()
    {
        var village = world.Village.Center;
        for (int ring = 60; ring < 300; ring += 20)
        for (int step = 0; step < 16; step++)
        {
            float angle = step * Mathf.Tau / 16;
            float x = village.X + Mathf.Cos(angle) * ring,
                z = village.Z + Mathf.Sin(angle) * ring;
            bool dry = River.Distance(x, z) > River.HalfWidth(z) + 12 && !River.OnBridge(x, z);
            if (
                dry
                && new Vector2(x, z).Length() < Settings.PlayableRadius - 40
                && !world.Village.IsReserved(new(x, 0, z))
            )
                return new Vector2(x, z);
        }
        throw new InvalidOperationException("no dry meadow near the village");
    }
}
