using System.Linq;
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

    private void Speak(string text, Character? speaker)
    {
        if (!DisplayServer.HasFeature(DisplayServer.Feature.TextToSpeech))
            return;
        czechVoices ??= DisplayServer.TtsGetVoicesForLanguage("cs");
        if (czechVoices.Length == 0)
            return;

        if (speaker == null)
        {
            DisplayServer.TtsSpeak(text, czechVoices[0], rate: .9f, interrupt: true);
            return;
        }

        // Actors are ordered by stable world ID. The speaking bandit follows the registered actors.
        int speakerIndex = world.InteractionActors.TakeWhile(actor => actor != speaker).Count();
        string voice = czechVoices[speakerIndex % czechVoices.Length];
        int variation = speakerIndex / czechVoices.Length;
        float pitch = AnimalSpeechPitches[variation % AnimalSpeechPitches.Length];
        float rate = AnimalSpeechRates[variation / AnimalSpeechPitches.Length % AnimalSpeechRates.Length];
        DisplayServer.TtsSpeak(text, voice, pitch: pitch, rate: rate, interrupt: true);
    }

    private void StopSpeech()
    {
        if (czechVoices is { Length: > 0 })
            DisplayServer.TtsStop();
    }

    private static string QuizInstruction(Character actor, IChallenge question)
    {
        string instruction = question switch
        {
            SyllableChallenge => "nejprve vybrala slabiky",
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
            { Farm: true } => $"přivez {expedition.FarmName}",
            { Garden: true } => $"přivez {expedition.FlowerName}",
            { Gems: true } or { Toys: true } => $"přivez {expedition.CollectionName}",
            { Collection: true } => Texts.PrivezTriJablka,
            _ => $"rozvez {DeliveryCargoInstruction(expedition.Cargo, expedition.Amount)}",
        };
        return $"Teď prosím {task}.";
    }

    private static string DeliveryCargoInstruction(string cargo, int amount)
    {
        var (one, two, three) = cargo switch
        {
            "bread" => (Texts.JedenKusPeciva, Texts.TytoDvaKusyPeciva, Texts.TytoTriKusyPeciva),
            "parcel" => (Texts.JedenBalicek, Texts.TytoDvaBalicky, Texts.TytoTriBalicky),
            "cake" => (Texts.JedenKolacek, Texts.TytoDvaKolacky, Texts.TytoTriKolacky),
            "carrot" => ("jednu mrkev", Texts.TytoDveMrkve, Texts.TytoTriMrkve),
            "apple" => ("jedno jablko", Texts.TatoDveJablka, Texts.TatoTriJablka),
            _ => ("jednu sklenici medu", Texts.TytoDveSkleniceMedu, Texts.TytoTriSkleniceMedu),
        };
        return amount switch
        {
            1 => one,
            2 => two,
            _ => three,
        };
    }
}
