using System;
using System.Linq;
using System.Text.Json;
using MathRacer.App;
using MathRacer.Buildings;
using MathRacer.Garage;
using MathRacer.Quizzes;
using MathRacer.Settlement;
using MathRacer.Tasks;
using MathRacer.Valley;
using MathRacer.Vehicles;

namespace MathRacer.Saves;

/// <summary>
/// The rules of <see cref="SaveRules"/> about the trailer trip under way: its target must still want a delivery, the
/// character being talked to must be one who can ask for it, and the works, hauls and expedition cargo must agree
/// with it.
/// </summary>
internal static class TripRules
{
    /// <summary>The cargo and the characters who may ask about it for the active trailer trip, or none without one.</summary>
    internal static (HaulLoad Cargo, int[] Askers, bool Loaded) ActiveTripAsker(SaveData data)
    {
        if (data.Task.Trip is not { } trip)
            return (default, Array.Empty<int>(), false);
        switch (trip.Kind)
        {
            case TripKinds.Build
                when trip.Index is >= 0 and < BuildPlans.Count
                    && data.Buildings.Stages is { Length: BuildPlans.Count } buildStages
                    && buildStages[trip.Index] is >= 0 and < BuildPlans.Trips:
            {
                var cargo = BuildPlans.For(trip.Index)[buildStages[trip.Index]].Cargo;
                return (cargo, World.AskerIds(cargo, trip.Index), trip.Loaded);
            }
            case TripKinds.Supply when trip.Index is >= 0 and < WorksCatalog.Total:
            {
                var cargo = WorksCatalog.All[trip.Index].Input;
                // A sand pit without a worker is loaded on the glassworks' behalf, so its worker asks the question.
                var askers = World.AskerIds(cargo, 0);
                if (trip.Index == WorksCatalog.BreadIndex)
                {
                    // The bakery took the farmer's wheat itself before the mill ground flour for it, and still does in a
                    // valley without the mill: the question may be the miller's about flour or the farmer's about wheat.
                    if (data.Quiz.Challenge is LoadChallenge { Cargo: HaulLoad.Wheat })
                        cargo = HaulLoad.Wheat;
                    askers = askers.Concat(World.AskerIds(HaulLoad.Wheat, 0)).ToArray();
                }
                return (
                    cargo,
                    trip.Index == WorksCatalog.GlassIndex ? askers.Append(World.GlassworksId).ToArray() : askers,
                    trip.Loaded
                );
            }
            case TripKinds.Plot
                when trip.Index is >= 0 and < PlotPlans.Count
                    && data.Buildings.PlotStages is { Length: PlotPlans.Count } plotStages
                    && plotStages[trip.Index] is >= 0 and < PlotPlans.Trips:
            {
                var cargo = PlotPlans.CargoOf(plotStages[trip.Index]);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Playground when trip.Index == 0 && data.Playground.Stage is >= 0 and < PlaygroundPlans.Trips:
            {
                var cargo = PlaygroundPlans.CargoOf(data.Playground.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Railway when trip.Index == 0 && data.Railway.Stage is >= 0 and < RailwayPlans.Trips:
            {
                var cargo = RailwayPlans.CargoOf(data.Railway.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Zoo when trip.Index == 0 && data.Zoo.Stage is >= 0 and < ZooPlans.Trips:
            {
                var cargo = ZooPlans.CargoOf(data.Zoo.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Hunter when trip.Index == 0 && data.Hunt.Hut is >= 0 and < HutPlans.Trips:
            {
                var cargo = HutPlans.CargoOf(data.Hunt.Hut);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Lighthouse when trip.Index == 0 && data.Lighthouse.Stage is >= 0 and < LighthousePlans.Trips:
            {
                // The stone is loaded at the coast quarry, which has no worker: the keeper of the sea asks the question.
                var cargo = LighthousePlans.CargoOf(data.Lighthouse.Stage);
                return (
                    cargo,
                    data.Lighthouse.Stage < LighthousePlans.StoneTrips
                        ? new[] { World.FirstBorderKeeperId }
                        : World.AskerIds(cargo, 0),
                    trip.Loaded
                );
            }
            case TripKinds.Ferry when trip.Index == 0 && data.Ferry.Stage is >= 0 and < FerryPlans.Trips:
            {
                var cargo = FerryPlans.CargoOf(data.Ferry.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.Castle when trip.Index == 0 && data.Castle.Stage is >= 0 and < CastlePlans.Trips:
            {
                // The stone is loaded by the sea or in the mountains, which have no worker: the princess asks.
                var cargo = CastlePlans.CargoOf(data.Castle.Stage);
                return (
                    cargo,
                    cargo == HaulLoad.Stones ? new[] { World.CastellanId } : World.AskerIds(cargo, 0),
                    trip.Loaded
                );
            }
            case TripKinds.MainLine when trip.Index == 0 && data.MainLine.Stage is >= 0 and < MainLinePlans.Trips:
            {
                // The rails are loaded at the forge, which has no worker: the engine driver asks the question.
                var cargo = MainLinePlans.CargoOf(data.MainLine.Stage);
                return (
                    cargo,
                    cargo == HaulLoad.Rails ? new[] { World.EngineerId } : World.AskerIds(cargo, 0),
                    trip.Loaded
                );
            }
            case TripKinds.MainLineForge when trip.Index == 0:
                return (HaulLoad.Stones, World.AskerIds(HaulLoad.Stones, 0), trip.Loaded);
            case TripKinds.Ship when trip.Index == 0 && data.Ship.Stage is >= 0 and < ShipPlans.Trips:
            {
                // The anchor is loaded at the forge, which has no worker: the captain asks the question.
                var cargo = ShipPlans.CargoOf(data.Ship.Stage);
                return (cargo, World.AskerIds(cargo, 0), trip.Loaded);
            }
            case TripKinds.ShipForge when trip.Index == 0:
                return (HaulLoad.Stones, World.AskerIds(HaulLoad.Stones, 0), trip.Loaded);
            case TripKinds.CastleBeacon when trip.Index is >= 0 and < CastlePlans.Beacons:
                return (HaulLoad.Logs, World.AskerIds(HaulLoad.Logs, 0), trip.Loaded);
            case TripKinds.CastleGuest when trip.Index is >= 0 and < CastlePlans.Guests:
                // The guest asks at home before getting on the bench.
                return (HaulLoad.Guest, new[] { World.CastleGuestId(trip.Index) }, trip.Loaded);
            case TripKinds.Dragon when trip.Index == 0 && data.Dragon.Stage is >= 0 and < DragonPlans.Trips:
                // Everything is loaded at the mountain slope, which has no worker: the dragon asks the question.
                return (DragonPlans.CargoOf(data.Dragon.Stage), new[] { World.DragonId }, trip.Loaded);
            case TripKinds.Mill when trip.Index == 0:
                return (HaulLoad.Wheat, World.AskerIds(HaulLoad.Wheat, 0), trip.Loaded);
            case TripKinds.Glassmaker when trip.Index == 0:
                return (HaulLoad.Glass, World.AskerIds(HaulLoad.Glass, 0), trip.Loaded);
            case TripKinds.House
                when SaveRules.SavedTask(data, trip.Index) is { } kind && Village.MaterialOf(kind) is { } goods:
                return (goods, World.AskerIds(goods, 0), trip.Loaded);
            case TripKinds.House when SaveRules.IsGlassOrder(data, trip.Index):
                return (HaulLoad.Glass, World.AskerIds(HaulLoad.Glass, 0), trip.Loaded);
            case TripKinds.House when SaveRules.IsBreadOrder(data, trip.Index):
                return (HaulLoad.Bread, World.AskerIds(HaulLoad.Bread, 0), trip.Loaded);
        }
        return (default, Array.Empty<int>(), false);
    }

    /// <summary>
    /// A trailer trip fetches the next delivery of its target, so that target must exist and be unfinished: a
    /// building or plot with deliveries left, a works that is not full, a house that asks for planks, bricks, glasses or
    /// bread, the mill with room for wheat or the lamp maker. It never runs together with an expedition.
    /// </summary>
    internal static bool HasInvalidTrip(SaveData data)
    {
        if (data.Task.Trip is not { } trip)
            return false;
        if (data.Task.Sender >= 0)
            return true;
        return trip.Kind switch
        {
            TripKinds.Build => trip.Index is < 0 or >= BuildPlans.Count
                || data.Buildings.Stages![trip.Index] >= BuildPlans.Trips,
            TripKinds.Supply => trip.Index is < 0 or >= WorksCatalog.Total
                || (
                    trip.Index == WorksCatalog.GlassIndex ? data.Buildings.GlassStock
                    : trip.Index == WorksCatalog.BreadIndex ? data.Buildings.BreadStock
                    : data.Buildings.WorksStock[trip.Index]
                ) >= Works.Capacity,
            TripKinds.House => trip.Index is < 0 or >= 4
                || (
                    SaveRules.SavedTask(data, trip.Index) is not (VillageTaskKind.Planks or VillageTaskKind.Bricks)
                    && !SaveRules.IsGlassOrder(data, trip.Index)
                    && !SaveRules.IsBreadOrder(data, trip.Index)
                ),
            TripKinds.Plot => trip.Index is < 0 or >= PlotPlans.Count
                || data.Buildings.PlotStages[trip.Index] >= PlotPlans.Trips,
            TripKinds.Playground => trip.Index != 0 || data.Playground.Stage >= PlaygroundPlans.Trips,
            TripKinds.Railway => trip.Index != 0 || data.Railway.Stage >= RailwayPlans.Trips,
            TripKinds.Zoo => trip.Index != 0 || data.Zoo.Stage >= ZooPlans.Trips,
            TripKinds.Hunter => trip.Index != 0 || data.Hunt.Hut >= HutPlans.Trips,
            // The keeper of the sea wants the lighthouse only after the first letter.
            TripKinds.Lighthouse => trip.Index != 0
                || data.Lighthouse.Stage >= LighthousePlans.Trips
                || data.Post.Delivered[0] == 0,
            TripKinds.Dragon => trip.Index != 0 || data.Dragon.Stage >= DragonPlans.Trips,
            TripKinds.Ferry => trip.Index != 0 || data.Ferry.Stage >= FerryPlans.Trips,
            TripKinds.Castle => trip.Index != 0 || data.Castle.Stage >= CastlePlans.Trips,
            TripKinds.MainLine => trip.Index != 0 || data.MainLine.Stage >= MainLinePlans.Trips,
            // Stone for the forge is wanted when the next delivery is rails and it has none.
            TripKinds.MainLineForge => trip.Index != 0
                || data.MainLine.Stage >= MainLinePlans.Trips
                || data.MainLine.Stage < MainLinePlans.RailStep(0),
            TripKinds.Ship => trip.Index != 0 || data.Ship.Stage >= ShipPlans.Trips,
            // Stone for the forge is wanted when the next delivery is the anchor and it is not forged yet.
            TripKinds.ShipForge => trip.Index != 0 || data.Ship.Stage != ShipPlans.AnchorStep || data.Ship.Anchor,
            // The beacons and the guests come once the castle stands, each one once.
            TripKinds.CastleBeacon => trip.Index is < 0 or >= CastlePlans.Beacons
                || data.Castle.Stage < CastlePlans.Trips
                || data.Castle.Beacons.Contains(trip.Index),
            TripKinds.CastleGuest => trip.Index is < 0 or >= CastlePlans.Guests
                || data.Castle.Stage < CastlePlans.Trips
                || data.Castle.Guests.Contains(trip.Index),
            // Wheat goes to the mill once it is finished, until its flour fills the yard.
            TripKinds.Mill => trip.Index != 0
                || !SaveRules.MillFinished(data)
                || data.Buildings.FlourStock >= FlourMill.Capacity,
            // The lamp maker takes glasses again and again: there is only the one of him.
            TripKinds.Glassmaker => trip.Index != 0,
            _ => true,
        };
    }

    /// <summary>
    /// Builders, works workers and source workers show a panel of their own and, during a trailer trip, ask the
    /// loading question. A question can only be open for the trip's own asker, and only before the load is aboard.
    /// </summary>
    /// <summary>
    /// The keeper of the sea asks the stone's loading question: a question of the loading kind, or any question while the
    /// trailer of a lighthouse trip stands empty (an arithmetic turn becomes the loading question, the others do not).
    /// </summary>
    internal static bool AsksForLighthouse(SaveData data) =>
        data.Nearby == World.FirstBorderKeeperId
        && (
            data.Quiz.Challenge is LoadChallenge
            || data.State == GameState.Challenge
                && data.Quiz.Departure == 0
                && data.Task.Trip is { Kind: TripKinds.Lighthouse, Loaded: false }
        );

    /// <summary>
    /// The dragon asks the loading question at the mountain slope: a question of the loading kind, or any question while
    /// the trailer of his trip stands empty (an arithmetic turn becomes the loading question, the others do not).
    /// </summary>
    internal static bool AsksForDragon(SaveData data) =>
        World.IsDragon(data.Nearby)
        && (
            data.Quiz.Challenge is LoadChallenge
            || data.State == GameState.Challenge
                && data.Quiz.Departure == 0
                && data.Task.Trip is { Kind: TripKinds.Dragon, Loaded: false }
        );

    internal static bool HasInvalidAsker(SaveData data)
    {
        // The dragon's panel, the question before his trip and his riddle are looked after by the dragon's rules.
        if (World.IsDragon(data.Nearby) && !AsksForDragon(data))
            return data.Quiz.Challenge is LoadChallenge;
        // The keepers' panels and the question of a letter are looked after by the rules of the post; only the keeper of
        // the sea asks a loading question, the one of the stone for the lighthouse.
        if (World.IsBorderKeeper(data.Nearby) && !AsksForLighthouse(data))
            return data.Quiz.Challenge is LoadChallenge;
        GameState? panel =
            World.IsBuilder(data.Nearby) ? GameState.BuildStatus
            : data.Nearby is >= World.FirstWorksId and <= World.LastWorksId or World.GlassworksId or World.BakeryId
                ? GameState.WorksStatus
            : data.Nearby is >= World.FirstSourceId and <= World.LastSourceId or World.FieldFarmerId
                ? GameState.SourceStatus
            : World.IsPlotResident(data.Nearby) ? GameState.PlotStatus
            : World.IsPlaygroundHelper(data.Nearby) ? GameState.PlaygroundStatus
            : World.IsStationHelper(data.Nearby) ? GameState.RailwayStatus
            : World.IsZooKeeper(data.Nearby) ? GameState.ZooStatus
            : World.IsHunter(data.Nearby) ? GameState.HunterStatus
            : data.Nearby == World.FirstBorderKeeperId ? GameState.LighthouseStatus
            : World.IsDragon(data.Nearby) ? GameState.DragonStatus
            : World.IsBirdwatcher(data.Nearby) ? GameState.FerryStatus
            : World.IsCastellan(data.Nearby) ? GameState.CastleStatus
            : World.IsEngineer(data.Nearby) ? GameState.MainLineStatus
            : World.IsCaptain(data.Nearby) ? GameState.ShipStatus
            : null;
        if (panel == null)
        {
            // The panels belong to their characters, and a loading question needs one of them to ask it.
            return data.State
                    is GameState.BuildStatus
                        or GameState.WorksStatus
                        or GameState.SourceStatus
                        or GameState.PlotStatus
                        or GameState.PlaygroundStatus
                        or GameState.RailwayStatus
                        or GameState.ZooStatus
                        or GameState.HunterStatus
                        or GameState.LighthouseStatus
                        or GameState.DragonStatus
                        or GameState.FerryStatus
                        or GameState.CastleStatus
                        or GameState.MainLineStatus
                        or GameState.ShipStatus
                || data.Quiz.Challenge is LoadChallenge;
        }
        if (data.State is not (GameState.Exploring or GameState.Challenge) && data.State != panel)
            return true;
        var (cargo, askers, loaded) = ActiveTripAsker(data);
        // A quiz before a trip is asked by the character who offers it, whatever trip is under way meanwhile.
        if (data.State == GameState.Challenge && data.Quiz.Departure == 0)
        {
            if (
                !askers.Contains(data.Nearby)
                || loaded
                || data.Quiz.Handover
                || data.Quiz.Challenge is DeliveryChallenge
            )
                return true;
        }
        return data.Quiz.Challenge is LoadChallenge load
            && (!load.IsValid() || askers.Length == 0 || load.Cargo != cargo);
    }

    /// <summary>
    /// The stock of the three works, which never exceeds the yard.
    /// </summary>
    internal static bool HasInvalidWorksState(SaveData data) =>
        data.Buildings.WorksStock == null
        || data.Buildings.WorksStock.Length != WorksCatalog.Count
        || data.Buildings.WorksStock.Any(stock => stock < 0 || stock > Works.Capacity);

    /// <summary>The counts of hauled loads: one for each raw material and each works, none negative or beyond the limit.</summary>
    internal static bool HasInvalidHauls(SaveData data) =>
        data.Hauls.Raw == null
        || data.Hauls.Raw.Length != HaulTally.RawKinds
        || data.Hauls.Works == null
        || data.Hauls.Works.Length != HaulTally.WorksKinds
        || data.Hauls.Raw.Concat(data.Hauls.Works)
            .Concat(new[] { data.Hauls.Wheat, data.Hauls.Baked })
            .Any(count => count < 0 || count > HaulTally.Limit);

    internal static bool HasInvalidPickupCount(SaveData data) =>
        (
            data.Task.Sender >= 0
            && data.Task.Picked.Length
                != (
                    data.Task.Sender == World.SorcererId ? 6
                    : data.Task.Sender >= World.FarmerId && data.Task.Sender < World.FirstTraderId ? 1
                    : data.Task.Collection ? 3
                    : 0
                )
        );
}
