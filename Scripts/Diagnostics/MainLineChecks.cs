using System;
using System.IO;
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
/// The great railway across the valley (<c>docs/MAINLINE_PLAN.md</c>): the hub, the three arms with their terminals and
/// the forge are planned clear of everything and the track keeps to the ground; seventeen trailer trips build the line,
/// each tipped out where the track laid so far ends, and the rails of each arm are forged of stone and loaded at the forge,
/// where the engine driver asks the question; the finished arms carry the car on the train to a terminal and back, and a
/// save made on the train puts the car and the driver at the hub; the damaged saves are turned down and a save from
/// before the line loads.
/// </summary>
public partial class MainLineChecks : Node
{
    private readonly Game game;
    private readonly World world;
    private readonly Vehicle car;
    private int files;

    public MainLineChecks(Game game, World world, Vehicle car)
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
            string path = DiagnosticArtifacts.PathFor("mainline-checks/progress.json");
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
                Check(before == after, $"the line restores: {what}");
            }
            void Rejected(Action<SaveData> damage, string what)
            {
                var data = game.CaptureProgress();
                damage(data);
                string file = DiagnosticArtifacts.PathFor($"mainline-checks/invalid-{files++}.json");
                Check(SaveStore.Write(data, file) && SaveStore.Read(file) == null, $"rejected: {what}");
            }
            void Near(Vector3 p) => car.SetPose(world.Ground.At(p.X, p.Z, .15f), 0);

            // ---- The places ----
            var site = world.MainLinePlace;
            var line = world.MainLine;
            var forge = world.LineForge;
            var engineer = world.Engineer;
            Check(
                site != null && line != null && forge != null && engineer != null,
                "the valley has the great line, its forge and its engine driver"
            );
            GD.Print(
                $"LINE hub={site!.Hub.Position} arms={string.Join('/', site.Arms.Select(a => (int)a.Length))} {world.LinePlanNote}"
            );
            Check(engineer!.Role is EngineerRole && line!.Engineer == engineer, "the engine driver keeps the line");
            Check(world.MainLineBuildable, "the works, the log yard and the glassworks are there to build it");
            Check(world.ActorAt(World.EngineerId) == engineer, "the driver has his actor ID");
            var hub = site.Hub;
            Check(
                World.InsideValley(hub.Position) && World.InsideValley(hub.Drop) && World.InsideValley(hub.Talk),
                "the hub, its apron and the driver's place are inside the valley"
            );
            Check(
                Flat(hub.Position, Vector3.Zero) > 262
                    && Math.Min(world.Routes.Distance(hub.Drop), world.OutlandRoutes.Distance(hub.Drop)) < 60,
                "the hub stands at the edge of the old valley, with a road to its apron"
            );
            Check(site.Arms.Length == MainLinePlans.Arms && site.Terminals.Length == MainLinePlans.Arms, "three arms");
            for (int arm = 0; arm < MainLinePlans.Arms; arm++)
            {
                var route = site.Arms[arm];
                var terminal = site.Terminals[arm];
                var border = MainLinePlans.ArmBorder(arm);
                Check(route.Length > 150 && route.Count > 60, $"arm {arm} is {route.Length:0} m long");
                Check(route.Points.All(p => World.InsideValley(p)), $"arm {arm} keeps inside the valley all the way");
                Check(
                    Enumerable
                        .Range(0, route.Count - 1)
                        .All(i => Flat(route.Ground(i), route.Ground(i + 1)) < LineRoute.Step + 3.5f),
                    $"arm {arm} has no gaps"
                );
                Check(
                    Enumerable.Range(0, route.Count).All(i => route.Bed(i) >= route.Ground(i).Y + .2f),
                    $"arm {arm} rides above the ground"
                );
                Check(
                    Flat(route.Ground(route.Count - 1), terminal.End) < 3,
                    $"arm {arm} ends at the end of the platform track of its terminal"
                );
                var roadEnd = world.BorderRoads.First(r => r.Border == border).Points[^1];
                Check(
                    Flat(terminal.Position, roadEnd) < 260 && World.InsideValley(terminal.Drop),
                    $"the terminal of the {border} arm stands near the end of the road to it"
                );
                Check(
                    Math.Min(world.Routes.Distance(terminal.Drop), world.OutlandRoutes.Distance(terminal.Drop)) < 40,
                    $"the terminal of the {border} arm has a road to its apron"
                );
                var middle = route.Ground(route.Count / 2);
                var across = Vector3.Up.Cross(route.Forward(route.Length / 2)) * 40;
                // A place well off every track and every station is not in the zone.
                var aside = new[] { middle + across, middle - across }.Where(p =>
                    site.Arms.All(a => a.Points.All(q => Flat(p, q) > 12))
                    && Flat(p, hub.Position) > 60
                    && site.Terminals.All(t => Flat(p, t.Position) > 60)
                );
                Check(
                    world.InMainLineZone(middle)
                        && world.InMainLineZone(terminal.Position)
                        && aside.All(p => !world.InMainLineZone(p)),
                    $"arm {arm} keeps the plants off its ground and only there"
                );
                var clipped = Enumerable
                    .Range(0, route.Count)
                    .Where(i => Flat(route.Ground(i), Vector3.Zero) <= WorldTuning.CorePlayableRadius)
                    .Where(i => world.Plants.NearTrunk(route.Ground(i), 1.9f))
                    .ToArray();
                Check(
                    clipped.Length <= 4,
                    $"arm {arm} passes the trees of the old valley, with at most a few points within a trunk's reach (points {string.Join(',', clipped.Take(6))} of {route.Count})"
                );
                if (arm > 0)
                    Check(
                        route.Parent >= 0 && route.Parent < arm && route.Trunk != null,
                        $"arm {arm} branches off the track before it"
                    );
            }
            Check(
                world.MainLinePlace!.Arms[0].Parent == -1 && Flat(site.Arms[0].Ground(0), hub.End) < 1,
                "the first arm leaves the hub"
            );
            Check(
                Flat(forge!.Drop, world.SlopePlace!.Position) < 60 && World.InsideValley(forge.Drop),
                "the forge stands by the mountain slope"
            );
            var mole = world.WorldPets.SingleOrDefault(pet => pet.Kind == CollectorPetKind.Mole);
            Check(mole != null && !mole.IsOwned && mole.Collector == engineer, "the mole waits by the driver");

            // ---- A saved game is made like the valley ----
            Check(
                line.Stage == 0 && !line.HubBedShown && !line.HubStationShown,
                "at first nothing stands but the stakes and the flag"
            );
            Check(
                Enumerable.Range(0, MainLinePlans.Arms).All(a => !line.SectionShown(a, 0) && line.StakesShown(a, 0)),
                "the way of every arm is marked with stakes"
            );
            Frames(1);
            Check(engineer.Guided, "the engine driver shows his mark");
            Rejected(data => data.MainLine = null!, "no section of the line");
            Rejected(data => data.MainLine.Stage = -1, "a line with a negative stage");
            Rejected(data => data.MainLine.Stage = MainLinePlans.Trips + 1, "a line with eighteen loads");
            Rejected(data => data.MainLine.Rails = 1, "rails in the forge before any wants them");
            Rejected(
                data =>
                {
                    data.MainLine.Stage = MainLinePlans.RailStep(0);
                    data.MainLine.Rails = MainLinePlans.RailCapacity + 1;
                },
                "a forge with five rails"
            );
            Rejected(
                data =>
                {
                    data.State = GameState.MainLineStatus;
                    data.Nearby = World.HunterId;
                },
                "the driver's panel at the hunter"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.MainLine, Index = 1 },
                "a trip for a second line"
            );
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.MainLineForge, Index = 0 },
                "stone for the forge before any rails are wanted"
            );

            // ---- Seventeen loads, and stone for the forge ----
            int coinsBefore = game.Coins,
                forgedLoads = 0;
            var fronts = new System.Collections.Generic.List<Vector3>();
            for (int step = 0; step < MainLinePlans.Trips; step++)
            {
                var plan = MainLinePlans.Step(step);
                var cargo = plan.Cargo;
                bool save = step is 0 or 4 or 6;
                foreach (var works in world.AllWorks)
                    works.SetStock(Works.Capacity);
                var talk = line.TalkAtHub();
                Near(talk);
                game.OpenEngineer(engineer);
                Check(
                    game.State == GameState.MainLineStatus && Has("LineLoads") && Has("LineForge"),
                    $"the driver offers load {step + 1} of the line"
                );
                if (cargo == HaulLoad.Rails && forge.Rails == 0)
                {
                    // The forge has no rails: the green check mark sends stone to the forge first.
                    Check(Has("StartLineTrip"), "the driver offers stone for the forge");
                    if (save)
                        RoundTrip("the driver's panel with an empty forge");
                    game.BeginMainLineStart();
                    Check(game.State == GameState.Challenge, "stone for the forge asks its quiz first");
                    ChallengeChecks.Solve(game);
                    var stone = game.Haul;
                    Check(
                        stone is { Loaded: false, ForgeTrip: not null }
                            && stone.Load == HaulLoad.Stones
                            && trailer.Attached,
                        "the empty trailer goes for stone"
                    );
                    if (save)
                        RoundTrip("a trip for stone for the forge");
                    Near(game.Haul!.Source.Position);
                    Frames(1);
                    Check(game.State == GameState.Challenge, "the quarry asks the loading question");
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Loaded: true } && trailer.Load == HaulLoad.Stones,
                        "the trailer is full of stone"
                    );
                    Near(forge.Drop);
                    Frames(1);
                    Check(trailer.Dumping, "the bed tips before the forge");
                    Frames(40);
                    Check(
                        forge.Rails == MainLinePlans.RailsPerStone && game.Haul == null && line.Stage == step,
                        "the forge makes two rails of the stone"
                    );
                    forgedLoads++;
                    Check(forge.Forging, "the forge works");
                    RoundTrip("rails in the forge");
                    Near(talk);
                    game.OpenEngineer(engineer);
                }
                else if (cargo == HaulLoad.Planks && step == MainLinePlans.FirstStep(0) + 1)
                {
                    // Without planks the driver shows what the sawmill needs and sends the player for the logs.
                    foreach (var works in world.AllWorks)
                        works.SetStock(0);
                    game.OpenEngineer(engineer);
                    Check(
                        !Has("StartLineTrip") && Has("MissingGoods") && Has("StartMissingSupply"),
                        "the driver shows what is missing when there are no planks"
                    );
                    game.BeginMissingSupply();
                    ChallengeChecks.Solve(game);
                    Check(
                        game.Haul is { Loaded: false, Works: not null },
                        "the driver sends the trailer for the raw material"
                    );
                    var noTrip = game.CaptureProgress();
                    noTrip.Task.Trip = null;
                    game.RestoreProgress(noTrip);
                    foreach (var works in world.AllWorks)
                        works.SetStock(Works.Capacity);
                    Near(talk);
                    game.OpenEngineer(engineer);
                }
                int rails = forge.Rails;
                Check(Has("StartLineTrip"), $"load {step + 1}: the green check mark is there");
                if (save)
                    RoundTrip("the driver's panel");
                game.BeginMainLineStart();
                Check(game.State == GameState.Challenge, "the trip asks its quiz first");
                if (save)
                    RoundTrip("the quiz before a line trip");
                ChallengeChecks.Solve(game);
                var trip = game.Haul;
                Check(
                    trip is { Loaded: false } && trip.Line == line && trip.Step == step && trailer.Attached,
                    $"load {step + 1} sends the empty trailer"
                );
                Check(trip!.Load == cargo, $"the trailer goes for {cargo}");
                Frames(1);
                Check(!engineer.Guided, "the driver's mark is gone while the trailer is away");
                if (save)
                    RoundTrip($"a trip for the line ({step + 1})");
                var source = game.Haul!.Source;
                if (cargo == HaulLoad.Rails)
                    Check(
                        Flat(source.Position, forge.Drop) < 1 && trip.Asker == engineer,
                        "the rails are loaded at the forge and the driver asks the question"
                    );
                Near(source.Position);
                Frames(1);
                Check(game.State == GameState.Challenge && game.CurrentChallenge != null, "the loading question");
                ChallengeChecks.Solve(game);
                Check(game.Haul is { Loaded: true } && trailer.Load == cargo, "solving the question fills the trailer");
                if (cargo == HaulLoad.Rails)
                    Check(forge.Rails == rails - 1, "a rail leaves the stack");
                if (save)
                    RoundTrip($"a full trailer for the line ({step + 1})");
                var front = line.Front(step);
                fronts.Add(front);
                Check(Flat(game.Haul.Destination, front) < 1, "the load goes where the track laid so far ends");
                Near(front);
                Frames(1);
                Check(trailer.Dumping, "the bed tips at the end of the track");
                Frames(40);
                Check(line.Stage == step + 1, $"load {step + 1} builds the {MainLinePlans.Parts[step]}");
                Check(game.Haul == null && !game.TaskActive, "the next load waits for the player");
                switch (plan.Part)
                {
                    case LinePart.HubBed:
                        Check(line.HubBedShown && !line.HubStationShown, "the hub's bed and platform stand");
                        break;
                    case LinePart.HubStation:
                        Check(line.HubStationShown, "the hub's station stands");
                        break;
                    case LinePart.Section:
                        Check(
                            line.SectionShown(plan.Arm, plan.Section) && !line.StakesShown(plan.Arm, plan.Section),
                            $"a stretch of arm {plan.Arm} is laid and its stakes are gone"
                        );
                        break;
                    case LinePart.Terminal:
                        Check(line.TerminalShown(plan.Arm), $"the terminal of arm {plan.Arm} stands");
                        break;
                    case LinePart.Lamps:
                        Check(line.LampsShown(plan.Arm), $"the lamps of arm {plan.Arm} shine");
                        break;
                }
                if (step < MainLinePlans.Trips - 1)
                    Check(
                        game.Coins
                            <= coinsBefore + MainLinePlans.HubCoins + MainLinePlans.ArmCoins * MainLinePlans.Arms,
                        "no big payment before the line is finished"
                    );
            }
            Check(line.Complete, "the seventeenth load finishes the line");
            Check(forgedLoads == 2, "three arms of rails are two loads of stone: the forge makes two rails of each");
            Check(
                game.Coins
                    >= coinsBefore
                        + MainLinePlans.HubCoins
                        + MainLinePlans.ArmCoins * MainLinePlans.Arms
                        + MainLinePlans.Coins,
                "the line pays for the hub, every arm and the whole"
            );
            for (int arm = 0; arm < MainLinePlans.Arms; arm++)
            {
                int first = MainLinePlans.FirstStep(arm);
                var along = Enumerable.Range(0, MainLinePlans.Sections).Select(k => fronts[first + k]).ToArray();
                var armRoute = site.Arms[arm];
                var distances = along.Select(p => Flat(p, armRoute.Ground(0))).ToArray();
                Check(
                    distances[0] < distances[1] && distances[1] < distances[2],
                    $"the stretches of arm {arm} are laid one beyond the other: the site moves on"
                );
            }
            Check(mole!.IsOwned, "the whole line brings the mole");
            Frames(1);
            Check(!engineer.Guided, "the driver's mark is gone once the line is finished");
            RoundTrip("a finished line");
            Rejected(
                data => data.Task.Trip = new SavedTrip { Kind = TripKinds.MainLine, Index = 0 },
                "a trip to a line that has no load left"
            );

            // ---- A ride on the train ----
            Near(line.TalkAtHub());
            game.OpenEngineer(engineer);
            Check(
                game.State == GameState.MainLineStatus && Has("LineRides") && Has("RideButton0") && Has("RideButton2"),
                "the finished arms offer a ride each"
            );
            for (int arm = 0; arm < MainLinePlans.Arms; arm++)
            {
                game.OpenEngineer(engineer);
                game.BeginMainLineRide(arm);
                Check(game.State == GameState.Challenge, $"the ticket for arm {arm} is a question");
                ChallengeChecks.Solve(game);
                Check(game.LineRiding && !car.Active, $"the train sets off with the car aboard ({arm})");
                var start = car.Position;
                Frames(60);
                Check(
                    Flat(car.Position, start) > 5 && Flat(car.Position, line.Train.GlobalPosition) < 12,
                    $"the car rides along on the flatbed (moved {Flat(car.Position, start):0} m, {Flat(car.Position, line.Train.GlobalPosition):0} m from the train, riding {game.LineRiding})"
                );
                Check(Flat(engineer.Position, line.Train.GlobalPosition) < 12, "the driver stands in the cab");
                Check(line.Train.Running, "the engine smokes and runs");
                if (arm == 1)
                {
                    // A save made on the train puts the car at the hub and the driver at the platform.
                    var saved = game.CaptureProgress();
                    var at = new Vector3(
                        saved.Vehicle.Position[0],
                        saved.Vehicle.Position[1],
                        saved.Vehicle.Position[2]
                    );
                    Check(Flat(at, hub.Return) < 1, "a save on the train puts the car on the apron of the hub");
                    game.RestoreProgress(saved);
                    Check(!game.LineRiding && game.LineAtTerminal < 0, "the loaded game has no train on its way");
                    Check(Flat(engineer.Position, line.TalkAtHub()) < 1, "the driver waits at the hub again");
                    continue;
                }
                for (int i = 0; i < 600 && game.LineRiding; i++)
                    Frames(1);
                Check(!game.LineRiding && game.LineAtTerminal == arm, $"the train stops at the terminal of arm {arm}");
                var terminal = site.Terminals[arm];
                Check(Flat(car.Position, terminal.Return) < 2 && car.Active, "the car is put down on the apron");
                Check(Flat(engineer.Position, terminal.Talk) < 1, "the driver steps onto the platform");
                Check(!line.Train.Running, "the train stands");
                game.OpenEngineer(engineer);
                Check(
                    Has("LineHome") && Has("LineHomeRide") && !Has("StartLineTrip"),
                    "at the terminal the driver offers the way back"
                );
                RoundTrip("a stay at the terminal");
                Check(
                    game.LineAtTerminal < 0 && Flat(engineer.Position, line.TalkAtHub()) < 1,
                    "a save puts the driver at the hub"
                );
                // Back to the terminal once more to ride home.
                Near(line.TalkAtHub());
                game.OpenEngineer(engineer);
                game.BeginMainLineRide(arm);
                ChallengeChecks.Solve(game);
                for (int i = 0; i < 600 && game.LineRiding; i++)
                    Frames(1);
                Check(game.LineAtTerminal == arm, "at the terminal again");
                game.OpenEngineer(engineer);
                game.BeginMainLineHome();
                Check(game.LineRiding && !car.Active, "the train takes the car home without a question");
                for (int i = 0; i < 600 && game.LineRiding; i++)
                    Frames(1);
                Check(!game.LineRiding && game.LineAtTerminal < 0, "the train stops at the hub");
                Check(Flat(car.Position, hub.Return) < 2 && car.Active, "the car stands on the apron of the hub");
                Check(Flat(engineer.Position, line.TalkAtHub()) < 1, "the driver steps onto the platform of the hub");
                // Driving off from a terminal sends the driver and the train home empty.
                Near(line.TalkAtHub());
                game.OpenEngineer(engineer);
                game.BeginMainLineRide(arm);
                ChallengeChecks.Solve(game);
                for (int i = 0; i < 600 && game.LineRiding; i++)
                    Frames(1);
                Check(game.LineAtTerminal == arm, "at the terminal for the third time");
                car.SetPose(world.Ground.At(0, 0, .15f), 0);
                Frames(2);
                Check(
                    game.LineAtTerminal < 0 && Flat(engineer.Position, line.TalkAtHub()) < 1,
                    "the driver goes home when the car drives away"
                );
            }
            Near(Vector3.Zero);

            // ---- A save from before the line ----
            {
                var legacy = game.CaptureProgress();
                string file = DiagnosticArtifacts.PathFor("mainline-checks/legacy.json");
                var node = JsonSerializer.SerializeToNode(legacy)!.AsObject();
                node.Remove("MainLine");
                File.WriteAllText(file, node.ToJsonString());
                var read = SaveStore.Read(file);
                Check(
                    read != null && read.MainLine.Stage == 0 && read.MainLine.Rails == 0,
                    "a save from before the line loads without it"
                );
                game.RestoreProgress(read!);
                Check(world.MainLine!.Stage == 0 && world.LineForge!.Rails == 0, "and the line starts from nothing");
            }
            GD.Print("MAINLINE_CHECKS_PASSED");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
