using System.Collections.Generic;
using System.Linq;
using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Ui;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The owl's map of the valley (<c>docs/EXPLORER_PLAN.md</c>). Sova kartografka Mapka has her stand beside the road where
/// it first leaves the old valley; her panel (state <see cref="GameState.ExplorerStatus"/>) shows her map of the whole
/// valley with the eight wonders of the land beyond the old valley on it: a question mark where one is still hidden, its
/// picture with an exclamation mark once the car has seen it, and its picture with her flag once it is written in. The
/// car sees a wonder by driving past it, from the very start. Back at the owl, a question writes every wonder seen into
/// the map: coins for each, a bonus for the full map and the little owl Houkalka. Her other button lights the road arrows
/// to the nearest wonder not yet seen. What she has shown, seen, written in and leads to is saved as
/// <c>SaveData.Explorer</c>.
/// </summary>
public partial class Game
{
    private ExplorerLog? explorerLog;
    private IReadOnlyList<Landmark>? explorerLandmarks;
    private ExplorerTrip? explorerTrip;
    private ValleyMapData? valleyMap;

    /// <summary>The owl's map of this valley, made anew when the valley is.</summary>
    internal ExplorerLog Explorer
    {
        get
        {
            if (explorerLog == null || !ReferenceEquals(explorerLandmarks, world.Landmarks))
            {
                explorerLog = new ExplorerLog();
                explorerLandmarks = world.Landmarks;
                valleyMap = null;
            }
            return explorerLog;
        }
    }

    /// <summary>The valley has the owl's stand and all eight wonders.</summary>
    private bool ExplorerWanted => world.Cartographer != null && world.LandmarkModels != null;

    /// <summary>The owl shows her mark until she has shown her map, and while a wonder seen is not yet in it.</summary>
    private bool CartographerAwaits => ExplorerWanted && (!Explorer.Started || Explorer.Unrecorded.Length > 0);

    /// <summary>The owl's panel: her map, and the buttons that write the wonders seen into it or lead to the next.</summary>
    internal void OpenCartographer(Character owl)
    {
        if (owl.Role is not CartographerRole || !ExplorerWanted)
            return;
        ClearChallenge();
        var log = Explorer;
        bool first = !log.Started;
        log.Start();
        EnterInteraction(GameState.ExplorerStatus, owl);
        ShowCartographer();
        int seen = log.Unrecorded.Length;
        Speak(
            first ? owl.Greeting + "\n" + Texts.ExplorerWanted
                : log.Complete ? Texts.ExplorerMapFull
                : seen > 0 ? Texts.ExplorerSeen(seen)
                : Texts.ExplorerStillHidden(ExplorerPlans.Landmarks - log.Spotted.Length),
            owl
        );
        UpdatePostGuides();
    }

    private void ShowCartographer()
    {
        if (nearby is not { Role: CartographerRole } owl || !ExplorerWanted)
            return;
        var log = Explorer;
        var status = Enumerable
            .Range(0, ExplorerPlans.Landmarks)
            .Select(i =>
                log.IsRecorded(i) ? LandmarkStatus.Recorded
                : log.IsSpotted(i) ? LandmarkStatus.Seen
                : LandmarkStatus.Hidden
            )
            .ToArray();
        ui.CartographerPanel(
            owl,
            ValleyMap,
            status,
            new Vector2(vehicle.Position.X, vehicle.Position.Z),
            log.Guided,
            world.AllPets.FirstOrDefault(pet => pet.Kind == CollectorPetKind.Owlet),
            log.Unrecorded.Length > 0,
            log.Spotted.Length < ExplorerPlans.Landmarks
        );
    }

    /// <summary>What the owl's map shows of this valley, worked out once: its roads, waters, village and wonders.</summary>
    private ValleyMapData ValleyMap
    {
        get
        {
            _ = Explorer;
            if (valleyMap != null)
                return valleyMap;
            static Vector2 Flat(Vector3 p) => new(p.X, p.Z);
            var (river, borderRiver, coast) = world.MapWaters();
            valleyMap = new ValleyMapData(
                WorldLayout.Plan.Routes.Select(route => route.Select(Flat).ToArray()).ToArray(),
                world.OutlandRoutePoints.Select(route => route.Select(Flat).ToArray()).ToArray(),
                river,
                borderRiver,
                coast,
                WorldLayout.Plan.Side,
                Flat(world.Village.Center),
                world.Cartographer is { } owl ? Flat(owl.Position) : Vector2.Zero,
                world.Landmarks.Select(landmark => Flat(landmark.Center)).ToArray()
            );
            return valleyMap;
        }
    }

    internal void BeginExplorerRecord() => BeginExplorerRecord(false);

    /// <summary>
    /// The owl's first button: a question first, then every wonder the car has seen goes into her map, with her flag
    /// beside it; coins for each, and for the last of the eight a bonus and the little owl.
    /// </summary>
    private void BeginExplorerRecord(bool quizzed)
    {
        if (
            State != GameState.ExplorerStatus
            || nearby is not { Role: CartographerRole } owl
            || world.LandmarkModels is not { } models
            || Explorer.Unrecorded.Length == 0
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Explorer);
            Speak(Texts.ExplorerRecordQuestion, owl);
            return;
        }
        ClearChallenge();
        var log = Explorer;
        var added = log.Record();
        models.SetRecorded(log.RecordedSet);
        owl.Celebrate();
        bool full = log.Complete;
        int coins = added.Length * ExplorerPlans.CoinsPerLandmark + (full ? ExplorerPlans.MapCoins : 0);
        string message = Texts.ExplorerRecorded(
            added.Select(ExplorerPlans.Name).ToArray(),
            log.RecordedCount,
            ExplorerPlans.Landmarks
        );
        if (full)
            message += "\n" + Texts.ExplorerMapDone(ExplorerPlans.MapCoins);
        CelebrateAt(owl.Position + Vector3.Up * 2, WithPets(message, EarnPets()));
        Speak(full ? Texts.ExplorerMapFull : Texts.ExplorerThanks(ExplorerPlans.Landmarks - log.RecordedCount), owl);
        UpdatePostGuides();
        PayReward(coins);
    }

    /// <summary>The owl's other button: the road arrows light the way to the nearest wonder the car has not seen.</summary>
    internal void BeginExplorerGuide()
    {
        if (State != GameState.ExplorerStatus || nearby is not { Role: CartographerRole } owl || !ExplorerWanted)
            return;
        int target = NearestHiddenLandmark(vehicle.Position);
        if (target < 0)
            return;
        Explorer.Guide(target);
        owl.Celebrate();
        CloseInteraction();
        UpdateExplorerTrip();
        Speak(Texts.ExplorerGo(ExplorerPlans.Name(target)), owl);
        SaveProgress();
    }

    /// <summary>The wonder not yet seen nearest a point, or -1 when every one has been seen.</summary>
    internal int NearestHiddenLandmark(Vector3 from)
    {
        int best = -1;
        float nearest = float.MaxValue;
        foreach (var landmark in world.Landmarks)
        {
            if (Explorer.IsSpotted(landmark.Kind))
                continue;
            float d = new Vector2(landmark.Center.X - from.X, landmark.Center.Z - from.Z).Length();
            if (d < nearest)
            {
                nearest = d;
                best = landmark.Kind;
            }
        }
        return best;
    }

    /// <summary>A question asked by the owl that is not before her map: there is none, so the panel closes.</summary>
    internal void CompleteCartographerQuiz(Character owl) => FinishInteraction();

    /// <summary>The road arrows lead to the wonder the owl showed, and are taken away once there is none.</summary>
    private void UpdateExplorerTrip()
    {
        var log = Explorer;
        var target = log.Guided >= 0 && log.Guided < world.Landmarks.Count ? world.Landmarks[log.Guided] : null;
        if (explorerTrip != null && explorerTrip.Landmark != target)
        {
            explorerTrip.GetParent()?.RemoveChild(explorerTrip);
            explorerTrip.QueueFree();
            explorerTrip = null;
        }
        if (target != null && explorerTrip == null)
        {
            explorerTrip = new ExplorerTrip(world, vehicle, target);
            world.AddChild(explorerTrip);
        }
    }

    /// <summary>
    /// The car sees every wonder it drives past; the arrows to the wonder the owl showed move on, and the corner panel
    /// shows which wonder they lead to and how far it is.
    /// </summary>
    private void TickExplorer(double delta)
    {
        if (State is GameState.Title || !ExplorerWanted)
        {
            ui.ExplorerHud(false, vehicle, Vector3.Zero, -1, 0, "");
            return;
        }
        var log = Explorer;
        bool exploring = State == GameState.Exploring;
        if (exploring)
        {
            var car = vehicle.Position;
            foreach (var landmark in world.Landmarks)
            {
                if (log.IsSpotted(landmark.Kind))
                    continue;
                float reach = ExplorerPlans.Radius(landmark.Kind) + ExplorerPlans.SpotReach;
                if (new Vector2(landmark.Center.X - car.X, landmark.Center.Z - car.Z).Length() > reach)
                    continue;
                log.Spot(landmark.Kind);
                string message = Texts.ExplorerSpotted(ExplorerPlans.Name(landmark.Kind));
                CelebrateAt(landmark.Center + Vector3.Up * 4, message);
                Speak(message);
                UpdateExplorerTrip();
                UpdatePostGuides();
                SaveProgress();
                break;
            }
        }
        bool free = exploring && Delivery == null && Haul == null && letterTrip == null;
        explorerTrip?.Tick(delta, free);
        if (explorerTrip == null || !free)
        {
            ui.ExplorerHud(false, vehicle, Vector3.Zero, -1, 0, "");
            return;
        }
        var goal = explorerTrip.Landmark;
        float distance = new Vector2(goal.Center.X - vehicle.Position.X, goal.Center.Z - vehicle.Position.Z).Length();
        ui.ExplorerHud(true, vehicle, goal.Center, goal.Kind, log.RecordedCount, $"{distance:0} m");
    }

    /// <summary>Whether the corner panel of the map shows, for the checks.</summary>
    internal bool ExplorerHudShown => ui.ExplorerHudShown;

    /// <summary>Whether the road arrows lead to a wonder now, for the checks.</summary>
    internal bool ExplorerGuiding => explorerTrip != null;

    /// <summary>Puts the owl's map back from a save, before the pets are, so that the little owl knows whether she is owned.</summary>
    private void RestoreExplorer(bool started, int[] spotted, int[] recorded, int guided)
    {
        Explorer.Restore(started, spotted, recorded, guided);
        world.LandmarkModels?.SetRecorded(Explorer.RecordedSet);
        UpdateExplorerTrip();
        UpdatePostGuides();
    }
}
