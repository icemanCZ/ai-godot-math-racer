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
/// The rules of <see cref="SaveRules"/> about what lies beyond the old valley: the post, the lighthouse, the dragon,
/// the balloon, the tracks, the sheep, the rally, the map, the shells, the ferry, the castle and the great railway.
/// </summary>
internal static class OutlandRules
{
    /// <summary>
    /// The post: a count of letters for each of the four keepers, at most one letter or one reply on its way (to a keeper,
    /// or to one of the four houses), the postman's and the keepers' panels with their own characters, and a question at a
    /// keeper only for the letter that is carried there, or for the lighthouse's trip.
    /// </summary>
    internal static bool HasInvalidPost(SaveData data)
    {
        var post = data.Post;
        if (
            post.Delivered is not { Length: Post.Keepers }
            || post.Delivered.Any(count => count is < 0 or > Post.Limit)
            || post.Carrying is < -1 or >= Post.Keepers
            || post.Reply is < -1 or >= 4
            || (post.Carrying >= 0 && post.Reply >= 0)
            || (data.State == GameState.PostStatus && !World.IsPostman(data.Nearby))
            || (data.State == GameState.KeeperStatus && !World.IsBorderKeeper(data.Nearby))
            || (World.IsPostman(data.Nearby) && (data.State == GameState.Challenge || data.Quiz.Handover))
            || (World.IsBorderKeeper(data.Nearby) && data.Quiz.Handover)
        )
            return true;
        if (data.State != GameState.Challenge || !World.IsBorderKeeper(data.Nearby))
            return false;
        // The question of the letter, the one before the lighthouse's trip, or the stone's loading question.
        bool letter = data.Quiz.Departure == 0 && post.Carrying == (int)World.BorderOf(data.Nearby);
        bool lighthouse =
            data.Nearby == World.FirstBorderKeeperId && (Game.Departure)data.Quiz.Departure == Game.Departure.Lighthouse
            || TripRules.AsksForLighthouse(data);
        // The question before the shepherd's sheep go into his fold.
        bool sheep = data.Nearby == World.ShepherdId && (Game.Departure)data.Quiz.Departure == Game.Departure.Sheep;
        return !letter && !lighthouse && !sheep && !AsksAsGuest(data);
    }

    /// <summary>The lighthouse has 0 to 8 deliveries, begun only after the first letter to the keeper of the sea.</summary>
    internal static bool HasInvalidLighthouse(SaveData data) =>
        data.Lighthouse.Stage is < 0 or > LighthousePlans.Trips
        || (data.Lighthouse.Stage > 0 && data.Post.Delivered is { Length: > 0 } delivered && delivered[0] == 0)
        || (data.State == GameState.LighthouseStatus && data.Nearby != World.FirstBorderKeeperId);

    /// <summary>
    /// The dragon's nest has 0 to 6 deliveries, his riddle 0 to 3 answers, given only once the nest is finished, and the
    /// player has flown on him only after the egg has hatched. His panel is his own, and a question he asks is the one
    /// before a trip, the loading question at the mountain slope or one of the riddle.
    /// </summary>
    internal static bool HasInvalidDragon(SaveData data)
    {
        var dragon = data.Dragon;
        if (
            dragon.Stage is < 0 or > DragonPlans.Trips
            || dragon.Riddle is < 0 or > DragonPlans.Riddles
            || (dragon.Riddle > 0 && dragon.Stage < DragonPlans.Trips)
            || (dragon.Flown && dragon.Riddle < DragonPlans.Riddles)
            || (data.State == GameState.DragonStatus && !World.IsDragon(data.Nearby))
        )
            return true;
        if (!World.IsDragon(data.Nearby))
            return false;
        if (data.Quiz.Handover || data.Quiz.Challenge is DeliveryChallenge)
            return true;
        if (data.State != GameState.Challenge)
            return data.State is not (GameState.Exploring or GameState.DragonStatus);
        // The question before a trip, the loading question at the slope, or an answer of the riddle.
        bool departure = (Game.Departure)data.Quiz.Departure == Game.Departure.Dragon;
        bool riddle =
            data.Quiz.Departure == 0
            && data.Quiz.Challenge is not LoadChallenge
            && dragon.Stage == DragonPlans.Trips
            && dragon.Riddle < DragonPlans.Riddles
            && data.Task.Trip is not { Kind: TripKinds.Dragon };
        return !departure && !riddle && !TripRules.AsksForDragon(data);
    }

    /// <summary>
    /// The balloon station: a count of flights within the limit and no more stars than its flights had, the
    /// balloonist's panel only beside her, and her one question, the one before a take-off, which hands nothing over.
    /// </summary>
    internal static bool HasInvalidBalloon(SaveData data)
    {
        var balloon = data.Balloon;
        if (
            balloon.Flights is < 0 or > BalloonPlans.FlightLimit
            || balloon.Stars < 0
            || balloon.Stars > (long)balloon.Flights * BalloonFlight.Stars
            || (data.State == GameState.BalloonStatus && !World.IsBalloonist(data.Nearby))
        )
            return true;
        if (!World.IsBalloonist(data.Nearby))
            return false;
        return data.Quiz.Handover
            || data.Quiz.Challenge is DeliveryChallenge or LoadChallenge
            || data.State is not (GameState.Exploring or GameState.BalloonStatus or GameState.Challenge)
            || (data.State == GameState.Challenge && data.Quiz.Departure != 0);
    }

    /// <summary>
    /// The book of tracks: no more animals found than it has, none before the keeper sent the player out, her book's
    /// panel only at the keeper of the forest, and the animal at the end of a trail asks only the question that coaxes
    /// it out, while a trail is followed, and shows only the coins it paid.
    /// </summary>
    internal static bool HasInvalidTracking(SaveData data)
    {
        var tracking = data.Tracking;
        int forester = World.FirstBorderKeeperId + (int)Border.Forest;
        if (
            tracking.Found is < 0 or > TrackingPlans.Animals
            || (tracking.Found > 0 && !tracking.Started)
            || (
                tracking.Started
                && data.Post.Delivered is { Length: > (int)Border.Forest } delivered
                && delivered[(int)Border.Forest] == 0
            )
            || (data.State == GameState.TrackingStatus && data.Nearby != forester)
        )
            return true;
        if (!World.IsTrackingAnimal(data.Nearby))
            return false;
        return data.Quiz.Handover
            || data.Quiz.Challenge is DeliveryChallenge or LoadChallenge
            || data.State is not (GameState.Exploring or GameState.Challenge or GameState.Reward)
            || (
                data.State == GameState.Challenge
                && (data.Quiz.Departure != 0 || !tracking.Started || tracking.Found >= TrackingPlans.Animals)
            )
            || (data.State == GameState.Reward && tracking.Found == 0);
    }

    /// <summary>
    /// The shepherd's sheep: known numbers, each at most once and never both at home and found, none of either before
    /// he sent the player out, and his fold's panel only at the shepherd.
    /// </summary>
    internal static bool HasInvalidSheep(SaveData data)
    {
        var sheep = data.Sheep;
        var all = sheep.Home.Concat(sheep.Following).ToArray();
        return all.Any(number => number is < 0 or >= SheepPlans.Sheep)
            || all.Distinct().Count() != all.Length
            || (!sheep.Started && all.Length > 0)
            || (data.State == GameState.ShepherdStatus && data.Nearby != World.ShepherdId);
    }

    /// <summary>
    /// The rally: a best time for each of its four stages, none longer than an hour, none before the post brought the
    /// ferrywoman her first letter, and her card's panel only at the ferrywoman.
    /// </summary>
    internal static bool HasInvalidRally(SaveData data)
    {
        var best = data.Rally.Best;
        return best.Length != RallyPlans.Stages
            || best.Any(tenths => tenths is < 0 or > RallyPlans.LongestTenths)
            || (
                best.Any(tenths => tenths > 0)
                && data.Post.Delivered is { Length: > (int)Border.River } delivered
                && delivered[(int)Border.River] == 0
            )
            || (data.State == GameState.RallyStatus && data.Nearby != World.FerrywomanId);
    }

    /// <summary>
    /// The owl's map: known wonders, each seen and written in at most once, written in only once seen and once she has
    /// shown her map, her arrows only to a wonder not yet seen, her panel only beside her, and her one question, the one
    /// before the wonders seen go into her map.
    /// </summary>
    internal static bool HasInvalidExplorer(SaveData data)
    {
        var map = data.Explorer;
        if (
            map.Spotted.Concat(map.Recorded).Any(kind => kind is < 0 or >= ExplorerPlans.Landmarks)
            || map.Spotted.Distinct().Count() != map.Spotted.Length
            || map.Recorded.Distinct().Count() != map.Recorded.Length
            || map.Recorded.Any(kind => !map.Spotted.Contains(kind))
            || (!map.Started && map.Recorded.Length > 0)
            || map.Guided < -1
            || map.Guided >= ExplorerPlans.Landmarks
            || (map.Guided >= 0 && map.Spotted.Contains(map.Guided))
            || (data.State == GameState.ExplorerStatus && !World.IsCartographer(data.Nearby))
        )
            return true;
        if (!World.IsCartographer(data.Nearby))
            return false;
        return data.Quiz.Handover
            || data.Quiz.Challenge is DeliveryChallenge or LoadChallenge
            || data.State
                is not (GameState.Exploring or GameState.ExplorerStatus or GameState.Challenge or GameState.Reward)
            || (
                data.State == GameState.Challenge
                && (Game.Departure)data.Quiz.Departure != Game.Departure.Explorer
                && !AsksAsGuest(data)
            );
    }

    /// <summary>
    /// The shells: a tide that is not negative, each of its shells picked up at most once, and a count that is not
    /// negative for each of the kinds.
    /// </summary>
    internal static bool HasInvalidShells(SaveData data)
    {
        var shells = data.Shells;
        return shells.Tide < 0
            || shells.Picked.Any(shell => shell is < 0 or >= ShellPlans.PerTide)
            || shells.Picked.Distinct().Count() != shells.Picked.Length
            || shells.Kinds.Length != ShellPlans.Kinds
            || shells.Kinds.Any(count => count is < 0 or > ShellPlans.MostOfAKind);
    }

    /// <summary>
    /// The ferry: a landing of 0 to 5 deliveries, crossings that are not negative, each bird of the last crossing seen at
    /// most once and none before the first crossing, a count that is not negative for each kind and that holds the birds
    /// of the crossing seen, the heron's panel only beside her, and her questions only those before a trailer trip for the
    /// landing, before the goods missing for it, or before a crossing.
    /// </summary>
    internal static bool HasInvalidFerry(SaveData data)
    {
        var ferry = data.Ferry;
        if (
            ferry.Stage is < 0 or > FerryPlans.Trips
            || ferry.Visit is < 0 or > FerryPlans.MostVisits
            || ferry.Spotted.Any(bird => bird is < 0 or >= FerryPlans.PerVisit)
            || ferry.Spotted.Distinct().Count() != ferry.Spotted.Length
            || (ferry.Visit == 0 && ferry.Spotted.Length > 0)
            || (ferry.Visit > 0 && ferry.Stage < FerryPlans.Trips)
            || ferry.Kinds.Length != FerryPlans.Kinds
            || ferry.Kinds.Any(count => count is < 0 or > FerryPlans.MostOfAKind)
            || ferry.Kinds.Sum(count => (long)count) < ferry.Spotted.Length
            || (data.State == GameState.FerryStatus && !World.IsBirdwatcher(data.Nearby))
        )
            return true;
        if (!World.IsBirdwatcher(data.Nearby))
            return false;
        return data.Quiz.Handover
            || data.Quiz.Challenge is DeliveryChallenge
            || data.State
                is not (GameState.Exploring or GameState.FerryStatus or GameState.Challenge or GameState.Reward)
            || (
                data.State == GameState.Challenge
                && data.Quiz.Departure != 0
                && (Game.Departure)data.Quiz.Departure
                    is not (Game.Departure.Ferry or Game.Departure.FerrySail or Game.Departure.Missing)
            );
    }

    /// <summary>
    /// The castle: 0 to 16 deliveries, beacons of known borders and guests of known chairs, each at most once and none
    /// before the castle stands, the feast only with a guest at the table, the princess's panel only beside her, and her
    /// questions only those before a trailer trip, before the goods missing for it, or the stone's loading question.
    /// </summary>
    internal static bool HasInvalidCastle(SaveData data)
    {
        var castle = data.Castle;
        if (
            castle.Stage is < 0 or > CastlePlans.Trips
            || castle.Beacons.Any(border => border is < 0 or >= CastlePlans.Beacons)
            || castle.Beacons.Distinct().Count() != castle.Beacons.Length
            || castle.Guests.Any(chair => chair is < 0 or >= CastlePlans.Guests)
            || castle.Guests.Distinct().Count() != castle.Guests.Length
            || (castle.Stage < CastlePlans.Trips && (castle.Beacons.Length > 0 || castle.Guests.Length > 0))
            || (castle.Feast && castle.Guests.Length == 0)
            || (data.State == GameState.CastleStatus && !World.IsCastellan(data.Nearby))
        )
            return true;
        if (!World.IsCastellan(data.Nearby))
            return false;
        return data.Quiz.Handover
            || data.Quiz.Challenge is DeliveryChallenge
            || data.State
                is not (GameState.Exploring or GameState.CastleStatus or GameState.Challenge or GameState.Reward)
            || (
                data.State == GameState.Challenge
                && data.Quiz.Departure != 0
                && (Game.Departure)data.Quiz.Departure is not (Game.Departure.Castle or Game.Departure.Missing)
            );
    }

    /// <summary>
    /// The great line: 0 to 17 deliveries and 0 to 4 rails in the forge, none before the third stretch of the first arm
    /// wants them, the driver's panel only beside him, and his questions only those before a trailer trip or a ride, before
    /// the goods missing for it, or the loading question of the rails.
    /// </summary>
    internal static bool HasInvalidMainLine(SaveData data)
    {
        var line = data.MainLine;
        if (
            line.Stage is < 0 or > MainLinePlans.Trips
            || line.Rails is < 0 or > MainLinePlans.RailCapacity
            || (line.Rails > 0 && line.Stage < MainLinePlans.RailStep(0))
            || (data.State == GameState.MainLineStatus && !World.IsEngineer(data.Nearby))
        )
            return true;
        if (!World.IsEngineer(data.Nearby))
            return false;
        return data.Quiz.Handover
            || data.Quiz.Challenge is DeliveryChallenge
            || data.State
                is not (GameState.Exploring or GameState.MainLineStatus or GameState.Challenge or GameState.Reward)
            || (
                data.State == GameState.Challenge
                && data.Quiz.Departure != 0
                && (Game.Departure)data.Quiz.Departure
                    is not (Game.Departure.MainLine or Game.Departure.MainLineRide or Game.Departure.Missing)
            );
    }

    /// <summary>
    /// A guest of the castle's feast asks the question at home before getting on the trailer's bench: a question that is
    /// not of the loading kind, while the trailer of that guest's trip stands empty.
    /// </summary>
    internal static bool AsksAsGuest(SaveData data) =>
        data.State == GameState.Challenge
        && data.Quiz.Departure == 0
        && data.Quiz.Challenge is not (LoadChallenge or DeliveryChallenge)
        && !data.Quiz.Handover
        && data.Task.Trip is { Kind: TripKinds.CastleGuest, Loaded: false } trip
        && trip.Index is >= 0 and < CastlePlans.Guests
        && World.CastleGuestId(trip.Index) == data.Nearby;
}
