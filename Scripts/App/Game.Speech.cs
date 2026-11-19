using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using MathRacer.Characters;
using MathRacer.Quizzes;
using MathRacer.Tasks;

namespace MathRacer.App;

public partial class Game
{
    private static readonly float[] AnimalSpeechPitches = { 1.20f, 1.08f, .85f, 1.29f, .94f, 1.14f, .78f, 1.02f };
    private static readonly float[] AnimalSpeechRates = { .9f, .8f, 1.02f };
    private string[]? czechVoices;

    private void Speak(string text) => Speak(text, null);

    /// <summary>
    /// Reads a text aloud. A new text normally cuts off the one being read; with <paramref name="interrupt"/> off it
    /// waits for it, which is how the coins of a reward follow the thanks of the character.
    /// </summary>
    private void Speak(string text, Character? speaker, bool interrupt = true)
    {
        if (!DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech))
            return;
        czechVoices ??= DisplayServer.TtsGetVoicesForLanguage("cs");
        if (czechVoices.Length == 0)
            return;
        text = Spoken(text);

        if (speaker == null)
        {
            DisplayServer.TtsSpeak(text, czechVoices[0], rate: .9f, interrupt: interrupt);
            return;
        }

        // Actors are ordered by stable world ID. The speaking bandit follows the registered actors.
        int speakerIndex = world.InteractionActors.TakeWhile(actor => actor != speaker).Count();
        string voice = czechVoices[speakerIndex % czechVoices.Length];
        int variation = speakerIndex / czechVoices.Length;
        float pitch = AnimalSpeechPitches[variation % AnimalSpeechPitches.Length];
        float rate = AnimalSpeechRates[variation / AnimalSpeechPitches.Length % AnimalSpeechRates.Length];
        DisplayServer.TtsSpeak(text, voice, pitch: pitch, rate: rate, interrupt: interrupt);
    }

    /// <summary>
    /// The text of a panel or a celebration as it is read: the signs that are only drawn (<c>+3</c>, <c>2 / 6</c>,
    /// the dot between two parts) become words, because a voice would spell them out.
    /// </summary>
    internal static string Spoken(string text) =>
        Regex.Replace(
            Regex.Replace(text, @"\+(\d+)", "plus $1").Replace("·", ",").Replace("✓", "").Replace("—", ","),
            @"(\d+)\s*/\s*(\d+)",
            "$1 z $2"
        );

    /// <summary>What a postcard's question asks, in words a voice can read before the child sees the answers.</summary>
    private static string PostcardInstruction(IChallenge question) =>
        question switch
        {
            SyllableChallenge => Texts.VyberSlabikyPodleObrazku,
            LetterChallenge => Texts.VyberObrazekPodlePrvnihoPismena,
            ReadingChallenge => Texts.PrectiSlovoAVyberObrazek,
            _ => Texts.VyresPriklad,
        };

    private void StopSpeech()
    {
        if (czechVoices is { Length: > 0 })
            DisplayServer.TtsStop();
    }

    private static string QuizInstruction(Character actor, IChallenge question)
    {
        string instruction = question switch
        {
            SyllableChallenge => Texts.NejprveVybralaSlabiky,
            LetterChallenge => Texts.NejprveVybralaObrazekPodlePrvnihoPismene,
            ReadingChallenge => Texts.NejprvePrecetlaSlovoAVybralaObrazek,
            DeliveryChallenge => Texts.SpocitalaNaklad,
            LoadChallenge => Texts.SpocitalaKolikSeTohoJesteVejde,
            _ => Texts.NejprveVyresilaPriklad,
        };
        return $"{actor.DisplayName} chce, abys {instruction}.";
    }

    private static string ExpeditionInstruction(Expedition expedition)
    {
        string task = expedition switch
        {
            { Golem: true } => Texts.PrivezSestDilkuGolema,
            { Farm: true } => Texts.Bring(expedition.FarmName),
            { Garden: true } => Texts.Bring(expedition.FlowerName),
            { Gems: true } or { Toys: true } => Texts.Bring(expedition.CollectionName),
            { Collection: true } => Texts.PrivezTriJablka,
            _ => Texts.Deliver(DeliveryCargoInstruction(expedition.Cargo, expedition.Amount)),
        };
        return Texts.PleaseNow(task);
    }

    private static string DeliveryCargoInstruction(string cargo, int amount)
    {
        var (one, two, three) = cargo switch
        {
            "bread" => (Texts.JedenKusPeciva, Texts.TytoDvaKusyPeciva, Texts.TytoTriKusyPeciva),
            "parcel" => (Texts.JedenBalicek, Texts.TytoDvaBalicky, Texts.TytoTriBalicky),
            "cake" => (Texts.JedenKolacek, Texts.TytoDvaKolacky, Texts.TytoTriKolacky),
            "carrot" => (Texts.JednuMrkev, Texts.TytoDveMrkve, Texts.TytoTriMrkve),
            "apple" => (Texts.JednoJablko, Texts.TatoDveJablka, Texts.TatoTriJablka),
            _ => (Texts.JednuSkleniciMedu, Texts.TytoDveSkleniceMedu, Texts.TytoTriSkleniceMedu),
        };
        return amount switch
        {
            1 => one,
            2 => two,
            _ => three,
        };
    }
}
