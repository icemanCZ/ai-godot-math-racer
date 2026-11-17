using System;
using Godot;

namespace MathRacer;

/// <summary>
/// One trailer trip for the beaver's ford: drive to the quarry or pit, answer the loading question,
/// then tip the load out at the ford. Road arrows and the target ring belong to this node.
/// </summary>
public partial class FordTrip : Node3D
{
    private readonly World world;
    private readonly Ford ford;
    private readonly RouteGuide guide;
    private Node3D? target;

    internal int Trip { get; }
    internal bool Loaded { get; private set; }

    /// <summary>Set on reaching the current target; cleared when the car leaves it again.</summary>
    internal bool Arrived { get; set; }
    internal HaulLoad Load => Ford.TripLoads[Trip];
    internal HaulSource Source => world.HaulSources[(int)Load];
    internal bool FarBank => Ford.DumpsOnFarBank(Trip);
    internal Vector3 Destination => Loaded ? ford.DumpFor(Trip) : Source.Position;
    internal Vector3 Waypoint => guide.Waypoint;

    private Vector3 RoadApproach =>
        !Loaded ? Source.Road
        : FarBank ? ford.Site.FarRoad
        : ford.Site.NearRoad;

    internal FordTrip(World world, Vehicle car, int trip, bool loaded)
    {
        this.world = world;
        ford = world.Ford!;
        Trip = trip;
        Loaded = loaded;
        Name = "FordTrip";
        guide = new RouteGuide(world, car, this, () => Destination);
        guide.Retarget(RoadApproach);
    }

    public override void _Ready()
    {
        target = new Node3D { Name = "HaulTarget" };
        AddChild(target);
        Art.Cylinder(target, Vector3.Up * .05f, 3.3f, 3.3f, .1f, "f1c232");
        Art.Cylinder(target, Vector3.Up * .07f, 2.7f, 2.7f, .1f, "fff1b8");
        Art.TaskMarker(target, Vector3.Up * 5, "!", true);
        guide.CreateArrows();
        PlaceTarget();
    }

    internal void SetLoaded(bool loaded)
    {
        Loaded = loaded;
        Arrived = false;
        guide.Retarget(RoadApproach);
        PlaceTarget();
    }

    internal void Tick(double delta) => guide.Tick(delta);

    internal bool Reached(Vector3 car) =>
        new Vector2(car.X - Destination.X, car.Z - Destination.Z).Length() < (Loaded ? 4.5f : 6);

    private void PlaceTarget()
    {
        if (target == null)
            return;
        var p = Destination;
        target.Position = new Vector3(p.X, world.Ground.Height(p.X, p.Z), p.Z);
    }
}

public partial class Game
{
    private Trailer trailer = null!;
    private bool trailerParade;
    private readonly Random loadRandom = new(Settings.Seed + 2313);

    /// <summary>The active ford trip, which takes the same single task slot as an expedition.</summary>
    internal FordTrip? Haul { get; private set; }
    internal bool TrailerOwned { get; private set; }
    internal Trailer Trailer => trailer;

    /// <summary>A delivery, collection or ford trip is under way; only one runs at a time.</summary>
    internal bool TaskActive => Delivery != null || Haul != null;

    /// <summary>Companions trail further back while the trailer is hitched.</summary>
    private float TrailerTrailShift => trailer?.Attached == true ? 3.5f : 0;

    private CpuParticles3D fordSplash = null!;

    private void SetupTrailer()
    {
        trailer = new Trailer(world, vehicle);
        world.AddChild(trailer);
        fordSplash = new CpuParticles3D
        {
            Name = "FordSplash",
            Emitting = false,
            Amount = 40,
            Lifetime = .7f,
            Mesh = new SphereMesh
            {
                Radius = .09f,
                Height = .18f,
                RadialSegments = 6,
                Rings = 3,
            },
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1.1f, .05f, 1.6f),
            Direction = Vector3.Up,
            Spread = 40,
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 3.2f,
            Gravity = new Vector3(0, -9, 0),
            LocalCoords = false,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(.86f, .95f, 1, .7f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        world.AddChild(fordSplash);
        ui.FordQuestRequested += BeginFordTrip;
        ui.TrailerToggleRequested += ToggleTrailer;
        ui.TrailerPaintSelected += SelectTrailerPaint;
    }

    internal void OpenBeaver()
    {
        if (world.Beaver == null || world.Ford == null)
            return;
        ClearChallenge();
        EnterInteraction(GameState.FordStatus, world.Beaver);
        ShowBeaver();
        if (Haul == null && !world.Ford.Passable && Delivery == null)
            Speak(world.Beaver.Greeting, world.Beaver);
    }

    private void ShowBeaver() =>
        ui.Beaver(world.Beaver!, world.Ford!.Stage, Haul, Haul == null && Delivery == null && !world.Ford.Passable);

    internal void BeginFordTrip()
    {
        if (State != GameState.FordStatus || world.Ford is not { Passable: false } ford || TaskActive)
            return;
        StartFordTrip(ford.Stage, loaded: false);
        Start();
        Speak(TripInstruction(Haul!), world.Beaver);
        SaveProgress();
    }

    private void StartFordTrip(int trip, bool loaded)
    {
        EndFordTrip(refreshUi: false);
        Haul = new FordTrip(world, vehicle, trip, loaded);
        AddChild(Haul);
        if (!trailer.Attached)
            trailer.Attach(true);
        // Keep the bed lowering after a dump; only a different load replaces the contents.
        HaulLoad? load = loaded ? Haul.Load : null;
        if (trailer.Load != load)
            trailer.SetLoad(load);
        ui.HaulHud(Haul, world.Beaver);
        UpdateDeliveryGuides();
    }

    private void EndFordTrip(bool refreshUi = true)
    {
        if (Haul == null)
            return;
        RemoveChild(Haul);
        Haul.QueueFree();
        Haul = null;
        if (!refreshUi)
            return;
        ui.DeliveryHud(null);
        UpdateDeliveryGuides();
    }

    /// <summary>Water sprays from the wheels while the car drives through the finished ford.</summary>
    private void TickFordSplash()
    {
        bool wading =
            State == GameState.Exploring
            && world.Ford is { Passable: true } ford
            && ford.Contains(vehicle.Position.X, vehicle.Position.Z)
            && Mathf.Abs(vehicle.Speed) > 2.5f;
        if (wading)
            fordSplash.Position = vehicle.Position + Vector3.Up * .2f;
        fordSplash.Emitting = wading;
    }

    private void UpdateBeaverGuide() => world.Beaver?.Guide(world.Ford is { Passable: false } && !TaskActive);

    private void TickHaul(double delta)
    {
        if (Haul == null)
            return;
        Haul.Tick(delta);
        ui.DeliveryDirection(Haul.Waypoint, vehicle);
        if (!Haul.Reached(vehicle.Position))
        {
            if (!trailer.Dumping)
                Haul.Arrived = false;
            return;
        }
        if (Haul.Arrived)
            return;
        Haul.Arrived = true;
        if (Haul.Loaded)
            BeginDump();
        else
            BeginLoading();
    }

    /// <summary>The loading question; the beaver asks it from afar, like a radio call.</summary>
    private void BeginLoading()
    {
        if (Haul is not { Loaded: false } || world.Beaver == null || State != GameState.Exploring)
            return;
        nearby = world.Beaver;
        vehicle.Active = false;
        BeginQuiz(load: Haul.Load);
    }

    internal void CompleteFordLoading()
    {
        ClearChallenge();
        if (Haul is not { Loaded: false } trip)
        {
            CloseInteraction();
            return;
        }
        trip.SetLoaded(true);
        trailer.Fill(trip.Load);
        ui.HaulHud(trip, world.Beaver);
        CloseInteraction();
        Speak(TripInstruction(trip), world.Beaver);
        SaveProgress();
    }

    private void BeginDump()
    {
        if (Haul is not { Loaded: true } trip || trailer.Dumping)
            return;
        // The car waits while the bed tips; the callback returns control.
        vehicle.Active = false;
        trailer.Dump(() => FinishDump(trip));
    }

    private void FinishDump(FordTrip trip)
    {
        if (State == GameState.Exploring)
            vehicle.Active = true;
        if (Haul != trip || world.Ford is not { } ford)
            return;
        ford.SetStage(trip.Trip + 1);
        world.Beaver?.Celebrate();
        if (ford.Passable)
        {
            CompleteFord();
            return;
        }
        CelebrateAt(ford.DumpFor(trip.Trip) + Vector3.Up * 2, $"Bóďa děkuje!  {ford.Stage} / {Ford.Trips}");
        StartFordTrip(ford.Stage, loaded: false);
        Speak(TripInstruction(Haul!), world.Beaver);
        SaveProgress();
    }

    private void CompleteFord()
    {
        var ford = world.Ford!;
        EndFordTrip();
        TrailerOwned = true;
        trailerParade = true;
        var center = new Vector3(ford.Site.CenterX, ford.Site.DeckHeight + 2, ford.Site.Z);
        CelebrateAt(center, "Brod je hotový! Vozík je tvůj.");
        var outward = followCamera.GlobalPosition - center;
        outward.Y = 0;
        followCamera.ShowMoment(center, outward.LengthSquared() > .01f ? outward.Normalized() : Vector3.Back);
        Speak("Hurá, brod je hotový! Teď můžeš přejet řeku. Vozík si nech, najdeš ho v garáži.", world.Beaver);
        UpdateDeliveryGuides();
        SaveProgress();
    }

    internal void ToggleTrailer()
    {
        if (State != GameState.Garage || !TrailerOwned || Haul != null)
            return;
        trailerParade = !trailerParade;
        trailer.Attach(trailerParade);
        ShowGarage();
        SaveProgress();
    }

    internal void SelectTrailerPaint(string id)
    {
        if (State != GameState.Garage || !TrailerOwned || !Array.Exists(TrailerModel.Paints, p => p.Id == id))
            return;
        trailer.SetPaint(id);
        ShowGarage();
        SaveProgress();
    }

    private void RestoreFord(SaveData data)
    {
        EndFordTrip(refreshUi: false);
        var ford = world.Ford;
        ford?.SetStage(data.FordStage);
        TrailerOwned = ford is { Passable: true } && data.TrailerOwned;
        trailerParade = TrailerOwned && data.TrailerAttached;
        trailer.SetPaint(data.TrailerPaint);
        if (ford != null && data.FordTrip >= 0 && data.FordTrip == ford.Stage && !ford.Passable)
        {
            trailer.SetLoad(null);
            StartFordTrip(data.FordTrip, data.FordTripLoaded);
            trailer.Snap();
        }
        else
        {
            trailer.SetLoad(null);
            trailer.Attach(trailerParade);
        }
    }

    private static string TripInstruction(FordTrip trip) =>
        trip.Loaded
            ? trip.FarBank
                ? "Teď odvez hlínu na druhý břeh řeky. Přejeď po mostě."
                : "Teď odvez náklad k bobrovi Bóďovi a vysyp ho do žlutého kruhu."
            : trip.Load switch
            {
                HaulLoad.Stones => "Jeď s vozíkem pro kamení do lomu.",
                HaulLoad.Soil => "Jeď s vozíkem pro hlínu.",
                _ => "Jeď s vozíkem pro písek.",
            };
}
