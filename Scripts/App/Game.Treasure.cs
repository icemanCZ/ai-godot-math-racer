using System;
using Godot;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The pirate captain's treasure hunt. Talking to her (state <see cref="GameState.TreasureStatus"/>) gives out one treasure; a
/// detector then shows how warm or cold the car is, beeping faster and higher the closer it gets, and on the spot the talk
/// button digs. The chest is locked by a question, asked on the captain's behalf. When its answer is right the chest opens,
/// coins are paid and the hunt is over: a new treasure is given out only when the player drives back to the captain.
/// Saved as <c>SaveData.Treasure</c> (the number found and whether a hunt is on); the place follows from the seed.
/// </summary>
public partial class Game
{
    private readonly TreasureHunt treasure = new();
    private Vector3 treasureSpot;
    private float beepTimer;
    private float pulse;
    private AudioStreamPlayer? beeper;

    private Character Treasurer => world.Village.Treasurer;

    /// <summary>Whether the car is close enough to the buried treasure to dig and nothing else holds the player.</summary>
    internal bool TreasureDigAvailable =>
        treasure.Active && State == GameState.Exploring && TreasureDistance() <= TreasureHunt.DigRadius;

    private float TreasureDistance() =>
        new Vector2(vehicle.Position.X - treasureSpot.X, vehicle.Position.Z - treasureSpot.Z).Length();

    internal void OpenTreasurer(Character captain)
    {
        if (captain.Role is not TreasureRole)
            return;
        ClearChallenge();
        EnterInteraction(GameState.TreasureStatus, captain);
        ShowTreasure();
        Speak(
            treasure.Active ? Texts.TreasureStillLooking
                : treasure.Found == 0 ? captain.Greeting
                : Texts.TreasureAnotherOne,
            captain
        );
    }

    private void ShowTreasure()
    {
        if (nearby?.Role is not TreasureRole)
            return;
        ui.TreasurePanel(nearby, treasure.Found, treasure.Active);
    }

    /// <summary>The captain gives out the next treasure: it is buried and the detector starts to beep.</summary>
    internal void BeginTreasureHunt()
    {
        if (State != GameState.TreasureStatus || nearby is not { Role: TreasureRole } captain || treasure.Active)
            return;
        treasure.Begin();
        BuryTreasure();
        captain.Celebrate();
        CloseInteraction();
        Speak(Texts.TreasureGoodLuck, captain);
        SaveProgress();
    }

    /// <summary>Buries the present treasure at the place that follows from the seed and the number found.</summary>
    private void BuryTreasure()
    {
        treasureSpot = world.TreasureSpot(treasure.Found);
        world.Treasure.Bury(treasureSpot, (treasure.Found * 1.7f) % Mathf.Tau);
        beepTimer = 0;
    }

    /// <summary>Puts the hunt back after a load: the number found, and a treasure in the ground if one is being looked for.</summary>
    private void RestoreTreasure(int found, bool active)
    {
        treasure.Restore(found, active);
        if (treasure.Active)
            BuryTreasure();
        else
            world.Treasure.Clear();
        ui.TreasureHud(false, "", 0, "", 0);
    }

    /// <summary>The car stops on the spot and the chest asks its question; a right answer opens it.</summary>
    internal void DigTreasure()
    {
        if (!TreasureDigAvailable)
            return;
        nearby = Treasurer;
        vehicle.Active = false;
        world.Treasure.Dig();
        BeginQuiz();
    }

    /// <summary>The question is answered: the chest swings open, its coins are paid and the hunt is over.</summary>
    internal void OpenTreasureChest()
    {
        if (!treasure.Active)
            return;
        int index = treasure.Found;
        int coins = TreasureHunt.Coins(index);
        treasure.Finish();
        world.Treasure.OpenChest();
        ClearChallenge();
        var captain = Treasurer;
        captain.Celebrate();
        string message = Texts.TreasureFound(Texts.TreasureContents(index), coins);
        CelebrateAt(world.Treasure.GlobalPosition + Vector3.Up * 1.6f, message);
        Speak(message, captain);
        ui.TreasureHud(false, "", 0, "", 0);
        PayReward(coins);
    }

    /// <summary>The detector: shows the warmth of the hunt and beeps; the chest and the cross animate.</summary>
    private void TickTreasure(float delta)
    {
        bool running = State is not (GameState.Paused or GameState.Title);
        world.Treasure.Tick(delta, vehicle.Position, running);
        if (!treasure.Active || State != GameState.Exploring)
        {
            if (State != GameState.Paused)
                ui.TreasureHud(false, "", 0, "", 0);
            return;
        }
        float distance = TreasureDistance();
        var warmth = TreasureHunt.WarmthAt(distance);
        beepTimer -= delta;
        pulse = Mathf.Max(0, pulse - delta * 3);
        if (beepTimer <= 0)
        {
            beepTimer = TreasureHunt.BeepInterval(distance);
            pulse = 1;
            Beep(TreasureHunt.Closeness(distance));
        }
        ui.TreasureHud(
            true,
            Texts.TreasureWarmthWord(warmth),
            TreasureHunt.Closeness(distance),
            warmth switch
            {
                TreasureWarmth.Freezing => "5aa9e6",
                TreasureWarmth.Cold => "7fb8e8",
                TreasureWarmth.Warm => "f2c14e",
                TreasureWarmth.Hot => "f08a3c",
                _ => "e2432f",
            },
            pulse
        );
    }

    private void Beep(float closeness)
    {
        if (beeper == null)
        {
            beeper = new AudioStreamPlayer
            {
                Name = "TreasureDetector",
                Stream = MakeBeep(),
                VolumeDb = -14,
            };
            AddChild(beeper);
        }
        beeper.PitchScale = .75f + closeness * 1.1f;
        beeper.Play();
    }

    /// <summary>A short soft tone, made once: 880 Hz for a tenth of a second under a smooth envelope.</summary>
    private static AudioStreamWav MakeBeep()
    {
        const int rate = 22050;
        const int samples = rate / 10;
        var data = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            float envelope = MathF.Sin(MathF.PI * i / samples);
            short value = (short)(MathF.Sin(MathF.Tau * 880 * i / rate) * envelope * .45f * short.MaxValue);
            data[i * 2] = (byte)(value & 0xFF);
            data[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Stereo = false,
            Data = data,
        };
    }
}
