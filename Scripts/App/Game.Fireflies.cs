using Godot;
using MathRacer.Characters;
using MathRacer.Valley;

namespace MathRacer.App;

/// <summary>
/// The firefly hunt of a starry night. It is switched on only at the witch, with a button of its own (state
/// <see cref="GameState.FireflyStatus"/>) that she shows when the night is deep enough for stars; the weather question stays
/// behind another button. The child then drives after blinking fireflies with a lantern that shines brighter with every one that
/// is caught; ten light a new star of the collection in the sky, and the pictures are drawn one after another. The hunt ends
/// with the night. Saved as <c>SaveData.Fireflies</c> (the number caught); the stars and the lantern follow from it.
/// </summary>
public partial class Game
{
    private readonly FireflyHunt fireflies = new();
    private AudioStreamPlayer? chime;
    private string fireflyNote = "";
    private float fireflyNoteTime;
    private bool collectionDrawn;

    internal FireflyHunt Fireflies => fireflies;

    /// <summary>The witch asks the weather question by day; on a starry night she first offers the firefly hunt.</summary>
    internal void OpenWeatherChallenge()
    {
        if (Weather.StarsOut)
        {
            OpenFireflies();
            return;
        }
        BeginWeatherQuiz();
    }

    /// <summary>The weather question of the witch, whose answer opens the choice of weather.</summary>
    internal void BeginWeatherQuiz()
    {
        if (State is not (GameState.Exploring or GameState.FireflyStatus))
            return;
        nearby = world.Witch;
        vehicle.Active = false;
        BeginQuiz();
    }

    private void OpenFireflies()
    {
        var witch = world.Witch;
        ClearChallenge();
        EnterInteraction(GameState.FireflyStatus, witch);
        ShowFireflies();
        Speak(
            fireflies.Complete ? Texts.FireflyCollectionComplete
                : fireflies.Active ? Texts.FireflyStillOn
                : Weather.StarsOut ? Texts.FireflyWelcome
                : Texts.FireflyNeedNight,
            witch
        );
    }

    private void ShowFireflies()
    {
        if (nearby?.Role is not WitchRole)
            return;
        ui.FireflyPanel(nearby, fireflies.LitStars(), fireflies.Active, fireflies.Complete, Weather.StarsOut);
    }

    /// <summary>The witch lends the lantern: fireflies begin to blink nearby. Needs a starry night.</summary>
    internal void BeginFireflyHunt()
    {
        if (State != GameState.FireflyStatus || nearby is not { Role: WitchRole } witch || !Weather.StarsOut)
            return;
        if (!fireflies.Start())
            return;
        witch.Celebrate();
        CloseInteraction();
        Speak(Texts.FireflyGoodLuck, witch);
    }

    /// <summary>Puts the collection back after a load: the number caught, and no hunt until the witch lends the lantern again.</summary>
    private void RestoreFireflies(int caught)
    {
        fireflies.Restore(caught);
        collectionDrawn = false;
        fireflyNote = "";
        fireflyNoteTime = 0;
        ui.FireflyHud(false, 0, FireflyHunt.PerStar, "");
    }

    /// <summary>Draws the collection once the stars are out, and after every load.</summary>
    private void DrawCollection((int Picture, int Star)? newest = null)
    {
        world.Atmosphere.ShowCollection(fireflies.LitStars(), newest);
        collectionDrawn = true;
    }

    private void TickFireflies(float delta)
    {
        bool exploring = State == GameState.Exploring;
        bool running = State is not (GameState.Paused or GameState.Title);
        if (!collectionDrawn && Weather.StarsOut)
            DrawCollection();
        if (fireflies.Active && State != GameState.Title && !Weather.StarsOut)
        {
            // Morning: the fireflies go to sleep and the lantern is given back.
            fireflies.Stop();
            Note(Texts.FireflyMorning);
            Speak(Texts.FireflyMorning);
        }
        if (running && fireflyNoteTime > 0 && (fireflyNoteTime -= delta) <= 0)
            fireflyNote = "";
        int caught = world.Sparks.Tick(delta, fireflies.Active, fireflies.Lantern, exploring);
        for (int i = 0; i < caught; i++)
            CatchFirefly();
        ui.FireflyHud(fireflies.Active && exploring, fireflies.TowardsStar, FireflyHunt.PerStar, fireflyNote);
    }

    private void Note(string text)
    {
        fireflyNote = text;
        fireflyNoteTime = 7;
    }

    /// <summary>A firefly flies into the lantern; the tenth lights a new star.</summary>
    private void CatchFirefly()
    {
        bool star = fireflies.Catch();
        Chime(star ? 2f : 1f + fireflies.TowardsStar * .07f);
        if (!star)
            return;
        var newest = FireflyHunt.StarAt(fireflies.Stars - 1);
        DrawCollection(newest);
        var picture = Constellation.All[newest.Picture];
        string text =
            fireflies.Complete ? Texts.FireflyComplete
            : newest.Star == picture.Stars.Length - 1 ? Texts.FireflyPictureDone(picture.Name)
            : Texts.FireflyNewStar(fireflies.Stars, FireflyHunt.TotalStars);
        if (fireflies.Complete)
            fireflies.Stop();
        Note(text);
        Speak(text);
        SaveProgress();
    }

    private void Chime(float pitch)
    {
        if (chime == null)
        {
            chime = new AudioStreamPlayer
            {
                Name = "FireflyChime",
                Stream = MakeBeep(),
                VolumeDb = -12,
            };
            AddChild(chime);
        }
        chime.PitchScale = pitch;
        chime.Play();
    }
}
