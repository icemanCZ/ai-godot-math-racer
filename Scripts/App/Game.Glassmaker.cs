using Godot;
using MathRacer.Buildings;
using MathRacer.Characters;
using MathRacer.Tasks;

namespace MathRacer.App;

/// <summary>
/// The lamp maker blows his candles and lamps from glasses: from his shop the player takes a load of glasses from the
/// glassworks to the village square, as often as the glassworks has some, and he pays for every load. So the glassworks
/// always has someone to make glasses for, long after the houses have had theirs. With no glasses in the glassworks he
/// sends the player for sand; in a valley without the glassworks he only gives out lamps. The trip is a
/// <see cref="GlassmakerTripTarget"/> and shares the task slot with the other trailer trips.
/// </summary>
public partial class Game
{
    /// <summary>Coins the lamp maker pays for a load of glasses.</summary>
    internal const int GlassmakerCoins = 5;

    /// <summary>The trip that takes glasses to the lamp maker, or null.</summary>
    private HaulTrip? GlassmakerTrip => Haul is { ForGlassmaker: true } trip ? trip : null;

    /// <summary>Whether the glassworks has a load of glasses for the lamp maker.</summary>
    private bool GlassForGlassmaker => world.Glassworks is { } works && works.Stock >= Works.PerTrip;

    /// <summary>The glassworks, while it has no glasses for the lamp maker and room for sand; else null.</summary>
    private Works? GlassmakerMissingGlass =>
        world.Glassworks is { Full: false } works && works.Stock < Works.PerTrip ? works : null;

    /// <summary>What the lamp maker says when the shop opens: the lamps, or the glasses he wants.</summary>
    private string GlassmakerSpeech(Character maker) =>
        Lamps.Carried.Count == 0 && Lamps.PlacedCount == 0 ? maker.Greeting
        : Haul != null || world.Glassworks == null ? Texts.JakeSvicticeSiVyberes
        : GlassForGlassmaker ? Texts.PrivezesMiSklenickyZeSklarny
        : GlassmakerMissingGlass != null ? Texts.SklarnaNemaSklenickyPoslusTePro
        : Texts.JakeSvicticeSiVyberes;

    /// <summary>The lamp maker's trip: glasses from the glassworks to the ring on the square, after a quiz.</summary>
    internal void BeginGlassmakerTrip() => BeginGlassmakerTrip(false);

    private void BeginGlassmakerTrip(bool quizzed)
    {
        if (
            State != GameState.GlassmakerShop
            || nearby?.Role is not GlassmakerRole
            || Haul != null
            || !GlassForGlassmaker
        )
            return;
        if (!quizzed)
        {
            AskBeforeDeparture(Departure.Glassmaker);
            return;
        }
        StartTrip(new GlassmakerTripTarget(world));
    }

    /// <summary>The glasses are tipped out on the square: the lamp maker pays and asks for more whenever there are some.</summary>
    internal void FinishGlassmakerDump(HaulTrip trip)
    {
        var maker = trip.Contractor;
        EndHaul();
        AddCoins(GlassmakerCoins);
        maker.Celebrate();
        CelebrateAt(world.Village.GlassmakerDrop + Vector3.Up * 2, Texts.GlassmakerPaid(GlassmakerCoins));
        Speak(Texts.ZTvychSklenicekVyfouknuLampy, maker);
        UpdateDeliveryGuides();
        SaveProgress();
    }
}
