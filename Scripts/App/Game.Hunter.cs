using Godot;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The hunter at the edge of the village. Talking to him (state <see cref="GameState.HunterStatus"/>) shows the animals in
/// his pen and, with a green check mark, sends the player out with a net: three animals then run free in a meadow far away,
/// and the corner of the screen points to them. The space bar (or E, or A) throws the net from the car; a net that comes down
/// on an animal wraps it, and the player loads it onto the car and drives it to the gate, where it runs into the pen and the
/// hunt is over. A missed net lies in the grass until the car comes back for it; one that falls into the river is lost, and
/// the hunter gives a new one, as he does for one left lying. Eight animals in the pen earn his pet. Before the first hunt
/// the player raises his hut with four trailer trips of goods (Game.HunterHut.cs). Saved as <c>SaveData.Hunt</c> (the
/// animals in the pen, whether a hunt is on, the animal on the car, the deliveries of the hut).
/// </summary>
public partial class Game
{
    /// <summary>The car hands an animal over when it comes this close to the gate.</summary>
    internal const float HuntDropRadius = 6.5f;

    private string huntNote = "";
    private float huntNoteTime;

    internal AnimalHunt Hunt => world.Hunt;

    private HuntField HuntGrounds => world.HuntGrounds;

    /// <summary>A hunt is on, the net is on the car and nothing else holds the player: the space bar throws it.</summary>
    internal bool NetThrowAvailable =>
        State == GameState.Exploring && Hunt.Active && Hunt.Carrying == null && HuntGrounds.Net == NetState.InHand;

    /// <summary>The missed net lies near the car: the talk button picks it up.</summary>
    internal bool NetPickupAvailable => State == GameState.Exploring && Hunt.Active && HuntGrounds.NetWithinReach;

    /// <summary>The caught animal waits in its net near the car: the talk button loads it.</summary>
    internal bool CatchLoadAvailable =>
        State == GameState.Exploring && Hunt.Active && Hunt.Carrying == null && HuntGrounds.CatchWithinReach;

    internal void OpenHunter(Character hunter)
    {
        if (hunter.Role is not HunterRole)
            return;
        ClearChallenge();
        // An animal on the car is handed over, whichever side of him the car stopped on.
        if (Hunt.Carrying != null && DeliverAnimal())
            return;
        EnterInteraction(GameState.HunterStatus, hunter);
        if (world.Pen is { Complete: false } pen)
        {
            OpenHut(hunter, pen);
            return;
        }
        bool newNet =
            Hunt.Active && Hunt.Carrying == null && HuntGrounds.Net is NetState.Lying or NetState.Lost or NetState.None;
        if (newNet)
            HuntGrounds.GiveNewNet();
        ShowHunter();
        Speak(
            newNet ? Texts.HunterNewNet
                : Hunt.Active ? Texts.HunterStillHunting
                : Hunt.Caught == 0 ? hunter.Greeting
                : Texts.HunterAnotherOne,
            hunter
        );
    }

    private void ShowHunter()
    {
        if (nearby?.Role is not HunterRole)
            return;
        if (world.Pen is { Complete: false } pen)
            ShowHut(nearby, pen);
        else
            ui.HunterPanel(nearby, Hunt.Penned, Hunt.Active, PetOfHunter());
    }

    private CollectorPet? PetOfHunter()
    {
        foreach (var pet in world.WorldPets)
            if (pet.Kind == CollectorPetKind.Fawn)
                return pet;
        return null;
    }

    /// <summary>The hunter hands over a net and the animals of the hunt start to run in a meadow far away.</summary>
    internal void BeginHunt()
    {
        if (
            State != GameState.HunterStatus
            || nearby is not { Role: HunterRole } hunter
            || world.Pen is not { Complete: true }
            || !Hunt.Begin()
        )
            return;
        HuntGrounds.Begin(hunter.Position, Hunt.Caught);
        hunter.Celebrate();
        CloseInteraction();
        Speak(Texts.HunterGoodLuck, hunter);
        SaveProgress();
    }

    /// <summary>
    /// Puts the hunt back after a load: the hut as far as it is built (standing at once in a valley without the works), the
    /// pen, and the animals of a hunt under way in a new meadow. It comes before the trailer trip, which may be for the hut.
    /// </summary>
    private void RestoreHunt(string[] penned, bool active, string carrying, int hut)
    {
        world.Pen?.SetStage(world.Industry.Length > 0 ? hut : HutPlans.Trips);
        Hunt.Restore(penned, active && world.Pen is { Complete: true }, carrying);
        world.Pen?.SetAnimals(Hunt.Penned);
        HuntGrounds.Clear();
        if (Hunt.Active && world.Hunter is { } hunter)
        {
            if (Hunt.Carrying is { } kind)
                HuntGrounds.Carry(kind, Delivery != null);
            else
                HuntGrounds.Begin(hunter.Position, Hunt.Caught);
        }
        huntNote = "";
        huntNoteTime = 0;
        ui.HuntHud(false, vehicle, Vector3.Zero, "", null, false, "", "");
    }

    /// <summary>Throws the net from the car; returns whether it flew.</summary>
    internal bool ThrowNet()
    {
        if (!NetThrowAvailable || !HuntGrounds.Throw())
            return false;
        Whoosh();
        return true;
    }

    /// <summary>Picks the missed net up from the grass.</summary>
    internal bool PickUpNet()
    {
        if (!NetPickupAvailable || !HuntGrounds.PickUpNet())
            return false;
        Note(Texts.HuntNetBack, speak: true);
        return true;
    }

    /// <summary>Loads the caught animal, wrapped in its net, onto the car's roof.</summary>
    internal bool LoadCatch()
    {
        if (!CatchLoadAvailable || HuntGrounds.LoadCatch() is not { } kind || !Hunt.Load(kind))
            return false;
        HuntGrounds.Carry(kind, Delivery != null);
        Note(Texts.HuntLoaded(kind), speak: true);
        SaveProgress();
        return true;
    }

    /// <summary>The animal on the car runs into the pen and the hunt is over; returns whether an animal was handed over.</summary>
    private bool DeliverAnimal()
    {
        if (world.Hunter is not { } hunter || Hunt.Deliver() is not { } kind)
            return false;
        HuntGrounds.Clear();
        world.Pen?.Admit(kind);
        hunter.Celebrate();
        string? pets = EarnPets();
        string message = WithPets(Texts.HuntDelivered(kind, Hunt.Caught), pets);
        CelebrateAt((world.Pen?.Focus ?? hunter.Position) + Vector3.Up, message);
        Speak(pets != null ? Texts.HunterPetEarned : Texts.HunterThanks(kind, Hunt.Caught), hunter);
        huntNote = "";
        huntNoteTime = 0;
        SaveProgress();
        return true;
    }

    private void Note(string text, bool speak = false)
    {
        huntNote = text;
        huntNoteTime = 6;
        if (speak)
            Speak(text);
    }

    /// <summary>
    /// The animals run, the net flies, and the corner of the screen points to where the hunt goes on: the nearest free animal
    /// while the net is on the car, the net in the grass, the caught animal, or the hunter.
    /// </summary>
    private void TickHunt(float delta)
    {
        bool running = State is not (GameState.Paused or GameState.Title);
        bool exploring = State == GameState.Exploring;
        world.Pen?.Tick(delta, vehicle.Position, running);
        switch (HuntGrounds.Tick(delta, running))
        {
            case NetLanding.Caught when HuntGrounds.WrappedKind is { } kind:
                Note(Texts.HuntCaught(kind), speak: true);
                break;
            case NetLanding.Missed:
                Note(Texts.HuntMissed, speak: true);
                break;
            case NetLanding.InWater:
                Note(Texts.HuntNetLost, speak: true);
                break;
        }
        if (running && huntNoteTime > 0 && (huntNoteTime -= delta) <= 0)
            huntNote = "";
        if (!Hunt.Active || world.Hunter is not { } hunter)
        {
            if (State != GameState.Paused)
                ui.HuntHud(false, vehicle, Vector3.Zero, "", null, false, "", "");
            return;
        }
        if (exploring && Hunt.Carrying != null && Flat(vehicle.Position - world.HunterPlace!.Drop) <= HuntDropRadius)
        {
            DeliverAnimal();
            ui.HuntHud(false, vehicle, Vector3.Zero, "", null, false, "", "");
            return;
        }
        Vector3 goal;
        string icon = "";
        Character? who = null;
        if (Hunt.Carrying != null || HuntGrounds.Net is NetState.Lost or NetState.None)
        {
            goal = world.HunterPlace!.Drop;
            who = hunter;
        }
        else if (HuntGrounds.NetSpot is { } spot)
        {
            goal = spot;
            icon = HuntGrounds.WrappedKind is { } kind ? AnimalHunt.Icon(kind) : "net";
        }
        else if (HuntGrounds.NearestFree(vehicle.Position) is { } prey)
        {
            goal = prey.Position;
            icon = AnimalHunt.Icon(prey.Kind);
        }
        else
        {
            goal = world.HunterPlace!.Drop;
            who = hunter;
        }
        float distance = Flat(goal - vehicle.Position);
        ui.HuntHud(
            exploring,
            vehicle,
            goal,
            icon,
            who,
            HuntGrounds.Net == NetState.InHand,
            distance < 10 ? "" : $"{distance:0} m",
            huntNote
        );
    }

    private static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();

    private AudioStreamPlayer? whoosher;

    /// <summary>The swish of the net leaving the car: the detector's tone, played low and short.</summary>
    private void Whoosh()
    {
        if (whoosher == null)
        {
            whoosher = new AudioStreamPlayer
            {
                Name = "NetWhoosh",
                Stream = MakeBeep(),
                VolumeDb = -16,
                PitchScale = .45f,
            };
            AddChild(whoosher);
        }
        whoosher.Play();
    }
}
