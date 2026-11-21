using System.Collections.Generic;
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
            Say(new Utterance(text, czechVoices[0], 1, .9f), interrupt);
            return;
        }

        // Actors are ordered by stable world ID. The speaking bandit follows the registered actors.
        int speakerIndex = world.InteractionActors.TakeWhile(actor => actor != speaker).Count();
        string voice = czechVoices[speakerIndex % czechVoices.Length];
        int variation = speakerIndex / czechVoices.Length;
        float pitch = AnimalSpeechPitches[variation % AnimalSpeechPitches.Length];
        float rate = AnimalSpeechRates[variation / AnimalSpeechPitches.Length % AnimalSpeechRates.Length];
        Say(new Utterance(text, voice, pitch, rate), interrupt);
    }

    // Godot 4.7's Windows voice (OneCore) closes its player from the "finished" event on another thread, and a stop
    // closes it on the main thread; when both happen at once the two threads wait for each other and the game hangs
    // for good. So a sentence is cut off only while it is still far from its end; otherwise the new one waits for it.
    // This narrows the window a great deal but cannot close it: the true length of a sentence is not known.

    /// <summary>A sentence to be read, with the voice and how it is read.</summary>
    private readonly record struct Utterance(string Text, string Voice, float Pitch, float Rate);

    /// <summary>Characters per second of a fast reading: a length worked out from it is never longer than the real one.</summary>
    private const double FastCharactersPerSecond = 20;

    /// <summary>How long before the shortest likely end of a sentence it is no longer cut off.</summary>
    private const double CutMargin = .8;

    /// <summary>A sentence handed over a moment ago may not be reported as speaking yet.</summary>
    private const double HandOverTime = .3;

    private readonly List<Utterance> waitingSpeech = new();
    private double speechStarted = double.NegativeInfinity;
    private double speechShortestLength;

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private bool Speaking => DisplayServer.TtsIsSpeaking() || Now - speechStarted < HandOverTime;

    /// <summary>Whether the sentence being read is surely far enough from its end to be stopped.</summary>
    private bool SafeToCut => Now - speechStarted < speechShortestLength - CutMargin;

    private void Say(Utterance utterance, bool interrupt)
    {
        if (interrupt)
            waitingSpeech.Clear();
        if (!Speaking)
            Begin(utterance, cut: false);
        else if (interrupt && SafeToCut)
            Begin(utterance, cut: true);
        else
            waitingSpeech.Add(utterance);
    }

    private void Begin(Utterance utterance, bool cut)
    {
        DisplayServer.TtsSpeak(
            utterance.Text,
            utterance.Voice,
            pitch: utterance.Pitch,
            rate: utterance.Rate,
            interrupt: cut
        );
        speechStarted = Now;
        speechShortestLength = utterance.Text.Length / (FastCharactersPerSecond * utterance.Rate);
    }

    /// <summary>Reads the next waiting sentence once the one before has finished.</summary>
    private void TickSpeech()
    {
        if (waitingSpeech.Count == 0 || Speaking)
            return;
        var next = waitingSpeech[0];
        waitingSpeech.RemoveAt(0);
        Begin(next, cut: false);
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

    /// <summary>Stops the reading; a sentence close to its end is left to finish, which is safer than stopping it.</summary>
    private void StopSpeech()
    {
        waitingSpeech.Clear();
        if (czechVoices is { Length: > 0 } && Speaking && SafeToCut)
        {
            DisplayServer.TtsStop();
            speechStarted = double.NegativeInfinity;
        }
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
