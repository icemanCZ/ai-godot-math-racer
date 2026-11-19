using System.Linq;
using Godot;
using MathRacer.Characters;
using MathRacer.Settlement;

namespace MathRacer.App;

/// <summary>
/// The glass lamp maker on the village square gives out candles and lamps for nothing: talking to him opens a card for
/// each kind and a lamp that is taken goes into the bag. The talk button (E, space, A) takes a standing lamp up when
/// the car is close to it and otherwise, where nothing else wants it, puts the next lamp down beside the car, wherever
/// the ground is dry. At night every lamp shines. Saved as <c>SaveData.Lamps</c>.
/// </summary>
public partial class Game
{
    /// <summary>How far from the car a standing lamp can be taken up.</summary>
    internal const float LampReach = 3f;

    private const float LampMessageSeconds = 3;

    private float lampMessageTime;
    private string lampMessage = "";

    private LampField Lamps => world.Village.Lamps;

    internal void OpenGlassmaker(Character maker)
    {
        if (maker.Role is not GlassmakerRole)
            return;
        ClearChallenge();
        EnterInteraction(GameState.GlassmakerShop, maker);
        ShowGlassmaker();
        Speak(Lamps.Carried.Count == 0 && Lamps.PlacedCount == 0 ? maker.Greeting : Texts.JakeSvicticeSiVyberes, maker);
    }

    private void ShowGlassmaker()
    {
        if (nearby?.Role is not GlassmakerRole)
            return;
        ui.GlassmakerShop(nearby, Lamps.Carried, Lamps.PlacedCount);
    }

    /// <summary>Takes a lamp of this kind out of the open shop into the bag, if the bag has room.</summary>
    internal void TakeLamp(string id)
    {
        if (State != GameState.GlassmakerShop || nearby?.Role is not GlassmakerRole || GlassLamps.Find(id) is null)
            return;
        if (!Lamps.TryTake(id))
        {
            Speak(Texts.VakJePlny, nearby);
            return;
        }
        nearby.Celebrate();
        ShowGlassmaker();
        RefreshLampHud();
        Speak(Texts.TadyMasPostavJi, nearby);
        SaveProgress();
    }

    /// <summary>Puts the first lamp of the bag on the ground at one side of the car, or ahead of it or behind it.</summary>
    internal bool PutDownLamp()
    {
        if (State != GameState.Exploring || Lamps.Carried.Count == 0)
            return false;
        if (Lamps.PlacedCount >= LampField.MaxPlaced)
        {
            LampMessage(Texts.UzMasSpoustuLampSeberNejakou);
            return false;
        }
        var basis = vehicle.GlobalTransform.Basis;
        var right = basis.X;
        var forward = -basis.Z;
        foreach (var offset in new[] { right * 3.6f, -right * 3.6f, forward * 4.2f, -forward * 4.2f })
        {
            var spot = vehicle.Position + offset;
            if (!Lamps.PutDown(spot.X, spot.Z, vehicle.Heading))
                continue;
            RefreshLampHud();
            LampMessage(Texts.LampaStojiSeberesJi);
            SaveProgress();
            return true;
        }
        LampMessage(Texts.SemLampuPostavitNejde);
        return false;
    }

    /// <summary>Takes the standing lamp nearest to the car up into the bag; false when none is within reach.</summary>
    internal bool TakeUpLamp()
    {
        if (State != GameState.Exploring || LampNearby() is not { } index)
            return false;
        string name = GlassLamps.Find(Lamps.KindAt(index))?.Name ?? "";
        if (!Lamps.PickUp(index))
        {
            LampMessage(Texts.VakJePlny);
            return true;
        }
        RefreshLampHud();
        LampMessage(Texts.LampInBag(name));
        SaveProgress();
        return true;
    }

    /// <summary>The index of the standing lamp within reach of the car, or null.</summary>
    private int? LampNearby()
    {
        int index = Lamps.NearestIndex(vehicle.Position, LampReach);
        return index < 0 ? null : index;
    }

    /// <summary>The talk button works on lamps only where nothing else wants it: no character is near and no load is waiting.</summary>
    private bool LampPickupAvailable =>
        nearby == null && !(Haul is { Loaded: false, Arrived: true } && Haul.Reached(vehicle.Position));

    private void LampMessage(string text)
    {
        lampMessage = text;
        lampMessageTime = LampMessageSeconds;
        RefreshLampHud();
    }

    internal void RefreshLampHud() =>
        ui.LampHud(
            Lamps.Carried.Count,
            Lamps.Carried.Count > 0 ? GlassLamps.Find(Lamps.Carried[0])?.Name ?? "" : "",
            lampMessageTime > 0 ? lampMessage : ""
        );

    private void TickLamps(float delta)
    {
        if (lampMessageTime <= 0)
            return;
        lampMessageTime -= delta;
        if (lampMessageTime <= 0)
            RefreshLampHud();
    }
}
