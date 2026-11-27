using Godot;
using MathRacer.Characters;
using MathRacer.Tasks;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The post to the four corners of the valley (<c>docs/POST_AND_LIGHTHOUSE_PLAN.md</c>, phase 2). The postman in front
/// of the post office (state <see cref="GameState.PostStatus"/>) shows the album of stamps and, with a green check mark,
/// gives out one letter for a keeper of a border: the keeper who has had the fewest. An envelope in the corner and the
/// road arrows lead there; the keeper asks a question, and a right answer puts the keeper's stamp in the album, pays coins
/// and gives a reply for a house of the village, which brings more coins. The next letter waits at the postman. A stamp
/// from every keeper earns the pigeon. Saved as <c>SaveData.Post</c>; the keepers' own panel is
/// <see cref="GameState.KeeperStatus"/>.
/// </summary>
public partial class Game
{
    private LetterTrip? letterTrip;

    internal Post Post => world.Post;

    /// <summary>The character the car is beside, for the checks.</summary>
    internal Character? NearbyActor => nearby;

    /// <summary>The character the letter or reply being carried is for, or null.</summary>
    private Character? LetterAddressee =>
        Post.Carrying >= 0 ? world.Keepers[Post.Carrying]
        : Post.Reply >= 0 && Post.Reply < world.Village.Residents.Count ? world.Village.Residents[Post.Reply]
        : null;

    internal void OpenPostman(Character postman)
    {
        if (postman.Role is not PostmanRole)
            return;
        ClearChallenge();
        EnterInteraction(GameState.PostStatus, postman);
        ShowPost();
        Speak(
            LetterAddressee is { } addressee ? Texts.PostStillCarrying(addressee.DisplayName)
                : Post.Total == 0 ? postman.Greeting
                : Texts.PostAnotherLetter,
            postman
        );
    }

    private void ShowPost()
    {
        if (nearby?.Role is not PostmanRole)
            return;
        ui.PostPanel(nearby, Post, LetterAddressee, NextKeeper() is { } next ? world.Keepers[next] : null);
    }

    /// <summary>The keeper the postman's next letter is for, or null when no keeper lives in the valley.</summary>
    private int? NextKeeper()
    {
        int keeper = Post.NextAddressee(Settings.Seed, k => world.Keepers[k] != null);
        return keeper < 0 ? null : keeper;
    }

    /// <summary>The postman hands over the next letter and the arrows lead to its keeper.</summary>
    internal void TakeLetter()
    {
        if (State != GameState.PostStatus || nearby is not { Role: PostmanRole } postman || Post.Busy)
            return;
        if (NextKeeper() is not { } keeper || !Post.Take(keeper))
            return;
        postman.Celebrate();
        CloseInteraction();
        Speak(Texts.PostLetterFor(world.Keepers[keeper]!.DisplayName), postman);
        UpdateLetterTrip();
        SaveProgress();
    }

    /// <summary>
    /// A keeper of a border: with a letter for the keeper on the car the handover question is asked, otherwise the keeper's
    /// panel tells about the border and the stamps sent; the lighthouse keeper shows the lighthouse once it is begun.
    /// </summary>
    internal void OpenBorderKeeper(Character keeper)
    {
        if (keeper.Role is not BorderKeeperRole role)
            return;
        ClearChallenge();
        int border = (int)role.Border;
        if (Post.Carrying == border)
        {
            nearby = keeper;
            vehicle.Active = false;
            BeginQuiz();
            return;
        }
        if (role.Border == Border.Sea && OpenLighthouse(keeper))
            return;
        if (role.Border == Border.Forest && OpenTracking(keeper))
            return;
        if (role.Border == Border.Mountains && OpenShepherd(keeper))
            return;
        if (role.Border == Border.River && OpenRally(keeper))
            return;
        EnterInteraction(GameState.KeeperStatus, keeper);
        ShowKeeper();
        Speak(Post.Delivered[border] == 0 ? keeper.Greeting : Texts.KeeperAbout(role.Border), keeper);
    }

    private void ShowKeeper()
    {
        if (nearby is not { Role: BorderKeeperRole role } keeper)
            return;
        ui.KeeperPanel(keeper, role.Border, Post.Stamps((int)role.Border));
    }

    /// <summary>The handover question is answered: a stamp, coins and a reply for a house of the village.</summary>
    internal void CompleteKeeperQuiz(Character keeper)
    {
        // At the coast quarry the keeper of the sea asks the stone's loading question, whatever letter is carried.
        if (Haul is { Loaded: false } trip && trip.Asker == keeper && trip.Reached(vehicle.Position))
        {
            CompleteHaulLoading();
            return;
        }
        if (keeper.Role is not BorderKeeperRole role || Post.Carrying != (int)role.Border)
        {
            FinishInteraction();
            return;
        }
        int residents = world.Village.Residents.Count;
        int house = residents == 0 ? -1 : (int)((uint)(Settings.Seed + Post.Total) % residents);
        bool wasFull = Post.AlbumFull;
        bool stamp = Post.Deliver(house);
        int coins = Post.LetterCoins + (!wasFull && Post.AlbumFull ? Post.AlbumCoins : 0);
        ClearChallenge();
        keeper.Celebrate();
        string message = Texts.LetterDelivered(
            stamp,
            !wasFull && Post.AlbumFull ? Post.AlbumCoins : 0,
            house >= 0 ? world.Village.Residents[house].DisplayName : null
        );
        CelebrateAt(keeper.Position + Vector3.Up * 1.6f, WithPets(message, EarnPets()));
        Speak(message, keeper);
        UpdateLetterTrip();
        if (role.Border == Border.Sea)
            UnlockLighthouse();
        PayReward(coins);
    }

    /// <summary>The reply is brought to its house: the resident thanks the driver and coins are paid.</summary>
    private bool DeliverReply(Character resident)
    {
        if (
            Post.Reply < 0
            || resident.Role is not HouseResidentRole house
            || house.House != Post.Reply
            || Delivery != null
            || Haul != null
        )
            return false;
        Post.BringReply();
        ClearChallenge();
        nearby = resident;
        vehicle.Active = false;
        resident.Celebrate();
        string message = Texts.ReplyDelivered;
        CelebrateAt(resident.Position + Vector3.Up * 1.6f, message);
        Speak(message, resident);
        UpdateLetterTrip();
        PayReward(Post.ReplyCoins);
        return true;
    }

    /// <summary>Puts the arrows and the envelope in line with what is carried: a new way, or none.</summary>
    private void UpdateLetterTrip()
    {
        var addressee = LetterAddressee;
        if (letterTrip != null && letterTrip.Addressee != addressee)
        {
            // Out of the tree at once, so the new trip takes the name while the old one waits to be freed.
            letterTrip.GetParent()?.RemoveChild(letterTrip);
            letterTrip.QueueFree();
            letterTrip = null;
        }
        if (addressee != null && letterTrip == null)
        {
            letterTrip = new LetterTrip(world, vehicle, addressee);
            world.AddChild(letterTrip);
        }
        UpdateDeliveryGuides();
        ui.LetterHud(addressee, Post.Carrying >= 0 ? Post.Carrying : -1);
    }

    /// <summary>
    /// The keeper a letter is for shows where to go; the postman shows his mark while he has a letter to give out, and
    /// the keeper of the sea while the lighthouse waits for its next load, like the builders of the old valley.
    /// </summary>
    private void UpdatePostGuides()
    {
        var addressee = LetterAddressee;
        foreach (var keeper in world.Keepers)
            keeper?.Guide(
                keeper == addressee
                    || keeper == world.LighthouseKeeper && LighthouseAwaitsLoad
                    || keeper == world.Forester && TrackingAwaitsStart
                    || keeper == world.Shepherd && SheepAwaitShepherd
                    || keeper == world.Ferrywoman && RallyAwaitsStart,
                keeper == addressee
            );
        world.Postman?.Guide(!Post.Busy && NextKeeper() != null);
        world.Cartographer?.Guide(CartographerAwaits);
        world.Birdwatcher?.Guide(BirdwatcherAwaits);
        world.Castellan?.Guide(CastellanAwaits);
    }

    /// <summary>The arrows of the letter move on; a delivery or a trailer trip under way shows its own instead.</summary>
    private void TickLetter(double delta)
    {
        ui.ShowLetterHud(State == GameState.Exploring);
        if (letterTrip == null)
            return;
        bool free = Delivery == null && Haul == null;
        letterTrip.Tick(delta, free && State == GameState.Exploring);
        if (free && State == GameState.Exploring)
            ui.DeliveryDirection(letterTrip.Addressee.Position, vehicle);
    }

    /// <summary>Puts the post back after a load: the letters handed over and what is carried.</summary>
    private void RestorePost(int[] delivered, int carrying, int reply)
    {
        Post.Restore(delivered, carrying, reply);
        UpdateLetterTrip();
    }
}
